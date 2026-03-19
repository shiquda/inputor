using System.Text.Json;
using Inputor.App.Models;

namespace Inputor.App.Services;

public sealed class StatsStore : IDisposable
{
    private const int MaxRecentActivityEntries = 8;
    private const int MaxDebugEventEntries = 40;

    private readonly object _syncRoot = new();
    private readonly string _statsPath;
    private readonly Dictionary<string, AppStat> _stats;
    private readonly List<RecentActivityEntry> _recentActivity = [];
    private readonly List<DebugEventEntry> _debugEvents = [];
    private DateOnly _today;
    private DateTime _sessionStartedAt;
    private string _statusMessage = "Monitoring has not started yet.";
    private string _currentAppName = "Idle";
    private bool _isCurrentTargetSupported;
    private bool _isPaused;
    private bool _showAdminReminder = true;

    public StatsStore(string dataDirectory)
    {
        _statsPath = Path.Combine(dataDirectory, "stats.json");
        (_today, _stats) = Load();
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

    public void SetStatus(string statusMessage, string currentAppName, bool isCurrentTargetSupported)
    {
        lock (_syncRoot)
        {
            ResetTodayIfNeededLocked();
            _statusMessage = statusMessage;
            _currentAppName = currentAppName;
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

    private (DateOnly Today, Dictionary<string, AppStat> Stats) Load()
    {
        if (!File.Exists(_statsPath))
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var json = File.ReadAllText(_statsPath);
            var persisted = JsonSerializer.Deserialize<PersistedStats>(json);
            if (persisted is null)
            {
                return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase));
            }

            var stats = persisted.AppStats
                .ToDictionary(item => item.AppName, StringComparer.OrdinalIgnoreCase);
            var today = persisted.Today == default
                ? DateOnly.FromDateTime(DateTime.Now)
                : persisted.Today;

            if (today != DateOnly.FromDateTime(DateTime.Now))
            {
                foreach (var stat in stats.Values)
                {
                    stat.TodayCount = 0;
                }

                today = DateOnly.FromDateTime(DateTime.Now);
            }

            return (today, stats);
        }
        catch
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private void ResetTodayIfNeededLocked()
    {
        var actualToday = DateOnly.FromDateTime(DateTime.Now);
        if (_today == actualToday)
        {
            return;
        }

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
            AppStats = _stats.Values.OrderBy(item => item.AppName, StringComparer.OrdinalIgnoreCase).ToList()
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statsPath, json);
    }

    private sealed class PersistedStats
    {
        public DateOnly Today { get; init; }
        public List<AppStat> AppStats { get; init; } = [];
    }
}
