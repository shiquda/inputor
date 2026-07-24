using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Inputor.WinUI;

internal static class TagSelectionDialog
{
    public static async Task<IReadOnlyList<string>?> ShowAsync(
        FrameworkElement host,
        string title,
        IEnumerable<string> currentTags,
        IEnumerable<string> knownTags)
    {
        var tags = new ObservableCollection<string>(knownTags
            .Concat(currentTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        var selectedTags = currentTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var feedback = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
            Visibility = Visibility.Collapsed
        };
        var tagList = new ListView
        {
            ItemsSource = tags,
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 240,
            IsTabStop = true
        };
        tagList.Loaded += (_, _) =>
        {
            foreach (var tag in tags.Where(selectedTags.Contains))
            {
                tagList.SelectedItems.Add(tag);
            }
        };

        var input = new AutoSuggestBox
        {
            PlaceholderText = AppStrings.Get("TagPicker.Placeholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        input.GotFocus += (_, _) =>
        {
            input.ItemsSource = tags;
            input.IsSuggestionListOpen = tags.Count > 0;
        };
        input.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var matches = tags
                .Where(tag => tag.Contains(input.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();
            input.ItemsSource = matches;
            input.IsSuggestionListOpen = matches.Count > 0;
        };
        input.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is string tag)
            {
                input.Text = tag;
            }
        };

        void AddOrSelectTag()
        {
            var tag = (input.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                feedback.Text = AppStrings.Get("TagPicker.Feedback.Empty");
                feedback.Visibility = Visibility.Visible;
                return;
            }

            var existing = tags.FirstOrDefault(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                tags.Add(tag);
                existing = tag;
                feedback.Text = AppStrings.Format("TagPicker.Feedback.Created", tag);
            }
            else
            {
                feedback.Text = AppStrings.Format("TagPicker.Feedback.Selected", existing);
            }

            if (!tagList.SelectedItems.Contains(existing))
            {
                tagList.SelectedItems.Add(existing);
            }

            feedback.Visibility = Visibility.Visible;
            input.Text = string.Empty;
        }

        input.QuerySubmitted += (_, _) => AddOrSelectTag();
        var addButton = new Button
        {
            Content = AppStrings.Get("TagPicker.Button.Add"),
            Padding = new Thickness(16, 8, 16, 8)
        };
        addButton.Click += (_, _) => AddOrSelectTag();

        var addRow = new Grid { ColumnSpacing = 8 };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addRow.Children.Add(input);
        Grid.SetColumn(addButton, 1);
        addRow.Children.Add(addButton);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("TagPicker.Description"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
        content.Children.Add(addRow);
        content.Children.Add(feedback);
        content.Children.Add(tagList);

        var dialog = new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = AppStrings.Get("QuickActions.Button.Save"),
            CloseButtonText = AppStrings.Get("QuickActions.Button.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return tagList.SelectedItems
            .OfType<string>()
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
