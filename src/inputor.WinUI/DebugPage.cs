using System;
using System.Collections.Generic;
using System.Linq;
using Inputor.App.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Inputor.WinUI;

public sealed class DebugPage : UserControl
{
    private readonly HashSet<string> _expandedEventKeys = [];
    private readonly Button _captureButton;
    private readonly Button _clearButton;
    private readonly ComboBox _filterComboBox;
    private readonly TextBlock _summaryTextBlock;
    private readonly Grid _summaryPanel;
    private readonly StackPanel _eventsPanel;
    private DashboardSnapshot? _pendingSnapshot;
    private int _interactionDepth;

    public DebugPage()
    {
        _captureButton = new Button
        {
            Padding = new Thickness(16, 8, 16, 8)
        };
        _captureButton.Click += (_, _) =>
        {
            var snapshot = App.Current.StatsStore.GetSnapshot();
            App.Current.SetDebugCaptureEnabled(!snapshot.IsDebugCaptureEnabled);
        };

        _clearButton = new Button
        {
            Content = AppStrings.Get("Debug.Button.ClearRecords"),
            Padding = new Thickness(16, 8, 16, 8)
        };
        _clearButton.Click += (_, _) => App.Current.ClearDebugEvents();

        _filterComboBox = new ComboBox
        {
            Width = 220,
            ItemsSource = new[]
            {
                AppStrings.Get("Debug.Filter.AllEvents"),
                AppStrings.Get("Debug.Filter.CounterOnly"),
                AppStrings.Get("Debug.Filter.PendingOnly"),
                AppStrings.Get("Debug.Filter.FilteredOnly")
            },
            SelectedIndex = 0
        };
        _filterComboBox.SelectionChanged += (_, _) => Refresh(App.Current.StatsStore.GetSnapshot());

        _summaryTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.78 };
        _summaryPanel = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        _summaryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _summaryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _summaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _summaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _eventsPanel = new StackPanel { Spacing = 12 };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };

        Unloaded += (_, _) => ResetInteractionState();
    }

    public void Refresh(DashboardSnapshot snapshot)
    {
        _captureButton.Content = snapshot.IsDebugCaptureEnabled ? AppStrings.Get("Debug.Button.PauseCapture") : AppStrings.Get("Debug.Button.StartCapture");
        _clearButton.IsEnabled = snapshot.DebugEvents.Count > 0;

        if (ShouldDeferRefresh())
        {
            _pendingSnapshot = snapshot;
            if (snapshot.IsDebugCaptureEnabled)
            {
                _summaryTextBlock.Text = AppStrings.Get("Debug.Summary.InspectionFrozen");
            }

            return;
        }

        _summaryPanel.Children.Clear();
        _eventsPanel.Children.Clear();

        if (!snapshot.IsDebugCaptureEnabled)
        {
            _summaryTextBlock.Text = AppStrings.Get("Debug.Summary.Paused");

            if (snapshot.DebugEvents.Count == 0)
            {
                _eventsPanel.Children.Add(CreateInfoCard(AppStrings.Get("Debug.Card.PausedTitle"), AppStrings.Get("Debug.Card.PausedEmptyBody")));
            }
            else
            {
                _eventsPanel.Children.Add(CreateInfoCard(AppStrings.Get("Debug.Card.PausedTitle"), AppStrings.Get("Debug.Card.PausedBufferedBody")));
            }

            RenderEvents(snapshot);
            return;
        }

        RenderEvents(snapshot);
    }

    public void ResetInteractionState()
    {
        _interactionDepth = 0;
        ApplyPendingRefresh();
    }

    private void RenderEvents(DashboardSnapshot snapshot)
    {
        var filteredEvents = ApplyFilter(snapshot.DebugEvents).ToList();
        var increasedCount = snapshot.DebugEvents.Count(item => item.Delta > 0 && !item.IsPaste && !item.IsBulkContentLoad);
        var filteredOutCount = snapshot.DebugEvents.Count(item => item.IsPaste || item.IsBulkContentLoad);
        var pendingCount = snapshot.DebugEvents.Count(item => item.IsPendingComposition);

        _summaryTextBlock.Text = filteredEvents.Count == snapshot.DebugEvents.Count
            ? (snapshot.IsDebugCaptureEnabled
                ? AppStrings.Format("Debug.Summary.Enabled", filteredEvents.Count)
                : AppStrings.Format("Debug.Summary.PausedBuffered", filteredEvents.Count))
            : (snapshot.IsDebugCaptureEnabled
                ? AppStrings.Format("Debug.Summary.Filtered", filteredEvents.Count, snapshot.DebugEvents.Count)
                : AppStrings.Format("Debug.Summary.PausedFiltered", filteredEvents.Count, snapshot.DebugEvents.Count));

        AddSummaryChip(0, 0, AppStrings.Get("Debug.Chip.Visible"), filteredEvents.Count.ToString());
        AddSummaryChip(0, 1, AppStrings.Get("Debug.Chip.CounterIncreased"), increasedCount.ToString());
        AddSummaryChip(1, 0, AppStrings.Get("Debug.Chip.FilteredOut"), filteredOutCount.ToString());
        AddSummaryChip(1, 1, AppStrings.Get("Debug.Chip.PendingComposition"), pendingCount.ToString());

        if (filteredEvents.Count == 0)
        {
            _eventsPanel.Children.Add(CreateInfoCard(AppStrings.Get("Debug.Card.NoMatchTitle"), AppStrings.Get("Debug.Card.NoMatchBody")));
            return;
        }

        foreach (var entry in filteredEvents)
        {
            var eventKey = BuildEventKey(entry);
            _eventsPanel.Children.Add(CreateEventCard(entry, _expandedEventKeys.Contains(eventKey), expanded =>
            {
                if (expanded)
                {
                    _expandedEventKeys.Add(eventKey);
                }
                else
                {
                    _expandedEventKeys.Remove(eventKey);
                }

                if (!ShouldDeferRefresh())
                {
                    ApplyPendingRefresh();
                }
            }));
        }
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Padding = new Thickness(28, 24, 28, 28), Spacing = 20 };

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = AppStrings.Get("Debug.Title"), FontSize = 28, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("Debug.Subtitle"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        header.Children.Add(_summaryTextBlock);
        root.Children.Add(header);

        var toolbar = new Grid { ColumnSpacing = 12 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        toolbar.Children.Add(_captureButton);

        Grid.SetColumn(_filterComboBox, 1);
        toolbar.Children.Add(_filterComboBox);

        Grid.SetColumn(_clearButton, 2);
        toolbar.Children.Add(_clearButton);
        root.Children.Add(toolbar);

        root.Children.Add(_summaryPanel);
        root.Children.Add(_eventsPanel);
        return root;
    }

    private IEnumerable<DebugEventEntry> ApplyFilter(IReadOnlyList<DebugEventEntry> events)
    {
        return _filterComboBox.SelectedIndex switch
        {
            1 => events.Where(item => item.Delta > 0 && !item.IsPaste && !item.IsBulkContentLoad),
            2 => events.Where(item => item.IsPendingComposition),
            3 => events.Where(item => item.IsPaste || item.IsBulkContentLoad),
            _ => events
        };
    }

    private static Border CreateSummaryChip(string label, string value)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.68 });
        panel.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.SemiBold });
        return CreateCard(panel, ThemeBrushes.Get("CardBackgroundFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"), new Thickness(16, 12, 16, 12));
    }

    private void AddSummaryChip(int row, int column, string label, string value)
    {
        var chip = CreateSummaryChip(label, value);
        Grid.SetRow(chip, row);
        Grid.SetColumn(chip, column);
        _summaryPanel.Children.Add(chip);
    }

    private Border CreateEventCard(DebugEventEntry entry, bool isExpanded, Action<bool> onExpandedChanged)
    {
        var content = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel { Spacing = 4 };
        title.Children.Add(new TextBlock { Text = entry.AppName, FontWeight = FontWeights.SemiBold, FontSize = 15 });
        title.Children.Add(new TextBlock { Text = BuildPrimarySummary(entry), TextWrapping = TextWrapping.Wrap, Opacity = 0.84 });
        header.Children.Add(title);

        var badge = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(999),
            Background = ThemeBrushes.Get("AccentFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush"),
            Child = new TextBlock
            {
                Text = entry.Delta > 0 && !entry.IsPaste && !entry.IsBulkContentLoad ? AppStrings.Get("Debug.Badge.Counter") : AppStrings.Get("Debug.Badge.NoCount"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            }
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        content.Children.Add(header);

        var summaryRow = new TextBlock
        {
            Text = AppStrings.Format("Debug.EventSummary", entry.Timestamp, entry.ControlTypeName, BuildFlagsSummary(entry)),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        };
        content.Children.Add(summaryRow);

        var toggleButton = new Button
        {
            Content = isExpanded ? AppStrings.Get("Debug.Button.HideDetails") : AppStrings.Get("Debug.Button.ShowDetails"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 6, 12, 6)
        };

        var detailsPanel = new StackPanel { Spacing = 8, Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed };
        detailsPanel.Children.Add(CreateMetricLine(AppStrings.Get("Debug.Label.Status"), entry.StatusMessage));
        detailsPanel.Children.Add(CreateMetricLine(AppStrings.Get("Debug.Label.Delta"), AppStrings.Format("Debug.DeltaLine", entry.Delta)));
        detailsPanel.Children.Add(CreateMetricLine(AppStrings.Get("Debug.Label.InsertedSegmentLength"), entry.InsertedSegmentLength.ToString()));
        detailsPanel.Children.Add(CreateMetricLine(AppStrings.Get("Debug.Label.CharacterMix"), AppStrings.Format("Debug.CharacterMixLine", entry.InsertedChineseCharacterCount, entry.InsertedEnglishLetterCount, entry.InsertedSupportedCharacterCount)));
        detailsPanel.Children.Add(CreateMetricLine(AppStrings.Get("Debug.Label.Flags"), BuildFlagsSummary(entry)));

        toggleButton.Click += (_, _) =>
        {
            var isOpen = detailsPanel.Visibility == Visibility.Visible;
            detailsPanel.Visibility = isOpen ? Visibility.Collapsed : Visibility.Visible;
            toggleButton.Content = isOpen ? AppStrings.Get("Debug.Button.ShowDetails") : AppStrings.Get("Debug.Button.HideDetails");
            onExpandedChanged(!isOpen);
        };

        content.Children.Add(toggleButton);
        content.Children.Add(detailsPanel);
        var card = CreateCard(content);
        card.PointerEntered += (_, _) => BeginInteraction();
        card.PointerExited += (_, _) => EndInteraction();
        return card;
    }

    private bool ShouldDeferRefresh()
    {
        return _interactionDepth > 0 && _expandedEventKeys.Count > 0;
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
        if (!ShouldDeferRefresh())
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

    private static string BuildPrimarySummary(DebugEventEntry entry)
    {
        if (entry.Delta > 0 && !entry.IsPaste && !entry.IsBulkContentLoad)
        {
            return AppStrings.Format("Debug.Primary.CounterIncreased", entry.Delta);
        }

        if (entry.IsPaste)
        {
            return AppStrings.Get("Debug.Primary.PasteFiltered");
        }

        if (entry.IsBulkContentLoad)
        {
            return AppStrings.Get("Debug.Primary.BulkFiltered");
        }

        if (entry.IsPendingComposition)
        {
            return AppStrings.Get("Debug.Primary.Pending");
        }

        return AppStrings.Get("Debug.Primary.NoIncrease");
    }

    private static string BuildFlagsSummary(DebugEventEntry entry)
    {
        var flags = new[]
        {
            entry.IsPendingComposition ? AppStrings.Get("Debug.Flag.PendingComposition") : null,
            entry.IsPaste ? AppStrings.Get("Debug.Flag.PasteFiltered") : null,
            entry.IsBulkContentLoad ? AppStrings.Get("Debug.Flag.BulkFiltered") : null,
            entry.IsNativeImeInputMode ? AppStrings.Get("Debug.Flag.NativeImeMode") : null,
            entry.IsCurrentTargetSupported ? AppStrings.Get("Debug.Flag.SupportedTarget") : AppStrings.Get("Debug.Flag.UnsupportedTarget")
        }
            .Where(item => item is not null)
            .Cast<string>()
            .ToList();

        return string.Join(" • ", flags);
    }

    private static UIElement CreateMetricLine(string label, string value)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.68 });
        panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private static Border CreateInfoCard(string title, UIElement content)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(content);
        return CreateCard(panel);
    }

    private static Border CreateInfoCard(string title, string text)
    {
        return CreateInfoCard(title, new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
    }

    private static string BuildEventKey(DebugEventEntry entry)
    {
        return string.Join('|', entry.Timestamp.Ticks, entry.AppName, entry.StatusMessage, entry.Delta, entry.ControlTypeName);
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
