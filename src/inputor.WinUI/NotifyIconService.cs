using System;
using System.Drawing;
using System.IO;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace Inputor.WinUI;

internal sealed class NotifyIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseItem;

    public NotifyIconService()
    {
        _pauseItem = new ToolStripMenuItem("Pause Monitoring", null, (_, _) => App.Current.TogglePauseMonitoring());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Show Dashboard", null, (_, _) => App.Current.ShowMainWindow()));
        contextMenu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => App.Current.ShowSettingsWindow()));
        contextMenu.Items.Add(new ToolStripMenuItem("Export Today CSV", null, (_, _) => App.Current.ExportToday()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_pauseItem);
        contextMenu.Items.Add(new ToolStripMenuItem("Reset Session", null, (_, _) => App.Current.ResetSession()));
        contextMenu.Items.Add(new ToolStripMenuItem("Exclude Current App", null, (_, _) => App.Current.ExcludeCurrentApp()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => App.Current.ExitApplication()));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "inputor.ico");
        _notifyIcon = new NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = "inputor",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => App.Current.ShowMainWindow();

        App.Current.StatsStore.Changed += (_, _) => UpdateState();
        UpdateState();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void UpdateState()
    {
        var snapshot = App.Current.StatsStore.GetSnapshot();
        _notifyIcon.Text = TrimToolTip($"inputor | Today {snapshot.TotalToday:N0} | Session {snapshot.TotalSession:N0} | {(snapshot.IsPaused ? "Paused" : snapshot.CurrentAppName)}");
        _pauseItem.Text = snapshot.IsPaused ? "Resume Monitoring" : "Pause Monitoring";
    }

    private static string TrimToolTip(string value)
    {
        return value.Length <= 63 ? value : value[..63];
    }
}
