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
                _statsStore.SetStatus($"Monitoring error: {ex.Message}", "Unavailable", false);
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
            _statsStore.SetStatus("No foreground window.", "Idle", false);
            _deltaTracker.Reset();
            return;
        }

        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == 0)
        {
            _statsStore.SetStatus("Unable to resolve the active process.", "Unavailable", false);
            _deltaTracker.Reset();
            return;
        }

        var processName = GetProcessName(processId);
        if (string.IsNullOrWhiteSpace(processName))
        {
            _statsStore.SetStatus("Unable to resolve the active process name.", "Unavailable", false);
            _deltaTracker.Reset();
            return;
        }

        if (_settings.IsExcluded(processName))
        {
            _statsStore.SetStatus($"{processName} is excluded.", processName, false);
            _deltaTracker.Reset();
            return;
        }

        if (string.Equals(processName, "inputor.App", StringComparison.OrdinalIgnoreCase))
        {
            _statsStore.SetStatus("inputor window is active; monitoring external apps only.", processName, false);
            _deltaTracker.Reset();
            return;
        }

        var focusedElement = automation.FocusedElement();
        if (focusedElement is null)
        {
            _statsStore.SetStatus($"{processName}: no focused element.", processName, false);
            _deltaTracker.Reset();
            return;
        }

        if (focusedElement.Properties.IsPassword.ValueOrDefault)
        {
            _statsStore.SetStatus($"{processName}: password field skipped.", processName, false);
            _deltaTracker.Reset();
            return;
        }

        var text = TryReadText(focusedElement);
        if (text is null)
        {
            _statsStore.SetStatus($"{processName}: focused control does not expose readable text.", processName, false);
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
                _statsStore.SetStatus($"Monitoring {processName}; pasted text excluded by default.", processName, true);
            }
            else if (BulkLoadDetectionService.LooksLikeBulkContentLoad(result.Delta, result.InsertedTextSegment, controlTypeName, isPaste))
            {
                _statsStore.SetStatus($"Monitoring {processName}; ignored suspicious bulk content refresh.", processName, true);
            }
            else
            {
                _statsStore.RecordDelta(processName, result.Delta);
                _statsStore.SetStatus($"Recorded +{result.Delta} supported characters in {processName}.", processName, true);
            }
        }
        else if (result.IsPendingComposition)
        {
            _statsStore.SetStatus($"Monitoring {processName}; waiting for composition confirmation.", processName, true);
        }
        else
        {
            _statsStore.SetStatus($"Monitoring {processName}; no positive delta detected.", processName, true);
        }
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
