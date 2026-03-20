using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Inputor.WinUI;

internal sealed class TrayHostWindow : Window, IDisposable
{
    private static readonly SolidColorBrush MenuForegroundBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush MenuDangerBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 255, 138, 138));
    private static readonly SolidColorBrush MenuBackgroundBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 39, 39, 39));
    private static readonly SolidColorBrush MenuBorderBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 64, 64, 64));
    private readonly MenuFlyoutItem _showDashboardItem;
    private readonly MenuFlyoutItem _openSettingsItem;
    private readonly MenuFlyoutItem _togglePauseItem;
    private readonly FontIcon _togglePauseIcon;
    private readonly MenuFlyoutItem _excludeCurrentAppItem;
    private readonly MenuFlyoutItem _restartItem;
    private readonly MenuFlyoutItem _exitItem;
    private bool _isDisposed;

    public TrayHostWindow()
    {
        var trayIcon = new TaskbarIcon
        {
            ContextMenuMode = ContextMenuMode.SecondWindow,
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/inputor.ico")),
            LeftClickCommand = new RelayCommand(() => App.Current.ShowMainWindow()),
            NoLeftClickDelay = true,
            ToolTipText = AppStrings.Get("App.Name")
        };

        _showDashboardItem = CreateMenuItem(AppStrings.Get("Tray.ShowDashboard"), "\uE80F", (_, _) => App.Current.ShowMainWindow());
        _openSettingsItem = CreateMenuItem(AppStrings.Get("Tray.OpenSettings"), "\uE713", (_, _) => App.Current.ShowSettingsPage());
        _togglePauseIcon = CreateIcon("\uE769");
        _togglePauseItem = CreateMenuItem(AppStrings.Get("Main.Button.PauseMonitoring"), _togglePauseIcon, (_, _) => App.Current.TogglePauseMonitoring(), MenuForegroundBrush);
        _excludeCurrentAppItem = CreateMenuItem(AppStrings.Get("Tray.ExcludeCurrentApp"), "\uE8D9", (_, _) => App.Current.ExcludeCurrentApp());
        _restartItem = CreateMenuItem(AppStrings.Get("Tray.RestartInputor"), "\uE777", (_, _) => App.Current.RestartApplication());
        _exitItem = CreateMenuItem(AppStrings.Get("Tray.ExitInputor"), CreateIcon("\uE7E8", MenuDangerBrush), (_, _) => App.Current.ExitApplication(), MenuDangerBrush);

        var menuFlyout = new MenuFlyout
        {
            AreOpenCloseAnimationsEnabled = true,
            LightDismissOverlayMode = LightDismissOverlayMode.On,
            ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        };
        menuFlyout.MenuFlyoutPresenterStyle = CreatePresenterStyle();
        menuFlyout.Items.Add(_showDashboardItem);
        menuFlyout.Items.Add(_openSettingsItem);
        menuFlyout.Items.Add(new MenuFlyoutSeparator());
        menuFlyout.Items.Add(_togglePauseItem);
        menuFlyout.Items.Add(_excludeCurrentAppItem);
        menuFlyout.Items.Add(new MenuFlyoutSeparator());
        menuFlyout.Items.Add(_restartItem);
        menuFlyout.Items.Add(_exitItem);
        trayIcon.ContextFlyout = menuFlyout;

        Content = new Grid
        {
            RequestedTheme = ElementTheme.Dark,
            Children = { trayIcon }
        };

        TrayIcon = trayIcon;
        Closed += TrayHostWindow_Closed;
        ConfigureAppWindow();
    }

    public TaskbarIcon TrayIcon { get; }

    public void Initialize()
    {
        TrayIcon.ForceCreate();
    }

    public void UpdateState(string toolTipText, string pauseText, string pauseGlyph, bool excludeEnabled)
    {
        TrayIcon.ToolTipText = toolTipText;
        _togglePauseItem.Text = pauseText;
        _togglePauseIcon.Glyph = pauseGlyph;
        _excludeCurrentAppItem.IsEnabled = excludeEnabled;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Closed -= TrayHostWindow_Closed;
        Close();
    }

    private void ConfigureAppWindow()
    {
        var appWindow = WindowHelpers.GetAppWindow(this);
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

    private void TrayHostWindow_Closed(object sender, WindowEventArgs args)
    {
        _isDisposed = true;
    }

    private static Style CreatePresenterStyle()
    {
        var style = new Style { TargetType = typeof(MenuFlyoutPresenter) };
        style.Setters.Add(new Setter(MenuFlyoutPresenter.MinWidthProperty, 276d));
        style.Setters.Add(new Setter(Control.BackgroundProperty, MenuBackgroundBrush));
        style.Setters.Add(new Setter(Control.ForegroundProperty, MenuForegroundBrush));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, MenuBorderBrush));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        return style;
    }

    private static MenuFlyoutItem CreateMenuItem(string text, string glyph, RoutedEventHandler clickHandler)
    {
        return CreateMenuItem(text, CreateIcon(glyph), clickHandler, MenuForegroundBrush);
    }

    private static MenuFlyoutItem CreateMenuItem(string text, IconElement icon, RoutedEventHandler clickHandler, Brush foreground)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = icon,
            Foreground = foreground,
            MinWidth = 252
        };
        item.Click += clickHandler;
        return item;
    }

    private static FontIcon CreateIcon(string glyph, Brush? foreground = null)
    {
        return new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Foreground = foreground ?? MenuForegroundBrush
        };
    }
}
