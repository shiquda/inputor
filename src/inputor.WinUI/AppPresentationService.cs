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
    private static readonly Dictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte[]> IconBytesCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> IconLoadsInFlight = new(StringComparer.OrdinalIgnoreCase);

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

    public static IReadOnlyList<AppAggregate> BuildAggregates(IEnumerable<AppStat> stats)
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
                    DisplayName = representative.DisplayName,
                    IconGlyph = representative.IconGlyph,
                    ProcessNames = processNames,
                    TodayCount = group.Sum(item => item.TodayCount),
                    SessionCount = group.Sum(item => item.SessionCount),
                    TotalCount = group.Sum(item => item.TotalCount)
                };
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
        foreach (var processName in processNames)
        {
            if (IconCache.TryGetValue(processName, out var iconSource))
            {
                return iconSource;
            }
        }

        return null;
    }

    public static void WarmIcons(IReadOnlyList<string> processNames, DispatcherQueue dispatcherQueue)
    {
        foreach (var processName in processNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IconCache.ContainsKey(processName) || IconLoadsInFlight.ContainsKey(processName))
            {
                continue;
            }

            if (!IconLoadsInFlight.TryAdd(processName, true))
            {
                continue;
            }

            _ = Task.Run(() => LoadIconBytes(processName))
                .ContinueWith(task =>
                {
                    IconLoadsInFlight.TryRemove(processName, out _);

                    if (task.Status != TaskStatus.RanToCompletion || task.Result is null)
                    {
                        return;
                    }

                    IconBytesCache[processName] = task.Result;
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        if (IconCache.ContainsKey(processName))
                        {
                            return;
                        }

                        var iconSource = CreateImageSource(task.Result);
                        if (iconSource is null)
                        {
                            return;
                        }

                        IconCache[processName] = iconSource;
                        IconsChanged?.Invoke(null, EventArgs.Empty);
                    });
                }, TaskScheduler.Default);
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

    private static byte[]? LoadIconBytes(string processName)
    {
        if (IconBytesCache.TryGetValue(processName, out var cachedBytes))
        {
            return cachedBytes;
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
                    return stream.ToArray();
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
