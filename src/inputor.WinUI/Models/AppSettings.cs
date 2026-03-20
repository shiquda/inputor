namespace Inputor.App.Models;

public sealed class AppSettings
{
    private static readonly string[] AlwaysExcludedApps = ["inputor.app", "betterlyrics.winui3"];

    public bool StartWithWindows { get; set; }
    public bool PrivacyMode { get; set; } = true;
    public bool DebugCaptureEnabled { get; set; }
    public string ThemeMode { get; set; } = string.Empty;
    public string StatisticsSourcePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ExcludedApps { get; set; } = "inputor.app";
    public List<AppTagMapping> AppTagMappings { get; set; } = [];

    public bool IsExcluded(string processName)
    {
        return GetExcludedApps().Any(part => string.Equals(part, processName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetExcludedApps()
    {
        return AlwaysExcludedApps
            .Concat(ExcludedApps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool AddExcludedApp(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var apps = GetExcludedApps().ToList();
        if (apps.Any(app => string.Equals(app, processName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        apps.Add(processName.Trim());
        ExcludedApps = string.Join(", ", apps.OrderBy(app => app, StringComparer.OrdinalIgnoreCase));
        return true;
    }

    public IReadOnlyList<string> GetTagsForApp(string processName)
    {
        return AppTagMappings
            .Where(item => string.Equals(item.AppName, processName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Tags)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetTagsForApps(IEnumerable<string> processNames)
    {
        return processNames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .SelectMany(GetTagsForApp)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetKnownTags()
    {
        return AppTagMappings
            .SelectMany(item => item.Tags)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AppTagMapping> GetNormalizedTagMappings()
    {
        return AppTagMappings
            .Where(item => !string.IsNullOrWhiteSpace(item.AppName))
            .Select(item => new AppTagMapping
            {
                AppName = item.AppName.Trim(),
                Tags = item.Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(item => item.Tags.Count > 0)
            .GroupBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppTagMapping
            {
                AppName = group.First().AppName,
                Tags = group.SelectMany(item => item.Tags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
