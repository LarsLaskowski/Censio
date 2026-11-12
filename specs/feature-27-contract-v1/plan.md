# Plan: JSON contract v1 between Companion and server

Source: Issue #27 | [spec.md](spec.md)
Tier: security — the change creates a security control: the strict, bounded reader of every message the server and the Companion will take from the network.
Security areas touched: Parsing of external input (the contract reader of the later WebSocket messages, `src/Censio.Contracts`); Ingest token and Companion authentication (the hello carries the token; no check is built)

## Problem / root cause

Feature: [spec.md](spec.md) (Approved by Lead). The plan fixes the wire form, the bounds and the reader's rules that
the spec leaves to it.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | #10 and #26 are built; both issues closed | `src/Censio.Pipe/PipeTickAssembler.cs`, `PipeTick.cs`; `src/Censio.Contracts/CatalogDocumentReader.cs`, `CatalogDocumentWriter.cs`; `gh api …/issues/10`, `/26`: `closed` | confirmed |
| 2 | A closed tick has a receive time and a game time; session events carry no time and no data besides the figure name | `PipeTick.cs` (`GameTime`, `ReceiveTime`); `PipeTickResult.cs` (`FromSessionStart(headline)`, `SessionEnd` field-less) | confirmed |
| 3 | An entry has a product GUID, 11 values (7 float32, 4 int32) and two int-to-int maps | `src/Censio.Pipe/ProductionEntry.cs` | confirmed |
| 4 | Floats are delivered as read, NaN included; a string is at most 255 wire bytes, each invalid sequence U+FFFD (≤ 765 UTF-8 bytes) | `docs/areas/pipe.md` lines 30–33 | confirmed |
| 5 | Tick limits: 256 islands, 524,288 entries and pairs; a map at most 1,024 pairs, repeated GUIDs summed | `docs/areas/pipe.md`, *Limits and errors*, *Frame layout* | confirmed |
| 6 | Largest recorded tick: 24 islands, 1,934 entries and pairs, frames ≤ 4,946 bytes | `docs/protocol.md`, *Sizes* | confirmed |
| 7 | A worst-case entry (extreme values, empty maps) is 380 JSON bytes; a tick at the pipe's entry limit is about 200 MB | scratch program (S.T.J, the property names of *Approach*): 380 × 524,288 | confirmed |
| 8 | Reading a 24 MiB worst-case tick (65,467 entries, 256 names of 255 U+FFFD) holds about 40 MiB managed after the read, input included; a 14 MB pair-heavy tick at the full 524,288 units about 127 MiB (pairs as arrays); peak working set 163 and 170 MiB, the generator included | to verify: peak working set of `ContractReader.Read` on both inputs, Dev, step 6, written into `contract.md`; so far a scratch measurement (`Utf8JsonReader` pass plus S.T.J deserialize, .NET 11, workstation GC) | unverified |
| 9 | An icon PNG is at most about 16.8 MB: side ≤ 2,048, RGBA 8 bit, one filter byte per row, zlib | `GameIconReader.MaxIconSide`, `DdsReader.Read(…, MaxIconSide)`, `PngWriter.cs` (`ZLibStream`); 2,048 × 8,193 = 16,779,264 + deflate's stored-block overhead (arithmetic); the 17,825,792 bound leaves over 1 MB | confirmed |
| 10 | Base64 of 17,825,792 bytes is 23,767,724 characters; of the catalog's 16,777,216 bytes 22,369,624; both fit 25,165,824 with the envelope | arithmetic | confirmed |
| 11 | The catalog reader is strict, returns a typed error with fixed texts, and its error factories are internal to `Censio.Contracts` | `CatalogDocumentError.cs` (`internal static Create…`), `game-catalog-format.md` | confirmed |
| 12 | The catalog writer gives the same bytes for the same catalog, so its SHA-256 identifies it | `game-catalog-format.md`, *Writing* | confirmed |
| 13 | `CatalogDocumentJsonContext` already uses `AllowDuplicateProperties = false`, `UnmappedMemberHandling.Disallow`, `RespectNullableAnnotations` | `src/Censio.Contracts/CatalogDocumentJsonContext.cs` | confirmed |
| 14 | `Utf8JsonReader` refuses a number outside float32 range (`TryGetSingle` false) and reads `-0` back as negative zero | to verify: AC2 and AC6 test cases, Tester, step 5 (S.T.J documentation, not run) | unverified |
| 15 | S.T.J Base64 reading refuses whitespace and non-canonical padding | to verify: AC6 case, Tester, step 5; if it accepts, the Dev adds the check | unverified |
| 16 | `Censio.Companion.Core` references `Censio.Contracts` and `Censio.Pipe`; the Contracts tests reference only Contracts | the `.csproj` files | confirmed |
| 17 | Records 0055 and 0056 are released (v0.2.0), so this feature adds records and edits none | `git ls-remote --tags origin`: `v0.2.0` at `abc3527`, after #163 | confirmed |
| 18 | A WebSocket close reason is at most 123 bytes | RFC 6455, 5.5 (control payload ≤ 125, 2 bytes status) | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `ContractWriterHelloRoundTripsEqual` (with and without token), `ContractWriterHelloAcceptedRoundTripsEqual` (catalog identity and null), `ContractWriterHelloRefusedRoundTripsEqual` (each reason), `ContractWriterCatalogUploadRoundTripsEqual`, `ContractWriterIconImageRoundTripsEqual`, `ContractWriterSessionStartRoundTripsEqual`, `ContractWriterSessionEndRoundTripsEqual`, `ContractWriterStatusRoundTripsEqual` (each state), `ContractWriterAcknowledgementRoundTripsEqual` (accepted with missing icons, each refusal kind with its text), `ContractWriterReadMessageWrittenAgainGivesSameBytes` (each kind) | `tests/Censio.Contracts.Tests/ContractWriterTests.cs` |
| AC1 | `ContractMessageConverterStatusMapsEveryPipeState` | `tests/Censio.Companion.Core.Tests/ContractMessageConverterTests.cs` |
| AC2 | `ContractWriterTickRoundTripsEveryValueExact`, `ContractWriterTickEdgeValuesRoundTripExact` (repeated product GUID, empty islands, all islands empty, empty name, negative GUIDs, game time `long.MinValue`/`MaxValue`, floats `-0`, subnormal, `float.MaxValue`, receive time with 7 fractional digits) | `ContractWriterTests.cs` |
| AC2 | `ContractMessageConverterTickCarriesEveryValueOfThePipeTick` | `ContractMessageConverterTests.cs` |
| AC3 | `ContractMessageConverterNonFiniteFloatCrossesAsNoValue` (NaN, +∞, −∞ in each float field, through write and read) | `ContractMessageConverterTests.cs` |
| AC4 | `ContractReaderSessionStartWithTextIsMalformed`, `ContractWriterTickAndSessionEventsKeepTheirProfile` | `ContractReaderTests.cs`, `ContractWriterTests.cs` |
| AC5 | `ContractReaderOtherVersionIsUnsupportedVersion` (0, 2, −1, `int.MaxValue`, also with a body malformed under version 1), `ContractReaderHelloOfOtherVersionIsRefusedWithItsVersion`, `ContractReaderFutureServerRefusalReadsVersionsBack` (version 2, received 1, supported `[2]`), `ContractReaderOtherVersionRefusalOfUnknownFormIsUnsupportedVersion` | `tests/Censio.Contracts.Tests/ContractReaderTests.cs` |
| AC6 | `ContractReaderMalformedInputIsMalformed`, `ContractReaderRuleBreakIsInvalid`, `ContractReaderRefusalTextHoldsNoContent`, `ContractWriterRuleBreakIsRefusedAndWritesNothing` (cases: *Approach*, *Reader*) | `ContractReaderTests.cs`, `ContractWriterTests.cs` |
| AC7 | `ContractWriterTickAtPipeCountLimitsRoundTrips`, `ContractReaderMessageAtByteBoundIsRead` (padded with JSON whitespace), `ContractReaderInputAboveByteBoundIsTooLarge`, `ContractReaderCountAboveBoundIsTooLarge`, `ContractWriterTickAboveByteBoundIsTooLarge`, `ContractWriterIconAtBinaryBoundRoundTrips`, `ContractWriterCatalogAtCatalogBoundRoundTrips`, `ContractWriterIslandNameOfReplacementCharactersRoundTrips` | `ContractReaderTests.cs`, `ContractWriterTests.cs` |
| AC8 | `ContractReaderCatalogUploadWithRefusedDocumentIsCatalogRefused` (catalog version 2 with text `Catalog schema version 2 is not supported; supported: 1`, malformed, invalid, too large) | `ContractReaderTests.cs` |
| AC9 | `ContractMessageConverterSessionEventsTakenSeparatelyGetDifferentIdentities`, `ContractMessageConverterSessionEventCarriesItsReceiveTime`, `ContractWriterWritingAgainKeepsTheIdentity` | `ContractMessageConverterTests.cs`, `ContractWriterTests.cs` |

## Approach

New output is a wire format, no user-visible text beyond the refusal texts the spec fixes.

**Wire form.** UTF-8 JSON without byte order mark, compact. Every message is an object whose first properties are
`type` and `contractVersion` (`1`), then its own in the order below; every property is required and always written,
`null` only where stated. Names are camelCase, enum values camelCase strings.

| `type` | Direction | Properties |
| ------ | --------- | ---------- |
| `hello` | C → S | `token` (string or null), `companionVersion`, `profileId`, `profileName` |
| `helloAccepted` | S → C | `serverVersion`, `catalogIdentity` (64 lowercase hex or null) |
| `helloRefused` | S → C | `reason` (`unsupportedVersion`, `tokenRefused`, `companionConnected`), `receivedVersion`, `supportedVersions` |
| `catalogUpload` | C → S | `id`, `document` (Base64 of the catalog writer's bytes) |
| `iconImage` | C → S | `id`, `reference`, `png` (Base64) |
| `tick` | C → S | `id`, `profileId`, `receiveTime`, `gameTime`, `islands` |
| `sessionStart`, `sessionEnd` | C → S | `id`, `profileId`, `receiveTime` |
| `status` | C → S | `pipeState` (`waitingForGame`, `connected`, `receiving`, `pipeBusy`, `protocolMismatch`, `error`) |
| `acknowledgement` | S → C | `id`, `refusal` (null, or `malformed`, `unsupportedVersion`, `invalid`, `tooLarge`, `catalogMalformed`, `catalogUnsupportedVersion`, `catalogInvalid`, `catalogTooLarge`), `refusedVersion`, `missingIcons` |

An island is `sessionGuid`, `islandId`, `areaIndex`, `sessionId`, `name`, `entries`; an entry is `productGuid`,
`generation`, `consumption`, `delta`, `perfectGeneration`, `perfectConsumption`, `buildingCount`, `maintenance`,
`income`, `profit`, `summedProductivity`, `averageProductivity`, `workforceAmounts`, `buildingAmounts`. A float is a
JSON number in float32 range written shortest-round-trip, or `null` for no value. A map is an array of
`[guid, amount]` pairs in ascending GUID order. UUIDs are 36-character lowercase hyphenated text, never all zero. A
receive time is `yyyy-MM-ddTHH:mm:ss.fffffffZ` exactly (UTC, 7 digits). Islands and entries keep the tick's order;
maps and `missingIcons` are written sorted, so the same message gives the same bytes.

**Frozen form (record 0058).** `type` and `contractVersion` are root properties in every version; `hello` and
`helloRefused` keep exactly the properties above, the three reasons included, in every later version. The endpoint
(#28) checks the version before the token and the second Companion.

**Reader** (`ContractReader.Read`), in this order, the first failing step deciding:

1. Input over 25,165,824 bytes: *too large*, before parsing.
2. One syntax pass (`Utf8JsonReader`, like `CatalogDocumentScanner`): well-formed JSON, root an object, depth ≤ 64,
   nothing after it, valid UTF-8, no lone surrogate also when escaped; each string's raw length is checked before it is
   unescaped and its unescaped UTF-8 length after, against its bound; the counts are checked before anything is built;
   root `contractVersion` present once as an int32 integer without fraction or exponent, root `type` once as a string.
   A failure is *malformed*, a bound *too large*.
3. `contractVersion` ≠ 1: a `helloRefused` of exactly the frozen form is read by the rules below and returned; anything
   else is *unsupported version* with the number found.
4. Structure of the type: deserialize with a source-generated context of the catalog's strict options (no unknown,
   repeated or missing property, exact case, JSON types, integer ranges, `byte` 0–255, float32 range, canonical Base64
   without whitespace). Unknown `type`: *malformed*.
5. Value rules: *invalid*. Then a `catalogUpload`'s decoded document goes through `CatalogDocumentReader.Read`; its
   refusal is *catalog refused* with the catalog error and its text.

| Bound | Value | Measured |
| ----- | ----- | -------- |
| Message | 25,165,824 bytes (24 MiB) | before parsing |
| `png`, `document` | 23,767,724 characters, decoded at most 17,825,792 bytes (`document` then also the catalog's 16 MiB) | after unescaping |
| Token, versions, profile name | 1,024 UTF-8 bytes | after unescaping |
| Island name | 765 UTF-8 bytes | after unescaping |
| Islands per tick / entries and pairs per tick / pairs per map | 256 / 524,288 / 1,024 | counted in step 2 |
| `supportedVersions` / `missingIcons` | 16 / 16,384 | counted in step 2 |

Value rules (*invalid*): UUID form; receive time form; a tick of 1 to 256 islands with distinct keys; map GUIDs
strictly ascending; token null or not empty; profile name, Companion and server version not empty; `reference` and
`catalogIdentity` 64 lowercase hex; `png` not empty and its SHA-256 equal to `reference`; `supportedVersions` 1 to 16
distinct ascending positive values; `refusedVersion` set exactly for the two *unsupported version* refusals;
`missingIcons` 64 lowercase hex, strictly ascending, empty for a refusal.

*Malformed* cases for AC6: not JSON, truncated, unknown type, unknown/repeated/missing property, wrong JSON type, content
after the root, depth 65, an escaped lone surrogate, Base64 with whitespace or wrong padding, a number outside float32,
a `byte` of 256. *Invalid*: each value rule above. Lone-surrogate inputs live in raw string literals (`.squad/team.md`,
*Escapes in files*); the test fails if the backslash is lost because the input then is a different, valid string.

**Texts.** `Contract message is malformed`, `Contract version <n> is not supported; supported: 1`,
`Contract message breaks a rule of contract version 1`, `Contract message exceeds a size bound`; *catalog refused*
carries the catalog's text. `ContractAcknowledgement.GetRefusalText()` gives the same texts for its kinds (catalog
kinds: the catalog's texts, built with its internal factories), so the Companion never shows text from the server.

**Writer** (`ContractWriter.Write`): checks the same bounds and rules (a non-finite float, a receive time with an
offset other than zero, an all-zero UUID are *invalid*), writes nothing on a refusal, and serializes with the same
context. A tick over the message bound is *too large*.

**Converter** (`ContractMessageConverter`, `Censio.Companion.Core`): a pipe tick to a contract tick with a new
`Guid.NewGuid()` id, every non-finite float as `null`, maps as they are (the writer sorts); session events with a new id
and the receive time the caller gives; the six `PipeConnectionState` values one to one. The message types are classes,
not records, so no generated `ToString` prints the token or a name.

## Options considered

- Strictness and versions: tolerant reader, major/minor, negotiation, refusals outside the contract — lost to exact
  version with a frozen hello (record 0058).
- Identity and profile: content hash, sequence number, profile per connection — lost to a Companion-assigned UUID and
  the profile per message (record 0059).
- Bounds and binary data: a bound at the pipe's tick limits (about 200 MB), ticks split into parts, binary WebSocket
  frames — lost to one 24 MiB bound and Base64 (record 0060).
- Polymorphic S.T.J (`[JsonPolymorphic]`) against dispatch on the `type` the syntax pass found: dispatch chosen, the
  pass already knows the type, and polymorphic reading needs the discriminator first or out-of-order metadata.
- Several projects for the converter: `Censio.Contracts` references no other project (record 0056), so the converter
  sits in `Censio.Companion.Core` beside `GameCatalogDocumentConverter`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Contracts` | the types of *Signatures*, `ContractJsonContext` (internal), the internal syntax pass and converters | new |
| `Censio.Companion.Core` | `ContractMessageConverter` | new |
| tests | `ContractReaderTests`, `ContractWriterTests`, `ContractMessageConverterTests` | new |

## Signatures (for the Dev's skeleton)

Namespace `Censio.Contracts` unless named. Properties are public required init-only properties of the type and C#
type given in prose (no backticks); enum members likewise.

- `public abstract class ContractMessage` — abstract property ContractMessageType Type (get only), overridden by each message.
- `public enum ContractMessageType` — Hello, HelloAccepted, HelloRefused, CatalogUpload, IconImage, Tick, SessionStart, SessionEnd, Status, Acknowledgement.
- `public sealed class ContractHello : ContractMessage` — string? Token, string CompanionVersion, Guid ProfileId, string ProfileName.
- `public sealed class ContractHelloAccepted : ContractMessage` — string ServerVersion, string? CatalogIdentity.
- `public sealed class ContractHelloRefused : ContractMessage` — ContractHelloRefusal Reason, int ReceivedVersion, IReadOnlyList<int> SupportedVersions.
- `public enum ContractHelloRefusal` — UnsupportedVersion, TokenRefused, CompanionConnected.
- `public sealed class ContractCatalogUpload : ContractMessage` — Guid Id, ReadOnlyMemory<byte> Document.
- `public string ComputeIdentity()` on ContractCatalogUpload — SHA-256 of Document, 64 lowercase hex.
- `public sealed class ContractIconImage : ContractMessage` — Guid Id, string Reference, ReadOnlyMemory<byte> Png.
- `public sealed class ContractTick : ContractMessage` — Guid Id, Guid ProfileId, DateTimeOffset ReceiveTime, long GameTime, IReadOnlyList<ContractTickIsland> Islands.
- `public sealed class ContractTickIsland` — int SessionGuid, byte IslandId, byte AreaIndex, byte SessionId, string Name, IReadOnlyList<ContractTickEntry> Entries.
- `public sealed class ContractTickEntry` — int ProductGuid; float? Generation, Consumption, Delta, PerfectGeneration, PerfectConsumption; int BuildingCount, Maintenance; float? Income; int Profit; float? SummedProductivity, AverageProductivity; IReadOnlyDictionary<int, int> WorkforceAmounts, BuildingAmounts.
- `public sealed class ContractSessionStart : ContractMessage` — Guid Id, Guid ProfileId, DateTimeOffset ReceiveTime.
- `public sealed class ContractSessionEnd : ContractMessage` — the same three.
- `public sealed class ContractStatus : ContractMessage` — ContractPipeState PipeState.
- `public enum ContractPipeState` — WaitingForGame, Connected, Receiving, PipeBusy, ProtocolMismatch, Error.
- `public sealed class ContractAcknowledgement : ContractMessage` — Guid Id, ContractAcknowledgementRefusal? Refusal, int? RefusedVersion, IReadOnlyList<string> MissingIcons.
- `public string? GetRefusalText()` on ContractAcknowledgement — null when accepted.
- `public enum ContractAcknowledgementRefusal` — Malformed, UnsupportedVersion, Invalid, TooLarge, CatalogMalformed, CatalogUnsupportedVersion, CatalogInvalid, CatalogTooLarge.
- `public enum ContractErrorKind` — Malformed, UnsupportedVersion, Invalid, TooLarge, CatalogRefused.
- `public sealed class ContractError` — get-only ContractErrorKind Kind, string Text, int? Version, CatalogDocumentError? CatalogError; internal constructor.
- `public sealed class ContractReadResult` — get-only ContractMessage? Message, ContractError? Error; internal constructor.
- `public sealed class ContractWriteResult` — get-only ReadOnlyMemory<byte> Utf8Json, ContractError? Error; internal constructor.
- `public static class ContractReader` — public const int CurrentVersion = 1, MaxMessageBytes = 25165824, MaxBinaryBytes = 17825792, MaxStringBytes = 1024, MaxIslandNameBytes = 765, MaxIslands = 256, MaxEntriesAndPairs = 524288, MaxPairsPerMap = 1024, MaxSupportedVersions = 16, MaxMissingIcons = 16384.
- `public static ContractReadResult Read(ReadOnlySpan<byte> utf8Json)` on ContractReader.
- `public static class ContractWriter`
- `public static ContractWriteResult Write(ContractMessage message)` on ContractWriter.
- `public static class ContractMessageConverter` in `Censio.Companion.Core`.
- `public static ContractTick ToTick(PipeTick tick, Guid profileId)`
- `public static ContractSessionStart ToSessionStart(Guid profileId, DateTimeOffset receiveTime)`
- `public static ContractSessionEnd ToSessionEnd(Guid profileId, DateTimeOffset receiveTime)`
- `public static ContractStatus ToStatus(PipeConnectionState state)`

Names checked: no enum ends in Flags or Enum, no nested type; the Contract prefix avoids a clash with the pipe's
SessionStartMessage inside Censio.Companion.Core. No existing file is rewritten by the skeleton.

## Test files

- `tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `tests/Censio.Contracts.Tests/ContractWriterTests.cs`,
  `tests/Censio.Companion.Core.Tests/ContractMessageConverterTests.cs`.
- Existing test code calling a changed signature: none.
- Files no unit test can cover: none.

## Areas

New area `contract` — scope for the index: "The JSON contract between Companion and server: messages, versions and the
frozen hello, identities, the reader's checks, bounds, refusal texts, privacy"; not here: sending, receiving and the
endpoint (#28, #40), the catalog document itself (game-catalog-format).

## Documentation updates

All by the Dev, against the green code:

- `docs/areas/contract.md` (new, from `docs/areas/_template.md`): the wire form with one example of invented values per
  message, the frozen form and the version-first rule, identities and acknowledgement meaning (accepted or refused: the
  Companion may discard; no answer: resend), the reader's order, the bound table with the measured memory of fact 8,
  texts, what never crosses (figure name, pipe errors, close reason), privacy (token, profile and island names private,
  no refusal text or log line holds them); links records 0058–0060, 0018, 0041, 0056.
- `docs/areas/README.md`: the row of `contract` (*Areas*).
- `docs/areas/game-catalog-format.md`, *Not here*: "Sending … (#27, #42)" becomes the upload's messages in
  [`contract.md`](contract.md), sending in #42.
- `docs/ARCHITECTURE.md`: project table row `Censio.Contracts` adds "the contract messages with their reader and writer
  ([`areas/contract.md`](areas/contract.md))"; *Data flow* after line 60: the Companion converts ticks, session events
  and its pipe state into the messages of contract v1, which nothing sends yet (#28, #40), records 0058–0060.
- `.squad/project.md` (the Dev reports the text, the orchestrator writes it): *Security areas*, *Parsing of external
  input*: "later WebSocket messages" becomes "the contract messages (`ContractReader` in `src/Censio.Contracts`, read by
  the server and the Companion)" with a link to `contract.md`.
- README and user guide: none (spec, *Out of scope*); the README *Status* line stays true.
- Sweep: `docs/areas/*.md`, `ARCHITECTURE.md`, `SECURITY.md`, README and user guide searched for `contract`,
  `Censio.Contracts`, `#27`: the hits above only.

## Architecture check

Record 0018 is built as decided; the token guarantee (0019) is untouched, the hello only carries it. The buffer guarantee
(0031): a tick over the message bound is never sent and so is a gap, never zeros. Record 0041: no message has a figure
name field. `Censio.Contracts` keeps no project reference (0056).

## Security considerations

- [x] Byte limits: the message bound applies to raw input before parsing; string bounds to the unescaped UTF-8 length,
  checked after a raw-length pre-check; island names at 765 bytes after U+FFFD replacement; binary bounds after Base64
  decoding.
- [x] Exceptions: none reaches a text; the reader catches `JsonException`, `InvalidOperationException`,
  `ArgumentException`, `FormatException` from deserialization like `CatalogDocumentReader` and returns *malformed*.
  `ArgumentNullException` for a null message in the writer and converter only.
- PNG data is carried, hashed and bounded, not validated as PNG: whoever serves it to a browser (#42) checks it.
- Memory: input plus model, fact 8; the endpoint bounds the WebSocket message at the same size (#28).
- The token, profile and island names appear in no error, text or `ToString`.

## Decision records

- [`0058`](../../docs/decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md) (Proposed)
- [`0059`](../../docs/decisions/0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md) (Proposed)
- [`0060`](../../docs/decisions/0060-contract-message-bound-caps-ticks-below-the-pipes-limits.md) (Proposed)

Their links to `areas/contract.md` and the area name `contract` resolve once the Dev adds the area document and its row;
`decision-check.py` reports them until then.

## Out of scope / follow-ups

Spec, *Out of scope*. No follow-up issue: #28, #40, #41, #42 exist and consume this contract.
