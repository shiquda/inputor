using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Inputor.App.Models;
using Inputor.App.Services;

namespace Inputor.App.Views;

public sealed class SettingsWindow : Window
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#F3F5F8"));
    private static readonly IBrush CardBrush = Brushes.White;
    private static readonly IBrush CardBorderBrush = new SolidColorBrush(Color.Parse("#E5EAF0"));
    private static readonly IBrush PrimaryTextBrush = new SolidColorBrush(Color.Parse("#111827"));
    private static readonly IBrush SecondaryTextBrush = new SolidColorBrush(Color.Parse("#5B6472"));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#0F6CBD"));

    private readonly AppSettings _settings;
    private readonly StatsStore _statsStore;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _showAdminReminder;
    private readonly CheckBox _privacyMode;
    private readonly TextBox _dailyGoal;
    private readonly TextBox _excludedApps;
    private readonly TextBlock _headerNoteText;

    public SettingsWindow(AppSettings settings, StatsStore statsStore)
    {
        _settings = settings;
        _statsStore = statsStore;

        Width = 700;
        Height = 560;
        MinWidth = 640;
        MinHeight = 520;
        Background = BackgroundBrush;
        CanResize = false;

        _startWithWindows = CreateCheckBox("Start with Windows", _settings.StartWithWindows);
        _showAdminReminder = CreateCheckBox("Show admin limitation reminder", _settings.ShowAdminReminder);
        _privacyMode = CreateCheckBox("Privacy mode (never persist raw text)", _settings.PrivacyMode);
        _dailyGoal = new TextBox { Text = _settings.DailyGoal.ToString(), Watermark = "Daily goal, e.g. 1000" };
        _excludedApps = new TextBox { Text = _settings.ExcludedApps, Watermark = "Comma-separated process names" };
        _headerNoteText = CreateCaption(string.Empty);

        Content = BuildLayout();
        ReloadFromSettings();
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    public void ReloadFromSettings()
    {
        _startWithWindows.IsChecked = _settings.StartWithWindows;
        _showAdminReminder.IsChecked = _settings.ShowAdminReminder;
        _privacyMode.IsChecked = _settings.PrivacyMode;
        _dailyGoal.Text = _settings.DailyGoal.ToString();
        _excludedApps.Text = _settings.ExcludedApps;

        var snapshot = _statsStore.GetSnapshot();
        _headerNoteText.Text = $"Current target: {snapshot.CurrentAppName} • Today {snapshot.TotalToday:N0} • Session {snapshot.TotalSession:N0}";
    }

    private Control BuildLayout()
    {
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    BuildHeader(),
                    CreateSectionCard(
                        "Preferences",
                        "Adjust startup, reminders, goals, and exclusion rules without leaving the main workflow.",
                        new StackPanel
                        {
                            Spacing = 16,
                            Children =
                            {
                                _startWithWindows,
                                _showAdminReminder,
                                _privacyMode,
                                new StackPanel
                                {
                                    Spacing = 8,
                                    Children =
                                    {
                                        CreateLabel("Daily goal"),
                                        _dailyGoal,
                                        CreateCaption("Set 0 to disable goal tracking on the overview page.")
                                    }
                                },
                                new StackPanel
                                {
                                    Spacing = 8,
                                    Children =
                                    {
                                        CreateLabel("Excluded apps"),
                                        _excludedApps,
                                        CreateCaption("Use process names separated by commas. Tray and overview quick actions can add the current app here for you.")
                                    }
                                }
                            }
                        }),
                    CreateSectionCard(
                        "Privacy",
                        "Keep the rules factual and visible, just like a native Windows utility would.",
                        new StackPanel
                        {
                            Spacing = 10,
                            Children =
                            {
                                CreateCaption("inputor stores counts, process names, and date buckets locally."),
                                CreateCaption("Raw text only exists in memory for snapshot diffing and is never written to disk."),
                                CreateCaption("Password fields, unsupported controls, and protected windows remain excluded by design.")
                            }
                        }),
                    BuildFooterActions()
                }
            }
        };
    }

    private Control BuildHeader()
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = "Settings",
                    FontSize = 24,
                    FontWeight = FontWeight.Bold,
                    Foreground = PrimaryTextBrush
                },
                CreateCaption("Keep inputor practical: startup behavior, privacy defaults, goals, and exclusions."),
                _headerNoteText
            }
        };
    }

    private Control BuildFooterActions()
    {
        var saveButton = CreateActionButton("Save", (_, _) => Save());
        var closeButton = CreateActionButton("Close", (_, _) => Hide(), true);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                new Border(),
                closeButton.WithColumn(1),
                saveButton.WithColumn(2)
            }
        };
    }

    private static Border CreateSectionCard(string title, string subtitle, Control body)
    {
        return new Border
        {
            Background = CardBrush,
            BorderBrush = CardBorderBrush,
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
                            new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = PrimaryTextBrush },
                            new TextBlock { Text = subtitle, Foreground = SecondaryTextBrush, FontSize = 13, TextWrapping = TextWrapping.Wrap }
                        }
                    },
                    body
                }
            }
        };
    }

    private static CheckBox CreateCheckBox(string text, bool value)
    {
        return new CheckBox
        {
            Content = text,
            IsChecked = value,
            Foreground = PrimaryTextBrush,
            FontSize = 14
        };
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = PrimaryTextBrush,
            FontWeight = FontWeight.SemiBold
        };
    }

    private static TextBlock CreateCaption(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = SecondaryTextBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Button CreateActionButton(string text, EventHandler<RoutedEventArgs> onClick, bool secondary = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(18, 10),
            CornerRadius = new CornerRadius(10),
            Background = secondary ? Brushes.White : AccentBrush,
            Foreground = secondary ? PrimaryTextBrush : Brushes.White,
            BorderBrush = CardBorderBrush,
            BorderThickness = new Thickness(1)
        };
        button.Click += onClick;
        return button;
    }

    private void Save()
    {
        _settings.StartWithWindows = _startWithWindows.IsChecked ?? false;
        _settings.ShowAdminReminder = _showAdminReminder.IsChecked ?? true;
        _settings.PrivacyMode = _privacyMode.IsChecked ?? true;
        _settings.DailyGoal = ParseDailyGoal(_dailyGoal.Text);
        _settings.ExcludedApps = string.Join(", ", (_excludedApps.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        SettingsSaved?.Invoke(this, _settings);
        ReloadFromSettings();
        Hide();
    }

    private static int ParseDailyGoal(string? value)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return 1000;
        }

        return Math.Max(0, parsed);
    }
}
