# Plan: Bounded local tick buffer and gaps

Source: Issue #41 | [spec.md](spec.md)
Tier: security — the change writes, prunes and reads back files in a new folder of the Companion and adds two parsed settings keys, both listed security areas.
Security areas touched: File paths the Companion writes, prunes or opens (`CompanionFolders.Buffer`, `LiveBuffer`); Parsing of external input (`LiveBufferFile` reading the buffer back, `CompanionSettings` with `bufferHours` and `bufferMegabytes`); Logging and showing external data (`CompanionLog`, the tray line); Outbound calls (`LiveSender.ForServer`, `LiveSender.ForLocalServer` send what the buffer returns)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Waiting messages are held only in memory, at most 64 messages and 16,777,216 bytes, oldest dropped first except the one in flight; the newest stays | `LiveSender.cs:25`, `:30`, `DropOldest` (`:390`–`:400`) | confirmed |
| 2 | The queue holds ticks with islands, trade-routes items, session starts and ends; `status` is passed straight to the connection | `LiveSender.Convert`, `NotifyState` | confirmed |
| 3 | After `TokenRefused`, `VersionNotSupported` or `LocalServerNotRunning` the run ends but `Enqueue` keeps queueing until dispose | `RetryAsync` returns; `Enqueue` checks only `_isDisposed` | confirmed |
| 4 | `Enqueue` runs on the pipe connection's `ItemReceived` event, so it must not wait | `App.xaml.cs:330` | confirmed |
| 5 | `localPort` is an integer literal of digits only (`TryReadPort`); forward and local settings are read in `ReadLocalServer`, a problem makes `LocalServerSettingKind.Invalid` | `CompanionSettings.cs:183`–`:345` | confirmed |
| 6 | An invalid server setting starts no local server and no live sending | `local-mode.md`, *Switching on*; `LiveSender.IsSending` | confirmed |
| 7 | The menu shows the local line in local mode, else the live line; `App.GetServerStatus` returns `Local` in local mode, so the live status never reaches the menu there | `TrayMenu.Create` (`:292`–`:299`), `App.xaml.cs:453`–`:470` | confirmed |
| 8 | An entry kind outside the clickable list is shown greyed | `TrayIcon.cs:142` | confirmed |
| 9 | A resent tick is acknowledged by its `id` without storing it twice; session events store nothing; a late tick is stored like any tick | `ingest.md`, *Messages after the hello*; `history.md` | confirmed |
| 10 | Charts break at a missing value and after 15 minutes without a point | `TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins`, `SparklineBuilderBreaksAtMissingValueAndAfterFifteenMinutes` | confirmed |
| 11 | The contract's message bound is 25,165,824 bytes | `ContractReader.MaxMessageBytes` (`ContractReader.cs:20`) | confirmed |
| 12 | Release 0.4.0 reads contract version 2 | `git show v0.4.0:src/Censio.Contracts/ContractReader.cs`: `CurrentVersion = 2` | confirmed |
| 13 | Record 0031 is released; 0103 and 0104 are not | `git tag --contains` of each adding commit | confirmed |
| 14 | The cache writes a temporary file with `FileMode.CreateNew`, `Flush(true)`, then `File.Move` | `GameCatalogCacheStore.cs:120`–`:134` | confirmed |
| 15 | A rename within one folder is atomic on NTFS | to verify: none needed before the build — the design does not rest on it, a torn final file fails its length or hash check and is discarded and counted (E3); the Product Manager's Windows run of S2 in step 9 shows a restart reading the buffer | unverified |
| 16 | One Companion runs per Windows session, so one writer per buffer folder | `SingleInstanceGuard`, `companion.md`, *Single instance* | confirmed |
| 17 | `Exit` waits at most 250 ms for the sender | `App.xaml.cs:698`–`:702` | confirmed |
| 18 | Ticks come about every two minutes (fixture: 31 ticks in about 1.5 hours) | `tests/Censio.Pipe.Tests/Fixtures/README.md` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `LiveSenderRunAfterOutageSendsBeyondMemoryBoundInOrder` (70 messages, over 16,777,216 bytes, taken while unreachable, all sent oldest first, none dropped); `LiveBufferNextAsyncReturnsInOrderTaken`; `LiveBufferAddDuringLoadComesAfterStoredMessages` | `LiveSenderTests.cs`, `LiveBufferTests.cs` |
| AC2 | `LiveBufferRunAsyncReadsEarlierRunInOrderAndLogs`; `LiveSenderRunAfterRestartResendsInFlightWithSameId`; `LiveBufferAcknowledgeDeletesFile`; `LiveBufferRunAsyncDeletesTemporaryFileAndCountsIt` | `LiveBufferTests.cs`, `LiveSenderTests.cs` |
| AC3 | `LiveBufferNextAsyncDropsMessagesOlderThanLimit`; `LiveBufferRunAsyncDropsExpiredStoredMessages`; `LiveBufferNextAsyncKeepsMessageTakenInFuture` (clock set back) | `LiveBufferTests.cs` |
| AC4 | `LiveBufferAddBeyondSizeDropsOldestKeepsNewest`; `LiveBufferAddLargerThanLimitKeepsOnlyNewest`; `LiveBufferFilesStayWithinLimitPlusHeaders` | `LiveBufferTests.cs` |
| AC5 | `TrayMenuFormatLiveBufferTexts` (DataRows: 0, waiting, dropped, memory only, both); `TrayMenuCreateShowsBufferLineBelowServerLine` (forward and local); `LiveBufferStatusCountsWaitingAndDropped`; existing `LiveSenderRunLogsDroppedCountOnce`, adapted | `TrayMenuTests.cs`, `LiveBufferTests.cs`, `LiveSenderTests.cs` |
| AC6 | `CompanionSettingsReadLocalServerBufferLimits` (DataRows: defaults, 48/500, bounds 1/168 and 1/10240, both modes); `CompanionSettingsReadLocalServerInvalidBufferLimitIsInvalid` (DataRows: `24.0`, `2.4e1`, `"24"`, `null`, 0, -1, 169, 10241, duplicate in other case — each its exact problem text); `LiveBufferLimitsDefaultsAndConversion` | `CompanionSettingsTests.cs`, `LiveBufferLimitsTests.cs` |
| AC7 | existing `TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins` and `SparklineBuilderBreaksAtMissingValueAndAfterFifteenMinutes`, tagged `AC7`; `LiveBufferNextAsyncNeverReturnsDroppedMessage` | `TrendSeriesBuilderTests.cs`, `SparklineBuilderTests.cs`, `LiveBufferTests.cs` |
| AC8 | `LiveBufferFileReadRefusesDamagedForms` (DataRows: short header, magic, version, empty or all-zero id, time out of range, length 0 or above 25,165,824, file length mismatch, hash mismatch); `LiveBufferFileFormatReadsBack`; `LiveBufferRunAsyncDiscardsUnreadableFileCountsAndLogs`; `LiveBufferRunAsyncLeavesForeignFiles`; `LiveBufferNextAsyncDiscardsFileDamagedAfterLoad`; `LiveBufferRunAsyncFolderBlockedKeepsMemoryOnly` (a file named `Buffer`); `LiveBufferAddAfterWriteFailureKeepsMemoryBound` (64 messages, 16 MiB, order kept) | `LiveBufferFileTests.cs`, `LiveBufferTests.cs` |
| AC9 | `LiveSenderEnqueueAfterTokenRefusedStillBuffers`; existing `LiveSenderIsSendingPerModeAndSetting`; `TrayMenuCreateWithoutBufferStatusShowsNoBufferLine` | `LiveSenderTests.cs`, `TrayMenuTests.cs` |
| AC10 | `LiveBufferAddAndStatusTouchNoFileSystem` (folder does not exist after `Add` and `Status` before `RunAsync`); `LiveBufferNextAsyncDiscardsFileDamagedAfterLoad` (the content is read at send, not held); `LiveBufferAddPendingBeyondMemoryBoundDropsOldestPending`; N7: check, Reviewer, step 8 — no file of `src/Censio.Contracts` or `src/Censio.Server*` in the diff | `LiveBufferTests.cs` |
| AC11 | check: Reviewer, step 8 — `README.md` and `docs/USER_GUIDE.md`, English then German, name the keys with defaults and ranges, the `Buffer:` line and its three forms, the folder and the log lines, and how to try S1 to S3 | — |

## Approach

**`LiveBuffer`** (new, `Censio.Companion.Core`) replaces `LiveSender`'s queue. It holds, under one lock, an index of the
stored messages (sequence, identity, time taken, JSON length, about 64 bytes each), a list of messages waiting for their
write, and, after a write failure, a list of memory-only messages; the JSON of a stored message is not held.

- `Add` stamps the message with `TimeProvider.GetUtcNow()`, appends it to the pending list, applies the bounds and
  signals the writer; it never touches the file system or waits. After `DisposeAsync` it ignores the message.
- `RunAsync` creates the folder, then loads: the top-level files whose whole name is a message name
  (`<19 decimal digits>.msg`) or a temporary name (`<19 digits>.tmp`), case-sensitive; any other name and a folder are
  left alone; a link with a buffer name is never followed: it is deleted as a link and counted unreadable. A temporary file is deleted and counted
  dropped. A message file is read up to its header (68 bytes) and checked with `LiveBufferFile.ReadHeader` against its
  length; a refused one is deleted and counted. The next sequence is the largest seen plus one. Then the age and size
  bounds apply, `Live buffer holds <n> messages from an earlier run` is logged for n > 0, the unreadable count is logged,
  and pending messages added during the load are written after the loaded ones. Then it writes pending messages one at
  a time — `<seq>.tmp` created new, the bytes of `LiveBufferFile.Format`, `Flush(true)`, `File.Move` to `<seq>.msg`
  without overwrite — and deletes the files the bounds or acknowledgements removed, until cancelled.
- A failure to create the folder, enumerate it or write a file (`IOException`, `UnauthorizedAccessException`,
  `SecurityException`, `NotSupportedException`) switches to memory only for the rest of the run: the failed message and
  every later one go to the memory list, the stored ones are still sent first, `LiveBufferNotWritable` is logged once.
  A failed delete is ignored: the file is read again at the next start and resent, and the server acknowledges it as known.
- `NextAsync` waits until the loader finished and a stored or memory message exists, drops the messages older than the
  age limit (never the one it returns), reads the first stored file whole with `LiveBufferFile.Read` (an unreadable one
  is deleted, counted, logged with count 1, and the next tried), marks it in flight and returns it. `Acknowledge(id)`
  removes the in-flight message and queues its delete; `ReleaseInFlight` clears the mark after a failed connection, so
  the same message (same identity) comes next.
- Bounds, after every `Add`, at load and in `NextAsync`: a message whose age (now minus taken, negative counts as 0)
  exceeds `MaxAge` is dropped; while the JSON of all waiting messages exceeds `MaxBytes` and at least two remain that are
  not in flight, the oldest not in flight is dropped; the memory-only list keeps `MaxMemoryMessages` and
  `MaxMemoryBytes`; the pending list keeps `MaxMemoryBytes`, beyond which its oldest is dropped (only with a disk slower
  than the pipe). Each drop increments the run's dropped count and the unlogged count.
- Seams: N4 through a folder that does not exist until `RunAsync`; N3 through a file damaged after the load, which
  `NextAsync` must then report unreadable (the content is read at send). Disk-full is not produced in tests; a file named
  `Buffer` in place of the folder and a read-only file stand for a write failure.

**`LiveBufferFile`** — little-endian header: `CNSB`, format version 1 (int32), time taken (UTC `DateTime` ticks, int64,
from 0 to `DateTime.MaxValue.Ticks`), identity (16 bytes, `Guid.TryWriteBytes`, not all zero), JSON length (int32, 1 to
`ContractReader.MaxMessageBytes`, checked before any allocation), SHA-256 of the JSON (32 bytes); then the JSON. The file
length must equal 68 plus the length. Every check failure is `Unreadable`, never an exception; the JSON is not parsed (the
server reads it).

**`LiveSender`** keeps its connection logic; `Enqueue` writes the JSON and calls `_buffer.Add`; `SendLoopAsync` races
`_buffer.NextAsync` against the failure as `WaitForMessageAsync` does today, calls `Acknowledge` after an
acknowledgement (accepted or refused) and `ReleaseInFlight` in the attempt's `finally`; `LogDropped` reads
`TakeUnloggedDropped`, with today's timing. To stay within seven constructor parameters, address, token reader and
`isLocal` move into the internal `LiveSendTarget`.

**Settings.** `ReadLocalServer`, for `forward` after the token and for `local` after the port, reads `bufferHours` then
`bufferMegabytes` with one shared helper that generalises `TryReadPort` to a range (`TryReadWholeNumber`, used for all
three keys); problem texts exactly as spec E1.

**App** (`App.xaml.cs`, no test project): `StartLiveSending` gains `localAppData`, creates
`new LiveBuffer(CompanionFolders.Buffer(localAppData), setting.BufferLimits, TimeProvider.System, _logger)` only after
`IsSending`, runs `buffer.RunAsync` with `Run`, passes it to the factory and keeps it in `_liveBuffer`; `Exit` disposes
the sender and then the buffer inside the existing 250 ms task; `GetServerStatus` sets `Buffer = _liveBuffer?.Status` on
both branches.

**Menu layout.** One greyed entry of kind `LiveBuffer` directly below the `Server:` line, only when `TrayServerStatus.Buffer`
is set: `Buffer: <n> waiting`, then `, <d> dropped` when d > 0, then `, not saved to disk` when memory only; numbers in
invariant digits without separators (`Buffer: 1234 waiting, 3 dropped, not saved to disk`). n counts pending, stored and
memory messages, the one in flight included; d is the run's total.

**Log lines** (`CompanionLog`): `Live buffer holds {Count} messages from an earlier run` (Information),
`Live buffer cannot be written; waiting messages are kept in memory until Exit` (Warning), `Live buffer entries
unreadable: {Count}; discarded` (Warning); `Live messages dropped: {Count}` unchanged. No line holds a path, identity,
content or exception text.

Early draft PR: no

## Options considered

- Storage: one file per message (chosen), an append-only log with a pointer, SQLite, a path from the settings file
  (record 0031's wording, raised by the spec challenge), persisting at `Exit` only — record 0105.
- Limits: settings keys (chosen), command-line switches, falling back to the default on a wrong value, fixed limits,
  age by the pipe's receive time — record 0106.
- Verifying every file's hash at start: costs a read of up to 10 GiB before the first send; checking the header and
  length at start and the hash at send finds the same damage, one message later.
- The buffer owned and run by `LiveSender`: its run ends at a final refusal, which would stop the writer against S7.
- A new area document `live-buffer.md`: the buffer is part of live sending and `live-sending.md` stays under
  `AREA_MAX_WORDS`; a separate area adds an index row and cross-links for no reader.
- A gap marker message: contradicts `contract.md` (silence is the gap); out of scope in the spec.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `LiveBuffer`, `LiveBufferEntry`, `LiveBufferStatus`, `LiveBufferLimits`, `LiveBufferFile`, `LiveBufferFileResult`, `LiveBufferFileResultKind`, `LiveSendTarget` | new |
| Censio.Companion.Core | `LiveSender` | queue replaced by the buffer; factories take it |
| Censio.Companion.Core | `CompanionSettings`, `LocalServerSetting`, `CompanionFolders`, `TrayMenu`, `TrayMenuEntryKind`, `TrayServerStatus`, `CompanionLog` | extended |
| Censio.Companion | `App.xaml.cs` | wiring, exit, menu status |

## Signatures (for the Dev's skeleton)

Every new class is sealed; a property is written Type.Member with its type and accessors in parentheses.

- `LiveBuffer` (public sealed class, implements IAsyncDisposable): `LiveBuffer.MaxMemoryMessages` (public const int, 64),
  `LiveBuffer.MaxMemoryBytes` (public const int, 16,777,216),
  `public LiveBuffer(string folder, LiveBufferLimits limits, TimeProvider timeProvider, ILogger logger)`,
  `LiveBuffer.Status` (public `LiveBufferStatus`, get), `public void Add(Guid id, ReadOnlyMemory<byte> utf8Json)`,
  `public Task RunAsync(CancellationToken cancellationToken)`,
  `public Task<LiveBufferEntry> NextAsync(CancellationToken cancellationToken)`, `public void Acknowledge(Guid id)`,
  `public void ReleaseInFlight()`, `public int TakeUnloggedDropped()`, `public ValueTask DisposeAsync()`
- `LiveBufferEntry` (public sealed class): `LiveBufferEntry.Id` (required Guid, get/init), `LiveBufferEntry.TakenAt`
  (required DateTimeOffset, get/init), `LiveBufferEntry.Utf8Json` (required ReadOnlyMemory of byte, get/init)
- `LiveBufferStatus` (public readonly record struct): `LiveBufferStatus.Waiting` (int, get/init),
  `LiveBufferStatus.Dropped` (int, get/init), `LiveBufferStatus.IsMemoryOnly` (bool, get/init)
- `LiveBufferLimits` (public sealed record): `LiveBufferLimits.DefaultHours` (const int, 24), `LiveBufferLimits.MaxHours`
  (const int, 168), `LiveBufferLimits.DefaultMegabytes` (const int, 100), `LiveBufferLimits.MaxMegabytes` (const int,
  10,240), `LiveBufferLimits.BytesPerMegabyte` (const int, 1,048,576), `LiveBufferLimits.Hours` (required int,
  get/init), `LiveBufferLimits.Megabytes` (required int, get/init), `LiveBufferLimits.MaxAge` (TimeSpan, get),
  `LiveBufferLimits.MaxBytes` (long, get), `LiveBufferLimits.Defaults` (static, get)
- `LiveBufferFile` (public static class): `LiveBufferFile.HeaderLength` (const int, 68), `LiveBufferFile.FormatVersion`
  (const int, 1), `public static byte[] Format(LiveBufferEntry entry)`,
  `public static LiveBufferFileResult ReadHeader(ReadOnlySpan<byte> header, long fileLength)`,
  `public static LiveBufferFileResult Read(ReadOnlySpan<byte> file)`, `public static string MessageName(long sequence)`,
  `public static string TemporaryName(long sequence)`,
  `public static bool TryParseMessageName(string name, out long sequence)`,
  `public static bool TryParseTemporaryName(string name, out long sequence)`
- `LiveBufferFileResult` (public sealed class): `LiveBufferFileResult.Kind` (required `LiveBufferFileResultKind`,
  get/init), `LiveBufferFileResult.Entry` (`LiveBufferEntry?`, get/init; from `ReadHeader` with empty JSON),
  `LiveBufferFileResult.Length` (int, get/init)
- `LiveBufferFileResultKind` (public enum): `LiveBufferFileResultKind.Read`, `LiveBufferFileResultKind.Unreadable`
- `LiveSendTarget` (internal sealed class): `LiveSendTarget.Address` (required Uri, get/init), `LiveSendTarget.ReadToken`
  (Func of CancellationToken to Task of string?, nullable, get/init), `LiveSendTarget.IsLocal` (bool, get/init)
- `LiveSender`: `public static LiveSender ForServer(ForwardServerSetting setting, LiveBuffer buffer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`,
  `public static LiveSender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, LiveBuffer buffer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`
- Deleted: `LiveSender.MaxWaitingMessages` and `LiveSender.MaxWaitingBytes` are removed (now `LiveBuffer.MaxMemoryMessages`, `MaxMemoryBytes`).
- `LocalServerSetting.BufferLimits` (`LiveBufferLimits`, get/init, initialised to `LiveBufferLimits.Defaults`)
- `CompanionSettings.BufferHoursKey` (const string, "bufferHours"), `CompanionSettings.BufferMegabytesKey` (const
  string, "bufferMegabytes")
- `public static string Buffer(string localApplicationData)` in `CompanionFolders`
- `TrayServerStatus.Buffer` (`LiveBufferStatus?`, get/init); `TrayMenuEntryKind.LiveBuffer` (appended last)
- `public static string FormatLiveBuffer(LiveBufferStatus status)` in `TrayMenu`
- `CompanionLog`: `public static partial void LiveBufferRestored(ILogger logger, int count);`,
  `public static partial void LiveBufferNotWritable(ILogger logger);`,
  `public static partial void LiveBufferUnreadable(ILogger logger, int count);`

No nested type, no enum name ending in Flags or Enum, at most seven parameters, no keyword names (Defaults, not
Default). The skeleton holds nothing for real; the `App.xaml.cs` wiring is written in step 6.

## Test files

`tests/Censio.Companion.Core.Tests/LiveBufferTests.cs`, `LiveBufferFileTests.cs`, `LiveBufferLimitsTests.cs` (new);
`LiveSenderTests.cs`, `CompanionSettingsTests.cs`, `TrayMenuTests.cs`, `CompanionFoldersTests.cs` (extended);
`tests/Censio.Server.Tests/TrendSeriesBuilderTests.cs`, `SparklineBuilderTests.cs` (tag only).

Existing call sites, adapted by the Tester: `LiveSenderTests.cs` lines 898, 1056, 1085, 1433, 1445 (factories gain a
buffer: a helper creates one in a per-test temporary folder, runs it and disposes it in `CleanupAsync`);
`LiveSenderEnqueueBeyondCountBoundDropsOldest`, `LiveSenderEnqueueBeyondByteBoundDropsOldest`,
`LiveSenderRunLogsDroppedCountOnce`, `LiveSenderRunLogsUnloggedDroppedCountWhenStoppedOrDisposed` and
`LiveSenderEnqueueAfterDisposeAsyncIsIgnored` use `MaxWaitingMessages`: they run with a memory-only buffer (folder
blocked) and `LiveBuffer.MaxMemoryMessages`, or with small limits. `LiveSenderRunResendsUnacknowledgedMessageFirstWithSameId`
keeps its inputs. `App.xaml.cs` is the Dev's.

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs` (no test project, record 0004; thin wiring, read in
review).

## Areas

- `docs/areas/live-sending.md (+900 words)` (about 2,280 words now, so about 3,200 after, under `AREA_MAX_WORDS`): *Waiting messages* becomes *Buffer* (what is buffered, folder and file
  format, limits, order, resend, drops, menu line, E2, E3); *Switching on* gains the keys and four problem texts; *Log*
  from seven to ten lines; *Privacy*, *Accepted risks* (*Memory only* replaced by the buffer's: a power cut, a clock
  change, a writer of the player's folder), *Scope*, *Related decisions* (0105, 0106), *Not here* (drop "#41").
- `docs/areas/companion.md (+120 words)` (about 3,770 now): menu order with the `Buffer:` line; *Settings file* names the two keys;
  *Folders* the buffer folder.
- `docs/areas/companion-log.md (+40 words)`: the three new lines.
- `docs/areas/README.md (+0 words)`: the live-sending and replay-sending rows name the buffer instead of "#41".
- `docs/areas/contract.md`, `replay-sending.md`, `ingest.md`, `pipe.md`: "#41" in their pointers becomes a link to
  `live-sending.md`, *Buffer*.

## Documentation updates

All by the Dev once the code is green, against that code:

- The area documents of *Areas*.
- `README.md`: the settings table (English and German) gains `bufferHours` and `bufferMegabytes`; the forward-mode
  paragraph at line 193 and its German twin describe the buffer instead of memory-only waiting; the menu-line tables
  gain the `Buffer:` line.
- `docs/USER_GUIDE.md`: *What is sent and what is lost* (line 472) and its German twin, the settings and menu sections:
  folder, limits, defaults, the `Buffer:` line, the log lines, and a short way to try S1 to S3 (stop the server, wait,
  start it; small limits for S3).
- `docs/ARCHITECTURE.md` line 66: waiting messages kept in the persistent buffer (link 0105, `live-sending.md`), the
  sentence "The persistent buffer is #41" removed.
- `.squad/project.md` (text reported by the Dev, written by the orchestrator): the guarantee line links
  `live-sending.md` (*Buffer*) and records 0031 and 0105 instead of "area document with #41"; *File paths the Companion
  writes* gains `LiveBuffer`, `CompanionFolders.Buffer`; *Parsing of external input* gains `LiveBufferFile`; the
  configuration-option entry names `bufferHours` and `bufferMegabytes` in `CompanionSettings`.

Sweep: `#41` in `docs/areas/*.md`, `README.md`, `docs/USER_GUIDE.md` and `docs/ARCHITECTURE.md` (every hit above),
"64 messages", "16 MiB" and "in memory" in the same files, and the lists of `settings.json` keys in `companion.md`,
`README.md` and `USER_GUIDE.md` (both languages).

## Architecture check

The guarantee "Unsent ticks are buffered within a bound and delivered in order; dropped ones show as a gap, never as
zeros" is fulfilled, not weakened. Windows-only code stays in `App.xaml.cs`; the buffer is platform-neutral
(record 0004). The contract and the server are unchanged. Readers return typed results (record 0033).

## Security considerations

- [x] Limits: the JSON length is checked against 25,165,824 bytes on the raw header field before any allocation, and the
  file length against 68 plus it before the content is read; no decoding happens. The settings keys are checked on the
  raw JSON number text (digits only) before parsing.
- [x] Exceptions: file system exceptions of the buffer end as memory only or a discarded file, with the fixed log lines
  above; no exception text, path or message content reaches the menu or the log.
- Paths: only `CompanionFolders.Buffer` under `%LOCALAPPDATA%`, names built from a sequence number; nothing from the
  settings file or received data; a link is never followed.
- The buffer holds what the server receives (island names, economy, trade routes), never the token; it stays when the
  Companion is removed, like the recordings.

## Decision records

- `docs/decisions/0105-unsent-live-messages-kept-one-file-each-in-a-fixed-buffer-folder.md` (Proposed; amends 0031's path consequence)
- `docs/decisions/0106-buffer-limits-are-settings-keys-in-hours-and-megabytes.md` (Proposed)
- `docs/decisions/0104-…` (unreleased, edited in place: its memory bound stays the bound of memory-only mode)

## Out of scope / follow-ups

As the spec's *Out of scope*. No follow-up issue.
