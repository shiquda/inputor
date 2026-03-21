# AGENTS.md - inputor agent guide
This file is for coding agents working in this repository.
Prefer the source tree over older docs when they disagree; `README.md` might be stale in a few places.

## Project snapshot
- Product: Windows-first desktop app for privacy-safe Chinese and English input statistics.
- Current UI stack: WinUI 3 on .NET 8, not Avalonia.
- Monitoring stack: FlaUI UIA3 over Windows UI Automation.
- Persistence: local JSON files under `%LocalAppData%\inputor`.
- Export: CSV files under `%Documents%\inputor-exports`.
- Privacy rule: never persist raw captured text; only counts, metadata, and derived debug fields.

## Repository layout
- `inputor.sln` - single-solution entry point.
- `src/inputor.WinUI/` - only active app project.
- `src/inputor.WinUI/Program.cs` - process entry point and CLI test harness.
- `src/inputor.WinUI/App.cs` - app bootstrap, service wiring, lifecycle, tray coordination.
- `src/inputor.WinUI/MainWindow.cs` - main dashboard shell with navigation and page switching.
- `src/inputor.WinUI/StatisticsPage.cs` - trend, heatmap, and distribution UI.
- `src/inputor.WinUI/DebugPage.cs` - debug capture UI and event inspection.
- `src/inputor.WinUI/SettingsPage.cs` - settings, export, and data-management UI.
- `src/inputor.WinUI/NotifyIconService.cs` - tray icon integration through Windows Forms.
- `src/inputor.WinUI/TrayMenuWindow.cs` - custom tray popup window.
- `src/inputor.WinUI/Services/` - business logic and OS integration.
- `src/inputor.WinUI/Models/` - POCO-style state and snapshot models.

## Architecture notes
- The project name is `inputor.WinUI`, but the assembly name stays `inputor.App` for compatibility.
- Namespaces are split intentionally: UI/root files use `Inputor.WinUI`, models use `Inputor.App.Models`, and services use `Inputor.App.Services`.
- There is no DI container. `App` constructs and owns the service graph directly.
- There is no ViewModel layer today. UI is built in C# code-behind, not XAML-heavy MVVM.
- `MainWindow` hosts a `NavigationView` with Overview, Statistics, Apps, Debug, and Settings surfaces.
- `StatsStore` is the shared state hub. UI and tray subscribe to its `Changed` event.
- `MonitoringService` runs on its own STA worker thread and polls the foreground control through UIA.
- Character counting flows through `CompositionAwareDeltaTracker`, paste detection, and bulk-load filtering before counts are recorded.
- `StartupDiagnostics` writes startup and crash clues to `%LocalAppData%\inputor\startup.log`.

## Build, run, and verification commands
Use these from the repository root.

### Restore
```bash
dotnet restore inputor.sln
```

### Build
```bash
dotnet build inputor.sln
```

### Clean build
```bash
dotnet clean inputor.sln
dotnet build inputor.sln
```

### Run the desktop app
```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj
```

## Test and lint reality
- There is no dedicated test project in this repository today.
- There is no lint command or StyleCop configuration in this repository today.
- There is no supported `dotnet test` target, so single-test execution is not available.
- If a task asks for "run a single test", state that no automated test suite exists yet and use the CLI probes below or a manual smoke test instead.

## Built-in CLI probes
- Count supported characters:
```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --count-sample "Hello世界"
```
- Simulate IME composition-aware deltas:
```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-sequence "你|你好|你好世|你好世界"
```
- Simulate paste detection:
```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-paste "Hello" "Hello World" "World"
```
- Simulate bulk-load filtering:
```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-bulk 12 "Hello world" "Edit" false
```

## Manual smoke test checklist
Run this before calling substantial app changes done.
1. `dotnet build inputor.sln` succeeds.
2. Launch the app and keep it open for at least 5 seconds.
3. Confirm the tray icon appears.
4. Confirm the main window opens and navigation works.
5. Type in a supported external app such as Notepad and verify counts change.
6. Open Settings and verify save/export actions still work.
7. Exit through the tray/menu path and confirm clean shutdown.

## Code style expectations

### File organization
- Keep one top-level class per file.
- Match file names to class names exactly.
- Use file-scoped namespaces.
- Put reusable business logic in `Services/` and data contracts in `Models/`.
- Keep WinUI page and window composition close to the owning UI file unless a helper is clearly reusable.

### Naming
- Classes, methods, properties, enums: PascalCase.
- Private fields: `_camelCase`.
- Locals and parameters: `camelCase`.
- Interfaces: `I` prefix.
- Constants: PascalCase, not screaming snake case.

### Imports and usings
- `ImplicitUsings` is enabled; do not add redundant basic framework usings.
- Order usings as: System, third-party, project namespaces.
- Remove unused usings.
- Use aliases only when type collisions are real, such as WinForms tray types.

### Types and nullability
- Nullable reference types are enabled; express optional values explicitly with `?`.
- Prefer `is null` and `is not null` checks.
- Prefer concrete return types over `object`.
- Do not suppress type issues with `as any`, `#pragma`, or ignore-style workarounds.
- Prefer `sealed` for classes that are not designed for inheritance.

### Formatting and structure
- Follow the existing C# formatting style in nearby files.
- Prefer early returns for guard clauses.
- Keep methods focused; extract helpers when a block becomes hard to scan.
- Avoid comment noise; add short comments only when behavior is non-obvious.

### Error handling
- Wrap OS, file system, registry, clipboard, and UI Automation calls in defensive error handling.
- Prefer returning a safe fallback or status update for expected environment failures.
- Do not swallow failures silently; log through `StartupDiagnostics` or update `StatsStore` status when appropriate.
- Generic `catch` is acceptable in monitoring loops when the fallback behavior is deliberate and visible.

### Threading and state
- `StatsStore` is shared mutable state; preserve its locking discipline with `lock (_syncRoot)`.
- Do not update WinUI controls from the monitoring thread; marshal back with `DispatcherQueue`.
- Keep the monitoring worker STA-compatible; do not remove apartment-state setup from `MonitoringService`.
- Be careful with event subscriptions; unsubscribe on window/service disposal paths.

## Domain-specific guardrails
- Never persist raw text content to disk, logs, exports, telemetry, or debug artifacts.
- Password fields must stay excluded.
- Elevated windows and unsupported controls are expected limitations, not automatic bugs.
- Do not bypass `CompositionAwareDeltaTracker`, paste detection, or bulk-load filtering when changing counting logic.
- `AppSettings.PrivacyMode` exists in the model; check real usage before assuming it is wired to UI behavior.
- Because the assembly name is `inputor.App`, process-name checks may still use `inputor.App` instead of `inputor.WinUI`.

## Working rules for agents
- Prefer small, focused edits over broad refactors.
- Avoid new NuGet dependencies unless the task clearly requires them.
- If you touch monitoring or persistence logic, run at least one CLI probe plus the build.
- If you touch WinUI interaction flow, do the manual smoke test as well.
- Do not "fix" documented MVP limitations unless the user explicitly asks.

## Repository-specific rule files
- Existing repository guide: `AGENTS.md` at the repo root.
- No `.cursorrules` file was found.
- No `.cursor/rules/` directory was found.
- No `.github/copilot-instructions.md` file was found.

## External references
- WinUI 3 / Windows App SDK docs: `Microsoft.WindowsAppSDK` APIs used by `App.cs` and `MainWindow.cs`.
- FlaUI docs: useful for `MonitoringService` text and control access patterns.
- Windows UI Automation docs: useful when changing control inspection behavior.
