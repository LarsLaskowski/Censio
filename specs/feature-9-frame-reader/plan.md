# Plan: Frame reader and message parser for pipe protocol version 2

Source: Issue #9 | [spec.md](spec.md)
Status: Draft
Tier: standard — new behavior in `Censio.Pipe` whose size bound and validation keep malformed input from breaking the program (input handling), with no security control created, changed or removed.
Security areas touched: Parsing of external input — the Anno 117 statistics pipe stream, read by the new `PipeFrameReader`, `PipeMessageParser` and `PipeMessageReader` in `src/Censio.Pipe/`

## Problem / root cause

Feature: see [spec.md](spec.md) (*Problem*, wire format, S1–S7, E1–E18, AC1–AC14).

## Verified facts

Reference = `UbisoftMainzAnno/anno117_pipe_example` at its default branch, `src/pipe.h` and `src/pipe.cpp`, read as
text on 2026-11-01 (line numbers of that read).

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Type values 0–3 are `Version`, `SessionStart`, `SessionEnd`, `AreaProductionStatistics`; `PROTOCOL_VERSION = 2` | `pipe.h` l. 21–29 | confirmed |
| 2 | The length counts the type byte and the payload, not the prefix; a frame of exactly 1 MiB is accepted | `pipe.cpp` l. 156 (`maxMessageSize{ 1024 * 1024 }`), l. 249–266 (`size > buffer->size()` rejects, then `size` bytes read, type taken from byte 0) | confirmed |
| 3 | Field order and types of the statistics payload and an entry as in the spec's wire format | `pipe.cpp` l. 83–119 (`ProductionEntryData::read`), l. 283–304; `pipe.h` l. 38–58 | confirmed |
| 4 | Session ID, island ID, area index and the string length byte are unsigned | `pipe.cpp` l. 70 and l. 287–289 read `unsigned char` | confirmed |
| 5 | Duplicate map keys are summed | `pipe.cpp` l. 106, l. 116 (`[guid] += amount`) | confirmed |
| 6 | The reference is lenient: short reads give `0`, an oversized frame is followed by `continue`, a wrong version is only printed | `pipe.cpp` l. 19–22, 33–36, 51–54, 65–74; l. 250–254; l. 224–227 | confirmed |
| 7 | Issue: the Ubisoft example is "the reference" | Its README: "for documentation purposes only and should not be regarded as an officially supported SDK or reference implementation" | refuted |
| 8 | Issue claims the type, length, version, string and duplicate-sum details | rows 1–5 | confirmed |
| 9 | `Censio.Pipe` holds only `PipeAssembly` and references no project; its test project references `Censio.Pipe` and `Censio.Contracts` | `src/Censio.Pipe/`, `Censio.Pipe.csproj`, `tests/Censio.Pipe.Tests/Censio.Pipe.Tests.csproj` | confirmed |
| 10 | No real sample exists; the Product Manager decided synthetic frames only | `log.md`, 2026-11-01 11:37; spec Q5 | confirmed |
| 11 | Strings are UTF-8 and the game writes `_` for umlauts (residual risk in record 0033) | to verify: against the #20 fixture of a renamed island, by #20's Tester; the reference names no encoding (`std::string_view`, l. 76), the `_` claim comes from a third-party capture (`Dealwatch/Tabularium117`, `docs/protocol.md`) | unverified |
| 12 | `Encoding.UTF8.GetString` replaces each invalid sequence with U+FFFD and never throws | to verify: AC11 test `PipeMessageParserInvalidUtf8ReturnsReplacementCharacter`, Tester, step 5 (green in step 6) | unverified |
| 13 | `Stream.ReadAtLeastAsync(buffer, n, throwOnEndOfStream: false, token)` returns fewer than `n` bytes only at the end of the stream | to verify: AC5 tests with `ChunkedStream` of chunk size 1, Dev, step 6 | unverified |
| 14 | `MemoryStream.ReadAsync` with a cancelled token completes cancelled without reading | to verify: AC12 test `PipeFrameReaderCancelledReadThrowsOperationCanceled`, Tester, step 5/6 | unverified |
| 15 | No decision record is released | `git ls-remote --tags origin` prints no tag | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeMessageParserVersionFrameReturnsVersionMessage`, `PipeMessageParserSessionStartFrameReturnsHeadline` (200-byte headline), `PipeMessageParserSessionEndFrameReturnsSessionEndMessage`, `PipeMessageParserStatisticsFrameReturnsAllFields` (IDs 200, 255, 128; two entries; maps with `CollectionAssert.AreEquivalent` on the pairs), `PipeMessageParserStatisticsFrameWithoutEntriesReturnsNoEntries`; `PipeMessageReaderTwoSessionEndFramesReturnTwoMessages` | `PipeMessageParserTests.cs`; `PipeMessageReaderTests.cs` |
| AC2 | `PipeMessageReaderChunkedStreamReturnsSameResults` (`[DataRow]` chunk sizes 1, 3, 4, 5 and `int.MaxValue`) | `PipeMessageReaderTests.cs` |
| AC3 | `PipeMessageParserDuplicateWorkforceGuidSumsAmounts`, `PipeMessageParserDuplicateBuildingGuidSumsAmounts`, `PipeMessageParserDuplicateGuidSumOverflowReturnsMalformed` (`[DataRow]` `int.MaxValue`+1, `int.MinValue`−1) | `PipeMessageParserTests.cs` |
| AC4 | `PipeFrameReaderEndAtFrameBoundaryReturnsEndOfStream`, `PipeFrameReaderReadAfterEndReturnsEndOfStream` (`[DataRow]` after end, truncated, oversized) | `PipeFrameReaderTests.cs` |
| AC5 | `PipeFrameReaderStreamEndsInPrefixReturnsTruncated` (`[DataRow]` 1, 2, 3 bytes), `PipeFrameReaderStreamEndsInPayloadReturnsTruncated` | `PipeFrameReaderTests.cs` |
| AC6 | `PipeFrameReaderOversizedLengthReturnsOversized` (`[DataRow]` 1048577, −1, `int.MinValue`; asserts `Length` and stream `Position == 4`), `PipeFrameReaderMaximumLengthReturnsFrame` | `PipeFrameReaderTests.cs` |
| AC7 | `PipeMessageParserEmptyFrameReturnsEmptyFrame`, `PipeMessageParserUnknownTypeReturnsUnknownType` (`[DataRow]` 4, 255); `PipeMessageReaderEmptyFrameThenFrameReturnsNextMessage`, `PipeMessageReaderUnknownTypeThenFrameReturnsNextMessage` | `PipeMessageParserTests.cs`; `PipeMessageReaderTests.cs` |
| AC8 | `PipeMessageParserTruncatedPayloadReturnsMalformed` (`[DynamicData]`: `Version` of 3 bytes, statistics cut in the header, in an entry, in each map), `PipeMessageParserStringBeyondFrameReturnsMalformed`, `PipeMessageParserNegativeCountReturnsMalformed` (entries, workforce, buildings), `PipeMessageParserCountBeyondFrameReturnsMalformed`; `PipeMessageReaderMalformedFrameThenFrameReturnsNextMessage` | `PipeMessageParserTests.cs`; `PipeMessageReaderTests.cs` |
| AC9 | `PipeMessageParserHugeCountAllocatesNoMemoryForCount` (`GC.GetAllocatedBytesForCurrentThread()` around `Parse`, under 64 KiB, `[DataRow]` entry count and pair count) | `PipeMessageParserTests.cs` |
| AC10 | `PipeMessageReaderUnsupportedVersionReturnsMismatch`, `PipeMessageReaderFrameAfterUnsupportedVersionReturnsMismatch`, `PipeMessageReaderSupportedVersionAfterMismatchDecodesFrames`, `PipeMessageReaderFrameBeforeVersionReturnsMismatchWithoutVersion` | `PipeMessageReaderTests.cs` |
| AC11 | `PipeMessageParserTrailingBytesReturnsMessage`, `PipeMessageParserInvalidUtf8ReturnsReplacementCharacter`, `PipeMessageParserStringKeptAsDelivered` (`[DataRow]` `Zycada `, `R_mische K_ste`) | `PipeMessageParserTests.cs` |
| AC12 | `PipeFrameReaderStreamIoErrorReturnsReadFailed`, `PipeFrameReaderReadAfterReadFailedReturnsEndOfStream`, `PipeFrameReaderCancelledReadThrowsOperationCanceled`, `PipeFrameReaderReadAfterCancelledReadReturnsEndOfStream` | `PipeFrameReaderTests.cs` |
| AC13 | `PipeMessageReaderTruncatedSequenceNeverThrows` (`[DynamicData]` every cut offset), `PipeMessageReaderCorruptedSequenceNeverThrows` (`[DynamicData]` every offset × `0x00`, `0xFF`); both read to the end through a helper and assert the last result is the end of the stream | `PipeMessageReaderTests.cs` |
| AC14 | `PipeMessageParserMalformedErrorCarriesNoPayloadText` (headline `secret`, length byte beyond the frame); `PipeErrorStringFormListsPresentFields` | `PipeMessageParserTests.cs`; `PipeErrorTests.cs` |

Notes for the Tester: U+FFFD in an expected value is built at run time (`((char)0xFFFD).ToString()`), not
written as a `\u` escape that *Format* rewrites (`.squad/stack.md`, *Known pitfalls*). Frames are built with the
helper `PipeFrameBuilder`, never as hand-typed byte arrays beyond the corrupt cases.

## Approach

**Frame reader** (`PipeFrameReader`, one per stream). `ReadFrameAsync`:
1. If ended, return `PipeFrameResult.End`. Call `cancellationToken.ThrowIfCancellationRequested()`.
2. Read the 4-byte prefix with `ReadAtLeastAsync(…, 4, throwOnEndOfStream: false, …)`: 0 bytes → ended, `End`;
   1–3 → ended, `TruncatedFrame`.
3. `length < 0 || length > PipeProtocol.MaxFrameLength` → ended, `OversizedFrame` with `Length`; nothing more is
   read (the stream stays at offset 4 of that frame).
4. Allocate `length` bytes and read them the same way; fewer → ended, `TruncatedFrame` with `Length`; else
   `PipeFrameResult.FromFrame` (length 0 gives an empty frame).
5. `IOException`, `ObjectDisposedException`, `NotSupportedException` from the stream → ended, `ReadFailed`
   (no exception text kept). `OperationCanceledException` → ended, rethrown.

The reader never reads past the current frame, so it holds one frame at most.

**Parser** (`PipeMessageParser.Parse`, static, synchronous, over `ReadOnlySpan<byte>` with a position).
- Empty span → `EmptyFrame`. Type byte above 3 → `UnknownType` with `TypeValue`.
- Each field reads through a bounds check; a short read → `MalformedMessage` with `TypeValue` and `Offset` (the
  position of the field that did not fit). No field ever defaults to `0`.
- Strings: length byte, bounds check, `Encoding.UTF8.GetString` (U+FFFD replacement, no trim).
- Counts: negative → malformed; entry count greater than `remaining / 56`, pair count greater than
  `remaining / 8` → malformed before any list or dictionary is created; collections are created with at most that
  capacity.
- Map amounts are summed with `checked` arithmetic; `OverflowException` caught inside the map loop → malformed.
- Bytes after the last field are ignored. The parser does not apply the version gate.

**Message reader** (`PipeMessageReader`, one per stream, owns a `PipeFrameReader`; gate state: unknown at start,
then supported, or mismatch with the received version). `ReadAsync`:
- Frame result `End` or an error → passed through.
- Empty frame → `EmptyFrame` (no type, gate not consulted).
- Type byte `0` → parsed; `Version 2` → gate supported, the message returned; any other version → gate mismatch
  with that version, `ProtocolMismatch` returned; a malformed `Version` → its malformed error, gate unchanged.
- Any other type byte (known or unknown) while the gate is not supported → `ProtocolMismatch` with
  `ReceivedVersion` (null while unknown), `ExpectedVersion` 2 and the frame's `TypeValue`, without parsing.
- Otherwise → `Parse` result.

**Error string form** (AC14): `PipeError.ToString()` is the kind name, then — only when at least one field is set —
`: ` and the set fields as `name=value` joined by `, `, in the order `length`, `type`, `offset`, `received`,
`expected`. Example: `ProtocolMismatch: type=3, received=3, expected=2`; `EmptyFrame`. No other user-visible output.

## Options considered

1. **One reader class doing framing and parsing** — fewer types; the recorder (#12) needs raw frames without the
   parser, and the parser could not be tested on a span without a stream. Lost.
2. **Throw typed exceptions instead of result objects** — idiomatic in .NET; the issue requires that bad input
   never throws, and a hostile stream would make exceptions the normal path. Lost.
3. **Lenient as the reference / skip an oversized frame / version gate that only warns** — weighed in record 0033
   (option 2 chosen there).
4. **Records with positional parameters for messages** — concise; the code style forbids primary constructors, and
   an entry has 14 values (Sonar S107). Classes with `required init` properties instead.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Pipe` | `PipeProtocol.cs`, `PipeMessageType.cs`, `PipeMessage.cs`, `VersionMessage.cs`, `SessionStartMessage.cs`, `SessionEndMessage.cs`, `AreaProductionStatisticsMessage.cs`, `ProductionEntry.cs` | new: constants and typed messages |
| `Censio.Pipe` | `PipeErrorKind.cs`, `PipeError.cs`, `PipeFrameResultKind.cs`, `PipeFrameResult.cs`, `PipeReadResultKind.cs`, `PipeReadResult.cs` | new: results and errors |
| `Censio.Pipe` | `PipeFrameReader.cs`, `PipeMessageParser.cs`, `PipeMessageReader.cs` | new: reader, parser, version gate |
| `Censio.Pipe` | `PipeAssembly.cs` | unchanged |

All in namespace `Censio.Pipe`, one public type per file, no new package, no new project reference.

## Signatures (for the Dev's skeleton)

Written below as Type.Member(parameters): type; the Dev writes them as C# with XML docs and regions. Properties
are get-only unless marked init (required init where stated).

- `class PipeProtocol` (static)
- `PipeProtocol.SupportedVersion: const int = 2`
- `PipeProtocol.MaxFrameLength: const int = 1048576`
- `enum PipeMessageType : byte` with `Version = 0`, `SessionStart = 1`, `SessionEnd = 2`, `AreaProductionStatistics = 3`
- `class PipeMessage` (abstract) with `PipeMessage.MessageType: PipeMessageType` (abstract property)
- `VersionMessage.VersionMessage(int version)`, `VersionMessage.Version: int` (sealed class, derives `PipeMessage`)
- `SessionStartMessage.SessionStartMessage(string headline)`, `SessionStartMessage.Headline: string`
- `SessionEndMessage.SessionEndMessage()`
- `class AreaProductionStatisticsMessage` (sealed, derives `PipeMessage`), required init properties: `AreaProductionStatisticsMessage.SessionId: byte`, `AreaProductionStatisticsMessage.IslandId: byte`, `AreaProductionStatisticsMessage.AreaIndex: byte`, `AreaProductionStatisticsMessage.SessionGuid: int`, `AreaProductionStatisticsMessage.AreaName: string`, `AreaProductionStatisticsMessage.Timestamp: long`, `AreaProductionStatisticsMessage.Entries: IReadOnlyList<ProductionEntry>`
- `class ProductionEntry` (sealed), required init properties: `ProductionEntry.ProductGuid: int`, `ProductionEntry.Generation: float`, `ProductionEntry.Consumption: float`, `ProductionEntry.Delta: float`, `ProductionEntry.PerfectGeneration: float`, `ProductionEntry.PerfectConsumption: float`, `ProductionEntry.BuildingCount: int`, `ProductionEntry.Maintenance: int`, `ProductionEntry.Income: float`, `ProductionEntry.Profit: int`, `ProductionEntry.SummedProductivity: float`, `ProductionEntry.AverageProductivity: float`, `ProductionEntry.WorkforceAmounts: IReadOnlyDictionary<int, int>`, `ProductionEntry.BuildingAmounts: IReadOnlyDictionary<int, int>`
- `enum PipeErrorKind` with `TruncatedFrame`, `OversizedFrame`, `EmptyFrame`, `UnknownType`, `MalformedMessage`, `ProtocolMismatch`, `ReadFailed`
- `class PipeError` (sealed), init properties: `PipeError.Kind: PipeErrorKind` (required), `PipeError.Length: int?`, `PipeError.TypeValue: byte?`, `PipeError.Offset: int?`, `PipeError.ReceivedVersion: int?`, `PipeError.ExpectedVersion: int?`; `PipeError.ToString(): string` (override)
- `enum PipeFrameResultKind` with `Frame`, `Error`, `EndOfStream`
- `class PipeFrameResult` (sealed, private constructor): `PipeFrameResult.Kind: PipeFrameResultKind`, `PipeFrameResult.Frame: ReadOnlyMemory<byte>`, `PipeFrameResult.Error: PipeError?`, `PipeFrameResult.End: PipeFrameResult` (static), `PipeFrameResult.FromFrame(ReadOnlyMemory<byte> frame): PipeFrameResult` (static), `PipeFrameResult.FromError(PipeError error): PipeFrameResult` (static)
- `enum PipeReadResultKind` with `Message`, `Error`, `EndOfStream`
- `class PipeReadResult` (sealed, private constructor): `PipeReadResult.Kind: PipeReadResultKind`, `PipeReadResult.Message: PipeMessage?`, `PipeReadResult.Error: PipeError?`, `PipeReadResult.End: PipeReadResult` (static), `PipeReadResult.FromMessage(PipeMessage message): PipeReadResult` (static), `PipeReadResult.FromError(PipeError error): PipeReadResult` (static)
- `PipeFrameReader.PipeFrameReader(Stream stream)` (sealed class; throws ArgumentNullException for a null stream), `PipeFrameReader.ReadFrameAsync(CancellationToken cancellationToken): ValueTask<PipeFrameResult>`
- `PipeMessageParser.Parse(ReadOnlySpan<byte> frame): PipeReadResult` (static class)
- `PipeMessageReader.PipeMessageReader(Stream stream)` (sealed class), `PipeMessageReader.ReadAsync(CancellationToken cancellationToken): ValueTask<PipeReadResult>`

## Test files

- `tests/Censio.Pipe.Tests/PipeFrameReaderTests.cs`
- `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`
- `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`
- `tests/Censio.Pipe.Tests/PipeErrorTests.cs`
- Test helpers (not test classes), written by the Tester in group G1: `tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`
  (builds synthetic frames: prefix, type byte, int32, int64, float32, unsigned byte, string, entries, maps, and a
  raw length for the corrupt cases) and `tests/Censio.Pipe.Tests/ChunkedStream.cs` (a read-only `Stream` over bytes
  that returns at most N bytes per read, or throws `IOException` after a given offset).

Existing test code that calls a changed signature: none (no existing signature changes).

Files no unit test can cover: none.

## Areas

New area `pipe` (Lead decision), document `docs/areas/pipe.md`, index row in `docs/areas/README.md`:
name `pipe`, scope "The statistics pipe as the Companion reads it: frame layout, the message types of protocol
version 2, the 1 MiB limit, error kinds, skip and stop rules, the version gate", not here "Opening the pipe and
reconnecting, ticks, recording". `companion` changes only in *Not here*.

## Documentation updates

Swept: `docs/ARCHITECTURE.md`, `docs/areas/*.md`, `docs/CONTRIBUTING.md`, `README.md`, `SECURITY.md`,
`.squad/project.md` for "pipe". `CONTRIBUTING.md` l. 143 (area `Pipe`), `SECURITY.md` l. 42 and
`.squad/project.md` *Security areas* stay true; `README.md` and the user guide stay unchanged (spec, *Out of
scope*).

- `docs/areas/pipe.md` (Dev, new, from `docs/areas/_template.md`): the spec's wire format and *Area documents*
  outline; *Behavior*: message types and fields, maps as GUID→amount sets with duplicates summed, strings as UTF-8
  with U+FFFD, untrimmed, extra bytes ignored, the version gate per stream; *Limits and errors*: the seven error
  kinds, which skip the frame (empty, unknown type, malformed, mismatch) and which end the stream (truncated,
  oversized, read failed, cancellation), the 1 MiB limit inclusive, errors carry no payload text. It states as
  deliberate differences from the reference: an oversized length ends the stream where the reference reads on out
  of step, so the caller (#11) reconnects (challenge 7); an overflowing duplicate sum rejects the frame where the
  reference wraps (challenge 5); a short field rejects the frame where the reference reads `0`; a wrong version
  stops decoding where the reference only warns. *Related decisions*: 0033, 0018. *Not here*: #10, #11, #12.
- `docs/areas/README.md` (Dev): the index row above, inside the `area-index` block.
- `docs/areas/companion.md` (Dev), *Not here*: the pipe bullet points to [`pipe.md`](pipe.md) for reading the
  statistics pipe; game files, server connection, settings and autostart stay "later issues".
- `docs/ARCHITECTURE.md` (Dev): the `Censio.Pipe` row says it frames and parses the statistics pipe into typed
  messages, with a link to `areas/pipe.md`; *Data flow*: the pipe stream is parsed strictly and a frame it cannot
  read is reported, never guessed (link record 0033).
- `.squad/project.md` *Test doubles* (Tester): entries for `ChunkedStream` and `PipeFrameBuilder`.

## Architecture check

Record 0018 confines the game's format to `Censio.Pipe`: every new type is there, `Censio.Pipe` gains no
reference, and nothing in `Censio.Contracts` changes. `.squad/project.md` lists no guarantees. The `Censio.Pipe`
row of `docs/ARCHITECTURE.md` changes in wording only (Dev, above).

## Security considerations

- [x] Limits: the 1 MiB frame limit applies to the raw length before any decoding; each string is at most 255 raw
  bytes and decodes to at most 255 UTF-16 chars (one U+FFFD per invalid byte at most); counts are checked against
  the raw bytes left before any collection is created (AC9).
- [x] Exceptions reaching a user-visible message: none. `ArgumentNullException` for a null stream is a programming
  error; stream exceptions become `ReadFailed` without their text; cancellation is rethrown as
  `OperationCanceledException`. No error carries payload text (AC14).

## Decision records

- `docs/decisions/0033-pipe-reader-strict-from-the-reference-source.md` (Proposed, indexed) — strict, fail-closed
  reader built from the reference source with synthetic frames; residual risks (encoding, `_`, duplicate keys,
  format revision) per spec challenge 6. Its `Area:` is `pipe`, which `decision-check.py` accepts once the Dev adds
  the area's index row and document (task 10).

## Out of scope / follow-ups

See spec, *Out of scope*. No follow-up issue: real-recording tests are #20's scope already.
