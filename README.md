# inputor

`inputor` is a Windows-first MVP for privacy-safe Chinese and English input statistics.

## Projects

- `src/inputor.App`: the Avalonia desktop MVP with tray menu, dashboard, settings, CSV export, and UIA-based monitoring.
- `src/inputor.Spike`: the original console spike kept for early feasibility history.

## MVP scope implemented here

- Foreground focused-control monitoring through Windows UI Automation via FlaUI UIA3.
- Per-app Chinese and English character counting based on positive visible-text deltas.
- Tray icon with dashboard, settings, export, and exit actions.
- Dashboard with today/all-time totals, per-app stats, and simple top-app bars.
- Settings for start with Windows, admin reminder visibility, privacy mode, and excluded apps.
- Daily reset for today counts and JSON persistence for cumulative stats.
- CSV export to the user's Documents folder under `inputor-exports`.

## Important MVP limits

- Counting depends on the focused control exposing readable UIA text or value patterns.
- Elevated windows, password fields, and unsupported custom editors may not be counted.
- The app stores counts, process names, and date buckets; raw text is only used in memory for snapshot diffing.

## Verification

- `dotnet build inputor.sln`
- launch smoke test of `src/inputor.App` for 5 seconds without early exit
