using System.Text.Json.Serialization;

namespace Inputor.App.Models;

public sealed class AppStat
{
    public string AppName { get; init; } = string.Empty;
    public int TodayCount { get; set; }
    [JsonIgnore]
    public int SessionCount { get; set; }
    public int TotalCount { get; set; }
}
