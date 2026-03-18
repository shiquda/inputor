# AGENTS.md - Development Guide for AI Coding Agents

This guide provides essential information for AI agents working on the `inputor` codebase.

## Project Overview

`inputor` is a Windows-first MVP for privacy-safe Chinese and English input statistics tracking.

**Tech Stack:**
- .NET 8.0 (Windows-specific)
- Avalonia 11.3.12 (cross-platform UI framework)
- FlaUI 5.0.0 (Windows UI Automation)
- CommunityToolkit.Mvvm 8.4.0
- CsvHelper 33.1.0

**Project Structure:**
- `src/inputor.App/` - Main Avalonia desktop application
- `src/inputor.Spike/` - Original console spike (historical reference)

---

## Build, Test, and Run Commands

### Build
```bash
dotnet build inputor.sln
```

### Run the Application
```bash
dotnet run --project src/inputor.App/inputor.App.csproj
```

### Clean Build
```bash
dotnet clean inputor.sln
dotnet build inputor.sln
```

### Restore Dependencies
```bash
dotnet restore inputor.sln
```

### CLI Testing Utilities

The app includes built-in CLI commands for testing specific functionality:

**Test character counting:**
```bash
dotnet run --project src/inputor.App/inputor.App.csproj -- --count-sample "Hello世界"
```

**Test composition-aware delta tracking:**
```bash
dotnet run --project src/inputor.App/inputor.App.csproj -- --simulate-sequence "你|你好|你好世|你好世界"
```

**Test paste detection:**
```bash
dotnet run --project src/inputor.App/inputor.App.csproj -- --simulate-paste "Hello" "Hello World" "World"
```

### Verification Checklist

Before considering work complete:
1. Run `dotnet build inputor.sln` - must succeed with no errors
2. Launch `src/inputor.App` and verify it runs for at least 5 seconds without crashing
3. Check that tray icon appears and dashboard opens

---

## Code Style Guidelines

### Namespace and File Organization

- **File-scoped namespaces** (C# 10+): Use `namespace Inputor.App.Services;` at the top
- **One class per file**: File name matches the class name exactly
- **Namespace structure**: `Inputor.App.<Folder>` (e.g., `Inputor.App.Models`, `Inputor.App.Services`)

### Naming Conventions

- **Classes**: PascalCase, descriptive nouns (e.g., `MonitoringService`, `AppSettings`)
- **Interfaces**: PascalCase with `I` prefix (e.g., `IDisposable`)
- **Methods**: PascalCase, verb phrases (e.g., `RecordDelta`, `TryReadText`)
- **Properties**: PascalCase (e.g., `CurrentAppName`, `IsPaused`)
- **Private fields**: camelCase with `_` prefix (e.g., `_statsStore`, `_isPaused`)
- **Local variables**: camelCase (e.g., `processName`, `focusedElement`)
- **Constants**: PascalCase (no SCREAMING_CASE)

### Type System

- **Nullable reference types enabled**: `<Nullable>enable</Nullable>` in .csproj
- Use `?` for nullable types explicitly (e.g., `string?`, `AutomationElement?`)
- Use `is null` / `is not null` for null checks (modern C# pattern)
- Prefer `sealed` classes when inheritance is not needed
- Use `static` for utility classes (e.g., `CharacterCountService`)

### Imports and Usings

- **Implicit usings enabled**: `<ImplicitUsings>enable</ImplicitUsings>` in .csproj
- Common namespaces (System, System.Collections.Generic, System.Linq, etc.) are auto-imported
- Explicit usings for:
  - Third-party libraries (Avalonia, FlaUI, CsvHelper)
  - Project namespaces (Inputor.App.Models, Inputor.App.Services)
- Order: System namespaces first, then third-party, then project namespaces
- No unused usings

### Error Handling

- **Try-catch for external operations**: File I/O, process access, UI Automation calls
- **Return null or empty for failures**: Prefer `string.Empty` over throwing in utility methods
- **Catch specific exceptions when possible**, but generic `catch` is acceptable for monitoring loops
- **Never swallow exceptions silently**: Log or set status messages
- Example pattern from `MonitoringService`:
  ```csharp
  try
  {
      return Process.GetProcessById((int)processId).ProcessName;
  }
  catch
  {
      return string.Empty;
  }
  ```

### Patterns and Idioms

**Locking for thread safety:**
```csharp
private readonly object _syncRoot = new();

public void RecordDelta(string appName, int delta)
{
    lock (_syncRoot)
    {
        // Critical section
    }
}
```

**Disposal pattern:**
```csharp
public sealed class MonitoringService : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    
    public void Dispose()
    {
        _cts.Cancel();
        _workerThread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
```

**String comparisons:**
- Use `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons
- Use `StringComparison.Ordinal` for case-sensitive comparisons
- Example: `string.Equals(part, processName, StringComparison.OrdinalIgnoreCase)`

**Null-conditional and null-coalescing:**
- Use `?.` for safe navigation: `element.Properties.Name.ValueOrDefault ?? string.Empty`
- Use `??` for default values
- Use `is null` / `is not null` for checks

---

## Architecture and Design Principles

### Service Layer

All business logic lives in `Services/`:
- `MonitoringService` - Core UIA polling and text snapshot tracking
- `StatsStore` - Thread-safe statistics storage and persistence
- `CharacterCountService` - Chinese/English character detection (static utility)
- `CompositionAwareDeltaTracker` - IME composition handling
- `PasteDetectionService` - Clipboard paste detection
- `CsvExportService` - Export to CSV
- `AutoStartService` - Windows startup integration
- `AppSettingsService` - Settings persistence

### Models

Simple POCOs in `Models/`:
- `AppSettings` - User preferences
- `AppStat` - Per-app statistics
- `DashboardSnapshot` - UI state snapshot

### Views

Avalonia UI in `Views/`:
- `MainWindow` - Dashboard and tray icon
- `SettingsWindow` - Settings dialog

### Privacy and Security

**CRITICAL**: This app is privacy-focused. Never:
- Store raw text content (only counts and deltas)
- Log sensitive information
- Transmit data over network
- Access password fields (check `IsPassword` property)

### Threading Model

- **Main thread**: Avalonia UI (STA)
- **Background thread**: `MonitoringService.WorkerLoop()` (STA for COM interop with UIA)
- **Synchronization**: Use `lock (_syncRoot)` for shared state in `StatsStore`
- **Cancellation**: Use `CancellationTokenSource` for graceful shutdown

---

## Common Tasks

### Adding a New Service

1. Create `Services/YourService.cs`
2. Use namespace `Inputor.App.Services;`
3. Make it `sealed` unless inheritance is needed
4. Implement `IDisposable` if it holds resources
5. Register in `App.cs` or `Program.cs` if needed

### Modifying Character Counting Logic

Edit `CharacterCountService.cs`:
- `IsChineseRune()` - Unicode ranges for Chinese characters
- `IsEnglishLetter()` - A-Z, a-z detection
- `CountSupportedCharacters()` - Combined count

Test with CLI: `dotnet run --project src/inputor.App/inputor.App.csproj -- --count-sample "测试text"`

### Adding Settings

1. Add property to `Models/AppSettings.cs`
2. Update `AppSettingsService.cs` for persistence
3. Add UI control in `Views/SettingsWindow.cs`
4. Use the setting in relevant service

---

## Testing and Verification

### Manual Testing Checklist

- [ ] Build succeeds: `dotnet build inputor.sln`
- [ ] App launches without errors
- [ ] Tray icon appears in system tray
- [ ] Dashboard opens and shows "Monitoring has not started yet" or current status
- [ ] Type in external app (e.g., Notepad) and verify counts increment
- [ ] Settings dialog opens and saves preferences
- [ ] CSV export creates file in Documents/inputor-exports
- [ ] App exits cleanly via tray menu

### Known Limitations (Do Not "Fix")

These are documented MVP constraints, not bugs:
- Elevated windows cannot be monitored (Windows security)
- Password fields are intentionally skipped
- Some custom editors may not expose UIA text patterns
- Paste detection is heuristic-based (may have false positives/negatives)

---

## Important Notes for AI Agents

1. **Windows-only**: This is a Windows-specific app. Do not suggest cross-platform alternatives for UIA or P/Invoke.

2. **No unit tests yet**: The project uses manual smoke testing. Do not add test projects unless explicitly requested.

3. **Minimal dependencies**: Avoid adding new NuGet packages unless absolutely necessary.

4. **Privacy first**: Never log or store raw text content. Only counts and metadata.

5. **Thread safety**: `StatsStore` is accessed from both UI and background threads. Always use `lock (_syncRoot)`.

6. **Avalonia patterns**: Follow Avalonia conventions for UI code (not WPF or WinForms).

7. **FlaUI patterns**: Use `element.Patterns.Text` and `element.Patterns.Value` for text extraction.

8. **IME awareness**: The `CompositionAwareDeltaTracker` handles Chinese IME composition. Do not bypass it.

---

## Troubleshooting

**Build fails with "SDK not found":**
- Ensure .NET 8.0 SDK is installed: `dotnet --version`

**App crashes on startup:**
- Check Windows version (requires Windows 10+)
- Verify FlaUI.UIA3 can initialize (requires UIAutomationCore.dll)

**Monitoring not working:**
- Run as administrator for elevated windows (optional)
- Check if target app exposes UIA text patterns
- Verify app is not in excluded list (Settings)

**Counts seem wrong:**
- Test with CLI: `--count-sample` and `--simulate-sequence`
- Check `CharacterCountService` Unicode ranges
- Verify paste detection is not excluding legitimate typing

---

## References

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [FlaUI Documentation](https://github.com/FlaUI/FlaUI)
- [.NET 8.0 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Windows UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32)
