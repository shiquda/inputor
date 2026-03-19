using System;
using System.Linq;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Inputor.WinUI;

public sealed class SettingsWindow : Window
{
    private readonly CheckBox _startWithWindowsCheckBox;
    private readonly CheckBox _showAdminReminderCheckBox;
    private readonly CheckBox _privacyModeCheckBox;
    private readonly TextBox _dailyGoalTextBox;
    private readonly TextBox _excludedAppsTextBox;
    private readonly TextBlock _headerNoteTextBlock;

    public SettingsWindow()
    {
        Title = "inputor Settings";
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        _startWithWindowsCheckBox = new CheckBox { Content = "Start with Windows" };
        _showAdminReminderCheckBox = new CheckBox { Content = "Show admin limitation reminder" };
        _privacyModeCheckBox = new CheckBox { Content = "Privacy mode (never persist raw text)" };
        _dailyGoalTextBox = new TextBox { PlaceholderText = "Daily goal, e.g. 1000" };
        _excludedAppsTextBox = new TextBox { AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap };
        _headerNoteTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };

        var root = new StackPanel { Padding = new Thickness(28, 24, 28, 28), Spacing = 20 };
        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = "Settings", FontSize = 28, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = "Native Windows host settings for startup, privacy defaults, goals, and exclusions.", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        header.Children.Add(_headerNoteTextBlock);
        root.Children.Add(header);

        root.Children.Add(CreateSectionHeader("Preferences", "Core app behavior and day-to-day defaults."));
        var preferences = new StackPanel { Spacing = 16 };
        preferences.Children.Add(_startWithWindowsCheckBox);
        preferences.Children.Add(_showAdminReminderCheckBox);
        preferences.Children.Add(_privacyModeCheckBox);
        preferences.Children.Add(CreateLabeledInput("Daily goal", _dailyGoalTextBox, "Set 0 to disable goal tracking."));
        preferences.Children.Add(CreateLabeledInput("Excluded apps", _excludedAppsTextBox, "Use comma-separated process names. Quick actions can add the current app for you."));
        root.Children.Add(CreateCard(preferences));

        root.Children.Add(CreateSectionHeader("Privacy", "What inputor stores and what it intentionally ignores."));
        var privacy = new StackPanel { Spacing = 12 };
        privacy.Children.Add(new TextBlock { Text = "inputor stores counts, process names, and date buckets locally.", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        privacy.Children.Add(new TextBlock { Text = "Raw text only exists in memory for snapshot diffing and is never written to disk.", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        privacy.Children.Add(new TextBlock { Text = "Password fields, unsupported controls, and protected windows remain excluded by design.", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        root.Children.Add(CreateCard(privacy));

        var actions = new Grid { ColumnSpacing = 12 };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var closeButton = new Button { Content = "Close", Padding = new Thickness(24, 8, 24, 8) };
        closeButton.Click += (_, _) => WindowHelpers.HideWindow(this);
        var saveButton = CreatePrimaryButton("Save");
        saveButton.Click += (_, _) => Save();
        Grid.SetColumn(closeButton, 1);
        Grid.SetColumn(saveButton, 2);
        actions.Children.Add(closeButton);
        actions.Children.Add(saveButton);
        root.Children.Add(actions);

        Content = new ScrollViewer { Content = root };
        ApplyWindowChrome();
        ReloadFromSettings();
    }

    public void ReloadFromSettings()
    {
        var settings = App.Current.Settings;
        var snapshot = App.Current.StatsStore.GetSnapshot();

        _startWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        _showAdminReminderCheckBox.IsChecked = settings.ShowAdminReminder;
        _privacyModeCheckBox.IsChecked = settings.PrivacyMode;
        _dailyGoalTextBox.Text = settings.DailyGoal.ToString();
        _excludedAppsTextBox.Text = settings.ExcludedApps;
        _headerNoteTextBlock.Text = $"Current target: {snapshot.CurrentAppName} • Today {snapshot.TotalToday:N0} • Session {snapshot.TotalSession:N0}";
    }

    private void Save()
    {
        var settings = App.Current.Settings;
        settings.StartWithWindows = _startWithWindowsCheckBox.IsChecked ?? false;
        settings.ShowAdminReminder = _showAdminReminderCheckBox.IsChecked ?? true;
        settings.PrivacyMode = _privacyModeCheckBox.IsChecked ?? true;
        settings.DailyGoal = ParseDailyGoal(_dailyGoalTextBox.Text);
        settings.ExcludedApps = string.Join(", ", (_excludedAppsTextBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        App.Current.SaveSettings();
        App.Current.StatsStore.SetAdminReminderVisible(settings.ShowAdminReminder);
        App.Current.StatsStore.SetStatus("Settings updated.", App.Current.StatsStore.CurrentAppName, App.Current.StatsStore.IsCurrentTargetSupported);
        ReloadFromSettings();
        WindowHelpers.HideWindow(this);
    }

    private static UIElement CreateLabeledInput(string title, Microsoft.UI.Xaml.Controls.Control input, string caption)
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

    private static Border CreateCard(UIElement child, Microsoft.UI.Xaml.Media.Brush? background = null)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", "SurfaceStrokeColorDefaultBrush"),
            Child = child
        };
        border.Background = background ?? GetThemeBrush("CardBackgroundFillColorDefaultBrush", "LayerFillColorDefaultBrush");
        return border;
    }

    private static Button CreatePrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(24, 8, 24, 8),
            Background = GetThemeBrush("AccentFillColorDefaultBrush", "SystemFillColorSolidAccentBrush"),
            Foreground = GetThemeBrush("TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorPrimaryBrush")
        };
    }

    private void ApplyWindowChrome()
    {
        var appWindow = WindowHelpers.GetAppWindow(this);
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
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

    private static int ParseDailyGoal(string? value)
    {
        return int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : 1000;
    }
}
