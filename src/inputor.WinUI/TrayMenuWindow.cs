using System;
using System.Drawing;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Screen = System.Windows.Forms.Screen;
using Windows.Graphics;

namespace Inputor.WinUI;

internal sealed class TrayMenuWindow : Window
{
    private const int WindowWidth = 248;
    private const int WindowHeight = 322;
    private readonly Button _pauseButton;
    private bool _isMenuVisible;
    private bool _closingForExit;

    public TrayMenuWindow()
    {
        Title = AppStrings.Get("Tray.MenuTitle");
        SystemBackdrop = new DesktopAcrylicBackdrop();
        _pauseButton = CreateActionButton("\uE769", AppStrings.Get("Main.Button.PauseMonitoring"), (_, _) =>
        {
            App.Current.TogglePauseMonitoring();
            RefreshActions();
        });

        var root = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(180, 255, 255, 255)),
            Background = new SolidColorBrush(ColorHelper.FromArgb(132, 32, 32, 32)),
            Child = BuildContent()
        };

        Content = root;
        Activated += TrayMenuWindow_Activated;
        Closed += TrayMenuWindow_Closed;
        ConfigureAppWindow();
        RefreshActions();
    }

    public void ShowAtCursor(int cursorX, int cursorY)
    {
        RefreshActions();

        var appWindow = WindowHelpers.GetAppWindow(this);
        var workingArea = Screen.FromPoint(new Point(cursorX, cursorY)).WorkingArea;
        var x = cursorX - (WindowWidth - 12);
        var y = cursorY - (WindowHeight - 12);
        x = Math.Clamp(x, workingArea.Left, workingArea.Right - WindowWidth);
        y = Math.Clamp(y, workingArea.Top, workingArea.Bottom - WindowHeight);
        StartupDiagnostics.Log($"TrayMenuWindow.ShowAtCursor moving to {x},{y}.");
        appWindow.Move(new PointInt32(x, y));

        WindowHelpers.ShowWindow(this);
        _isMenuVisible = true;
        StartupDiagnostics.Log("TrayMenuWindow shown.");
    }

    public void HideMenu()
    {
        if (!_isMenuVisible)
        {
            return;
        }

        _isMenuVisible = false;
        WindowHelpers.HideWindow(this);
        StartupDiagnostics.Log("TrayMenuWindow hidden.");
    }

    public void CloseForExit()
    {
        _closingForExit = true;
        Close();
    }

    private UIElement BuildContent()
    {
        var stack = new StackPanel { Spacing = 4 };

        var title = new TextBlock
        {
            Text = AppStrings.Get("App.Name"),
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(12, 6, 12, 8)
        };
        stack.Children.Add(title);

        stack.Children.Add(CreateActionButton("\uE80F", AppStrings.Get("Tray.ShowDashboard"), (_, _) =>
        {
            HideMenu();
            App.Current.ShowMainWindow();
        }));
        stack.Children.Add(CreateActionButton("\uE713", AppStrings.Get("Tray.OpenSettings"), (_, _) =>
        {
            HideMenu();
            App.Current.ShowSettingsPage();
        }));

        stack.Children.Add(CreateSeparator());

        stack.Children.Add(_pauseButton);
        stack.Children.Add(CreateActionButton("\uE8D9", AppStrings.Get("Tray.ExcludeCurrentApp"), (_, _) =>
        {
            HideMenu();
            App.Current.ExcludeCurrentApp();
        }));

        stack.Children.Add(CreateSeparator());

        stack.Children.Add(CreateActionButton("\uE7E8", AppStrings.Get("Tray.ExitInputor"), (_, _) =>
        {
            HideMenu();
            App.Current.ExitApplication();
        }, isDestructive: true));

        return stack;
    }

    private void ConfigureAppWindow()
    {
        var appWindow = WindowHelpers.GetAppWindow(this);
        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
        appWindow.IsShownInSwitchers = false;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        WindowHelpers.HideWindow(this);
    }

    private void RefreshActions()
    {
        var snapshot = App.Current.StatsStore.GetSnapshot();
        _pauseButton.Content = CreateActionContent(
            snapshot.IsPaused ? "\uF5B0" : "\uE769",
            snapshot.IsPaused ? AppStrings.Get("Main.Button.ResumeMonitoring") : AppStrings.Get("Main.Button.PauseMonitoring"),
            false);
    }

    private void TrayMenuWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && !_closingForExit)
        {
            HideMenu();
        }
    }

    private void TrayMenuWindow_Closed(object sender, WindowEventArgs args)
    {
        App.Current.OnTrayMenuClosed(this);
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Height = 1,
            Margin = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(ColorHelper.FromArgb(96, 255, 255, 255))
        };
    }

    private static Button CreateActionButton(string glyph, string text, RoutedEventHandler onClick, bool isDestructive = false)
    {
        var button = new Button
        {
            Content = CreateActionContent(glyph, text, isDestructive),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        button.Click += onClick;
        return button;
    }

    private static UIElement CreateActionContent(string glyph, string text, bool isDestructive)
    {
        var foreground = isDestructive
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 255, 120, 120))
            : new SolidColorBrush(Colors.White);

        var row = new Grid
        {
            Height = 44
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            FontSize = 18,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(icon);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 15,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(ColorHelper.FromArgb(54, 255, 255, 255)),
            Child = row
        };
    }
}
