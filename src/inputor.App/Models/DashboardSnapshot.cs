namespace Inputor.App.Models;

public sealed class DashboardSnapshot
{
    public required DateOnly Today { get; init; }
    public required DateTime SessionStartedAt { get; init; }
    public required string StatusMessage { get; init; }
    public required string CurrentAppName { get; init; }
    public required bool IsCurrentTargetSupported { get; init; }
    public required bool IsPaused { get; init; }
    public required bool ShowAdminReminder { get; init; }
    public required IReadOnlyList<AppStat> AppStats { get; init; }
    public required IReadOnlyList<RecentActivityEntry> RecentActivity { get; init; }
    public int TotalToday => AppStats.Sum(item => item.TodayCount);
    public int TotalSession => AppStats.Sum(item => item.SessionCount);
    public int TotalAllTime => AppStats.Sum(item => item.TotalCount);
}
