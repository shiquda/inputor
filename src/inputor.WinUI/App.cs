using System;
using System.IO;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace Inputor.WinUI;

public sealed class App : Application, IXamlMetadataProvider
{
    private readonly string _dataDirectory;
    private bool _exitRequested;
    private XamlControlsXamlMetaDataProvider? _metadataProvider;

    public App()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "inputor");
        Directory.CreateDirectory(_dataDirectory);

        SettingsService = new AppSettingsService(_dataDirectory);
        Settings = SettingsService.Load();
        StatsStore = new StatsStore(_dataDirectory);
        Exporter = new CsvExportService();
        AutoStartService = new AutoStartService();
        AutoStartService.Apply(Settings.StartWithWindows);
        MonitoringService = new MonitoringService(StatsStore, Settings);
    }

    public static new App Current => (App)Application.Current;

    public AppSettings Settings { get; }

    public AppSettingsService SettingsService { get; }

    public StatsStore StatsStore { get; }

    public CsvExportService Exporter { get; }

    public AutoStartService AutoStartService { get; }

    public MonitoringService MonitoringService { get; }

    public MainWindow? MainWindow { get; private set; }

    public SettingsWindow? SettingsWindow { get; private set; }

    private NotifyIconService? _notifyIconService;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        XamlControlsXamlMetaDataProvider.Initialize();
        _metadataProvider ??= new XamlControlsXamlMetaDataProvider();
        Resources ??= new ResourceDictionary();
        Resources.MergedDictionaries.Add(new XamlControlsResources());

        MainWindow = new MainWindow();
        MainWindow.Activate();
        WindowHelpers.RegisterHideOnClose(MainWindow, () => !_exitRequested);

        SettingsWindow = new SettingsWindow();
        WindowHelpers.RegisterHideOnClose(SettingsWindow, () => !_exitRequested);
        WindowHelpers.HideWindow(SettingsWindow);

        _notifyIconService = new NotifyIconService();

        MonitoringService.Start();
        base.OnLaunched(args);
    }

    public void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        WindowHelpers.ShowWindow(MainWindow);
    }

    public void ShowSettingsWindow()
    {
        if (SettingsWindow is null)
        {
            return;
        }

        SettingsWindow.ReloadFromSettings();
        WindowHelpers.ShowWindow(SettingsWindow);
    }

    public void TogglePauseMonitoring()
    {
        MonitoringService.TogglePause();
    }

    public void ResetSession()
    {
        StatsStore.ResetSession();
        StatsStore.SetStatus("Session counters reset.", StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported);
    }

    public void ExcludeCurrentApp()
    {
        var processName = StatsStore.CurrentAppName;
        if (!CanExclude(processName))
        {
            StatsStore.SetStatus("No active app is available for exclusion right now.", processName, false);
            return;
        }

        if (!Settings.AddExcludedApp(processName))
        {
            StatsStore.SetStatus($"{processName} is already excluded.", processName, false);
            return;
        }

        SettingsService.Save(Settings);
        SettingsWindow?.ReloadFromSettings();
        StatsStore.SetStatus($"Added {processName} to excluded apps.", processName, false);
    }

    public void ExportToday()
    {
        var path = Exporter.ExportToday(StatsStore.GetSnapshot());
        StatsStore.SetStatus($"Exported CSV to {path}", StatsStore.CurrentAppName, StatsStore.IsCurrentTargetSupported);
    }

    public void SaveSettings()
    {
        SettingsService.Save(Settings);
        AutoStartService.Apply(Settings.StartWithWindows);
    }

    public void ExitApplication()
    {
        _exitRequested = true;
        _notifyIconService?.Dispose();
        MonitoringService.Dispose();
        StatsStore.Dispose();
        SettingsWindow?.Close();
        MainWindow?.Close();
    }

    private static bool CanExclude(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName)
            && !string.Equals(processName, "Idle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(processName, "Unavailable", StringComparison.OrdinalIgnoreCase)
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
