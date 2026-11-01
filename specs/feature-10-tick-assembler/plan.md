# Plan: Tick assembler and island identity

Source: Issue #10 | [spec.md](spec.md)
Status: Revised (1)
Tier: standard — a new behavior of `Censio.Pipe` (tick assembly) that adds an input-size bound but creates, changes or removes no security control.
Security areas touched: Parsing of external input (the tick limits on decoded pipe messages in `PipeTickBuilder`; the map sizing in `PipeMessageParser.TryReadMap`); a dependency (`Microsoft.Extensions.TimeProvider.Testing`, test project only)

## Problem / root cause

See [spec.md](spec.md): the reader delivers single messages; nothing groups them into ticks with an island identity
and typed session events.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The island frame carries session ID, island ID, area index (bytes), session GUID (int32), area name, timestamp (int64) and entries | `src/Censio.Pipe/AreaProductionStatisticsMessage.cs` | confirmed |
| 2 | Names and headlines arrive untrimmed with `_` kept | `docs/areas/pipe.md` (*Frame layout*), `PipeMessageParser` | confirmed |
| 3 | A cancelled read ends the reader's stream: every later read returns the end | `src/Censio.Pipe/PipeFrameReader.cs:70`, `:81-83` (`_ended = true`) | confirmed |
| 4 | The reader returns only `PipeReadResult`, no time and no frame length | `src/Censio.Pipe/PipeReadResult.cs`, `PipeMessageReader.cs:50` | confirmed |
| 5 | Truncated, oversized and read-failed errors end the stream; the other kinds skip a frame | `docs/areas/pipe.md` (*Limits and errors*), `PipeErrorKind.cs` | confirmed |
| 6 | Map duplicates are summed, so a map's `Count` is its decoded pair count; but the map is pre-sized to the wire pair count, so up to 1,024 wire pairs of one GUID keep space for about 1,031 entries while counting as 1 | `PipeMessageParser.cs:210-224` (`new Dictionary<int, int>(pairCount)` at line 222); Security's scratch measurement: 256 frames, 63 entries each with two 1,024-pair maps of one GUID, 48,384 counted items, 685.0 MiB retained | confirmed |
| 6a | After `TrimExcess()` a dictionary whose decoded count is below its wire count keeps a capacity near its `Count` (`Capacity` property, .NET 9 and later) | to verify: `PipeMessageParserDuplicateMapKeysKeepNoSpareCapacity`, Tester, step 5 | unverified |
| 7 | `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 is the latest version on nuget.org | `https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/index.json`, read 2026-11-01 | confirmed |
| 8 | `FakeTimeProvider.Advance` fires a `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` exactly when the clock reaches its due time and not 1 ms before | to verify: the AC2 tests at 29.999 s and 30 s, Tester, step 5 | unverified |
| 9 | Memory of an open tick at the limits of AC10 (256 islands, 524,288 entries and pairs) in its worst shape, after the `TrimExcess` fix; Security measured 124.1 MiB for the all-entries shape before it | to verify: Dev, task 3 — build frames with `PipeFrameBuilder`-style bytes and decode them through `PipeMessageParser` (never shared empty dictionaries), fill a `PipeTickBuilder` at the limits in three shapes (all entries without pairs; all distinct pairs; 1,024-pair maps of one repeated GUID, Security's shape), read `GC.GetTotalMemory(true)` before and after, record the largest figure in `areas/pipe.md`; above 256 MiB the Dev stops and the Lead lowers the limit | unverified |
| 10 | Real ticks hold well under 256 islands and 524,288 entries and pairs (estimate: about 50 islands × 200 goods × 11) | to verify: recordings of #17 (M2), not in this change; listed as a residual risk | unverified |
| 11 | The timing (frames about 1 s apart, ticks about 2 min), the shared timestamp, the zero-entry tick after a load, the double `SessionEnd` and the connect sequence | to verify: the recordings of #17 (M2), not in this change; today third-party observations (issue #10) | unverified |
| 12 | No README, user guide, `SECURITY.md` or `CONTRIBUTING.md` statement enumerates what `Censio.Pipe` does except `CONTRIBUTING.md:143` (already names tick assembly) | `grep -i "tick\|Censio.Pipe\|typed messages"` over `docs/`, `README.md`, `SECURITY.md`, `.squad/project.md` | confirmed |

## Tests per criterion

Builder = `PipeTickBuilderTests` (`tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs`), Assembler =
`PipeTickAssemblerTests` (`tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs`). Builder tests feed
`PipeReadResult` values built directly from messages with explicit receive times; assembler tests use
`ScriptedPipeMessageSource` and `FakeTimeProvider`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeTickBuilderFramesWithOneTimestampFormOneTick` (game time, islands in order, key, session ID, name, entries); `PipeTickBuilderTickReceiveTimeIsLastFrame`; `PipeTickBuilderChangedTimestampClosesTickAsGameTimeChanged` (DataRow higher, lower) | Builder |
| AC2 | `PipeTickAssemblerQuietPeriodClosesTickWhileReadPending`; `PipeTickAssemblerCompletedReadPastDeadlineGivesQuietTickThenFrame` (read completes, clock moves past the deadline before the next request: quiet-period tick, then the frame's next tick, no exception); `PipeTickAssemblerBeforeQuietPeriodDeliversNothing` (29.999 s); `PipeTickAssemblerPendingReadDeliveredAfterQuietClose` (same frame, source read once, not cancelled); `PipeTickBuilderFrameAtQuietDeadlineClosesTickAsQuietPeriodFirst`; `PipeTickBuilderFrameAfterQuietCloseOpensTickWithSameGameTime` | Assembler, Builder |
| AC3 | `PipeTickBuilderRepeatedIslandKeyClosesTickAsIslandRepeated` | Builder |
| AC4 | `PipeTickBuilderDifferentKeyPartJoinsSameTick` (DataRow: other session GUID, other area index) | Builder |
| AC5 | `PipeTickBuilderAllIslandsWithoutEntriesMarkTickNoDataYet`; `PipeTickBuilderMixedIslandsMarkOnlyEmptyIslands`; `PipeTickBuilderSkippedFrameLeavesIslandAbsent` | Builder |
| AC6 | `PipeTickBuilderSessionStartDeliversHeadline`; `PipeTickBuilderSessionEndAtStreamStartIsRepeated`; `PipeTickBuilderSecondSessionEndIsRepeated`; `PipeTickBuilderSessionEndAfterIslandFrameIsSessionEnd`; `PipeTickBuilderSessionEndAfterStartIsSessionEnd`; `PipeTickBuilderTwoStartsDeliverTwoStarts` | Builder |
| AC7 | `PipeTickBuilderSessionEventClosesTickFirst` (DataRow start, end) | Builder |
| AC8 | `PipeTickBuilderNamesAndHeadlineDeliveredUnchanged` (`R_mische K_ste`, `Zycada `, empty headline); `PipeTickBuilderRenamedIslandKeepsKey` | Builder |
| AC9 | `PipeTickBuilderErrorAndVersionLeaveTickOpen`; `PipeTickBuilderEndOfStreamClosesTickAsStreamEnded`; `PipeTickBuilderStreamEndingErrorClosesTickBeforeError` (DataRow truncated, oversized, read failed); `PipeTickAssemblerAfterEndReturnsEnd` | Builder, Assembler |
| AC10 | `PipeTickBuilderTickAtLimitsKeepsAllIslands` (exactly 256 islands; exactly 524,288 items); `PipeTickBuilderIslandOverIslandLimitGivesTickLimit`; `PipeTickBuilderIslandOverItemLimitGivesTickLimit`; `PipeTickBuilderDroppedKeyDoesNotCountAsRepeated`; `PipeTickBuilderEachFrameOverLimitGivesOwnTickLimit`; `PipeMessageParserDuplicateMapKeysKeepNoSpareCapacity` (a 1,024-pair map of one GUID: `Count` 1, `Capacity` below 8) | Builder, `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` |
| AC11 | `PipeTickAssemblerCancelledReadThrowsAndEndsStream` (source saw its token cancelled; next request returns the end; no tick delivered); `PipeTickAssemblerPreCancelledTokenThrowsWithQueuedItems`; `PipeTickAssemblerReadAfterDisposeThrowsObjectDisposed`; `PipeTickAssemblerDisposeCancelsPendingRead` | Assembler |
| AC12 | `PipeTickAssemblerSynchronousAndHeldReadsGiveSameResults` (one script, completed synchronously in one run and through held reads released later in the other, same fake times); `PipeTickBuilderQuietChecksBeforeDeadlineChangeNothing` | Assembler, Builder |
| AC13 | `PipeTickBuilderNonIslandItemsDoNotRestartQuietPeriod` (DataRow error, version; one per second for 31 s) | Builder |

## Approach

Two layers in `Censio.Pipe` (record 0034):

- **`PipeTickBuilder`** — pure and synchronous, no clock, no I/O. `Add(result, receiveTime)` returns the items the
  result produces, in order; `CloseIfQuiet(now)` returns the quiet-period tick when `now` reached
  `QuietDeadline`. State: the open tick (game time, islands, key set, item count, last island receive time), a flag
  `_endable` (an island frame or a `SessionStart` came since the last `SessionEnd`; starts `false`), and an ended
  flag. `Add` in this order:
  1. Ended: return nothing (the driver never calls it then).
  2. If a tick is open and `receiveTime >= QuietDeadline`: close it as *quiet period* first (AC2, determinism).
  3. By result: island frame → step 4; `SessionStart` → close open tick (*session event*), `_endable = true`, emit
     session start; `SessionEnd` → close open tick, emit *session end* when `_endable` else *repeated session end*,
     `_endable = false`; `Version` → emit version; skip-error → emit error; stream-ending error (truncated,
     oversized, read failed) or end → close open tick (*stream ended*), emit the error, then end, set ended.
  4. Island frame: `_endable = true`. If a tick is open and its timestamp differs → close (*game time changed*); else
     if the key is in the key set → close (*island repeated*). Then the limit check on the (possibly new) tick:
     items of the frame = entries + workforce pairs + building pairs (decoded counts); if islands + 1 > 256 or items
     + frame items > 524,288 → emit *tick limit* with the key; nothing is added (a frame that alone exceeds the item
     limit on an empty tick opens no tick). Otherwise add the island, its key, its items, and set the last island
     receive time.
  - A closed tick's receive time is the receive time of its last island frame; *no data yet* when every island has
    no entries.
- **`PipeTickAssembler`** — the async driver over `IPipeMessageSource` and a `TimeProvider`; it owns a
  `CancellationTokenSource` for the source reads and a queue of pending items. `ReadAsync(token)`:
  1. `ThrowIfDisposed`; `cancellationToken.ThrowIfCancellationRequested()` through the cancel path of step 4; then
     queue not empty → dequeue; ended → end.
  2. Start the read if none is pending: `_pending = ReadStampedAsync(_readCancellation.Token)`, which awaits the
     source and stamps `timeProvider.GetUtcNow()` in its own continuation — so the receive time is when the read
     completed, not when the caller asked again.
  3. In this order, in a loop until an item is queued:
     a. `_pending` has completed → feed `Add(result, stamp)` (the builder's step 2 closes a due tick as *quiet
        period* first, by the stamp), clear `_pending`, enqueue.
     b. No open tick → await `_pending.WaitAsync(token)`, then a.
     c. Open tick and `now >= QuietDeadline` → `CloseIfQuiet(now)`, enqueue.
     d. Open tick and `now < QuietDeadline` (the only case that computes a delay, always positive) → race `_pending`
        against `Task.Delay(QuietDeadline - now, timeProvider, delayToken)` (the delay's own source, linked to
        `token`, cancelled when the read wins); whichever wins, go back to a — so a read that completed in the same
        instant is fed by its stamp before any quiet close (c).
     The pending read is never cancelled by the wait; it stays in `_pending` for the next request (S2).
  4. `token` cancelled (at any await) → cancel `_readCancellation`, discard builder and queue, set ended, rethrow
     `OperationCanceledException` (E9). The reader then ends its stream (fact 3).
  - `Dispose` cancels and disposes `_readCancellation` (the pending read sees its token cancelled) and marks the
    assembler disposed; a later `ReadAsync` throws `ObjectDisposedException`. One caller, no concurrent requests (not thread-safe, as the
    reader).
- **`PipeMessageParser.TryReadMap`** — after the loop, `map.TrimExcess()` when `map.Count < pairCount`, so a map
  keeps space for its distinct GUIDs only and the tick limit's decoded count bounds what is kept (Security B1). Its
  results are unchanged; only the memory a duplicate-key map holds shrinks.
- **`IPipeMessageSource`** — `ReadAsync(CancellationToken)`; `PipeMessageReader` implements it unchanged; #12's
  replay implements it later and drives the same `TimeProvider` from the recorded times, or feeds `PipeTickBuilder`
  directly.
- Nothing is logged; `ToString()` of the new types is not overridden, so no payload text leaves through them.
- No user-visible output: the items are API values. Layout of `areas/pipe.md`: below.

## Options considered

Weighed in record [0034](../../docs/decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md):
no quiet period (lost: two-minute delay for the last tick); receive times from the source instead of a clock (lost:
time from two places); one async type (lost: every rule tested through timers); the tick limit as a reader error
kind (lost: changes the reader's error kinds). Test clock: a hand-written `TimeProvider` with timers (lost: the
timer semantics of `CreateTimer` are easy to get wrong, and `docs/UNIT_TESTS.md` already prescribes
`FakeTimeProvider`) against the test-only package (chosen). Map memory (Security B1): count wire pairs instead of
decoded ones (lost: the message carries no wire count, so the reader's message types would grow a field for the
assembler's sake, and an all-duplicates map would still count far more than it keeps); stop pre-sizing maps (lost:
every normal map grows by rehashing); `TrimExcess` when duplicates were summed (chosen: normal maps keep their single
allocation, a duplicate-key map keeps space for its distinct GUIDs only).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Pipe` | `IPipeMessageSource.cs` | new interface |
| `Censio.Pipe` | `PipeMessageParser.cs` | `TryReadMap` trims a map with duplicate GUIDs; results unchanged |
| `Censio.Pipe` | `PipeMessageReader.cs` | implements `IPipeMessageSource`; behavior unchanged |
| `Censio.Pipe` | `PipeIslandKey.cs` | new key (session GUID, island ID, area index) |
| `Censio.Pipe` | `PipeTickIsland.cs`, `PipeTick.cs`, `PipeTickCloseReason.cs` | new tick types |
| `Censio.Pipe` | `PipeTickResult.cs`, `PipeTickResultKind.cs` | new result of the assembler |
| `Censio.Pipe` | `PipeTickLimits.cs` | new constants |
| `Censio.Pipe` | `PipeTickBuilder.cs` | new pure core |
| `Censio.Pipe` | `PipeTickAssembler.cs` | new async driver |
| build | `Directory.Packages.props`; `tests/Censio.Pipe.Tests/Censio.Pipe.Tests.csproj` | `PackageVersion` `Microsoft.Extensions.TimeProvider.Testing` `10.11.0`; `PackageReference` without version |

## Signatures (for the Dev's skeleton)

All types in namespace `Censio.Pipe`, public, with XML documentation and regions.

```csharp
public interface IPipeMessageSource
{
    ValueTask<PipeReadResult> ReadAsync(CancellationToken cancellationToken);
}

public sealed class PipeMessageReader : IPipeMessageSource   // existing ReadAsync unchanged

public readonly record struct PipeIslandKey
{
    public PipeIslandKey(int sessionGuid, byte islandId, byte areaIndex);
    public int SessionGuid { get; }
    public byte IslandId { get; }
    public byte AreaIndex { get; }
}

public sealed class PipeTickIsland
{
    public required PipeIslandKey Key { get; init; }
    public required byte SessionId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<ProductionEntry> Entries { get; init; }
    public bool HasNoEntries { get; }            // Entries.Count == 0
}

public enum PipeTickCloseReason { GameTimeChanged, QuietPeriod, IslandRepeated, SessionEvent, StreamEnded }

public sealed class PipeTick
{
    public required long GameTime { get; init; }
    public required DateTimeOffset ReceiveTime { get; init; }
    public required IReadOnlyList<PipeTickIsland> Islands { get; init; }
    public required PipeTickCloseReason CloseReason { get; init; }
    public bool IsNoDataYet { get; }             // every island HasNoEntries
}

public enum PipeTickResultKind { Tick, SessionStart, SessionEnd, RepeatedSessionEnd, Version, Error, TickLimit, EndOfStream }

public sealed class PipeTickResult
{
    public static PipeTickResult End { get; }
    public static PipeTickResult SessionEnd { get; }
    public static PipeTickResult RepeatedSessionEnd { get; }
    public PipeTickResultKind Kind { get; }
    public PipeTick? Tick { get; }
    public string? Headline { get; }
    public int? Version { get; }
    public PipeError? Error { get; }
    public PipeIslandKey? IslandKey { get; }
    public static PipeTickResult FromTick(PipeTick tick);
    public static PipeTickResult FromSessionStart(string headline);
    public static PipeTickResult FromVersion(int version);
    public static PipeTickResult FromError(PipeError error);
    public static PipeTickResult FromTickLimit(PipeIslandKey key);
}

public static class PipeTickLimits
{
    public const int MaxIslands = 256;
    public const int MaxEntriesAndPairs = 524288;
    public static TimeSpan QuietPeriod { get; }  // 30 seconds
}

public sealed class PipeTickBuilder
{
    public PipeTickBuilder();
    public DateTimeOffset? QuietDeadline { get; }   // last island receive time + QuietPeriod; null without open tick
    public IReadOnlyList<PipeTickResult> Add(PipeReadResult result, DateTimeOffset receiveTime);
    public PipeTickResult? CloseIfQuiet(DateTimeOffset now);
}

public sealed class PipeTickAssembler : IDisposable
{
    public PipeTickAssembler(IPipeMessageSource source, TimeProvider timeProvider);
    public ValueTask<PipeTickResult> ReadAsync(CancellationToken cancellationToken);
    public void Dispose();
}
```

`PipeIslandKey` uses an explicit constructor, not a positional record (no primary constructors). Existing files the
skeleton rewrites: `PipeMessageReader.cs` (interface on the declaration and an `IPipeMessageSource` region for
`ReadAsync`, body unchanged); every other file is new.

## Test files

- `tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs`, `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` (Tester).
- New test double `tests/Censio.Pipe.Tests/ScriptedPipeMessageSource.cs` (Tester): an `IPipeMessageSource` that
  returns queued results, either completed at once or held until the test releases them, counts its reads and
  records whether its token was cancelled; it models no real component.
- The types `PipeTick`, `PipeTickIsland`, `PipeTickResult`, `PipeIslandKey`, `PipeTickLimits` are covered through
  the builder and assembler tests; a small type left under the coverage gate gets its own `{Type}Tests.cs` from the
  Tester.
- `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` (existing; Tester adds `PipeMessageParserDuplicateMapKeysKeepNoSpareCapacity`).
- Existing test call sites of a changed signature: none (`PipeMessageReader`'s and `PipeMessageParser`'s members are
  unchanged); existing parser and reader tests keep their inputs and results, since `TrimExcess` changes no value.
- Files no unit test can cover: none.

## Areas

- `pipe`: gains *Ticks and sessions* (below); no new area.

## Documentation updates

Owned by the Dev once the code is green, unless marked Tester.

- `docs/areas/pipe.md`: *Scope* adds ticks and session events. New section *Ticks and sessions* after *Version
  gate*, with `###` subsections in this order: *Island key*; *Closing a tick* (the five rules as a list with their
  close reasons, the quiet period of 30 seconds on the injected clock, a non-island item does not restart it, the
  receive time and game time of a tick, ticks never reopened); *No data yet* (island *no entries*, which can also
  mean an island without production; tick *no data yet*); *Session events* (start with headline, end, repeated end
  and its rule; no session gate on ticks); *Order and end* (items in stream order, a tick at its close, stream end,
  cancellation; the caller must keep requesting, since a frame's receive time is stamped when its read completes and
  the next read starts only on a request — a slow consumer belongs behind the tick buffer (#41) or #11's
  decoupling). *Reading* gains that a map keeps memory for its distinct GUIDs only. *Limits and errors* gains the two tick limits, counted after decoding, the *tick limit* item with
  the key only, and the memory bound measured in fact 9. *Residual risks* gains: the unconfirmed timing and island
  count; a stall over 30 seconds splits a tick into two with one game time; a game sending one island twice in a
  tick splits it as *island repeated*; a replay ending at its last frame closes it as *stream ended*; a caller that pauses over 30 seconds between requests
  stamps frames late and may split ticks. *Related
  decisions* gains record 0034. *Not here* drops "Assembling ticks from the messages (#10)" and gains "Telling a
  resent or paused tick from a new one (#19, #35)".
- `docs/areas/README.md`: the pipe row's *Scope* adds "ticks and session events, the tick limits"; *Not here* drops
  "ticks".
- `docs/ARCHITECTURE.md`: the `Censio.Pipe` row reads "Framing and parsing the statistics pipe into typed messages
  and assembling them into ticks"; *Data flow* gains one sentence that the Companion groups the messages into ticks
  with an island identity and session events, linking record 0034.
- `.squad/project.md` (*Test doubles*, Tester): `ScriptedPipeMessageSource` with its purpose, and `FakeTimeProvider`
  from `Microsoft.Extensions.TimeProvider.Testing` as the clock of timing tests.
- `README.md`, `docs/USER_GUIDE.md`: none (spec, *Open questions*); the PR description says the user documentation
  follows with #11 and #13.

## Architecture check

No guarantee in `.squad/project.md` exists yet. The pipe format stays confined to `Censio.Pipe` (record 0018); the
reader's strict, fail-closed behavior (record 0033) is unchanged — the assembler never cancels a read except on the
caller's cancellation, which ends the stream as before. `Censio.Pipe` keeps no project reference and stays
platform-neutral.

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding: the tick limits
  count decoded islands, entries and map pairs (after decoding and after the duplicate-key sum); the reader's 1 MiB
  frame and 1,024-pair map limits apply before, unchanged. A map with summed duplicates is trimmed to its distinct
  GUIDs, so the decoded count bounds what is kept (Security B1); fact 9 measures the bound.
- [x] Every exception that can reach a user-visible failure reason or message: none — the assembler surfaces only
  `OperationCanceledException` on the caller's cancellation and `ObjectDisposedException` after `Dispose`; nothing
  reaches a user yet, and no item carries a name, headline or entry in an error.

## Decision records

- `docs/decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md` (Proposed), index row added.

## Out of scope / follow-ups

- As in the spec. #12 implements `IPipeMessageSource` for replay and drives the clock from recorded times; #11 wraps
  `PipeMessageReader` over the named pipe and hands it to `PipeTickAssembler`.

## Challenge

Devil's Advocate, mode `plan`: five minor notes, all accepted.

1. Race of quiet timer and finishing read — accepted: assembler step 3 always feeds a completed read first (3a) and
   computes no delay unless `now < QuietDeadline` (3d); `PipeTickAssemblerCompletedReadPastDeadlineGivesQuietTickThenFrame`.
2. Pre-cancelled token with queued items — accepted: step 1 checks the token before the queue;
   `PipeTickAssemblerPreCancelledTokenThrowsWithQueuedItems` (AC11).
3. Receive times depend on the caller's request rate — accepted, no code change: `areas/pipe.md` *Order and end* and
   *Residual risks* (Documentation updates).
4. Memory measurement through the parser, aligned on task 3 — accepted: fact 9, with Security's duplicate-key shape.
5. `Dispose` untested — accepted: `PipeTickAssemblerReadAfterDisposeThrowsObjectDisposed` and
   `PipeTickAssemblerDisposeCancelsPendingRead` (AC11).

## Security review

- B1 (tick limits do not bound memory: maps pre-sized to the wire count) — resolved: `PipeMessageParser.TryReadMap`
  calls `TrimExcess()` when duplicates were summed (Approach; Options considered), pinned by
  `PipeMessageParserDuplicateMapKeysKeepNoSpareCapacity`; fact 9 adds the duplicate-key shape and builds through the
  parser. Reader results and error behavior stay unchanged (spec, *Compatibility*); only the memory of a
  duplicate-key map shrinks, and `areas/pipe.md` *Reading* says so. Re-checked every counted form: entries (one
  object and two maps each, the all-entries shape), distinct pairs (one slot each) and duplicate pairs (now one slot
  per distinct GUID) — each kept object is counted or bounded by a counted one.
- B2 (`Task.Delay` with a negative delay) — resolved: step 3 feeds a completed read first and computes a delay only
  for `now < QuietDeadline`; `PipeTickAssemblerCompletedReadPastDeadlineGivesQuietTickThenFrame` (AC2).
