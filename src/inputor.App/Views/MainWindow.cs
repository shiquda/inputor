using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Inputor.App.Models;
using Inputor.App.Services;

namespace Inputor.App.Views;

public sealed class MainWindow : Window
{
    private readonly StatsStore _statsStore;
    private readonly CsvExportService _exporter;
    private readonly Action _openSettings;
    private readonly TextBlock _statusText;
    private readonly TextBlock _currentTargetText;
    private readonly TextBlock _todayText;
    private readonly TextBlock _totalText;
    private readonly TextBlock _adminReminderText;
    private readonly StackPanel _statsPanel;
    private readonly StackPanel _barsPanel;

    public MainWindow(
        StatsStore statsStore,
        AppSettings settings,
        CsvExportService exporter,
        Action openSettings)
    {
        _statsStore = statsStore;
        _exporter = exporter;
        _openSettings = openSettings;

        Width = 980;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;

        _statusText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _currentTargetText = new TextBlock();
        _todayText = CreateMetricTextBlock();
        _totalText = CreateMetricTextBlock();
        _adminReminderText = new TextBlock
        {
            Foreground = Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap
        };
        _statsPanel = new StackPanel { Spacing = 8 };
        _barsPanel = new StackPanel { Spacing = 8 };

        Content = BuildLayout();

        _statsStore.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public event EventHandler? RequestExit;

    private Control BuildLayout()
    {
        var exportButton = new Button { Content = "Export Today CSV", HorizontalAlignment = HorizontalAlignment.Left };
        exportButton.Click += ExportToday;

        var settingsButton = new Button { Content = "Settings", HorizontalAlignment = HorizontalAlignment.Left };
        settingsButton.Click += (_, _) => _openSettings();

        var exitButton = new Button { Content = "Exit", HorizontalAlignment = HorizontalAlignment.Left };
        exitButton.Click += (_, _) => RequestExit?.Invoke(this, EventArgs.Empty);

        var headerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { exportButton, settingsButton, exitButton }
        };

        var metricsPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(0, 12, 0, 12)
        };
        metricsPanel.Children.Add(CreateMetricCard("Today", _todayText));
        var totalCard = CreateMetricCard("All Time", _totalText);
        Grid.SetColumn(totalCard, 1);
        metricsPanel.Children.Add(totalCard);

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,2*"),
            ColumnSpacing = 16
        };

        var statsScroll = new ScrollViewer
        {
            Content = _statsPanel,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        contentGrid.Children.Add(statsScroll);

        var barsCard = new Border
        {
            Padding = new Thickness(16),
            Background = new SolidColorBrush(Color.Parse("#111827")),
            CornerRadius = new CornerRadius(12),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Top Apps", FontSize = 20, FontWeight = FontWeight.SemiBold },
                    _barsPanel
                }
            }
        };
        Grid.SetColumn(barsCard, 1);
        contentGrid.Children.Add(barsCard);

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "inputor", FontSize = 32, FontWeight = FontWeight.Bold },
                    new TextBlock
                    {
                        Text = "Per-app Chinese and English character monitoring via focused UIA text snapshots.",
                        Foreground = Brushes.Gray
                    },
                    headerButtons,
                    _statusText,
                    _currentTargetText,
                    _adminReminderText,
                    metricsPanel,
                    contentGrid
                }
            }
        };
    }

    private static TextBlock CreateMetricTextBlock()
        => new()
        {
            FontSize = 28,
            FontWeight = FontWeight.Bold
        };

    private static Border CreateMetricCard(string title, TextBlock valueBlock)
        => new()
        {
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 12, 0),
            Background = new SolidColorBrush(Color.Parse("#111827")),
            CornerRadius = new CornerRadius(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = title, Foreground = Brushes.Gray },
                    valueBlock
                }
            }
        };

    private void ExportToday(object? sender, RoutedEventArgs e)
    {
        var path = _exporter.ExportToday(_statsStore.GetSnapshot());
        _statsStore.SetStatus($"Exported CSV to {path}", _statsStore.CurrentAppName, _statsStore.IsCurrentTargetSupported);
    }

    private void Refresh()
    {
        var snapshot = _statsStore.GetSnapshot();

        _statusText.Text = $"Status: {snapshot.StatusMessage}";
        _currentTargetText.Text = $"Current target: {snapshot.CurrentAppName} | Supported: {(snapshot.IsCurrentTargetSupported ? "Yes" : "No")}";
        _todayText.Text = snapshot.TotalToday.ToString();
        _totalText.Text = snapshot.TotalAllTime.ToString();
        _adminReminderText.IsVisible = snapshot.ShowAdminReminder;
        _adminReminderText.Text = snapshot.ShowAdminReminder
            ? "Admin reminder: elevated windows and UAC-protected surfaces may not be counted in this MVP."
            : string.Empty;

        _statsPanel.Children.Clear();
        foreach (var stat in snapshot.AppStats)
        {
            _statsPanel.Children.Add(new Border
            {
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.Parse("#0F172A")),
                CornerRadius = new CornerRadius(10),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("2*,*,*"),
                    Children =
                    {
                        new TextBlock { Text = stat.AppName, FontWeight = FontWeight.SemiBold },
                        CreateAlignedText(stat.TodayCount.ToString(), 1),
                        CreateAlignedText(stat.TotalCount.ToString(), 2)
                    }
                }
            });
        }

        _barsPanel.Children.Clear();
        var topStats = snapshot.AppStats.Take(6).ToList();
        var maxToday = topStats.Count == 0 ? 1 : Math.Max(1, topStats.Max(item => item.TodayCount));
        foreach (var stat in topStats)
        {
            var widthRatio = stat.TodayCount / (double)maxToday;
            _barsPanel.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = $"{stat.AppName} ({stat.TodayCount})" },
                    new Border
                    {
                        Height = 14,
                        Width = Math.Max(24, 220 * widthRatio),
                        Background = new SolidColorBrush(Color.Parse("#38BDF8")),
                        CornerRadius = new CornerRadius(6)
                    }
                }
            });
        }
    }

    private static TextBlock CreateAlignedText(string value, int column)
    {
        var text = new TextBlock
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(text, column);
        return text;
    }
}
