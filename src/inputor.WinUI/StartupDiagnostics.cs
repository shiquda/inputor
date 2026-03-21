using System.Text;

namespace Inputor.WinUI;

internal static class StartupDiagnostics
{
    private static readonly object SyncRoot = new();

    public static void Log(string message)
    {
        lock (SyncRoot)
        {
            var logPath = GetLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
    }

    public static string GetLogPath()
    {
        return Path.Combine(AppVariant.GetDataDirectory(), "startup.log");
    }
}
