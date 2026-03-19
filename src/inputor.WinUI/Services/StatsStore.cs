using System.Text.Json;
using Inputor.App.Models;

namespace Inputor.App.Services;

public sealed class StatsStore : IDisposable
{
    private const int MaxRecentActivityEntries = 8;
    private const int MaxDebugEventEntries = 40;
    private const int MaxDailyHistoryEntries = 90;

    private readonly object _syncRoot = new();
    private readonly string _statsPath;
    private readonly Dictionary<string, AppStat> _stats;
    private readonly List<DailyTotalEntry> _dailyHistory;
    private readonly List<DailyAppTotalEntry> _dailyAppHistory;
    private readonly List<RecentActivityEntry> _recentActivity = [];
    private readonly List<DebugEventEntry> _debugEvents = [];
    private DateOnly _today;
    private DateTime _sessionStartedAt;
    private string _statusMessage = StatusText.MonitoringNotStartedYet();
    private string _currentAppName = StatusText.IdleDisplayName();
    private string? _currentProcessName;
    private bool _isCurrentTargetSupported;
    private bool _isPaused;
    private bool _showAdminReminder = true;
    private bool _isDebugCaptureEnabled;

    public StatsStore(string dataDirectory)
    {
        _statsPath = Path.Combine(dataDirectory, "stats.json");
        (_today, _stats, _dailyHistory, _dailyAppHistory) = Load();
        _sessionStartedAt = DateTime.Now;
    }

    public event EventHandler? Changed;

    public string CurrentAppName
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentAppName;
            }
        }
    }

    public bool IsCurrentTargetSupported
    {
        get
        {
            lock (_syncRoot)
            {
                return _isCurrentTargetSupported;
            }
        }
    }

    public string? CurrentProcessName
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentProcessName;
            }
        }
    }

    public void RecordDelta(string appName, int delta)
    {
        lock (_syncRoot)
        {
            ResetTodayIfNeededLocked();

            if (!_stats.TryGetValue(appName, out var stat))
            {
                stat = new AppStat { AppName = appName };
                _stats[appName] = stat;
            }

            stat.TodayCount += delta;
            stat.SessionCount += delta;
            stat.TotalCount += delta;
            _recentActivity.Insert(0, new RecentActivityEntry
            {
                AppName = appName,
                Delta = delta,
                Timestamp = DateTime.Now
            });
            if (_recentActivity.Count > MaxRecentActivityEntries)
            {
                _recentActivity.RemoveRange(MaxRecentActivityEntries, _recentActivity.Count - MaxRecentActivityEntries);
            }
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string statusMessage, string currentAppName, bool isCurrentTargetSupported, string? currentProcessName = null)
    {
        lock (_syncRoot)
        {
            ResetTodayIfNeededLocked();
            _statusMessage = statusMessage;
            _currentAppName = currentAppName;
            _currentProcessName = currentProcessName;
            _isCurrentTargetSupported = isCurrentTargetSupported;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddDebugEvent(DebugEventEntry entry)
    {
        lock (_syncRoot)
        {
            _debugEvents.Insert(0, entry);
            if (_debugEvents.Count > MaxDebugEventEntries)
            {
                _debugEvents.RemoveRange(MaxDebugEventEntries, _debugEvents.Count - MaxDebugEventEntries);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDebugCaptureEnabled(bool isEnabled)
    {
        lock (_syncRoot)
        {
            _isDebugCaptureEnabled = isEnabled;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDebugEvents()
    {
        lock (_syncRoot)
        {
            _debugEvents.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool isPaused)
    {
        lock (_syncRoot)
        {
            _isPaused = isPaused;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetAdminReminderVisible(bool isVisible)
    {
        lock (_syncRoot)
        {
            _showAdminReminder = isVisible;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetSession()
    {
        lock (_syncRoot)
        {
            _sessionStartedAt = DateTime.Now;
            _recentActivity.Clear();
            _debugEvents.Clear();
            foreach (var stat in _stats.Values)
            {
                stat.SessionCount = 0;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAllStatistics()
    {
        lock (_syncRoot)
        {
            _today = DateOnly.FromDateTime(DateTime.Now);
            _sessionStartedAt = DateTime.Now;
            _stats.Clear();
            _dailyHistory.Clear();
            _recentActivity.Clear();
            _debugEvents.Clear();
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public DashboardSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            ResetTodayIfNeededLocked();
            return new DashboardSnapshot
            {
                Today = _today,
                SessionStartedAt = _sessionStartedAt,
                StatusMessage = _statusMessage,
                CurrentAppName = _currentAppName,
                IsCurrentTargetSupported = _isCurrentTargetSupported,
                IsPaused = _isPaused,
                ShowAdminReminder = _showAdminReminder,
                IsDebugCaptureEnabled = _isDebugCaptureEnabled,
                DailyHistory = BuildDailyHistoryLocked(),
                DailyAppHistory = BuildDailyAppHistoryLocked(),
                AppStats = _stats.Values
                    .OrderByDescending(item => item.TodayCount)
                    .ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new AppStat
                    {
                        AppName = item.AppName,
                        TodayCount = item.TodayCount,
                        SessionCount = item.SessionCount,
                        TotalCount = item.TotalCount
                    })
                    .ToList(),
                RecentActivity = _recentActivity
                    .Select(item => new RecentActivityEntry
                    {
                        AppName = item.AppName,
                        Delta = item.Delta,
                        Timestamp = item.Timestamp
                    })
                    .ToList(),
                DebugEvents = _debugEvents
                    .Select(item => new DebugEventEntry
                    {
                        Timestamp = item.Timestamp,
                        AppName = item.AppName,
                        StatusMessage = item.StatusMessage,
                        ControlTypeName = item.ControlTypeName,
                        Delta = item.Delta,
                        InsertedSegmentLength = item.InsertedSegmentLength,
                        InsertedSupportedCharacterCount = item.InsertedSupportedCharacterCount,
                        InsertedChineseCharacterCount = item.InsertedChineseCharacterCount,
                        InsertedEnglishLetterCount = item.InsertedEnglishLetterCount,
                        IsPendingComposition = item.IsPendingComposition,
                        IsPaste = item.IsPaste,
                        IsBulkContentLoad = item.IsBulkContentLoad,
                        IsNativeImeInputMode = item.IsNativeImeInputMode,
                        IsCurrentTargetSupported = item.IsCurrentTargetSupported
                    })
                    .ToList()
            };
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            PersistLocked();
        }
    }

    private (DateOnly Today, Dictionary<string, AppStat> Stats, List<DailyTotalEntry> DailyHistory, List<DailyAppTotalEntry> DailyAppHistory) Load()
    {
        if (!File.Exists(_statsPath))
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
        }

        try
        {
            var json = File.ReadAllText(_statsPath);
            var persisted = JsonSerializer.Deserialize<PersistedStats>(json);
            if (persisted is null)
            {
                return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
            }

            var stats = persisted.AppStats
                .ToDictionary(item => item.AppName, StringComparer.OrdinalIgnoreCase);
            var dailyHistory = (persisted.DailyHistory ?? [])
                .Where(item => item.TotalCount >= 0)
                .OrderBy(item => item.Date)
                .TakeLast(MaxDailyHistoryEntries)
                .ToList();
            var dailyAppHistory = (persisted.DailyAppHistory ?? [])
                .Where(item => item.TotalCount >= 0 && !string.IsNullOrWhiteSpace(item.AppName))
                .OrderBy(item => item.Date)
                .ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var today = persisted.Today == default
                ? DateOnly.FromDateTime(DateTime.Now)
                : persisted.Today;

            if (today != DateOnly.FromDateTime(DateTime.Now))
            {
                AppendDailyHistory(dailyHistory, today, stats.Values.Sum(item => item.TodayCount));
                AppendDailyAppHistory(dailyAppHistory, today, stats.Values);
                foreach (var stat in stats.Values)
                {
                    stat.TodayCount = 0;
                }

                today = DateOnly.FromDateTime(DateTime.Now);
            }

            return (today, stats, dailyHistory, dailyAppHistory);
        }
        catch
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
        }
    }

    private void ResetTodayIfNeededLocked()
    {
        var actualToday = DateOnly.FromDateTime(DateTime.Now);
        if (_today == actualToday)
        {
            return;
        }

        AppendDailyHistory(_dailyHistory, _today, _stats.Values.Sum(item => item.TodayCount));
        AppendDailyAppHistory(_dailyAppHistory, _today, _stats.Values);
        _today = actualToday;
        foreach (var stat in _stats.Values)
        {
            stat.TodayCount = 0;
        }

        PersistLocked();
    }

    private void PersistLocked()
    {
        var payload = new PersistedStats
        {
            Today = _today,
            DailyHistory = _dailyHistory.ToList(),
            DailyAppHistory = _dailyAppHistory.ToList(),
            AppStats = _stats.Values.OrderBy(item => item.AppName, StringComparer.OrdinalIgnoreCase).ToList()
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statsPath, json);
    }

    private IReadOnlyList<DailyTotalEntry> BuildDailyHistoryLocked()
    {
        var history = _dailyHistory
            .OrderBy(item => item.Date)
            .ToList();
        AppendDailyHistory(history, _today, _stats.Values.Sum(item => item.TodayCount));
        return history;
    }

    private IReadOnlyList<DailyAppTotalEntry> BuildDailyAppHistoryLocked()
    {
        var history = _dailyAppHistory
            .OrderBy(item => item.Date)
            .ThenBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppendDailyAppHistory(history, _today, _stats.Values);
        return history;
    }

    private static void AppendDailyHistory(List<DailyTotalEntry> history, DateOnly date, int totalCount)
    {
        var entry = new DailyTotalEntry
        {
            Date = date,
            TotalCount = totalCount
        };

        var existingIndex = history.FindIndex(item => item.Date == date);
        if (existingIndex >= 0)
        {
            history[existingIndex] = entry;
        }
        else
        {
            history.Add(entry);
        }

        history.Sort((left, right) => left.Date.CompareTo(right.Date));
        if (history.Count > MaxDailyHistoryEntries)
        {
            history.RemoveRange(0, history.Count - MaxDailyHistoryEntries);
        }
    }

    private static void AppendDailyAppHistory(List<DailyAppTotalEntry> history, DateOnly date, IEnumerable<AppStat> stats)
    {
        history.RemoveAll(item => item.Date == date);

        foreach (var stat in stats.Where(item => item.TodayCount > 0))
        {
            history.Add(new DailyAppTotalEntry
            {
                Date = date,
                AppName = stat.AppName,
                TotalCount = stat.TodayCount
            });
        }

        history.Sort((left, right) =>
        {
            var byDate = left.Date.CompareTo(right.Date);
            return byDate != 0 ? byDate : StringComparer.OrdinalIgnoreCase.Compare(left.AppName, right.AppName);
        });

        var cutoff = date.AddDays(-(MaxDailyHistoryEntries - 1));
        history.RemoveAll(item => item.Date < cutoff);
    }

    private sealed class PersistedStats
    {
        public DateOnly Today { get; init; }
        public List<DailyTotalEntry>? DailyHistory { get; init; }
        public List<DailyAppTotalEntry>? DailyAppHistory { get; init; }
        public List<AppStat> AppStats { get; init; } = [];
    }
}
