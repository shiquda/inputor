using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Inputor.App.Models;

namespace Inputor.App.Views;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _showAdminReminder;
    private readonly CheckBox _privacyMode;
    private readonly TextBox _excludedApps;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;

        Width = 480;
        Height = 360;
        CanResize = false;

        _startWithWindows = new CheckBox { Content = "Start with Windows", IsChecked = _settings.StartWithWindows };
        _showAdminReminder = new CheckBox { Content = "Show admin limitation reminder", IsChecked = _settings.ShowAdminReminder };
        _privacyMode = new CheckBox { Content = "Privacy mode (never persist raw text)", IsChecked = _settings.PrivacyMode };
        _excludedApps = new TextBox { Text = _settings.ExcludedApps, Watermark = "Comma-separated process names" };

        var saveButton = new Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Left };
        saveButton.Click += (_, _) => Save();

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Settings", FontSize = 28 },
                _startWithWindows,
                _showAdminReminder,
                _privacyMode,
                new TextBlock { Text = "Excluded apps" },
                _excludedApps,
                new TextBlock
                {
                    Text = "inputor only stores counts, process names, and date buckets. Raw text is kept in memory only for snapshot diffing.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                saveButton
            }
        };
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    private void Save()
    {
        _settings.StartWithWindows = _startWithWindows.IsChecked ?? false;
        _settings.ShowAdminReminder = _showAdminReminder.IsChecked ?? true;
        _settings.PrivacyMode = _privacyMode.IsChecked ?? true;
        _settings.ExcludedApps = _excludedApps.Text?.Trim() ?? string.Empty;

        SettingsSaved?.Invoke(this, _settings);
        Hide();
    }
}
