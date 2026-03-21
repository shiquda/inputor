using Microsoft.Win32;

namespace Inputor.App.Services;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _appName;

    public AutoStartService(string appName)
    {
        _appName = appName;
    }

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        if (enabled)
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                key.SetValue(_appName, processPath);
            }
        }
        else
        {
            key.DeleteValue(_appName, false);
        }
    }
}
