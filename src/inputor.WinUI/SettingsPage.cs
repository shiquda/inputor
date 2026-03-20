using System;
using System.Linq;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Inputor.WinUI;

public sealed class SettingsPage : UserControl
{
    private readonly CheckBox _startWithWindowsCheckBox;
    private readonly ComboBox _themeModeComboBox;
    private readonly ComboBox _languageComboBox;
    private readonly TextBox _excludedAppsTextBox;
    private readonly TextBox _appTagMappingsTextBox;
    private readonly CheckBox _confirmClearStatisticsCheckBox;
    private readonly Button _clearStatisticsButton;
    private readonly TextBlock _headerNoteTextBlock;
    private readonly TextBlock _restartNoticeTextBlock;
    private readonly List<Border> _cards = [];

    public SettingsPage()
    {
        _startWithWindowsCheckBox = new CheckBox { Content = AppStrings.Get("Settings.Label.StartWithWindows") };
        _themeModeComboBox = new ComboBox
        {
            ItemsSource = AppStrings.GetThemeModeOptions(),
            DisplayMemberPath = nameof(AppThemeModeOption.DisplayName),
            SelectedValuePath = nameof(AppThemeModeOption.Tag),
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _languageComboBox = new ComboBox
        {
            ItemsSource = AppStrings.GetLanguageOptions(),
            DisplayMemberPath = nameof(AppLanguageOption.DisplayName),
            SelectedValuePath = nameof(AppLanguageOption.Tag)
        };
        _excludedAppsTextBox = new TextBox { AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap };
        _appTagMappingsTextBox = new TextBox { AcceptsReturn = true, MinHeight = 120, TextWrapping = TextWrapping.Wrap };
        _confirmClearStatisticsCheckBox = new CheckBox { Content = AppStrings.Get("Settings.Label.ConfirmClearStatistics") };
        _clearStatisticsButton = new Button { Content = AppStrings.Get("Settings.Button.ClearStoredStatistics"), Padding = new Thickness(24, 8, 24, 8), IsEnabled = false };
        _headerNoteTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        _restartNoticeTextBlock = new TextBlock { Text = AppStrings.Get("Settings.RestartNotice"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Visibility = Visibility.Collapsed };

        _confirmClearStatisticsCheckBox.Checked += (_, _) => _clearStatisticsButton.IsEnabled = true;
        _confirmClearStatisticsCheckBox.Unchecked += (_, _) => _clearStatisticsButton.IsEnabled = false;
        _clearStatisticsButton.Click += (_, _) => ClearStoredStatistics();

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };
        Unloaded += (_, _) => ThemeBrushes.Changed -= ThemeBrushes_Changed;
        ThemeBrushes.Changed += ThemeBrushes_Changed;
        RefreshFromState();
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

    public void RefreshFromState()
    {
        var settings = App.Current.Settings;
        var snapshot = App.Current.StatsStore.GetSnapshot();

        _startWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        _themeModeComboBox.SelectedValue = settings.ThemeMode;
        _languageComboBox.SelectedValue = settings.Language;
        _excludedAppsTextBox.Text = settings.ExcludedApps;
        _appTagMappingsTextBox.Text = string.Join(Environment.NewLine,
            settings.GetNormalizedTagMappings().Select(item => $"{item.AppName}: {string.Join(", ", item.Tags)}"));
        _confirmClearStatisticsCheckBox.IsChecked = false;
        _clearStatisticsButton.IsEnabled = false;
        _restartNoticeTextBlock.Visibility = string.Equals(AppStrings.ResolveLanguageTag(settings.Language), AppStrings.CurrentLanguageTag, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
        _headerNoteTextBlock.Text = AppStrings.Format("Settings.HeaderNote", snapshot.CurrentAppName, snapshot.TotalToday, snapshot.TotalSession);
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Padding = new Thickness(28, 24, 28, 28), Spacing = 20 };

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Title"), FontSize = 28, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("Settings.Subtitle"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });
        header.Children.Add(_headerNoteTextBlock);
        header.Children.Add(_restartNoticeTextBlock);
        root.Children.Add(header);

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Label.ThemeMode"), AppStrings.Get("Settings.Caption.ThemeMode")));
        var themePanel = new StackPanel { Spacing = 10 };
        themePanel.Children.Add(_themeModeComboBox);
        root.Children.Add(CreateCard(themePanel, ThemeBrushes.GetSubtleSurfaceBrush()));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Section.PreferencesTitle"), AppStrings.Get("Settings.Section.PreferencesSubtitle")));
        var preferences = new StackPanel { Spacing = 16 };
        preferences.Children.Add(_startWithWindowsCheckBox);
        preferences.Children.Add(CreateLabeledInput(AppStrings.Get("Settings.Label.Language"), _languageComboBox, AppStrings.Get("Settings.Caption.Language")));
        preferences.Children.Add(CreateLabeledInput(AppStrings.Get("Settings.Label.ExcludedApps"), _excludedAppsTextBox, AppStrings.Get("Settings.Caption.ExcludedApps")));
        preferences.Children.Add(CreateLabeledInput(AppStrings.Get("Settings.Label.AppTagMappings"), _appTagMappingsTextBox, AppStrings.Get("Settings.Caption.AppTagMappings")));
        root.Children.Add(CreateCard(preferences));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Section.ExportTitle"), AppStrings.Get("Settings.Section.ExportSubtitle")));
        var exportPanel = new StackPanel { Spacing = 12 };
        exportPanel.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("Settings.Export.Description"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8
        });
        var exportButton = CreatePrimaryButton(AppStrings.Get("Settings.Button.ExportTodayCsv"));
        exportButton.Click += (_, _) => App.Current.ExportToday();
        exportPanel.Children.Add(exportButton);
        root.Children.Add(CreateCard(exportPanel));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Section.PrivacyTitle"), AppStrings.Get("Settings.Section.PrivacySubtitle")));
        var privacy = new StackPanel { Spacing = 12 };
        privacy.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Privacy.Item1"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        privacy.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Privacy.Item2"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        privacy.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Privacy.Item3"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        root.Children.Add(CreateCard(privacy));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Section.DataManagementTitle"), AppStrings.Get("Settings.Section.DataManagementSubtitle")));
        var dataManagement = new StackPanel { Spacing = 12 };
        dataManagement.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Data.Item1"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        dataManagement.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Data.Item2"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        dataManagement.Children.Add(_confirmClearStatisticsCheckBox);
        dataManagement.Children.Add(_clearStatisticsButton);
        root.Children.Add(CreateCard(dataManagement));

        var actions = new Grid { ColumnSpacing = 12 };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var saveButton = CreatePrimaryButton(AppStrings.Get("Settings.Button.Save"));
        saveButton.Click += (_, _) => Save();
        Grid.SetColumn(saveButton, 1);
        actions.Children.Add(saveButton);
        root.Children.Add(actions);

        return root;
    }

    private void Save()
    {
        var settings = App.Current.Settings;
        var previousLanguage = settings.Language;
        var previousMappings = settings.AppTagMappings.ToList();
        settings.StartWithWindows = _startWithWindowsCheckBox.IsChecked ?? false;
        settings.ThemeMode = _themeModeComboBox.SelectedValue as string ?? string.Empty;
        settings.Language = _languageComboBox.SelectedValue as string ?? string.Empty;
        settings.ExcludedApps = string.Join(", ", (_excludedAppsTextBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        var invalidLineCount = 0;
        if (TryParseAppTagMappings(_appTagMappingsTextBox.Text, out var parsedMappings, out invalidLineCount))
        {
            settings.AppTagMappings = parsedMappings;
        }
        else
        {
            settings.AppTagMappings = previousMappings;
        }

        App.Current.SaveSettings();
        var languageChanged = !string.Equals(previousLanguage, settings.Language, StringComparison.OrdinalIgnoreCase);
        _restartNoticeTextBlock.Visibility = languageChanged ? Visibility.Visible : Visibility.Collapsed;
        App.Current.StatsStore.SetStatus(
            invalidLineCount > 0
                ? StatusText.InvalidAppTagMappings(invalidLineCount)
                : languageChanged ? StatusText.LanguageChangeRequiresRestart() : StatusText.SettingsUpdated(),
            App.Current.StatsStore.CurrentAppName,
            App.Current.StatsStore.IsCurrentTargetSupported,
            App.Current.StatsStore.CurrentProcessName);

        if (invalidLineCount == 0)
        {
            RefreshFromState();
        }

        _restartNoticeTextBlock.Visibility = languageChanged ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool TryParseAppTagMappings(string? rawText, out List<AppTagMapping> mappings, out int invalidLineCount)
    {
        mappings = [];
        invalidLineCount = 0;

        foreach (var line in (rawText ?? string.Empty).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                invalidLineCount++;
                continue;
            }

            var tags = parts[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tags.Count == 0)
            {
                invalidLineCount++;
                continue;
            }

            mappings.Add(new AppTagMapping
            {
                AppName = parts[0],
                Tags = tags
            });
        }

        mappings = new AppSettings { AppTagMappings = mappings }.GetNormalizedTagMappings().ToList();
        return invalidLineCount == 0;
    }

    private void ClearStoredStatistics()
    {
        if (!(_confirmClearStatisticsCheckBox.IsChecked ?? false))
        {
            return;
        }

        App.Current.StatsStore.ClearAllStatistics();
        App.Current.StatsStore.SetStatus(StatusText.StoredStatisticsCleared(), App.Current.StatsStore.CurrentAppName, App.Current.StatsStore.IsCurrentTargetSupported, App.Current.StatsStore.CurrentProcessName);
        RefreshFromState();
    }

    private static UIElement CreateLabeledInput(string title, Control input, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(input);
        panel.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 });
        return panel;
    }

    private static UIElement CreateSectionHeader(string title, string subtitle)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private Border CreateCard(UIElement child, Brush? background = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.GetCardBorderBrush(),
            Child = child
        };
        border.Background = background ?? ThemeBrushes.GetCardBackgroundBrush();
        _cards.Add(border);
        return border;
    }

    private static Button CreatePrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(24, 8, 24, 8),
            Background = ThemeBrushes.Get("AccentFillColorDefaultBrush", "SystemFillColorSolidAccentBrush"),
            Foreground = ThemeBrushes.Get("TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorPrimaryBrush")
        };
    }
}
