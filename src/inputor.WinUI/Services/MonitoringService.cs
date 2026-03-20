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
    private bool _hasLoggedFirstPoll;

    public MonitoringService(StatsStore statsStore, AppSettings settings)
    {
        _statsStore = statsStore;
        _settings = settings;
    }

    public bool IsPaused => _isPaused;

    public bool IsStarted => _workerThread is not null;

    public void Start()
    {
        if (_workerThread is not null)
        {
            return;
        }

        global::Inputor.WinUI.StartupDiagnostics.Log("MonitoringService.Start invoked.");
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "inputor-monitor"
        };
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.Start();
        global::Inputor.WinUI.StartupDiagnostics.Log("MonitoringService worker thread started.");
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        _statsStore.SetPaused(_isPaused);
        _statsStore.SetStatus(StatusText.MonitoringPauseChanged(_isPaused), _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported, _statsStore.CurrentProcessName);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _workerThread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    private void WorkerLoop()
    {
        global::Inputor.WinUI.StartupDiagnostics.Log("MonitoringService.WorkerLoop entered.");
        global::Inputor.WinUI.StartupDiagnostics.Log("MonitoringService about to create UIA3Automation.");
        using var automation = new UIA3Automation();
        global::Inputor.WinUI.StartupDiagnostics.Log("MonitoringService created UIA3Automation successfully.");

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
                var statusMessage = StatusText.MonitoringError(ex.Message);
                var displayName = StatusText.UnavailableDisplayName();
                _statsStore.SetStatus(statusMessage, displayName, false);
                RecordDebugEvent(displayName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
                _deltaTracker.Reset();
            }

            Sleep();
        }
    }

    private void PollForeground(UIA3Automation automation)
    {
        LogFirstPoll("PollForeground entered.");
        var foregroundWindow = GetForegroundWindow();
        LogFirstPoll($"GetForegroundWindow returned {(foregroundWindow == IntPtr.Zero ? "zero" : foregroundWindow.ToString())}.");
        if (foregroundWindow == IntPtr.Zero)
        {
            var statusMessage = StatusText.NoForegroundWindow();
            var displayName = StatusText.IdleDisplayName();
            _statsStore.SetStatus(statusMessage, displayName, false);
            RecordDebugEvent(displayName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        LogFirstPoll($"GetWindowThreadProcessId returned {processId}.");
        if (processId == 0)
        {
            var statusMessage = StatusText.UnableToResolveActiveProcess();
            var displayName = StatusText.UnavailableDisplayName();
            _statsStore.SetStatus(statusMessage, displayName, false);
            RecordDebugEvent(displayName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var processName = GetProcessName(processId);
        LogFirstPoll($"Resolved process name '{processName}'.");
        if (string.IsNullOrWhiteSpace(processName))
        {
            var statusMessage = StatusText.UnableToResolveActiveProcessName();
            var displayName = StatusText.UnavailableDisplayName();
            _statsStore.SetStatus(statusMessage, displayName, false);
            RecordDebugEvent(displayName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (_settings.IsExcluded(processName))
        {
            var statusMessage = StatusText.ProcessExcluded(processName);
            _statsStore.SetStatus(statusMessage, processName, false, processName);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (string.Equals(processName, "inputor.App", StringComparison.OrdinalIgnoreCase))
        {
            var statusMessage = StatusText.SelfWindowActive();
            _statsStore.SetStatus(statusMessage, processName, false, processName);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var focusedElement = automation.FocusedElement();
        LogFirstPoll($"FocusedElement is {(focusedElement is null ? "null" : "present")}.");
        if (focusedElement is null)
        {
            var statusMessage = StatusText.NoFocusedElement(processName);
            _statsStore.SetStatus(statusMessage, processName, false, processName);
            RecordDebugEvent(processName, statusMessage, string.Empty, 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        if (focusedElement.Properties.IsPassword.ValueOrDefault)
        {
            var statusMessage = StatusText.PasswordFieldSkipped(processName);
            _statsStore.SetStatus(statusMessage, processName, false, processName);
            RecordDebugEvent(processName, statusMessage, focusedElement.Properties.ControlType.ValueOrDefault.ToString(), 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var text = TryReadText(focusedElement);
        LogFirstPoll($"TryReadText returned {(text is null ? "null" : $"length {text.Length}")}.");
        if (text is null)
        {
            var statusMessage = StatusText.FocusedControlUnreadable(processName);
            _statsStore.SetStatus(statusMessage, processName, false, processName);
            RecordDebugEvent(processName, statusMessage, focusedElement.Properties.ControlType.ValueOrDefault.ToString(), 0, null, null, false, false, false, false);
            _deltaTracker.Reset();
            return;
        }

        var snapshotKey = BuildSnapshotKey(processName, focusedElement);
        var isNativeImeInputMode = IsNativeChineseImeInputMode(foregroundWindow);
        var result = _deltaTracker.ProcessSnapshot(snapshotKey, text, DateTime.UtcNow, isNativeImeInputMode, _settings.DebugCaptureEnabled);
        LogFirstPoll($"Delta tracker produced delta {result.Delta}, pending={result.IsPendingComposition}.");
        var clipboardText = result.Delta > 0 ? _clipboardTextService.TryGetText() : null;
        var controlTypeName = focusedElement.Properties.ControlType.ValueOrDefault.ToString();
        LogFirstPoll($"ControlType '{controlTypeName}'.");

        if (result.Delta > 0)
        {
            var isPaste = PasteDetectionService.LooksLikePaste(result.InsertedTextSegment, clipboardText);
            if (isPaste)
            {
                var statusMessage = StatusText.PasteExcluded(processName);
                _statsStore.SetStatus(statusMessage, processName, true, processName);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.TextComparison, result.IsPendingComposition, true, false, isNativeImeInputMode);
            }
            else if (BulkLoadDetectionService.LooksLikeBulkContentLoad(result.Delta, result.InsertedTextSegment, controlTypeName, isPaste))
            {
                var statusMessage = StatusText.BulkRefreshIgnored(processName);
                _statsStore.SetStatus(statusMessage, processName, true, processName);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.TextComparison, result.IsPendingComposition, false, true, isNativeImeInputMode);
            }
            else
            {
                _statsStore.RecordDelta(processName, result.Delta);
                var statusMessage = StatusText.RecordedSupportedCharacters(result.Delta, processName);
                _statsStore.SetStatus(statusMessage, processName, true, processName);
                RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.TextComparison, result.IsPendingComposition, false, false, isNativeImeInputMode);
            }
        }
        else if (result.IsPendingComposition)
        {
            var statusMessage = StatusText.WaitingForComposition(processName);
            _statsStore.SetStatus(statusMessage, processName, true, processName);
            RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.TextComparison, true, false, false, isNativeImeInputMode);
        }
        else
        {
            var statusMessage = StatusText.NoPositiveDelta(processName);
            _statsStore.SetStatus(statusMessage, processName, true, processName);
            RecordDebugEvent(processName, statusMessage, controlTypeName, result.Delta, result.InsertedTextSegment, result.TextComparison, false, false, false, isNativeImeInputMode);
        }
    }

    private void LogFirstPoll(string message)
    {
        if (_hasLoggedFirstPoll)
        {
            return;
        }

        global::Inputor.WinUI.StartupDiagnostics.Log($"First poll: {message}");
        if (message.Contains("ControlType", StringComparison.Ordinal)
            || message.Contains("Delta tracker", StringComparison.Ordinal)
            || message.Contains("TryReadText", StringComparison.Ordinal)
            || message.Contains("FocusedElement", StringComparison.Ordinal))
        {
            _hasLoggedFirstPoll = true;
        }
    }

    private void RecordDebugEvent(
        string appName,
        string statusMessage,
        string controlTypeName,
        int delta,
        string? insertedTextSegment,
        DebugTextComparison? textComparison,
        bool isPendingComposition,
        bool isPaste,
        bool isBulkContentLoad,
        bool isNativeImeInputMode)
    {
        if (!_settings.DebugCaptureEnabled)
        {
            return;
        }

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
            InsertedOtherSupportedCharacterCount = CharacterCountService.CountOtherSupportedCharacters(segment),
            IsPendingComposition = isPendingComposition,
            IsPaste = isPaste,
            IsBulkContentLoad = isBulkContentLoad,
            IsNativeImeInputMode = isNativeImeInputMode,
            IsCurrentTargetSupported = _statsStore.IsCurrentTargetSupported,
            TextComparison = textComparison
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
