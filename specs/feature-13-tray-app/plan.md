# Plan: Tray app with connection status and recording control

Source: Issue #13 | [spec.md](spec.md)
Status: Draft
Tier: security — the change creates two guards: the log writer escapes external text it writes (control and format characters, length caps) and the log pruning confines deletion to an exact name pattern in a fixed folder; it also adds a dependency.
Security areas touched: File paths for recordings (the new log folder `%LOCALAPPDATA%\Censio\Logs`, its pruning, the Explorer launch of both folders, `PipeRecordingFiles`), Logging of external data (the new log writer `LogLine`, `LogFileLoggerProvider`; replay file name and exception texts), dependency (`Microsoft.Extensions.Logging.Abstractions` in `Directory.Packages.props`), build configuration (`src/Censio.Companion.Core/Censio.Companion.Core.csproj`)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Issue: "Tray icon reflects the connection states" — today it does not; the icon is fixed | `src/Censio.Companion/TrayIcon.cs:78` `SystemIcons.Application` | refuted |
| 2 | The live connection assembles no ticks; only the replay does | `PipeConnection.cs:256` (`PipeMessageReader` only); `PipeReplayRunner.cs` uses `PipeTickAssembler`. `docs/ARCHITECTURE.md` lines 45-46 ("groups the messages into ticks") is true only for the replay until this change | confirmed |
| 3 | The assembler's read observer gets the same stamp the tick uses, and an exception of the source surfaces from `ReadAsync` | `PipeTickAssembler.ReadStampedAsync` and `Feed` (awaits the faulted `_pending`) | confirmed |
| 4 | `NotifyIcon.Text` throws `ArgumentOutOfRangeException` above 127 characters | `dotnet/winforms` `release/10.0` `NotifyIcon.cs:19` `MaxTextSize = 127`, `:242` | confirmed |
| 5 | `TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)` clamps an overflow to `DateTimeOffset.MinValue`/`MaxValue`, so a time at the range edge would show a wrong value | `dotnet/runtime` `release/10.0` `TimeZoneInfo.cs:705-720` | confirmed |
| 6 | Longest tooltip: `Censio Companion: Protocol mismatch (recording failed)` (53) + newline + `Last tick: 0001-01-01 00:00:00Z, 256 islands` (45) = 99 < 127; `Replay error: invalid line 2147483647` lines are shorter | counted; a tick has at most 256 islands (`pipe.md`, *Limits*) | confirmed |
| 7 | A `\n` in `NotifyIcon.Text` shows a two-line tooltip | to verify: maintainer's manual run on Windows (AC10), before merge | unverified |
| 8 | `ContextMenuStrip` stays light with Windows in dark mode (WinForms color mode defaults to classic) | to verify: maintainer's manual run on Windows in dark mode (AC10) | unverified |
| 9 | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 restores and its `[LoggerMessage]` source generator builds clean under Reihitsu and Sonar | to verify: Dev, the G1 skeleton build and *Analyzer gate* (the package is in the local NuGet cache) | unverified |
| 10 | The recorder names and heads its file by the time of its first queued entry | `PipeRecorder.WriteAsync` (`_openFile(entry.Time)`, `FormatHeader(entry.Time)`) | confirmed |
| 11 | A session's failure is one-way today | `PipeRecordingSession.SetFailed`, `BeginConnection` returns `null` when failed | confirmed |
| 12 | A `Version` frame is the frame whose type byte is `0`, also when its version is not 2 | `pipe.md`, *Message types*, *Version gate* | confirmed |
| 13 | No record is released (no `v*` tag), so 0036 is edited in place | `git ls-remote --tags origin` returns nothing | confirmed |
| 14 | The release ZIP check publishes the Companion and checks it holds one file | `.github/scripts/release-check.sh companion` | confirmed |
| 15 | `Directory.Packages.props` is an input of the server image restore | `.squad/stack.md`, *Image build check* | confirmed |
| 16 | `explorer.exe <folder>` opens the folder | to verify: maintainer's manual run on Windows (AC10) | unverified |
| 17 | Existing call sites of changed members: `PipeRecordingSessionTests.cs` (27), `PipeConnectionTests.cs` (11), `PipeReplayRunnerTests.cs` (4), `TrayMenuTests.cs` (4) | `grep` of `new PipeRecordingSession`, `BeginConnection`, `EndConnection`, `new PipeConnection(`, `new PipeReplayRunner(`, `TrayMenu.Create` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeTickSummaryFromTickCountsIslands`; `TrayMenuFormatLastTickTodayShowsTimeOnly`, `...OtherDayShowsDate`, `...NearMinValueShowsUtc`, `...NearMaxValueShowsUtc`, `...OneIslandSingular`, `...NoneBeforeFirstTick`; `TrayMenuFormatToolTipWithoutTickHasOneLine`, `...WithTickHasTwoLines`, `...LongestFitsLimit`; `PipeConnectionTickClosedSetsLastTickAndRaisesEvent`, `PipeConnectionLastTickSurvivesConnectionEnd`, `PipeConnectionNoTickBeforeClose` | `TrayMenuTests.cs`, `PipeTickSummaryTests.cs`, `PipeConnectionTests.cs` |
| AC2 | `TrayMenuCreateListsEntriesInOrder`, `TrayMenuCreateRecordingShowsStop`, `TrayMenuCreateReplayGreysToggle`, `TrayMenuCreateInvalidGreysToggle`, `TrayMenuCreateStatusEntryShowsStatusText` | `TrayMenuTests.cs` |
| AC3 | `TraySnapshotForConnectionMapsLook` (one data row per state), `TraySnapshotForReplayMapsLook` (connected, receiving, mismatch, finished, error), `TraySnapshotForInvalidCommandLineIsProblem` | `TraySnapshotTests.cs` |
| AC4 | `PipeRecordingSessionStartsOff`, `...StartWhileOffRecords`, `...StopEndsRecorderAndClosesFile`, `...StartAfterStopWritesNewFile`, `...StartWithoutConnectionCreatesFileAtFirstFrame`, `...StartAfterFailureRecordsAgain`, `...StoppedRecorderFailureKeepsState`, `...ToggleSwitches`, `...StateChangedOnEveryChange` | `PipeRecordingSessionTests.cs` |
| AC5 | `PipeConnectionRecordingStartedMidConnectionWritesVersionFirst` (line 2 is the type-0 frame, its time equals line 3's), `...ReplaysAsReceiving` (file through `PipeReplayRunner`: `Receiving`, then finished), `...ReplayedTicksEqualLiveTicks` (ticks opened after the first frame), `...InMismatchReplaysAsMismatch`, `...BeforeVersionWritesNoExtraLine` | `PipeConnectionTests.cs` |
| AC6 | `PipeRecordingSessionStopAsyncCompletesActiveAndStoppedRecorders`, `PipeRecordingSessionStopAsyncGivesUpAfterTimeout` | `PipeRecordingSessionTests.cs` |
| AC7 | `FolderOpenerOpenCreatesMissingFolderAndLaunches`, `FolderOpenerOpenLaunchFailureLogsWarning`, `FolderOpenerOpenCreateFailureLogsWarningWithoutLaunch`; `CompanionFoldersLogsIsUnderCensio` | `FolderOpenerTests.cs`, `CompanionFoldersTests.cs` |
| AC8 | `LogFileNamesFormatFirstAndContinuation`, `LogFileNamesTryParseAcceptsOnlyExactForms` (data rows below), `LogFileNamesSelectForDeletionKeepsNewestByDateThenNumber`; `LogLineFormatLayout`, `LogLineSanitizeReplacesControlAndFormatCharacters` (rows below), `LogLineSanitizeCutsLongMessage`, `LogLineFormatExceptionKeepsLineBreaksOnly`; `LogFileLoggerProviderWritesLineToDailyFile`, `...RollsPastMaxBytes`, `...NewDayStartsNewFile`, `...ContinuesTodaysFileAfterRestart`, `...PrunesToMaxFilesAndKeepsOtherFiles`, `...UnwritableFolderDropsLines`, `...FullQueueDropsLines`, `...BelowInformationNotWritten`, `...FlushAsyncWritesQueue`; `PipeConnectionLogsStateChangesOnly`, `PipeRecordingSessionLogsStartStopAndFailure`, `PipeRecordingFilesLogsFileName`, `PipeReplayRunnerLogsFinishAndProblem`; `CompanionLogTemplates` | `LogFileNamesTests.cs`, `LogLineTests.cs`, `LogFileLoggerProviderTests.cs`, `PipeConnectionTests.cs`, `PipeRecordingSessionTests.cs`, `PipeRecordingFilesTests.cs`, `PipeReplayRunnerTests.cs`, `CompanionLogTests.cs` |
| AC9 | manual: the shell creates the log provider only after `SingleInstanceGuard.TryAcquire` succeeded (code read by the Reviewer; second start checked in the manual run) | — (shell, record 0004) |
| AC10 | manual run on Windows by the maintainer (steps in *Approach*); `bash .github/scripts/release-check.sh companion 0.0.0 <scratch>` run by the Dev | — |

Data rows the Tester writes for `LogFileNamesTryParseAcceptsOnlyExactForms`: accepted `companion-20261102.log`,
`companion-20261102-2.log`, `companion-20261102-9999.log`, `COMPANION-20261102.LOG`; refused `companion-20261102-1.log`,
`companion-20261102-02.log`, `companion-20261102-10000.log`, `companion-20261332.log`, `companion-2026110.log`,
`companion-20261102.log.bak`, `companion-20261102.logx`, `companion-20261102_001.log`, `xcompanion-20261102.log`,
`companion-20261102 .log`, `recording-20261102-182603Z.jsonl`, `companion-20261102.log:stream`, `..\companion-20261102.log`.

Rows for `LogLineSanitizeReplacesControlAndFormatCharacters`, each input a C# escape the test asserts still contains its
backslash form before the call: U+0000, U+0009, U+000A, U+000D, U+001B, U+007F, U+0085, U+200B, U+200E, U+202E, U+2066,
U+2028, U+2029, U+FEFF, a lone surrogate U+D800 — each becomes `?`; a valid surrogate pair (U+1F600) and `ä` stay.

## Approach

**Ticks in the live connection.** `PipeConnection.RunSessionAsync` wraps its `PipeMessageReader` in a
`PipeTickAssembler` on the same `TimeProvider`, with a read observer that does what the loop body does today (record,
`PipeStatusRule`, receiving time) and remembers the session-ending status. The loop requests items until
`EndOfStream`; each `Tick` item sets `LastTick` and raises `TickReceived` (a throwing handler is swallowed like
`StatusChanged`). A non-cancellation exception from `ReadAsync` ends the session as read failed, as today. The
assembler is disposed with the session. `LastTick` is kept across connections. `PipeReplayRunner` does the same with
its existing loop. State logging sits in `SetStatus`, so only changes are logged (E14).

**Recording session.** One `PipeRecordingSession` exists in every live run (Normal and Record), starts `Off`;
`--record` calls `Start()` at once. Rules, all under the session lock:
- `Start()`: from `Off` or `Failed` to `Recording`, new generation, log `Recording started`; in `Recording` nothing.
- `Stop()`: from `Recording` to `Off`, log `Recording stopped`; each open connection's active recorder is completed in
  the background (kept for `StopAsync`); in `Off`/`Failed` nothing. `Toggle()`: `Stop()` when `Recording`, else `Start()`.
- `BeginConnection()` returns a `PipeRecordingConnection` always (also when off); `EndConnection` completes its
  active recorder in the background.
- `PipeRecordingConnection.Record(read, time)`: a read with a frame whose first byte is `0` becomes the connection's
  last `Version` read (also while off). When the session is `Recording` and the connection has no recorder of the
  current generation and the read has a frame: create the recorder; if a last `Version` read exists and this frame is
  not itself type `0`, record that `Version` read first with `time`; then record the read.
- A recorder's `Failed`: when it is the active recorder of the current generation, the state becomes `Failed` (log
  warning `Recording failed`) and the connection drops it; otherwise only log `A stopped recording failed while
  writing its remaining frames`.
- `StopAsync(timeout)` completes active and stopped recorders together within the timeout, as today.
- `StateChanged` is raised on every state change (now also `Off`↔`Recording`).

`PipeRecordingFiles` with a logger logs `Recording file {FileName} created` (name only) after the `FileStream` opens.

**Tray model (Core).** `TraySnapshot` holds status text, look, last tick, `CanRecord`, `IsRecording`. Looks:
`WaitingForGame`, `Connected`, `PipeBusy` → Waiting; `Receiving` → Receiving; `ProtocolMismatch`, `Error` → Problem;
replay: finished without problem → Waiting, problem → Problem, else by state as live; invalid command line → Problem.
Status texts are today's tooltip texts without `Censio Companion: ` (the existing `FormatToolTip` overloads and
`FormatReplayToolTip` stay and are built on them). `FormatLastTick`: `Last tick: none` without a tick; else the UTC
time converted with `clock.LocalTimeZone`, shown `HH:mm:ss` when its local date equals the local date of
`clock.GetUtcNow()`, otherwise `yyyy-MM-dd HH:mm:ss`; a UTC time less than one day after `DateTimeOffset.MinValue` or
before `MaxValue` is shown `yyyy-MM-dd HH:mm:ssZ` in UTC without conversion; invariant culture; then `, 1 island` or
`, <n> islands`. Tooltip layout: line 1 `Censio Companion: <status text>`; with a last tick a `\n` and the
`FormatLastTick` text. Menu (`TrayMenu.Create`): `Status: <status text>` (disabled), last-tick text (disabled),
separator, `Start recording`/`Stop recording` (enabled iff `CanRecord`; `Stop` iff `IsRecording`), `Open recordings
folder`, `Open log folder`, separator, `Version <version>` (disabled), separator, `Exit`.

**Folders.** `FolderOpener.Open(folder, displayName)`: `Directory.CreateDirectory(folder)`, then `launch(folder)`;
any exception → warning `Could not open the {Folder} folder` with the exception (`{Folder}` is `recordings` or `log`).
The shell's launch starts `<Windows folder>\explorer.exe` (full path from `Environment.SpecialFolder.Windows`) with the
folder as a single `ArgumentList` item, `UseShellExecute = false`.

**Log writer.** `LogFileLoggerProvider(folder, timeProvider)`: `IsEnabled` for Information and above; `Log` renders the
message (`formatter`), builds the line with `LogLine.Format` (local time from `timeProvider.LocalTimeZone`,
`yyyy-MM-ddTHH:mm:ss.fffzzz`, level `INF`/`WRN`/`ERR`/`CRT`, message), and enqueues it without waiting (at most 4,096
lines queued; more are dropped). One background writer per provider: per line, it picks the file — today's (by the
line's local date) highest-numbered existing file if under 10 MiB with the line, else the next name (`-2`, `-3`, ...
up to 9999; beyond, the line is dropped); opens it append with `FileShare.Read`, writes, flushes; after opening a new
file it lists the folder's top-level files, keeps names `LogFileNames.TryParse` accepts, and deletes all but the 10
newest by (date, number), never the open one; a failing delete is ignored. Any I/O failure drops that line and closes
the file; the next line tries again. `FlushAsync(timeout)` waits for the queue or the timeout. `LogLine.Sanitize`:
every char of category `Control`, `Format`, `LineSeparator`, `ParagraphSeparator` or an unpaired surrogate becomes
`?`, then the text is cut to `MaxMessageLength` (2,048) with `…` appended when cut; exception text
(`exception.ToString()`, cut to 16,384) is split at `\n`, `\r` dropped, each line sanitized and written as its own
line indented by two spaces. Log lines (exact templates in `CompanionLog`): `Censio Companion {Version} started in
mode {Mode}`, `Connection state {State}, error kind {ErrorKind}, Windows error code {ErrorCode}, received version
{ReceivedVersion}`, `Recording started`, `Recording stopped`, `Recording file {FileName} created`, `Recording failed`,
`A stopped recording failed while writing its remaining frames`, `Replay of {FileName} started`, `Replay finished`,
`Replay error: {Reason}`, `Could not open the {Folder} folder`, `A background task failed`, `Exit`.

**Shell.** `App.OnStartup`: after the guard, create the provider for `CompanionFolders.Logs(localAppData)` and one
logger, log the start, build `PipeRecordingSession(files.Create, logger)` (files with the logger) for Normal and
Record, `Start()` for Record. `TrayStatusUpdater<TraySnapshot>` reads the snapshot; `StatusChanged`, `StateChanged`
and `TickReceived` notify it; it sets tooltip and icon. `TrayIcon` rebuilds the menu on `ContextMenuStrip.Opening`
from `TrayMenu.Create(version, snapshot(), TimeProvider.System)` and dispatches the entry kinds to toggle, open
folders, exit. `TrayIconImages` draws three icons at `SystemInformation.SmallIconSize` with GDI+: Waiting a grey ring,
Receiving a green filled circle, Problem a red filled triangle; created once, disposed at exit (the `HICON` of
`GetHicon` released with `DestroyIcon` after cloning). `Run` logs a faulted task as `A background task failed`.
`OnExit`: log `Exit`, dispose the tray icon, cancel, `StopAsync(1 s)` waited at most 1 s, `FlushAsync(500 ms)` waited
at most 500 ms, dispose the provider. A replay logs `Replay of {FileName} started` with `Path.GetFileName(path)`.

**Manual run (AC10, maintainer, Windows x64 without .NET):** unzip the release-check ZIP, start; menu as S2 and light
in dark mode; second start ends, no second log line; game with `/pipe`, save loaded: tooltip two lines `Receiving` and
`Last tick`, green icon; `Start recording`, wait for two ticks, `Stop recording`; `--replay <file> --speed max`:
`Replay: Receiving`, then `Replay finished`; both folder entries open Explorer; log file present with the lines.

## Options considered

- Logging: Serilog, an own interface, JSON lines — rejected in record 0037.
- Mid-connection `Version` stamp: own receive time, or waiting for the next `Version` — rejected in record 0036
  (options 16, 17); a failure for the rest of the run — option 18.
- Icons: embedded `.ico` files (binary assets authored with an outside tool, unreviewable in the diff) and Windows'
  `SystemIcons.Information`/`Error` (read as Windows messages, not as a state) — rejected for icons drawn at start.
- Recording control: a new session object per start (the shell would rewire events and lose stopped recorders for
  `Exit`) — rejected for one session that resets.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| (root) | `Directory.Packages.props` | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 |
| Censio.Companion.Core | `Censio.Companion.Core.csproj` | package reference |
| Censio.Companion.Core | `PipeConnection`, `PipeReplayRunner`, `PipeRecordingSession`, `PipeRecordingFiles`, `TrayMenu`, `TrayMenuEntryKind` | changed |
| Censio.Companion.Core | `PipeRecordingConnection`, `PipeTickSummary`, `TraySnapshot`, `TrayIconLook`, `FolderOpener`, `CompanionFolders`, `CompanionLog`, `LogFileNames`, `LogLine`, `LogFileLoggerProvider`, `LogFileLogger` | new |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs`, `TrayIconImages.cs` (new) | wiring, icons, menu |
| tests | `Censio.Companion.Core.Tests.csproj` | link `..\Censio.Server.Tests\RecordingLogger.cs` |

## Signatures (for the Dev's skeleton)

Namespace `Censio.Companion.Core`; ILogger, LogLevel and ILoggerProvider from Microsoft.Extensions.Logging. Existing
constructors keep working and pass NullLogger.Instance. Properties and constants are written as Type.Member with their
type after them.

- `public readonly record struct PipeTickSummary`: `PipeTickSummary.ReceiveTime` (required init DateTimeOffset), `PipeTickSummary.IslandCount` (required init int), `public static PipeTickSummary From(PipeTick tick)`
- `public enum TrayIconLook { Waiting, Receiving, Problem }`
- sealed record `TraySnapshot`: `TraySnapshot.StatusText` (required string), `TraySnapshot.Look` (required TrayIconLook), `TraySnapshot.LastTick` (PipeTickSummary?), `TraySnapshot.CanRecord` (bool), `TraySnapshot.IsRecording` (bool)
- `public static TraySnapshot ForConnection(PipeConnectionStatus status, PipeRecordingState recording, PipeTickSummary? lastTick)`
- `public static TraySnapshot ForReplay(PipeReplayStatus status, PipeTickSummary? lastTick)`
- `public static TraySnapshot ForInvalidCommandLine()`
- Enum members `TrayMenuEntryKind.Status`, `TrayMenuEntryKind.LastTick`, `TrayMenuEntryKind.ToggleRecording`, `TrayMenuEntryKind.OpenRecordingsFolder`, `TrayMenuEntryKind.OpenLogFolder`
- Constants `TrayMenu.MaxToolTipLength` (int 127), `TrayMenu.StartRecordingText` (`Start recording`), `TrayMenu.StopRecordingText` (`Stop recording`), `TrayMenu.OpenRecordingsFolderText` (`Open recordings folder`), `TrayMenu.OpenLogFolderText` (`Open log folder`)
- `public static string FormatStatusText(PipeConnectionState state, PipeRecordingState recording)`
- `public static string FormatReplayStatusText(PipeReplayStatus status)`
- `public static string FormatReplayReason(PipeReplayProblem problem)`
- `public static string FormatLastTick(PipeTickSummary? tick, TimeProvider clock)`
- `public static string FormatSnapshotToolTip(TraySnapshot snapshot, TimeProvider clock)`
- `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, TimeProvider clock)` (replaces `Create(string version)`)
- `public PipeRecordingSession(Func<DateTimeOffset, Stream> openFile, ILogger logger)`
- `public void Start()`, `public void Stop()`, `public void Toggle()` on PipeRecordingSession
- `public PipeRecordingConnection BeginConnection()` (return type changed), `public void EndConnection(PipeRecordingConnection connection)`
- sealed class `PipeRecordingConnection` with `public void Record(PipeReadResult read, DateTimeOffset receiveTime)`
- `public PipeRecordingFiles(string folder, ILogger logger)`
- `public PipeConnection(IPipeConnector connector, TimeProvider timeProvider, PipeRecordingSession? recording, ILogger logger)`
- `PipeConnection.LastTick` and `PipeReplayRunner.LastTick` (PipeTickSummary?, get only)
- `PipeConnection.TickReceived` and `PipeReplayRunner.TickReceived` (event EventHandler<PipeTick>?)
- `public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider, ILogger logger)`
- `public static class CompanionFolders` with `public static string Logs(string localApplicationData)`
- sealed class `FolderOpener` with `public FolderOpener(Action<string> launch, ILogger logger)`
- `public void Open(string folder, string displayName)`
- static partial class `CompanionLog` with these source-generated methods (each public static partial void): `Started(ILogger logger, string version, CompanionMode mode)`, `ConnectionStateChanged(ILogger logger, PipeConnectionState state, PipeErrorKind? errorKind, int? errorCode, int? receivedVersion)`, `RecordingStarted(ILogger logger)`, `RecordingStopped(ILogger logger)`, `RecordingFileCreated(ILogger logger, string fileName)`, `RecordingFailed(ILogger logger)`, `StoppedRecordingFailed(ILogger logger)`, `ReplayStarted(ILogger logger, string fileName)`, `ReplayFinished(ILogger logger)`, `ReplayProblem(ILogger logger, string reason)`, `FolderOpenFailed(ILogger logger, string folder, Exception exception)`, `BackgroundTaskFailed(ILogger logger, Exception exception)`, `Exiting(ILogger logger)`
- `public static class LogFileNames` with `public static string Format(DateOnly date, int sequence)`, `public static bool TryParse(string fileName, out DateOnly date, out int sequence)`, `public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keep)`
- `public static class LogLine` with `LogLine.MaxMessageLength` (const int 2048), `LogLine.MaxExceptionLength` (const int 16384), `public static string Format(DateTimeOffset localTime, LogLevel level, string message, Exception? exception)`, `public static string Sanitize(string text, int maxLength)`
- sealed class `LogFileLoggerProvider` (an ILoggerProvider) with `LogFileLoggerProvider.MaxFileBytes` (const long 10485760), `LogFileLoggerProvider.MaxFiles` (const int 10), `LogFileLoggerProvider.MaxQueuedLines` (const int 4096), `public LogFileLoggerProvider(string folder, TimeProvider timeProvider)`, `public ILogger CreateLogger(string categoryName)`, `public Task FlushAsync(TimeSpan timeout)`, `public void Dispose()`
- internal sealed class `LogFileLogger` (an ILogger) with `internal LogFileLogger(LogFileLoggerProvider provider)`

Files the skeleton rewrites: `PipeRecordingSession.cs`, `PipeConnection.cs`, `PipeReplayRunner.cs`, `PipeRecordingFiles.cs`,
`TrayMenu.cs`, `TrayMenuEntryKind.cs` (members added; existing members kept unless named above).

## Test files

New: `tests/Censio.Companion.Core.Tests/` `PipeTickSummaryTests.cs`, `TraySnapshotTests.cs`, `FolderOpenerTests.cs`,
`CompanionFoldersTests.cs`, `CompanionLogTests.cs`, `LogFileNamesTests.cs`, `LogLineTests.cs`,
`LogFileLoggerProviderTests.cs`. Extended: `TrayMenuTests.cs`, `PipeRecordingSessionTests.cs`,
`PipeRecordingFilesTests.cs`, `PipeConnectionTests.cs`, `PipeReplayRunnerTests.cs`.

Existing call sites (Tester adapts): `PipeRecordingSessionTests.cs` — the start state is now `Off` (add `Start()`),
`BeginConnection` returns `PipeRecordingConnection` (record through it); `PipeConnectionTests.cs` — recording tests
pass a started session; `TrayMenuTests.cs` — `Create(string)` becomes `Create(version, snapshot, clock)`.
`PipeReplayRunnerTests.cs` and the other `PipeConnectionTests` keep their inputs; the Tester re-runs them and adapts
one only if the assembler's quiet-period timer changes a `FakeTimeProvider` step count.

Test doubles: `RecordingLogger` linked as a `Compile` item; `FakeTimeProvider.SetLocalTimeZone` for the local-time
cases; a temporary folder per test for the log provider. `.squad/project.md` *Test doubles* gets the link (Tester).

Code no unit test can cover: `src/Censio.Companion/App.xaml.cs`, `TrayIcon.cs`, `TrayIconImages.cs` — Windows shell
glue, excluded by construction and in `sonar.coverage.exclusions` (record 0004); verified by the manual run.

## Areas

- `companion` — tray, menu, toggle, folders, log (outline in spec, *Area documents*). `pipe` — live ticks, recording
  start mid-connection, failure reset.

## Documentation updates

Dev, once the code is green, against the code:

- `docs/areas/companion.md`: *Tray icon and menu* (looks, two-line tooltip, the new menu), *Recording* (toggle,
  `--record` as start state, failure retry, a stopped recording's failure only logged, the 1 s at `Exit` for all),
  new *Folders* and *Log* sections (path, names, 10 MiB and 10 files, pruning pattern, line layout, what is logged,
  sanitizing, user name in exception texts), *Command line* (menu sentence), *Limits and errors*, *Related decisions*
  (0037).
- `docs/areas/pipe.md`: scope sentence and *Order and end* (the live connection assembles ticks), *What is recorded*
  (exception: the prepended `Version` line), *Writing* (no "rest of the run"; a new start resets), *Recorder started in
  the middle of a connection* (now the rule), *Residual risks* (drop the #13 item).
- `docs/areas/README.md`: the companion row's scope (log, folders, recording toggle).
- `docs/ARCHITECTURE.md`: `Censio.Companion.Core` row (tray snapshot, logging), flow paragraph (live ticks; log, link
  0037).
- `README.md` and `docs/USER_GUIDE.md`, English and German: first start (menu), *Recording and replay* (menu toggle,
  lines 128/288 and 134/331), *Data and logs*, *Reporting problems* (log location; check it for the user name before
  sharing).
- `.squad/project.md` *Security areas*: *File paths* adds the log folder, its pruning pattern and the Explorer launch;
  *Logging of external data* adds `LogLine` sanitizing and caps.

## Architecture check

No guarantee is recorded in `.squad/project.md`. The Companion still sends nothing anywhere (no outbound call); pipe
and recording paths stay fixed; the tray never waits on pipe, disk or log; `Exit` stays within 2 s (1 s recording + 0.5
s log). The shell stays thin (record 0004): all decisions above are in Core.

## Security considerations

- [x] Limits: log message 2,048 and exception 16,384 UTF-16 chars, applied after rendering and before sanitizing
  (replacement is 1:1, so the size does not grow); 4,096 queued lines; 10 MiB per file checked in UTF-8 bytes before the
  write; 10 files. Recording limits unchanged; the kept `Version` read is one frame of at most 1 MiB per connection.
- [x] Exceptions reaching user-visible text: none — tray texts come from enums and counts; exceptions reach only the log
  (as given, documented).
- Deletion: only top-level files whose whole name passes `LogFileNames.TryParse` (rows above); a link is deleted as a
  link. Explorer is started by full path with one argument; no shell execution of a path.

## Decision records

- `docs/decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md` (Proposed, new)
- `docs/decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md` (unreleased, extended: options
  15-18, toggle context)

## Out of scope / follow-ups

See spec, *Out of scope*. No follow-up issue.
