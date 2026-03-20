using Microsoft.UI.Dispatching;

namespace Inputor.WinUI;

internal sealed class NotifyIconService : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TrayHostWindow _trayHostWindow;
    private bool _isDisposed;

    public NotifyIconService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _trayHostWindow = new TrayHostWindow();

        App.Current.StatsStore.Changed += StatsStore_Changed;
        UpdateState();
        _trayHostWindow.Initialize();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        App.Current.StatsStore.Changed -= StatsStore_Changed;
        _trayHostWindow.Dispose();
    }

    private void StatsStore_Changed(object? sender, EventArgs e)
    {
        QueueUpdateState();
    }

    private void QueueUpdateState()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            UpdateState();
            return;
        }

        _dispatcherQueue.TryEnqueue(UpdateState);
    }

    private void UpdateState()
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshot = App.Current.StatsStore.GetSnapshot();
        var toolTip = TrimToolTip(AppStrings.Format(
            "NotifyIcon.Tooltip",
            AppStrings.Get("App.Name"),
            snapshot.TotalToday,
            snapshot.TotalSession,
            snapshot.IsPaused ? AppStrings.Get("Common.Paused") : snapshot.CurrentAppName));
        var pauseText = snapshot.IsPaused ? AppStrings.Get("Main.Button.ResumeMonitoring") : AppStrings.Get("Main.Button.PauseMonitoring");
        var pauseGlyph = snapshot.IsPaused ? "\uF5B0" : "\uE769";

        _trayHostWindow.UpdateState(toolTip, pauseText, pauseGlyph);
    }

    private static string TrimToolTip(string value)
    {
        return value.Length <= 63 ? value : value[..63];
    }
}
