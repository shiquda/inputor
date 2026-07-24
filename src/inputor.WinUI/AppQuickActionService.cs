using Inputor.App.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Inputor.WinUI;

internal static class AppQuickActionService
{
    public static void AttachContextMenu(FrameworkElement host, AppAggregate aggregate, Action? onOpened = null, Action? onClosed = null)
    {
        host.ContextFlyout = CreateContextMenu(host, aggregate, onOpened, onClosed);
    }

    public static Button? CreateMoreButton(FrameworkElement host, AppAggregate aggregate, Action? onOpened = null, Action? onClosed = null)
    {
        var menu = CreateContextMenu(host, aggregate, onOpened, onClosed);
        if (menu is null)
        {
            return null;
        }

        var button = new Button
        {
            Content = new FontIcon
            {
                Glyph = "\uE712",
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                FontSize = 16
            },
            Padding = new Thickness(8),
            MinWidth = 36,
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ContextFlyout = menu
        };
        ToolTipService.SetToolTip(button, AppStrings.Get("QuickActions.Tooltip.More"));
        button.Click += (_, _) => button.ContextFlyout?.ShowAt(button);
        return button;
    }

    private static MenuFlyout? CreateContextMenu(FrameworkElement host, AppAggregate aggregate, Action? onOpened, Action? onClosed)
    {
        var menu = new MenuFlyout();
        menu.Opened += (_, _) => onOpened?.Invoke();
        menu.Closed += (_, _) => onClosed?.Invoke();

        if (aggregate.GroupKey.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var manageTagsItem = new MenuFlyoutItem { Text = AppStrings.Get("QuickActions.Menu.ManageTags") };
            manageTagsItem.Click += (_, _) => App.Current.ShowSettingsPage();
            menu.Items.Add(manageTagsItem);
            return menu;
        }

        var excludeItem = new MenuFlyoutItem
        {
            Text = aggregate.ProcessNames.Count > 1
                ? AppStrings.Format("QuickActions.Menu.ExcludeAppMultiple", aggregate.ProcessNames.Count)
                : AppStrings.Get("QuickActions.Menu.ExcludeApp")
        };
        excludeItem.Click += (_, _) => App.Current.ExcludeAppsForAggregate(aggregate);
        menu.Items.Add(excludeItem);

        var aliasItem = new MenuFlyoutItem { Text = AppStrings.Get("QuickActions.Menu.EditAlias") };
        aliasItem.Click += async (_, _) => await EditAliasAsync(host, aggregate);
        menu.Items.Add(aliasItem);

        var groupingItem = new MenuFlyoutItem { Text = AppStrings.Get("QuickActions.Menu.EditGrouping") };
        groupingItem.Click += async (_, _) => await EditGroupingAsync(host, aggregate);
        menu.Items.Add(groupingItem);

        return menu;
    }

    private static async Task EditAliasAsync(FrameworkElement host, AppAggregate aggregate)
    {
        var currentAlias = App.Current.Settings.GetAliasForGroup(aggregate.GroupKey) ?? string.Empty;
        var input = new TextBox
        {
            Text = currentAlias,
            PlaceholderText = aggregate.DisplayName,
            MinWidth = 320,
            MaxLength = 60
        };

        var dialog = new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = AppStrings.Format("QuickActions.Dialog.EditAlias.Title", aggregate.DisplayName),
            Content = BuildDialogContent(
                AppStrings.Format("QuickActions.Dialog.EditAlias.Body", string.Join(", ", aggregate.ProcessNames)),
                input),
            PrimaryButtonText = AppStrings.Get("QuickActions.Button.Save"),
            CloseButtonText = AppStrings.Get("QuickActions.Button.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Current.SetAliasForAggregate(aggregate, input.Text);
    }

    private static async Task EditGroupingAsync(FrameworkElement host, AppAggregate aggregate)
    {
        var currentTags = App.Current.Settings.GetTagsForApps(aggregate.ProcessNames);
        var tags = await TagSelectionDialog.ShowAsync(
            host,
            AppStrings.Format("QuickActions.Dialog.EditGrouping.Title", aggregate.DisplayName),
            currentTags,
            App.Current.Settings.GetKnownTags());
        if (tags is null)
        {
            return;
        }

        App.Current.SetGroupingForAggregate(aggregate, tags);
    }

    private static UIElement BuildDialogContent(string bodyText, Control input)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = bodyText,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
            MaxWidth = 360
        });
        panel.Children.Add(input);
        return panel;
    }
}
