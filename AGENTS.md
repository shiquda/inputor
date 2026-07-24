# inputor Agent Guide

## Source Of Truth

- This repository builds `inputor`, a Windows 10+ x64 desktop application for
  local Chinese and English input statistics.
- Prefer the active source tree and `inputor.sln` over `README.md` when they
  disagree. The app is WinUI 3 on .NET 8, not Avalonia.
- The single active project is `src/inputor.WinUI/inputor.WinUI.csproj`.
  Its assembly name remains `inputor.App` for compatibility.
- Do not add dependencies or change packaging unless the task requires it.
  The app is self-contained through Windows App SDK and targets
  `net8.0-windows10.0.19041.0`, x64.

## Code Map

- `src/inputor.WinUI/Program.cs`: process entry point and command-line probes.
- `src/inputor.WinUI/App.cs`: service composition, app lifecycle, tray actions,
  export, backup/restore, and settings persistence.
- `src/inputor.WinUI/MainWindow.cs`: `NavigationView` shell for Overview,
  Statistics, Apps, Debug, and Settings.
- `StatisticsPage.cs`, `DebugPage.cs`, and `SettingsPage.cs`: code-behind UI
  surfaces. There is no ViewModel or DI container; keep related UI composition
  near its owning file.
- `Services/StatsStore.cs`: lock-protected statistics state, JSON persistence,
  snapshots, debug events, and the shared `Changed` event.
- `Services/MonitoringService.cs`: STA worker that reads the foreground control
  with FlaUI UIA3 and records valid deltas.
- `Services/CompositionAwareDeltaTracker.cs`, `PasteDetectionService.cs`, and
  `BulkLoadDetectionService.cs`: count-validation path.
- `Services/AppSettingsService.cs`, `CsvExportService.cs`,
  `BackupArchiveService.cs`, `AutoStartService.cs`, and
  `DebugDiskLogService.cs`: local storage and Windows integration.
- `Models/`: persistence and snapshot contracts. Root/UI code uses
  `Inputor.WinUI`; models and services use `Inputor.App.Models` and
  `Inputor.App.Services`.
- `Strings/zh-Hans/Resources.resw` and `Strings/en-US/Resources.resw`: UI
  localization. `AppStrings*.cs` exposes the resources.
- `scripts/publish/` and `packaging/installer/inputor.iss`: release packaging.

## Data And Privacy

- `AppVariant` resolves local paths. Release builds use
  `%LocalAppData%\inputor`, `%Documents%\inputor-exports`, and
  `%Documents%\inputor-backups`; debug builds use the corresponding `-dev`
  directories. Statistics are JSON and daily exports are CSV.
- Normal statistics persistence stores aggregates and application metadata, not
  captured text. Never add raw-text fields to statistics JSON, CSV exports,
  telemetry, or routine diagnostic logs.
- Password controls are excluded. Elevated applications and unsupported UIA
  controls are expected monitoring limitations.
- Debug capture is sensitive: `CompositionAwareDeltaTracker` can retain
  truncated changed segments in debug events, and `DebugDiskLogService` can
  write them to a user-selected disk path only when the user enables the
  raw-text option. Preserve this opt-in boundary, avoid expanding the data
  collected, and review every debug or persistence change for text leakage.
- Do not bypass composition-aware tracking, paste detection, or bulk-load
  filtering when modifying counting behavior.

## Implementation Rules

- Use one top-level class per file and match file and class names. Use
  file-scoped namespaces, nullable annotations, PascalCase public identifiers,
  `_camelCase` private fields, and `camelCase` locals and parameters.
- `ImplicitUsings` is enabled. Order explicit imports as System, third-party,
  then project namespaces; remove unused imports and alias only genuine type
  collisions.
- Prefer focused methods and early returns. Extract reusable business logic to
  `Services/` and data contracts to `Models/`; avoid broad refactors.
- Wrap Windows, file-system, registry, clipboard, and UI Automation boundaries
  defensively. Surface deliberate fallbacks through `StartupDiagnostics` or
  `StatsStore` status rather than silently swallowing errors.
- Preserve `StatsStore` locking with `lock (_syncRoot)`. Do not access WinUI
  controls from the monitoring thread; marshal UI work through `DispatcherQueue`.
  Keep `MonitoringService` STA-compatible and unsubscribe event handlers when
  their owner is disposed or unloaded.
- Check whether an existing setting is actually wired before relying on it;
  `AppSettings.PrivacyMode` is a model property, not proof of UI behavior.

## Build And Run

Run commands from the repository root on Windows with .NET 8 SDK and the
Windows SDK installed.

```powershell
dotnet restore inputor.sln
dotnet build inputor.sln
just dev
```

Use `just dev` for an interactive launch when available. It builds the current
worktree, stops an existing windowed `inputor.App` process, and starts that
worktree's executable. Do not launch a previously installed executable by
mistake. Other recipes are `just build`, `just run-cli <args>`, and
`just publish`; publishing requires Inno Setup 6.

There is no test project, supported `dotnet test` target, or lint/StyleCop
command. Use the CLI probes or a manual smoke test instead of claiming a
single automated test ran.

```powershell
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --count-sample "Hello世界"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-sequence "你|你好|你好世|你好世界"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-paste "Hello" "Hello World" "World"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-bulk 12 "Hello world" "Edit" false
```

For a substantial WinUI-flow change, build, launch with `just dev`, keep the
app open for at least five seconds, verify the tray icon and navigation, type
in a supported external app such as Notepad, check settings/export, and exit
via the tray path. For monitoring or persistence changes, run the build and at
least one relevant CLI probe.

## Change Boundaries

- Keep user changes intact. Do not revert unrelated working-tree files or
  generated diagnostics.
- `inputor.App` remains the process name in compatibility-sensitive checks.
- Treat raw-text handling, backup restore, statistics-source switching, and
  tray shutdown as high-risk paths; verify error handling and lifecycle cleanup.
- Use the primary worktree for integration and human review. Create one branch
  and linked worktree for a nontrivial mutable task, reuse an existing task
  worktree when possible, and do not merge or force-remove worktrees without
  explicit authorization.
