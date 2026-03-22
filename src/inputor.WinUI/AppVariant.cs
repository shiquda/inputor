namespace Inputor.WinUI;

internal static class AppVariant
{
#if INPUTOR_DEV_CHANNEL
    public static bool IsDevelopment => true;
    public static string ChannelName => "development";
    public static string DataDirectoryName => "inputor-dev";
    public static string ExportDirectoryName => "inputor-exports-dev";
    public static string BackupDirectoryName => "inputor-backups-dev";
    public static string AutoStartEntryName => "inputor-dev";
#else
    public static bool IsDevelopment => false;
    public static string ChannelName => "release";
    public static string DataDirectoryName => "inputor";
    public static string ExportDirectoryName => "inputor-exports";
    public static string BackupDirectoryName => "inputor-backups";
    public static string AutoStartEntryName => "inputor";
#endif

    public static string GetDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DataDirectoryName);
    }

    public static string GetSettingsPath()
    {
        return Path.Combine(
            GetDataDirectory(),
            "settings.json");
    }

    public static string GetDefaultStatsPath()
    {
        return Path.Combine(
            GetDataDirectory(),
            "stats.json");
    }

    public static string GetExportDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ExportDirectoryName);
    }

    public static string GetBackupDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            BackupDirectoryName);
    }

    public static string GetIconCacheDirectory()
    {
        return Path.Combine(
            GetDataDirectory(),
            "icons");
    }
}
