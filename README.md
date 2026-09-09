# SQL Error Atlas

**Offline triage for the SQL Server ERRORLOG.** You are on site, there is no
internet, and the ERRORLOG is full of red. Paste it in — SQL Error Atlas tells you
what each message *means*, what to *investigate*, and what to *do*.

A single self-contained Windows app. No install of .NET, no server, no network,
no SQL Server connection. The whole knowledge base travels inside the executable.

> "SQL Server" is a trademark of Microsoft Corporation. SQL Error Atlas is an
> independent tool and is not affiliated with or endorsed by Microsoft.

## Install

Download the latest **`SQLErrorAtlas-Setup-<version>.exe`** from the
[Releases](https://github.com/ronaldgithub/SQLErrorAtlas/releases) page and run it
(no admin rights required — it installs per-user if you are not elevated). A
portable `SQLErrorAtlas-<version>-win-x64.zip` is attached to the same release if
you would rather just unzip and run `SQLErrorAtlas.exe`.

## Using it

- **Analyze log** — paste a chunk of the ERRORLOG. It pulls out every
  `Error: N, Severity: S, State: T`, ranks them by severity, and shows guidance
  for each. Noteworthy un-numbered lines (I/O stalls, stack dumps, memory
  warnings) are flagged separately. Export the whole analysis to a self-contained
  HTML file to hand off.
- **Lookup** — type an error number, get its entry.
- **Browse groups** — the 32-group taxonomy, ordered Emergency → Info, each with
  group-level guidance, its deep playbook entries, and every message in it.
- **Search** — free text across messages, playbook, groups and runbooks; a number
  jumps straight to that message.
- **Runbooks** — long-form incident write-ups (e.g. the corruption / DBCC CHECKDB
  response) with copy-ready diagnostic queries.

Every error number resolves to either a **message-specific playbook entry** or,
if none exists yet, the **group-level guidance** for its area — so there is always
an answer. Dark mode by default; toggle in the toolbar. Everything is local:
no telemetry, no network calls.

## What's in the box

| | |
| --- | --- |
| Catalogued messages | 16,708 (`sys.messages`, with severity band and event-logged flag) |
| Groups | 32-area taxonomy with what-it-means / investigate / fix / key-tools |
| Playbook entries | 180 message-specific deep entries (the fatal band, mostly complete) |
| Incident runbooks | 3 long-form, with diagnostic SQL |

The dataset ships as a read-only [DuckDB](https://duckdb.org/) file embedded in
the app. See the About box for the exact generation date and source build.

## Build from source

Requires the .NET 9 SDK or newer (the app targets **.NET 8**; the newer SDK is
only needed to read the `.slnx` solution file).

```bash
git clone https://github.com/ronaldgithub/SQLErrorAtlas
cd SQLErrorAtlas

dotnet test SQLErrorAtlas.slnx -c Release
dotnet run  --project src/SQLErrorAtlas

# standalone single-file build
dotnet publish src/SQLErrorAtlas/SQLErrorAtlas.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -o build/publish
```

Tagging a commit `vX.Y.Z` triggers the release workflow, which publishes the
self-contained build, wraps it in an Inno Setup installer, and attaches the
installer plus a portable zip to a GitHub Release.

### Refreshing the dataset

The knowledge base lives in an `issues` database on SQL Server. `tools/DbExport`
reads it and rewrites `data/sqlerroratlas.duckdb`, which is committed to the repo
and embedded at build time.

```bash
dotnet run --project tools/DbExport      # uses SQLERRORATLAS_ISSUES_CONN or a WIN10 default
```

## License

[MIT](LICENSE) © 2026 Ronald de Groot
