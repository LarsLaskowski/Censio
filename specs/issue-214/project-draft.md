# Project

What the squad needs to know beyond the stack; a product PR updates an entry it makes untrue (`.squad/routing.md`,
*Scope of a product PR*).

Every entry points: it names files, types or endpoints and links the document that says more. Behavior, values,
limits and sequences live in the area documents (`docs/areas/README.md`), reasons in the decision records.
`config-check.py` warns past `PROJECT_MAX_WORDS` words (`.squad/tools/squad_settings.py`).

## Security areas

A plan lists every area its change touches under *Security areas touched*, so Security reviews it
(`.squad/routing.md`, *Tiers*). One line per area: what is protected, its types, files or endpoints, and the area
document with its rules.

- Ingest token and Companion authentication: the WebSocket endpoint `/ingest` and its token (`IngestEndpoint`, `IngestSession` with every message type it stores, `ContractTradeRoutes` included, `IngestConnections`, `IngestHosts`, `IngestHost`, `IngestAdmission`, `IngestTokenSetup`, `IngestAccess` in `src/Censio.Server`; the data directory's `ingest-token`; `Censio:Ingest:TokenFile` and the file it names) — [ingest](../docs/areas/ingest.md), [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md).
- Network exposure: the server's listener and its routes reachable without login (`/`, `/health`, `/ingest`, the data pages (`/regions`, `/trade-routes`, `/profiles/…` including `/profiles/{profile}/trade-routes`), `/icons/{reference}`, the Blazor hub, static assets) in `src/Censio.Server/Program.cs`, `IconEndpoint` and `src/Censio.Server/Dockerfile`; the history deletion (`DeletionHostRule`); the preference cookies and `Accept-Language` (`UiPreferences`, `App.razor`) — [server](../docs/areas/server.md), [web-ui](../docs/areas/web-ui.md), [web-pages](../docs/areas/web-pages.md).
- Data-protection key ring: `DataProtectionSetup` under the data directory — [server](../docs/areas/server.md), *Data directory and key ring*.
- Server database: the file with its side files and folders, owner-only (`ServerDatabase`, `DatabaseSetup`), its history (`HistoryStore`, `HistoryRetention`), each profile's latest trade routes (tables `TradeRouteSets`, `TradeRoutes`, `TradeRouteStations`; `ServerStore.StoreTradeRoutesAsync`, `ReadTradeRoutesAsync`, `DeleteProfileAsync`) and the in-memory state (`ServerState`, `ServerStateSetup`) — [server-data](../docs/areas/server-data.md), [server-state](../docs/areas/server-state.md), [history](../docs/areas/history.md), [web-trends](../docs/areas/web-trends.md) (*The batch reads of the trend column*), [record 0097](../docs/decisions/0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md).
- Local pipe connection: `NamedPipeConnector` and `PipeEndpoint` open the game's pipe read-only at identification level — [pipe-connection](../docs/areas/pipe-connection.md), *Opening*.
- Parsing of external input: all of `src/Censio.Pipe` and `src/Censio.GameData`; `CatalogDocumentReader` and `ContractReader` in `src/Censio.Contracts`; `CompanionSettings`, `RegistryUbisoftInstallRecords`, `GameInstallationSearch`, `PageAddress`, `PngHeader`, `BrowserOffset` — [contract](../docs/areas/contract.md), [pipe](../docs/areas/pipe.md), [pipe-recording](../docs/areas/pipe-recording.md), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-catalog-format](../docs/areas/game-catalog-format.md), [game-installation](../docs/areas/game-installation.md), [companion](../docs/areas/companion.md) (*Settings file*), [web-pages](../docs/areas/web-pages.md), [web-charts](../docs/areas/web-charts.md).
- File paths the Companion writes, prunes or opens: `CompanionFolders`, `PipeRecordingFiles`, `LogFileLoggerProvider`, `LogFileNames`, `CompanionSettings`, `GameCatalogCacheStore`, `FolderOpener`, the replay file, `AnonymiserCommand` (output and temporary file), `ReplayTokenFile`; read-only the installation and its archives (`GameInstallation`, `WindowsFileVersionReader`, `RdaArchive`, `RdaInstallationSurvey`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [companion](../docs/areas/companion.md) (*Recording*, *Folders*, *Settings file*), [companion-log](../docs/areas/companion-log.md), [pipe-recording](../docs/areas/pipe-recording.md) (*Anonymising*), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-installation](../docs/areas/game-installation.md).
- Windows autostart: `CompanionAutostart` over `RegistryStartupEntryStore`, the per-user Run value — [companion](../docs/areas/companion.md), *Start with Windows*.
- Logging and showing external data: the Companion's log (`LogLine`, `LogFileLoggerProvider`, `CompanionLog`, `GameCatalogLoader`, `GameCatalogReporter`), its diagnostics window (`DiagnosticsWindow`, `GameIconImageConverter`, PNGs from `PngWriter` and `GameCatalogCacheFile`), the server's request logging and pages (`ProfileViewBuilder`, `PageNames`, `IconEndpoint`, `PngHeader`, `HistoryRetention`, `Regions`, `TradeRoutes`, `TrendChart`, `TrendCell`, `SparklineBuilder`, `TrendSeriesBuilder`, `TrendReader`) — [companion-log](../docs/areas/companion-log.md), [companion-diagnostics](../docs/areas/companion-diagnostics.md), [pipe](../docs/areas/pipe.md), [server](../docs/areas/server.md), [web-pages](../docs/areas/web-pages.md), [web-charts](../docs/areas/web-charts.md), [web-trends](../docs/areas/web-trends.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md).
- Release publishing: `.github/workflows/release.yml` and `notify-website.yml`, their secrets in the environment `release`, Docker Hub `latest` — [record 0012](../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md), `docs/CONTRIBUTING.md` (*Versioning and releases*), [server](../docs/areas/server.md) (*Docker image*).
- Outbound calls: only the replay's WebSocket to `--server` (`ReplaySender`, `ClientWebSocketConnector`, token from `ReplayTokenFile`); any other call to a server is a new security area. A UNC path (`--replay`, `--token-file`, the settings file's installation folder) is read over SMB (`GameInstallation`, `GameArchiveStateReader`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [replay-sending](../docs/areas/replay-sending.md), [game-installation](../docs/areas/game-installation.md), *Folders from outside*, [game-catalog](../docs/areas/game-catalog.md), *Limits and errors*.

## Guarantees

Deliberate behavior that changes only with the Product Manager. One line each, with its record and area document.

- Plain HTTP with TLS at a reverse proxy; the start page and `/health` need no login, `/health` reveals only its status — [record 0006](../docs/decisions/0006-server-plain-http-and-open-health.md), [server](../docs/areas/server.md).
- The server stores data from a Companion only with the shared ingest token, which only the setting `Censio:Ingest:RequireToken` `false` switches off — [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md), [ingest](../docs/areas/ingest.md).
- History in SQLite with the retention of record 0021, deletable per profile — [history](../docs/areas/history.md), [record 0021](../docs/decisions/0021-sqlite-history-with-retention.md), [0085](../docs/decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md).
- Unsent ticks are buffered within a bound and delivered in order; dropped ones show as a gap, never as zeros — [record 0031](../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) (area document with #41).

## Integration surface

Every place the Reviewer checks when a diff adds or changes a thing of this kind.

**A new or changed configuration option** touches:
- the key and its validation: `Censio:DataDirectory` in `DataProtectionSetup`; `Censio:Database:File` in `DatabaseSetup`; `Censio:Ingest:RequireToken` and `Censio:Ingest:TokenFile` in `IngestTokenSetup`; `AllowedHosts` in `DeletionHostRule`; the Companion's switches in `CompanionCommandLine`; `gameFolder` in `CompanionSettings`
- registration and binding: `src/Censio.Server/Program.cs`, `src/Censio.Companion/App.xaml.cs`
- defaults: `src/Censio.Server/appsettings.json`, the `ENV` lines of `src/Censio.Server/Dockerfile`
- the configuration table of `README.md` and the user guide (English and German)
- `docs/areas/server.md` or `docs/areas/companion.md` (*Command line*, *Settings file*)
- its tests: `DataProtectionSetupTests`, `DatabaseSetupTests`, `IngestTokenSetupTests`, `DeletionHostRuleTests`, `ProgramTests`, `CompanionCommandLineTests`, `CompanionSettingsTests`

**A new or changed service / module** touches:
- its interface where platform-neutral logic meets Windows: `IPipeConnector`, `IStartupEntryStore`, `IDiagnosticsWindow`, `IUbisoftInstallRecords`, `IFileVersionReader` in `Censio.Companion.Core`
- registration and lifetime: `Program.cs` (server) or `App.xaml.cs` (Companion)
- its fake or stub (*Test doubles*)
- the project table in `docs/ARCHITECTURE.md` and its area document

**A new external API call or DTO** touches:
- the reader of the external shape and its types (`Censio.Pipe`, `Censio.GameData`, `ContractReader` and `CatalogDocumentReader` in `Censio.Contracts`), past which the shape must not leak
- synthetic test input such as `PipeFrameBuilder`, never real recordings except the anonymised fixtures in `tests/Censio.Pipe.Tests/Fixtures/` (records 0029, 0033, 0042)

**A new or changed contract message** touches:
- the message class, `ContractMessageType`, `ContractReader` (`CurrentVersion`, bounds, `Deserialize`, `GetRefusedId`), `ContractScanner`, `ContractRules`, `ContractJsonContext` and the serialize switch of `ContractWriter` in `Censio.Contracts`, and the version texts (`ContractError`, `TrayMenu`)
- `ContractMessageConverter`, `ReplaySender` and `PipeReplayRunner` in `Censio.Companion.Core`; `IngestSession` (`StoreMessageAsync`, `GetId`) and `ServerState` on the server
- `docs/areas/contract.md`, `ingest.md`, `replay-sending.md`, the version row of the user guide (English and German) and `docs/ARCHITECTURE.md`

**A new pipe message type, field or tick rule** touches:
- `PipeMessageType`, `PipeMessageParser` and the message class in `Censio.Pipe`; `PipeFrameAnonymiser` (record 0042); `PipeTickBuilder` and `PipeTickLimits` for a tick rule
- `PipeRecordingFormat`'s version when the recorded content changes
- the columns of `DiagnosticsView`
- `docs/areas/pipe.md` (*Message types*, *Limits and errors*)

**A new tray menu entry, tooltip text or icon look** touches:
- `TrayMenu`, `TrayMenuEntryKind`, `TraySnapshot` or `TrayIconLook` in `Censio.Companion.Core`, and `TrayIcon` / `TrayIconImages` in `Censio.Companion`
- `docs/areas/companion.md` (*Tray icon and menu*), the menu list in `README.md` and the tooltip table in the user guide (English and German)

**A new page or UI text** touches: `UiTexts` (English and German) and its tests, the navigation in `MainLayout.razor`, `docs/areas/web-ui.md` (*Texts*), the user guide.

**A new log message** touches: `CompanionLog` (a `LoggerMessage` method) and the list of what is logged in `docs/areas/companion-log.md`.

## Test doubles

Hand-written doubles the tests reuse, no mocking library (`docs/UNIT_TESTS.md`): where each lives and what it stands
in for; their behavior is in their code.

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`, linked into the Companion and Server.Data tests) and `ConcurrentRecordingLogger` (`tests/Censio.Server.Tests/ConcurrentRecordingLogger.cs`) — `ILogger`s keeping every entry, the second from any thread.
- `ExtendedMigrationsAssembly` (`tests/Censio.Server.Data.Tests/`) — an `IMigrationsAssembly` of the real migrations plus test ones.
- `FakeProfileStateSource` (`tests/Censio.Server.Tests/FakeProfileStateSource.cs`) — an `IProfileStateSource`.
- `FakeHistoryTrends` (`tests/Censio.Server.Tests/FakeHistoryTrends.cs`) — an `IHistoryTrends` with held, failing and recorded reads.
- `HeldTimerTimeProvider` (`tests/Censio.Server.Tests/HeldTimerTimeProvider.cs`) — a `FakeTimeProvider` whose timer callbacks run by hand, also after disposal.
- `BlockingClockTimeProvider` (`tests/Censio.Server.Tests/BlockingClockTimeProvider.cs`) — a `FakeTimeProvider` whose first clock read blocks.
- `ChunkedStream` and `ChunkedClockStream` (`tests/Censio.Pipe.Tests/`) — chunked streams for the frame reader and for record-and-replay.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, linked into the Companion and Anonymiser tests) — synthetic and corrupt pipe frames.
- `ScriptedPipeMessageSource` with `HeldPipeRead` (`tests/Censio.Pipe.Tests/`) — an `IPipeMessageSource`.
- `ScriptedPipeConnector` (`tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`) — an `IPipeConnector`.
- `ControlledPipeStream` (`tests/Censio.Companion.Core.Tests/ControlledPipeStream.cs`) — models an asynchronous pipe read's end and cancellation (checked against `dotnet/runtime` `release/11.0` `PipeStream.ValueTaskSource.cs`).
- `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into the Companion tests) — models a file stream's write and flush failures (checked against `dotnet/runtime` `release/11.0` `Win32Marshal`).
- `SparseStream`, `NonSeekableStream`, `TrackingStream`, `RdaArchiveBuilder`, `DdsFileBuilder` (`tests/Censio.GameData.Tests/`) — sparse, forward-only and disposal-logging streams; builders of RDA archives and BC7 DDS files.
- `ScriptedPipeAnonymiserRandom` (`tests/Censio.Pipe.Tests/ScriptedPipeAnonymiserRandom.cs`, linked into the Anonymiser tests) — an `IPipeAnonymiserRandom`.
- `TwoPassRecordingStream` (`tests/Censio.Pipe.Tests/TwoPassRecordingStream.cs`) — a stream that differs per pass, for the anonymiser.
- `FailingTextWriter` (`tests/Censio.Anonymiser.Tests/FailingTextWriter.cs`) — a `TextWriter` whose writes fail.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — the clock of every timing test; a test library, not a mocking one.
- `BackwardsFakeTimeProvider` (`tests/Censio.Pipe.Tests/BackwardsFakeTimeProvider.cs`) — models a system clock set back (checked by a scratch run).
- `RecordingTimeProvider` (`tests/Censio.Pipe.Tests/RecordingTimeProvider.cs`, linked into the Companion and Server tests) — a `FakeTimeProvider` that records and awaits its timers.
- `RecordingLoggerProvider` (`tests/Censio.Server.Data.Tests/`, linked into the Server tests) — an `ILoggerProvider` over one `RecordingLogger`.
- `CascadedLanguageProbe` (`tests/Censio.Server.Tests/CascadedLanguageProbe.cs`) — a page, for what `MainLayout` cascades.
- `InMemoryDuplexStream` (`tests/Censio.Server.Tests/InMemoryDuplexStream.cs`, linked into the Companion tests) — a connection end, for real `WebSocket` pairs.
- `FailingWebSocketFeature` (`tests/Censio.Server.Tests/FailingWebSocketFeature.cs`) — an `IHttpWebSocketFeature` failing every accept.
- `FakeStartupEntryStore` (`tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs`) — models `RegistryStartupEntryStore` (checked against the .NET 11 `RegistryKey` source).
- `FakeDiagnosticsWindow`, `FakeUbisoftInstallRecords`, `FakeFileVersionReader` (`tests/Censio.Companion.Core.Tests/`) — an `IDiagnosticsWindow`, `IUbisoftInstallRecords` and `IFileVersionReader`.
- `RegExportUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/RegExportUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords` over the `reg export` sample in `Fixtures/GameInstallation/`.
- `ScriptedIngestServer`, `DuplexReplayConnector`, `ThrowingWebSocket`, `SendFailingWebSocket`, `LoopbackWebSocketHost`, `LoopbackWebProxy` (`tests/Censio.Companion.Core.Tests/`), `TestServerReplayConnector` (`tests/Censio.Server.Tests/`) — doubles of the replay sender's tests.
- `ForeignContractMessage` (`tests/Censio.Contracts.Tests/ForeignContractMessage.cs`) — a `ContractMessage` of no contract type.
