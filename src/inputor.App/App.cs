using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using Inputor.App.Models;
using Inputor.App.Services;
using Inputor.App.Views;

namespace Inputor.App;

public sealed class App : Application
{
    private AppSettings? _settings;
    private AppSettingsService? _settingsService;
    private StatsStore? _statsStore;
    private CsvExportService? _exporter;
    private AutoStartService? _autoStartService;
    private MonitoringService? _monitoringService;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIcon? _trayIcon;
    private bool _exitRequested;

    public override void Initialize()
    {
        Styles.Add(new FluentAvaloniaTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "inputor");
            Directory.CreateDirectory(dataDirectory);

            _settingsService = new AppSettingsService(dataDirectory);
            _settings = _settingsService.Load();
            _statsStore = new StatsStore(dataDirectory);
            _exporter = new CsvExportService();
            _autoStartService = new AutoStartService();
            _autoStartService.Apply(_settings.StartWithWindows);

            _mainWindow = new MainWindow(_statsStore, _settings, _exporter, ShowSettingsWindow, TogglePauseMonitoring, ResetSession, ExcludeCurrentApp)
            {
                Icon = LoadIcon(),
                Title = "inputor"
            };
            _mainWindow.Closing += OnMainWindowClosing;

            _settingsWindow = new SettingsWindow(_settings, _statsStore)
            {
                Icon = LoadIcon(),
                Title = "inputor Settings"
            };
            _settingsWindow.SettingsSaved += (_, updatedSettings) =>
            {
                _settingsService?.Save(updatedSettings);
                _autoStartService?.Apply(updatedSettings.StartWithWindows);
                _statsStore?.SetStatus(
                    "Settings updated.",
                    _statsStore.CurrentAppName,
                    _statsStore.IsCurrentTargetSupported);
                UpdateTrayToolTip();
            };

            _trayIcon = CreateTrayIcon(desktop);
            _statsStore.Changed += (_, _) => Dispatcher.UIThread.Post(UpdateTrayToolTip);
            UpdateTrayToolTip();

            desktop.MainWindow = _mainWindow;
            desktop.Exit += (_, _) =>
            {
                _trayIcon?.Dispose();
                _monitoringService?.Dispose();
                _statsStore?.Dispose();
            };

            _monitoringService = new MonitoringService(_statsStore, _settings);
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

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Show Dashboard");
        showItem.Click += (_, _) => ShowDashboardWindow();
        menu.Add(showItem);

        var pauseItem = new NativeMenuItem("Pause/Resume Monitoring");
        pauseItem.Click += (_, _) => TogglePauseMonitoring();
        menu.Add(pauseItem);

        var resetSessionItem = new NativeMenuItem("Reset Session");
        resetSessionItem.Click += (_, _) => ResetSession();
        menu.Add(resetSessionItem);

        var excludeCurrentItem = new NativeMenuItem("Exclude Current App");
        excludeCurrentItem.Click += (_, _) => ExcludeCurrentApp();
        menu.Add(excludeCurrentItem);

        var settingsItem = new NativeMenuItem("Settings");
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        menu.Add(settingsItem);

        var exportItem = new NativeMenuItem("Export Today CSV");
        exportItem.Click += (_, _) =>
        {
            if (_exporter is null || _statsStore is null)
            {
                return;
            }

            var path = _exporter.ExportToday(_statsStore.GetSnapshot());
            _statsStore.SetStatus($"Exported CSV to {path}", _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported);
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

    private void TogglePauseMonitoring()
    {
        _monitoringService?.TogglePause();
        UpdateTrayToolTip();
    }

    private void ResetSession()
    {
        if (_statsStore is null)
        {
            return;
        }

        _statsStore.ResetSession();
        _statsStore.SetStatus("Session counters reset.", _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported);
        UpdateTrayToolTip();
    }

    private void ExcludeCurrentApp()
    {
        if (_settings is null || _settingsService is null || _statsStore is null)
        {
            return;
        }

        var processName = _statsStore.CurrentAppName;
        if (!CanExclude(processName))
        {
            _statsStore.SetStatus("No active app is available for exclusion right now.", processName, false);
            return;
        }

        if (!_settings.AddExcludedApp(processName))
        {
            _statsStore.SetStatus($"{processName} is already excluded.", processName, false);
            return;
        }

        _settingsService.Save(_settings);
        _settingsWindow?.ReloadFromSettings();
        _statsStore.SetStatus($"Added {processName} to excluded apps.", processName, false);
    }

    private void UpdateTrayToolTip()
    {
        if (_trayIcon is null || _statsStore is null)
        {
            return;
        }

        var snapshot = _statsStore.GetSnapshot();
        var stateText = snapshot.IsPaused ? "Paused" : snapshot.CurrentAppName;
        _trayIcon.ToolTipText = $"inputor | Today {snapshot.TotalToday:N0} | Session {snapshot.TotalSession:N0} | {stateText}";
    }

    private static bool CanExclude(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName)
            && !string.Equals(processName, "Idle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(processName, "Unavailable", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(processName, "inputor.App", StringComparison.OrdinalIgnoreCase);
    }

    private WindowIcon LoadIcon()
    {
        using var iconStream = AssetLoader.Open(new Uri("avares://inputor.App/Assets/inputor.ico"));
        return new WindowIcon(iconStream);
    }
}
