namespace Inputor.App.Models;

public sealed class RecentActivityEntry
{
    public required string AppName { get; init; }
    public required int Delta { get; init; }
    public required DateTime Timestamp { get; init; }
}
