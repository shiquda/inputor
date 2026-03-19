namespace Inputor.App.Models;

public sealed class AppSettings
{
    private static readonly string[] AlwaysExcludedApps = ["inputor.app", "betterlyrics.winui3"];

    public bool StartWithWindows { get; set; }
    public bool ShowAdminReminder { get; set; } = true;
    public bool PrivacyMode { get; set; } = true;
    public bool DebugCaptureEnabled { get; set; }
    public string Language { get; set; } = string.Empty;
    public string ExcludedApps { get; set; } = "inputor.app";

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
}
