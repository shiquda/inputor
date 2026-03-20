using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Inputor.WinUI;

internal static class ThemeBrushes
{
    public static event EventHandler? Changed;

    public static Brush GetCardBackgroundBrush(bool elevated = false)
    {
        return new SolidColorBrush(GetThemeColor(
            light: elevated ? Microsoft.UI.ColorHelper.FromArgb(244, 255, 255, 255) : Microsoft.UI.ColorHelper.FromArgb(232, 255, 255, 255),
            dark: elevated ? Microsoft.UI.ColorHelper.FromArgb(176, 40, 44, 52) : Microsoft.UI.ColorHelper.FromArgb(152, 32, 36, 43)));
    }

    public static Brush GetCardBorderBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(24, 24, 32, 42),
            dark: Microsoft.UI.ColorHelper.FromArgb(62, 255, 255, 255)));
    }

    public static Brush GetSubtleSurfaceBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(214, 245, 247, 250),
            dark: Microsoft.UI.ColorHelper.FromArgb(118, 49, 54, 63)));
    }

    public static Brush GetShellChromeBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(250, 247, 248, 250),
            dark: Microsoft.UI.ColorHelper.FromArgb(40, 18, 22, 28)));
    }

    public static Brush GetWindowSurfaceBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(246, 244, 245, 247),
            dark: Microsoft.UI.ColorHelper.FromArgb(116, 16, 20, 26)));
    }

    public static Brush GetNavigationPaneBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(242, 239, 240, 243),
            dark: Microsoft.UI.ColorHelper.FromArgb(168, 22, 26, 33)));
    }

    public static Brush GetTitleBarBackgroundBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(188, 249, 250, 252),
            dark: Microsoft.UI.ColorHelper.FromArgb(106, 18, 22, 28)));
    }

    public static Brush GetTitleBarBorderBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(22, 24, 32, 42),
            dark: Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)));
    }

    public static Brush GetAccentBadgeBackgroundBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(222, 227, 239, 252),
            dark: Microsoft.UI.ColorHelper.FromArgb(164, 48, 84, 118)));
    }

    public static Brush GetHeatmapCellBrush(byte intensity)
    {
        var color = IsDarkTheme()
            ? Microsoft.UI.ColorHelper.FromArgb(intensity, 82, 176, 226)
            : Microsoft.UI.ColorHelper.FromArgb((byte)Math.Min(255, intensity + 34), 66, 145, 204);
        return new SolidColorBrush(color);
    }

    public static Brush GetHeatmapBorderBrush(bool active = false)
    {
        return new SolidColorBrush(GetThemeColor(
            light: active ? Microsoft.UI.ColorHelper.FromArgb(108, 66, 145, 204) : Microsoft.UI.ColorHelper.FromArgb(18, 24, 32, 42),
            dark: active ? Microsoft.UI.ColorHelper.FromArgb(184, 255, 255, 255) : Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)));
    }

    public static Windows.UI.Color GetChartAccentColor()
    {
        return GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(255, 54, 129, 191),
            dark: Microsoft.UI.ColorHelper.FromArgb(255, 88, 182, 236));
    }

    public static Brush GetChartMarkerFillBrush()
    {
        return new SolidColorBrush(GetThemeColor(
            light: Microsoft.UI.ColorHelper.FromArgb(255, 255, 255, 255),
            dark: Microsoft.UI.ColorHelper.FromArgb(255, 248, 250, 252)));
    }

    public static Brush GetChartOutlineBrush(bool active = false)
    {
        return new SolidColorBrush(GetThemeColor(
            light: active ? Microsoft.UI.ColorHelper.FromArgb(126, 24, 32, 42) : Microsoft.UI.ColorHelper.FromArgb(46, 24, 32, 42),
            dark: active ? Microsoft.UI.ColorHelper.FromArgb(214, 255, 255, 255) : Microsoft.UI.ColorHelper.FromArgb(84, 255, 255, 255)));
    }

    public static IReadOnlyList<Windows.UI.Color> GetDistributionPalette()
    {
        if (IsDarkTheme())
        {
            return
            [
                Microsoft.UI.ColorHelper.FromArgb(255, 88, 182, 236),
                Microsoft.UI.ColorHelper.FromArgb(255, 74, 211, 187),
                Microsoft.UI.ColorHelper.FromArgb(255, 255, 193, 92),
                Microsoft.UI.ColorHelper.FromArgb(255, 255, 138, 138),
                Microsoft.UI.ColorHelper.FromArgb(255, 177, 150, 255),
                Microsoft.UI.ColorHelper.FromArgb(255, 132, 222, 255)
            ];
        }

        return
        [
            Microsoft.UI.ColorHelper.FromArgb(255, 54, 129, 191),
            Microsoft.UI.ColorHelper.FromArgb(255, 47, 160, 140),
            Microsoft.UI.ColorHelper.FromArgb(255, 210, 149, 35),
            Microsoft.UI.ColorHelper.FromArgb(255, 210, 96, 96),
            Microsoft.UI.ColorHelper.FromArgb(255, 124, 104, 214),
            Microsoft.UI.ColorHelper.FromArgb(255, 67, 167, 207)
        ];
    }

    public static bool IsDarkTheme()
    {
        return GetEffectiveTheme() == AppThemeMode.Dark;
    }

    public static Brush Get(string primaryKey, string fallbackKey)
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

    private static AppThemeMode GetEffectiveTheme()
    {
        var themeMode = AppStrings.ResolveThemeMode(App.Current?.Settings.ThemeMode);
        return themeMode switch
        {
            AppThemeMode.Dark => AppThemeMode.Dark,
            AppThemeMode.Light => AppThemeMode.Light,
            _ => IsSystemDarkTheme() ? AppThemeMode.Dark : AppThemeMode.Light
        };
    }

    private static Windows.UI.Color GetThemeColor(Windows.UI.Color light, Windows.UI.Color dark)
    {
        return IsDarkTheme() ? dark : light;
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            var background = new UISettings().GetColorValue(UIColorType.Background);
            return background.R + background.G + background.B < 382;
        }
        catch
        {
            return false;
        }
    }

    public static void NotifyChanged()
    {
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
