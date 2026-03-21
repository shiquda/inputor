# inputor

[中文](README.md)

---

inputor is a Windows tool that helps you understand your daily typing behavior. Run it in the background and it silently tracks how many characters you type in each app. Use inputor's statistics to observe trends and distributions across multiple dimensions.

inputor **never records raw text** — only character counts and metadata — keeping your input private.

![Statistics screenshot](imgs/stats-page.png)

## Features

- **Per-app statistics** — track input volume separately for each application
- **Trends & heatmap** — visualize how your input volume changes over time
- **Chinese & English aware** — heuristic-based counting compatible with Microsoft Pinyin and other Chinese IMEs
- **System tray integration** — minimizes to tray, stays out of your way
- **Privacy-first** — all data stays on your machine at `%LocalAppData%\inputor`, no network access

## Installation

### Option 1: Download a pre-built release

> Recommended for users who just want to try inputor.

Download the latest release from the [Releases](https://github.com/shiquda/inputor/releases) page:

| File | Description |
|------|-------------|
| `inputor-x.x.x-setup-win-x64.exe` | Installer (recommended) |
| `inputor-x.x.x-portable-win-x64.zip` | Portable — unzip and run |

**Requirements**: Windows 10 1809 or later, x64

> Both packages automatically deploy the Windows App Runtime if not already installed.

### Option 2: Build from source

> For developers or users who need to customize the app.

**Prerequisites**: .NET 8 SDK, Windows 10 SDK

```bash
git clone https://github.com/shiquda/inputor.git
cd inputor
dotnet restore inputor.sln
dotnet build inputor.sln
dotnet run --project src/inputor.WinUI/inputor.WinUI.csproj
```

To build a release package (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```bash
just publish
```

Build output is placed under `artifacts/publish/`.

## User Guide

See the [User Guide](./docs/user-guide.md).

## Privacy Commitment

Zero raw-text persistence is a core design constraint:

- Raw input text is used only transiently in memory for snapshot diffing
- Disk, logs, and exports contain **only character counts, app names, and date-bucketed statistics**
- Password fields are automatically excluded
- **No telemetry, no network access**

## Known Limitations

- Counting relies on the focused control exposing text through Windows UI Automation; some custom editors may not be supported
- Elevated windows (UAC prompts, admin-privilege apps) cannot be monitored

## Contributing

This project is under active development — all kinds of contributions are welcome! Feedback of any kind is greatly appreciated.

Ways to contribute include, but are not limited to:

- Starring the repository
- Sharing it with others who might find it useful
- Filing an Issue
- Submitting a Pull Request

If you'd like to contribute code, please read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

---

### Why did I build inputor?

I was just curious how many characters I type into AI apps every day.
