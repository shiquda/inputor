using System;
using System.Collections.Generic;
using System.Linq;
using Inputor.App.Models;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Inputor.WinUI;

public sealed class StatisticsPage : UserControl
{
    private const int TrendDays = 14;
    private const int HeatmapDays = 91;
    private const double TrendWidth = 540;
    private const double TrendHeight = 180;
    private const double DistributionBarWidth = 240;

    private readonly TextBlock _summaryTextBlock;
    private readonly Canvas _trendCanvas;
    private readonly TextBlock _trendCaptionTextBlock;
    private readonly StackPanel _heatmapPanel;
    private readonly TextBlock _heatmapCaptionTextBlock;
    private readonly StackPanel _distributionPanel;
    private readonly TextBlock _distributionCaptionTextBlock;

    public StatisticsPage()
    {
        _summaryTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.78 };
        _trendCanvas = new Canvas { Width = TrendWidth, Height = TrendHeight };
        _trendCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _heatmapPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _heatmapCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _distributionPanel = new StackPanel { Spacing = 10 };
        _distributionCaptionTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };
    }

    public void Refresh(DashboardSnapshot snapshot)
    {
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

    private UIElement BuildContent()
    {
        var root = new StackPanel { Padding = new Thickness(28, 24, 28, 28), Spacing = 20 };

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = AppStrings.Get("Statistics.Title"), FontSize = 28, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("Statistics.Subtitle"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
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
        heatmapPanel.Children.Add(_heatmapCaptionTextBlock);
        root.Children.Add(CreateCard(heatmapPanel));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Statistics.Section.DistributionTitle"), AppStrings.Get("Statistics.Section.DistributionSubtitle")));
        var distributionPanel = new StackPanel { Spacing = 12 };
        distributionPanel.Children.Add(_distributionPanel);
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
                    Stroke = new SolidColorBrush(ColorHelper.FromArgb(255, 46, 156, 202)),
                    StrokeThickness = 3
                });
            }

            var marker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255)),
                Stroke = new SolidColorBrush(ColorHelper.FromArgb(255, 46, 156, 202)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(marker, point.X - 4);
            Canvas.SetTop(marker, point.Y - 4);
            _trendCanvas.Children.Add(marker);

            if (index == 0 || index == data.Count - 1 || index == data.Count / 2)
            {
                var label = new TextBlock
                {
                    Text = entry.Date.ToString("MM-dd"),
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
            _heatmapCaptionTextBlock.Text = AppStrings.Get("Statistics.Heatmap.Empty");
            return;
        }

        var maxValue = Math.Max(1, data.Max(item => item.TotalCount));
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
                    Background = new SolidColorBrush(ColorHelper.FromArgb(intensity, 46, 156, 202))
                };
                ToolTipService.SetToolTip(cell, AppStrings.Format("Statistics.Heatmap.Tooltip", entry.Date, entry.TotalCount));
                weekColumn.Children.Add(cell);
            }

            _heatmapPanel.Children.Add(weekColumn);
        }

        _heatmapCaptionTextBlock.Text = AppStrings.Format("Statistics.Heatmap.Caption", data.First().Date);
    }

    private void RenderDistribution(DashboardSnapshot snapshot)
    {
        _distributionPanel.Children.Clear();
        var useToday = snapshot.TotalToday > 0;
        var ordered = snapshot.AppStats
            .Select(item => new
            {
                item.AppName,
                Value = useToday ? item.TodayCount : item.TotalCount
            })
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .Take(5)
            .ToList();

        if (ordered.Count == 0)
        {
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
        foreach (var item in ordered)
        {
            var share = item.Value / (double)total;
            var row = new StackPanel { Spacing = 6 };
            row.Children.Add(new TextBlock { Text = item.AppName, FontWeight = FontWeights.SemiBold });
            row.Children.Add(new TextBlock { Text = AppStrings.Format("Statistics.Distribution.Row", item.Value, share), Opacity = 0.72 });

            var track = new Border
            {
                Width = DistributionBarWidth,
                Height = 10,
                CornerRadius = new CornerRadius(999),
                Background = ThemeBrushes.Get("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"),
                Child = new Border
                {
                    Width = DistributionBarWidth * share,
                    Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(999),
                    Background = new SolidColorBrush(ColorHelper.FromArgb(255, 46, 156, 202))
                }
            };
            row.Children.Add(track);
            _distributionPanel.Children.Add(CreateCard(row, ThemeBrushes.Get("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"), new Thickness(16, 14, 16, 14)));
        }

        _distributionCaptionTextBlock.Text = useToday
            ? AppStrings.Get("Statistics.Distribution.Today")
            : AppStrings.Get("Statistics.Distribution.AllTimeFallback");
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

    private static Border CreateCard(UIElement child, Brush? background = null, Thickness? padding = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = padding ?? new Thickness(20),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.Get("CardStrokeColorDefaultBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = child
        };
        border.Background = background ?? ThemeBrushes.Get("CardBackgroundFillColorDefaultBrush", "LayerFillColorDefaultBrush");
        return border;
    }
}
