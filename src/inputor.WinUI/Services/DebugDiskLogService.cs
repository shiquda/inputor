using Inputor.App.Models;
using Inputor.WinUI;

namespace Inputor.App.Services;

/// <summary>
/// Appends debug event entries to a user-chosen log file on disk.
/// Writing is controlled independently by an enabled flag; it is also
/// gated by the caller on the outer debug-capture switch.
/// Thread-safety: all mutable state is protected by _syncRoot.
/// </summary>
public sealed class DebugDiskLogService : IDisposable
{
    private readonly object _syncRoot = new();
    private string _path = string.Empty;
    private bool _isEnabled;
    private bool _includeRawText;
    private StreamWriter? _writer;
    private bool _disposed;

    public bool IsEnabled
    {
        get { lock (_syncRoot) { return _isEnabled; } }
    }

    public string Path
    {
        get { lock (_syncRoot) { return _path; } }
    }

    public bool IncludeRawText
    {
        get { lock (_syncRoot) { return _includeRawText; } }
    }

    public void SetEnabled(bool isEnabled)
    {
        lock (_syncRoot)
        {
            if (_isEnabled == isEnabled)
            {
                return;
            }

            _isEnabled = isEnabled;

            if (!isEnabled)
            {
                CloseWriterLocked();
            }
            else if (!string.IsNullOrWhiteSpace(_path))
            {
                OpenWriterLocked();
            }
        }
    }

    public void SetPath(string path)
    {
        lock (_syncRoot)
        {
            if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CloseWriterLocked();
            _path = path;

            if (_isEnabled && !string.IsNullOrWhiteSpace(_path))
            {
                OpenWriterLocked();
            }
        }
    }

    public void SetIncludeRawText(bool includeRawText)
    {
        lock (_syncRoot)
        {
            _includeRawText = includeRawText;
        }
    }

    /// <summary>
    /// Appends a single debug event entry to the log file.
    /// Must be called OUTSIDE any StatsStore lock to avoid I/O blocking the lock.
    /// </summary>
    public void Write(DebugEventEntry entry)
    {
        StreamWriter? writer;
        bool includeRawText;

        lock (_syncRoot)
        {
            if (!_isEnabled || _writer is null)
            {
                return;
            }

            writer = _writer;
            includeRawText = _includeRawText;
        }

        try
        {
            var line = FormatEntry(entry, includeRawText);
            lock (_syncRoot)
            {
                // Re-check after acquiring lock; state may have changed.
                if (!_isEnabled || _writer is null || !ReferenceEquals(_writer, writer))
                {
                    return;
                }

                _writer.WriteLine(line);
                _writer.Flush();
            }
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"DebugDiskLogService.Write failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseWriterLocked();
        }
    }

    private void OpenWriterLocked()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(_path, append: true, System.Text.Encoding.UTF8)
            {
                AutoFlush = false
            };
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"DebugDiskLogService.OpenWriter failed for '{_path}': {exception.Message}");
            _writer = null;
        }
    }

    private void CloseWriterLocked()
    {
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"DebugDiskLogService.CloseWriter failed: {exception.Message}");
        }
        finally
        {
            _writer = null;
        }
    }

    private static string FormatEntry(DebugEventEntry entry, bool includeRawText)
    {
        // Format: [timestamp] app | control | delta | CJK EN OT | status | flags
        var flags = string.Join(" ",
            $"Paste:{(entry.IsPaste ? "Y" : "N")}",
            $"Bulk:{(entry.IsBulkContentLoad ? "Y" : "N")}",
            $"Pending:{(entry.IsPendingComposition ? "Y" : "N")}",
            $"IME:{(entry.IsNativeImeInputMode ? "Y" : "N")}",
            $"Supported:{(entry.IsCurrentTargetSupported ? "Y" : "N")}");

        var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.AppName} | {entry.ControlTypeName} | {entry.Delta:+#;-#;0} | " +
                   $"{entry.InsertedChineseCharacterCount}CJK {entry.InsertedEnglishLetterCount}EN {entry.InsertedOtherSupportedCharacterCount}OT | " +
                   $"{entry.StatusMessage} | {flags}";

        if (includeRawText && entry.TextComparison is not null)
        {
            var before = entry.TextComparison.PreviousText ?? string.Empty;
            var after = entry.TextComparison.CurrentText ?? string.Empty;
            line += $" | Before:[{before}] After:[{after}]";
        }

        return line;
    }
}
