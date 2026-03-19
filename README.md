# inputor

`inputor` is a Windows-first desktop app for privacy-safe Chinese and English input statistics.

## Current app

- `src/inputor.WinUI` is the active .NET 8 WinUI 3 application.
- The app monitors the focused control through Windows UI Automation via FlaUI UIA3.
- It tracks supported Chinese and English character deltas per app.
- It stores only counts, app names, and date-bucketed statistics locally.
- It exports CSV snapshots to the user's Documents folder under `inputor-exports`.

## Architecture at a glance

- `src/inputor.WinUI/Program.cs` is the process entry point and CLI probe harness.
- `src/inputor.WinUI/App.cs` wires services, main window lifecycle, tray behavior, and startup flow.
- `src/inputor.WinUI/MainWindow.cs` hosts the main dashboard and navigation shell.
- `src/inputor.WinUI/Services/` contains monitoring, persistence, export, autostart, and counting logic.
- `src/inputor.WinUI/Models/` contains settings and dashboard snapshot models.

## What the app currently includes

- Foreground focused-control monitoring through Windows UI Automation.
- Per-app Chinese and English character counting based on positive visible-text deltas.
- Tray icon with dashboard, settings, export, pause, and exit actions.
- Dashboard surfaces for overview, statistics, apps, debug capture, and settings.
- JSON persistence under `%LocalAppData%\inputor`.
- CSV export to `%Documents%\inputor-exports`.

## Important limits

- Counting depends on the focused control exposing readable UIA text or value patterns.
- Elevated windows, password fields, and unsupported custom editors may not be counted.
- Raw text is used only transiently in memory for snapshot diffing and debug derivation; it is not persisted.

## Build and run

```bash
dotnet restore inputor.sln
dotnet build inputor.sln
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj
```

## CLI probes

```bash
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --count-sample "Hello世界"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-sequence "你|你好|你好世|你好世界"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-paste "Hello" "Hello World" "World"
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj -- --simulate-bulk 12 "Hello world" "Edit" false
```

## Verification

- `dotnet build inputor.sln`
- Launch the app and keep it open for at least 5 seconds.
- Confirm the tray icon appears and the main window opens.
- Type in a supported external app such as Notepad and verify counts change.
