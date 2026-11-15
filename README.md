# DiskAnalyze

A small Windows disk-usage analyzer (WPF, .NET 10). It shows where your disk space goes and finds
duplicate files.

- **Scan** all fixed drives, one drive, or any folder. Shows live progress and can be cancelled.
- **Pie view**: the largest items (5 by default, 3–8 in Settings) plus "Other". Click a folder to drill in; hover for size and date.
- **Bar view**: every item in the current folder, sized to the window.
- **Side panel**: the contents of any slice, largest first ("Show contents" in the context menu, or click "Other").
- **Show in Explorer** from any item.
- Drive **% used**, plus how much used space the scan couldn't read (e.g. folders that need admin rights).
- **OneDrive aware**: cloud folders are scanned, and online-only files count as 0 bytes because they take
  no local space.
- **Duplicates** between two folders (also when one is inside the other): content-hashed, with the total
  space you could reclaim.
- **Light and dark themes**, switched live; "System" follows Windows. Flat, square-cornered UI.
- **Settings** (gear button): theme, whether and what to scan on startup, pie slice count. Saved to
  `%LOCALAPPDATA%\DiskAnalyze\settings.json`.

## Build & run

Requires the .NET 10 SDK (or newer) on Windows.

```
dotnet build DiskAnalyze.sln
dotnet run --project DiskAnalyze/DiskAnalyze.csproj
dotnet test DiskAnalyze.sln
```

## Project layout

| Project | Purpose |
|---|---|
| `DiskAnalyze.Application` | UI-agnostic core: parallel scanner, entities, duplicate finder |
| `DiskAnalyze` | WPF app (MVVM) |
| `DiskAnalyze.Application.Tests` | xUnit tests for the core |

History, known issues, roadmap and design decisions are kept in [DEVELOPMENT.md](DEVELOPMENT.md).

## License

MIT, see [LICENSE](LICENSE).
