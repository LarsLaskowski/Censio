# Plan: Tray app with connection status and recording control

Source: Issue #13 | [spec.md](spec.md)
Status: Revised (1)
Tier: security — the change creates two guards: the log writer escapes external text it writes (control and format characters, length caps) and the log pruning confines deletion to an exact name pattern in a fixed folder; it also adds a dependency.
Security areas touched: File paths for recordings (the new log folder `%LOCALAPPDATA%\Censio\Logs`, its pruning, the Explorer launch of both folders, `PipeRecordingFiles`), Logging of external data (the new log writer `LogLine`, `LogFileLoggerProvider`; replay file name and exception texts), dependency (`Microsoft.Extensions.Logging.Abstractions` 10.0.14 in `Directory.Packages.props`, with its transitive `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14), build configuration (`src/Censio.Companion.Core/Censio.Companion.Core.csproj`)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Issue: "Tray icon reflects the connection states" — today it does not; the icon is fixed | `src/Censio.Companion/TrayIcon.cs:78` `SystemIcons.Application` | refuted |
| 2 | The live connection assembles no ticks; only the replay does (so `docs/ARCHITECTURE.md` lines 45-46 hold only for the replay) | `PipeConnection.cs:256`; `PipeReplayRunner.cs` | confirmed |
| 3 | The assembler's read observer gets the same stamp the tick uses, and an exception of the source surfaces from `ReadAsync` | `PipeTickAssembler.ReadStampedAsync` and `Feed` (awaits the faulted `_pending`) | confirmed |
| 4 | `NotifyIcon.Text` throws `ArgumentOutOfRangeException` above 127 characters | `dotnet/winforms` `release/10.0` `NotifyIcon.cs:19` `MaxTextSize = 127`, `:242` | confirmed |
| 5 | `TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)` clamps an overflow to `DateTimeOffset.MinValue`/`MaxValue`, so a time at the range edge would show a wrong value | `dotnet/runtime` `release/10.0` `TimeZoneInfo.cs:705-720` | confirmed |
| 6 | Longest tooltip: `Censio Companion: Replay error: invalid line 9223372036854775807` (64) + newline + `Last tick: 0001-01-01 00:00:00Z, 256 islands` (44) = 109 < 127 | `PipeReplayProblem.LineNumber` is `long?` (`src/Censio.Pipe/PipeReplayProblem.cs:18`); a tick has at most 256 islands (`pipe.md`, *Limits*) | confirmed |
| 7 | A `\n` in `NotifyIcon.Text` shows a two-line tooltip | to verify: maintainer's manual run on Windows (AC10), before merge | unverified |
| 8 | `ContextMenuStrip` stays light with Windows in dark mode (WinForms color mode defaults to classic) | to verify: maintainer's manual run on Windows in dark mode (AC10) | unverified |
| 9 | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 restores and its `[LoggerMessage]` source generator builds clean under Reihitsu and Sonar | to verify: Dev, the G1 skeleton build and *Analyzer gate* (the package is in the local NuGet cache) | unverified |
| 10 | The recorder names and heads its file by the time of its first queued entry | `PipeRecorder.WriteAsync` (`_openFile(entry.Time)`, `FormatHeader(entry.Time)`) | confirmed |
| 11 | A session's failure is one-way today | `PipeRecordingSession.SetFailed`, `BeginConnection` returns `null` when failed | confirmed |
| 12 | A `Version` frame is the frame whose type byte is `0`, also when its version is not 2 | `pipe.md`, *Message types*, *Version gate* | confirmed |
| 13 | No record is released (no `v*` tag), so 0036 is edited in place | `git ls-remote --tags origin` returns nothing | confirmed |
| 14 | The release ZIP check publishes the Companion and checks it holds one file | `.github/scripts/release-check.sh companion` | confirmed |
| 15 | `Directory.Packages.props` is an input of the server image restore | `.squad/stack.md`, *Image build check* | confirmed |
| 16 | `explorer.exe <folder>` opens the folder (Explorer splits its command line at commas: a profile path with a comma would open another folder) | to verify: maintainer's manual run on Windows (AC10) | unverified |
| 18 | `Microsoft.Extensions.Logging.Abstractions` 10.0.14 for `net10.0` depends only on `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 | its `.nuspec` in the local NuGet cache | confirmed |
| 19 | Deleting a symbolic link deletes the link, not its target; `Directory.EnumerateFiles` returns no directories, so no junction | `DeleteFileW`, Remarks (Microsoft Learn: "If the path points to a symbolic link, the symbolic link is deleted, not the target"); `File.Delete` calls it | confirmed |
| 22 | `new Icon(Stream)` accepts an ICO whose one entry is PNG-compressed | to verify: maintainer's manual run on Windows (AC10): all three looks show | unverified |
| 23 | `ContextMenuStrip.Opening` arrives with `e.Cancel = true` when the strip has no items | Devil's Advocate objection 3 (WinForms `ToolStripDropDown` source) | confirmed |
| 17 | Existing call sites of changed members: `PipeRecordingSessionTests.cs` (27), `PipeConnectionTests.cs` (11), `PipeReplayRunnerTests.cs` (4), `TrayMenuTests.cs` (4) | `grep` of `new PipeRecordingSession`, `BeginConnection`, `EndConnection`, `new PipeConnection(`, `new PipeReplayRunner(`, `TrayMenu.Create` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeTickSummaryFromTickCountsIslands`; `TrayMenuFormatLastTickTodayShowsTimeOnly`, `...OtherDayShowsDate`, `...NearMinValueShowsUtc`, `...NearMaxValueShowsUtc`, `...OneIslandSingular`, `...NoneBeforeFirstTick`; `TrayMenuFormatToolTipWithoutTickHasOneLine`, `...WithTickHasTwoLines`, `...LongestFitsLimit` (replay error at line `long.MaxValue` plus `Last tick: 0001-01-01 00:00:00Z, 256 islands`); `TrayMenuUntilNextLocalMidnightNormalDay`, `...SkipsInvalidMidnight` (custom zone whose clocks jump at 00:00), `...AtLeastOneSecond`; `PipeConnectionTickClosedSetsLastTickAndRaisesEvent`, `PipeConnectionLastTickSurvivesConnectionEnd`, `PipeConnectionNoTickBeforeClose` | `TrayMenuTests.cs`, `PipeTickSummaryTests.cs`, `PipeConnectionTests.cs` |
| AC2 | `TrayMenuCreateListsEntriesInOrder`, `TrayMenuCreateRecordingShowsStop`, `TrayMenuCreateReplayGreysToggle`, `TrayMenuCreateInvalidGreysToggle`, `TrayMenuCreateStatusEntryShowsStatusText` | `TrayMenuTests.cs` |
| AC3 | `TraySnapshotForConnectionMapsLook` (one data row per state), `TraySnapshotForReplayMapsLook` (connected, receiving, mismatch, finished, error), `TraySnapshotForInvalidCommandLineIsProblem` | `TraySnapshotTests.cs` |
| AC4 | `PipeRecordingSessionStartsOff`, `...StartWhileOffRecords`, `...StopEndsRecorderAndClosesFile`, `...StartAfterStopWritesNewFile`, `...StartWithoutConnectionCreatesFileAtFirstFrame`, `...StartAfterFailureRecordsAgain`, `...StoppedRecorderFailureKeepsState`, `...ToggleSwitches`, `...StateChangedOnEveryChange` | `PipeRecordingSessionTests.cs` |
| AC5 | `PipeConnectionRecordingStartedMidConnectionWritesVersionFirst` (line 2 is the type-0 frame, its time equals line 3's), `...ReplaysAsReceiving` (file through `PipeReplayRunner`: `Receiving`, then finished), `...ReplayedTicksEqualLiveTicks` (ticks opened after the first frame), `...InMismatchReplaysAsMismatch`, `...BeforeVersionWritesNoExtraLine` | `PipeConnectionTests.cs` |
| AC6 | `PipeRecordingSessionStopAsyncCompletesActiveAndStoppedRecorders`, `PipeRecordingSessionStopAsyncGivesUpAfterTimeout` | `PipeRecordingSessionTests.cs` |
| AC7 | `FolderOpenerOpenCreatesMissingFolderAndLaunches`, `FolderOpenerOpenLaunchFailureLogsWarning`, `FolderOpenerOpenCreateFailureLogsWarningWithoutLaunch`, `FolderOpenerOpenRelativeFolderLogsWarningWithoutLaunch`; `CompanionFoldersLogsIsUnderCensio` | `FolderOpenerTests.cs`, `CompanionFoldersTests.cs` |
| AC8 | `LogFileNamesFormatFirstAndContinuation`, `LogFileNamesTryParseAcceptsOnlyExactForms` (data rows below), `LogFileNamesSelectForDeletionKeepsNewestByDateThenNumber`; `LogLineFormatLayout`, `LogLineSanitizeReplacesControlAndFormatCharacters` (rows below), `LogLineSanitizeCutsLongMessage`, `LogLineFormatExceptionKeepsLineBreaksOnly`; `LogFileLoggerProviderWritesLineToDailyFile`, `...RollsPastMaxBytes`, `...NewDayStartsNewFile`, `...ContinuesTodaysFileAfterRestart`, `...PrunesToMaxFilesAndKeepsOtherFiles`, `...UnwritableFolderDropsLines`, `...RelativeFolderWritesNothing`, `...FullQueueDropsLines` (by characters), `...BelowInformationNotWritten`, `...FlushAsyncWritesQueue`; `PipeConnectionLogsStateChangesOnly`, `PipeRecordingSessionLogsStartStopAndFailure`, `PipeRecordingFilesLogsFileName`, `PipeReplayRunnerLogsFinishAndProblem`; `CompanionLogTemplates` | `LogFileNamesTests.cs`, `LogLineTests.cs`, `LogFileLoggerProviderTests.cs`, `PipeConnectionTests.cs`, `PipeRecordingSessionTests.cs`, `PipeRecordingFilesTests.cs`, `PipeReplayRunnerTests.cs`, `CompanionLogTests.cs` |
| AC9 | manual: the shell creates the log provider only after `SingleInstanceGuard.TryAcquire` succeeded (code read by the Reviewer; second start checked in the manual run) | — (shell, record 0004) |
| AC10 | manual run on Windows by the maintainer (steps in *Approach*); `bash .github/scripts/release-check.sh companion 0.0.0 <scratch>` run by the Dev | — |

Data rows the Tester writes for `LogFileNamesTryParseAcceptsOnlyExactForms`: accepted `companion-20261102.log`,
`companion-20261102-2.log`, `companion-20261102-9999.log`, `COMPANION-20261102.LOG`; refused `companion-20261102-1.log`,
`companion-20261102-02.log`, `companion-20261102-10000.log`, `companion-20261332.log`, `companion-2026110.log`,
`companion-20261102.log.bak`, `companion-20261102.logx`, `companion-20261102_001.log`, `xcompanion-20261102.log`,
`companion-20261102 .log`, `recording-20261102-182603Z.jsonl`, `companion-20261102.log:stream`, `..\companion-20261102.log`,
`companion-20261102-+2.log`, `companion-20261102- 2.log`, `companion-20261102-2 .log`, `companion-20261102-2` + U+0000 +
`.log`, `companion-20261102-` + U+0662 + `.log`, `companion-` + U+FF12 U+FF10 U+FF12 U+FF16 + `1102.log`, `compan` + U+0131 +
`on-20261102.log` (dotless i, equal to `I` under `OrdinalIgnoreCase`). The rule: the name is ASCII only, date and number
are parsed from ASCII digits `0`-`9` only, and the name equals `LogFileNames.Format(date, sequence)` under
`OrdinalIgnoreCase` (round trip).

Rows for `LogLineSanitizeReplacesControlAndFormatCharacters`, each input a C# escape the test asserts still contains its
backslash form before the call: U+0000, U+0009, U+000A, U+000D, U+001B, U+007F, U+0085, U+200B, U+200E, U+202E, U+2066,
U+2028, U+2029, U+FEFF, a lone surrogate U+D800 and U+DC00 — each becomes one `?`; U+E0001, U+E0041 and U+1D173 (each a
surrogate pair) become one `?` each; a valid pair U+1F600 and `ä` stay. A cut never splits a pair.

## Approach

**Ticks in the live connection.** `PipeConnection.RunSessionAsync` wraps its `PipeMessageReader` in a
`PipeTickAssembler` on the same `TimeProvider`, with a read observer that does what the loop body does today (record,
`PipeStatusRule`, receiving time) and remembers the session-ending status; the end status and `_receivingSince`
are touched only by the observer and read by the loop only after `EndOfStream` (the observer of the last read has run
before `Feed` enqueues it), because a quiet-period tick can return while the observer of a pending read runs. The loop
requests items until `EndOfStream`; each `Tick` item sets `LastTick` under `_statusLock` and raises `TickReceived` (a throwing handler is swallowed like
`StatusChanged`). A non-cancellation exception from `ReadAsync` ends the session as read failed, as today. The
assembler is disposed with the session. `LastTick` is kept across connections. `PipeReplayRunner` does the same with
its existing loop (its `LastTick` under its `_statusLock`). State logging sits in `SetStatus`, so only changes are logged (E14).

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
- `StateChanged` is raised on every state change (now also `Off`↔`Recording`), outside the lock, as `SetFailed` does.
- Stopped recorders still writing are not capped together: each holds up to 16 MiB of queue while its disk stalls,
  and only a user click adds one (accepted, *Security considerations*).

`PipeRecordingFiles` with a logger logs `Recording file {FileName} created` (name only) after the `FileStream` opens.

**Tray model (Core).** `TraySnapshot` holds status text, look, last tick, `CanRecord`, `IsRecording`. Looks:
`WaitingForGame`, `Connected`, `PipeBusy` → Waiting; `Receiving` → Receiving; `ProtocolMismatch`, `Error` → Problem;
replay: finished without problem → Waiting, problem → Problem, else by state as live; invalid command line → Problem.
Status texts are today's tooltip texts without `Censio Companion: ` (the existing `FormatToolTip` overloads and
`FormatReplayToolTip` stay and are built on them). `FormatLastTick`: `Last tick: none` without a tick; else the UTC
time converted with `clock.LocalTimeZone`, shown `HH:mm:ss` when its local date equals the local date of
`clock.GetUtcNow()`, otherwise `yyyy-MM-dd HH:mm:ss`; a UTC time less than one day after `DateTimeOffset.MinValue` or
before `MaxValue` is shown `yyyy-MM-dd HH:mm:ssZ` in UTC without conversion; invariant culture; then `, 1 island` or
`, <n> islands`. `UntilNextLocalMidnight(clock)`: the time to the next local 00:00 (an invalid local time there moves
it one hour later), at least 1 second; `FormatSnapshotToolTip` stays a pure function. Tooltip layout: line 1 `Censio Companion: <status text>`; with a last tick a `\n` and the
`FormatLastTick` text. Menu (`TrayMenu.Create`): `Status: <status text>` (disabled), last-tick text (disabled),
separator, `Start recording`/`Stop recording` (enabled iff `CanRecord`; `Stop` iff `IsRecording`), `Open recordings
folder`, `Open log folder`, separator, `Version <version>` (disabled), separator, `Exit`.

**Folders.** `FolderOpener.Open(folder, displayName)`: a folder that is not `Path.IsPathFullyQualified` logs the warning
(without exception) and launches nothing; otherwise `Directory.CreateDirectory(folder)`, then `launch(folder)`; any exception → warning `Could not open the {Folder} folder` with the exception (`{Folder}` is `recordings` or `log`).
The shell's launch starts `<Windows folder>\explorer.exe` (full path from `Environment.SpecialFolder.Windows`) with the
folder as a single `ArgumentList` item, `UseShellExecute = false`, and disposes the `Process` at once.

**Log writer.** `LogFileLoggerProvider(folder, timeProvider)`: `IsEnabled` for Information and above; `Log` renders the
message (`formatter`), builds the line with `LogLine.Format` (local time from `timeProvider.LocalTimeZone`,
`yyyy-MM-ddTHH:mm:ss.fffzzz`, level `INF`/`WRN`/`ERR`/`CRT`, message), and enqueues it without waiting (at most
1,048,576 characters queued, counted per line; a line that does not fit is dropped). A folder that is not
`Path.IsPathFullyQualified` writes, creates and deletes nothing: every line is dropped. One background writer per provider: per line, it picks the file — today's (by the
line's local date) highest-numbered existing file if under 10 MiB with the line, else the next name (`-2`, `-3`, ...
up to 9999; beyond, the line is dropped); opens it append with `FileShare.Read`, writes, flushes; after opening a new
file it lists the folder's top-level files, keeps names `LogFileNames.TryParse` accepts, and deletes all but the 10
newest by (date, number), never the open one; a failing delete is ignored. Any I/O failure drops that line and closes
the file; the next line tries again. `FlushAsync(timeout)` waits for the queue or the timeout. `LogLine.Sanitize`
walks the text by code point (`Rune.DecodeFromUtf16`): an unpaired surrogate becomes one `?`, a code point of
`Rune.GetUnicodeCategory` `Control`, `Format`, `LineSeparator` or `ParagraphSeparator` becomes one `?`, others are kept;
then the text is cut at a code-point boundary to `MaxMessageLength` (2,048) with `…` appended when cut; exception text
(`exception.ToString()`, cut to 16,384) is split at `\n`, `\r` dropped, each line sanitized and written as its own
line indented by two spaces. Log lines (exact templates in `CompanionLog`): `Censio Companion {Version} started in
mode {Mode}`, `Connection state {State}, error kind {ErrorKind}, Windows error code {ErrorCode}, received version
{ReceivedVersion}`, `Recording started`, `Recording stopped`, `Recording file {FileName} created`, `Recording failed`,
`A stopped recording failed while writing its remaining frames`, `Replay of {FileName} started`, `Replay finished`,
`Replay error: {Reason}`, `Could not open the {Folder} folder`, `A background task failed`, `Exit`.

**Shell.** `App.OnStartup`: after the guard, create the provider for `CompanionFolders.Logs(localAppData)` and one
logger, log the start, build `PipeRecordingSession(files.Create, logger)` (files with the logger) for Normal and
Record, `Start()` for Record. `TrayStatusUpdater<TraySnapshot>` reads the snapshot; `StatusChanged`, `StateChanged`
and `TickReceived` notify it, and so does a one-shot timer at `UntilNextLocalMidnight`, re-armed after each run; it
sets tooltip and icon. `TrayIcon` builds the entries in its constructor, rebuilds them on `ContextMenuStrip.Opening`
from `TrayMenu.Create(version, snapshot(), TimeProvider.System)`, disposes the items it replaces and sets
`e.Cancel = false`; it dispatches the entry kinds to toggle, folders and exit. The folder entries call
`FolderOpener.Open` through `Run` (off the UI thread, faults logged). `TrayIconImages` draws three looks at
`SystemInformation.SmallIconSize` with GDI+ (Waiting a grey ring, Receiving a green filled circle, Problem a red filled
triangle) into a `Bitmap`, saves it as PNG into a `MemoryStream` behind a 22-byte ICO header (one entry) and creates
`new Icon(stream)`: no handle, no P/Invoke, no project change; created once, disposed at exit. `Run` logs a faulted task as `A background task failed`.
`OnExit`: log `Exit`, dispose the tray icon, cancel, `StopAsync(1 s)` waited at most 1 s, `FlushAsync(500 ms)` waited
at most 500 ms, dispose the provider. A replay logs `Replay of {FileName} started` with `Path.GetFileName(path)`.

**Manual run (AC10, maintainer, Windows x64 without .NET):** unzip the release-check ZIP, start; menu as S2 and light
in dark mode; second start ends, no second log line; game with `/pipe`, save loaded: tooltip two lines `Receiving` and
`Last tick`, green icon; `Start recording`, wait for two ticks, `Stop recording`; `--replay <file> --speed max`:
`Replay: Receiving`, then `Replay finished`; both folder entries open Explorer (a profile path with a comma is noted, fact 16); the three looks show; the tooltip
gets the date after local midnight; log file present with the lines.

## Options considered

- Logging: record 0037. Mid-connection recording and failure reset: record 0036, options 15-18.
- Icons: a `GetHicon` handle released with `DestroyIcon` (needs the first P/Invoke and a project change, objection 2);
  embedded `.ico` files (binary assets authored with an outside tool, unreviewable in the diff) and Windows'
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
- `public static TimeSpan UntilNextLocalMidnight(TimeProvider clock)`
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
- static partial class `CompanionLog` with these source-generated methods (each public static partial void): `Started(ILogger logger, string version, CompanionMode mode)`, `ConnectionStateChanged(ILogger logger, PipeConnectionState state, PipeErrorKind? errorKind, int? errorCode, int? receivedVersion)`, `RecordingStarted(ILogger logger)`, `RecordingStopped(ILogger logger)`, `RecordingFileCreated(ILogger logger, string fileName)`, `RecordingFailed(ILogger logger)`, `StoppedRecordingFailed(ILogger logger)`, `ReplayStarted(ILogger logger, string fileName)`, `ReplayFinished(ILogger logger)`, `ReplayProblem(ILogger logger, string reason)`, `FolderOpenFailed(ILogger logger, string folder, Exception? exception)` (null for a relative folder, N5), `BackgroundTaskFailed(ILogger logger, Exception exception)`, `Exiting(ILogger logger)`
- `public static class LogFileNames` with `public static string Format(DateOnly date, int sequence)`, `public static bool TryParse(string fileName, out DateOnly date, out int sequence)`, `public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keep)`
- `public static class LogLine` with `LogLine.MaxMessageLength` (const int 2048), `LogLine.MaxExceptionLength` (const int 16384), `public static string Format(DateTimeOffset localTime, LogLevel level, string message, Exception? exception)`, `public static string Sanitize(string text, int maxLength)`
- sealed class `LogFileLoggerProvider` (an ILoggerProvider) with `LogFileLoggerProvider.MaxFileBytes` (const long 10485760), `LogFileLoggerProvider.MaxFiles` (const int 10), `LogFileLoggerProvider.MaxQueuedChars` (const int 1048576), `public LogFileLoggerProvider(string folder, TimeProvider timeProvider)`, `public ILogger CreateLogger(string categoryName)`, `public Task FlushAsync(TimeSpan timeout)`, `public void Dispose()`
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

- `companion` and `pipe`, as spec *Area documents* and *Documentation updates* say.

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

No guarantee is recorded in `.squad/project.md`. No outbound call; paths stay fixed; `Exit` stays within 2 s (1 s +
0.5 s); the shell stays thin (record 0004).

## Security considerations

- [x] Limits: log message 2,048 and exception 16,384 UTF-16 chars, applied after rendering; sanitizing never grows the
  text (a pair becomes one `?`); one line with an exception is at most about 51,000 chars (16,384 one-char lines with
  their 2-space indent); the queue holds at most 1,048,576 chars (2 MiB) whatever the lines; 10 MiB per file checked in
  UTF-8 bytes before the write; 10 files. Recording: 16 MiB queue per recorder, as before; stopped recorders that still
  write add 16 MiB each, one per user click on a stalled disk — accepted as user-driven, not capped in total. The kept
  `Version` read is one frame of at most 1 MiB per connection.
- [x] Exceptions reaching user-visible text: none — tray texts come from enums and counts; exceptions reach only the log
  (as given, documented).
- Deletion: only top-level files of a fully qualified log folder whose whole name passes `LogFileNames.TryParse`
  (rows above, round trip); a symbolic link is deleted as a link (fact 19). Explorer is started by full path with one
  argument and only for a fully qualified folder; no shell execution of a path.

## Decision records

- `docs/decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md` (Proposed, new)
- `docs/decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md` (unreleased, extended: options
  15-18, toggle context)

## Challenge

Devil's Advocate, 6 minor objections, all accepted: (1) longest tooltip — fact 6 and the test use line
`long.MaxValue`; (2) no P/Invoke — PNG ICO through `new Icon(stream)` (fact 22); (3) `Opening` cancel — entries built in
the constructor, `e.Cancel = false`, replaced items disposed; (4) folder entries off the UI thread through `Run`; (5)
midnight — `UntilNextLocalMidnight` arms a shell timer; (6) thread safety — `LastTick` under `_statusLock`, end status
and `_receivingSince` observer-only, `StateChanged` outside the lock (*Approach*).

## Security review

Round 1, all answered in the body: B1 code-point sanitizing, B2 ASCII round-trip names (plus the dotless i), B3
relative folder (two tests, spec E10/E11), N1 queue in characters, N2 accepted as user-driven, N3 fact 19, N4 comma noted
and transitive package named. N5 (round 2): `FolderOpenFailed` takes `Exception?`.

## Out of scope / follow-ups

See spec, *Out of scope*. No follow-up issue.

## Security notes

Non-blocking finding of Security's plan review, round 2, verbatim.

**N5. A null exception would go into a non-nullable parameter.** For a relative folder, `FolderOpener` logs the warning "without exception", but the planned signature is `FolderOpenFailed(ILogger logger, string folder, Exception exception)`. Make the parameter `Exception?` in the skeleton, or the call passes null into a non-nullable parameter and gets a nullable warning.
