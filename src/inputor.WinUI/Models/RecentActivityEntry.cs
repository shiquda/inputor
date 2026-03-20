namespace Inputor.App.Models;

public sealed class RecentActivityEntry
{
    public required string AppName { get; init; }
    public required int TotalDelta { get; set; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; set; }
}
