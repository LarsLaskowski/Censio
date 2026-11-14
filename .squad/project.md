# Project

What the squad needs to know about this project that is not stack-specific, read by the Lead, the Devil's
Advocate, Security, the Tester and the Reviewer. `adopt-template` creates it once and never overwrites it; a
product PR updates it when the change makes an entry untrue (`.squad/routing.md`, *Scope of a product PR*).

Every entry points: it names files, types or endpoints and links the document that says more. Behavior, values,
limits and sequences live in the area documents (`docs/areas/README.md`), reasons in the decision records.
`config-check.py` warns past `PROJECT_MAX_WORDS` words (`.squad/tools/squad_settings.py`).

## Security areas

A change that touches one of these lists it under *Security areas touched* in its plan, so Security reviews the
plan (`.squad/routing.md`, *Tiers*, which also says when a change is tier `security`). One line per area: what is
protected, the concrete types, files or endpoints, and a link to the area document that states its rules
(`- Session tokens: issued in src/Auth/, checked by every /api route — [Sessions](../docs/areas/sessions.md)`); no
limits, patterns or sequences.

- Ingest token and Companion authentication: the WebSocket endpoint `/ingest` and its token (`IngestEndpoint`, `IngestSession`, `IngestConnections`, `IngestTokenSetup`, `IngestAccess` in `src/Censio.Server`, the file `ingest-token` in the data directory, the setting `Censio:Ingest:TokenFile` and the file it names) — [ingest](../docs/areas/ingest.md), [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md).
- Network exposure: the server's listener and its routes reachable without login (`/`, `/health`, `/ingest`, the data pages (`/regions`, `/profiles/…`), `/icons/{reference}`, the Blazor hub, static assets) in `src/Censio.Server/Program.cs`, `IconEndpoint` and `src/Censio.Server/Dockerfile`; the preference cookies and `Accept-Language` (`UiPreferences`, `App.razor`) — [server](../docs/areas/server.md), [web-ui](../docs/areas/web-ui.md), [web-pages](../docs/areas/web-pages.md).
- Data-protection key ring: `DataProtectionSetup` under the data directory — [server](../docs/areas/server.md), *Data directory and key ring*.
- Server database: the file, its side files and folders, owner-only (`ServerDatabase`, `DatabaseSetup`), and the in-memory state of the stored data (`ServerState`, `ServerStateSetup`) — [server-data](../docs/areas/server-data.md), [server-state](../docs/areas/server-state.md).
- Local pipe connection: `NamedPipeConnector` and `PipeEndpoint` open the game's pipe read-only at identification level — [pipe-connection](../docs/areas/pipe-connection.md), *Opening*.
- Parsing of external input: all of `src/Censio.Pipe` and `src/Censio.GameData`; `CatalogDocumentReader` and `ContractReader` in `src/Censio.Contracts`; `CompanionSettings`, `RegistryUbisoftInstallRecords`, `GameInstallationSearch`, `PageAddress`, `PngHeader` and `DataStatus` (the browser's UTC offset) — [contract](../docs/areas/contract.md), [pipe](../docs/areas/pipe.md), [pipe-recording](../docs/areas/pipe-recording.md), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-catalog-format](../docs/areas/game-catalog-format.md), [game-installation](../docs/areas/game-installation.md), [companion](../docs/areas/companion.md) (*Settings file*), [web-pages](../docs/areas/web-pages.md).
- File paths the Companion writes, prunes or opens: `CompanionFolders`, `PipeRecordingFiles`, `LogFileLoggerProvider`, `LogFileNames`, `CompanionSettings`, `GameCatalogCacheStore`, `FolderOpener`, the replay file, `AnonymiserCommand` (output and temporary file), `ReplayTokenFile`; read-only the installation and its archives (`GameInstallation`, `WindowsFileVersionReader`, `RdaArchive`, `RdaInstallationSurvey`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [companion](../docs/areas/companion.md) (*Recording*, *Folders*, *Settings file*), [companion-log](../docs/areas/companion-log.md), [pipe-recording](../docs/areas/pipe-recording.md) (*Anonymising*), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-installation](../docs/areas/game-installation.md).
- Windows autostart: `CompanionAutostart` over `RegistryStartupEntryStore`, the per-user Run value — [companion](../docs/areas/companion.md), *Start with Windows*.
- Logging and showing external data: the Companion's log (`LogLine`, `LogFileLoggerProvider`, `CompanionLog`, `GameCatalogLoader`, `GameCatalogReporter`), its diagnostics window (`DiagnosticsWindow`, `GameIconImageConverter`, PNGs from `PngWriter` and `GameCatalogCacheFile`), the server's request logging and pages (`ProfileViewBuilder`, `PageNames`, `IconEndpoint`, `PngHeader`) — [companion-log](../docs/areas/companion-log.md), [companion-diagnostics](../docs/areas/companion-diagnostics.md), [pipe](../docs/areas/pipe.md), [server](../docs/areas/server.md), [web-pages](../docs/areas/web-pages.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md).
- Release publishing: `.github/workflows/release.yml` and `notify-website.yml`, their secrets in the environment `release`, Docker Hub `latest` — [record 0012](../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md), `docs/CONTRIBUTING.md` (*Versioning and releases*), [server](../docs/areas/server.md) (*Docker image*).
- Outbound calls: only the replay's WebSocket to the server named by `--server` (`ReplaySender`, `ClientWebSocketConnector`, `IReplayServerConnector`), with the token from `ReplayTokenFile`, no proxy, no redirect; any other call to a server is a new security area. A UNC installation folder from the settings file is probed and read over SMB (`GameInstallation`, `GameArchiveStateReader`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [replay-sending](../docs/areas/replay-sending.md), [game-installation](../docs/areas/game-installation.md), *Folders from outside*, [game-catalog](../docs/areas/game-catalog.md), *Limits and errors*.

## Guarantees

Deliberate behavior that changes only with the Product Manager. One line each: the guarantee in a few words, its
decision record where it was a real choice, and the area document that states it.

- Plain HTTP with TLS at a reverse proxy; the start page and `/health` need no login, `/health` reveals only its status — [record 0006](../docs/decisions/0006-server-plain-http-and-open-health.md), [server](../docs/areas/server.md).
- The server stores data from a Companion only with the shared ingest token, which only the setting `Censio:Ingest:RequireToken` `false` switches off — [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md), [ingest](../docs/areas/ingest.md).
- History in SQLite with the retention of record 0021, deletable per profile — [record 0021](../docs/decisions/0021-sqlite-history-with-retention.md), [server-data](../docs/areas/server-data.md) (retention with #35).
- Unsent ticks are buffered within a bound and delivered in order; dropped ones show as a gap, never as zeros — [record 0031](../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) (area document with #41).

## Integration surface

What the Reviewer checks when the diff introduces or changes a thing of this kind: every place that must
change with it.

**A new or changed configuration option** touches:
- the key and its validation: `Censio:DataDirectory` in `DataProtectionSetup`; `Censio:Database:File` in `DatabaseSetup`; `Censio:Ingest:RequireToken` and `Censio:Ingest:TokenFile` in `IngestTokenSetup`; the Companion's switches in `CompanionCommandLine`; `gameFolder` in `CompanionSettings`
- registration and binding: `src/Censio.Server/Program.cs`, `src/Censio.Companion/App.xaml.cs`
- the defaults: `src/Censio.Server/appsettings.json` and the `ENV` lines of `src/Censio.Server/Dockerfile`
- the configuration table in `README.md` (English and German) and the user guide
- its area document: `docs/areas/server.md` or `docs/areas/companion.md` (*Command line*, *Settings file*)
- the tests that pin it: `DataProtectionSetupTests`, `DatabaseSetupTests`, `IngestTokenSetupTests`, `ProgramTests`, `CompanionCommandLineTests`, `CompanionSettingsTests`

**A new or changed service / module** touches:
- its interface where platform-neutral logic meets Windows: `IPipeConnector`, `IStartupEntryStore`, `IDiagnosticsWindow`, `IUbisoftInstallRecords`, `IFileVersionReader` in `Censio.Companion.Core`
- registration and lifetime: `Program.cs` (server) or `App.xaml.cs` (Companion)
- the fake or stub in the test project (*Test doubles* below)
- the project table in `docs/ARCHITECTURE.md` and the area document in `docs/areas/`

**A new external API call or DTO** touches:
- the reader of the external shape and its types (`Censio.Pipe`, `Censio.GameData`, `ContractReader` and `CatalogDocumentReader` in `Censio.Contracts`), past which the shape must not leak
- `PipeFrameBuilder` or other synthetic input in the tests, never real recordings, except the anonymised fixtures under `tests/Censio.Pipe.Tests/Fixtures/` (records 0029, 0033, 0042)

**A new pipe message type, field or tick rule** touches:
- `PipeMessageType`, `PipeMessageParser` and the message class in `Censio.Pipe`; `PipeFrameAnonymiser` (record 0042); `PipeTickBuilder` and `PipeTickLimits` for a tick rule
- the recording format version in `PipeRecordingFormat` when the recorded content changes
- the diagnostics columns in `DiagnosticsView`
- `docs/areas/pipe.md` (*Message types*, *Limits and errors*)

**A new tray menu entry, tooltip text or icon look** touches:
- `TrayMenu`, `TrayMenuEntryKind`, `TraySnapshot` or `TrayIconLook` in `Censio.Companion.Core`, and `TrayIcon` / `TrayIconImages` in `Censio.Companion`
- `docs/areas/companion.md` (*Tray icon and menu*), the menu list in `README.md` and the tooltip table in the user guide (English and German)

**A new page or UI text** touches: `UiTexts` (English and German) and its tests, the navigation in `MainLayout.razor`, `docs/areas/web-ui.md` (*Texts*), the user guide.

**A new log message** touches:
- `CompanionLog` (a `LoggerMessage` method) and the list of what is logged in `docs/areas/companion-log.md`

## Test doubles

Hand-written fakes and stubs the tests reuse, no mocking library (`docs/UNIT_TESTS.md`). One line each: the double,
its file and what it stands in for; its behavior is read in its code.

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`, linked into the Companion and Server.Data tests) — an `ILogger` that keeps every entry.
- `ConcurrentRecordingLogger` (`tests/Censio.Server.Tests/ConcurrentRecordingLogger.cs`) — an `ILogger` for entries from any thread.
- `ExtendedMigrationsAssembly` (`tests/Censio.Server.Data.Tests/`) — an `IMigrationsAssembly` of the real migrations plus test ones, for a test-only schema history.
- `FakeProfileStateSource` (`tests/Censio.Server.Tests/FakeProfileStateSource.cs`) — an `IProfileStateSource` of held and stored states.
- `ChunkedStream` (`tests/Censio.Pipe.Tests/ChunkedStream.cs`) — a chunked read-only stream with faults, for the frame reader.
- `ChunkedClockStream` (`tests/Censio.Pipe.Tests/ChunkedClockStream.cs`) — a chunked stream for record-and-replay round trips.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, linked into the Companion and Anonymiser tests) — synthetic and corrupt pipe frames.
- `ScriptedPipeMessageSource` with `HeldPipeRead` (`tests/Censio.Pipe.Tests/`) — an `IPipeMessageSource`, for the tick assembler.
- `ScriptedPipeConnector` (`tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`) — an `IPipeConnector`, for the pipe connection.
- `ControlledPipeStream` (`tests/Censio.Companion.Core.Tests/ControlledPipeStream.cs`) — models an asynchronous pipe read's end and cancellation (checked against `dotnet/runtime` `release/11.0` `PipeStream.ValueTaskSource.cs`).
- `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into the Companion tests) — models a file stream's write and flush failures (checked against `dotnet/runtime` `release/11.0` `Win32Marshal`).
- `SparseStream` (`tests/Censio.GameData.Tests/SparseStream.cs`) — a sparse seekable stream; `RdaArchiveBuilder` (`tests/Censio.GameData.Tests/RdaArchiveBuilder.cs`) builds RDA archives.
- `ScriptedPipeAnonymiserRandom` (`tests/Censio.Pipe.Tests/ScriptedPipeAnonymiserRandom.cs`, linked into the Anonymiser tests) — an `IPipeAnonymiserRandom` of queued draws.
- `TwoPassRecordingStream` (`tests/Censio.Pipe.Tests/TwoPassRecordingStream.cs`) — a seekable stream that differs per pass, for the anonymiser's two reads.
- `FailingTextWriter` (`tests/Censio.Anonymiser.Tests/FailingTextWriter.cs`) — a `TextWriter` whose writes fail.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — the clock of every timing test; a test library, not a mocking library.
- `BackwardsFakeTimeProvider` (`tests/Censio.Pipe.Tests/BackwardsFakeTimeProvider.cs`) — models a system clock set back (checked by a scratch run).
- `RecordingTimeProvider` (`tests/Censio.Pipe.Tests/RecordingTimeProvider.cs`, linked into the Companion and Server tests) — a `FakeTimeProvider` that records and awaits its timers.
- `RecordingLoggerProvider` (`tests/Censio.Server.Data.Tests/`, linked into the Server tests) — an `ILoggerProvider` over one `RecordingLogger`.
- `CascadedLanguageProbe` (`tests/Censio.Server.Tests/CascadedLanguageProbe.cs`) — a page stand-in, for what pages receive from `MainLayout`.
- `InMemoryDuplexStream` (`tests/Censio.Server.Tests/InMemoryDuplexStream.cs`) — an in-memory connection end, for real `WebSocket` pairs.
- `FailingWebSocketFeature` (`tests/Censio.Server.Tests/FailingWebSocketFeature.cs`) — an `IHttpWebSocketFeature` whose every accept fails.
- `FakeDiagnosticsWindow` (`tests/Censio.Companion.Core.Tests/FakeDiagnosticsWindow.cs`) — an `IDiagnosticsWindow`.
- `FakeStartupEntryStore` (`tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs`) — models the store contract of `RegistryStartupEntryStore` (checked against the .NET 11 `RegistryKey` source).
- `FakeUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/FakeUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords`.
- `FakeFileVersionReader` (`tests/Censio.Companion.Core.Tests/FakeFileVersionReader.cs`) — an `IFileVersionReader`.
- `RegExportUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/RegExportUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords` over a `reg export` file, for the sample in `Fixtures/GameInstallation/`.
- `NonSeekableStream` (`tests/Censio.GameData.Tests/NonSeekableStream.cs`) — a forward-only stream; `TrackingStream` (`tests/Censio.GameData.Tests/TrackingStream.cs`) — a memory stream that logs its disposal.
- `DdsFileBuilder` (`tests/Censio.GameData.Tests/DdsFileBuilder.cs`) — synthetic BC7 DDS files and their expected pixels.
- `ForeignContractMessage` (`tests/Censio.Contracts.Tests/ForeignContractMessage.cs`) — a `ContractMessage` of no contract type.
