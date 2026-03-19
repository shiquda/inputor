namespace Inputor.App.Models;

public sealed class DailyAppTotalEntry
{
    public required DateOnly Date { get; init; }
    public required string AppName { get; init; }
    public required int TotalCount { get; init; }
}
