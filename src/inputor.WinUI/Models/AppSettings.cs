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
    public List<AppAliasMapping> AppAliasMappings { get; set; } = [];

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

    public string? GetAliasForGroup(string groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return null;
        }

        return GetNormalizedAliasMappings()
            .FirstOrDefault(item => string.Equals(item.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
            ?.Alias;
    }

    public IReadOnlyList<AppAliasMapping> GetNormalizedAliasMappings()
    {
        return AppAliasMappings
            .Where(item => !string.IsNullOrWhiteSpace(item.GroupKey) && !string.IsNullOrWhiteSpace(item.Alias))
            .Select(item => new AppAliasMapping
            {
                GroupKey = item.GroupKey.Trim(),
                Alias = item.Alias.Trim()
            })
            .Where(item => item.GroupKey.Length > 0 && item.Alias.Length > 0)
            .GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppAliasMapping
            {
                GroupKey = group.First().GroupKey,
                Alias = group.Last().Alias
            })
            .OrderBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool SetAliasForGroup(string groupKey, string? alias)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return false;
        }

        var normalizedGroupKey = groupKey.Trim();
        var normalizedAlias = (alias ?? string.Empty).Trim();
        var aliases = GetNormalizedAliasMappings().ToList();
        var existingIndex = aliases.FindIndex(item => string.Equals(item.GroupKey, normalizedGroupKey, StringComparison.OrdinalIgnoreCase));

        if (normalizedAlias.Length == 0)
        {
            if (existingIndex < 0)
            {
                return false;
            }

            aliases.RemoveAt(existingIndex);
            AppAliasMappings = aliases;
            return true;
        }

        if (existingIndex >= 0 && string.Equals(aliases[existingIndex].Alias, normalizedAlias, StringComparison.Ordinal))
        {
            return false;
        }

        var mapping = new AppAliasMapping
        {
            GroupKey = normalizedGroupKey,
            Alias = normalizedAlias
        };

        if (existingIndex >= 0)
        {
            aliases[existingIndex] = mapping;
        }
        else
        {
            aliases.Add(mapping);
        }

        AppAliasMappings = aliases;
        return true;
    }

    public bool ReplaceTagsForApps(IEnumerable<string> processNames, IEnumerable<string> tags)
    {
        var normalizedProcessNames = processNames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedProcessNames.Count == 0)
        {
            return false;
        }

        var normalizedTags = tags
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mappings = GetNormalizedTagMappings().ToList();
        mappings.RemoveAll(item => normalizedProcessNames.Any(processName => string.Equals(processName, item.AppName, StringComparison.OrdinalIgnoreCase)));

        foreach (var processName in normalizedProcessNames)
        {
            if (normalizedTags.Count == 0)
            {
                continue;
            }

            mappings.Add(new AppTagMapping
            {
                AppName = processName,
                Tags = normalizedTags.ToList()
            });
        }

        var updatedMappings = mappings
            .OrderBy(item => item.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentMappings = GetNormalizedTagMappings().ToList();
        if (currentMappings.Count == updatedMappings.Count
            && !currentMappings.Where((item, index) => !string.Equals(item.AppName, updatedMappings[index].AppName, StringComparison.OrdinalIgnoreCase)
                || !item.Tags.SequenceEqual(updatedMappings[index].Tags, StringComparer.OrdinalIgnoreCase)).Any())
        {
            return false;
        }

        AppTagMappings = updatedMappings;
        return true;
    }
}
