# Plan: Recorder and replay of the statistics pipe

Source: Issue #12 | [spec.md](spec.md)
Status: Draft
Tier: standard — new behavior (recording files, replay from a file) that creates, changes or removes no security control: the recording path is fixed and built from no input, and the replay file's line bounds are input handling.
Security areas touched: File paths for recordings (`PipeRecordingFiles`, `%LOCALAPPDATA%\Censio\Recordings`); Parsing of external input (a recording file read by `PipeReplayStream`); Logging of external data (unchanged: nothing is logged, tooltips carry no file content)

## Problem / root cause

Feature: see [spec.md](spec.md) (S1–S8, E1–E11, AC1–AC10). Spec defects fixed while planning: E6 gains the reason
`read failed` (an I/O error after the file was opened had no reason), a field given twice is `invalid line <n>`;
E10 says a line a stalled disk is still writing at the 1-second limit may be cut off (the process ends, as in E2);
E11 accepts a UTF-8 byte order mark before the header and requires only `format` and `version` in it.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The frame reader allocates a fresh array per frame, so a frame can be kept without a copy | `src/Censio.Pipe/PipeFrameReader.cs`, `ReadCoreAsync`: `var frame = new byte[length]` | confirmed |
| 2 | The tick assembler reads the clock once per result, after the source's read returned | `PipeTickAssembler.ReadStampedAsync`: `return (result, _timeProvider.GetUtcNow())` | confirmed |
| 3 | `PipeTickBuilder.Add` closes a tick by the quiet period before it adds a frame stamped at or after the deadline, so a replay gives the same ticks whether the quiet timer or the read completes first | `PipeTickBuilder.Add` calls `CloseIfQuiet(receiveTime)` first; `PipeTickAssembler.Step` calls `CloseIfQuiet(now)` when `now >= deadline` | confirmed |
| 4 | The live Companion reads messages without a tick assembler | `PipeConnection.RunSessionAsync` loops on `PipeMessageReader.ReadAsync` and only sets the status | confirmed |
| 5 | Skipped frames (empty, unknown type, malformed, protocol mismatch) are built in `PipeMessageReader.ReadAsync` or `PipeMessageParser.Parse` from a whole frame; stream-ending errors and the end carry none | `PipeMessageReader.ReadAsync`; `PipeFrameReader.Fail` | confirmed |
| 6 | No release tag exists, so every record is unreleased | `git ls-remote --tags origin` printed nothing | confirmed |
| 7 | The Windows shell is outside the coverage measure: it has no test project, so its lines are absent from the local report, and CI excludes `src/Censio.Companion/**` | `.squad/tools/coverage-check.py` `new_code_coverage` counts only lines present in the report; `.github/workflows/ci.yml` line 71; record 0004 | confirmed |
| 8 | The server image does not build `Censio.Pipe` or `Censio.Companion.Core` | `src/Censio.Server/Censio.Server.csproj` references only Contracts and Server.Data | confirmed |
| 9 | Base64 of 1 MiB is 1,398,104 characters | 4 × ceil(1,048,576 / 3) | confirmed |
| 10 | `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` waits only through `TimeProvider.CreateTimer`, so a clock that fires its timers in `SetUtcNow` drives the assembler's quiet period | to verify: Dev reads `dotnet/runtime` `release/10.0` `Task.Delay` with `TimeProvider` before writing `PipeReplayClock`; Tester's `PipeReplayClockTests` and the AC1 quiet-period test confirm it | unverified |
| 11 | WPF raises `Application.Exit` (`OnExit`) after an uncancelled `SessionEnding` at sign-out and shutdown, so the recording flush in `OnExit` also runs then | to verify: Dev reads the WPF `Application` source (`release/10.0`); maintainer's manual test on Windows (sign-out with `--record`, file ends with a complete line) in step 9 | unverified |
| 12 | `NotifyIcon.Text` accepts up to 127 characters on .NET 10; the longest new tooltip (`Censio Companion: Replay error: invalid line 9223372036854775807`) has 64 | to verify: Dev reads `System.Windows.Forms` `NotifyIcon.Text` (`release/10.0`) | unverified |
| 13 | A full disk and a deleted folder surface from `FileStream` writes and flushes as `IOException` (access denied as `UnauthorizedAccessException`), the cases `ControlledWriteStream` models | to verify: Dev reads `dotnet/runtime` `release/10.0` `Win32Marshal.GetExceptionForWin32Error` (`ERROR_DISK_FULL`, `ERROR_ACCESS_DENIED`) before the Tester writes the double | unverified |
| 14 | The issue's claims: recording of raw frames with receive time as JSON lines with base64 payload, one file per session, replay at real, factor and maximum speed — buildable on the merged #9/#10 code | `src/Censio.Pipe` (reader, assembler on an injected clock); "session" read as connection (spec Q2) | confirmed |
| 15 | No real sample exists of the recording format: it is defined by this change; samples are produced by `PipeRecorder` in the tests, and real recordings come with #17 and are anonymised by #20 | spec *Out of scope*; record 0029 | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeReplayStreamRecordedRunReplaysSameItemsAtMaximumSpeed`, `PipeReplayStreamRecordedRunReplaysSameItemsAtFactorSpeed`, `PipeReplayStreamClockMovedInsideChunkedFrameKeepsReceiveTime`, `PipeReplayStreamQuietPeriodCloseReplaysAsQuietPeriod`, `PipeReplayStreamLastTickClosedByQuietPeriodReplaysAsStreamEnded`, `PipeReplayStreamSessionEventsAndVersionsReplayInOrder` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC1 | `PipeTickAssemblerReadObserverGetsEachResultWithItsStamp`, `PipeTickAssemblerThrowingReadObserverKeepsReading` | `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` |
| AC2 | `PipeMessageReaderResultCarriesWholeFrame` (message, empty, unknown type, malformed, protocol mismatch), `PipeMessageReaderEndAndStreamEndingErrorCarryNoFrame` | `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs` |
| AC2 | `PipeRecorderRecordsEachFrameOnceInOrder`, `PipeRecorderResultWithoutFrameIsNotRecorded`, `PipeRecorderCompleteWritesQueuedFramesAsCompleteLines`, `PipeRecorderFlushesEachLine` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC2 | `PipeConnectionRecordingEndInsideChunkedFrameRecordsOnlyWholeFrames`, `PipeConnectionRecordingStopInsideChunkedFrameRecordsOnlyWholeFrames`, `PipeConnectionRecordingStampsEachFrameOnce` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC3 | `PipeRecordingFormatHeaderHasFormatVersionAndStarted`, `PipeRecordingFormatFrameLineHasSevenDigitUtcTimeAndBase64`, `PipeRecordingFormatFileNameUsesStartSecondAndSuffix` | `tests/Censio.Pipe.Tests/PipeRecordingFormatTests.cs` |
| AC3 | `PipeRecorderOpensFileAtFirstFrameWithItsTime`, `PipeRecorderWritesHeaderBeforeFirstFrame` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC3 | `PipeRecordingFilesCreatesFolderAndFile`, `PipeRecordingFilesSameSecondGetsNextSuffix`, `PipeRecordingFilesNeverOverwrites` | `tests/Censio.Companion.Core.Tests/PipeRecordingFilesTests.cs` |
| AC4 | `PipeConnectionRecordingWritesOneFilePerConnection`, `PipeConnectionRecordingCreatesNoFileWithoutFrame`, `PipeConnectionWithoutRecordingCreatesNoRecorder` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC4 | `PipeRecordingSessionEndConnectionClosesFile` | `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` |
| AC5 | `PipeRecorderQueueOverLimitFailsAndKeepsQueuedFrames`, `PipeRecorderRecordDoesNotWaitForStalledWrite`, `PipeRecorderWriteFailureFailsAndKeepsCompleteLines`, `PipeRecorderOpenFailureFails`, `PipeRecorderCompleteCancelledStopsWaiting`, `PipeRecorderThrowingFailedHandlerIsIgnored` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC5 | `PipeRecordingSessionStateIsRecordingUntilFailure`, `PipeRecordingSessionFailureStopsRecordingForRestOfRun`, `PipeRecordingSessionStopReturnsWithinTimeoutOnStalledDisk`, `PipeRecordingSessionStopCompletesAllConnections` | `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` |
| AC5 | `PipeConnectionRecordingFailureKeepsStatesAndReads` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC5 | `TrayMenuFormatToolTipWithRecordingStateAppendsSuffix` (off, recording, failed per state) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC6 | `PipeReplayStreamSkippedFramesReplayAsSkipped`, `PipeReplayStreamFrameBeforeVersionGivesProtocolMismatch`, `PipeReplayStreamTickLimitAppliesToReplayedFrames` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC7 | `PipeReplayStreamEmptyFileIsNotARecording`, `PipeReplayStreamOtherFormatIsNotARecording`, `PipeReplayStreamHeaderWithoutVersionIsNotARecording`, `PipeReplayStreamOtherVersionIsUnsupported`, `PipeReplayStreamInvalidLineEndsAfterEarlierFrames` (invalid JSON, invalid base64, missing time, missing frame, field twice, frame over 1 MiB, cut last line), `PipeReplayStreamLineOverMaxLengthIsInvalidWithoutReadingItWhole`, `PipeReplayStreamAcceptedLineForms` (CRLF, no final line feed, one empty last line, byte order mark, unknown fields, any field order), `PipeReplayStreamEmptyLineInsideIsInvalid`, `PipeReplayStreamReadFailureIsReadFailed`, `PipeReplayStreamHeaderOnlyEndsAtOnce` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC7 | `PipeReplayRunnerMissingFileIsFileNotFound`, `PipeReplayRunnerDeniedFileIsFileNotFound` | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC8 | `PipeReplayStreamRealSpeedWaitsRecordedPause`, `PipeReplayStreamFactorDividesPause`, `PipeReplayStreamMaximumSpeedDoesNotWait`, `PipeReplayStreamBackwardOrRepeatedTimeDoesNotWaitAndKeepsTime`, `PipeReplayStreamCancelledWaitThrowsCancellation` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC8 | `PipeReplaySpeedTryParseAcceptsValidValues` (`max`, `1`, `0.1`, `1000`, `10`), `PipeReplaySpeedTryParseRejectsInvalidValues` (`0.09`, `1000.1`, `0`, `-1`, `1,5`, `NaN`, `Infinity`, empty, null), `PipeReplaySpeedScaleDividesPause` | `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs` |
| AC8 | `PipeReplayClockSetUtcNowFiresDueTimers`, `PipeReplayClockSetBackwardsFiresNothing`, `PipeReplayClockChangedOrDisposedTimerDoesNotFire`, `PipeReplayClockInfiniteDueTimeNeverFires` | `tests/Censio.Pipe.Tests/PipeReplayClockTests.cs` |
| AC9 | `CompanionCommandLineParseReturnsMode` (none, `--record`, `--replay f`, `--replay f --speed 10`, `--speed max --replay f`, option names in any case), `CompanionCommandLineParseInvalidReturnsInvalid` (unknown argument, `--speed` alone, bad speed, `--replay` without file, `--record --replay f`, an option twice) | `tests/Censio.Companion.Core.Tests/CompanionCommandLineTests.cs` |
| AC9 | `PipeReplayRunnerConnectedUntilFirstMessage`, `PipeReplayRunnerReceivingAfterVersion2`, `PipeReplayRunnerProtocolMismatchAfterOtherVersion`, `PipeReplayRunnerFinishedAfterLastFrame`, `PipeReplayRunnerProblemShownAfterEarlierFrames`, `PipeReplayRunnerCancelledEndsWithoutEvent` | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC9 | `PipeStatusRuleForReadReturnsStatusPerResult` (message, skip, protocol mismatch with and without version, each stream-ending error, end) | `tests/Censio.Companion.Core.Tests/PipeStatusRuleTests.cs` |
| AC9 | `TrayMenuFormatReplayToolTipReturnsTextPerStatus` (connected, receiving, protocol mismatch, finished, each problem), `TrayMenuInvalidCommandLineToolTipHasExactText` | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC10 | Reviewer check, no unit test: `README.md` and `docs/USER_GUIDE.md` describe `--record`, the folder, privacy, `--replay` and `--speed`, English first, then German (step 8) | `README.md`, `docs/USER_GUIDE.md` |

The shell parts of AC9 (no pipe opened while replaying, E9's single-instance end, `Exit` during a replay wait) and
facts 11 and 12 are checked in the maintainer's manual test on Windows in step 9.

## Approach

**Frames on the read result.** `PipeReadResult` gains `Frame`: the whole frame (type byte and payload) a message or
a skipped-frame error came from, `null` for the end and for truncated, oversized and read-failed errors.
`PipeMessageReader.ReadAsync` attaches it with `WithFrame` to every result it builds from a whole frame. The array is
the frame reader's own (fact 1), so nothing is copied.

**One clock reading.** `PipeTickAssembler` gets an optional read observer, called in `ReadStampedAsync` with the
result and the stamp it just read; an observer that throws is caught and ignored (no other exception leaves the
assembler, *Order and end*). `PipeConnection` reads `_timeProvider.GetUtcNow()` once after each read and hands that
value to the recorder (it uses the same value for `_receivingSince`).

**Format** (`PipeRecordingFormat`). Header `{"format":"censio-pipe-recording","version":1,"started":"<time>"}`, frame
line `{"time":"<time>","frame":"<base64>"}`, fields in this order when written, each line ending with `\n`. Time:
UTC, `yyyy-MM-ddTHH:mm:ss.fffffffZ`, invariant culture. File name `recording-yyyyMMdd-HHmmssZ.jsonl`, sequence 2 and
up adds `-<n>` before `.jsonl`. Written with `System.Text.Json`'s `Utf8JsonWriter`; read with `Utf8JsonReader`:
header needs `format` (exact string) and `version` (integer; other integer → unsupported version; missing or not an
integer → not a recording), skips other fields and their values; frame line needs `time` (parsed as ISO 8601 with
`GetDateTimeOffset`, converted to UTC) and `frame` (base64 string at most 1,398,104 characters, checked before
decoding, decoded at most 1,048,576 bytes); a field twice, another JSON value than an object, or trailing content →
invalid line. `MaxLineLength` = 1,398,104 + 4,096 bytes, counted before decoding, line terminator excluded.

**Recorder** (`PipeRecorder`, `Censio.Pipe`). `Record` is synchronous and never waits: it ignores a result without
frame and every call after a failure or `CompleteAsync`; otherwise it adds the frame length to the queued byte count
(`Interlocked`), and if that would exceed `MaxQueuedBytes` (16,777,216) it marks the recorder failed (the frames
already queued are still written) — else it enqueues `(frame, time)` on an unbounded `Channel` and starts the
writer task on the first frame. The writer opens the file through the factory with the first frame's time, writes
the header, then one line per frame as one `WriteAsync` of the whole line followed by `FlushAsync`. An exception
from the factory, a write or a flush (any exception, the factory's included) marks it failed, drops the queue and
disposes the stream. `Failed` is raised once, on the thread that detected the failure, each handler in its own
`try`. `CompleteAsync(token)` closes the channel and waits for the writer; a cancelled token stops the wait only
(the writer is abandoned, its task observed). `DisposeAsync` = `CompleteAsync(CancellationToken.None)`.

**Recording in the Companion** (`Censio.Companion.Core`). `PipeRecordingFiles.Create(started)` creates the folder
and the file with `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.Read`; an `IOException` because the file
exists tries the next suffix, up to 1,000, then rethrows. The folder comes only from the constructor
(`DefaultFolder(localApplicationData)` = `<LocalApplicationData>\Censio\Recordings`); the name only from the time
and the sequence. `PipeRecordingSession` hands out one `PipeRecorder` per connection (`BeginConnection`, `null` once
failed), completes it in the background on `EndConnection` (tracked), switches `State` from `Recording` to `Failed`
on the first `Failed` of any recorder and raises `StateChanged` once; `StopAsync(timeout)` completes every recorder
with one token that cancels after `timeout`. `PipeConnection` takes the optional session: `BeginConnection` after
connecting, `Record` after each read, `EndConnection` in the session's `finally` — it never awaits the disk.

**Replay** (`Censio.Pipe`). `PipeReplayStream` is a read-only, non-seekable stream: on each `ReadAsync` it serves the
rest of the current frame's bytes (4-byte little-endian length, then the frame), and when none are left reads the
next line. It reads the recording in chunks into a buffer of at most `MaxLineLength` + 2 bytes; a line longer than
that is invalid at once. The first line (after an optional UTF-8 BOM) must be the header. Before serving a frame
it waits `speed.Scale(time − previous time)` on the wait provider (no wait for the first frame, for a pause of zero
or less, or at maximum speed), then sets the replay clock to the frame's time. A problem or the end of the file
returns 0 at a frame boundary and leaves `Problem` set (or `null`); an `IOException` of the recording is
`ReadFailed`. Synchronous `Read` throws `NotSupportedException`. `PipeReplayClock` is a `TimeProvider` whose time
changes only by `SetUtcNow` (also backwards); its timers fire inside `SetUtcNow` once the time reaches their due
time, outside its lock, a finite period rescheduling them. A test replays with
`new PipeTickAssembler(new PipeMessageReader(new PipeReplayStream(file, PipeReplaySpeed.Maximum, clock, TimeProvider.System)), clock)`.

**Replay in the Companion.** `PipeReplayRunner` opens the file (`FileNotFoundException`,
`DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` on open → problem `FileNotFound`),
builds that pipeline with a read observer that applies `PipeStatusRule.ForRead` (end results ignored), reads items
until the end, then sets `IsFinished` (and the stream's problem). `PipeStatusRule.ForRead` is the rule moved out of
`PipeConnection.RunSessionAsync` (message → `Receiving`, protocol mismatch → `Protocol mismatch` with version,
stream-ending error → `Error` with kind, end → `Waiting for game`, skipped → `null`); `PipeConnection` calls it, its
behavior unchanged.

**Command line** (`CompanionCommandLine.Parse`). Options are matched case-insensitively, in any order, each once:
`--record`; `--replay <path>` (the next argument, which must not start with `--`); `--speed <value>` (needs
`--replay`; `PipeReplaySpeed.TryParse`: `max` case-insensitive, or a number in invariant culture, `NumberStyles.AllowDecimalPoint`, from 0.1 to 1000).
Anything else is `Invalid`.

**Tooltip layout** (exact texts the tests compare):

- `Censio Companion: <state>` without `--record` (unchanged); `Censio Companion: <state> (recording)`;
  `Censio Companion: <state> (recording failed)`.
- `Censio Companion: Replay: Connected`, `Censio Companion: Replay: Receiving`,
  `Censio Companion: Replay: Protocol mismatch`, `Censio Companion: Replay finished`,
  `Censio Companion: Replay error: <reason>` with `file not found`, `not a recording`, `unsupported version`,
  `invalid line <n>` (decimal, no grouping), `read failed`. A problem wins over finished.
- `Censio Companion: Invalid command line`.

**Shell** (`App.xaml.cs`). After the single-instance guard: parse `e.Args`; `Invalid` → that tooltip only; `Replay`
→ start the runner instead of the connection, tooltip from `FormatReplayToolTip`; `Record` → a session over
`PipeRecordingFiles(DefaultFolder(Environment.GetFolderPath(LocalApplicationData)))` passed to the connection, the
tooltip from `FormatToolTip(state, session.State)`, `StateChanged` notifying the updater. `OnExit` removes the icon,
cancels, then waits `session.StopAsync(TimeSpan.FromSeconds(1))` at most 1 second. `TrayStatusUpdater` becomes
generic over the status type for the replay tooltip.

## Options considered

The weighed options and why each lost are in record 0036: the frame on the read result versus a tee stream or a
frame-reader hook; a bounded queue versus writing on the read path; replay as a byte stream into the live reader
and assembler versus feeding the pure core, an own message source, or the test library's clock; a header line;
flush per line. Also weighed here: `TrayStatusUpdater` generic versus a second updater type (a copy) — generic wins,
the Tester adapts its tests.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `PipeReadResult.cs` | `Frame`, `WithFrame` |
| Censio.Pipe | `PipeMessageReader.cs` | attaches the frame to each result from a whole frame |
| Censio.Pipe | `PipeTickAssembler.cs` | constructor with read observer |
| Censio.Pipe | `PipeRecordingFormat.cs`, `PipeRecorder.cs`, `PipeReplayStream.cs`, `PipeReplayClock.cs`, `PipeReplaySpeed.cs`, `PipeReplayProblem.cs`, `PipeReplayProblemKind.cs` | new |
| Censio.Companion.Core | `PipeConnection.cs` | constructor with recording session; one stamp per read; uses `PipeStatusRule` |
| Censio.Companion.Core | `PipeStatusRule.cs`, `PipeRecordingFiles.cs`, `PipeRecordingSession.cs`, `PipeRecordingState.cs`, `PipeReplayRunner.cs`, `PipeReplayStatus.cs`, `CompanionCommandLine.cs`, `CompanionMode.cs` | new |
| Censio.Companion.Core | `TrayMenu.cs` | recording and replay tooltips |
| Censio.Companion.Core | `TrayStatusUpdater.cs` | generic over the status type |
| Censio.Companion | `App.xaml.cs` | command line, recording session, replay, flush on exit |

## Signatures (for the Dev's skeleton)

Properties, fields and events are written as the type and member name, a colon and the declaration; every
non-static class below is sealed.

Censio.Pipe:

- `PipeReadResult.Frame: public ReadOnlyMemory<byte>? Frame { get; }` and `public PipeReadResult WithFrame(ReadOnlyMemory<byte> frame)`
- `public PipeTickAssembler(IPipeMessageSource source, TimeProvider timeProvider, Action<PipeReadResult, DateTimeOffset>? readObserver)` (the existing constructor chains with null)
- `public static class PipeRecordingFormat` with `PipeRecordingFormat.FormatName: public const string = "censio-pipe-recording"`, `PipeRecordingFormat.FormatVersion: public const int = 1`, `PipeRecordingFormat.MaxLineLength: public const int = 1402200`, `public static string FormatHeader(DateTimeOffset started)`, `public static string FormatFrameLine(DateTimeOffset receiveTime, ReadOnlySpan<byte> frame)`, `public static string FormatFileName(DateTimeOffset started, int sequence)`, `public static PipeReplayProblemKind? CheckHeader(ReadOnlySpan<byte> line)`, `public static bool TryParseFrameLine(ReadOnlySpan<byte> line, out DateTimeOffset receiveTime, out byte[] frame)`
- `public class PipeRecorder : IAsyncDisposable` with `PipeRecorder.MaxQueuedBytes: public const long = 16777216`, `public PipeRecorder(Func<DateTimeOffset, Stream> openFile)`, `PipeRecorder.HasFailed: public bool { get; }`, `PipeRecorder.Failed: public event EventHandler?`, `public void Record(PipeReadResult result, DateTimeOffset receiveTime)`, `public Task CompleteAsync(CancellationToken cancellationToken)`, `public ValueTask DisposeAsync()`
- `public class PipeReplayClock : TimeProvider` with `public PipeReplayClock(DateTimeOffset start)`, `public void SetUtcNow(DateTimeOffset now)`, `public override DateTimeOffset GetUtcNow()`, `public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)`
- `public readonly record struct PipeReplaySpeed` with `PipeReplaySpeed.RealTime: public static PipeReplaySpeed { get; }`, `PipeReplaySpeed.Maximum: public static PipeReplaySpeed { get; }`, `PipeReplaySpeed.Factor: public double? { get; }` (null at maximum speed), `public static bool TryParse(string? text, out PipeReplaySpeed speed)`, `public TimeSpan Scale(TimeSpan recordedPause)`
- `public enum PipeReplayProblemKind { FileNotFound, NotARecording, UnsupportedVersion, InvalidLine, ReadFailed }`
- `public class PipeReplayProblem` with `PipeReplayProblem.Kind: public required PipeReplayProblemKind { get; init; }`, `PipeReplayProblem.LineNumber: public long? { get; init; }`
- `public class PipeReplayStream : Stream` with `public PipeReplayStream(Stream recording, PipeReplaySpeed speed, PipeReplayClock clock, TimeProvider waitProvider)`, `PipeReplayStream.Problem: public PipeReplayProblem? { get; }`, `public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)` and the required stream overrides

Censio.Companion.Core:

- `public static class PipeStatusRule` with `public static PipeConnectionStatus? ForRead(PipeReadResult read)`
- `public PipeConnection(IPipeConnector connector, TimeProvider timeProvider, PipeRecordingSession? recording)` (the existing constructor chains with null)
- `public enum PipeRecordingState { Off, Recording, Failed }`
- `public class PipeRecordingFiles` with `public PipeRecordingFiles(string folder)`, `public static string DefaultFolder(string localApplicationData)`, `public Stream Create(DateTimeOffset started)`
- `public class PipeRecordingSession` with `public PipeRecordingSession(Func<DateTimeOffset, Stream> openFile)`, `PipeRecordingSession.State: public PipeRecordingState { get; }`, `PipeRecordingSession.StateChanged: public event EventHandler?`, `public PipeRecorder? BeginConnection()`, `public void EndConnection(PipeRecorder recorder)`, `public Task StopAsync(TimeSpan timeout)`
- `public readonly record struct PipeReplayStatus` with `PipeReplayStatus.State: public required PipeConnectionState { get; init; }`, `PipeReplayStatus.IsFinished: public bool { get; init; }`, `PipeReplayStatus.Problem: public PipeReplayProblem? { get; init; }`
- `public class PipeReplayRunner` with `public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider)`, `PipeReplayRunner.Status: public PipeReplayStatus { get; }`, `PipeReplayRunner.StatusChanged: public event EventHandler<PipeReplayStatus>?`, `public Task RunAsync(CancellationToken cancellationToken)`
- `public enum CompanionMode { Normal, Record, Replay, Invalid }`
- `public class CompanionCommandLine` with `CompanionCommandLine.Mode: public required CompanionMode { get; init; }`, `CompanionCommandLine.ReplayPath: public string? { get; init; }`, `CompanionCommandLine.Speed: public PipeReplaySpeed { get; init; }`, `public static CompanionCommandLine Parse(IReadOnlyList<string> args)`
- on `TrayMenu`: `public static string FormatToolTip(PipeConnectionState state, PipeRecordingState recording)`, `public static string FormatReplayToolTip(PipeReplayStatus status)`, `TrayMenu.InvalidCommandLineToolTip: public const string = "Censio Companion: Invalid command line"`
- `public class TrayStatusUpdater<TStatus>` with `public TrayStatusUpdater(Func<TStatus> readStatus, Action<Action> post, Action<TStatus> show)` (replaces the non-generic class)

The skeleton rewrites `PipeReadResult.cs`, `PipeTickAssembler.cs`, `PipeConnection.cs`, `TrayMenu.cs` and
`TrayStatusUpdater.cs` (constructors and members added; the existing bodies read and found final apart from the
changes above).

## Test files

New: `tests/Censio.Pipe.Tests/PipeRecordingFormatTests.cs`, `PipeRecorderTests.cs`, `PipeReplayStreamTests.cs`,
`PipeReplayClockTests.cs`, `PipeReplaySpeedTests.cs`; `tests/Censio.Companion.Core.Tests/PipeStatusRuleTests.cs`,
`PipeRecordingFilesTests.cs` (in a temporary folder per test, deleted after), `PipeRecordingSessionTests.cs`,
`PipeReplayRunnerTests.cs`, `CompanionCommandLineTests.cs`. Extended: `PipeMessageReaderTests.cs`,
`PipeTickAssemblerTests.cs`, `PipeConnectionTests.cs`, `TrayMenuTests.cs`.

New test double: `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into
`tests/Censio.Companion.Core.Tests` as a `Compile` item like `PipeFrameBuilder`): a writable stream that keeps the
bytes written, can hold writes until released, fails a given write or flush with a given exception, counts flushes
and records its disposal; it models `FileStream` failures as fact 13 states. AC1's tests use `ChunkedStream` or
`ControlledPipeStream` and `FakeTimeProvider` for the live run.

Existing call sites of a changed signature: `TrayStatusUpdaterTests.cs` (`new TrayStatusUpdater(...)` becomes
`TrayStatusUpdater<PipeConnectionStatus>`) — adapted by the Tester. The `PipeConnection` and `PipeTickAssembler`
constructors and `TrayMenu.FormatToolTip(state)` keep their old forms, so their tests stay unchanged: no existing
input is handled differently (a result now also carries `Frame`, which no existing test asserts against).

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs` — process startup glue of the Windows shell, which
has no test project (record 0004) and is outside both coverage measures already (fact 7); no new exclusion.

## Areas

- `pipe`: new *Recording and replay* section; scope and index row extended.
- `companion`: command line, recordings, tooltips.

## Documentation updates

All owned by the Dev, written once the code is green; *Test doubles* by the Tester.

- `docs/areas/pipe.md`: *Scope* names recording and replay; a new section *Recording and replay* after *Ticks and
  sessions*: what is recorded (each whole frame with its one receive time, skipped frames included, nothing for
  stream-ending errors), one file per connection opened at its first frame, the format of *Approach* (header, frame
  line, time form, line feed, file name and suffix), the 16 MiB queue and what stops a recording, flush per line,
  the replay pipeline and clock, speeds and waits, E7's backward times, the line rules of E11, the replay problems
  and `MaxLineLength`; *Residual risks* gains: the last replayed tick closes as *stream ended* (already there, link
  the section), a recorder started mid-connection must write the last `Version` first (#13), a stalled disk at
  `Exit` can cut the last line; *Related decisions* gains 0036; *Not here* drops "Recording the stream (#12)" and
  gains "Anonymising recordings (#20)".
- `docs/areas/companion.md`: a *Command line* section (`--record`, `--replay <file>`, `--speed`, invalid command line,
  single instance applies); *Tray icon and menu* adds the tooltip suffixes and replay tooltips; a *Recordings*
  bullet (folder, never deleted, private content); *Limits and errors* says the Companion writes recordings only
  with `--record`; *Related decisions* gains 0036; *Not here* keeps the format in `pipe.md`.
- `docs/areas/README.md`: pipe row's scope adds "recording and replay", *Not here* becomes "Anonymising
  recordings"; companion row's scope adds "command line, recordings, replay".
- `docs/ARCHITECTURE.md`: `Censio.Pipe` row adds recording and replay; `Censio.Companion.Core` row adds command
  line, recording session and replay runner; one sentence after the tick sentence: the Companion records the raw
  frames with `--record` and replays a recording through the same reader and assembler (record 0036).
- `README.md`: *What works* bullet for `--record` and `--replay`; a *Recording and replay* subsection under
  *Companion* (start from a command prompt or a shortcut: `Censio.Companion.exe --record`, folder, privacy and
  anonymisation note, `--replay <file> [--speed <factor>|max]`, the replay tooltips, close the running Companion
  first); then the same in the German half.
- `docs/USER_GUIDE.md`: a *Recording and replay* subsection under *Companion* with the switches, file format in one
  sentence linking `docs/areas/pipe.md`, the tooltips and the errors; *Data and logs*: the Companion stores
  recordings in `%LOCALAPPDATA%\Censio\Recordings` only with `--record`, deleted by hand; *Reporting problems*: never
  attach a raw recording publicly; then the same in the German half.
- `.squad/project.md` *Security areas*: the recordings line names `PipeRecordingFiles` and the fixed folder; the
  parsing line adds recording files read by `PipeReplayStream`. *Test doubles* (Tester): `ControlledWriteStream`.

## Architecture check

No guarantee in `.squad/project.md` exists yet. Record 0018 (the format stays in `Censio.Pipe`) holds: the
recording format and replay live in `Censio.Pipe`; the Companion core only names files and states. Record 0034
holds: replay uses its assembler and clock injection unchanged, plus an observer. Record 0035's connection
behavior is unchanged with and without recording (the rule extraction is behavior-neutral, its tests stay).
Record 0004 holds: all new logic is in tested platform-neutral projects.

## Security considerations

- [ ] Limits on the recording file, each before decoding: a line is at most `MaxLineLength` bytes (refused before
  it is buffered whole); a `frame` string at most 1,398,104 characters before base64 decoding, the decoded frame
  at most 1,048,576 bytes; the decoded frame then passes the reader's own limits. The recorder's queue counts raw
  frame bytes (16,777,216), before base64 encoding. Strings are not decoded beyond JSON, so U+FFFD cannot grow them.
- [ ] Exceptions reaching a user-visible text: file open (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) → `Replay error: file not found`; read `IOException` →
  `Replay error: read failed`; recording factory, write or flush exceptions → `(recording failed)`. No exception
  message reaches the tooltip; nothing is logged.
- Paths: the recording folder comes from `Environment.SpecialFolder.LocalApplicationData` only; the file name from
  the time and a counter; no pipe data enters a path. The replay path is the user's own argument, opened read-only.

## Decision records

- `docs/decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md` (Proposed), index row added.

## Out of scope / follow-ups

As in the spec. No new issue: #13 (mid-connection start), #17 (real recordings) and #20 (anonymiser) exist.
