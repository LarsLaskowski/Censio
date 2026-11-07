# Plan: Anonymiser for recordings and test fixtures

Source: Issue #20 | [spec.md](spec.md)
Tier: security — the anonymiser is the guard that decides what of a private recording reaches a public repository (raised from `standard` after the plan challenge, M3).
Plan size: tier `security`, within its ceiling; the challenge and review answers below need the room.
Security areas touched: Parsing of external input (recording files: `PipeReplayStream` refactored onto a new `PipeRecordingReader`, new `PipeRecordingAnonymiser`); file paths written (the anonymiser's output and temporary file, from the command line); build configuration (two new projects in `Censio.slnx`).

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The line rules (BOM, CR LF, line limit, empty lines) live privately in `PipeReplayStream`; only header and frame-line parsing are public | `src/Censio.Pipe/PipeReplayStream.cs:143-320` (`LoadFrameAsync`, `ReadHeaderAsync`, `ReadLineAsync`); `PipeRecordingFormat.CheckHeader`, `TryParseFrameLine` | confirmed |
| 2 | `started` is never parsed | `PipeRecordingFormat.TryReadHeaderField` reads only `format` and `version`; area *Line rules* | confirmed |
| 3 | The parser merges a repeated map GUID, so parsed messages lose pairs | `PipeMessageParser.TryReadMap` (`map[guid] = sum`) | confirmed |
| 4 | Entry field order: GUID; generation, consumption, delta, perfect generation, perfect consumption (float); building count, maintenance (int); income (float); profit (int); summed, average productivity (float); workforce map; building map | `PipeMessageParser.ParseEntry`; `docs/areas/pipe.md`, *Message types* | confirmed |
| 5 | The replay's error texts are `file not found`, `not a recording`, `unsupported version`, `invalid line <n>`, `read failed` | `src/Censio.Companion.Core/TrayMenu.cs:285-289` | confirmed |
| 6 | A replay maps any open failure (`IOException`, `UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException`) to `file not found` | `PipeReplayRunner.RunAsync` | confirmed |
| 7 | `Censio.Pipe` exposes no internals to its tests (no `InternalsVisibleTo`) | `grep InternalsVisibleTo src` empty | confirmed |
| 8 | The server image publishes only `Censio.Server`; a new project under `src/` is copied but not built | `src/Censio.Server/Dockerfile:7-23` | confirmed |
| 9 | The relations delta, profit, average productivity and the building-map sum hold in the reference recording | `docs/protocol.md`, *AreaProductionStatistics* table | confirmed |
| 10 | No recording or fixture is in the repository; the #17 recordings are with the Product Manager | `git ls-files`; issue #17 | confirmed |
| 11 | The #17 recordings contain no frame the anonymiser refuses (E2) | to verify: Product Manager, pause after G3 | unverified |
| 12 | A `Release` test project can reference an `Exe` project and call its public `Program.Main` | to verify: Dev, G3 skeleton build | unverified |

## Tests per criterion

`PipeRecordingAnonymiserTests` build recordings from `PipeRecordingFormat.FormatHeader`/`FormatFrameLine` and
`PipeFrameBuilder` frames (duplicate map pairs through `Map` with a list), and drive the random source with the new
double `ScriptedPipeAnonymiserRandom` unless the test is about the system random.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeRecordingAnonymiserReplayGivesSameItemsAsOriginal` (version, repeated session end, session start, two ticks, a rename, a tick closed by the stream end; compares ticks, keys, game times, close reasons, events, receive times minus the offset); `PipeRecordingAnonymiserKeepsLinesAndStructure`; `PipeRecordingAnonymiserRepeatedMapGuidKeepsEveryPair`; `PipeRecordingAnonymiserTrailingBytesAreDropped` (version, session start, session end, island frame); `PipeRecordingAnonymiserEmptyFrameIsCopied`; `PipeRecordingAnonymiserHeaderOnlyGivesHeaderOnly` (valid `started` shifted; unusable `started` gives the default shifted); `PipeRecordingAnonymiserOutputLinesAreFormattedAnew` (unknown JSON fields, escapes, a `+02:00` offset, BOM, CRLF in the input) | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs` |
| AC2 | `PipeRecordingAnonymiserSameNameGetsSameInventedName`; `PipeRecordingAnonymiserTwoNamesGetTwoInventedNames` (island rename, two figures); `PipeRecordingAnonymiserOriginalEqualToInventedNameIsSkipped` (`Island 1`, `island 2`, `PLAYER 1`); `PipeRecordingAnonymiserEmptyNameStaysEmpty` | as above |
| AC3 | `PipeRecordingAnonymiserFloatAmountsShareOneEntryFactor`; `PipeRecordingAnonymiserDerivedValuesFollowRelations`; `PipeRecordingAnonymiserSmallIntegersChangeByOne` (1, 2, 4, -3); `PipeRecordingAnonymiserLargeIntegersChangeWithinBounds` (5, 1000, `int.MaxValue`, `int.MinValue`); `PipeRecordingAnonymiserZeroNaNAndInfinityAreKept` (a NaN with payload bits becomes the standard NaN with its sign); `PipeRecordingAnonymiserProfitWhoseChangeRoundsToZeroMovesOne` (both directions, profit 1 to 0); `PipeRecordingAnonymiserRepeatedWorkforceGuidSumStaysInRange`; `PipeRecordingAnonymiserOverflowingFactorUsesLowerRange`; `PipeAnonymiserRandomNextDoubleStaysInUnitRange` | `PipeRecordingAnonymiserTests.cs`, `tests/Censio.Pipe.Tests/PipeAnonymiserRandomTests.cs` |
| AC4 | `PipeRecordingAnonymiserReceiveTimesShiftByOneOffset` (header `started` equal to the first frame's time, as the recorder writes it); `PipeRecordingAnonymiserOffsetBoundsAreNotWholeDays` (random 0, 0.5, just below 1); `PipeRecordingAnonymiserTwoRunsWithSystemRandomDiffer`; `PipeRecordingAnonymiserShiftBeforeEarliestTimeIsInvalidLine`; `AnonymiserCommandSuccessPrintsOnlyCountAndOutput` | `PipeRecordingAnonymiserTests.cs`, `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs` |
| AC5 | `AnonymiserCommandMissingInputPrintsFileNotFound`; `AnonymiserCommandRefusedRecordingPrintsReplayReason` (not a recording, unsupported version, invalid line 3); `AnonymiserCommandUndecodableFramePrintsLine` (unknown type, malformed, island before `Version`, island after `Version 3`); `AnonymiserCommandExistingOutputIsNotOverwritten`; `AnonymiserCommandOutputIsInputIsRefused`; `AnonymiserCommandMissingOutputFolderPrintsWriteFailed`; `AnonymiserCommandNonSeekableInputPrintsReadFailed` (a FIFO on Linux); `AnonymiserCommandWrongArgumentCountPrintsUsage` (0, 1, 3); `AnonymiserCommandLeavesInputUnchanged`; `AnonymiserCommandCancelledLeavesNoOutput`; `ProgramMainWithoutArgumentsReturnsUsageCode`; `PipeRecordingAnonymiserReadErrorIsReadFailed` (`ChunkedStream`); `PipeRecordingAnonymiserFileChangedBetweenPassesIsReadFailed`; `PipeRecordingAnonymiserNonSeekableRecordingIsReadFailed`; `PipeRecordingAnonymiserStartedNearEarliestTimeUsesDefault` | `AnonymiserCommandTests.cs`, `tests/Censio.Anonymiser.Tests/ProgramTests.cs`, `PipeRecordingAnonymiserTests.cs` |
| AC6 | `PipeMessageReaderFixturesGiveReadmeFrameCounts`; `PipeTickAssemblerFixturesGiveReadmeTicksAndEvents` (deferred to step 6 of G4: they test existing code against new data) | `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`, `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` |
| AC7 | `FixtureDenyListFixturesAgainstLocalDenyList` (inconclusive without `CENSIO_FIXTURE_DENY_LIST`); `FixtureDenyListAnonymisedRecordingPasses`; `FixtureDenyListDeniedNameFails` (case differs); `FixtureDenyListNonInventedNameFails`; `FixtureDenyListUnchangedAmountFails`; `FixtureDenyListMissingOriginalFails`; `FixtureDenyListReportHoldsNoDeniedValue`; manual: Product Manager, branch head after G4, runs *Test* with the variable set | `tests/Censio.Pipe.Tests/FixtureDenyListTests.cs` |
| AC8 | manual: Reviewer, step 8, reads `README.md` and `docs/USER_GUIDE.md` (English and German) against the command as built | — |

## Approach

**Reading.** `PipeRecordingReader` takes over the line rules and header check from `PipeReplayStream` unchanged
(one line in memory, same problems and line numbers); `PipeReplayStream` keeps its signature and behavior and reads
through it, so `PipeReplayStreamTests` stay as they are and guard the move. The reader also exposes the header's
`started` (`PipeRecordingFormat.ReadStarted`, `null` when missing or unusable).

**Two passes** (`PipeRecordingAnonymiser.AnonymiseAsync`; a recording stream that cannot seek gives `ReadFailed`):
1. Draw the offset: `30 days + u × 335 days` in ticks, plus one tick if a whole number of days.
2. Pass 1: every line through the reader; a problem returns it. Each frame: its shifted time below
   `DateTimeOffset.MinValue` → `invalid line <n>`; `PipeFrameAnonymiser.TryCollect` applies the version gate as the
   reader does (only `Version` decoded until a `Version 2`) and `PipeMessageParser.Parse`; an error or a gated frame →
   `FrameNotAnonymisable` with the line; else its names are collected. Pass 1 keeps the first receive time and the
   frame count.
3. `PipeAnonymousNames.Complete`: islands `Island <n>`, figures `Player <n>` in order of first appearance, skipping
   any number whose name equals an original name of the file under `OrdinalIgnoreCase` (AC7 matches ignoring case).
   Names compare as decoded strings, as the reader delivers them.
4. Seek to 0, pass 2: write the header with `started` = the first frame's shifted time, else the reader's shifted
   `started`, else `2000-01-01T00:00:00Z` shifted; then each frame line with the shifted time and the rewritten frame.
   Every output line is made only by `PipeRecordingFormat.FormatHeader` and `FormatFrameLine`; nothing of an input
   line is copied (unknown fields, escapes, offsets, base64 padding bits, BOM and CR are dropped). `ReadStarted`
   returns `null` for a time less than `MaxShiftDays` + 1 day after `DateTimeOffset.MinValue`, so the default is used.
   Any problem, an unknown name, a refused frame or another frame count → `RecordingProblem` `ReadFailed` (the file
   changed).

**Rewriting** (`PipeFrameAnonymiser.Anonymise`), after `Parse` accepted the frame: every frame is written anew and ends
after its last field (trailing bytes dropped): `Version` as type byte and int32, session end as its type byte alone,
session start as type byte, length byte and invented name; only an empty frame is copied. An island frame is walked by
the layout of fact 4 and written field by field. The written frame is then parsed back with `PipeMessageParser.Parse`;
unless it gives the same type, the same entry count and the same number of pairs per map, read in full, the frame is
refused (`frame cannot be anonymised in line <n>`), so a walker mismatch can never copy bytes. Every map pair is written in
wire order; workforce amounts follow the integer rule (a pair is drawn downwards when the sum of its GUID's amounts in that map would leave int32), building pairs are copied. Random draws come in this order (the
scripted double relies on it): the offset; per entry the factor (side, then magnitude), maintenance (side,
magnitude), each workforce amount (side, magnitude). A side draw below 0.5 is downwards.
- Factor `f`: `0.80 + 0.19u` or `1.01 + 0.19u`; if a multiplied finite value would become infinite, the lower range is
  used for that entry. Multiplied: generation, consumption, perfect generation, perfect consumption, income, summed
  productivity, each `(float)(v × f)` when finite; a NaN is written as `float.NaN` with its original sign bit (its payload bits could carry data), infinities stay.
- Delta: `gen' − cons'` when generation, consumption and delta are finite; a non-finite delta is copied; otherwise
  `(float)(delta × f)`. Average productivity: copied when not finite; `0` with building count 0; else
  `(float)(100 × summed' / buildingCount)`, or `(float)(avg × f)` when `summed'` is not finite.
- Profit: when income is finite, `profit + round((income' − maint') − (income − maint))`, away from zero, clamped to
  the int32 range; when that equals a non-zero profit, it moves 1 towards `income' − maint'` (upwards on a tie), which
  keeps it within 1 of `income' − maint'`; with a non-finite income the integer rule applies.
- Integer rule (on 64-bit magnitudes): 0 stays; magnitude 1 → 2; 2 to 4 → ±1 by side; from 5 on
  `round(m × (1 ± (0.01 + 0.19u)))`, at least 1 apart, downwards when upwards leaves int32; the sign is kept.
- A subnormal float can round back to itself (accepted).

**Command** (`AnonymiserCommand.RunAsync`), in this order: not two arguments → usage (code 2); open the input
read-only with `FileShare.Read` (any open failure of fact 6 → `file not found`); full paths equal
(`OrdinalIgnoreCase` on Windows, else `Ordinal`) → `output is the input`; output exists as file or folder → `output
file exists`; create `<output>.<Path.GetRandomFileName()>.tmp` with `CreateNew` in the output's folder, anonymise into
it, flush, close, `File.Move` without overwrite (`IOException` while the output now exists → `output file exists`).
Every failure deletes the temporary file (a delete failure is ignored). `IOException`, `UnauthorizedAccessException`,
`ArgumentException`, `NotSupportedException` on the output → `write failed`; no exception text is printed. An
input that cannot seek (a FIFO or device) → `read failed`, checked after opening, before any output exists
(`PipeRecordingAnonymiser` returns `ReadFailed` for such a stream too). A recording the Companion is still writing
cannot be opened (`file not found`); the user guide says to stop the recording first.
`Program.Main` cancels on Ctrl+C (`Console.CancelKeyPress`, `e.Cancel = true`): `Error: cancelled`, code 1.

Output layout (fixed): success on standard output, one line `Anonymised <n> frames to <output>` (`<output>` as typed);
every error on standard error, one line `Error: <reason>`; usage on standard error, `Usage: Censio.Anonymiser
<recording> <output>`. Reasons: the five of fact 5, `frame cannot be anonymised in line <n>`, `output file exists`,
`output is the input`, `write failed`, `cancelled`. Exit codes 0, 1, 2 as constants.

**Fixtures.** After G3 the orchestrator pauses for the Product Manager (open question 5): on the branch head they run
`dotnet run --project src/Censio.Anonymiser -c Release -- <recording> <fixture>.jsonl` on the #17 recordings they
choose (the variants of #17: loaded save, regions, pause, rename, save switch, quitting, a long session; whole files),
and hand in the outputs with a note of the variants each shows, never the original names. A refusal (E2) is reported
to the Lead and that recording is left out. The files go to `tests/Censio.Pipe.Tests/Fixtures/<variant>.jsonl` with a
`README.md` there (Tester): per fixture its variants, the command and "the anonymiser of the pull request that added
the fixtures" (no commit: squash merge), and the facts the tests assert; never an original file name or time. If
`PipeFrameAnonymiser`, `PipeAnonymousNames` or `PipeRecordingAnonymiser` changes after the pause, the Product Manager
remakes the fixtures before task 12. The deny list (`CENSIO_FIXTURE_DENY_LIST`, a UTF-8 file of lines
`deny <name>` and `original <fixture file name> <path>`, kept as `censio-fixture-deny-list.txt` outside the
repository) stays with the Product Manager; `.gitignore` gains `recording-*.jsonl` and `censio-fixture-deny-list*`.

**Deny-list check** (`FixtureDenyList`, test code): for each fixture, every non-empty name must match `Island <n>` or
`Player <n>` and equal no `deny` entry ignoring case; with an `original` line, both files are walked line by line, the
frames parsed, and every non-zero economic value of N3 compared with the one at the same line, entry and field (the
workforce map by GUID after the reader's merge). The report holds counts, fixture names, line numbers and field names
only.

## Options considered

1. **Duplicating the line rules in the anonymiser** — no change to `PipeReplayStream`; two copies of a parser in a
   security area drift apart. Lost to the extracted `PipeRecordingReader`.
2. **Driving `PipeReplayStream` with a clock and a frame reader to get frames** — no refactor; no line numbers for E2
   and no `started`. Lost.
3. **Re-encoding from the parsed message / extending the parser with wire pairs** — record 0042, options 4 and 5.
4. **All logic in the console project** — no change to `Censio.Pipe`; the recording format would leak past
   `Censio.Pipe` (`.squad/project.md`, *Integration surface*). Lost to logic in `Censio.Pipe`, a thin exe.
5. **Writing the output directly with `CreateNew` and deleting it on failure** — no temporary file; a kill leaves a
   partial file under the output name. Lost to temp file and rename.
6. **`Random.Shared`** — simpler; a non-cryptographic generator (Sonar hotspot S2245) for a privacy tool. Lost to
   `RandomNumberGenerator`.
7. **Moving the replay error texts from `TrayMenu` to `Censio.Pipe`** — one copy of five texts; changes the Companion
   for a contributor tool. Lost: the command holds its own five texts, pinned by its tests.
8. Delivery, perturbation, seeds: record 0042; the deny list by place: spec, open question 3.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `PipeRecordingReader`, `PipeRecordingLine`, `PipeRecordingLineKind` | new: line rules and header check moved from `PipeReplayStream` |
| Censio.Pipe | `PipeReplayStream` | reads through `PipeRecordingReader`; no public change |
| Censio.Pipe | `PipeReplayLineStatus` | used by `PipeRecordingReader` (stays internal) |
| Censio.Pipe | `PipeRecordingFormat` | new `ReadStarted` |
| Censio.Pipe | `PipeRecordingAnonymiser`, `PipeAnonymiseResult`, `PipeAnonymiseResultKind`, `IPipeAnonymiserRandom`, `PipeAnonymiserRandom` | new |
| Censio.Pipe | `PipeFrameAnonymiser`, `PipeAnonymousNames` | new, internal |
| Censio.Anonymiser (new, `src/`) | `Censio.Anonymiser.csproj` (`Exe`, `net10.0`, references Censio.Pipe), `Program`, `AnonymiserCommand` | new |
| — | `Censio.slnx` | both new projects (Dev) |

## Signatures (for the Dev's skeleton)

Properties are get/init unless named otherwise; every class marked (sealed) is sealed.

Censio.Pipe:
- `public enum PipeRecordingLineKind { Frame, End, Problem }`
- `public class PipeRecordingLine` (sealed); `PipeRecordingLine.Kind: required PipeRecordingLineKind`, `PipeRecordingLine.LineNumber: long`, `PipeRecordingLine.ReceiveTime: DateTimeOffset`, `PipeRecordingLine.Frame: byte[] = []`, `PipeRecordingLine.Problem: PipeReplayProblem?`
- `public class PipeRecordingReader` (sealed); `public PipeRecordingReader(Stream recording)`; `PipeRecordingReader.Started: DateTimeOffset? { get; }`; `public ValueTask<PipeRecordingLine> ReadAsync(CancellationToken cancellationToken)`
- `public static DateTimeOffset? ReadStarted(ReadOnlySpan<byte> line)` (in PipeRecordingFormat)
- `public interface IPipeAnonymiserRandom`; `double NextDouble()` (in `[0, 1)`); `public class PipeAnonymiserRandom : IPipeAnonymiserRandom` (sealed)
- `public enum PipeAnonymiseResultKind { Anonymised, RecordingProblem, FrameNotAnonymisable }`
- `public class PipeAnonymiseResult` (sealed); `PipeAnonymiseResult.Kind: required PipeAnonymiseResultKind`, `PipeAnonymiseResult.FrameCount: long`, `PipeAnonymiseResult.LineNumber: long?`, `PipeAnonymiseResult.Problem: PipeReplayProblem?`
- `public class PipeRecordingAnonymiser` (sealed); `PipeRecordingAnonymiser.MinShiftDays: const int = 30`, `PipeRecordingAnonymiser.MaxShiftDays: const int = 365`; `public PipeRecordingAnonymiser(IPipeAnonymiserRandom random)`; `public Task<PipeAnonymiseResult> AnonymiseAsync(Stream recording, Stream output, CancellationToken cancellationToken)`
- `internal class PipeAnonymousNames` (sealed); `public void AddIsland(string name)`, `public void AddFigure(string name)`, `public void Complete()`, `public bool TryGetIsland(string original, out string invented)`, `public bool TryGetFigure(string original, out string invented)`
- `internal class PipeFrameAnonymiser` (sealed); `public PipeFrameAnonymiser(PipeAnonymousNames names, IPipeAnonymiserRandom random)`, `public bool TryCollect(ReadOnlySpan<byte> frame)`, `public byte[]? Anonymise(ReadOnlySpan<byte> frame)`, `public void Reset()`

Censio.Anonymiser:
- `public static class Program`; `public static Task<int> Main(string[] args)`
- `public class AnonymiserCommand` (sealed); `AnonymiserCommand.SuccessCode: const int = 0`, `AnonymiserCommand.ErrorCode: const int = 1`, `AnonymiserCommand.UsageCode: const int = 2`; `public AnonymiserCommand(TextWriter output, TextWriter error, IPipeAnonymiserRandom random)`; `public Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)`

The G1 skeleton rewrites PipeReplayStream.cs (its line methods move).

## Test files

New: `tests/Censio.Pipe.Tests/PipeRecordingReaderTests.cs` (the reader's own API: `Started`, line numbers, `End`),
`PipeRecordingAnonymiserTests.cs`, `PipeAnonymiserRandomTests.cs`, `FixtureDenyListTests.cs`, the helper
`FixtureDenyList.cs` and the double `ScriptedPipeAnonymiserRandom.cs` (linked into the Anonymiser tests); new project
`tests/Censio.Anonymiser.Tests/` with `Censio.Anonymiser.Tests.csproj`, `AnonymiserCommandTests.cs`, `ProgramTests.cs`;
`PipeRecordingFormatTests.cs` gains `PipeRecordingFormatReadStarted…` cases; `PipeMessageReaderTests.cs` and
`PipeTickAssemblerTests.cs` gain the AC6 tests; `Censio.Pipe.Tests.csproj` copies `Fixtures/**` to the output (Tester).

Existing call sites of a changed signature: none (`PipeReplayStream` keeps its API).

Files no unit test can cover: none (`Program.Main` is called by `ProgramTests`; only its Ctrl+C handler body is not).

## Areas

- `pipe-recording.md`: new section *Anonymising* (what is replaced, multiplied, derived, shifted and kept; the two
  passes; the refusals and messages; temp file and rename; the deny list stays local); *Scope*, *Privacy*, the format
  note and *Not here* name it instead of #20; *Related decisions* adds 0042.
- `pipe.md`: *Residual risks* and *Not here* point to the fixtures and to `pipe-recording.md`.
- `docs/areas/README.md`: index rows of `pipe` and `pipe-recording` (anonymising moves into `pipe-recording`).

## Documentation updates

- Dev: the area documents above; `docs/ARCHITECTURE.md` (eight production projects, a row for `Censio.Anonymiser`:
  "Contributor console tool that anonymises a recording; not released", references Pipe); `README.md` English and
  German (remove the "no tool" limitation; a short *Anonymising a recording (contributors)* note linking the user
  guide); `docs/USER_GUIDE.md` English and German (a subsection under *Recording and replay*: command, what changes,
  what stays, the errors, to stop a recording before anonymising it, that it needs the repository and the .NET SDK and
  that raw recordings still go to a maintainer privately); `.gitignore` (`recording-*.jsonl`,
  `censio-fixture-deny-list*`); `docs/protocol.md` line 16 (anonymised fixtures now exist under `tests/`);
  `.squad/stack.md` *Layout* (`Censio.Anonymiser`, and fixtures under `tests/<Project>.Tests/Fixtures/`);
  `.squad/project.md` (*Security areas*: the parsing line adds `PipeRecordingReader`, `PipeRecordingAnonymiser`; the
  file-path line adds the anonymiser's output; *Integration surface*: "never real recordings" becomes "or the
  anonymised fixtures", and a new pipe field also changes `PipeFrameAnonymiser`; *Test doubles*:
  `ScriptedPipeAnonymiserRandom`).
- Tester: `tests/Censio.Pipe.Tests/Fixtures/README.md`; `docs/UNIT_TESTS.md` layout block (fixtures folder,
  `CENSIO_FIXTURE_DENY_LIST`).

## Architecture check

No guarantee of `.squad/project.md` is touched: nothing is sent, stored or released. The recording format stays in
`Censio.Pipe`.

## Security considerations

- [x] Limits: the line and frame limits of `PipeRecordingFormat` apply before decoding, as for a replay; names are at
  most 255 bytes before decoding; the name set grows with distinct names only (bounded by the file).
- [x] Exceptions reaching the user: input open failures → `file not found`; a non-seekable input → `read failed`;
  output failures (fact 6's four types) → `write failed`; cancellation → `cancelled`; input read errors are typed
  problems. No exception text is printed.

## Decision records

- `docs/decisions/0042-recordings-anonymised-by-a-contributor-tool-rewriting-frames.md` (Proposed)

## Challenge

1. M1, version and session-end frames copied with trailing bytes — accepted: every frame but an empty one is written
   anew and ends after its last field; the trailing-bytes test covers all four types (*Rewriting*).
2. M2, a profit whose rounded change is 0 stays and fails AC7 — accepted with the second option: such a profit moves 1
   towards the new income minus maintenance, which keeps the relation within 1; spec N3 changed accordingly (AC3
   unchanged, it refers to N3).
3. M3, tier — accepted: tier `security`; Security reviews the step-8 diff.
4. m1, a repeated workforce GUID's sum leaving int32 — accepted (*Rewriting*).
5. m2, commit in the fixtures README; fixtures after a later change — accepted (*Fixtures*; task 12 waits for remade
   fixtures).
6. m3, a recording still being written — accepted: kept as `file not found`, the user guide says to stop it first.
7. m4, `started` tests — accepted: the shift test uses `started` equal to the first frame; header-only fallbacks are
   tested separately (AC1, AC4 rows).

## Security review

- B1 (= M1) — resolved as Challenge 1; a session start is type byte, length byte and invented name.
- B2 (= M3) — resolved: tier `security`.
- N1, NaN payload as a channel — taken: a NaN is written as `float.NaN` with its sign; spec E8 aligned.
- N2, input bytes copied into lines — taken: lines are made only by the two format methods (*Two passes*, step 4) and
  tested with hostile line forms.
- N3, `started` underflow — taken: `ReadStarted` treats such a time as unusable; test added.
- N4, non-seekable input — taken: `read failed`, tested in the command and in `PipeRecordingAnonymiser`.
- N5, `.gitignore` — taken: `recording-*.jsonl` and `censio-fixture-deny-list*` (Dev); the README names the file.
- Defensive parse-back — taken: each written frame is parsed back and compared, else refused (*Rewriting*).
- `File.Move` without overwrite measured on Linux; on Windows it is documented to throw `IOException` for an existing
  target (Security notes it was not run there; accepted, the check before creating the temp file catches the usual
  case).

## Out of scope / follow-ups

See the spec's *Out of scope*. No follow-up issue.
