using System;
using System.Drawing;
using System.IO;
using Microsoft.UI.Dispatching;
using Control = System.Windows.Forms.Control;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using MouseButtons = System.Windows.Forms.MouseButtons;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace Inputor.WinUI;

internal sealed class NotifyIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isDisposed;

    public NotifyIconService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "inputor.ico");
        _notifyIcon = new NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = AppStrings.Get("App.Name"),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => App.Current.ShowMainWindow();
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;

        App.Current.StatsStore.Changed += StatsStore_Changed;
        UpdateState();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        App.Current.StatsStore.Changed -= StatsStore_Changed;
        _notifyIcon.MouseUp -= NotifyIcon_MouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void StatsStore_Changed(object? sender, EventArgs e)
    {
        QueueUpdateState();
    }

    private void UpdateState()
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshot = App.Current.StatsStore.GetSnapshot();
        _notifyIcon.Text = TrimToolTip(AppStrings.Format(
            "NotifyIcon.Tooltip",
            AppStrings.Get("App.Name"),
            snapshot.TotalToday,
            snapshot.TotalSession,
            snapshot.IsPaused ? AppStrings.Get("Common.Paused") : snapshot.CurrentAppName));
    }

    private void NotifyIcon_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        StartupDiagnostics.Log($"NotifyIcon mouse up: {e.Button}");

        if (e.Button == MouseButtons.Right)
        {
            App.Current.ShowTrayMenu(Control.MousePosition.X, Control.MousePosition.Y);
        }
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

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            UpdateState();
        });
    }

    private static string TrimToolTip(string value)
    {
        return value.Length <= 63 ? value : value[..63];
    }
}
