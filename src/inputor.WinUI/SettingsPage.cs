using System;
using System.Linq;
using Inputor.App.Models;
using Inputor.App.Services;
using Microsoft.UI.Dispatching;
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
    private readonly TextBlock _versionValueTextBlock;
    private readonly TextBlock _buildValueTextBlock;
    private readonly TextBlock _channelValueTextBlock;
    private readonly TextBox _excludedAppsTextBox;
    private readonly TextBox _appTagNewAppTextBox;
    private readonly TextBox _appTagSearchTextBox;
    private readonly StackPanel _appTagAssignmentsPanel;
    private readonly TextBlock _appTagSummaryTextBlock;
    private readonly CheckBox _confirmClearStatisticsCheckBox;
    private readonly Button _clearStatisticsButton;
    private readonly Button _clearIconCacheButton;
    private readonly Button _openDataDirectoryButton;
    private readonly Button _exportBackupArchiveButton;
    private readonly Button _restoreBackupArchiveButton;
    private readonly TextBlock _headerNoteTextBlock;
    private readonly TextBlock _restartNoticeTextBlock;
    private readonly DispatcherQueueTimer _settingsSaveTimer;
    private readonly List<Border> _cards = [];
    private readonly List<AppTagEditorState> _appTagEditors = [];
    private bool _isRefreshingFromState;
    private bool _hasQueuedSettingsSave;

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
        _versionValueTextBlock = CreateReadOnlyValueTextBlock();
        _buildValueTextBlock = CreateReadOnlyValueTextBlock();
        _channelValueTextBlock = CreateReadOnlyValueTextBlock();
        _excludedAppsTextBox = new TextBox { AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap };
        _appTagNewAppTextBox = new TextBox { PlaceholderText = AppStrings.Get("Settings.Placeholder.AppTagNewApp") };
        _appTagSearchTextBox = new TextBox { PlaceholderText = AppStrings.Get("Settings.Placeholder.AppTagSearch") };
        _appTagSearchTextBox.TextChanged += (_, _) => RefreshAppTagEditorList();
        _appTagAssignmentsPanel = new StackPanel { Spacing = 12 };
        _appTagSummaryTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
        _confirmClearStatisticsCheckBox = new CheckBox { Content = AppStrings.Get("Settings.Label.ConfirmClearStatistics") };
        _clearStatisticsButton = new Button { Content = AppStrings.Get("Settings.Button.ClearStoredStatistics"), Padding = new Thickness(24, 8, 24, 8), IsEnabled = false };
        _clearIconCacheButton = new Button { Content = AppStrings.Get("Settings.Button.ClearIconCache"), Padding = new Thickness(24, 8, 24, 8) };
        _openDataDirectoryButton = new Button { Content = AppStrings.Get("Settings.Button.OpenDataDirectory"), Padding = new Thickness(20, 8, 20, 8) };
        _exportBackupArchiveButton = CreatePrimaryButton(AppStrings.Get("Settings.Button.ExportBackupArchive"));
        _restoreBackupArchiveButton = new Button { Content = AppStrings.Get("Settings.Button.RestoreBackupArchive"), Padding = new Thickness(20, 8, 20, 8) };
        _headerNoteTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        _restartNoticeTextBlock = new TextBlock { Text = AppStrings.Get("Settings.RestartNotice"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7, Visibility = Visibility.Collapsed };
        _settingsSaveTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(450);
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;

        _startWithWindowsCheckBox.Checked += (_, _) => SaveSettingsImmediately();
        _startWithWindowsCheckBox.Unchecked += (_, _) => SaveSettingsImmediately();
        _themeModeComboBox.SelectionChanged += (_, _) => SaveSettingsImmediately();
        _languageComboBox.SelectionChanged += (_, _) => SaveSettingsImmediately();
        _excludedAppsTextBox.TextChanged += (_, _) => QueueSettingsSave();

        _confirmClearStatisticsCheckBox.Checked += (_, _) => _clearStatisticsButton.IsEnabled = true;
        _confirmClearStatisticsCheckBox.Unchecked += (_, _) => _clearStatisticsButton.IsEnabled = false;
        _clearStatisticsButton.Click += (_, _) => ClearStoredStatistics();
        _clearIconCacheButton.Click += (_, _) => ClearIconCache();
        _openDataDirectoryButton.Click += (_, _) => App.Current.OpenDataDirectory();
        _exportBackupArchiveButton.Click += (_, _) => App.Current.ExportBackupArchive();
        _restoreBackupArchiveButton.Click += (_, _) => RestoreBackupArchive();

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent()
        };
        Unloaded += SettingsPage_Unloaded;
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
        RefreshAppTagEditorList();
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        FlushPendingSettingsSave();
        ThemeBrushes.Changed -= ThemeBrushes_Changed;
    }

    public void RefreshFromState()
    {
        _isRefreshingFromState = true;

        var settings = App.Current.Settings;
        var snapshot = App.Current.StatsStore.GetSnapshot();

        _startWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        _themeModeComboBox.SelectedValue = settings.ThemeMode;
        _languageComboBox.SelectedValue = settings.Language;
        _excludedAppsTextBox.Text = settings.ExcludedApps;
        _confirmClearStatisticsCheckBox.IsChecked = false;
        _clearStatisticsButton.IsEnabled = false;
        _restartNoticeTextBlock.Visibility = string.Equals(AppStrings.ResolveLanguageTag(settings.Language), AppStrings.CurrentLanguageTag, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
        _headerNoteTextBlock.Text = AppStrings.Format("Settings.HeaderNote", snapshot.CurrentAppName, snapshot.TotalToday, snapshot.TotalSession);
        _versionValueTextBlock.Text = VersionInfo.DisplayVersion;
        _buildValueTextBlock.Text = VersionInfo.BuildVersion;
        _channelValueTextBlock.Text = GetLocalizedChannel();

        RebuildAppTagEditors(snapshot, settings);
        _appTagNewAppTextBox.Text = string.Empty;
        RefreshAppTagEditorList();
        _settingsSaveTimer.Stop();
        _hasQueuedSettingsSave = false;
        _isRefreshingFromState = false;
    }

    private void RebuildAppTagEditors(DashboardSnapshot snapshot, AppSettings settings)
    {
        _appTagEditors.Clear();
        MergeSnapshotIntoEditors(snapshot, settings);
    }

    private void MergeSnapshotIntoEditors(DashboardSnapshot snapshot, AppSettings settings)
    {
        var statsByApp = snapshot.AppStats.ToDictionary(item => item.AppName, StringComparer.OrdinalIgnoreCase);
        var candidateNames = snapshot.AppStats
            .Select(item => item.AppName)
            .Concat(settings.GetNormalizedTagMappings().Select(item => item.AppName))
            .Concat(_appTagEditors.Select(item => item.AppName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var appName in candidateNames)
        {
            statsByApp.TryGetValue(appName, out var stat);
            var existing = _appTagEditors.FirstOrDefault(item => string.Equals(item.AppName, appName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _appTagEditors.Add(new AppTagEditorState
                {
                    AppName = appName,
                    TodayCount = stat?.TodayCount ?? 0,
                    SessionCount = stat?.SessionCount ?? 0,
                    TotalCount = stat?.TotalCount ?? 0,
                    Tags = settings.GetTagsForApp(appName).ToList()
                });
                continue;
            }

            existing.TodayCount = stat?.TodayCount ?? 0;
            existing.SessionCount = stat?.SessionCount ?? 0;
            existing.TotalCount = stat?.TotalCount ?? 0;
        }

        AppPresentationService.WarmIcons(_appTagEditors.Select(item => item.AppName).ToList(), Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
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

        var appTagPanel = new StackPanel { Spacing = 12 };
        appTagPanel.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Label.AppTagMappings"), FontWeight = FontWeights.SemiBold, FontSize = 14 });
        appTagPanel.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Caption.AppTagMappings"), TextWrapping = TextWrapping.Wrap, Opacity = 0.72, FontSize = 12 });
        var addAppRow = new Grid { ColumnSpacing = 8 };
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addAppRow.Children.Add(_appTagNewAppTextBox);
        var addAppButton = new Button { Content = AppStrings.Get("Settings.Button.AddAppTagTarget"), Padding = new Thickness(16, 8, 16, 8) };
        addAppButton.Click += (_, _) => AddManualAppTagTarget();
        Grid.SetColumn(addAppButton, 1);
        addAppRow.Children.Add(addAppButton);
        appTagPanel.Children.Add(addAppRow);
        appTagPanel.Children.Add(_appTagSearchTextBox);
        appTagPanel.Children.Add(_appTagSummaryTextBlock);
        appTagPanel.Children.Add(_appTagAssignmentsPanel);
        preferences.Children.Add(appTagPanel);
        root.Children.Add(CreateCard(preferences));

        root.Children.Add(CreateSectionHeader(AppStrings.Get("Settings.Section.VersionTitle"), AppStrings.Get("Settings.Section.VersionSubtitle")));
        var versionPanel = new StackPanel { Spacing = 16 };
        versionPanel.Children.Add(CreateLabeledValue(AppStrings.Get("Settings.Label.Version"), _versionValueTextBlock, AppStrings.Get("Settings.Caption.Version")));
        versionPanel.Children.Add(CreateLabeledValue(AppStrings.Get("Settings.Label.Build"), _buildValueTextBlock, AppStrings.Get("Settings.Caption.Build")));
        versionPanel.Children.Add(CreateLabeledValue(AppStrings.Get("Settings.Label.Channel"), _channelValueTextBlock, AppStrings.Get("Settings.Caption.Channel")));
        root.Children.Add(CreateCard(versionPanel));

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
        dataManagement.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Caption.DataDirectory"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        dataManagement.Children.Add(_openDataDirectoryButton);
        dataManagement.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Caption.BackupArchive"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        var archiveActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        archiveActions.Children.Add(_exportBackupArchiveButton);
        archiveActions.Children.Add(_restoreBackupArchiveButton);
        dataManagement.Children.Add(archiveActions);
        dataManagement.Children.Add(new TextBlock { Text = AppStrings.Get("Settings.Caption.ClearIconCache"), TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        dataManagement.Children.Add(_clearIconCacheButton);
        dataManagement.Children.Add(_confirmClearStatisticsCheckBox);
        dataManagement.Children.Add(_clearStatisticsButton);
        root.Children.Add(CreateCard(dataManagement));

        return root;
    }

    private void SettingsSaveTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _hasQueuedSettingsSave = false;
        SaveSettingsFromInputs();
    }

    private void QueueSettingsSave()
    {
        if (_isRefreshingFromState)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _hasQueuedSettingsSave = true;
        _settingsSaveTimer.Start();
    }

    private void SaveSettingsImmediately()
    {
        if (_isRefreshingFromState)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _hasQueuedSettingsSave = false;
        SaveSettingsFromInputs();
    }

    private void FlushPendingSettingsSave()
    {
        if (_isRefreshingFromState || !_hasQueuedSettingsSave)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _hasQueuedSettingsSave = false;
        SaveSettingsFromInputs();
    }

    private void SaveSettingsFromInputs()
    {
        if (_isRefreshingFromState)
        {
            return;
        }

        var settings = App.Current.Settings;
        var previousLanguage = settings.Language;
        settings.StartWithWindows = _startWithWindowsCheckBox.IsChecked ?? false;
        settings.ThemeMode = _themeModeComboBox.SelectedValue as string ?? string.Empty;
        settings.Language = _languageComboBox.SelectedValue as string ?? string.Empty;
        var normalizedExcludedApps = string.Join(", ", (_excludedAppsTextBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        settings.ExcludedApps = normalizedExcludedApps;
        settings.AppTagMappings = _appTagEditors
            .Where(item => item.Tags.Count > 0)
            .Select(item => new AppTagMapping
            {
                AppName = item.AppName,
                Tags = item.Tags.ToList()
            })
            .ToList();

        App.Current.SaveSettings();
        var languageChanged = !string.Equals(previousLanguage, settings.Language, StringComparison.OrdinalIgnoreCase);
        var restartRequired = !string.Equals(AppStrings.ResolveLanguageTag(settings.Language), AppStrings.CurrentLanguageTag, StringComparison.OrdinalIgnoreCase);
        _restartNoticeTextBlock.Visibility = restartRequired ? Visibility.Visible : Visibility.Collapsed;
        App.Current.StatsStore.SetStatus(
            languageChanged ? StatusText.LanguageChangeRequiresRestart() : StatusText.SettingsUpdated(),
            App.Current.StatsStore.CurrentAppName,
            App.Current.StatsStore.IsCurrentTargetSupported,
            App.Current.StatsStore.CurrentProcessName);

        if (_excludedAppsTextBox.FocusState == FocusState.Unfocused
            && !string.Equals(_excludedAppsTextBox.Text, normalizedExcludedApps, StringComparison.Ordinal))
        {
            _isRefreshingFromState = true;
            _excludedAppsTextBox.Text = normalizedExcludedApps;
            _isRefreshingFromState = false;
        }
    }

    private void RefreshAppTagEditorList()
    {
        _appTagAssignmentsPanel.Children.Clear();

        var filter = (_appTagSearchTextBox.Text ?? string.Empty).Trim();
        var visibleEditors = _appTagEditors
            .Where(item => string.IsNullOrWhiteSpace(filter)
                || item.AppName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.Tags.Count > 0)
            .ThenByDescending(item => item.TodayCount)
            .ThenByDescending(item => item.TotalCount)
            .ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var taggedCount = _appTagEditors.Count(item => item.Tags.Count > 0);
        _appTagSummaryTextBlock.Text = AppStrings.Format(
            "Settings.AppTagMappings.Summary",
            visibleEditors.Count,
            _appTagEditors.Count,
            taggedCount,
            GetKnownTags().Count);

        if (visibleEditors.Count == 0)
        {
            _appTagAssignmentsPanel.Children.Add(new TextBlock
            {
                Text = AppStrings.Get("Settings.AppTagMappings.Empty"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72
            });
            return;
        }

        foreach (var editor in visibleEditors)
        {
            _appTagAssignmentsPanel.Children.Add(CreateAppTagEditorRow(editor));
        }
    }

    private void AddManualAppTagTarget()
    {
        var appName = (_appTagNewAppTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(appName))
        {
            return;
        }

        if (_appTagEditors.Any(item => string.Equals(item.AppName, appName, StringComparison.OrdinalIgnoreCase)))
        {
            _appTagSearchTextBox.Text = appName;
            _appTagNewAppTextBox.Text = string.Empty;
            RefreshAppTagEditorList();
            return;
        }

        _appTagEditors.Add(new AppTagEditorState
        {
            AppName = appName,
            TodayCount = 0,
            SessionCount = 0,
            TotalCount = 0,
            Tags = []
        });
        _appTagNewAppTextBox.Text = string.Empty;
        _appTagSearchTextBox.Text = appName;
        RefreshAppTagEditorList();
    }

    private Border CreateAppTagEditorRow(AppTagEditorState editor)
    {
        var root = new StackPanel { Spacing = 10 };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        header.Children.Add(CreateAppIconBadge(editor.AppName));

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = editor.AppName,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new TextBlock
        {
            Text = AppStrings.Format("Settings.AppTagMappings.RowCounts", editor.TodayCount, editor.SessionCount, editor.TotalCount),
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        var tagsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (editor.Tags.Count == 0)
        {
            tagsPanel.Children.Add(new TextBlock
            {
                Text = AppStrings.Get("Settings.AppTagMappings.NoTags"),
                Opacity = 0.68,
                FontSize = 12
            });
        }
        else
        {
            foreach (var tag in editor.Tags)
            {
                tagsPanel.Children.Add(CreateTagChip(tag, () => RemoveTag(editor, tag)));
            }
        }

        body.Children.Add(tagsPanel);

        var addRow = new Grid { ColumnSpacing = 8 };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tagInput = new AutoSuggestBox
        {
            PlaceholderText = AppStrings.Get("Settings.Placeholder.AppTagInput"),
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        tagInput.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var suggestions = BuildTagSuggestions(tagInput.Text, editor).ToList();
            tagInput.ItemsSource = suggestions;
            tagInput.IsSuggestionListOpen = suggestions.Count > 0;
        };
        tagInput.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is string selectedTag)
            {
                tagInput.Text = selectedTag;
            }
        };
        tagInput.QuerySubmitted += (_, _) =>
        {
            if (TryAddTag(editor, tagInput.Text))
            {
                tagInput.Text = string.Empty;
            }
        };
        addRow.Children.Add(tagInput);

        var addButton = new Button
        {
            Content = AppStrings.Get("Settings.Button.AddTag"),
            Padding = new Thickness(16, 8, 16, 8)
        };
        addButton.Click += (_, _) =>
        {
            if (TryAddTag(editor, tagInput.Text))
            {
                tagInput.Text = string.Empty;
            }
        };
        Grid.SetColumn(addButton, 1);
        addRow.Children.Add(addButton);

        body.Children.Add(addRow);
        body.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("Settings.Caption.AppTagInput"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.68,
            FontSize = 12
        });

        Grid.SetColumn(body, 1);
        header.Children.Add(body);
        root.Children.Add(header);

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.GetCardBorderBrush(),
            Background = ThemeBrushes.GetSubtleSurfaceBrush(),
            Child = root
        };
        return card;
    }

    private UIElement CreateAppIconBadge(string appName)
    {
        var iconSource = AppPresentationService.TryGetIconSource([appName]);
        return new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = ThemeBrushes.GetCardBackgroundBrush(true),
            VerticalAlignment = VerticalAlignment.Top,
            Child = iconSource is not null
                ? new Image
                {
                    Source = iconSource,
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : new FontIcon
                {
                    Glyph = "\uE71D",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
        };
    }

    private Button CreateTagChip(string tag, Action removeAction)
    {
        var button = new Button
        {
            Content = $"{tag} ×",
            Padding = new Thickness(10, 4, 10, 4),
            MinWidth = 0,
            Background = ThemeBrushes.GetAccentBadgeBackgroundBrush(),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => removeAction();
        return button;
    }

    private IEnumerable<string> BuildTagSuggestions(string? rawText, AppTagEditorState editor)
    {
        var query = (rawText ?? string.Empty).Trim();
        return GetKnownTags()
            .Where(item => !editor.Tags.Contains(item, StringComparer.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(query) || item.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(8);
    }

    private IReadOnlyList<string> GetKnownTags()
    {
        return _appTagEditors
            .SelectMany(item => item.Tags)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryAddTag(AppTagEditorState editor, string? rawTag)
    {
        var tag = (rawTag ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tag) || editor.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        editor.Tags.Add(tag);
        editor.Tags = editor.Tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        RefreshAppTagEditorList();
        SaveSettingsImmediately();
        return true;
    }

    private void RemoveTag(AppTagEditorState editor, string tag)
    {
        editor.Tags = editor.Tags
            .Where(item => !string.Equals(item, tag, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        RefreshAppTagEditorList();
        SaveSettingsImmediately();
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

    private void ClearIconCache()
    {
        App.Current.ClearIconCache();
        RefreshAppTagEditorList();
    }

    private void RestoreBackupArchive()
    {
        App.Current.RestoreBackupArchive();
        RefreshFromState();
        RefreshAppTagEditorList();
    }

    private static UIElement CreateLabeledInput(string title, Control input, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(input);
        panel.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 });
        return panel;
    }

    private static UIElement CreateLabeledValue(string title, TextBlock value, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        panel.Children.Add(value);
        panel.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 });
        return panel;
    }

    private static TextBlock CreateReadOnlyValueTextBlock()
    {
        return new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            FontWeight = FontWeights.Medium
        };
    }

    private static string GetLocalizedChannel()
    {
        return VersionInfo.Channel switch
        {
            "development" => AppStrings.Get("Settings.Value.ChannelDevelopment"),
            "release" => AppStrings.Get("Settings.Value.ChannelRelease"),
            _ => VersionInfo.Channel
        };
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

    private sealed class AppTagEditorState
    {
        public required string AppName { get; init; }
        public int TodayCount { get; set; }
        public int SessionCount { get; set; }
        public int TotalCount { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
