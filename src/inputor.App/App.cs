using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Inputor.App.Models;
using Inputor.App.Services;
using Inputor.App.Views;

namespace Inputor.App;

public sealed class App : Application
{
    private MonitoringService? _monitoringService;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIcon? _trayIcon;
    private bool _exitRequested;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "inputor");
            Directory.CreateDirectory(dataDirectory);

            var settingsService = new AppSettingsService(dataDirectory);
            var settings = settingsService.Load();
            var statsStore = new StatsStore(dataDirectory);
            var exporter = new CsvExportService();
            var autoStartService = new AutoStartService();
            autoStartService.Apply(settings.StartWithWindows);

            _mainWindow = new MainWindow(statsStore, settings, exporter, ShowSettingsWindow)
            {
                Icon = LoadIcon(),
                Title = "inputor"
            };
            _mainWindow.RequestExit += (_, _) => ExitApplication(desktop);
            _mainWindow.Closing += OnMainWindowClosing;

            _settingsWindow = new SettingsWindow(settings)
            {
                Icon = LoadIcon(),
                Title = "inputor Settings"
            };
            _settingsWindow.SettingsSaved += (_, updatedSettings) =>
            {
                settingsService.Save(updatedSettings);
                autoStartService.Apply(updatedSettings.StartWithWindows);
                statsStore.SetStatus(
                    "Settings updated.",
                    statsStore.CurrentAppName,
                    statsStore.IsCurrentTargetSupported);
            };

            _trayIcon = CreateTrayIcon(desktop, exporter, statsStore);

            desktop.MainWindow = _mainWindow;
            desktop.Exit += (_, _) =>
            {
                _trayIcon?.Dispose();
                _monitoringService?.Dispose();
                statsStore.Dispose();
            };

            _monitoringService = new MonitoringService(statsStore, settings);
            _monitoringService.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowDashboardWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _exitRequested = true;
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }

    private TrayIcon CreateTrayIcon(
        IClassicDesktopStyleApplicationLifetime desktop,
        CsvExportService exporter,
        StatsStore statsStore)
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Show Dashboard");
        showItem.Click += (_, _) => ShowDashboardWindow();
        menu.Add(showItem);

        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        menu.Add(settingsItem);

        var exportItem = new NativeMenuItem("Export Today CSV");
        exportItem.Click += (_, _) =>
        {
            var path = exporter.ExportToday(statsStore.GetSnapshot());
            statsStore.SetStatus($"Exported CSV to {path}", statsStore.CurrentAppName, statsStore.IsCurrentTargetSupported);
            ShowDashboardWindow();
        };
        menu.Add(exportItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication(desktop);
        menu.Add(exitItem);

        var trayIcon = new TrayIcon
        {
            ToolTipText = "inputor",
            IsVisible = true,
            Menu = menu,
            Icon = LoadIcon()
        };

        trayIcon.Clicked += (_, _) => ShowDashboardWindow();
        return trayIcon;
    }

    private WindowIcon LoadIcon()
    {
        using var iconStream = AssetLoader.Open(new Uri("avares://inputor.App/Assets/inputor.ico"));
        return new WindowIcon(iconStream);
    }
}
