using System.Text.Json;
using Inputor.App.Models;
using Inputor.WinUI;

namespace Inputor.App.Services;

public sealed class StatsStore : IDisposable
{
    private const int MaxRecentActivityEntries = 20;
    private const int MaxDebugEventEntries = 120;
    private const int MaxDailyHistoryEntries = 90;

    private readonly object _syncRoot = new();
    private readonly string _defaultStatsPath;
    private string _statsPath;
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
    private bool _isDebugCaptureEnabled;
    private Action<DebugEventEntry>? _debugDiskLogHook;
    private bool _isDebugDiskLogEnabled;
    private string _debugDiskLogPath = string.Empty;
    private bool _debugDiskLogIncludeRawText;

    public StatsStore(string dataDirectory, string? sourcePath = null, bool strictSourceValidation = false)
    {
        _defaultStatsPath = Path.Combine(dataDirectory, "stats.json");
        _statsPath = NormalizeSourcePath(sourcePath, _defaultStatsPath);
        (_today, _stats, _dailyHistory, _dailyAppHistory) = strictSourceValidation
            ? LoadStrict(_statsPath)
            : Load(_statsPath);
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

    public string CurrentSourcePath
    {
        get
        {
            lock (_syncRoot)
            {
                return _statsPath;
            }
        }
    }

    public string DefaultSourcePath => _defaultStatsPath;

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
            var now = DateTime.Now;
            if (_recentActivity.Count > 0 && _recentActivity[0].AppName == appName)
            {
                _recentActivity[0].TotalDelta += delta;
                _recentActivity[0].EndTime = now;
            }
            else
            {
                _recentActivity.Insert(0, new RecentActivityEntry
                {
                    AppName = appName,
                    TotalDelta = delta,
                    StartTime = now,
                    EndTime = now
                });
                if (_recentActivity.Count > MaxRecentActivityEntries)
                {
                    _recentActivity.RemoveRange(MaxRecentActivityEntries, _recentActivity.Count - MaxRecentActivityEntries);
                }
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
        Action<DebugEventEntry>? hook;
        lock (_syncRoot)
        {
            _debugEvents.Insert(0, entry);
            if (_debugEvents.Count > MaxDebugEventEntries)
            {
                _debugEvents.RemoveRange(MaxDebugEventEntries, _debugEvents.Count - MaxDebugEventEntries);
            }

            hook = _debugDiskLogHook;
        }

        // Call hook outside the lock to avoid blocking I/O inside the critical section.
        hook?.Invoke(entry);

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

    public void SetDebugDiskLogHook(Action<DebugEventEntry>? hook)
    {
        lock (_syncRoot)
        {
            _debugDiskLogHook = hook;
        }
    }

    public void SetDebugDiskLogState(bool isEnabled, string path, bool includeRawText)
    {
        lock (_syncRoot)
        {
            _isDebugDiskLogEnabled = isEnabled;
            _debugDiskLogPath = path;
            _debugDiskLogIncludeRawText = includeRawText;
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
            _dailyAppHistory.Clear();
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
                IsDebugCaptureEnabled = _isDebugCaptureEnabled,
                IsDebugDiskLogEnabled = _isDebugDiskLogEnabled,
                DebugDiskLogPath = _debugDiskLogPath,
                DebugDiskLogIncludeRawText = _debugDiskLogIncludeRawText,
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
                        TotalDelta = item.TotalDelta,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime
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
                        InsertedOtherSupportedCharacterCount = item.InsertedOtherSupportedCharacterCount,
                        IsPendingComposition = item.IsPendingComposition,
                        IsPaste = item.IsPaste,
                        IsBulkContentLoad = item.IsBulkContentLoad,
                        IsNativeImeInputMode = item.IsNativeImeInputMode,
                        IsCurrentTargetSupported = item.IsCurrentTargetSupported,
                        TextComparison = item.TextComparison is null
                            ? null
                            : new DebugTextComparison
                            {
                                ChangeStartIndex = item.TextComparison.ChangeStartIndex,
                                PreviousTextLength = item.TextComparison.PreviousTextLength,
                                CurrentTextLength = item.TextComparison.CurrentTextLength,
                                PreviousSegmentLength = item.TextComparison.PreviousSegmentLength,
                                CurrentSegmentLength = item.TextComparison.CurrentSegmentLength,
                                PreviousSupportedCharacterCount = item.TextComparison.PreviousSupportedCharacterCount,
                                PreviousChineseCharacterCount = item.TextComparison.PreviousChineseCharacterCount,
                                PreviousEnglishLetterCount = item.TextComparison.PreviousEnglishLetterCount,
                                CurrentSupportedCharacterCount = item.TextComparison.CurrentSupportedCharacterCount,
                                CurrentChineseCharacterCount = item.TextComparison.CurrentChineseCharacterCount,
                                CurrentEnglishLetterCount = item.TextComparison.CurrentEnglishLetterCount,
                                PreviousText = item.TextComparison.PreviousText,
                                CurrentText = item.TextComparison.CurrentText
                            }
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

    public string BackupCurrentSource(string backupDirectory)
    {
        lock (_syncRoot)
        {
            PersistLocked();
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(
                backupDirectory,
                $"inputor-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(_statsPath, backupPath, overwrite: false);
            return backupPath;
        }
    }

    public void RestoreSource(string? targetPath, string json)
    {
        lock (_syncRoot)
        {
            var normalizedPath = NormalizeSourcePath(targetPath, _defaultStatsPath);
            var parentDirectory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            var (today, stats, dailyHistory, dailyAppHistory) = LoadFromJson(json);
            _statsPath = normalizedPath;
            _today = today;
            _stats.Clear();
            foreach (var pair in stats)
            {
                _stats[pair.Key] = pair.Value;
            }

            _dailyHistory.Clear();
            _dailyHistory.AddRange(dailyHistory);
            _dailyAppHistory.Clear();
            _dailyAppHistory.AddRange(dailyAppHistory);
            _sessionStartedAt = DateTime.Now;
            _recentActivity.Clear();
            _debugEvents.Clear();
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ValidateSourceJson(string json)
    {
        lock (_syncRoot)
        {
            _ = LoadFromJson(json);
        }
    }

    public void SwitchSource(string? sourcePath)
    {
        lock (_syncRoot)
        {
            PersistLocked();

            var normalizedPath = NormalizeSourcePath(sourcePath, _defaultStatsPath);
            var parentDirectory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            if (!File.Exists(normalizedPath))
            {
                File.WriteAllText(normalizedPath, "");
            }

            var (today, stats, dailyHistory, dailyAppHistory) = LoadStrict(normalizedPath);
            _statsPath = normalizedPath;
            _today = today;
            _stats.Clear();
            foreach (var pair in stats)
            {
                _stats[pair.Key] = pair.Value;
            }

            _dailyHistory.Clear();
            _dailyHistory.AddRange(dailyHistory);
            _dailyAppHistory.Clear();
            _dailyAppHistory.AddRange(dailyAppHistory);
            _sessionStartedAt = DateTime.Now;
            _recentActivity.Clear();
            _debugEvents.Clear();
            PersistLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private (DateOnly Today, Dictionary<string, AppStat> Stats, List<DailyTotalEntry> DailyHistory, List<DailyAppTotalEntry> DailyAppHistory) Load(string path)
    {
        try
        {
            return LoadStrict(path);
        }
        catch
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
        }
    }

    private (DateOnly Today, Dictionary<string, AppStat> Stats, List<DailyTotalEntry> DailyHistory, List<DailyAppTotalEntry> DailyAppHistory) LoadStrict(string path)
    {
        if (!File.Exists(path))
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    private (DateOnly Today, Dictionary<string, AppStat> Stats, List<DailyTotalEntry> DailyHistory, List<DailyAppTotalEntry> DailyAppHistory) LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (DateOnly.FromDateTime(DateTime.Now), new Dictionary<string, AppStat>(StringComparer.OrdinalIgnoreCase), [], []);
        }

        var persisted = JsonSerializer.Deserialize<PersistedStats>(json)
            ?? throw new InvalidDataException(AppStrings.Get("Status.StatisticsSourceJsonInvalid"));

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

    private static string NormalizeSourcePath(string? sourcePath, string defaultStatsPath)
    {
        return string.IsNullOrWhiteSpace(sourcePath)
            ? defaultStatsPath
            : Path.GetFullPath(sourcePath.Trim());
    }
}
