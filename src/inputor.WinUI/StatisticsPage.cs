using System;
using System.Collections.Generic;
using System.Linq;
using Inputor.App.Models;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Inputor.WinUI;

public sealed class StatisticsPage : UserControl
{
    private const int TrendDays = 14;
    private const int HeatmapDays = 91;
    private const double TrendWidth = 540;
    private const double TrendHeight = 180;
    private const double PieSize = 220;

    private readonly TextBlock _summaryTextBlock;
    private readonly Canvas _trendCanvas;
    private readonly TextBlock _trendCaptionTextBlock;
    private readonly StackPanel _heatmapPanel;
    private readonly TextBlock _heatmapDetailTextBlock;
    private readonly TextBlock _heatmapCaptionTextBlock;
    private readonly StackPanel _distributionPanel;
    private readonly TextBlock _distributionDetailTextBlock;
    private readonly TextBlock _distributionCaptionTextBlock;
    private readonly ComboBox _distributionRangeComboBox;
    private readonly ComboBox _distributionAggregationComboBox;
    private DashboardSnapshot? _pendingSnapshot;
    private int _interactionDepth;
    private readonly List<Border> _cards = [];

    public StatisticsPage()
    {
        _summaryTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.78 };
        _trendCanvas = new Canvas { Width = TrendWidth, Height = TrendHeight };
        _trendCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _heatmapPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _heatmapDetailTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.82, MinHeight = 24 };
        _heatmapCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _distributionPanel = new StackPanel { Spacing = 10 };
        _distributionDetailTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.82, MinHeight = 24 };
        _distributionCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _distributionRangeComboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = AppStrings.GetDistributionRangeOptions(),
            DisplayMemberPath = nameof(AppRangeOption.DisplayName),
            SelectedValuePath = nameof(AppRangeOption.Value),
            SelectedValue = 0
        };
        _distributionRangeComboBox.SelectionChanged += (_, _) => Refresh(AppPresentationService.CreateVisibleSnapshot(App.Current.StatsStore.GetSnapshot(), App.Current.Settings));
        _distributionAggregationComboBox = new ComboBox
        {
            Width = 160,
            ItemsSource = AppStrings.GetAggregationOptions(),
            DisplayMemberPath = nameof(AppAggregationOption.DisplayName),
            SelectedValuePath = nameof(AppAggregationOption.Tag),
            SelectedValue = "app"
        };
        _distributionAggregationComboBox.SelectionChanged += (_, _) => Refresh(AppPresentationService.CreateVisibleSnapshot(App.Current.StatsStore.GetSnapshot(), App.Current.Settings));

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };

        ThemeBrushes.Changed += ThemeBrushes_Changed;
        Unloaded += (_, _) => ResetInteractionState();
        Unloaded += (_, _) => ThemeBrushes.Changed -= ThemeBrushes_Changed;
    }

    public void RefreshTheme()
    {
        foreach (var card in _cards)
        {
            card.Background = ThemeBrushes.GetCardBackgroundBrush();
            card.BorderBrush = ThemeBrushes.GetCardBorderBrush();
        }
    }

    private void ThemeBrushes_Changed(object? sender, EventArgs e)
    {
        RefreshTheme();
    }

    public void Refresh(DashboardSnapshot snapshot)
    {
        if (_interactionDepth > 0)
        {
            _pendingSnapshot = snapshot;
            return;
        }

        var hasRecordedActivity = snapshot.TotalAllTime > 0 || snapshot.DailyHistory.Any(item => item.TotalCount > 0);
        if (!hasRecordedActivity)
        {
            _summaryTextBlock.Text = AppStrings.Get("Statistics.Summary.Empty");
            RenderTrend([]);
            RenderHeatmap([]);
            RenderDistribution(snapshot);
            return;
        }

        var dailySeries = BuildDailySeries(snapshot).ToList();
        var todayTotal = dailySeries.Count == 0 ? 0 : dailySeries[^1].TotalCount;
        var peakDay = dailySeries.OrderByDescending(item => item.TotalCount).First();

        _summaryTextBlock.Text = AppStrings.Format("Statistics.Summary.Overview", dailySeries.Count, todayTotal, peakDay.Date, peakDay.TotalCount);

        RenderTrend(dailySeries.TakeLast(TrendDays).ToList());
        RenderHeatmap(dailySeries.TakeLast(HeatmapDays).ToList());
        RenderDistribution(snapshot);
    }

    public void ResetInteractionState()
    {
        _interactionDepth = 0;
        ApplyPendingRefresh();
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Padding = new Thickness(28, 24, 28, 28), Spacing = 20 };

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = AppStrings.Get("Statistics.Title"), FontSize = 28, FontWeight = FontWeights.SemiBold });
        header.Children.Add(_summaryTextBlock);
        root.Children.Add(header);

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Statistics.Section.TrendTitle"), AppStrings.Get("Statistics.Section.TrendSubtitle")));
        var trendPanel = new StackPanel { Spacing = 12 };
        trendPanel.Children.Add(_trendCanvas);
        trendPanel.Children.Add(_trendCaptionTextBlock);
        root.Children.Add(CreateCard(trendPanel));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Statistics.Section.HeatmapTitle"), AppStrings.Get("Statistics.Section.HeatmapSubtitle")));
        var heatmapPanel = new StackPanel { Spacing = 12 };
        heatmapPanel.Children.Add(_heatmapPanel);
        heatmapPanel.Children.Add(_heatmapDetailTextBlock);
        heatmapPanel.Children.Add(_heatmapCaptionTextBlock);
        root.Children.Add(CreateCard(heatmapPanel));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Statistics.Section.DistributionTitle"), AppStrings.Get("Statistics.Section.DistributionSubtitle")));
        var distributionPanel = new StackPanel { Spacing = 12 };
        var distributionToolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        distributionToolbar.Children.Add(_distributionAggregationComboBox);
        distributionToolbar.Children.Add(_distributionRangeComboBox);
        distributionPanel.Children.Add(distributionToolbar);
        distributionPanel.Children.Add(_distributionPanel);
        distributionPanel.Children.Add(_distributionDetailTextBlock);
        distributionPanel.Children.Add(_distributionCaptionTextBlock);
        root.Children.Add(CreateCard(distributionPanel));

        return root;
    }

    private void RenderTrend(IReadOnlyList<DailyTotalEntry> data)
    {
        _trendCanvas.Children.Clear();
        if (data.Count == 0)
        {
            _trendCaptionTextBlock.Text = AppStrings.Get("Statistics.Trend.Empty");
            return;
        }

        var maxValue = Math.Max(1, data.Max(item => item.TotalCount));
        var xStep = data.Count == 1 ? 0 : TrendWidth / (data.Count - 1);
        Point? previous = null;

        for (var index = 0; index < data.Count; index++)
        {
            var entry = data[index];
            var point = new Point(index * xStep, TrendHeight - ((double)entry.TotalCount / maxValue * (TrendHeight - 24)) - 12);
            if (previous is not null)
            {
                _trendCanvas.Children.Add(new Line
                {
                    X1 = previous.Value.X,
                    Y1 = previous.Value.Y,
                    X2 = point.X,
                    Y2 = point.Y,
                    Stroke = new SolidColorBrush(ThemeBrushes.GetChartAccentColor()),
                    StrokeThickness = 3
                });
            }

            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = ThemeBrushes.GetChartMarkerFillBrush(),
                Stroke = new SolidColorBrush(ThemeBrushes.GetChartAccentColor()),
                StrokeThickness = 2
            };
            Canvas.SetLeft(marker, point.X - 4);
            Canvas.SetTop(marker, point.Y - 4);
            _trendCanvas.Children.Add(marker);

            if (index == 0 || index == data.Count - 1 || index == data.Count / 2)
            {
                var label = new TextBlock
                {
                    Text = AppStrings.Format("Statistics.Trend.AxisDate", entry.Date),
                    FontSize = 11,
                    Opacity = 0.7
                };
                Canvas.SetLeft(label, Math.Max(0, Math.Min(TrendWidth - 42, point.X - 16)));
                Canvas.SetTop(label, TrendHeight - 16);
                _trendCanvas.Children.Add(label);
            }

            previous = point;
        }

        _trendCaptionTextBlock.Text = AppStrings.Format("Statistics.Trend.Caption", data.Count, maxValue);
    }

    private void RenderHeatmap(IReadOnlyList<DailyTotalEntry> data)
    {
        _heatmapPanel.Children.Clear();
        if (data.Count == 0)
        {
            _heatmapDetailTextBlock.Text = string.Empty;
            _heatmapCaptionTextBlock.Text = AppStrings.Get("Statistics.Heatmap.Empty");
            return;
        }

        var maxValue = Math.Max(1, data.Max(item => item.TotalCount));
        var busiestDay = data.OrderByDescending(item => item.TotalCount).First();
        _heatmapDetailTextBlock.Text = AppStrings.Format("Statistics.Heatmap.Detail.Default", busiestDay.Date, busiestDay.TotalCount);
        foreach (var week in ChunkByWeek(data))
        {
            var weekColumn = new StackPanel { Spacing = 6 };
            foreach (var entry in week)
            {
                var intensity = (byte)(entry.TotalCount == 0 ? 32 : 64 + (int)Math.Round(entry.TotalCount * 191.0 / maxValue));
                var cell = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    Background = ThemeBrushes.GetHeatmapCellBrush(intensity),
                    BorderThickness = new Thickness(1),
                    BorderBrush = ThemeBrushes.GetHeatmapBorderBrush()
                };
                ToolTipService.SetToolTip(cell, new ToolTip
                {
                    Content = AppStrings.Format("Statistics.Heatmap.Tooltip", entry.Date, entry.TotalCount)
                });
                cell.PointerEntered += (_, _) =>
                {
                    BeginInteraction();
                    cell.BorderBrush = ThemeBrushes.GetHeatmapBorderBrush(true);
                    cell.Opacity = 0.94;
                    _heatmapDetailTextBlock.Text = AppStrings.Format("Statistics.Heatmap.Detail.Entry", entry.Date, entry.TotalCount);
                };
                cell.PointerExited += (_, _) =>
                {
                    cell.BorderBrush = ThemeBrushes.GetHeatmapBorderBrush();
                    cell.Opacity = 1;
                    _heatmapDetailTextBlock.Text = AppStrings.Format("Statistics.Heatmap.Detail.Default", busiestDay.Date, busiestDay.TotalCount);
                    EndInteraction();
                };
                weekColumn.Children.Add(cell);
            }

            _heatmapPanel.Children.Add(weekColumn);
        }

        _heatmapCaptionTextBlock.Text = AppStrings.Format("Statistics.Heatmap.Caption", data.First().Date);
    }

    private void RenderDistribution(DashboardSnapshot snapshot)
    {
        _distributionPanel.Children.Clear();
        var range = _distributionRangeComboBox.SelectedValue is int selectedRange ? selectedRange : _distributionRangeComboBox.SelectedIndex;
        var aggregateByTag = (_distributionAggregationComboBox.SelectedValue as string) == "tag";
        var ordered = BuildDistributionData(snapshot, range, aggregateByTag)
            .OrderByDescending(item => item.Value)
            .Take(6)
            .ToList();
        AppPresentationService.WarmIcons(ordered.SelectMany(item => item.Aggregate.ProcessNames).ToList(), DispatcherQueue.GetForCurrentThread());

        if (ordered.Count == 0)
        {
            _distributionDetailTextBlock.Text = string.Empty;
            _distributionPanel.Children.Add(new TextBlock
            {
                Text = AppStrings.Get("Statistics.Distribution.Empty"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.78
            });
            _distributionCaptionTextBlock.Text = AppStrings.Get("Statistics.Distribution.EmptyCaption");
            return;
        }

        var total = Math.Max(1, ordered.Sum(item => item.Value));
        var contentGrid = new Grid { ColumnSpacing = 20 };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var pieHost = new Grid { Width = PieSize, Height = PieSize };
        var pieCanvas = new Canvas { Width = PieSize, Height = PieSize };
        pieHost.Children.Add(pieCanvas);

        var centerBadge = new Border
        {
            Width = 92,
            Height = 92,
            CornerRadius = new CornerRadius(999),
            Background = ThemeBrushes.GetSubtleSurfaceBrush(),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.GetCardBorderBrush(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = total.ToString("N0"),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 20
            }
        };
        pieHost.Children.Add(centerBadge);

        var palette = ThemeBrushes.GetDistributionPalette();

        var startAngle = -90.0;
        _distributionDetailTextBlock.Text = AppStrings.Get("Statistics.Distribution.Detail.Default");
        var defaultDetailText = _distributionDetailTextBlock.Text;
        Microsoft.UI.Xaml.Shapes.Path? selectedSlice = null;
        for (var index = 0; index < ordered.Count; index++)
        {
            var item = ordered[index];
            var share = item.Value / (double)total;
            var sweepAngle = share * 360.0;
            var slice = CreatePieSlice(PieSize / 2, PieSize / 2, PieSize / 2 - 6, startAngle, sweepAngle, palette[index % palette.Count]);
            ToolTipService.SetToolTip(slice, new ToolTip
            {
                Content = AppStrings.Format("Statistics.Distribution.Tooltip", item.Aggregate.DisplayName, item.Value, share)
            });

            void SetSliceState(bool active)
            {
                slice.Stroke = ThemeBrushes.GetChartOutlineBrush(active);
                slice.Opacity = active ? 1 : 0.95;
            }

            void UpdateSliceDetail(bool active)
            {
                _distributionDetailTextBlock.Text = active
                    ? AppStrings.Format("Statistics.Distribution.Detail.Entry", item.Aggregate.DisplayName, item.Value, share)
                    : defaultDetailText;
            }

            slice.PointerEntered += (_, _) =>
            {
                BeginInteraction();
                if (!ReferenceEquals(selectedSlice, slice))
                {
                    SetSliceState(true);
                }

                UpdateSliceDetail(true);
            };
            slice.PointerExited += (_, _) =>
            {
                if (!ReferenceEquals(selectedSlice, slice))
                {
                    SetSliceState(false);
                }

                UpdateSliceDetail(ReferenceEquals(selectedSlice, slice));
                EndInteraction();
            };
            slice.Tapped += (_, _) =>
            {
                if (selectedSlice is not null && !ReferenceEquals(selectedSlice, slice))
                {
                    selectedSlice.Stroke = ThemeBrushes.GetChartOutlineBrush();
                    selectedSlice.Opacity = 0.95;
                }

                selectedSlice = slice;
                SetSliceState(true);
                UpdateSliceDetail(true);
            };
            pieCanvas.Children.Add(slice);
            startAngle += sweepAngle;
        }

        contentGrid.Children.Add(pieHost);

        var legend = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        for (var index = 0; index < ordered.Count; index++)
        {
            var item = ordered[index];
            var share = item.Value / (double)total;
            legend.Children.Add(CreateDistributionLegendRow(item.Aggregate, item.Value, share, palette[index % palette.Count], defaultDetailText, detail =>
            {
                _distributionDetailTextBlock.Text = detail;
            }));
        }

        Grid.SetColumn(legend, 1);
        contentGrid.Children.Add(legend);
        _distributionPanel.Children.Add(contentGrid);

        _distributionCaptionTextBlock.Text = range switch
        {
            0 => AppStrings.Get(aggregateByTag ? "Statistics.Distribution.Caption.Tag.Today" : "Statistics.Distribution.Caption.App.Today"),
            1 => AppStrings.Get(aggregateByTag ? "Statistics.Distribution.Caption.Tag.Last7" : "Statistics.Distribution.Caption.App.Last7"),
            2 => AppStrings.Get(aggregateByTag ? "Statistics.Distribution.Caption.Tag.Last30" : "Statistics.Distribution.Caption.App.Last30"),
            _ => AppStrings.Get(aggregateByTag ? "Statistics.Distribution.Caption.Tag.AllTime" : "Statistics.Distribution.Caption.App.AllTime")
        };

    }

    private static IReadOnlyList<DistributionSlice> BuildDistributionData(DashboardSnapshot snapshot, int range, bool aggregateByTag)
    {
        if (range == 0)
        {
            var groupedToday = aggregateByTag
                ? AppPresentationService.BuildTagAggregates(snapshot.AppStats, App.Current.Settings)
                : AppPresentationService.BuildAggregates(snapshot.AppStats, App.Current.Settings);
            return groupedToday
                .Where(item => item.TodayCount > 0)
                .Select(item => new DistributionSlice { Aggregate = item, Value = item.TodayCount })
                .ToList();
        }

        if (range == 3)
        {
            var groupedAllTime = aggregateByTag
                ? AppPresentationService.BuildTagAggregates(snapshot.AppStats, App.Current.Settings)
                : AppPresentationService.BuildAggregates(snapshot.AppStats, App.Current.Settings);
            return groupedAllTime
                .Where(item => item.TotalCount > 0)
                .Select(item => new DistributionSlice { Aggregate = item, Value = item.TotalCount })
                .ToList();
        }

        var days = range == 1 ? 7 : 30;
        var cutoff = snapshot.Today.AddDays(-(days - 1));
        var rawStats = snapshot.DailyAppHistory
            .Where(item => item.Date >= cutoff)
            .GroupBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppStat
            {
                AppName = group.First().AppName,
                TodayCount = group.Sum(item => item.TotalCount),
                SessionCount = group.Sum(item => item.TotalCount),
                TotalCount = group.Sum(item => item.TotalCount)
            })
            .ToList();

        var aggregates = aggregateByTag
            ? AppPresentationService.BuildTagAggregates(rawStats, App.Current.Settings)
            : AppPresentationService.BuildAggregates(rawStats, App.Current.Settings);

        return aggregates
            .Where(item => item.TodayCount > 0)
            .Select(item => new DistributionSlice { Aggregate = item, Value = item.TodayCount })
            .ToList();
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double sweepAngle, Windows.UI.Color color)
    {
        if (sweepAngle >= 359.99)
        {
            return new Microsoft.UI.Xaml.Shapes.Path
            {
                Fill = new SolidColorBrush(color),
                Data = new EllipseGeometry { Center = new Point(centerX, centerY), RadiusX = radius, RadiusY = radius }
            };
        }

        var startRadians = Math.PI * startAngle / 180.0;
        var endRadians = Math.PI * (startAngle + sweepAngle) / 180.0;
        var startPoint = new Point(centerX + radius * Math.Cos(startRadians), centerY + radius * Math.Sin(startRadians));
        var endPoint = new Point(centerX + radius * Math.Cos(endRadians), centerY + radius * Math.Sin(endRadians));

        var figure = new PathFigure { StartPoint = new Point(centerX, centerY), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment { Point = startPoint });
        figure.Segments.Add(new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Fill = new SolidColorBrush(color),
            Data = geometry,
            Stroke = ThemeBrushes.GetChartOutlineBrush(),
            StrokeThickness = 1
        };
    }

    private UIElement CreateDistributionLegendRow(AppAggregate aggregate, int value, double share, Windows.UI.Color color, string defaultDetail, Action<string> updateDetail)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconSource = AppPresentationService.TryGetIconSource(aggregate.ProcessNames);
        var swatch = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(999),
            Background = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(swatch);

        var iconBadge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = ThemeBrushes.GetSubtleSurfaceBrush(),
            VerticalAlignment = VerticalAlignment.Center,
            Child = iconSource is not null
            ? new Image
            {
                Source = iconSource,
                Width = 18,
                Height = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            : new FontIcon
            {
                Glyph = aggregate.IconGlyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                Foreground = ThemeBrushes.IsDarkTheme() ? new SolidColorBrush(Colors.White) : ThemeBrushes.Get("TextFillColorPrimaryBrush", "TextFillColorPrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(iconBadge, 1);
        row.Children.Add(iconBadge);

        var textPanel = new StackPanel { Spacing = 2 };
        textPanel.Children.Add(new TextBlock { Text = aggregate.DisplayName, FontWeight = FontWeights.SemiBold });
        textPanel.Children.Add(new TextBlock { Text = AppStrings.Format("Statistics.Distribution.Row", value, share), Opacity = 0.72 });
        Grid.SetColumn(textPanel, 2);
        row.Children.Add(textPanel);

        var container = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(10),
            Background = ThemeBrushes.GetSubtleSurfaceBrush(),
            Child = row
        };
        var detail = AppStrings.Format("Statistics.Distribution.Detail.Entry", aggregate.DisplayName, value, share);
        AppQuickActionService.AttachContextMenu(container, aggregate, BeginInteraction, EndInteraction);
        container.PointerEntered += (_, _) =>
        {
            BeginInteraction();
            updateDetail(detail);
        };
        container.PointerExited += (_, _) =>
        {
            updateDetail(defaultDetail);
            EndInteraction();
        };
        container.Tapped += (_, _) => updateDetail(detail);
        return container;
    }

    private void BeginInteraction()
    {
        _interactionDepth++;
    }

    private void EndInteraction()
    {
        if (_interactionDepth == 0)
        {
            return;
        }

        _interactionDepth--;
        if (_interactionDepth == 0)
        {
            ApplyPendingRefresh();
        }
    }

    private void ApplyPendingRefresh()
    {
        if (_pendingSnapshot is null)
        {
            return;
        }

        var snapshot = _pendingSnapshot;
        _pendingSnapshot = null;
        Refresh(snapshot);
    }

    private sealed class DistributionSlice
    {
        public required AppAggregate Aggregate { get; init; }
        public required int Value { get; init; }
    }

    private static IEnumerable<DailyTotalEntry> BuildDailySeries(DashboardSnapshot snapshot)
    {
        var historyByDate = snapshot.DailyHistory.ToDictionary(item => item.Date, item => item.TotalCount);
        var start = snapshot.Today.AddDays(-(HeatmapDays - 1));
        for (var offset = 0; offset < HeatmapDays; offset++)
        {
            var date = start.AddDays(offset);
            yield return new DailyTotalEntry
            {
                Date = date,
                TotalCount = historyByDate.TryGetValue(date, out var total) ? total : 0
            };
        }
    }

    private static IEnumerable<IReadOnlyList<DailyTotalEntry>> ChunkByWeek(IReadOnlyList<DailyTotalEntry> data)
    {
        for (var index = 0; index < data.Count; index += 7)
        {
            yield return data.Skip(index).Take(7).ToList();
        }
    }

    private static UIElement CreateSectionHeader(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private Border CreateCard(UIElement child, Brush? background = null, Thickness? padding = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = padding ?? new Thickness(20),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.GetCardBorderBrush(),
            Child = child
        };
        border.Background = background ?? ThemeBrushes.GetCardBackgroundBrush();
        _cards.Add(border);
        return border;
    }
}
