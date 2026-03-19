using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Inputor.WinUI;

internal static class ThemeBrushes
{
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
}
