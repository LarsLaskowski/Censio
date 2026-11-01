# Plan: Recorder and replay of the statistics pipe

Source: Issue #12 | [spec.md](spec.md)
Plan size: a feature over two projects with eleven new types, a file format, its parser's bounds and the recorder's queue needs the length, as each rule is a test the Tester writes from it.
Status: Revised (2), final (Lead decision after the Plan ↔ Security loop limit)
Tier: standard — new behavior (recording files, replay from a file) that creates, changes or removes no security control: the recording path is fixed and built from no input, and the replay file's line bounds are input handling.
Security areas touched: File paths for recordings (`PipeRecordingFiles`, `%LOCALAPPDATA%\Censio\Recordings`); Parsing of external input (a recording file read by `PipeReplayStream`); Logging of external data (unchanged: nothing is logged, tooltips carry no file content)

## Problem / root cause

Feature: see [spec.md](spec.md) (S1–S8, E1–E11, AC1–AC10). Spec fixes made while planning: E6 (`read failed`, field
twice, unusable times, nesting), E10 (cut line at the `Exit` limit; line cost per entry), E11 (byte order mark;
header needs `format` and `version`), E2 (no power-loss promise), E3 (folder not a full path), S5 (first tooltip
per the status rule).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The frame reader allocates a fresh array per frame, so a frame can be kept without a copy | `src/Censio.Pipe/PipeFrameReader.cs`, `ReadCoreAsync`: `var frame = new byte[length]` | confirmed |
| 2 | The tick assembler reads the clock once per result, after the source's read returned | `PipeTickAssembler.ReadStampedAsync`: `return (result, _timeProvider.GetUtcNow())` | confirmed |
| 3 | `PipeTickBuilder.Add` closes a tick by the quiet period before it adds a frame stamped at or after the deadline, so a replay gives the same ticks whether the quiet timer or the read completes first | `PipeTickBuilder.Add` calls `CloseIfQuiet(receiveTime)` first; `PipeTickAssembler.Step` calls `CloseIfQuiet(now)` when `now >= deadline` | confirmed |
| 4 | The live Companion reads messages without a tick assembler | `PipeConnection.RunSessionAsync` loops on `PipeMessageReader.ReadAsync` and only sets the status | confirmed |
| 5 | Skipped frames (empty, unknown type, malformed, protocol mismatch) are built in `PipeMessageReader.ReadAsync` or `PipeMessageParser.Parse` from a whole frame; stream-ending errors and the end carry none | `PipeMessageReader.ReadAsync`; `PipeFrameReader.Fail` | confirmed |
| 6 | No release tag exists, so every record is unreleased | `git ls-remote --tags origin` printed nothing | confirmed |
| 7 | The Windows shell is outside both coverage measures | `coverage-check.py` `new_code_coverage` counts only reported lines (no test project); `ci.yml` line 71 excludes `src/Censio.Companion/**` | confirmed |
| 8 | The server image builds neither changed project | `Censio.Server.csproj` references Contracts and Server.Data only | confirmed |
| 9 | Base64 of 1 MiB is 1,398,104 characters | 4 × ceil(1,048,576 / 3) | confirmed |
| 10 | The replay clock's timers need never fire: the assembler races the quiet timer against the pending read (`Step`, `Task.WhenAny`) and cancels the delay after it; with a timer that never fires the read decides, and `Add` closes the quiet tick first (fact 3), so the items are the same | `PipeTickAssembler.Step`; `PipeTickBuilder.Add` | confirmed |
| 16 | `FileStream.FlushAsync` and `Flush()` hand the bytes to the operating system only; `Flush(true)` or `FileOptions.WriteThrough` reach the disk. Bytes handed to the system survive the end of the process, not a power loss | Devil's Advocate M1; .NET API documentation of `FileStream.Flush(Boolean)` | confirmed |
| 17 | `Task.Delay` refuses a delay over `UInt32.MaxValue − 1` milliseconds (about 49.7 days); `DateTimeOffset` + 30 s throws near `MaxValue`; `Utf8JsonReader.GetDateTimeOffset` throws `FormatException` where `TryGetDateTimeOffset` returns false; `Skip` past the depth limit (64) throws `JsonException` | Security plan review B1 | confirmed |
| 18 | `Environment.GetFolderPath(LocalApplicationData)` returns an empty string when the folder cannot be determined | Security plan review B3; .NET API documentation of `GetFolderPath` | confirmed |
| 19 | `PipeTickAssembler.Step` passes `deadline − now` to `Task.Delay` uncapped (line 143); `_lastReceiveTime` moves only on islands, so a clock set back 59 days or more after an island makes `Task.Delay` throw `ArgumentOutOfRangeException` before `CreateTimer`. A live system clock set back that far does the same (latent: no live assembler yet, fact 4) | `src/Censio.Pipe/PipeTickAssembler.cs` lines 122–143; Security plan review F1, measured on .NET 10.0.403 | confirmed |
| 20 | `Utf8JsonReader`'s `TryGetDateTimeOffset`, `TryGetBytesFromBase64`, `TryGetInt32` and `ValueTextEquals` throw `InvalidOperationException` on a token of another type; `GetString` throws it on invalid UTF-8 | Security plan review F2 | confirmed |
| 11 | WPF raises `Application.Exit` (`OnExit`) after an uncancelled `SessionEnding` at sign-out and shutdown, so the recording flush in `OnExit` also runs then | to verify: Dev reads the WPF `Application` source (`release/10.0`); maintainer's manual test on Windows (sign-out with `--record`, file ends with a complete line) in step 9 | unverified |
| 12 | `NotifyIcon.Text` accepts up to 127 characters on .NET 10; the longest new tooltip (`Censio Companion: Replay error: invalid line 9223372036854775807`) has 64 | to verify: Dev reads `System.Windows.Forms` `NotifyIcon.Text` (`release/10.0`) | unverified |
| 13 | A full disk and a deleted folder surface from `FileStream` writes and flushes as `IOException` (access denied as `UnauthorizedAccessException`), the cases `ControlledWriteStream` models | to verify: Dev reads `dotnet/runtime` `release/10.0` `Win32Marshal.GetExceptionForWin32Error` (`ERROR_DISK_FULL`, `ERROR_ACCESS_DENIED`) before the Tester writes the double | unverified |
| 14 | The issue's claims (raw frames with receive time as base64 JSON lines, a file per session, three speeds) fit the merged #9/#10 code | `src/Censio.Pipe`; "session" = connection (spec Q2) | confirmed |
| 15 | The recording format is defined here, so no real sample exists; tests use `PipeRecorder` output; real recordings come with #17, anonymised by #20 | record 0029 | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeReplayStreamRecordedRunReplaysSameItemsAtMaximumSpeed`, `PipeReplayStreamRecordedRunReplaysSameItemsAtFactorSpeed`, `PipeReplayStreamClockMovedInsideChunkedFrameKeepsReceiveTime`, `PipeReplayStreamQuietPeriodCloseReplaysAsQuietPeriod`, `PipeReplayStreamLastTickClosedByQuietPeriodReplaysAsStreamEnded`, `PipeReplayStreamSessionEventsAndVersionsReplayInOrder` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC1 | `PipeTickAssemblerReadObserverGetsEachResultWithItsStamp`, `PipeTickAssemblerThrowingReadObserverKeepsReading` | `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` |
| AC2 | `PipeMessageReaderResultCarriesWholeFrame` (message, empty, unknown type, malformed, protocol mismatch), `PipeMessageReaderEndAndStreamEndingErrorCarryNoFrame` | `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs` |
| AC2 | `PipeRecorderRecordsEachFrameOnceInOrder`, `PipeRecorderResultWithoutFrameIsNotRecorded`, `PipeRecorderCompleteWritesQueuedFramesAsCompleteLines`, `PipeRecorderFlushesEachLine`, `PipeRecorderWrittenLinesFreeQueueSpace` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC2 | `PipeConnectionRecordingEndInsideChunkedFrameRecordsOnlyWholeFrames`, `PipeConnectionRecordingStopInsideChunkedFrameRecordsOnlyWholeFrames`, `PipeConnectionRecordingStampsEachFrameOnce` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC3 | `PipeRecordingFormatHeaderHasFormatVersionAndStarted`, `PipeRecordingFormatFrameLineHasSevenDigitUtcTimeAndBase64`, `PipeRecordingFormatFileNameUsesStartSecondAndSuffix` | `tests/Censio.Pipe.Tests/PipeRecordingFormatTests.cs` |
| AC3 | `PipeRecorderOpensFileAtFirstFrameWithItsTime`, `PipeRecorderWritesHeaderBeforeFirstFrame` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC3 | `PipeRecordingFilesCreatesFolderAndFile`, `PipeRecordingFilesSameSecondGetsNextSuffix`, `PipeRecordingFilesNeverOverwrites`, `PipeRecordingFilesOtherCreateErrorIsNotRetried` (a directory with the file's name), `PipeRecordingFilesNameIsGregorianUnderOtherCulture` (`th-TH`) | `tests/Censio.Companion.Core.Tests/PipeRecordingFilesTests.cs` |
| AC4 | `PipeConnectionRecordingWritesOneFilePerConnection`, `PipeConnectionRecordingCreatesNoFileWithoutFrame`, `PipeConnectionWithoutRecordingCreatesNoRecorder`, `PipeConnectionRecordingReplaysSameTicksAsLiveAssembler` (a `ScriptedPipeConnector` connection over a `ControlledPipeStream` fed in chunks while the `FakeTimeProvider` moves, recorded, replayed at maximum speed, compared with a `PipeTickAssembler` over the same chunks and clock moves) | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC4 | `PipeRecordingSessionEndConnectionClosesFile` | `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` |
| AC5 | `PipeRecorderQueueOverLimitFailsAndKeepsQueuedFrames`, `PipeRecorderEmptyFramesOnHeldStreamFailAtEntryLimit` (fails at entry 131,073), `PipeRecorderRecordDoesNotWaitForStalledWrite`, `PipeRecorderWriteFailureFailsAndKeepsCompleteLines`, `PipeRecorderOpenFailureFails`, `PipeRecorderCompleteCancelledStopsWaiting`, `PipeRecorderThrowingFailedHandlerIsIgnored` | `tests/Censio.Pipe.Tests/PipeRecorderTests.cs` |
| AC5 | `PipeRecordingSessionStateIsRecordingUntilFailure`, `PipeRecordingSessionFailureStopsRecordingForRestOfRun`, `PipeRecordingSessionStopReturnsWithinTimeoutOnStalledDisk`, `PipeRecordingSessionStopCompletesAllConnections` |
| AC5 | `PipeRecordingFilesFolderNotFullyQualifiedIsRefused` (empty, relative) | `tests/Censio.Companion.Core.Tests/PipeRecordingFilesTests.cs` | `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` |
| AC5 | `PipeConnectionRecordingFailureKeepsStatesAndReads` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC5 | `TrayMenuFormatToolTipWithRecordingStateAppendsSuffix` (off, recording, failed per state) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC6 | `PipeReplayStreamSkippedFramesReplayAsSkipped`, `PipeReplayStreamFrameBeforeVersionGivesProtocolMismatch`, `PipeReplayStreamTickLimitAppliesToReplayedFrames` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC7 | `PipeReplayStreamEmptyFileIsNotARecording`, `PipeReplayStreamOtherFormatIsNotARecording`, `PipeReplayStreamHeaderWithoutVersionIsNotARecording`, `PipeReplayStreamHeaderFieldOfWrongTypeIsNotARecording` (`format` as number, null, object, with a raw 0xFF byte; `version` as `"1"`, null, `1.5`), `PipeReplayStreamOtherVersionIsUnsupported`, `PipeReplayStreamInvalidLineEndsAfterEarlierFrames` (invalid JSON, invalid base64, missing time, missing frame, field twice, frame over 1 MiB, a frame string of 1,398,104 characters without padding, cut last line, time `9999-12-31T23:59:59.9999999Z`, time `0001-01-01T00:00:00+01:00`, an unknown field nested 65 deep, `time` and `frame` each as number, null, object and array, `time` with a raw 0xFF byte), `PipeReplayStreamLineOverMaxLengthIsInvalidWithoutReadingItWhole`, `PipeReplayStreamAcceptedLineForms` (CRLF, no final line feed, one empty last line, byte order mark, unknown fields, any field order), `PipeReplayStreamEmptyLineInsideIsInvalid`, `PipeReplayStreamReadFailureIsReadFailed`, `PipeReplayStreamHeaderOnlyEndsAtOnce` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC7 | `PipeReplayRunnerMissingFileIsFileNotFound`, `PipeReplayRunnerDeniedFileIsFileNotFound`, `PipeReplayRunnerRefusedPathIsFileNotFound` (open throws `ArgumentException`) | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC8 | `PipeReplayStreamRealSpeedWaitsRecordedPause`, `PipeReplayStreamFactorDividesPause`, `PipeReplayStreamMaximumSpeedDoesNotWait`, `PipeReplayStreamBackwardOrRepeatedTimeDoesNotWaitAndKeepsTime`, `PipeReplayStreamCancelledWaitThrowsCancellation` | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs` |
| AC8 | `PipeReplaySpeedTryParseAcceptsValidValues` (`max`, `1`, `0.1`, `1000`, `10`), `PipeReplaySpeedTryParseRejectsInvalidValues` (`0.09`, `1000.1`, `0`, `-1`, `1,5`, `NaN`, `Infinity`, empty, null), `PipeReplaySpeedScaleDividesPause` | `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs` |
| AC8 | `PipeReplayClockGetUtcNowReturnsLastSetTimeAlsoBackwards`, `PipeReplayClockTimerNeverFiresAndAcceptsChangeAndDispose` | `tests/Censio.Pipe.Tests/PipeReplayClockTests.cs` |
| AC8 | `PipeReplayStreamGapOverDelayLimitIsWaitedInSteps` (60-day gap at speed 1), `PipeReplayStreamBackwardTimeOverDelayLimitAfterIslandDoesNotThrow` (island at T1, a skipped frame at T1 − 60 days, a frame one second later, speed 1: the items of the recorded times, no exception), `PipeReplaySpeedScaleSaturatesAtMaximum` (full-range gap at speed 0.1) | `tests/Censio.Pipe.Tests/PipeReplayStreamTests.cs`, `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs` |
| AC8 | `PipeTickAssemblerClockBackOverDelayLimitWaitsInSteps` (`FakeTimeProvider`: island at T, clock set to T − 60 days with a read pending; two 1-day advances throw nothing and close nothing; advancing to T + 30 s closes the tick as quiet period) | `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` |
| AC9 | `CompanionCommandLineParseReturnsMode` (none, `--record`, `--replay f`, `--replay f --speed 10`, `--speed max --replay f`, option names in any case), `CompanionCommandLineParseInvalidReturnsInvalid` (unknown argument, `--speed` alone, bad speed, `--replay` without file, `--replay ""`, `--record --replay f`, an option twice) | `tests/Censio.Companion.Core.Tests/CompanionCommandLineTests.cs` |
| AC9 | `PipeReplayRunnerConnectedUntilFirstStatus` (skipped frames keep `Connected`), `PipeReplayRunnerFrameBeforeVersionShowsProtocolMismatch`, `PipeReplayRunnerReceivingAfterVersion2`, `PipeReplayRunnerProtocolMismatchAfterOtherVersion`, `PipeReplayRunnerFinishedAfterLastFrame`, `PipeReplayRunnerProblemShownAfterEarlierFrames`, `PipeReplayRunnerCancelledEndsWithoutEvent` | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
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

**Quiet wait** (`PipeTickAssembler.Step`, Security F1, fact 19). The delay is `min(deadline − now, 1 day)`
(private constant `MaxQuietWait`); when it elapses, `Step` returns and the next pass reads the clock again and waits
on until the deadline, so a clock set back after an island waits in steps instead of throwing. Close rules and
items are unchanged: a replay's timers never fire (fact 10), and a live clock reaches the same deadline.

**Format** (`PipeRecordingFormat`). Header `{"format":"censio-pipe-recording","version":1,"started":"<time>"}`, frame
line `{"time":"<time>","frame":"<base64>"}`, fields in this order when written, each line ending with `\n`. Time:
UTC, `yyyy-MM-ddTHH:mm:ss.fffffffZ`, invariant culture (Gregorian). File name `recording-yyyyMMdd-HHmmssZ.jsonl`,
invariant culture, sequence 2 and up adds `-<n>` before `.jsonl`. Written with `System.Text.Json`'s `Utf8JsonWriter`; read with `Utf8JsonReader`:
each known field's `TokenType` is checked before its getter (fact 20): `String` for `format`, `time` and `frame`,
`Number` for `version`; `GetString` is never called. Header needs `format` (`ValueTextEquals` on the UTF-8 name) and
`version` (`TryGetInt32`; other integer → unsupported version; missing, of another type or not an `Int32` → not a
recording), skips other fields and their values; frame line needs `time` (`TryGetDateTimeOffset`, converted to UTC,
and at most `DateTimeOffset.MaxValue` − 30 seconds so the quiet deadline cannot overflow) and `frame` (base64 string
at most 1,398,104 characters, checked before decoding, and at most 1,048,576 bytes after decoding — 1,398,104
characters without padding decode to 1,048,578); a field of another type, a field twice, another JSON value than an
object, trailing content, or a `FormatException`, `JsonException` (depth over 64 included) or
`InvalidOperationException` of the reader (a fall-back only; the type checks come first) → invalid line, in the
header → not a recording. `MaxLineLength` = 1,398,104 + 4,096 bytes, counted before decoding, line terminator excluded.

**Recorder** (`PipeRecorder`, `Censio.Pipe`). `Record` is synchronous and never waits: it ignores a result without
frame and every call after a failure or `CompleteAsync`; otherwise it adds the entry's cost `EntryCost(frame length)` = 4 × ceil(length / 3) +
`EntryOverhead` (128) to the queued count (`Interlocked`), and if that would exceed `MaxQueuedBytes` (16,777,216)
it marks the recorder failed (so at most 131,072 empty frames queue) (the frames
already queued are still written) — else it enqueues `(frame, time)` on an unbounded `Channel` and starts the
writer task on the first frame. The writer opens the file through the factory with the first frame's time, writes
the header, then one line per frame as one `WriteAsync` of the whole line followed by `FlushAsync`, and subtracts
the entry's cost once the line is flushed. The flush hands the line to the operating system (fact 16): it survives
a killed Companion, not a power loss; no disk sync per line, so the 1-second flush at `Exit` is not spent on it. An exception
from the factory, a write or a flush (any exception, the factory's included) marks it failed, drops the queue and
disposes the stream. `Failed` is raised once, on the thread that detected the failure, each handler in its own
`try`. `CompleteAsync(token)` closes the channel and waits for the writer; a cancelled token stops the wait only
(the writer is abandoned, its task observed). `DisposeAsync` = `CompleteAsync(CancellationToken.None)`.

**Recording in the Companion** (`Censio.Companion.Core`). `PipeRecordingFiles.Create(started)` creates the folder
and the file with `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.Read`; an `IOException` after which
`File.Exists(path)` is true tries the next suffix, up to 1,000; any other exception is rethrown at once. A folder
that is not `Path.IsPathFullyQualified` is refused with `InvalidOperationException` in `Create` (fact 18), so the
recorder fails as E3. The folder comes only from the constructor
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
or less, or at maximum speed), in steps of at most one day (fact 17), then sets the replay clock to the frame's
time. `Scale` computes in `double` ticks and saturates at `TimeSpan.MaxValue`. A problem or the end of the file
returns 0 at a frame boundary and leaves `Problem` set (or `null`); an `IOException` of the recording is
`ReadFailed`. Synchronous `Read` throws `NotSupportedException`. `PipeReplayClock` is a `TimeProvider` whose time
changes only by `SetUtcNow` (also backwards); `CreateTimer` returns a timer that never fires and accepts `Change`
and `Dispose` (fact 10); the real waits run on the wait provider. A test replays with
`new PipeTickAssembler(new PipeMessageReader(new PipeReplayStream(file, PipeReplaySpeed.Maximum, clock, TimeProvider.System)), clock)`.

**Replay in the Companion.** `PipeReplayRunner` opens the file (`FileNotFoundException`,
`DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException`, `ArgumentException`, `NotSupportedException`
(a path .NET refuses, N6; the catch covers only the open) on open → problem `FileNotFound`),
builds that pipeline with a read observer that applies `PipeStatusRule.ForRead` (end results and stream-ending errors ignored — `PipeReplayStream` ends only at a frame boundary and reports its
problems itself, S5; the status
stays `Connected` until the first result the rule maps to a status, so a recording whose first frame is not a
`Version` shows `Protocol mismatch`), reads items
until the end, then sets `IsFinished` (and the stream's problem). `PipeStatusRule.ForRead` is the rule moved out of
`PipeConnection.RunSessionAsync` (message → `Receiving`, protocol mismatch → `Protocol mismatch` with version,
stream-ending error → `Error` with kind, end → `Waiting for game`, skipped → `null`); `PipeConnection` calls it, its
behavior unchanged.

**Command line** (`CompanionCommandLine.Parse`). Options are matched case-insensitively, in any order, each once:
`--record`; `--replay <path>` (the next argument, which must not be empty or start with `--`); `--speed <value>` (needs
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

In record 0036 (options 1–11). Also: `TrayStatusUpdater` generic versus a second updater type — generic wins,
the Tester adapts its tests.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `PipeReadResult.cs` | `Frame`, `WithFrame` |
| Censio.Pipe | `PipeMessageReader.cs` | attaches the frame to each result from a whole frame |
| Censio.Pipe | `PipeTickAssembler.cs` | constructor with read observer; quiet wait in steps of at most one day |
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
- `public class PipeRecorder : IAsyncDisposable` with `PipeRecorder.MaxQueuedBytes: public const long = 16777216`, `PipeRecorder.EntryOverhead: public const int = 128`, `public static long EntryCost(int frameLength)`, `public PipeRecorder(Func<DateTimeOffset, Stream> openFile)`, `PipeRecorder.HasFailed: public bool { get; }`, `PipeRecorder.Failed: public event EventHandler?`, `public void Record(PipeReadResult result, DateTimeOffset receiveTime)`, `public Task CompleteAsync(CancellationToken cancellationToken)`, `public ValueTask DisposeAsync()`
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

New test double `ControlledWriteStream` (`tests/Censio.Pipe.Tests`, linked into `Censio.Companion.Core.Tests` like
`PipeFrameBuilder`): keeps the bytes written, holds writes until released, fails a given write or flush with a given
exception, counts flushes, records disposal; models `FileStream` failures (fact 13).

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

- `docs/areas/pipe.md`: *Order and end*: the quiet period is waited in steps of at most one day, so a clock set
  back, by a replay or the system, waits on and throws nothing — "No other exception leaves the assembler" stays;
  *Scope* names recording and replay; new section *Recording and replay* after *Ticks and
  sessions*, stating the rules of *Approach* (what is recorded, file per connection, format, queue and its cost,
  flush, replay pipeline, clock, speeds, waits, line rules, problems, limits); *Residual risks*: mid-connection
  start needs the last `Version` (#13), a stalled disk or power loss can lose the last lines; *Related decisions*
  gains 0036; *Not here*: "Anonymising recordings (#20)" instead of "Recording the stream (#12)".
- `docs/areas/companion.md`: *Command line* section (the switches, invalid command line, single instance); the
  tooltips; recordings folder (never deleted, private); *Related decisions* gains 0036.
- `docs/areas/README.md`: pipe row adds "recording and replay", *Not here* "Anonymising recordings"; companion row
  adds "command line, recordings, replay".
- `docs/ARCHITECTURE.md`: the `Censio.Pipe` and `Censio.Companion.Core` rows; one flow sentence on recording and
  replay through the same reader and assembler (record 0036).
- `README.md` and `docs/USER_GUIDE.md`, English then German: *What works* bullet (README); *Recording and replay*
  under *Companion* (`--record`, folder, privacy and anonymisation, `--replay <file> [--speed <factor>|max]` on a
  local file, tooltips, close the running Companion first); user guide also *Data and logs* (recordings only with
  `--record`, deleted by hand) and *Reporting problems* (never attach a raw recording publicly).
- `.squad/project.md` *Security areas*: recordings line names `PipeRecordingFiles` and the folder; parsing line
  adds recording files (`PipeReplayStream`). *Test doubles* (Tester): `ControlledWriteStream`.

## Architecture check

No guarantee exists yet. Record 0018 holds (format and replay in `Censio.Pipe`), 0034 (close rules unchanged; an
observer and a capped quiet wait), 0035 (connection behavior unchanged; the rule extraction keeps its tests), 0004 (new logic tested).

## Security considerations

- [ ] Limits on the recording file, each before decoding: a line is at most `MaxLineLength` bytes (refused before
  it is buffered whole); a `frame` string at most 1,398,104 characters before base64 decoding, the decoded frame
  at most 1,048,576 bytes; the decoded frame then passes the reader's own limits. The recorder's queue counts each
  entry's line cost (`EntryCost`, base64 length plus 128), at most 16,777,216 in total. Strings are not decoded beyond JSON, so U+FFFD cannot grow them.
- [ ] Exceptions reaching a user-visible text: file open (`FileNotFoundException`, `DirectoryNotFoundException`,
  `UnauthorizedAccessException`, `IOException`) → `Replay error: file not found`; read `IOException` →
  `Replay error: read failed`; recording factory, write or flush exceptions → `(recording failed)`. No exception
  message reaches the tooltip; nothing is logged.
- Paths: the recording folder comes from `Environment.SpecialFolder.LocalApplicationData` only; the file name from
  the time and a counter; no pipe data enters a path. The replay path is the user's own argument, opened read-only.

## Decision records

- `docs/decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md` (Proposed), index row added.

## Challenge

Devil's Advocate plan challenge, all accepted (reasons in record 0036 where they are decisions):

- M1: narrowed. E2 covers a killed or crashed Companion; per-line `FlushAsync` reaches the OS only (fact 16); no
  disk sync per line (record 0036 option 11).
- m1: S5 and *Approach* follow `PipeStatusRule`; AC9 tests renamed and added.
- m2: the replay clock's timers never fire (fact 10); timer-firing tests dropped.
- m3: `PipeConnectionRecordingReplaysSameTicksAsLiveAssembler` (AC4).
- m4: suffix only when `File.Exists`; other errors rethrown; test in AC3.

## Security review

Round 1 (B1–B3, N1–N5) resolved; the answers are in *Approach*, the tests and record 0036. Round 2
`CHANGES_REQUIRED`, the second rejection: the Lead decided (mode `decide`, *Loop limits*) to accept and fix, since
Security measured F1 and supplied F2's statements, and neither fix changes the scope or a criterion
(`PipeTickAssembler.cs` is already in the plan). One Security delta confirmation follows; a further rejection goes to
the Product Manager.

- F1: the quiet wait is capped at one day and re-checked (*Quiet wait*, record 0036 option 12); refusing backward
  times in the replay lost (option 13: changes E7 and leaves the live clock's case open). Tests in AC8.
- F2: token types checked before each getter, no `GetString`, `InvalidOperationException` caught as a fall-back;
  spec E6 and E11 updated; tests in AC7.
- N6: taken both ways: `Parse` refuses an empty `--replay` (spec E8), the runner maps `ArgumentException` on open to
  `file not found` (spec E6); tests in AC7 and AC9.

## Out of scope / follow-ups

As in the spec. No new issue: #13 (mid-connection start), #17 (real recordings) and #20 (anonymiser) exist.
