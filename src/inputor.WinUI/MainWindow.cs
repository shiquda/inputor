using System;
using System.Collections.Generic;
using System.Linq;
using Inputor.App.Models;
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
    private readonly NavigationView _rootNavigation;
    private readonly NavigationViewItem _overviewItem;
    private readonly NavigationViewItem _appsItem;
    private readonly Grid _overviewPage;
    private readonly Grid _appsPage;
    private readonly TextBlock _todayTextBlock;
    private readonly TextBlock _sessionTextBlock;
    private readonly TextBlock _totalTextBlock;
    private readonly TextBlock _currentTargetTextBlock;
    private readonly TextBlock _statusTextBlock;
    private readonly TextBlock _goalSummaryTextBlock;
    private readonly TextBlock _goalMilestoneTextBlock;
    private readonly ProgressBar _goalProgressBar;
    private readonly Button _pauseButton;
    private readonly TextBlock _appsSummaryTextBlock;
    private readonly TextBlock _paneStatusTextBlock;
    private readonly TextBox _searchBox;
    private readonly ComboBox _sortComboBox;
    private readonly StackPanel _recentActivityPanel;
    private readonly StackPanel _topAppsPanel;
    private readonly StackPanel _allAppsPanel;
    private readonly TextBlock _adminReminderTextBlock;

    public MainWindow()
    {
        Title = "inputor";
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _todayTextBlock = CreateMetricValue();
        _sessionTextBlock = CreateMetricValue();
        _totalTextBlock = CreateMetricValue();
        _currentTargetTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _statusTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _goalSummaryTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _goalMilestoneTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _goalProgressBar = new ProgressBar { Height = 8, Maximum = 100 };
        _pauseButton = new Button { Content = "Pause monitoring" };
        _pauseButton.Click += (_, _) => App.Current.TogglePauseMonitoring();
        _appsSummaryTextBlock = new TextBlock();
        _paneStatusTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.7 };
        _searchBox = new TextBox { PlaceholderText = "Search apps", Width = 240 };
        _searchBox.TextChanged += (_, _) => Refresh();
        _sortComboBox = new ComboBox { Width = 160, ItemsSource = new[] { "Today", "Session", "All time", "Name" }, SelectedIndex = 0 };
        _sortComboBox.SelectionChanged += (_, _) => Refresh();
        _recentActivityPanel = new StackPanel { Spacing = 8 };
        _topAppsPanel = new StackPanel { Spacing = 8 };
        _allAppsPanel = new StackPanel { Spacing = 8 };
        _adminReminderTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

        _overviewPage = BuildOverviewPage();
        _appsPage = BuildAppsPage();
        _appsPage.Visibility = Visibility.Collapsed;

        _overviewItem = new NavigationViewItem { Content = "Overview", Icon = new SymbolIcon(Symbol.Home), Tag = "Overview" };
        _appsItem = new NavigationViewItem { Content = "Apps", Icon = new SymbolIcon(Symbol.Library), Tag = "Apps" };

        _rootNavigation = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsPaneToggleButtonVisible = true,
            OpenPaneLength = 240,
            CompactModeThresholdWidth = 760,
            ExpandedModeThresholdWidth = 1080,
            AlwaysShowHeader = true,
            PaneHeader = BuildPaneHeader(),
            PaneFooter = BuildPaneFooter(),
            Header = BuildHeader(),
            Content = new Grid
            {
                Children =
                {
                    _overviewPage,
                    _appsPage
                }
            }
        };
        _rootNavigation.MenuItems.Add(_overviewItem);
        _rootNavigation.MenuItems.Add(_appsItem);
        _rootNavigation.SelectionChanged += RootNavigation_SelectionChanged;
        _rootNavigation.SelectedItem = _overviewItem;
        _rootNavigation.Margin = new Thickness(0, 8, 0, 0);
        _rootNavigation.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        Content = _rootNavigation;
        ApplyWindowChrome();
        _rootNavigation.ActualThemeChanged += (_, _) => ApplyWindowChrome();
        App.Current.StatsStore.Changed += (_, _) => _dispatcherQueue.TryEnqueue(Refresh);
        Refresh();
    }

    private UIElement BuildHeader()
    {
        var grid = new Grid { ColumnSpacing = 16, Padding = new Thickness(0, 12, 0, 12), MinHeight = 72 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(new TextBlock { Text = "inputor", FontSize = 28, FontWeight = FontWeights.SemiBold });
        left.Children.Add(new TextBlock { Text = "Privacy-safe input statistics in a native Windows shell.", Opacity = 0.72 });
        grid.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var exportButton = CreatePrimaryButton("Export today");
        exportButton.Click += (_, _) => App.Current.ExportToday();
        var settingsButton = new Button { Content = "Settings", Padding = new Thickness(16, 8, 16, 8) };
        settingsButton.Click += (_, _) => App.Current.ShowSettingsWindow();
        right.Children.Add(exportButton);
        right.Children.Add(settingsButton);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return grid;
    }

    private UIElement BuildPaneHeader()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(12, 8, 12, 12) };
        panel.Children.Add(new TextBlock { Text = "inputor", FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Private typing insights", FontSize = 12, Opacity = 0.72 });
        return panel;
    }

    private UIElement BuildPaneFooter()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(12, 12, 12, 16) };
        panel.Children.Add(new TextBlock { Text = "Live status", FontSize = 12, Opacity = 0.72 });
        panel.Children.Add(_paneStatusTextBlock);
        return panel;
    }

    private Grid BuildOverviewPage()
    {
        var grid = new Grid();
        var stack = new StackPanel { Spacing = 24, Padding = new Thickness(28, 24, 28, 28) };

        stack.Children.Add(CreateSectionHeader("Today at a glance", "A quick snapshot of your current typing totals."));

        var metrics = new Grid { ColumnSpacing = 16 };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.Children.Add(CreateCard("Today", _todayTextBlock));
        var sessionCard = CreateCard("Session", _sessionTextBlock); Grid.SetColumn(sessionCard, 1); metrics.Children.Add(sessionCard);
        var totalCard = CreateCard("All time", _totalTextBlock); Grid.SetColumn(totalCard, 2); metrics.Children.Add(totalCard);
        stack.Children.Add(metrics);

        stack.Children.Add(CreateSectionHeader("Focus and progress", "Track the active target, goal completion, and monitoring state."));
        var details = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 20, 0, 0) };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.Children.Add(CreateInfoCard("Current target", _currentTargetTextBlock, _statusTextBlock));
        var goalPanel = new StackPanel { Spacing = 8 };
        goalPanel.Children.Add(_goalSummaryTextBlock);
        goalPanel.Children.Add(_goalProgressBar);
        goalPanel.Children.Add(_goalMilestoneTextBlock);
        var goalCard = CreateInfoCard("Daily goal", goalPanel);
        Grid.SetColumn(goalCard, 1);
        details.Children.Add(goalCard);
        stack.Children.Add(details);

        stack.Children.Add(CreateSectionHeader("Workflow", "Quick actions and recent patterns in one place."));
        var lower = new Grid { ColumnSpacing = 16 };
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var actions = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        _pauseButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _pauseButton.Padding = new Thickness(12, 8, 12, 8);
        actions.Children.Add(_pauseButton);
        actions.Children.Add(CreateActionButton("Reset session", (_, _) => App.Current.ResetSession()));
        actions.Children.Add(CreateActionButton("Exclude current app", (_, _) => App.Current.ExcludeCurrentApp()));
        actions.Children.Add(CreateActionButton("View all apps", (_, _) => ShowAppsPage()));
        lower.Children.Add(CreateInfoCard("Quick actions", actions));

        var recentCard = CreateInfoCard("Recent activity", _recentActivityPanel); Grid.SetColumn(recentCard, 1); lower.Children.Add(recentCard);
        var topAppsCard = CreateInfoCard("Top apps today", _topAppsPanel); Grid.SetColumn(topAppsCard, 2); lower.Children.Add(topAppsCard);
        var adminCard = CreateInfoCard("Admin reminder", _adminReminderTextBlock); Grid.SetColumn(adminCard, 3); lower.Children.Add(adminCard);
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
        summaryPanel.Children.Add(new TextBlock { Text = "Apps", FontSize = 22, FontWeight = FontWeights.SemiBold });
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
        var selectedTag = (args.SelectedItemContainer as NavigationViewItem)?.Tag as string;
        _overviewPage.Visibility = selectedTag == "Apps" ? Visibility.Collapsed : Visibility.Visible;
        _appsPage.Visibility = selectedTag == "Apps" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowAppsPage()
    {
        _rootNavigation.SelectedItem = _appsItem;
        _overviewPage.Visibility = Visibility.Collapsed;
        _appsPage.Visibility = Visibility.Visible;
    }

    private void Refresh()
    {
        var snapshot = App.Current.StatsStore.GetSnapshot();
        _todayTextBlock.Text = snapshot.TotalToday.ToString("N0");
        _sessionTextBlock.Text = snapshot.TotalSession.ToString("N0");
        _totalTextBlock.Text = snapshot.TotalAllTime.ToString("N0");
        _currentTargetTextBlock.Text = $"{snapshot.CurrentAppName} • {(snapshot.IsCurrentTargetSupported ? "Supported" : "Unsupported")}";
        _statusTextBlock.Text = snapshot.StatusMessage;
        _pauseButton.Content = snapshot.IsPaused ? "Resume monitoring" : "Pause monitoring";
        _paneStatusTextBlock.Text = snapshot.IsPaused
            ? "Monitoring paused."
            : $"Watching {snapshot.CurrentAppName}";
        _adminReminderTextBlock.Text = snapshot.ShowAdminReminder
            ? "Elevated windows and protected surfaces can still be unavailable to UIA monitoring."
            : "Admin reminder is turned off.";

        UpdateGoal(snapshot);
        UpdateRecentActivity(snapshot);
        UpdateTopApps(snapshot);
        UpdateAppList(snapshot);
    }

    private void UpdateGoal(DashboardSnapshot snapshot)
    {
        var dailyGoal = Math.Max(0, App.Current.Settings.DailyGoal);
        if (dailyGoal == 0)
        {
            _goalSummaryTextBlock.Text = "Daily goal is disabled. Set a value in Settings to track progress.";
            _goalMilestoneTextBlock.Text = $"Session started at {snapshot.SessionStartedAt:HH:mm}.";
            _goalProgressBar.Value = 0;
            return;
        }

        var progress = Math.Min(100, snapshot.TotalToday * 100.0 / dailyGoal);
        var remaining = Math.Max(0, dailyGoal - snapshot.TotalToday);
        _goalSummaryTextBlock.Text = remaining == 0
            ? $"Goal reached. {snapshot.TotalToday - dailyGoal:N0} above today's target of {dailyGoal:N0}."
            : $"{remaining:N0} characters remaining to hit today's goal of {dailyGoal:N0}.";
        _goalMilestoneTextBlock.Text = $"Session started at {snapshot.SessionStartedAt:HH:mm}.";
        _goalProgressBar.Value = progress;
    }

    private void UpdateRecentActivity(DashboardSnapshot snapshot)
    {
        _recentActivityPanel.Children.Clear();
        if (snapshot.RecentActivity.Count == 0)
        {
            _recentActivityPanel.Children.Add(new TextBlock { Text = "No recorded input yet in this session." });
            return;
        }

        foreach (var item in snapshot.RecentActivity)
        {
            _recentActivityPanel.Children.Add(CreateInfoRow(item.AppName, $"+{item.Delta} • {item.Timestamp:HH:mm:ss}"));
        }
    }

    private void UpdateTopApps(DashboardSnapshot snapshot)
    {
        _topAppsPanel.Children.Clear();
        foreach (var stat in snapshot.AppStats.Take(5))
        {
            var percentage = snapshot.TotalToday == 0 ? 0 : stat.TodayCount * 100.0 / snapshot.TotalToday;
            _topAppsPanel.Children.Add(CreateInfoRow(stat.AppName, $"{stat.TodayCount:N0} today • {percentage:0.#}%"));
        }

        if (_topAppsPanel.Children.Count == 0)
        {
            _topAppsPanel.Children.Add(new TextBlock { Text = "No activity yet." });
        }
    }

    private void UpdateAppList(DashboardSnapshot snapshot)
    {
        var query = _searchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<AppStat> stats = snapshot.AppStats;
        if (!string.IsNullOrWhiteSpace(query))
        {
            stats = stats.Where(item => item.AppName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        stats = _sortComboBox.SelectedIndex switch
        {
            1 => stats.OrderByDescending(item => item.SessionCount).ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase),
            2 => stats.OrderByDescending(item => item.TotalCount).ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase),
            3 => stats.OrderBy(item => item.AppName, StringComparer.OrdinalIgnoreCase),
            _ => stats.OrderByDescending(item => item.TodayCount).ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
        };

        var filtered = stats.ToList();
        _appsSummaryTextBlock.Text = filtered.Count == 0
            ? "No apps match the current filter."
            : $"{filtered.Count} apps • {snapshot.TotalToday:N0} today • {snapshot.TotalSession:N0} session • {snapshot.TotalAllTime:N0} total";

        _allAppsPanel.Children.Clear();
        foreach (var stat in filtered)
        {
            var percentage = snapshot.TotalToday == 0 ? 0 : stat.TodayCount * 100.0 / snapshot.TotalToday;
            _allAppsPanel.Children.Add(CreateInfoRow(stat.AppName, $"{stat.TodayCount:N0} today • {stat.SessionCount:N0} session • {stat.TotalCount:N0} total • {percentage:0.#}%"));
        }
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

    private static Button CreatePrimaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(16, 8, 16, 8),
            Background = GetThemeBrush("AccentFillColorDefaultBrush", "SystemFillColorSolidAccentBrush"),
            Foreground = GetThemeBrush("TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorPrimaryBrush")
        };
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
        panel.Children.Add(new TextBlock { Text = "Status", FontWeight = FontWeights.SemiBold, FontSize = 12, Opacity = 0.7 });
        panel.Children.Add(secondary);
        return CreateCard(panel);
    }

    private static Border CreateCard(UIElement child, Microsoft.UI.Xaml.Media.Brush? background = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = child
        };
        border.Background = background ?? GetThemeBrush("CardBackgroundFillColorDefaultBrush", "LayerFillColorDefaultBrush");
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
            BorderBrush = GetThemeBrush("SurfaceStrokeColorFlyoutBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = panel
        };
        border.Background = GetThemeBrush("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush");
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

    private static Brush GetThemeBrush(string primaryKey, string fallbackKey)
    {
        if (Application.Current.Resources.TryGetValue(primaryKey, out var primary) && primary is Brush primaryBrush)
        {
            return primaryBrush;
        }

        if (Application.Current.Resources.TryGetValue(fallbackKey, out var fallback) && fallback is Brush fallbackBrush)
        {
            return fallbackBrush;
        }

        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32));
    }
}
