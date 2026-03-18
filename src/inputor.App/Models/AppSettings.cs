namespace Inputor.App.Models;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowAdminReminder { get; set; } = true;
    public bool PrivacyMode { get; set; } = true;
    public string ExcludedApps { get; set; } = "inputor.app";

    public bool IsExcluded(string processName)
    {
        var parts = ExcludedApps
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(part => string.Equals(part, processName, StringComparison.OrdinalIgnoreCase));
    }
}
