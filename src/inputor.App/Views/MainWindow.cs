using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Inputor.App.Models;
using Inputor.App.Services;

namespace Inputor.App.Views;

public sealed class MainWindow : Window
{
    private readonly StatsStore _statsStore;
    private readonly AppSettings _settings;
    private readonly CsvExportService _exporter;
    private readonly Action _openSettings;
    private readonly Action _togglePause;
    private readonly Action _resetSession;
    private readonly Action _excludeCurrentApp;

    private readonly NavigationView _navigationView;
    private readonly ContentControl _pageHost;
    private readonly NavigationViewItem _overviewItem;
    private readonly NavigationViewItem _appsItem;

    private readonly TextBlock _todayText;
    private readonly TextBlock _sessionText;
    private readonly TextBlock _totalText;
    private readonly TextBlock _overviewTargetText;
    private readonly TextBlock _overviewStatusText;
    private readonly TextBlock _goalSummaryText;
    private readonly TextBlock _goalMilestoneText;
    private readonly ProgressBar _goalProgressBar;
    private readonly StackPanel _topAppsPanel;
    private readonly StackPanel _recentActivityPanel;

    private readonly Button _pauseButton;
    private readonly Button _excludeButton;

    private readonly TextBlock _appsSummaryText;
    private readonly TextBox _searchBox;
    private readonly ComboBox _sortComboBox;
    private readonly StackPanel _allAppsPanel;

    private readonly Control _overviewPage;
    private readonly Control _appsPage;

    public MainWindow(
        StatsStore statsStore,
        AppSettings settings,
        CsvExportService exporter,
        Action openSettings,
        Action togglePause,
        Action resetSession,
        Action excludeCurrentApp)
    {
        _statsStore = statsStore;
        _settings = settings;
        _exporter = exporter;
        _openSettings = openSettings;
        _togglePause = togglePause;
        _resetSession = resetSession;
        _excludeCurrentApp = excludeCurrentApp;

        Width = 1120;
        Height = 760;
        MinWidth = 920;
        MinHeight = 620;
        Background = new SolidColorBrush(Color.Parse("#F5F6F8"));

        _todayText = CreateMetricTextBlock();
        _sessionText = CreateMetricTextBlock();
        _totalText = CreateMetricTextBlock();
        _overviewTargetText = CreateSecondaryTextBlock();
        _overviewStatusText = CreateSecondaryTextBlock();
        _goalSummaryText = CreateSecondaryTextBlock();
        _goalMilestoneText = CreateSecondaryTextBlock();
        _goalProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            Margin = new Thickness(0, 2, 0, 2)
        };
        _topAppsPanel = new StackPanel { Spacing = 8 };
        _recentActivityPanel = new StackPanel { Spacing = 8 };

        _pauseButton = CreateActionButton("Pause monitoring", (_, _) => _togglePause());
        _excludeButton = CreateActionButton("Exclude current app", (_, _) => _excludeCurrentApp(), true);

        _appsSummaryText = CreateSecondaryTextBlock();
        _searchBox = new TextBox { Width = 220, Watermark = "Search apps" };
        _searchBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                Refresh();
            }
        };
        _sortComboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "Today", "Session", "All time", "Name" },
            SelectedIndex = 0
        };
        _sortComboBox.SelectionChanged += (_, _) => Refresh();
        _allAppsPanel = new StackPanel { Spacing = 8 };

        _overviewPage = BuildOverviewPage();
        _appsPage = BuildAppsPage();

        _pageHost = new ContentControl { Content = _overviewPage };
        _overviewItem = CreateNavigationItem("Overview", Symbol.Home, "overview");
        _appsItem = CreateNavigationItem("Apps", Symbol.Library, "apps");

        _navigationView = new NavigationView
        {
            IsBackButtonVisible = false,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            OpenPaneLength = 250,
            CompactPaneLength = 52,
            Header = BuildHeader(),
            Content = _pageHost,
            MenuItems =
            {
                _overviewItem,
                _appsItem
            },
            SelectedItem = _overviewItem
        };
        _navigationView.ItemInvoked += OnNavigationItemInvoked;

        Content = _navigationView;

        _statsStore.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private Control BuildHeader()
    {
        var titleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "inputor",
                            FontSize = 24,
                            FontWeight = FontWeight.Bold
                        },
                        CreateSecondaryTextBlock("Privacy-safe input statistics with direct actions and app-level visibility.")
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        CreateHeaderButton("Export today", (_, _) => ExportToday()),
                        CreateHeaderButton("Settings", (_, _) => _openSettings())
                    }
                }.WithColumn(1)
            }
        };

        return new Border
        {
            Padding = new Thickness(0, 0, 0, 8),
            Child = titleRow
        };
    }

    private Control BuildOverviewPage()
    {
        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = double.NaN,
            Margin = new Thickness(0),
            Children =
            {
                _pauseButton,
                _excludeButton,
                CreateActionButton("Reset session", (_, _) => _resetSession(), true),
                CreateActionButton("Export today", (_, _) => ExportToday(), true),
                CreateActionButton("Open settings", (_, _) => _openSettings(), true),
                CreateActionButton("View all apps", (_, _) => ShowAppsPage(), true)
            }
        };

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    CreateSectionCard(
                        "Overview",
                        "Today's work, the current target, and your goal progress in one place.",
                        new StackPanel
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                                    ColumnSpacing = 12,
                                    Children =
                                    {
                                        CreateMetricCard("Today", _todayText),
                                        CreateMetricCard("Session", _sessionText).WithColumn(1),
                                        CreateMetricCard("All time", _totalText).WithColumn(2)
                                    }
                                },
                                new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                                    ColumnSpacing = 12,
                                    Children =
                                    {
                                        CreateInfoCard("Current target", _overviewTargetText),
                                        CreateInfoCard("Status", _overviewStatusText).WithColumn(1)
                                    }
                                },
                                CreateGoalCard()
                            }
                        }),
                    CreateSectionCard(
                        "Quick actions",
                        "The same direct utility actions you expect from a tray-first Windows tool.",
                        actions),
                    CreateSectionCard(
                        "Activity",
                        "Recent deltas keep the overview useful without turning it into a diagnostic page.",
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,*"),
                            ColumnSpacing = 16,
                            Children =
                            {
                                CreateInlineSection("Recent activity", _recentActivityPanel),
                                CreateInlineSection("Top apps today", _topAppsPanel).WithColumn(1)
                            }
                        })
                }
            }
        };
    }

    private Control BuildAppsPage()
    {
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    CreateSectionCard(
                        "Apps",
                        "Search, sort, and compare activity without leaving the main window.",
                        new StackPanel
                        {
                            Spacing = 14,
                            Children =
                            {
                                new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                                    ColumnSpacing = 12,
                                    Children =
                                    {
                                        _appsSummaryText,
                                        _searchBox.WithColumn(1),
                                        _sortComboBox.WithColumn(2)
                                    }
                                },
                                _allAppsPanel
                            }
                        })
                }
            }
        };
    }

    private Border CreateGoalCard()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Daily goal",
                        FontSize = 14,
                        FontWeight = FontWeight.SemiBold
                    },
                    _goalSummaryText,
                    _goalProgressBar,
                    _goalMilestoneText
                }
            }
        };
    }

    private static Border CreateMetricCard(string label, TextBlock value)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    CreateSecondaryTextBlock(label),
                    value
                }
            }
        };
    }

    private static Border CreateInlineSection(string title, Control body)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 14,
                        FontWeight = FontWeight.SemiBold
                    },
                    body
                }
            }
        };
    }

    private static NavigationViewItem CreateNavigationItem(string text, Symbol symbol, string tag)
    {
        return new NavigationViewItem
        {
            Content = text,
            IconSource = new SymbolIconSource { Symbol = symbol },
            Tag = tag
        };
    }

    private void OnNavigationItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is not NavigationViewItem item)
        {
            return;
        }

        _navigationView.SelectedItem = item;
        _pageHost.Content = item.Tag as string == "apps" ? _appsPage : _overviewPage;
        Refresh();
    }

    private void ShowAppsPage()
    {
        _navigationView.SelectedItem = _appsItem;
        _pageHost.Content = _appsPage;
        Refresh();
    }

    private static TextBlock CreateMetricTextBlock()
    {
        return new TextBlock
        {
            FontSize = 36,
            FontWeight = FontWeight.Bold
        };
    }

    private static TextBlock CreateSecondaryTextBlock(string? text = null)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#5B6472")),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Border CreateSectionCard(string title, string subtitle, Control body)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold },
                            CreateSecondaryTextBlock(subtitle)
                        }
                    },
                    body
                }
            }
        };
    }

    private static Border CreateInfoCard(string title, TextBlock value)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#5B6472"))
                    },
                    value
                }
            }
        };
    }

    private static Button CreateHeaderButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D7DEE7")),
            BorderThickness = new Thickness(1)
        };
        button.Click += onClick;
        return button;
    }

    private static Button CreateActionButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick, bool secondary = false)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(16, 10),
            CornerRadius = new CornerRadius(10),
            Background = secondary ? Brushes.White : new SolidColorBrush(Color.Parse("#0F6CBD")),
            Foreground = secondary ? new SolidColorBrush(Color.Parse("#111827")) : Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D7DEE7")),
            BorderThickness = new Thickness(1)
        };
        button.Click += onClick;
        return button;
    }

    private void ExportToday()
    {
        var path = _exporter.ExportToday(_statsStore.GetSnapshot());
        _statsStore.SetStatus($"Exported CSV to {path}", _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported);
    }

    private void Refresh()
    {
        var snapshot = _statsStore.GetSnapshot();

        _todayText.Text = snapshot.TotalToday.ToString("N0");
        _sessionText.Text = snapshot.TotalSession.ToString("N0");
        _totalText.Text = snapshot.TotalAllTime.ToString("N0");
        _overviewTargetText.Text = $"{snapshot.CurrentAppName} • {(snapshot.IsCurrentTargetSupported ? "Supported" : "Unsupported")}";
        _overviewStatusText.Text = snapshot.IsPaused
            ? "Monitoring paused. Resume when you want inputor to continue counting."
            : snapshot.StatusMessage;

        UpdateGoal(snapshot);
        UpdateQuickActions(snapshot);
        UpdateRecentActivity(snapshot);
        UpdateTopApps(snapshot);
        UpdateAppList(snapshot);
    }

    private void UpdateGoal(DashboardSnapshot snapshot)
    {
        var dailyGoal = Math.Max(0, _settings.DailyGoal);
        if (dailyGoal == 0)
        {
            _goalSummaryText.Text = "Daily goal is disabled. Set a value in Settings to track progress.";
            _goalMilestoneText.Text = $"Session started at {snapshot.SessionStartedAt:HH:mm}.";
            _goalProgressBar.Value = 0;
            return;
        }

        var progress = Math.Min(100, snapshot.TotalToday * 100.0 / dailyGoal);
        var remaining = Math.Max(0, dailyGoal - snapshot.TotalToday);
        var nextMilestone = GetNextMilestone(snapshot.TotalToday, dailyGoal);

        _goalSummaryText.Text = remaining == 0
            ? $"Goal reached. {snapshot.TotalToday - dailyGoal:N0} above today's target of {dailyGoal:N0}."
            : $"{remaining:N0} characters remaining to hit today's goal of {dailyGoal:N0}.";
        _goalMilestoneText.Text = nextMilestone <= snapshot.TotalToday
            ? $"Session started at {snapshot.SessionStartedAt:HH:mm}."
            : $"Next milestone: {nextMilestone:N0}. Session started at {snapshot.SessionStartedAt:HH:mm}.";
        _goalProgressBar.Value = progress;
    }

    private void UpdateQuickActions(DashboardSnapshot snapshot)
    {
        _pauseButton.Content = snapshot.IsPaused ? "Resume monitoring" : "Pause monitoring";
        _excludeButton.IsEnabled = CanExclude(snapshot.CurrentAppName);
    }

    private void UpdateRecentActivity(DashboardSnapshot snapshot)
    {
        _recentActivityPanel.Children.Clear();
        if (snapshot.RecentActivity.Count == 0)
        {
            _recentActivityPanel.Children.Add(CreateSecondaryTextBlock("No recorded input yet in this session."));
            return;
        }

        foreach (var item in snapshot.RecentActivity)
        {
            _recentActivityPanel.Children.Add(CreateRecentActivityRow(item));
        }
    }

    private void UpdateTopApps(DashboardSnapshot snapshot)
    {
        _topAppsPanel.Children.Clear();
        foreach (var stat in snapshot.AppStats.Take(5))
        {
            _topAppsPanel.Children.Add(CreateTopAppRow(stat, snapshot.TotalToday));
        }

        if (_topAppsPanel.Children.Count == 0)
        {
            _topAppsPanel.Children.Add(CreateSecondaryTextBlock("No activity yet."));
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
        _appsSummaryText.Text = filtered.Count == 0
            ? "No apps match the current filter."
            : $"{filtered.Count} apps • {snapshot.TotalToday:N0} today • {snapshot.TotalSession:N0} session • {snapshot.TotalAllTime:N0} total";

        _allAppsPanel.Children.Clear();
        foreach (var stat in filtered)
        {
            _allAppsPanel.Children.Add(CreateAppRow(stat, snapshot.TotalToday));
        }
    }

    private static Border CreateRecentActivityRow(RecentActivityEntry entry)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = entry.AppName,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    CreateSmallValueBlock($"+{entry.Delta}", 1),
                    CreateSmallValueBlock(entry.Timestamp.ToString("HH:mm:ss"), 2)
                }
            }
        };
    }

    private static Border CreateTopAppRow(AppStat stat, int totalToday)
    {
        var percentage = totalToday <= 0 ? 0 : stat.TodayCount * 100.0 / totalToday;
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = stat.AppName,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    CreateSmallValueBlock($"{stat.TodayCount:N0} today", 1),
                    CreateSmallValueBlock($"{percentage:0.#}%", 2)
                }
            }
        };
    }

    private static Border CreateAppRow(AppStat stat, int totalToday)
    {
        var percentage = totalToday <= 0 ? 0 : stat.TodayCount * 100.0 / totalToday;
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("3*,Auto,Auto,Auto,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = stat.AppName,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    CreateSmallValueBlock($"{stat.TodayCount:N0} today", 1),
                    CreateSmallValueBlock($"{stat.SessionCount:N0} session", 2),
                    CreateSmallValueBlock($"{stat.TotalCount:N0} total", 3),
                    CreateSmallValueBlock($"{percentage:0.#}%", 4)
                }
            }
        };
    }

    private static TextBlock CreateSmallValueBlock(string text, int column)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#5B6472")),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(value, column);
        return value;
    }

    private static bool CanExclude(string appName)
    {
        return !string.IsNullOrWhiteSpace(appName)
            && !string.Equals(appName, "Idle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(appName, "Unavailable", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(appName, "inputor.App", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetNextMilestone(int totalToday, int dailyGoal)
    {
        if (totalToday < dailyGoal)
        {
            return dailyGoal;
        }

        var step = Math.Max(250, dailyGoal / 2);
        return ((totalToday / step) + 1) * step;
    }
}

internal static class LayoutExtensions
{
    public static T WithColumn<T>(this T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
