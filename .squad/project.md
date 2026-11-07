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
- Parsing of external input: the pipe stream (`PipeFrameReader`, `PipeMessageParser`, `PipeTickAssembler`), recording files (`PipeReplayStream`, `PipeRecordingReader`, `PipeRecordingFormat`, `PipeRecordingAnonymiser`), later game files and WebSocket messages — [pipe](../docs/areas/pipe.md), [pipe-recording](../docs/areas/pipe-recording.md).
- File paths the Companion writes, prunes or opens: the recordings folder (`PipeRecordingFiles`), the log folder (`CompanionFolders`, `LogFileLoggerProvider`, `LogFileNames`), Explorer (`FolderOpener`), the replay file from the command line, the anonymiser's output and temporary file from its command line (`AnonymiserCommand`), later the cache — [companion](../docs/areas/companion.md) (*Recording*, *Folders*, *Log*), [pipe-recording](../docs/areas/pipe-recording.md) (*Anonymising*).
- Windows autostart: `CompanionAutostart` over `RegistryStartupEntryStore`, the per-user Run value — [companion](../docs/areas/companion.md), *Start with Windows*.
- Logging and showing external data: the Companion's log writer (`LogLine`, `LogFileLoggerProvider`, `CompanionLog`), the diagnostics window, the pipe's error results, the server's request logging — [companion](../docs/areas/companion.md) (*Log*, *Diagnostics window*), [pipe](../docs/areas/pipe.md), [server](../docs/areas/server.md).
- Release publishing: `.github/workflows/release.yml` and `notify-website.yml`, their secrets in the environment `release`, Docker Hub `latest` — [record 0012](../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md), `docs/CONTRIBUTING.md` (*Versioning and releases*), [server](../docs/areas/server.md) (*Docker image*).
- Outbound calls: none exist; the first one is a new security area.

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
- the key and its validation: `Censio:DataDirectory` in `DataProtectionSetup`; the Companion's switches in `CompanionCommandLine`
- registration and binding: `src/Censio.Server/Program.cs`, `src/Censio.Companion/App.xaml.cs`
- the defaults: `src/Censio.Server/appsettings.json` and the `ENV` lines of `src/Censio.Server/Dockerfile`
- the configuration table in `README.md` (key, environment variable, default; English, then German) and the user guide
- the area document that states the option: `docs/areas/server.md` or `docs/areas/companion.md` (*Command line*)
- the tests that pin it: `DataProtectionSetupTests`, `ProgramTests`, `CompanionCommandLineTests`

**A new or changed service / module** touches:
- its interface where platform-neutral logic meets Windows: `IPipeConnector`, `IStartupEntryStore`, `IDiagnosticsWindow` in `Censio.Companion.Core`
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
- `CompanionLog` (a `LoggerMessage` method) and the list of what is logged in `docs/areas/companion.md` (*Log*)

## Test doubles

The hand-written fakes and stubs the tests reuse (no mocking library unless `docs/UNIT_TESTS.md` says
otherwise), one line each: the double, where it lives and what it stands in for; how it behaves is read in its code.

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`, linked into the Companion tests) — an `ILogger` that keeps every entry, for tests of what a component logs.
- `ChunkedStream` (`tests/Censio.Pipe.Tests/ChunkedStream.cs`) — a read-only stream with chunked reads, I/O errors and cancellation at an offset, for the frame reader.
- `ChunkedClockStream` (`tests/Censio.Pipe.Tests/ChunkedClockStream.cs`) — a chunked read-only stream that runs an action per read, for record-and-replay round trips.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, linked into the Companion tests) — builds synthetic and corrupt pipe frames.
- `ScriptedPipeMessageSource` with `HeldPipeRead` (`tests/Censio.Pipe.Tests/`) — an `IPipeMessageSource` of queued or held results, for the tick assembler.
- `ScriptedPipeConnector` (`tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`) — an `IPipeConnector` of queued or held results, for the pipe connection.
- `ControlledPipeStream` (`tests/Censio.Companion.Core.Tests/ControlledPipeStream.cs`) — models an asynchronous pipe read's end and cancellation (checked against `dotnet/runtime` `release/10.0` `PipeStream.ValueTaskSource.cs`).
- `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into the Companion tests) — models a file stream's write and flush failures (checked against `dotnet/runtime` `release/10.0` `Win32Marshal`).
- `ScriptedPipeAnonymiserRandom` (`tests/Censio.Pipe.Tests/ScriptedPipeAnonymiserRandom.cs`, linked into the Anonymiser tests) — an `IPipeAnonymiserRandom` of queued draws, for the anonymiser's offset and factors.
- `TwoPassRecordingStream` (`tests/Censio.Pipe.Tests/TwoPassRecordingStream.cs`) — a seekable stream whose content or read errors differ per pass, for the anonymiser's two reads.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — the clock of every timing test; a test library, not a mocking library.
- `BackwardsFakeTimeProvider` (`tests/Censio.Pipe.Tests/BackwardsFakeTimeProvider.cs`) — models a system clock set back (checked by a scratch run).
- `RecordingTimeProvider` (`tests/Censio.Pipe.Tests/RecordingTimeProvider.cs`) — a `FakeTimeProvider` that records and awaits its timers, for replay and retry waits.
- `FakeDiagnosticsWindow` (`tests/Censio.Companion.Core.Tests/FakeDiagnosticsWindow.cs`) — an `IDiagnosticsWindow` that records what it shows, for the diagnostics session.
- `FakeStartupEntryStore` (`tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs`) — models the store contract of `RegistryStartupEntryStore` (checked against the .NET 10 `RegistryKey` source), for the autostart.
