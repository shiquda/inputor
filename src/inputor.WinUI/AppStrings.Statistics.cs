namespace Inputor.WinUI;

internal sealed class AppRangeOption
{
    public required int Value { get; init; }
    public required string DisplayName { get; init; }
}

internal static partial class AppStrings
{
    public static IReadOnlyList<AppRangeOption> GetDistributionRangeOptions()
    {
        return
        [
            new AppRangeOption { Value = 0, DisplayName = Get("Statistics.Range.Today") },
            new AppRangeOption { Value = 1, DisplayName = Get("Statistics.Range.Last7Days") },
            new AppRangeOption { Value = 2, DisplayName = Get("Statistics.Range.Last30Days") },
            new AppRangeOption { Value = 3, DisplayName = Get("Statistics.Range.AllTime") }
        ];
    }
}
