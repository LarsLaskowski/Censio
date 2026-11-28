# Project

What the squad needs to know beyond the stack; a product PR updates an entry it makes untrue (`.squad/routing.md`,
*Scope of a product PR*). The test doubles are in `docs/UNIT_TESTS.md` (*Test doubles*).

Every entry points: it names files, types or endpoints and links the document that says more. Behavior, values,
limits and sequences live in the area documents (`docs/areas/README.md`), reasons in the decision records.
`config-check.py` warns about an entry of more than `PROJECT_ENTRY_MAX_PROSE` prose words (code spans and links do
not count) and about a change that adds more than `PROJECT_MAX_GROWTH` words to this file
(`.squad/tools/squad_settings.py`).

## Security areas

A plan lists every area its change touches under *Security areas touched*, so Security reviews it
(`.squad/routing.md`, *Tiers*). One line per area: what is protected, its types, files or endpoints, and the area
document with its rules.

- Ingest token and Companion authentication: the WebSocket endpoint `/ingest` and its token (`IngestEndpoint`, `IngestSession` (and `IngestStores`) with every message type it handles, `ContractTradeRoutes`, `ContractProfileDeletion`, `ContractProfileMerge` included, `IngestConnections`, `IngestHosts`, `IngestHost`, `IngestAdmission`, `IngestTokenSetup`, `IngestAccess` in `src/Censio.Server`; the data directory's `ingest-token`; `Censio:Ingest:TokenFile` and the file it names) — [ingest](../docs/areas/ingest.md), [ingest-token](../docs/areas/ingest-token.md), [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md).
- Network exposure: the server's listener and its routes reachable without login (`/`, `/health`, `/ingest`, the data pages (`/regions`, `/trade-routes`, `/profiles/…` including `/profiles/{profile}/trade-routes`), `/icons/{reference}`, the Blazor hub, static assets) in `src/Censio.Server/CensioServer.cs`, `IconEndpoint` and `src/Censio.Server/Dockerfile`; local mode's listener (`CensioServer.StartLocalAsync`, `LocalServerHost`); the history deletion (`DeletionHostRule` on the pages, the token through `/ingest`) and the history merge (the token through `/ingest`); the preference cookies and `Accept-Language` (`UiPreferences`, `App.razor`) — [server](../docs/areas/server.md), [web-ui](../docs/areas/web-ui.md), [web-pages](../docs/areas/web-pages.md), [local-mode](../docs/areas/local-mode.md).
- Data-protection key ring: `DataProtectionSetup` under the data directory — [server](../docs/areas/server.md), *Data directory and key ring*.
- Server database: the file with its side files and folders, owner-only (`ServerDatabase`, `DatabaseSetup`), its history (`HistoryStore`, `HistoryRetention`), each profile's latest trade routes (tables `TradeRouteSets`, `TradeRoutes`, `TradeRouteStations`; `ServerStore.StoreTradeRoutesAsync`, `ReadTradeRoutesAsync`, `DeleteProfileAsync` (also from `IngestSession`)) and the in-memory state (`ServerState`, `ServerStateSetup`) — [server-data](../docs/areas/server-data.md), [server-state](../docs/areas/server-state.md), [history](../docs/areas/history.md), [history-deletion](../docs/areas/history-deletion.md), [web-trends](../docs/areas/web-trends.md) (*The batch reads of the trend column*), [record 0097](../docs/decisions/0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md).
- Local pipe connection: `NamedPipeConnector` and `PipeEndpoint` open the game's pipe read-only at identification level — [pipe-connection](../docs/areas/pipe-connection.md), *Opening*.
- Parsing of external input: all of `src/Censio.Pipe` and `src/Censio.GameData`; `CatalogDocumentReader` and `ContractReader` in `src/Censio.Contracts`; `CompanionSettings`, `CompanionSettingsDocument`, `CompanionProfiles`, `ForwardServerSetting`, `LiveBufferFile`, `RegistryUbisoftInstallRecords`, `GameInstallationSearch`, `PageAddress`, `PngHeader`, `BrowserOffset` — [contract](../docs/areas/contract.md), [pipe](../docs/areas/pipe.md), [pipe-recording](../docs/areas/pipe-recording.md), [live-sending](../docs/areas/live-sending.md), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-catalog-format](../docs/areas/game-catalog-format.md), [game-installation](../docs/areas/game-installation.md), [companion](../docs/areas/companion.md) (*Settings file*), [companion-settings](../docs/areas/companion-settings.md), [companion-profiles](../docs/areas/companion-profiles.md), [web-pages](../docs/areas/web-pages.md), [web-charts](../docs/areas/web-charts.md).
- File paths the Companion writes, prunes or opens: `CompanionFolders`, `PipeRecordingFiles`, `LogFileLoggerProvider`, `LogFileNames`, `CompanionSettings`, `CompanionSettingsDocument` (`settings.json`, `.tmp`), `CompanionProfiles` (`profiles.json`), `CompanionRestart` (own exe, `--restarted`), `GameCatalogCacheStore`, `FolderOpener`, `LiveBuffer`, the replay file, `AnonymiserCommand` (output and temporary file), `ReplayTokenFile`; read-only the installation and its archives (`GameInstallation`, `WindowsFileVersionReader`, `RdaArchive`, `RdaInstallationSurvey`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [companion](../docs/areas/companion.md) (*Recording*, *Folders*, *Settings file*), [companion-settings](../docs/areas/companion-settings.md), [companion-profiles](../docs/areas/companion-profiles.md), [companion-log](../docs/areas/companion-log.md), [pipe-recording](../docs/areas/pipe-recording.md) (*Anonymising*), [game-files](../docs/areas/game-files.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md), [game-installation](../docs/areas/game-installation.md), [local-mode](../docs/areas/local-mode.md).
- Windows autostart: `CompanionAutostart` over `RegistryStartupEntryStore`, the per-user Run value, toggled only by `SettingsWindowSession` — [companion](../docs/areas/companion.md), *Start with Windows*, [companion-settings](../docs/areas/companion-settings.md).
- Logging and showing external data: the Companion's log (`LogLine`, `LogFileLoggerProvider`, `CompanionLog`, `GameCatalogLoader`, `GameCatalogReporter`), its diagnostics window (`DiagnosticsWindow`, `SettingsWindow` with the masked token and profile names, `TrayIcon.ShowNotice`, `GameIconImageConverter`, PNGs from `PngWriter` and `GameCatalogCacheFile`), the server's request logging and pages (`ProfileViewBuilder`, `PageNames`, `IconEndpoint`, `PngHeader`, `HistoryRetention`, `Regions`, `TradeRoutes`, `TrendChart`, `TrendCell`, `SparklineBuilder`, `TrendSeriesBuilder`, `TrendReader`), the hosted server's lines in the Companion log (`BorrowedLoggerProvider`) — [local-mode](../docs/areas/local-mode.md), [companion-log](../docs/areas/companion-log.md), [companion-diagnostics](../docs/areas/companion-diagnostics.md), [pipe](../docs/areas/pipe.md), [server](../docs/areas/server.md), [web-pages](../docs/areas/web-pages.md), [web-charts](../docs/areas/web-charts.md), [web-trends](../docs/areas/web-trends.md), [game-catalog](../docs/areas/game-catalog.md), [game-catalog-icons](../docs/areas/game-catalog-icons.md), [game-catalog-cache](../docs/areas/game-catalog-cache.md).
- Release publishing: `.github/workflows/release.yml` and `notify-website.yml`, their secrets in the environment `release`, Docker Hub `latest` — [record 0012](../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md), `docs/CONTRIBUTING.md` (*Versioning and releases*), [server](../docs/areas/server.md) (*Docker image*).
- Outbound calls: the replay's WebSocket to `--server` (`ReplaySender`, `IngestClient`, `ClientWebSocketConnector`, token from `ReplayTokenFile`), live sending to the settings file's `serverAddress` with its `serverToken` (`LiveSender.ForServer`, `ForwardServerSetting`) and, in local mode, both to `127.0.0.1` (`ReplaySender.ForLocalServer`, `LiveSender.ForLocalServer`); any other call to a server is a new security area. A UNC path (`--replay`, `--token-file`, the settings file's installation folder, `SettingsWindowSession`, `CompanionSettingsValidator`) is read over SMB (`GameInstallation`, `GameArchiveStateReader`, `GameCatalogReader.Load`, `GameIconReader.Load`) — [replay-sending](../docs/areas/replay-sending.md), [live-sending](../docs/areas/live-sending.md), [game-installation](../docs/areas/game-installation.md), *Folders from outside*, [game-catalog](../docs/areas/game-catalog.md), *Limits and errors*.

## Guarantees

Deliberate behavior that changes only with the Product Manager. One line each, with its record and area document.

- Plain HTTP with TLS at a reverse proxy; the start page and `/health` need no login, `/health` reveals only its status — [record 0006](../docs/decisions/0006-server-plain-http-and-open-health.md), [server](../docs/areas/server.md).
- The server stores data from a Companion only with the shared ingest token, which only the setting `Censio:Ingest:RequireToken` `false` switches off — [record 0019](../docs/decisions/0019-websocket-transport-with-shared-token.md), [ingest](../docs/areas/ingest.md), [ingest-token](../docs/areas/ingest-token.md).
- History in SQLite with the retention of record 0021, deletable per profile — [history](../docs/areas/history.md), [history-deletion](../docs/areas/history-deletion.md), [record 0021](../docs/decisions/0021-sqlite-history-with-retention.md), [0085](../docs/decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md), [0116](../docs/decisions/0116-history-deleted-through-the-ingest-connection-with-the-token.md).
- Unsent ticks are buffered within a bound and delivered in order; dropped ones show as a gap, never as zeros — [record 0031](../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) ([live-sending](../docs/areas/live-sending.md), *Buffer*; record [0105](../docs/decisions/0105-unsent-live-messages-kept-one-file-each-in-a-fixed-buffer-folder.md)).

## Integration surface

Every place the Reviewer checks when a diff adds or changes a thing of this kind.

**A new or changed configuration option** touches:
- the key and its validation: `Censio:DataDirectory` in `DataProtectionSetup`; `Censio:Database:File` in `DatabaseSetup`; `Censio:Ingest:RequireToken` and `Censio:Ingest:TokenFile` in `IngestTokenSetup`; `AllowedHosts` in `DeletionHostRule`; the Companion's switches in `CompanionCommandLine`; `gameFolder`, `mode`, `localPort`, `serverAddress`, `serverToken`, `bufferHours`, `bufferMegabytes` and `language` in `CompanionSettings` (`ReadLanguage`, written by `CompanionSettingsDocument`)
- registration and binding: `src/Censio.Server/CensioServer.cs`, `src/Censio.Companion/App.xaml.cs`
- defaults: `src/Censio.Server/appsettings.json`, the `ENV` lines of `src/Censio.Server/Dockerfile`
- the configuration table of `README.md` and the user guide (English and German)
- `docs/areas/server.md`, `docs/areas/companion.md` (*Command line*, *Settings file*), `docs/areas/companion-settings.md` or `docs/areas/live-sending.md`
- its tests: `DataProtectionSetupTests`, `DatabaseSetupTests`, `IngestTokenSetupTests`, `DeletionHostRuleTests`, `ProgramTests`, `CompanionCommandLineTests`, `CompanionSettingsTests`, `CompanionSettingsDocumentTests`

**A new or changed service / module** touches:
- its interface where platform-neutral logic meets Windows: `IPipeConnector`, `IStartupEntryStore`, `IDiagnosticsWindow`, `ISettingsWindow`, `IUbisoftInstallRecords`, `IFileVersionReader` in `Censio.Companion.Core`
- registration and lifetime: `CensioServer` (server) or `App.xaml.cs` (Companion, `LocalServerSession`)
- its fake or stub (`docs/UNIT_TESTS.md`, *Test doubles*)
- the project table in `docs/ARCHITECTURE.md` and its area document

**A new external API call or DTO** touches:
- the reader of the external shape and its types (`Censio.Pipe`, `Censio.GameData`, `ContractReader` and `CatalogDocumentReader` in `Censio.Contracts`), past which the shape must not leak
- synthetic test input such as `PipeFrameBuilder`, never real recordings except the anonymised fixtures in `tests/Censio.Pipe.Tests/Fixtures/` (records 0029, 0033, 0042)

**A new or changed contract message** touches:
- the message class, `ContractMessageType`, `ContractReader` (`CurrentVersion`, bounds, `Deserialize`, `GetRefusedId`), `ContractScanner`, `ContractRules`, `ContractJsonContext` and the serialize switch of `ContractWriter` in `Censio.Contracts`, and the version texts (`ContractError`, `TrayMenu`)
- `ContractMessageConverter`, `ReplaySender`, `LiveSender`, `IngestClient` and `PipeReplayRunner` in `Censio.Companion.Core`; `IngestSession` (`StoreMessageAsync`, `GetId`) and `ServerState` on the server
- `docs/areas/contract.md`, `ingest.md`, `replay-sending.md`, `live-sending.md`, the version row of the user guide (English and German) and `docs/ARCHITECTURE.md`

**A new pipe message type, field or tick rule** touches:
- `PipeMessageType`, `PipeMessageParser` and the message class in `Censio.Pipe`; `PipeFrameAnonymiser` (record 0042); `PipeTickBuilder` and `PipeTickLimits` for a tick rule
- `PipeRecordingFormat`'s version when the recorded content changes
- the columns of `DiagnosticsView`
- `docs/areas/pipe.md` (*Message types*, *Limits and errors*)

**A new tray menu entry, tooltip text or icon look** touches:
- `TrayMenu`, `TrayMenuEntryKind`, `TraySnapshot`, `TrayServerStatus` or `TrayIconLook` in `Censio.Companion.Core`, and `TrayIcon` / `TrayIconImages` in `Censio.Companion`
- `docs/areas/companion.md` (*Tray icon and menu*), the menu list in `README.md` and the tooltip table in the user guide (English and German)

**A new page or UI text** touches: `UiTexts` (English and German) and its tests, the navigation in `MainLayout.razor`, `docs/areas/web-ui.md` (*Texts*), the user guide.

**A new log message** touches: `CompanionLog` (a `LoggerMessage` method) and the list of what is logged in `docs/areas/companion-log.md`.
