using System.Reflection;
using System.Text.RegularExpressions;

namespace Inputor.WinUI;

internal static class VersionInfo
{
    private static readonly Assembly Assembly = typeof(VersionInfo).Assembly;
    private static readonly Regex CommitHashPattern = new("^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);
    private static readonly string InformationalVersion =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static string Channel => AppVariant.ChannelName;

    public static string DisplayVersion => InformationalVersion.Split('+')[0];

    public static string BuildVersion => TryGetShortCommitHash()
        ?? Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? Assembly.GetName().Version?.ToString()
        ?? "0.0.0.0";

    private static string? TryGetShortCommitHash()
    {
        var parts = InformationalVersion.Split('+', 2);
        if (parts.Length < 2)
        {
            return null;
        }

        var metadata = parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in metadata)
        {
            if (!CommitHashPattern.IsMatch(part))
            {
                continue;
            }

            return part[..Math.Min(7, part.Length)].ToLowerInvariant();
        }

        return null;
    }
}
