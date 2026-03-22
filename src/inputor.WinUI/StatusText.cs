using Inputor.WinUI;

namespace Inputor.App.Services;

internal static class StatusText
{
    public static string MonitoringNotStartedYet() => AppStrings.Get("Status.MonitoringNotStartedYet");
    public static string MonitoringStarted() => AppStrings.Get("Status.MonitoringStarted");
    public static string SessionCountersReset() => AppStrings.Get("Status.SessionCountersReset");
    public static string NoActiveAppAvailable() => AppStrings.Get("Status.NoActiveAppAvailable");
    public static string ProcessAlreadyExcluded(string processName) => AppStrings.Format("Status.ProcessAlreadyExcluded", processName);
    public static string AddedExcludedApp(string processName) => AppStrings.Format("Status.AddedExcludedApp", processName);
    public static string ExportedCsv(string path) => AppStrings.Format("Status.ExportedCsv", path);
    public static string StatisticsBackupCreated(string path) => AppStrings.Format("Status.StatisticsBackupCreated", path);
    public static string StatisticsBackupFailed(string message) => AppStrings.Format("Status.StatisticsBackupFailed", message);
    public static string DataDirectoryOpened(string path) => AppStrings.Format("Status.DataDirectoryOpened", path);
    public static string DataDirectoryOpenFailed(string message) => AppStrings.Format("Status.DataDirectoryOpenFailed", message);
    public static string BackupArchiveExported(string path) => AppStrings.Format("Status.BackupArchiveExported", path);
    public static string BackupArchiveExportFailed(string message) => AppStrings.Format("Status.BackupArchiveExportFailed", message);
    public static string BackupArchiveRestored(string path) => AppStrings.Format("Status.BackupArchiveRestored", path);
    public static string BackupArchiveRestoreFailed(string message) => AppStrings.Format("Status.BackupArchiveRestoreFailed", message);
    public static string LegacyStatisticsSourceMigratedToDefault(string path) => AppStrings.Format("Status.LegacyStatisticsSourceMigratedToDefault", path);
    public static string LegacyStatisticsSourceMigrationFailed(string message) => AppStrings.Format("Status.LegacyStatisticsSourceMigrationFailed", message);
    public static string SettingsUpdated() => AppStrings.Get("Status.SettingsUpdated");
    public static string InvalidAppTagMappings(int invalidLineCount) => AppStrings.Format("Status.InvalidAppTagMappings", invalidLineCount);
    public static string StoredStatisticsCleared() => AppStrings.Get("Status.StoredStatisticsCleared");
    public static string IconCacheCleared() => AppStrings.Get("Status.IconCacheCleared");
    public static string IconCacheClearFailed(string message) => AppStrings.Format("Status.IconCacheClearFailed", message);
    public static string StatisticsSourceSwitched(string path) => AppStrings.Format("Status.StatisticsSourceSwitched", path);
    public static string StatisticsSourceSwitchFailed(string message) => AppStrings.Format("Status.StatisticsSourceSwitchFailed", message);
    public static string StatisticsSourceFallbackToDefault() => AppStrings.Get("Status.StatisticsSourceFallbackToDefault");
    public static string DebugCaptureChanged(bool isEnabled) => AppStrings.Get(isEnabled ? "Status.DebugCaptureEnabled" : "Status.DebugCaptureDisabled");
    public static string DebugEventsCleared() => AppStrings.Get("Status.DebugEventsCleared");
    public static string LanguageChangeRequiresRestart() => AppStrings.Get("Status.LanguageChangeRequiresRestart");
    public static string MonitoringPauseChanged(bool isPaused) => AppStrings.Get(isPaused ? "Status.MonitoringPaused" : "Status.MonitoringResumed");
    public static string MonitoringError(string message) => AppStrings.Format("Status.MonitoringError", message);
    public static string NoForegroundWindow() => AppStrings.Get("Status.NoForegroundWindow");
    public static string UnableToResolveActiveProcess() => AppStrings.Get("Status.UnableToResolveActiveProcess");
    public static string UnableToResolveActiveProcessName() => AppStrings.Get("Status.UnableToResolveActiveProcessName");
    public static string ProcessExcluded(string processName) => AppStrings.Format("Status.ProcessExcluded", processName);
    public static string SelfWindowActive() => AppStrings.Get("Status.SelfWindowActive");
    public static string NoFocusedElement(string processName) => AppStrings.Format("Status.NoFocusedElement", processName);
    public static string PasswordFieldSkipped(string processName) => AppStrings.Format("Status.PasswordFieldSkipped", processName);
    public static string FocusedControlUnreadable(string processName) => AppStrings.Format("Status.FocusedControlUnreadable", processName);
    public static string PasteExcluded(string processName) => AppStrings.Format("Status.PasteExcluded", processName);
    public static string BulkRefreshIgnored(string processName) => AppStrings.Format("Status.BulkRefreshIgnored", processName);
    public static string RecordedSupportedCharacters(int delta, string processName) => AppStrings.Format("Status.RecordedSupportedCharacters", delta, processName);
    public static string WaitingForComposition(string processName) => AppStrings.Format("Status.WaitingForComposition", processName);
    public static string NoPositiveDelta(string processName) => AppStrings.Format("Status.NoPositiveDelta", processName);
    public static string IdleDisplayName() => AppStrings.Get("Target.Idle");
    public static string UnavailableDisplayName() => AppStrings.Get("Target.Unavailable");
}
