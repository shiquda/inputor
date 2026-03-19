using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Inputor.App.Models;

namespace Inputor.App.Services;

public sealed class MonitoringService : IDisposable
{
    private readonly StatsStore _statsStore;
    private readonly AppSettings _settings;
    private readonly ClipboardTextService _clipboardTextService = new();
    private readonly CompositionAwareDeltaTracker _deltaTracker = new();
    private readonly CancellationTokenSource _cts = new();
    private Thread? _workerThread;
    private bool _isPaused;

    public MonitoringService(StatsStore statsStore, AppSettings settings)
    {
        _statsStore = statsStore;
        _settings = settings;
        _statsStore.SetAdminReminderVisible(_settings.ShowAdminReminder);
    }

    public bool IsPaused => _isPaused;

    public void Start()
    {
        if (_workerThread is not null)
        {
            return;
        }

        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "inputor-monitor"
        };
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.Start();
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        _statsStore.SetPaused(_isPaused);
        _statsStore.SetStatus(_isPaused ? "Monitoring paused." : "Monitoring resumed.", _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _workerThread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    private void WorkerLoop()
    {
        using var automation = new UIA3Automation();

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_isPaused)
                {
                    Sleep();
                    continue;
                }

                PollForeground(automation);
            }
            catch (Exception ex)
            {
                var statusMessage = $"Monitoring error: {ex.Message}";
                _statsStore.SetStatus(statusMessage, "Unavailable", false);
                RecordDebugEvent("Unavailable", statusMessage, string.Empty, 0, null, false, false, false, false);
                _deltaTracker.Reset();
            }

            Sleep();
        }
    }

    private void PollForeground(UIA3Automation automation)
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            const string statusMessage = "No foreground window.";
            _statsStore.SetStatus(statusMessage, "Idle", false);
            RecordDebugEvent("Idle", statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == 0)
        {
            const string statusMessage = "Unable to resolve the active process.";
            _statsStore.SetStatus(statusMessage, "Unavailable", false);
            RecordDebugEvent("Unavailable", statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var processName = GetProcessName(processId);
        if (string.IsNullOrWhiteSpace(processName))
        {
            const string statusMessage = "Unable to resolve the active process name.";
            _statsStore.SetStatus(statusMessage, "Unavailable", false);
            RecordDebugEvent("Unavailable", statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (_settings.IsExcluded(processName))
        {
            var statusMessage = $"{processName} is excluded.";
            _statsStore.SetStatus(statusMessage, processName, false);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (string.Equals(processName, "inputor.App", StringComparison.OrdinalIgnoreCase))
        {
            const string statusMessage = "inputor window is active; monitoring external apps only.";
            _statsStore.SetStatus(statusMessage, processName, false);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var focusedElement = automation.FocusedElement();
        if (focusedElement is null)
        {
            var statusMessage = $"{processName}: no focused element.";
            _statsStore.SetStatus(statusMessage, processName, false);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (focusedElement.Properties.IsPassword.ValueOrDefault)
        {
            var statusMessage = $"{processName}: password field skipped.";
            _statsStore.SetStatus(statusMessage, processName, false);
            RecordDebugEvent(processName, statusMessage, focusedElement.Properties.ControlType.ValueOrDefault.ToString(), 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var text = TryReadText(focusedElement);
        if (text is null)
        {
            var statusMessage = $"{processName}: focused control does not expose readable text.";
            _statsStore.SetStatus(statusMessage, processName, false);
            RecordDebugEvent(processName, statusMessage, focusedElement.Properties.ControlType.ValueOrDefault.ToString(), 0, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var snapshotKey = BuildSnapshotKey(processName, focusedElement);
        var isNativeImeInputMode = IsNativeChineseImeInputMode(foregroundWindow);
        var result = _deltaTracker.ProcessSnapshot(snapshotKey, text, DateTime.UtcNow, isNativeImeInputMode);
        var clipboardText = result.Delta > 0 ? _clipboardTextService.TryGetText() : null;
        var controlTypeName = focusedElement.Properties.ControlType.ValueOrDefault.ToString();

        if (result.Delta > 0)
        {
            var isPaste = PasteDetectionService.LooksLikePaste(result.InsertedTextSegment, clipboardText);
            if (isPaste)
            {
                var statusMessage = $"Monitoring {processName}; pasted text excluded by default.";
                _statsStore.SetStatus(statusMessage, processName, true);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.IsPendingComposition, true, false, isNativeImeInputMode);
            }
            else if (BulkLoadDetectionService.LooksLikeBulkContentLoad(result.Delta, result.InsertedTextSegment, controlTypeName, isPaste))
            {
                var statusMessage = $"Monitoring {processName}; ignored suspicious bulk content refresh.";
                _statsStore.SetStatus(statusMessage, processName, true);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.IsPendingComposition, false, true, isNativeImeInputMode);
            }
            else
            {
                _statsStore.RecordDelta(processName, result.Delta);
                var statusMessage = $"Recorded +{result.Delta} supported characters in {processName}.";
                _statsStore.SetStatus(statusMessage, processName, true);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.IsPendingComposition, false, false, isNativeImeInputMode);
            }
        }
        else if (result.IsPendingComposition)
        {
            var statusMessage = $"Monitoring {processName}; waiting for composition confirmation.";
            _statsStore.SetStatus(statusMessage, processName, true);
            RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, true, false, false, isNativeImeInputMode);
        }
        else
        {
            var statusMessage = $"Monitoring {processName}; no positive delta detected.";
            _statsStore.SetStatus(statusMessage, processName, true);
            RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, false, false, false, isNativeImeInputMode);
        }
    }

    private void RecordDebugEvent(
        string appName,
        string statusMessage,
        string controlTypeName,
        int delta,
        string? insertedTextSegment,
        bool isPendingComposition,
        bool isPaste,
        bool isBulkContentLoad,
        bool isNativeImeInputMode)
    {
        var segment = insertedTextSegment ?? string.Empty;
        _statsStore.AddDebugEvent(new DebugEventEntry
        {
            Timestamp = DateTime.Now,
            AppName = appName,
            StatusMessage = statusMessage,
            ControlTypeName = controlTypeName,
            Delta = delta,
            InsertedSegmentLength = segment.Length,
            InsertedSupportedCharacterCount = CharacterCountService.CountSupportedCharacters(segment),
            InsertedChineseCharacterCount = CharacterCountService.CountChineseCharacters(segment),
            InsertedEnglishLetterCount = CharacterCountService.CountEnglishLetters(segment),
            IsPendingComposition = isPendingComposition,
            IsPaste = isPaste,
            IsBulkContentLoad = isBulkContentLoad,
            IsNativeImeInputMode = isNativeImeInputMode,
            IsCurrentTargetSupported = _statsStore.IsCurrentTargetSupported
        });
    }

    private static string? TryReadText(AutomationElement element)
    {
        if (element.Patterns.Text.IsSupported)
        {
            return element.Patterns.Text.Pattern.DocumentRange.GetText(int.MaxValue)
                ?.Replace("\0", string.Empty, StringComparison.Ordinal);
        }

        if (element.Patterns.Value.IsSupported)
        {
            return element.Patterns.Value.Pattern.Value.ValueOrDefault
                ?.Replace("\0", string.Empty, StringComparison.Ordinal);
        }

        return null;
    }

    private static string BuildSnapshotKey(string processName, AutomationElement element)
    {
        var automationId = element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        var className = element.Properties.ClassName.ValueOrDefault ?? string.Empty;
        var name = element.Properties.Name.ValueOrDefault ?? string.Empty;
        var controlType = element.Properties.ControlType.ValueOrDefault.ToString();
        return $"{processName}|{automationId}|{className}|{name}|{controlType}";
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsNativeChineseImeInputMode(IntPtr foregroundWindow)
    {
        const int ChinesePrimaryLanguageId = 0x04;
        const int PrimaryLanguageMask = 0x03ff;
        const int ImeCmodeNative = 0x0001;

        try
        {
            var windowThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            var keyboardLayout = GetKeyboardLayout(windowThreadId).ToInt64();
            var languageId = unchecked((ushort)(keyboardLayout & 0xffff));
            if ((languageId & PrimaryLanguageMask) != ChinesePrimaryLanguageId)
            {
                return false;
            }

            var inputContext = ImmGetContext(foregroundWindow);
            if (inputContext == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (!ImmGetOpenStatus(inputContext))
                {
                    return false;
                }

                return ImmGetConversionStatus(inputContext, out var conversion, out _)
                    && (conversion & ImeCmodeNative) != 0;
            }
            finally
            {
                _ = ImmReleaseContext(foregroundWindow, inputContext);
            }
        }
        catch
        {
            return false;
        }
    }

    private void Sleep()
    {
        _cts.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmGetConversionStatus(IntPtr hIMC, out int conversion, out int sentence);

    [DllImport("imm32.dll")]
    private static extern bool ImmGetOpenStatus(IntPtr hIMC);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
}
