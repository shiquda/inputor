using System.Globalization;
using System.Xml.Linq;

namespace Inputor.WinUI;

internal static partial class AppStrings
{
    private static readonly Dictionary<string, string> Strings = new(StringComparer.OrdinalIgnoreCase);

    public static string CurrentLanguageTag { get; private set; } = "en-US";

    public static IReadOnlyList<AppLanguageOption> GetLanguageOptions()
    {
        return
        [
            new AppLanguageOption { Tag = string.Empty, DisplayName = Get("Language.FollowSystem") },
            new AppLanguageOption { Tag = "en-US", DisplayName = Get("Language.English") },
            new AppLanguageOption { Tag = "zh-Hans", DisplayName = Get("Language.SimplifiedChinese") }
        ];
    }

    public static void Initialize(string appBaseDirectory, string? requestedLanguageTag)
    {
        var resolvedLanguageTag = ResolveLanguageTag(requestedLanguageTag);
        var resourceRoot = Path.Combine(appBaseDirectory, "Strings");

        Strings.Clear();
        LoadLanguageFile(Path.Combine(resourceRoot, "en-US", "Resources.resw"), Strings);
        if (!string.Equals(resolvedLanguageTag, "en-US", StringComparison.OrdinalIgnoreCase))
        {
            LoadLanguageFile(Path.Combine(resourceRoot, resolvedLanguageTag, "Resources.resw"), Strings);
        }

        CurrentLanguageTag = resolvedLanguageTag;

        var culture = CultureInfo.GetCultureInfo(GetCultureTagForFormatting(resolvedLanguageTag));
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string ResolveLanguageTag(string? requestedLanguageTag)
    {
        var normalized = NormalizeLanguageTag(requestedLanguageTag);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return NormalizeLanguageTag(CultureInfo.InstalledUICulture.Name) ?? "en-US";
    }

    public static string Get(string key)
    {
        return Strings.TryGetValue(key, out var value) ? value : key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private static string? NormalizeLanguageTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-Hans";
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        return null;
    }

    private static string GetCultureTagForFormatting(string resolvedLanguageTag)
    {
        return string.Equals(resolvedLanguageTag, "zh-Hans", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";
    }

    private static void LoadLanguageFile(string filePath, IDictionary<string, string> destination)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var document = XDocument.Load(filePath);
        foreach (var element in document.Root?.Elements("data") ?? [])
        {
            var key = element.Attribute("name")?.Value;
            var value = element.Element("value")?.Value;
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            destination[key] = value;
        }
    }
}
