using System.Text.Json;
using Inputor.App.Models;

namespace Inputor.App.Services;

public sealed class StatsStore : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly string _statsPath;
    private readonly Dictionary<string, AppStat> _stats;
    private DateOnly _today;
    private string _statusMessage = "Monitoring has not started yet.";
    private string _currentAppName = "Idle";
    private bool _isCurrentTargetSupported;
    private bool _isPaused;
    private bool _showAdminReminder = true;

    public StatsStore(string dataDirectory)
    {
        _statsPath = Path.Combine(dataDirectory, "stats.json");
        (_today, _stats) = Load();
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
            stat.TotalCount += delta;
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

    public DashboardSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            ResetTodayIfNeededLocked();
            return new DashboardSnapshot
            {
                Today = _today,
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
                        TotalCount = item.TotalCount
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
