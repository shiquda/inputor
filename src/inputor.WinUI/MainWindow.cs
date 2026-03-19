using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Inputor.WinUI;

public sealed class MainWindow : Window
{
    private readonly DispatcherQueue _dispatcherQueue;
    private int _refreshQueued;
    private readonly NavigationView _rootNavigation;
    private readonly NavigationViewItem _overviewItem;
    private readonly NavigationViewItem _statisticsItem;
    private readonly NavigationViewItem _appsItem;
    private readonly NavigationViewItem _debugItem;
    private readonly NavigationViewItem _settingsItem;
    private readonly Grid _overviewPage;
    private readonly StatisticsPage _statisticsPage;
    private readonly Grid _appsPage;
    private readonly DebugPage _debugPage;
    private readonly SettingsPage _settingsPage;
    private readonly TextBlock _todayTextBlock;
    private readonly TextBlock _sessionTextBlock;
    private readonly TextBlock _totalTextBlock;
    private readonly TextBlock _currentTargetTextBlock;
    private readonly TextBlock _statusTextBlock;
    private readonly Button _pauseButton;
    private readonly TextBlock _appsSummaryTextBlock;
    private readonly TextBlock _paneStatusTextBlock;
    private readonly TextBox _searchBox;
    private readonly ComboBox _sortComboBox;
    private readonly StackPanel _recentActivityPanel;
    private readonly StackPanel _topAppsPanel;
    private readonly StackPanel _allAppsPanel;
    private bool _isClosed;

    public MainWindow()
    {
        Title = AppStrings.Get("App.Name");
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _todayTextBlock = CreateMetricValue();
        _sessionTextBlock = CreateMetricValue();
        _totalTextBlock = CreateMetricValue();
        _currentTargetTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _statusTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _pauseButton = new Button { Content = AppStrings.Get("Main.Button.StartMonitoring") };
        _pauseButton.Click += (_, _) =>
        {
            if (App.Current.MonitoringService.IsStarted)
            {
                App.Current.TogglePauseMonitoring();
                return;
            }

            App.Current.StartMonitoring();
            Refresh();
        };
        _appsSummaryTextBlock = new TextBlock();
        _paneStatusTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.7 };
        _searchBox = new TextBox { PlaceholderText = AppStrings.Get("Main.Placeholder.SearchApps"), Width = 240 };
        _searchBox.TextChanged += (_, _) => Refresh();
        _sortComboBox = new ComboBox
        {
            Width = 160,
            ItemsSource = new[]
            {
                AppStrings.Get("Common.Today"),
                AppStrings.Get("Common.Session"),
                AppStrings.Get("Common.AllTime"),
                AppStrings.Get("Common.Name")
            },
            SelectedIndex = 0
        };
        _sortComboBox.SelectionChanged += (_, _) => Refresh();
        _recentActivityPanel = new StackPanel { Spacing = 8 };
        _topAppsPanel = new StackPanel { Spacing = 8 };
        _allAppsPanel = new StackPanel { Spacing = 8 };

        _overviewPage = BuildOverviewPage();
        _statisticsPage = new StatisticsPage { Visibility = Visibility.Collapsed };
        _appsPage = BuildAppsPage();
        _debugPage = new DebugPage { Visibility = Visibility.Collapsed };
        _settingsPage = new SettingsPage { Visibility = Visibility.Collapsed };
        _appsPage.Visibility = Visibility.Collapsed;

        _overviewItem = new NavigationViewItem { Content = AppStrings.Get("Main.Navigation.Overview"), Icon = new SymbolIcon(Symbol.Home), Tag = "Overview" };
        _statisticsItem = new NavigationViewItem { Content = AppStrings.Get("Main.Navigation.Statistics"), Icon = new SymbolIcon(Symbol.World), Tag = "Statistics" };
        _appsItem = new NavigationViewItem { Content = AppStrings.Get("Main.Navigation.Apps"), Icon = new SymbolIcon(Symbol.Library), Tag = "Apps" };
        _debugItem = new NavigationViewItem { Content = AppStrings.Get("Main.Navigation.Debug"), Icon = new SymbolIcon(Symbol.ReportHacked), Tag = "Debug" };
        _settingsItem = new NavigationViewItem { Content = AppStrings.Get("Main.Navigation.Settings"), Icon = new SymbolIcon(Symbol.Setting), Tag = "Settings" };

        _rootNavigation = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsPaneToggleButtonVisible = true,
            OpenPaneLength = 240,
            CompactModeThresholdWidth = 760,
            ExpandedModeThresholdWidth = 1080,
            AlwaysShowHeader = false,
            PaneHeader = BuildPaneHeader(),
            PaneFooter = BuildPaneFooter(),
            Content = new Grid
            {
                Children =
                {
                    _overviewPage,
                    _statisticsPage,
                    _appsPage,
                    _debugPage,
                    _settingsPage
                }
            }
        };
        _rootNavigation.MenuItems.Add(_overviewItem);
        _rootNavigation.MenuItems.Add(_statisticsItem);
        _rootNavigation.MenuItems.Add(_appsItem);
        _rootNavigation.MenuItems.Add(_debugItem);
        _rootNavigation.MenuItems.Add(_settingsItem);
        _rootNavigation.SelectionChanged += RootNavigation_SelectionChanged;
        _rootNavigation.SelectedItem = _overviewItem;
        _rootNavigation.Margin = new Thickness(0, 8, 0, 0);
        _rootNavigation.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        Content = _rootNavigation;
        ApplyWindowChrome();
        _rootNavigation.ActualThemeChanged += (_, _) => ApplyWindowChrome();
        App.Current.StatsStore.Changed += StatsStore_Changed;
        AppPresentationService.IconsChanged += AppPresentationService_IconsChanged;
        Closed += MainWindow_Closed;
        Refresh();
    }

    private void AppPresentationService_IconsChanged(object? sender, EventArgs e)
    {
        QueueRefresh();
    }

    private void StatsStore_Changed(object? sender, EventArgs e)
    {
        QueueRefresh();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        App.Current.StatsStore.Changed -= StatsStore_Changed;
        AppPresentationService.IconsChanged -= AppPresentationService_IconsChanged;
    }

    private void QueueRefresh()
    {
        if (_isClosed || Interlocked.Exchange(ref _refreshQueued, 1) == 1)
        {
            return;
        }

        void FlushRefresh()
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            if (_isClosed)
            {
                return;
            }

            Refresh();
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            FlushRefresh();
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(FlushRefresh))
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
        }
    }

    public void ShowSettingsPage()
    {
        _settingsPage.RefreshFromState();
        _rootNavigation.SelectedItem = _settingsItem;
        SwitchPage("Settings");
    }

    private UIElement BuildPaneHeader()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(12, 8, 12, 12) };
        panel.Children.Add(new TextBlock { Text = AppStrings.Get("App.Name"), FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = AppStrings.Get("App.Tagline"), FontSize = 12, Opacity = 0.72 });
        return panel;
    }

    private UIElement BuildPaneFooter()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(12, 12, 12, 16) };
        panel.Children.Add(new TextBlock { Text = AppStrings.Get("Main.Pane.LiveStatus"), FontSize = 12, Opacity = 0.72 });
        panel.Children.Add(_paneStatusTextBlock);
        return panel;
    }

    private Grid BuildOverviewPage()
    {
        var grid = new Grid();
        var stack = new StackPanel { Spacing = 24, Padding = new Thickness(28, 24, 28, 28) };

        stack.Children.Add(CreateSectionHeader(AppStrings.Get("Main.Section.TodayTitle"), AppStrings.Get("Main.Section.TodaySubtitle")));

        var metrics = new Grid { ColumnSpacing = 16 };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.Children.Add(CreateCard(AppStrings.Get("Common.Today"), _todayTextBlock));
        var sessionCard = CreateCard(AppStrings.Get("Common.Session"), _sessionTextBlock);
        Grid.SetColumn(sessionCard, 1);
        metrics.Children.Add(sessionCard);
        var totalCard = CreateCard(AppStrings.Get("Common.AllTime"), _totalTextBlock);
        Grid.SetColumn(totalCard, 2);
        metrics.Children.Add(totalCard);
        stack.Children.Add(metrics);

        stack.Children.Add(CreateSectionHeader(AppStrings.Get("Main.Section.FocusTitle"), AppStrings.Get("Main.Section.FocusSubtitle")));
        var details = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 20, 0, 0) };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.Children.Add(CreateInfoCard(AppStrings.Get("Main.Card.CurrentTarget"), _currentTargetTextBlock, _statusTextBlock));
        stack.Children.Add(details);

        stack.Children.Add(CreateSectionHeader(AppStrings.Get("Main.Section.WorkflowTitle"), AppStrings.Get("Main.Section.WorkflowSubtitle")));
        var lower = new Grid { ColumnSpacing = 16 };
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var actions = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        _pauseButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _pauseButton.Padding = new Thickness(12, 8, 12, 8);
        actions.Children.Add(_pauseButton);
        actions.Children.Add(CreateActionButton(AppStrings.Get("Main.Button.ExcludeCurrentApp"), (_, _) => App.Current.ExcludeCurrentApp()));
        actions.Children.Add(CreateActionButton(AppStrings.Get("Main.Button.ViewAllApps"), (_, _) => ShowAppsPage()));
        actions.Children.Add(CreateActionButton(AppStrings.Get("Main.Button.OpenStatistics"), (_, _) => ShowStatisticsPage()));
        actions.Children.Add(CreateActionButton(AppStrings.Get("Main.Button.OpenSettings"), (_, _) => ShowSettingsPage()));
        actions.Children.Add(CreateActionButton(AppStrings.Get("Main.Button.OpenDebug"), (_, _) => ShowDebugPage()));
        lower.Children.Add(CreateInfoCard(AppStrings.Get("Main.Card.QuickActions"), actions));

        var recentCard = CreateInfoCard(AppStrings.Get("Main.Card.RecentActivity"), _recentActivityPanel);
        Grid.SetColumn(recentCard, 1);
        lower.Children.Add(recentCard);
        var topAppsCard = CreateInfoCard(AppStrings.Get("Main.Card.TopAppsToday"), _topAppsPanel);
        Grid.SetColumn(topAppsCard, 2);
        lower.Children.Add(topAppsCard);
        stack.Children.Add(lower);

        grid.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        });

        return grid;
    }

    private Grid BuildAppsPage()
    {
        var grid = new Grid { Padding = new Thickness(28, 24, 28, 28) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var summaryPanel = new StackPanel { Spacing = 6 };
        summaryPanel.Children.Add(new TextBlock { Text = AppStrings.Get("Main.Apps.Title"), FontSize = 22, FontWeight = FontWeights.SemiBold });
        _appsSummaryTextBlock.FontSize = 14;
        _appsSummaryTextBlock.Opacity = 0.78;
        summaryPanel.Children.Add(_appsSummaryTextBlock);
        grid.Children.Add(summaryPanel);

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        toolbar.Children.Add(_searchBox);
        toolbar.Children.Add(_sortComboBox);
        var toolbarCard = CreateCard(toolbar);
        toolbarCard.Margin = new Thickness(0, 16, 0, 0);
        Grid.SetRow(toolbarCard, 1);
        grid.Children.Add(toolbarCard);

        var scroll = new ScrollViewer { Margin = new Thickness(0, 16, 0, 0) };
        scroll.Content = _allAppsPanel;
        Grid.SetRow(scroll, 2);
        grid.Children.Add(scroll);

        return grid;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedTag = (args.SelectedItemContainer as NavigationViewItem)?.Tag as string ?? "Overview";
        SwitchPage(selectedTag);
    }

    private void SwitchPage(string selectedTag)
    {
        if (selectedTag != "Statistics")
        {
            _statisticsPage.ResetInteractionState();
        }

        if (selectedTag != "Debug")
        {
            _debugPage.ResetInteractionState();
        }

        _overviewPage.Visibility = selectedTag == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        _statisticsPage.Visibility = selectedTag == "Statistics" ? Visibility.Visible : Visibility.Collapsed;
        _appsPage.Visibility = selectedTag == "Apps" ? Visibility.Visible : Visibility.Collapsed;
        _debugPage.Visibility = selectedTag == "Debug" ? Visibility.Visible : Visibility.Collapsed;
        _settingsPage.Visibility = selectedTag == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        if (selectedTag == "Settings")
        {
            _settingsPage.RefreshFromState();
        }
        else if (selectedTag == "Statistics")
        {
            _statisticsPage.Refresh(App.Current.StatsStore.GetSnapshot());
        }
        else if (selectedTag == "Debug")
        {
            _debugPage.Refresh(App.Current.StatsStore.GetSnapshot());
        }
    }

    private void ShowStatisticsPage()
    {
        _rootNavigation.SelectedItem = _statisticsItem;
        SwitchPage("Statistics");
    }

    private void ShowAppsPage()
    {
        _rootNavigation.SelectedItem = _appsItem;
        SwitchPage("Apps");
    }

    private void ShowDebugPage()
    {
        _rootNavigation.SelectedItem = _debugItem;
        SwitchPage("Debug");
    }

    private void Refresh()
    {
        var snapshot = App.Current.StatsStore.GetSnapshot();
        _todayTextBlock.Text = snapshot.TotalToday.ToString("N0");
        _sessionTextBlock.Text = snapshot.TotalSession.ToString("N0");
        _totalTextBlock.Text = snapshot.TotalAllTime.ToString("N0");
        _currentTargetTextBlock.Text = AppStrings.Format(
            "Main.CurrentTargetLine",
            snapshot.CurrentAppName,
            AppStrings.Get(snapshot.IsCurrentTargetSupported ? "Target.Supported" : "Target.Unsupported"));
        _statusTextBlock.Text = snapshot.StatusMessage;
        _pauseButton.Content = !App.Current.MonitoringService.IsStarted
            ? AppStrings.Get("Main.Button.StartMonitoring")
            : snapshot.IsPaused ? AppStrings.Get("Main.Button.ResumeMonitoring") : AppStrings.Get("Main.Button.PauseMonitoring");
        _paneStatusTextBlock.Text = snapshot.IsPaused
            ? StatusText.MonitoringPauseChanged(true)
            : AppStrings.Format("Main.Pane.Watching", snapshot.CurrentAppName);

        UpdateRecentActivity(snapshot);
        UpdateTopApps(snapshot);
        UpdateAppList(snapshot);
        if (_statisticsPage.Visibility == Visibility.Visible)
        {
            _statisticsPage.Refresh(snapshot);
        }
        if (_debugPage.Visibility == Visibility.Visible)
        {
            _debugPage.Refresh(snapshot);
        }
    }

    private void UpdateRecentActivity(DashboardSnapshot snapshot)
    {
        _recentActivityPanel.Children.Clear();
        if (snapshot.RecentActivity.Count == 0)
        {
            _recentActivityPanel.Children.Add(new TextBlock { Text = AppStrings.Get("Main.RecentActivity.Empty") });
            return;
        }

        foreach (var item in snapshot.RecentActivity)
        {
            _recentActivityPanel.Children.Add(CreateInfoRow(item.AppName, AppStrings.Format("Main.RecentActivity.Row", item.Delta, item.Timestamp)));
        }
    }

    private void UpdateTopApps(DashboardSnapshot snapshot)
    {
        _topAppsPanel.Children.Clear();
        var topApps = AppPresentationService.BuildAggregates(snapshot.AppStats)
            .OrderByDescending(item => item.TodayCount)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        foreach (var stat in topApps)
        {
            var percentage = snapshot.TotalToday == 0 ? 0 : stat.TodayCount * 100.0 / snapshot.TotalToday;
            _topAppsPanel.Children.Add(CreateAppInfoRow(stat, AppStrings.Format("Main.TopApps.Row", stat.TodayCount, percentage)));
        }

        AppPresentationService.WarmIcons(topApps.SelectMany(item => item.ProcessNames).ToList(), _dispatcherQueue);

        if (_topAppsPanel.Children.Count == 0)
        {
            _topAppsPanel.Children.Add(new TextBlock { Text = AppStrings.Get("Main.TopApps.Empty") });
        }
    }

    private void UpdateAppList(DashboardSnapshot snapshot)
    {
        var query = _searchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<AppAggregate> stats = AppPresentationService.BuildAggregates(snapshot.AppStats);
        stats = stats.Where(item => AppPresentationService.MatchesQuery(item, query));

        stats = _sortComboBox.SelectedIndex switch
        {
            1 => stats.OrderByDescending(item => item.SessionCount).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            2 => stats.OrderByDescending(item => item.TotalCount).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            3 => stats.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => stats.OrderByDescending(item => item.TodayCount).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        };

        var filtered = stats.ToList();
        _appsSummaryTextBlock.Text = filtered.Count == 0
            ? AppStrings.Get("Main.Apps.NoMatch")
            : AppStrings.Format("Main.Apps.Summary", filtered.Count, snapshot.TotalToday, snapshot.TotalSession, snapshot.TotalAllTime);

        _allAppsPanel.Children.Clear();
        foreach (var stat in filtered)
        {
            var percentage = snapshot.TotalToday == 0 ? 0 : stat.TodayCount * 100.0 / snapshot.TotalToday;
            _allAppsPanel.Children.Add(CreateAppInfoRow(
                stat,
                AppStrings.Format("Main.Apps.Row", stat.TodayCount, stat.SessionCount, stat.TotalCount, percentage)));
        }

        AppPresentationService.WarmIcons(filtered.SelectMany(item => item.ProcessNames).ToList(), _dispatcherQueue);
    }

    private static TextBlock CreateMetricValue()
    {
        return new TextBlock { FontSize = 34, FontWeight = FontWeights.SemiBold };
    }

    private static Button CreateActionButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 8, 12, 8)
        };
        button.Click += handler;
        return button;
    }

    private static Border CreateCard(string title, TextBlock value)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Opacity = 0.72 });
        panel.Children.Add(value);
        return CreateCard(panel);
    }

    private static UIElement CreateSectionHeader(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private static Border CreateInfoCard(string title, UIElement content)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(content);
        return CreateCard(panel);
    }

    private static Border CreateInfoCard(string title, TextBlock primary, TextBlock secondary)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(primary);
        panel.Children.Add(new TextBlock { Text = AppStrings.Get("Main.Card.Status"), FontWeight = FontWeights.SemiBold, FontSize = 12, Opacity = 0.7 });
        panel.Children.Add(secondary);
        return CreateCard(panel);
    }

    private static Border CreateCard(UIElement child, Brush? background = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.Get("CardStrokeColorDefaultBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = child
        };
        border.Background = background ?? ThemeBrushes.Get("CardBackgroundFillColorDefaultBrush", "LayerFillColorDefaultBrush");
        return border;
    }

    private static Border CreateInfoRow(string primary, string secondary)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = primary, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = secondary, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.Get("SurfaceStrokeColorFlyoutBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = panel
        };
        border.Background = ThemeBrushes.Get("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush");
        return border;
    }

    private static Border CreateAppInfoRow(AppAggregate aggregate, string secondary)
    {
        var iconSource = AppPresentationService.TryGetIconSource(aggregate.ProcessNames);
        var layout = new Grid { ColumnSpacing = 12 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(12),
            Background = ThemeBrushes.Get("AccentFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"),
            Child = iconSource is not null
                ? new Image
                {
                    Source = iconSource,
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : new FontIcon
                {
                    Glyph = aggregate.IconGlyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
        };
        layout.Children.Add(icon);

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock { Text = aggregate.DisplayName, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.WrapWholeWords });
        body.Children.Add(new TextBlock { Text = secondary, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        if (aggregate.ProcessNames.Count > 1)
        {
            body.Children.Add(new TextBlock
            {
                Text = AppStrings.Format("Main.Apps.GroupedFrom", string.Join(", ", aggregate.ProcessNames)),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.68,
                FontSize = 12
            });
        }

        Grid.SetColumn(body, 1);
        layout.Children.Add(body);

        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.Get("SurfaceStrokeColorFlyoutBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = layout
        };
        border.Background = ThemeBrushes.Get("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush");
        return border;
    }

    private void ApplyWindowChrome()
    {
        var appWindow = WindowHelpers.GetAppWindow(this);
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.PreferredTheme = _rootNavigation.ActualTheme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode
        };

        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(32, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(48, 255, 255, 255);
    }
}
