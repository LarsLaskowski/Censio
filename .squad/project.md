# Project

What the squad needs to know about this project that is not stack-specific. Read by the Lead, the Devil's
Advocate, Security, the Tester and the Reviewer. Not template-managed: `adopt-template` creates it once
and never overwrites it. A product PR updates it when the change makes an entry untrue
(`.squad/routing.md`, *Scope of a product PR*).

Every entry points: it names files, types or endpoints and links the document that says more. Behavior, values,
limits and sequences are written once, in the area documents (`docs/areas/README.md`), and the reasons in the
decision records; a copy here goes stale with the next change. `config-check.py` warns when this file grows past
`PROJECT_MAX_WORDS` words (`.squad/tools/squad_settings.py`).

## Security areas

A change that touches one of these lists it under *Security areas touched* in its plan, so Security reviews the
plan (`.squad/routing.md`, *Tiers*, which also says when a change is tier `security`). One line per area: what is
protected, the concrete types, files or endpoints, and a link to the area document that states its rules
(`- Session tokens: issued in src/Auth/, checked by every /api route — [Sessions](../docs/areas/sessions.md)`); no
limits, patterns or sequences.

- Ingest token and Companion authentication: the WebSocket ingest of `src/Censio.Server` (not built yet, #28) — [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md); its area document comes with #28.
- Network exposure: the server's listener and its routes reachable without login (`/`, `/health`, the Blazor hub, static assets) in `src/Censio.Server/Program.cs` and `src/Censio.Server/Dockerfile` — [server](../docs/areas/server.md).
- Data-protection key ring: `DataProtectionSetup` under the data directory — [server](../docs/areas/server.md), *Data directory and key ring*.
- Local pipe connection: `NamedPipeConnector` and `PipeEndpoint` open the game's pipe read-only at identification level — [pipe-connection](../docs/areas/pipe-connection.md), *Opening*.
- Parsing of external input: all of `src/Censio.Pipe` and `src/Censio.GameData` (pipe stream, recordings and their anonymiser, game archives and what is read from them, the catalog cache file), the settings file (`CompanionSettings`), Ubisoft Connect's install records (`RegistryUbisoftInstallRecords`, `GameInstallationSearch`), later WebSocket messages — [pipe](../docs/areas/pipe.md), [pipe-recording](../docs/areas/pipe-recording.md), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-installation](../docs/areas/game-installation.md), [companion](../docs/areas/companion.md) (*Settings file*).
- File paths the Companion writes, prunes or opens: `CompanionFolders` with recordings (`PipeRecordingFiles`), logs (`LogFileLoggerProvider`, `LogFileNames`), settings (`CompanionSettings`) and cache (`GameCatalogCacheStore`), Explorer (`FolderOpener`), the replay file, the anonymiser's output and temporary file (`AnonymiserCommand`), the installation and its archives, read-only (`GameInstallation`, `WindowsFileVersionReader`, `RdaArchive`, `RdaInstallationSurvey`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [companion](../docs/areas/companion.md) (*Recording*, *Folders*, *Settings file*), [companion-log](../docs/areas/companion-log.md), [pipe-recording](../docs/areas/pipe-recording.md) (*Anonymising*), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-installation](../docs/areas/game-installation.md).
- Windows autostart: `CompanionAutostart` over `RegistryStartupEntryStore`, the per-user Run value — [companion](../docs/areas/companion.md), *Start with Windows*.
- Logging and showing external data: the Companion's log (`LogLine`, `LogFileLoggerProvider`, `CompanionLog`, `GameCatalogLoader`, `GameCatalogReporter`), the diagnostics window's game names and icons (`DiagnosticsWindow`, `GameIconImageConverter`; WPF decodes PNGs from `PngWriter`, `GameCatalogCacheFile`), the server's request logging — [companion-log](../docs/areas/companion-log.md), [companion-diagnostics](../docs/areas/companion-diagnostics.md), [pipe](../docs/areas/pipe.md), [server](../docs/areas/server.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md).
- Release publishing: `.github/workflows/release.yml` and `notify-website.yml`, their secrets in the environment `release`, Docker Hub `latest` — [record 0012](../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md), `docs/CONTRIBUTING.md` (*Versioning and releases*), [server](../docs/areas/server.md) (*Docker image*).
- Outbound calls: none to a server; the first one is a new security area. A UNC installation folder from the settings file is probed and read over SMB (`GameInstallation`, `GameArchiveStateReader`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [game-installation](../docs/areas/game-installation.md), *Folders from outside*, [game-catalog](../docs/areas/game-catalog.md), *Limits and errors*.

## Guarantees

Deliberate behavior that must not change without the Product Manager. One line each: the guarantee in a few words,
a link to its decision record where it was a real choice, and a link to the area document that states it.

- Plain HTTP with TLS at a reverse proxy; the start page and `/health` need no login, `/health` reveals only its status — [record 0006](../docs/decisions/0006-server-plain-http-and-open-health.md), [server](../docs/areas/server.md).
- The server stores data from a Companion only with the shared ingest token, which only an explicit setting switches off — [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md); its area document comes with #28.
- History in SQLite in the data directory: full resolution for 7 days, then hourly averages kept without limit, deletable per profile — [record 0021](../docs/decisions/0021-sqlite-history-with-retention.md); its area document comes with #29 and #35.
- Unsent ticks wait in a bounded local buffer and are delivered in order; beyond the bound the oldest are dropped and shown as a gap, never as zeros — [record 0031](../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md); its area document comes with #41.

## Integration surface

What the Reviewer checks when the diff introduces or changes a thing of this kind: every place that must
change with it.

**A new or changed configuration option** touches:
- the key and its validation: `Censio:DataDirectory` in `DataProtectionSetup`; the Companion's switches in `CompanionCommandLine`; the option `gameFolder` in `CompanionSettings`
- registration and binding: `src/Censio.Server/Program.cs`, `src/Censio.Companion/App.xaml.cs`
- the defaults: `src/Censio.Server/appsettings.json` and the `ENV` lines of `src/Censio.Server/Dockerfile`
- the configuration table in `README.md` (key, environment variable, default; English, then German) and the user guide
- the area document that states the option: `docs/areas/server.md` or `docs/areas/companion.md` (*Command line*)
- the tests that pin it: `DataProtectionSetupTests`, `ProgramTests`, `CompanionCommandLineTests`, `CompanionSettingsTests`

**A new or changed service / module** touches:
- its interface where platform-neutral logic meets Windows: `IPipeConnector`, `IStartupEntryStore`, `IDiagnosticsWindow`, `IUbisoftInstallRecords`, `IFileVersionReader` in `Censio.Companion.Core`
- registration and lifetime: `Program.cs` (server) or `App.xaml.cs` (Companion)
- the fake or stub in the test project (*Test doubles* below)
- the project table in `docs/ARCHITECTURE.md` and the area document in `docs/areas/`

**A new external API call or DTO** touches:
- the reader of the external shape and its types: `Censio.Pipe` for the pipe and recordings, later `Censio.GameData` and the ingest messages in `Censio.Contracts`; the external shape must not leak past it
- `PipeFrameBuilder` or other synthetic input in the tests, never real recordings, except the anonymised fixtures under `tests/Censio.Pipe.Tests/Fixtures/` (records 0029, 0033, 0042)

**A new pipe message type, field or tick rule** touches:
- `PipeMessageType`, `PipeMessageParser` and the message class in `Censio.Pipe`; `PipeFrameAnonymiser`, which walks the wire layout a second time (record 0042); `PipeTickBuilder` and `PipeTickLimits` for a tick rule
- the recording format version in `PipeRecordingFormat` when the recorded content changes
- the diagnostics columns in `DiagnosticsView`
- `docs/areas/pipe.md` (*Message types*, *Limits and errors*)

**A new tray menu entry, tooltip text or icon look** touches:
- `TrayMenu`, `TrayMenuEntryKind`, `TraySnapshot` or `TrayIconLook` in `Censio.Companion.Core`, and `TrayIcon` / `TrayIconImages` in `Censio.Companion`
- `docs/areas/companion.md` (*Tray icon and menu*), the menu list in `README.md` and the tooltip table in the user guide (English and German)

**A new log message** touches:
- `CompanionLog` (a `LoggerMessage` method) and the list of what is logged in `docs/areas/companion-log.md`

## Test doubles

The hand-written fakes and stubs the tests reuse (no mocking library unless `docs/UNIT_TESTS.md` says
otherwise), one line each: the double, where it lives and what it stands in for; how it behaves is read in its code.

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`, linked into the Companion tests) — an `ILogger` that keeps every entry, for tests of what a component logs.
- `ChunkedStream` (`tests/Censio.Pipe.Tests/ChunkedStream.cs`) — a read-only stream with chunked reads, I/O errors and cancellation at an offset, for the frame reader.
- `ChunkedClockStream` (`tests/Censio.Pipe.Tests/ChunkedClockStream.cs`) — a chunked read-only stream that runs an action per read, for record-and-replay round trips.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, linked into the Companion tests) — builds synthetic and corrupt pipe frames.
- `ScriptedPipeMessageSource` with `HeldPipeRead` (`tests/Censio.Pipe.Tests/`) — an `IPipeMessageSource` of queued or held results, for the tick assembler.
- `ScriptedPipeConnector` (`tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`) — an `IPipeConnector` of queued or held results, for the pipe connection.
- `ControlledPipeStream` (`tests/Censio.Companion.Core.Tests/ControlledPipeStream.cs`) — models an asynchronous pipe read's end and cancellation (checked against `dotnet/runtime` `release/11.0` `PipeStream.ValueTaskSource.cs`).
- `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into the Companion tests) — models a file stream's write and flush failures (checked against `dotnet/runtime` `release/11.0` `Win32Marshal`).
- `SparseStream` (`tests/Censio.GameData.Tests/SparseStream.cs`) — a sized, seekable read-only stream with written regions and an `IOException` at an offset; `RdaArchiveBuilder` (`tests/Censio.GameData.Tests/RdaArchiveBuilder.cs`) builds the archives it reads.
- `ScriptedPipeAnonymiserRandom` (`tests/Censio.Pipe.Tests/ScriptedPipeAnonymiserRandom.cs`, linked into the Anonymiser tests) — an `IPipeAnonymiserRandom` of queued draws, for the anonymiser's offset and factors.
- `TwoPassRecordingStream` (`tests/Censio.Pipe.Tests/TwoPassRecordingStream.cs`) — a seekable stream whose content or read errors differ per pass, for the anonymiser's two reads.
- `FailingTextWriter` (`tests/Censio.Anonymiser.Tests/FailingTextWriter.cs`) — a `TextWriter` whose writes throw `IOException` and are counted, for a standard output that cannot be written.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — the clock of every timing test; a test library, not a mocking library.
- `BackwardsFakeTimeProvider` (`tests/Censio.Pipe.Tests/BackwardsFakeTimeProvider.cs`) — models a system clock set back (checked by a scratch run).
- `RecordingTimeProvider` (`tests/Censio.Pipe.Tests/RecordingTimeProvider.cs`) — a `FakeTimeProvider` that records and awaits its timers, for replay and retry waits.
- `FakeDiagnosticsWindow` (`tests/Censio.Companion.Core.Tests/FakeDiagnosticsWindow.cs`) — an `IDiagnosticsWindow` that records what it shows, for the diagnostics session.
- `FakeStartupEntryStore` (`tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs`) — models the store contract of `RegistryStartupEntryStore` (checked against the .NET 11 `RegistryKey` source), for the autostart.
- `FakeUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/FakeUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords` of fixed records or a thrown exception, counting its reads.
- `FakeFileVersionReader` (`tests/Censio.Companion.Core.Tests/FakeFileVersionReader.cs`) — an `IFileVersionReader` with a version per full path or an exception, recording the paths asked for.
- `RegExportUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/RegExportUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords` over a `reg export` file, for the sample in `Fixtures/GameInstallation/`.
- `NonSeekableStream` (`tests/Censio.GameData.Tests/NonSeekableStream.cs`) — a forward-only stream of unknown length; `TrackingStream` (`tests/Censio.GameData.Tests/TrackingStream.cs`) — a memory stream that logs its disposal.
- `DdsFileBuilder` (`tests/Censio.GameData.Tests/DdsFileBuilder.cs`) — builds synthetic and patched BC7 DDS files and their expected pixels in code, no game icon.
