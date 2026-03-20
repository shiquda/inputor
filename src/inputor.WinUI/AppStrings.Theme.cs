namespace Inputor.WinUI;

internal enum AppThemeMode
{
    FollowSystem,
    Light,
    Dark
}

internal sealed class AppThemeModeOption
{
    public required string Tag { get; init; }
    public required string DisplayName { get; init; }
}

internal static partial class AppStrings
{
    public static IReadOnlyList<AppThemeModeOption> GetThemeModeOptions()
    {
        return
        [
            new AppThemeModeOption { Tag = string.Empty, DisplayName = Get("Theme.FollowSystem") },
            new AppThemeModeOption { Tag = "light", DisplayName = Get("Theme.Light") },
            new AppThemeModeOption { Tag = "dark", DisplayName = Get("Theme.Dark") }
        ];
    }

    public static AppThemeMode ResolveThemeMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "light" => AppThemeMode.Light,
            "dark" => AppThemeMode.Dark,
            _ => AppThemeMode.FollowSystem
        };
    }
}
