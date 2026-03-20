namespace Inputor.WinUI;

internal static partial class AppStrings
{
    public static IReadOnlyList<AppAggregationOption> GetAggregationOptions()
    {
        return
        [
            new AppAggregationOption { Tag = "app", DisplayName = Get("Aggregation.App") },
            new AppAggregationOption { Tag = "tag", DisplayName = Get("Aggregation.Tag") }
        ];
    }
}
