namespace Inputor.App.Models;

public sealed class DailyTotalEntry
{
    public required DateOnly Date { get; init; }
    public required int TotalCount { get; init; }
}
