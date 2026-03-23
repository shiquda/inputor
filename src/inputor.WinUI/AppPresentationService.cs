using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Inputor.App.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Inputor.WinUI;

internal static class AppPresentationService
{
    private const int MaxCachedIconBytes = 1024 * 1024;

    private static readonly object IconCacheLock = new();
    private static readonly Dictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte[]> IconBytesCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> IconLoadsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string IconCacheDirectory = AppVariant.GetIconCacheDirectory();
    private static int IconCacheGeneration;

    private static readonly Dictionary<string, AppPresentationDefinition> Definitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome"] = new("google-chrome", AppStrings.Get("AppPresentation.GoogleChrome"), "\uE774"),
            ["msedge"] = new("microsoft-edge", AppStrings.Get("AppPresentation.MicrosoftEdge"), "\uE774"),
            ["msedgewebview2"] = new("microsoft-edge", AppStrings.Get("AppPresentation.MicrosoftEdge"), "\uE774"),
            ["firefox"] = new("mozilla-firefox", AppStrings.Get("AppPresentation.MozillaFirefox"), "\uE774"),
            ["brave"] = new("brave", AppStrings.Get("AppPresentation.Brave"), "\uE774"),
            ["notepad"] = new("notepad", AppStrings.Get("AppPresentation.Notepad"), "\uE70B"),
            ["notepad++"] = new("notepad-plus-plus", AppStrings.Get("AppPresentation.NotepadPlusPlus"), "\uE70B"),
            ["code"] = new("visual-studio-code", AppStrings.Get("AppPresentation.VisualStudioCode"), "\uE943"),
            ["devenv"] = new("visual-studio", AppStrings.Get("AppPresentation.VisualStudio"), "\uE943"),
            ["rider64"] = new("jetbrains-rider", AppStrings.Get("AppPresentation.JetBrainsRider"), "\uE943"),
            ["idea64"] = new("intellij-idea", AppStrings.Get("AppPresentation.IntelliJIdea"), "\uE943"),
            ["wechat"] = new("wechat", AppStrings.Get("AppPresentation.WeChat"), "\uE8BD"),
            ["qq"] = new("qq", AppStrings.Get("AppPresentation.QQ"), "\uE8BD"),
            ["telegram"] = new("telegram", AppStrings.Get("AppPresentation.Telegram"), "\uE8BD"),
            ["slack"] = new("slack", AppStrings.Get("AppPresentation.Slack"), "\uE8BD"),
            ["teams"] = new("microsoft-teams", AppStrings.Get("AppPresentation.MicrosoftTeams"), "\uE8BD"),
            ["spotify"] = new("spotify", AppStrings.Get("AppPresentation.Spotify"), "\uE189"),
            ["music.ui"] = new("windows-media-player", AppStrings.Get("AppPresentation.MediaPlayer"), "\uE189"),
            ["obs64"] = new("obs-studio", AppStrings.Get("AppPresentation.ObsStudio"), "\uE7FC"),
            ["winword"] = new("microsoft-word", AppStrings.Get("AppPresentation.MicrosoftWord"), "\uE8A5"),
            ["excel"] = new("microsoft-excel", AppStrings.Get("AppPresentation.MicrosoftExcel"), "\uE9D5"),
            ["powerpnt"] = new("microsoft-powerpoint", AppStrings.Get("AppPresentation.MicrosoftPowerPoint"), "\uE8A5")
        };

    public static event EventHandler? IconsChanged;

    public static string DescribeDisplayName(string processName)
    {
        return Describe(processName).DisplayName;
    }

    public static DashboardSnapshot CreateVisibleSnapshot(DashboardSnapshot snapshot, AppSettings settings)
    {
        var visibleAppStats = snapshot.AppStats
            .Where(item => !settings.IsExcluded(item.AppName))
            .ToList();

        var visibleDailyAppHistory = snapshot.DailyAppHistory
            .Where(item => !settings.IsExcluded(item.AppName))
            .ToList();

        var totalsByDate = visibleDailyAppHistory
            .GroupBy(item => item.Date)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.TotalCount));

        var visibleDailyHistory = snapshot.DailyHistory
            .Select(item => new DailyTotalEntry
            {
                Date = item.Date,
                TotalCount = totalsByDate.TryGetValue(item.Date, out var total) ? total : 0
            })
            .ToList();

        return new DashboardSnapshot
        {
            Today = snapshot.Today,
            SessionStartedAt = snapshot.SessionStartedAt,
            StatusMessage = snapshot.StatusMessage,
            CurrentAppName = snapshot.CurrentAppName,
            IsCurrentTargetSupported = snapshot.IsCurrentTargetSupported,
            IsPaused = snapshot.IsPaused,
            IsDebugCaptureEnabled = snapshot.IsDebugCaptureEnabled,
            IsDebugDiskLogEnabled = snapshot.IsDebugDiskLogEnabled,
            DebugDiskLogPath = snapshot.DebugDiskLogPath,
            DebugDiskLogIncludeRawText = snapshot.DebugDiskLogIncludeRawText,
            AppStats = visibleAppStats,
            DailyHistory = visibleDailyHistory,
            DailyAppHistory = visibleDailyAppHistory,
            RecentActivity = snapshot.RecentActivity
                .Where(item => !settings.IsExcluded(item.AppName))
                .ToList(),
            DebugEvents = snapshot.DebugEvents
                .Where(item => !settings.IsExcluded(item.AppName))
                .ToList()
        };
    }

    public static IReadOnlyList<AppAggregate> BuildAggregates(IEnumerable<AppStat> stats, AppSettings? settings = null)
    {
        return stats
            .GroupBy(item => Describe(item.AppName).GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var representative = Describe(group.First().AppName);
                var processNames = group
                    .Select(item => item.AppName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new AppAggregate
                {
                    GroupKey = representative.GroupKey,
                    DisplayName = settings?.GetAliasForGroup(representative.GroupKey) ?? representative.DisplayName,
                    IconGlyph = representative.IconGlyph,
                    ProcessNames = processNames,
                    TodayCount = group.Sum(item => item.TodayCount),
                    SessionCount = group.Sum(item => item.SessionCount),
                    TotalCount = group.Sum(item => item.TotalCount)
                };
            })
            .ToList();
    }

    public static IReadOnlyList<AppAggregate> BuildTagAggregates(IEnumerable<AppStat> stats, AppSettings settings)
    {
        const string UntaggedGroupKey = "tag:__untagged__";
        var rows = new List<(string GroupKey, string DisplayName, string ProcessName, AppStat Stat)>();
        foreach (var stat in stats)
        {
            var tags = settings.GetTagsForApp(stat.AppName);
            if (tags.Count == 0)
            {
                rows.Add((UntaggedGroupKey, AppStrings.Get("Aggregation.Untagged"), stat.AppName, stat));
                continue;
            }

            foreach (var tag in tags)
            {
                rows.Add(($"tag:{tag}", tag, stat.AppName, stat));
            }
        }

        return rows
            .GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppAggregate
            {
                GroupKey = group.Key,
                DisplayName = group.First().DisplayName,
                IconGlyph = "\uE8EC",
                ProcessNames = group.Select(item => item.ProcessName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                TodayCount = group.Sum(item => item.Stat.TodayCount),
                SessionCount = group.Sum(item => item.Stat.SessionCount),
                TotalCount = group.Sum(item => item.Stat.TotalCount)
            })
            .ToList();
    }

    public static bool MatchesQuery(AppAggregate aggregate, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return aggregate.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || aggregate.ProcessNames.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public static ImageSource? TryGetIconSource(IReadOnlyList<string> processNames)
    {
        lock (IconCacheLock)
        {
            foreach (var processName in processNames)
            {
                if (IconCache.TryGetValue(processName, out var iconSource))
                {
                    return iconSource;
                }
            }
        }

        return null;
    }

    public static void WarmIcons(IReadOnlyList<string> processNames, DispatcherQueue dispatcherQueue)
    {
        foreach (var processName in processNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (HasLoadedIcon(processName) || IconLoadsInFlight.ContainsKey(processName))
            {
                continue;
            }

            var generation = IconCacheGeneration;

            if (!IconLoadsInFlight.TryAdd(processName, true))
            {
                continue;
            }

            _ = Task.Run(() => LoadIconBytes(processName, generation))
                .ContinueWith(task =>
                {
                    IconLoadsInFlight.TryRemove(processName, out _);

                    if (task.Status != TaskStatus.RanToCompletion || task.Result is null || generation != IconCacheGeneration)
                    {
                        return;
                    }

                    IconBytesCache[processName] = task.Result;
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (HasLoadedIcon(processName))
                        {
                            return;
                        }

                        var iconSource = CreateImageSource(task.Result);
                        if (iconSource is null)
                        {
                            return;
                        }

                        lock (IconCacheLock)
                        {
                            IconCache[processName] = iconSource;
                        }

                        IconsChanged?.Invoke(null, EventArgs.Empty);
                    });
                }, TaskScheduler.Default);
        }
    }

    public static void ClearIconCache()
    {
        Exception? deleteException = null;

        IconCacheGeneration++;

        lock (IconCacheLock)
        {
            IconCache.Clear();
        }

        IconBytesCache.Clear();
        IconLoadsInFlight.Clear();

        try
        {
            if (Directory.Exists(IconCacheDirectory))
            {
                Directory.Delete(IconCacheDirectory, recursive: true);
            }
        }
        catch (Exception exception)
        {
            deleteException = exception;
            StartupDiagnostics.Log($"ClearIconCache failed to delete disk cache: {exception}");
        }

        IconsChanged?.Invoke(null, EventArgs.Empty);

        if (deleteException is not null)
        {
            throw deleteException;
        }
    }

    private static AppPresentationDefinition Describe(string processName)
    {
        if (Definitions.TryGetValue(processName, out var definition))
        {
            return definition;
        }

        return new AppPresentationDefinition(
            processName.ToLowerInvariant(),
            Humanize(processName),
            "\uE71D");
    }

    private static string Humanize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return AppStrings.Get("AppPresentation.UnknownApp");
        }

        var normalized = processName
            .Replace(".", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal);

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static byte[]? LoadIconBytes(string processName, int generation)
    {
        if (generation != IconCacheGeneration)
        {
            return null;
        }

        if (IconBytesCache.TryGetValue(processName, out var cachedBytes))
        {
            return cachedBytes;
        }

        var cacheFilePath = GetIconCacheFilePath(processName);
        if (cacheFilePath is not null)
        {
            var diskBytes = TryReadCachedIconBytes(cacheFilePath, processName);
            if (diskBytes is not null)
            {
                if (generation != IconCacheGeneration)
                {
                    return null;
                }

                IconBytesCache[processName] = diskBytes;
                return diskBytes;
            }
        }

        try
        {
            var executablePath = Process.GetProcessesByName(processName)
                .Select(process =>
                {
                    try
                    {
                        return process.MainModule?.FileName;
                    }
                    catch
                    {
                        return null;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                })
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    using var bitmap = icon.ToBitmap();
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, ImageFormat.Png);
                    var iconBytes = stream.ToArray();

                    if (generation != IconCacheGeneration)
                    {
                        return null;
                    }

                    TryPersistIconBytes(cacheFilePath, iconBytes, generation);
                    return iconBytes;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static ImageSource? CreateImageSource(byte[] pngBytes)
    {
        try
        {
            using var randomAccessStream = new InMemoryRandomAccessStream();
            using var outputStream = randomAccessStream.GetOutputStreamAt(0);
            using var writer = new DataWriter(outputStream);
            writer.WriteBytes(pngBytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            outputStream.FlushAsync().AsTask().GetAwaiter().GetResult();
            randomAccessStream.Seek(0);

            var bitmapImage = new BitmapImage();
            bitmapImage.SetSource(randomAccessStream);
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasLoadedIcon(string processName)
    {
        lock (IconCacheLock)
        {
            return IconCache.ContainsKey(processName);
        }
    }

    private static string? GetIconCacheFilePath(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var safeFileName = new string(processName
            .Trim()
            .Select(character => invalidFileNameChars.Contains(character) ? '_' : character)
            .ToArray());

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return null;
        }

        return Path.Combine(IconCacheDirectory, $"{safeFileName}.png");
    }

    private static byte[]? TryReadCachedIconBytes(string cacheFilePath, string processName)
    {
        try
        {
            using var stream = new FileStream(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= 0)
            {
                return null;
            }

            if (stream.Length > MaxCachedIconBytes)
            {
                StartupDiagnostics.Log($"LoadIconBytes ignored oversized cached icon for {processName}: {stream.Length} bytes.");
                return null;
            }

            var diskBytes = new byte[stream.Length];
            var totalRead = 0;
            while (totalRead < diskBytes.Length)
            {
                var bytesRead = stream.Read(diskBytes, totalRead, diskBytes.Length - totalRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            if (totalRead == 0)
            {
                return null;
            }

            if (totalRead != diskBytes.Length)
            {
                Array.Resize(ref diskBytes, totalRead);
            }

            return diskBytes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"LoadIconBytes failed to read cached icon for {processName}: {exception}");
            return null;
        }
    }

    private static void TryPersistIconBytes(string? cacheFilePath, byte[] iconBytes, int generation)
    {
        if (cacheFilePath is null || iconBytes.Length == 0 || iconBytes.Length > MaxCachedIconBytes || generation != IconCacheGeneration)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(IconCacheDirectory);

            if (generation != IconCacheGeneration)
            {
                return;
            }

            File.WriteAllBytes(cacheFilePath, iconBytes);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"TryPersistIconBytes failed for {cacheFilePath}: {exception}");
        }
    }

    private sealed record AppPresentationDefinition(string GroupKey, string DisplayName, string IconGlyph);
}

internal sealed class AppAggregate
{
    public required string GroupKey { get; init; }
    public required string DisplayName { get; init; }
    public required string IconGlyph { get; init; }
    public required IReadOnlyList<string> ProcessNames { get; init; }
    public required int TodayCount { get; init; }
    public required int SessionCount { get; init; }
    public required int TotalCount { get; init; }
}
