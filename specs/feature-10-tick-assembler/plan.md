# Plan: Tick assembler and island identity

Source: Issue #10 | [spec.md](spec.md)
Status: Draft
Tier: standard — a new behavior of `Censio.Pipe` (tick assembly) that adds an input-size bound but creates, changes or removes no security control.
Security areas touched: Parsing of external input (the tick limits on decoded pipe messages in `PipeTickBuilder`); a dependency (`Microsoft.Extensions.TimeProvider.Testing`, test project only)

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
| 6 | Map duplicates are summed, so a map's `Count` is its decoded pair count | `PipeMessageParser.cs:210-224`, `docs/areas/pipe.md` | confirmed |
| 7 | `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 is the latest version on nuget.org | `https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/index.json`, read 2026-11-01 | confirmed |
| 8 | `FakeTimeProvider.Advance` fires a `Task.Delay(TimeSpan, TimeProvider, CancellationToken)` exactly when the clock reaches its due time and not 1 ms before | to verify: the AC2 tests at 29.999 s and 30 s, Tester, step 5 | unverified |
| 9 | Memory of an open tick at the limits of AC10 (256 islands, 524,288 entries and pairs), worst shape (all entries, no pairs; and all pairs) | to verify: Dev, step 6 — fill a `PipeTickBuilder` at the limits in a scratch program and read `GC.GetTotalMemory(true)` before and after; record the larger figure in `areas/pipe.md`; above 256 MiB the Dev stops and the Lead lowers the limit | unverified |
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
| AC2 | `PipeTickAssemblerQuietPeriodClosesTickWhileReadPending`; `PipeTickAssemblerBeforeQuietPeriodDeliversNothing` (29.999 s); `PipeTickAssemblerPendingReadDeliveredAfterQuietClose` (same frame, source read once, not cancelled); `PipeTickBuilderFrameAtQuietDeadlineClosesTickAsQuietPeriodFirst`; `PipeTickBuilderFrameAfterQuietCloseOpensTickWithSameGameTime` | Assembler, Builder |
| AC3 | `PipeTickBuilderRepeatedIslandKeyClosesTickAsIslandRepeated` | Builder |
| AC4 | `PipeTickBuilderDifferentKeyPartJoinsSameTick` (DataRow: other session GUID, other area index) | Builder |
| AC5 | `PipeTickBuilderAllIslandsWithoutEntriesMarkTickNoDataYet`; `PipeTickBuilderMixedIslandsMarkOnlyEmptyIslands`; `PipeTickBuilderSkippedFrameLeavesIslandAbsent` | Builder |
| AC6 | `PipeTickBuilderSessionStartDeliversHeadline`; `PipeTickBuilderSessionEndAtStreamStartIsRepeated`; `PipeTickBuilderSecondSessionEndIsRepeated`; `PipeTickBuilderSessionEndAfterIslandFrameIsSessionEnd`; `PipeTickBuilderSessionEndAfterStartIsSessionEnd`; `PipeTickBuilderTwoStartsDeliverTwoStarts` | Builder |
| AC7 | `PipeTickBuilderSessionEventClosesTickFirst` (DataRow start, end) | Builder |
| AC8 | `PipeTickBuilderNamesAndHeadlineDeliveredUnchanged` (`R_mische K_ste`, `Zycada `, empty headline); `PipeTickBuilderRenamedIslandKeepsKey` | Builder |
| AC9 | `PipeTickBuilderErrorAndVersionLeaveTickOpen`; `PipeTickBuilderEndOfStreamClosesTickAsStreamEnded`; `PipeTickBuilderStreamEndingErrorClosesTickBeforeError` (DataRow truncated, oversized, read failed); `PipeTickAssemblerAfterEndReturnsEnd` | Builder, Assembler |
| AC10 | `PipeTickBuilderTickAtLimitsKeepsAllIslands` (exactly 256 islands; exactly 524,288 items); `PipeTickBuilderIslandOverIslandLimitGivesTickLimit`; `PipeTickBuilderIslandOverItemLimitGivesTickLimit`; `PipeTickBuilderDroppedKeyDoesNotCountAsRepeated`; `PipeTickBuilderEachFrameOverLimitGivesOwnTickLimit` | Builder |
| AC11 | `PipeTickAssemblerCancelledReadThrowsAndEndsStream` (source saw its token cancelled; next request returns the end; no tick delivered) | Assembler |
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
  1. Queue not empty → dequeue. Ended → end.
  2. Start the read if none is pending: `_pending = ReadStampedAsync(_readCancellation.Token)`, which awaits the
     source and stamps `timeProvider.GetUtcNow()` in its own continuation — so the receive time is when the read
     completed, not when the caller asked again.
  3. No open tick → await `_pending.WaitAsync(token)`. Open tick → if `now >= QuietDeadline` and `_pending` has not
     completed → `CloseIfQuiet(now)`. Else race `_pending` against `Task.Delay(QuietDeadline - now, timeProvider,
     delayToken)` (the delay's own source, linked to `token`, cancelled when the read wins). The read wins → feed
     `Add(result, stamp)`, enqueue, loop if nothing came out. The delay wins → `CloseIfQuiet(timeProvider.GetUtcNow())`.
     The pending read is never cancelled by the wait; it stays in `_pending` for the next request (S2).
  4. `token` cancelled (at any await) → cancel `_readCancellation`, discard builder and queue, set ended, rethrow
     `OperationCanceledException` (E9). The reader then ends its stream (fact 3).
  - `Dispose` cancels and disposes `_readCancellation`. One caller, no concurrent requests (not thread-safe, as the
    reader).
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
`FakeTimeProvider`) against the test-only package (chosen).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Pipe` | `IPipeMessageSource.cs` | new interface |
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
- Existing test call sites of a changed signature: none (`PipeMessageReader`'s members are unchanged).
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
  cancellation). *Limits and errors* gains the two tick limits, counted after decoding, the *tick limit* item with
  the key only, and the memory bound measured in fact 9. *Residual risks* gains: the unconfirmed timing and island
  count; a stall over 30 seconds splits a tick into two with one game time; a game sending one island twice in a
  tick splits it as *island repeated*; a replay ending at its last frame closes it as *stream ended*. *Related
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
  frame and 1,024-pair map limits apply before, unchanged.
- [x] Every exception that can reach a user-visible failure reason or message: none — the assembler surfaces only
  `OperationCanceledException` on the caller's cancellation and `ObjectDisposedException` after `Dispose`; nothing
  reaches a user yet, and no item carries a name, headline or entry in an error.

## Decision records

- `docs/decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md` (Proposed), index row added.

## Out of scope / follow-ups

- As in the spec. #12 implements `IPipeMessageSource` for replay and drives the clock from recorded times; #11 wraps
  `PipeMessageReader` over the named pipe and hands it to `PipeTickAssembler`.

## Challenge notes

Devil's Advocate, mode `plan`: no major objections, five minor ones (verbatim).

1. **minor: the receive time can be wrong when the quiet-period timer and a finishing read race.** This can split a tick in a way the stamps would not.
   - **Evidence:** plan, *Approach*, assembler step 3, "The delay wins → `CloseIfQuiet(timeProvider.GetUtcNow())`". `ReadStampedAsync` takes its stamp inside its own continuation. So on the live clock a read can stamp at 29.9999 s and complete its task just after the delay fires at 30 s. The tick is then closed as *quiet period*, and the frame with the same timestamp opens a second tick, although by its stamp it belonged to the first.
   - **Instruction:** when the delay wins, check `_pending.IsCompletedSuccessfully` first. If it has completed, feed `Add(result, stamp)` and let the builder's step 2 decide by the stamp. Call `CloseIfQuiet` only when the read is still pending.

2. **minor: a request whose token is already cancelled can return a queued item instead of throwing.**
   - **Evidence:** plan, assembler step 1, "Queue not empty → dequeue", and step 4, "token cancelled (at any await)". The dequeue path has no await, so it never sees the token. The reader checks the token first (`PipeFrameReader.cs:76`, `ThrowIfCancellationRequested`), and spec E9/AC11 say a cancelled request ends in cancellation.
   - **Instruction:** call `cancellationToken.ThrowIfCancellationRequested()` at the start of `ReadAsync`, before the queue, and run it through the same cancel/discard/end path as step 4. Add a DataRow or a test for a pre-cancelled token while items are queued.

3. **minor: in live use, receive times depend on how often the caller asks.**
   - **Evidence:** plan, assembler step 2, "Start the read if none is pending". The next read only starts when the caller asks again, and the receive time is stamped when that read completes. A live consumer that pauses for more than 30 s (for example, a WebSocket send in #11/#13) stamps the buffered frames late. That splits ticks and moves receive times, despite the spec's NFR "never on how fast they are processed". AC12 only covers this with fixed injected times.
   - **Instruction:** no code change. In `docs/areas/pipe.md`, *Order and end* and *Residual risks*, state that the caller must keep requesting and that a slow consumer belongs behind the tick buffer (#41) or #11's decoupling.

4. **minor: the memory measurement (plan fact 9) must use objects built the way the parser builds them.**
   - **Evidence:** `PipeMessageParser.cs:222` allocates a new `Dictionary<int,int>(pairCount)` per map. That is two dictionaries per entry, even when they are empty. The "all entries, no pairs" case is therefore the heaviest per counted item, at roughly 200+ bytes per entry.
   - **Instruction:** build the scratch tick's frames through `PipeMessageParser` (or with two new empty dictionaries per entry), not with shared empty dictionaries, and record the larger of the two shapes.
   - **Also fix:** fact 9 says "Dev, step 6", but tasks.md puts the measurement in task 3. Align them on task 3.

5. **minor: the `ObjectDisposedException` in *Security considerations* has no test.**
   - **Evidence:** plan, *Security considerations* says the assembler surfaces `ObjectDisposedException` after `Dispose`. No test in *Tests per criterion* covers `Dispose`.
   - **Instruction:** the Tester adds `PipeTickAssemblerReadAfterDisposeThrowsObjectDisposed` and `PipeTickAssemblerDisposeCancelsPendingRead` (the source saw its token cancelled). Alternatively, the Lead drops the claim if `Dispose` should not throw.
