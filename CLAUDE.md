# CLAUDE.md

Guidance for working in this repository.

## What this is

**SQL Error Atlas** — a standalone .NET 8 / Avalonia desktop app for Windows that
helps triage the **SQL Server ERRORLOG offline**: paste a log chunk (or type an
error number) and get *what it means*, *what to investigate*, and *what to do*.

The knowledge base is a read-only **DuckDB** file shipped inside the app. The app
never touches SQL Server at runtime — SQL Server is only a **build-time** source
for regenerating that file.

## Layout

| Path | What |
| --- | --- |
| `src/SQLErrorAtlas/` | the Avalonia app (net8.0, MVVM, CommunityToolkit.Mvvm) |
| `tools/DbExport/` | console tool: `issues` DB on SQL Server → `data/sqlerroratlas.duckdb` |
| `tests/SQLErrorAtlas.Tests/` | xUnit — parser, `AtlasDatabase`, and view-model wiring against the real dataset |
| `data/sqlerroratlas.duckdb` | **committed**; the shipped dataset and source of truth for releases |
| `build/installer.iss` | Inno Setup script |
| `.github/workflows/` | `ci.yml` (build+test), `release.yml` (tag `v*` → installer + zip on a Release) |

The solution file is `SQLErrorAtlas.slnx` (XML format — needs the .NET 9+ SDK;
`global.json` pins `9.0.100` with `rollForward: latestMajor`).

## App architecture

- **`Services/AtlasDatabase`** — all queries. `Resolve(int)` is the core call:
  playbook entry → else group-level fallback → else "unknown". One `DuckDBConnection`
  opened `ACCESS_MODE=READ_ONLY`, guarded by a lock (the provider is not thread-safe).
- **`Services/ErrorLogParser`** — pure; regex over pasted text for
  `Error: N, Severity: S, State: T` plus a keyword list for noteworthy un-numbered lines.
- **`Services/DataFileProvisioner`** — copies the embedded `.duckdb` to
  `%LOCALAPPDATA%\SQLErrorAtlas\` on first run / when the bundled copy is newer
  (DuckDB needs a real file path, so it can't be opened from inside the single-file bundle).
- **`Services/EntryExporter` + `MiniMarkdown`** — guidance → Markdown / standalone HTML hand-off docs.
- **ViewModels** — `MainWindowViewModel` owns the five tab VMs and implements
  `IAtlasNavigator` for cross-tab jumps. `EntryDetailViewModel` is the shared right-hand pane.
- **Theme** — Fluent; `SelectedTheme` (Dark default / Light / System) persisted in
  `%LOCALAPPDATA%\SQLErrorAtlas\settings.json`; brushes defined per variant in `App.axaml`.

Avalonia XAML gotchas seen here: model records live in `SQLErrorAtlas.Models`, so
`DataTemplate x:DataType` uses the `m:` namespace, not `vm:`. Use plain `TextBlock`
(not `SelectableTextBlock`) for wrapped body text — `SelectableTextBlock` mis-measures
width. Content `ScrollViewer`s set `HorizontalScrollBarVisibility="Disabled"`.

## Common commands

```bash
# regenerate the dataset from SQL Server (issues DB on WIN10), then commit it
dotnet run --project tools/DbExport
git add data/sqlerroratlas.duckdb && git commit -m "Refresh dataset"

dotnet build  SQLErrorAtlas.slnx -c Release
dotnet test   SQLErrorAtlas.slnx -c Release
dotnet run --project src/SQLErrorAtlas

# standalone build (what the installer/zip ship)
dotnet publish src/SQLErrorAtlas/SQLErrorAtlas.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -o build/publish

# installer (needs Inno Setup 6)
ISCC /DAppVersion=0.1.0 build/installer.iss   # -> build/Output/SQLErrorAtlas-Setup-0.1.0.exe
```

`DbExport` connects with `SQLERRORATLAS_ISSUES_CONN` or the default
`Server=win10;Database=issues;Trusted_Connection=True;TrustServerCertificate=True`.
For ad-hoc queries against that DB from a shell: `sqlcmd -S win10 -d issues -E -C`.

## Dataset shape (DuckDB, snake_case)

`error_message_catalog` (16,708 — every message: severity, band, text, `is_event_logged`, `group_id`),
`error_message_group` (32 — the taxonomy: `what_it_means` / `investigate` / `fix` / `key_tools`),
`error_message_playbook` (180 — message-specific deep entries; `status` is `done` or `stub`),
`issue` / `issue_analysis` / `issue_diagnostic_query` (3 long-form runbooks),
`issue_group_link` (curated Issue↔group cross-links — authored in `DbExport`, not in the source DB),
`meta` (key/value: `generated_utc`, `source_build`, row counts).

To extend guidance, edit the `issues` DB on SQL Server, then re-run `DbExport`.
`DbExport` fails if row counts drift from the expected values — update them there if the change is intentional.

## Attribution

End commit messages with `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
