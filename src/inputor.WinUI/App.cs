using System;
using System.Diagnostics;
using System.IO;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;
using Windows.UI.ViewManagement;

namespace Inputor.WinUI;

public sealed class App : Application, IXamlMetadataProvider
{
    private readonly string _dataDirectory;
    private bool _exitRequested;
    private bool _shutdownCompleted;
    private readonly UISettings _uiSettings;
    private NotifyIconService? _notifyIconService;
    private TrayMenuWindow? _trayMenuWindow;
    private XamlControlsXamlMetaDataProvider? _metadataProvider;

    public App()
    {
        StartupDiagnostics.Log("App constructor entered.");
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "inputor");
        Directory.CreateDirectory(_dataDirectory);

        SettingsService = new AppSettingsService(_dataDirectory);
        Settings = SettingsService.Load();
        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        AppStrings.Initialize(AppContext.BaseDirectory, Settings.Language);
        try
        {
            StatsStore = new StatsStore(_dataDirectory, Settings.StatisticsSourcePath, strictSourceValidation: !string.IsNullOrWhiteSpace(Settings.StatisticsSourcePath));
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"StatsStore custom source initialization failed; falling back to default source. {exception}");
            Settings.StatisticsSourcePath = string.Empty;
            SettingsService.Save(Settings);
            StatsStore = new StatsStore(_dataDirectory);
            StatsStore.SetStatus(StatusText.StatisticsSourceFallbackToDefault(), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        }
        Exporter = new CsvExportService();
        AutoStartService = new AutoStartService();
        AutoStartService.Apply(Settings.StartWithWindows);
        MonitoringService = new MonitoringService(StatsStore, Settings);
        StatsStore.SetDebugCaptureEnabled(Settings.DebugCaptureEnabled);
        UnhandledException += (_, args) =>
        {
            StartupDiagnostics.Log($"App.UnhandledException: {args.Exception}");
        };
        StartupDiagnostics.Log("App constructor completed.");
    }

    public static new App Current => (App)Application.Current;

    public AppSettings Settings { get; }

    public AppSettingsService SettingsService { get; }

    public StatsStore StatsStore { get; }

    public CsvExportService Exporter { get; }

    public AutoStartService AutoStartService { get; }

    public MonitoringService MonitoringService { get; }

    public MainWindow? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Log("App.OnLaunched entered.");
        XamlControlsXamlMetaDataProvider.Initialize();
        _metadataProvider ??= new XamlControlsXamlMetaDataProvider();
        Resources ??= new ResourceDictionary();
        Resources.MergedDictionaries.Add(new XamlControlsResources());

        MainWindow = new MainWindow();
        ApplyThemeMode();
        StartupDiagnostics.Log("MainWindow created.");
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "inputor.ico");
        WindowHelpers.SetWindowIcon(MainWindow, iconPath);
        StartupDiagnostics.Log($"Window icon path {(File.Exists(iconPath) ? "applied" : "missing")}: {iconPath}");
        MainWindow.Activate();
        StartupDiagnostics.Log("MainWindow activated.");
        MainWindow.Closed += MainWindow_Closed;

        try
        {
            _notifyIconService = new NotifyIconService();
            StartupDiagnostics.Log("NotifyIconService initialized successfully.");
            WindowHelpers.RegisterHideOnClose(MainWindow, () => !_exitRequested && _notifyIconService is not null);
            StartupDiagnostics.Log("Hide-on-close registered because tray is available.");
        }
        catch (Exception exception)
        {
            _notifyIconService = null;
            StartupDiagnostics.Log($"NotifyIconService initialization failed: {exception}");
        }

        MonitoringService.Start();
        StartupDiagnostics.Log("MonitoringService auto-start re-enabled after refresh crash fix.");
        StatsStore.SetStatus(StatusText.MonitoringStarted(), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        base.OnLaunched(args);
        StartupDiagnostics.Log("App.OnLaunched completed.");
    }

    public void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        _trayMenuWindow?.HideMenu();
        WindowHelpers.ShowWindow(MainWindow);
    }

    public void ShowSettingsPage()
    {
        if (MainWindow is null)
        {
            return;
        }

        _trayMenuWindow?.HideMenu();
        WindowHelpers.ShowWindow(MainWindow);
        MainWindow.ShowSettingsPage();
    }

    public void ShowTrayMenu(int cursorX, int cursorY)
    {
        StartupDiagnostics.Log($"ShowTrayMenu requested at {cursorX},{cursorY}, but tray menu is handled by NotifyIconService.");
    }

    public void HideTrayMenu()
    {
        _trayMenuWindow?.HideMenu();
    }

    internal void OnTrayMenuClosed(TrayMenuWindow trayMenuWindow)
    {
        if (ReferenceEquals(_trayMenuWindow, trayMenuWindow))
        {
            _trayMenuWindow = null;
        }
    }

    public void TogglePauseMonitoring()
    {
        MonitoringService.TogglePause();
    }

    public void StartMonitoring()
    {
        if (MonitoringService.IsStarted)
        {
            return;
        }

        MonitoringService.Start();
        StatsStore.SetStatus(StatusText.MonitoringStarted(), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
    }

    public void ResetSession()
    {
        StatsStore.ResetSession();
        StatsStore.SetStatus(StatusText.SessionCountersReset(), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
    }

    public void ExcludeCurrentApp()
    {
        var processName = StatsStore.CurrentProcessName;
        if (!CanExclude(processName))
        {
            StatsStore.SetStatus(StatusText.NoActiveAppAvailable(), StatsStore.CurrentAppName, false, StatsStore.CurrentProcessName);
            return;
        }

        var nonNullProcessName = processName!;
        if (!Settings.AddExcludedApp(nonNullProcessName))
        {
            StatsStore.SetStatus(StatusText.ProcessAlreadyExcluded(nonNullProcessName), nonNullProcessName, false, nonNullProcessName);
            return;
        }

        SettingsService.Save(Settings);
        MainWindow?.ShowSettingsPage();
        StatsStore.SetStatus(StatusText.AddedExcludedApp(nonNullProcessName), nonNullProcessName, false, nonNullProcessName);
    }

    public void ExportToday()
    {
        var path = Exporter.ExportToday(StatsStore.GetSnapshot());
        StatsStore.SetStatus(StatusText.ExportedCsv(path), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
    }

    public void BackupStatisticsSource()
    {
        try
        {
            var backupPath = StatsStore.BackupCurrentSource(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "inputor-backups"));
            StatsStore.SetStatus(StatusText.StatisticsBackupCreated(backupPath), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"BackupStatisticsSource failed: {exception}");
            StatsStore.SetStatus(StatusText.StatisticsBackupFailed(exception.Message), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        }
    }

    public void SwitchStatisticsSource(string? sourcePath)
    {
        var previousSourcePath = StatsStore.CurrentSourcePath;
        var previousSettingsSource = Settings.StatisticsSourcePath;

        try
        {
            var normalizedSourcePath = string.IsNullOrWhiteSpace(sourcePath)
                ? string.Empty
                : Path.GetFullPath(sourcePath.Trim());
            StatsStore.SwitchSource(normalizedSourcePath);
            MonitoringService.ResetTrackingState();
            Settings.StatisticsSourcePath = normalizedSourcePath;
            SaveSettings();
            StatsStore.SetStatus(StatusText.StatisticsSourceSwitched(StatsStore.CurrentSourcePath), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        }
        catch (Exception exception)
        {
            try
            {
                StatsStore.SwitchSource(previousSourcePath);
                MonitoringService.ResetTrackingState();
                Settings.StatisticsSourcePath = previousSettingsSource;
            }
            catch (Exception rollbackException)
            {
                StartupDiagnostics.Log($"SwitchStatisticsSource rollback failed: {rollbackException}");
            }

            StartupDiagnostics.Log($"SwitchStatisticsSource failed: {exception}");
            StatsStore.SetStatus(StatusText.StatisticsSourceSwitchFailed(exception.Message), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
        }
    }

    public void SaveSettings()
    {
        SettingsService.Save(Settings);
        AutoStartService.Apply(Settings.StartWithWindows);
        ApplyThemeMode();
    }

    public void SetDebugCaptureEnabled(bool isEnabled)
    {
        Settings.DebugCaptureEnabled = isEnabled;
        SaveSettings();
        StatsStore.SetDebugCaptureEnabled(isEnabled);
        StatsStore.SetStatus(
            StatusText.DebugCaptureChanged(isEnabled),
            StatsStore.CurrentAppName,
            StatsStore.IsCurrentTargetSupported,
            StatsStore.CurrentProcessName);
    }

    public void ClearDebugEvents()
    {
        StatsStore.ClearDebugEvents();
        StatsStore.SetStatus(StatusText.DebugEventsCleared(), StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported, StatsStore.CurrentProcessName);
    }

    public void ExitApplication()
    {
        StartupDiagnostics.Log("ExitApplication called.");
        _exitRequested = true;
        _trayMenuWindow?.CloseForExit();
        _trayMenuWindow = null;
        PerformShutdownCleanup();
        MainWindow?.Close();
    }

    public void RestartApplication()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            ExitApplication();
            return;
        }

        StartupDiagnostics.Log($"RestartApplication relaunching {processPath}.");
        Process.Start(new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = true
        });

        ExitApplication();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        StartupDiagnostics.Log("MainWindow.Closed fired.");
        if (_notifyIconService is null)
        {
            PerformShutdownCleanup();
        }
    }

    private void PerformShutdownCleanup()
    {
        if (_shutdownCompleted)
        {
            return;
        }

        _shutdownCompleted = true;
        _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
        _notifyIconService?.Dispose();
        _notifyIconService = null;
        MonitoringService.Dispose();
        StatsStore.Dispose();
    }

    private void ApplyThemeMode()
    {
        MainWindow?.ApplyThemeMode(AppStrings.ResolveThemeMode(Settings.ThemeMode));
    }

    private void UiSettings_ColorValuesChanged(UISettings sender, object args)
    {
        if (AppStrings.ResolveThemeMode(Settings.ThemeMode) is not AppThemeMode.FollowSystem)
        {
            return;
        }

        var window = MainWindow;
        if (window is null)
        {
            return;
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            window.ApplyThemeMode(AppThemeMode.FollowSystem);
            return;
        }

        window.DispatcherQueue.TryEnqueue(() => window.ApplyThemeMode(AppThemeMode.FollowSystem));
    }

    private static bool CanExclude(string? processName)
    {
        return !string.IsNullOrWhiteSpace(processName)
            && !string.Equals(processName, "inputor.App", StringComparison.OrdinalIgnoreCase);
    }

    public IXamlType GetXamlType(Type type)
    {
        _metadataProvider ??= new XamlControlsXamlMetaDataProvider();
        return _metadataProvider.GetXamlType(type);
    }

    public IXamlType GetXamlType(string fullName)
    {
        _metadataProvider ??= new XamlControlsXamlMetaDataProvider();
        return _metadataProvider.GetXamlType(fullName);
    }

    public XmlnsDefinition[] GetXmlnsDefinitions()
    {
        _metadataProvider ??= new XamlControlsXamlMetaDataProvider();
        return _metadataProvider.GetXmlnsDefinitions();
    }
}
