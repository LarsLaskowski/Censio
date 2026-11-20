# Plan: Decode trade routes and item offers of pipe protocol 3

Source: Issue #221 | [spec.md](spec.md)
Tier: security — the change rewrites the parsing of untrusted pipe input (new fail-closed checks and caps) and the anonymiser's privacy rules for player-typed text, both security controls.
Security areas touched: Parsing of external input (`PipeMessageParser`, the new message types, `PipeFrameAnonymiser`, `PipeAnonymousNames` in `src/Censio.Pipe`); logging and showing external data (route and folder names never reach a log or an error).

## Problem / root cause

See [spec.md](spec.md), *Problem*. Today `PipeMessageParser.ParseUndecoded` (`src/Censio.Pipe/PipeMessageParser.cs:117-129`) reads only the leading count of types 4 and 5, and `PipeFrameAnonymiser.Anonymise` (`src/Censio.Pipe/PipeFrameAnonymiser.cs:127`) writes `[type, 0, 0, 0, 0]`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The reader checks only the leading count of types 4 and 5 and returns `UndecodedMessage` | `PipeMessageParser.cs:72`, `:117-129` | confirmed |
| 2 | The anonymiser writes types 4 and 5 as `[type, 0, 0, 0, 0]` | `PipeFrameAnonymiser.cs:127` | confirmed |
| 3 | Type 4 layout: int32 count; per route int32 ID, string name, string folder, int32 Errors, Warnings, Ships, LostShips, int32 station count; per station int32 ID, bytes session ID, island ID, area index, int32 session GUID, int32 owner, flags byte (bit 0 throw overboard, bit 1 wait until full, bit 2 wait until empty), int32 goods count; per good int32 GUID, int32 amount, flags byte (bit 0 loading) | reference `4cc8ad7`, `src/pipe.cpp:123-188`, `:402-421` | confirmed |
| 4 | Type 5 layout: int32 count; per trader int32 ID, int32 item count, that many int32 item GUIDs | reference `src/pipe.cpp:192-200`, `:422-445` | confirmed |
| 5 | The reference reads a missing int as 0 (`pipe.cpp:17-29`), loops zero times on a negative top-level count, and hands a negative nested count to `std::vector::resize`, which throws; `catch (...)` exits the program (`:136`, `:168`, `:195`, `:451-455`); short station and goods fields `return` early with defaults (`:148-151`, `:158-161`, `:182-185`); so the issue's "zero loop passes" holds for the top-level count only | reference source | confirmed |
| 6 | The reference keeps the last offer of a repeated trader (`itemOffers[trader] = …`, `:443`) and groups routes by folder without the ID (`tradeData[entry.Folder]`, `:419`) | reference source | confirmed |
| 7 | The supplied recording (outside the repository) holds 325 frames: 1 version (3), 1 session start, 300 island frames, 11 trade-route and 12 item-offer frames, no session end | Lead's decode with a scratch script, counts only | confirmed |
| 8 | Every type 4 and 5 frame of it decodes with the layout of rows 3 and 4, ends exactly at the frame's end, and no route or folder name holds U+FFFD or a non-ASCII byte (AC12 holds on the sample) | same decode | confirmed |
| 9 | Its nested counts: 57 routes per frame, at most 4 stations per route (none without), at most 5 goods per station (none without), 7 traders, at most 6 items per trader (none without); no missing or negative count; frames 8,186 and 229 bytes | same decode | confirmed |
| 10 | Its values: problem counts all 0, ships at most 2, lost ships all 0 (so never above ships), goods amounts 1 to 50 (none 0 or negative), one owner value, station flags only 0 or bit 2, goods flags only 0 or bit 0 (no unnamed bit), no repeated route ID, trader ID or item GUID within a list; 56 distinct route names over 57 routes, 9 folders | same decode | confirmed |
| 11 | All 11 trade-route frames are byte-identical; the 12 item-offer frames are 9 distinct ones | same decode | confirmed |
| 12 | `UndecodedMessage` and `PipeFrameBuilder.UndecodedEntries` are used only by the parser, the anonymiser, `PipeMessageParserTests`, `PipeMessageReaderTests`, `PipeTickBuilderTests`, `PipeRecordingAnonymiserTests` and, through the linked builder, `PipeConnectionTests` (`UndecodedFrame`, `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs:2041-2044`, used by `PipeConnectionTradeRoutesAndItemOffersRaiseNoEvent`, `:457-473`) | grep of `src` and `tests` for both names | confirmed |
| 13 | `PipeTickBuilder.AddMessage` (`PipeTickBuilder.cs:142-174`) switches on island, session and version messages only; any other message adds no item | source | confirmed |
| 14 | No write-side length check exists: `Anonymise` returns the written bytes after `IsReadBack` only (`PipeFrameAnonymiser.cs:114-131`, `:367-378`) | source | confirmed |
| 15 | `PipeAnonymousNames` keeps one `_originals` set over every kind, so the collision skip already spans kinds once routes and folders are added to it | `PipeAnonymousNames.cs` | confirmed |
| 16 | Record 0094 is unreleased | `git tag --contains 30e8960` lists no tag | confirmed |
| 17 | Neither the Companion, the server nor `DiagnosticsView` reads types 4 or 5 | `grep -rn "TradeRoutes\|ItemOffers" src` hits only `Censio.Pipe` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeMessageParserTradeRoutesReturnsEveryField`, `PipeMessageParserTradeRoutesKeepsRepeatedIds`, `PipeMessageParserTradeRoutesKeepsNamesAsDecoded`, `PipeMessageParserTradeRoutesOrItemOffersIgnoresTrailingBytes`; `PipeMessageReaderTradeRoutesAfterVersionThreeReturnsTradeRoutesMessage`, `PipeMessageReaderTradeRoutesAfterVersionTwoReturnsTradeRoutesMessage` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`, `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs` |
| AC2 | `PipeMessageParserItemOffersReturnsEveryOffer`, `PipeMessageParserItemOffersKeepsRepeatedTradersAndItems` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` |
| AC3 | `PipeMessageParserTradeRoutesOrItemOffersZeroCountGivesEmptyList`; `PipeMessageReaderProtocolThreeFixtureGivesEmptyTradeRoutesAndItemOffers` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`, `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs` |
| AC4 | `PipeMessageParserTradeRoutesOrItemOffersMissingCountReturnsMalformed`, `PipeMessageParserTradeRoutesOrItemOffersNegativeCountReturnsMalformed`, `PipeMessageParserTradeRoutesShortFieldReturnsMalformed`, `PipeMessageParserTradeRoutesStringPastFrameReturnsMalformed` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` |
| AC5 | `PipeMessageParserTradeRoutesOrItemOffersCountAboveCapReturnsMalformed`, `PipeMessageParserTradeRoutesOrItemOffersCountAtCapReturnsMessage`, `PipeMessageParserTradeRoutesOrItemOffersCountBeyondFrameReturnsMalformed` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` |
| AC6 | `PipeMessageParserTradeRoutesUnnamedFlagBitsAreIgnored` | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs` |
| AC7 | `PipeTickBuilderTradeRoutesOrItemOffersKeepOpenTick`, `PipeTickBuilderTradeRoutesOrItemOffersDoNotRestartQuietPeriod`, `PipeTickBuilderTradeRoutesOrItemOffersKeepRepeatedSessionEnd`; `PipeConnectionTradeRoutesAndItemOffersRaiseNoEvent` (frames rebuilt in the reference layout, so they decode) | `tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs`, `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC8 | `PipeRecordingAnonymiserRouteAndFolderNamesAreInvented`, `PipeRecordingAnonymiserInventedRouteNameSkipsOriginalOfAnotherKind`, `PipeRecordingAnonymiserEmptyRouteAndFolderNamesStayEmpty`; `FixtureDenyListFixturesHaveInventedNames` (extended to routes and folders) | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs`, `tests/Censio.Pipe.Tests/FixtureDenyListTests.cs` |
| AC9 | `PipeRecordingAnonymiserTradeRoutesKeepStructure`, `PipeRecordingAnonymiserTradeRouteAmountsChange`, `PipeRecordingAnonymiserLostShipsStayAtMostShips`, `PipeRecordingAnonymiserNegativeGoodsAmountIsKept`, `PipeRecordingAnonymiserUnnamedFlagBitsAreWrittenAsZero`, `PipeRecordingAnonymiserItemOffersKeepStructure` | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs` |
| AC10 | `PipeRecordingAnonymiserMalformedTradeRoutesOrItemOffersAreRefused`, `PipeRecordingAnonymiserTradeRoutesOverOneMebibyteAfterRewritingAreRefused` | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs` |
| AC11 | `PipeMessageReaderFixturesGiveReadmeFrameCounts` and `PipeTickAssemblerFixturesGiveReadmeTicksAndEvents` (a `DataRow` for `protocol-3-trade-routes.jsonl` each); manual: the Tester, in step 5, runs `FixtureDenyListFixturesAgainstLocalDenyList` with a scratch deny list naming the supplied recording as the fixture's original and every original name as denied | `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`, `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs`, `tests/Censio.Pipe.Tests/FixtureDenyListTests.cs` |
| AC12 | `FixtureDenyListOriginalTradeFrameWithTrailingBytesIsReported`, `FixtureDenyListOriginalTradeFrameWithReplacementCharacterIsReported`; manual: the Tester, in step 5, runs `FixtureDenyListOriginalTradeFramesDecodeToTheirEnd` against the supplied recording (inconclusive without `CENSIO_FIXTURE_DENY_LIST`), counts only | `tests/Censio.Pipe.Tests/FixtureDenyListTests.cs` |

## Approach

**Group G1 — reader.**

- New message types replace `UndecodedMessage` (deleted): `TradeRoutesMessage` with its routes, `ItemOffersMessage`
  with its offers, and the plain data classes `TradeRoute`, `TradeRouteStation`, `TradeRouteGood`, `ItemOffer`
  (*Signatures*), in the shape of `AreaProductionStatisticsMessage` and `ProductionEntry`: `required` init
  properties, `IReadOnlyList<…>` in frame order. Flags are delivered as named booleans; unnamed bits are not kept.
- `PipeMessageParser.Parse` dispatches type 4 to a private `ParseTradeRoutes` and type 5 to `ParseItemOffers`, built
  of private readers per level (`ParseTradeRoute`, `ParseStation`, `ParseGood`, `ParseItemOffer`) over the existing
  `TryReadInt`, `TryReadByte`, `TryReadString`. One private helper `TryReadCount(frame, ref position, minimumSize, cap,
  out count)` reads a count and rejects it — position reset to the count's offset, as `TryReadMap` does — when it is
  missing, negative, above `cap`, or above `(frame.Length - position) / minimumSize`, before any list is created. The
  list is then created with that capacity. Minimum sizes in bytes (private constants): route 26 (ID, two empty
  strings, four ints, station count), station 20, good 9, offer 8, item 4.
- Caps (public constants in `PipeProtocol`, *Signatures*): 1,024 routes per frame, 256 stations per route, 256 goods
  per station, 1,024 offers per frame, 1,024 items per offer. Margins over the recording (fact 9): 18x, 64x, 51x, 146x,
  170x. Memory needs no cap: the bytes-left check bounds every list by the frame; the largest decoded message of a
  1 MiB frame is at most about 116,000 goods objects, a few MiB, held only for the read.
  The seam the tests observe the bounds through: a count above its cap in a frame that holds that many complete
  elements, and a count at most its cap that the rest of the frame cannot hold, each give the malformed error at the
  count's own offset (`PipeError.Offset`); without the check the parse fails later, at a deeper offset.
- A malformed error carries type value and offset only (`PipeError`, unchanged): nothing of a name.
- `PipeTickBuilder` needs no change (fact 13); its three tests are adapted to the new messages.

**Group G2 — anonymiser.**

- `PipeAnonymousNames` gains routes and folders (*Signatures*); `Complete` invents `Route <n>` and `Folder <n>` after
  islands and players, each kind numbered from 1, every candidate checked against the shared `_originals` (fact 15),
  so E12 holds across all four kinds.
- `PipeFrameAnonymiser.TryCollect` adds each route's name and folder. `Anonymise` writes a trade-routes frame from the
  decoded message by a private `WriteTradeRoutes` and an item-offers frame by `WriteItemOffers`, never copying frame
  bytes: IDs, island key bytes, session GUID, owner, list counts and order as decoded; flags from the named booleans
  only (unnamed bits 0); item GUIDs as decoded; names through `TryGetRoute`/`TryGetFolder` (an uncollected name gives
  `null`, as for islands).
- Integer amounts, drawn strictly in frame order with the existing `PerturbInteger` (two draws per value, a zero
  included, `PipeFrameAnonymiser.cs:670-675`): route 1's major problems, minor problems, ships and lost ships, then
  route 1's goods amounts station by station, then route 2, and so on. Only a negative goods amount is written
  unchanged and takes no draw. Lost ships: when the original lost-ship count is at
  most the original ship count and the perturbed lost ships exceed the perturbed ships, lost ships become the
  perturbed ships, or the perturbed ships minus 1 when that would equal the original non-zero lost-ship count. That
  value is never the original, and at least 0 when the original ship count is positive.
- `IsReadBack` gains private structure checks for both messages: same route, station, goods, offer and item counts,
  and equal IDs, island keys, owners, named flags, goods GUIDs and loading, trader IDs and item GUIDs. Then
  `Anonymise` refuses any written frame longer than `PipeProtocol.MaxFrameLength`, for every type (E11).
- Every frame draws its amounts afresh, as island frames do (record 0042). A recording that repeats one trade-route
  frame (all 11 of the sample are byte-identical, fact 11) therefore lets a reader infer a small original value from
  the spread of its copies (2 to 4 move by exactly 1; 1 always becomes 2). This is accepted and documented as a
  residual risk, not prevented (plan challenge, objection 4).

**Group G3 — fixture and documentation.**

- The Tester makes `tests/Censio.Pipe.Tests/Fixtures/protocol-3-trade-routes.jsonl` from the supplied recording with
  the branch's `Censio.Anonymiser` (`dotnet run --project src/Censio.Anonymiser -c Release -- <recording>
  <fixture>`). The recording and any deny list stay in the scratch directory, never in the repository. Before the
  Tester hands back, the deny-list run of AC11 must report no finding for the new fixture. The two older fixtures
  have no original in the session, and their findings for that are expected and named. `protocol-3-session.jsonl`
  stays as it is for AC3.
- `FixtureDenyList` (test helper, Tester): `CheckNames` also checks route names (`Route <n>`, kind `route`) and
  folders (`Folder <n>`, kind `folder`). `CompareFrames` compares problem counts, ships, lost ships and goods
  amounts, skipping negative goods amounts. A new check over an original reports each type 4 or 5 frame that fails
  to parse, ends before the frame's end, or holds U+FFFD in a name: by line number and field, never by value.
  `FixtureDenyListOriginalTradeFramesDecodeToTheirEnd` runs it on the deny list's original of
  `protocol-3-trade-routes.jsonl` and is inconclusive without the variable.
- No new user-visible output: the anonymiser's messages are unchanged.

## Options considered

The reader and anonymiser options are in record 0095 (decode strictly vs. skip vs. read as the reference; ordered
lists vs. the reference's keyed shapes; ignore vs. reject unnamed flag bits; perturb vs. keep the integers; keep
emptying). Plan-level alternatives:

- **Parse in a new class beside `PipeMessageParser`**: shorter files, but the field readers are private there, and
  sharing them means moving them all. Lost: one parser keeps one set of rules.
- **Replace `protocol-3-session.jsonl` instead of adding a fixture**: one fixture fewer, but its emptied frames are
  the only real input of AC3 and its README facts are asserted by server-free tests. Lost.
- **Caps only through the bytes-left check**: simpler, but record 0033 and the map cap set a plausibility bound as
  well. Lost.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `TradeRoutesMessage.cs`, `TradeRoute.cs`, `TradeRouteStation.cs`, `TradeRouteGood.cs`, `ItemOffersMessage.cs`, `ItemOffer.cs` | new |
| Censio.Pipe | `UndecodedMessage.cs` | deleted |
| Censio.Pipe | `PipeMessageParser.cs` | types 4 and 5 decoded |
| Censio.Pipe | `PipeProtocol.cs` | five caps |
| Censio.Pipe | `PipeAnonymousNames.cs`, `PipeFrameAnonymiser.cs` | routes, folders, amounts, read-back, size check |
| Censio.Pipe.Tests | `PipeFrameBuilder.cs`, the test files below, `FixtureDenyList.cs`, `Fixtures/` | builders, tests, fixture |

## Signatures (for the Dev's skeleton)

Namespace Censio.Pipe; each type in its own file; every property below is an init-only, required property.

- `public sealed class TradeRoutesMessage : PipeMessage` — `public required IReadOnlyList<TradeRoute> Routes`;
  `public override PipeMessageType MessageType => PipeMessageType.TradeRoutes`
- `public sealed class TradeRoute` — `public required int Id`, `public required string Name`,
  `public required string Folder`, `public required int MajorProblems`, `public required int MinorProblems`,
  `public required int Ships`, `public required int LostShips`, `public required IReadOnlyList<TradeRouteStation> Stations`
- `public sealed class TradeRouteStation` — `public required int Id`, `public required byte SessionId`,
  `public required byte IslandId`, `public required byte AreaIndex`, `public required int SessionGuid`,
  `public required int Owner`, `public required bool ThrowOverboard`, `public required bool WaitUntilFull`,
  `public required bool WaitUntilEmpty`, `public required IReadOnlyList<TradeRouteGood> Goods`
- `public sealed class TradeRouteGood` — `public required int ProductGuid`, `public required int Amount`,
  `public required bool Loading`
- `public sealed class ItemOffersMessage : PipeMessage` — `public required IReadOnlyList<ItemOffer> Offers`;
  `public override PipeMessageType MessageType => PipeMessageType.ItemOffers`
- `public sealed class ItemOffer` — `public required int TraderId`, `public required IReadOnlyList<int> ItemGuids`
- `PipeProtocol`: `public const int MaxTradeRoutes = 1024;`, `public const int MaxRouteStations = 256;`,
  `public const int MaxStationGoods = 256;`, `public const int MaxItemOffers = 1024;`,
  `public const int MaxOfferItems = 1024;`
- `PipeAnonymousNames` (internal): `public void AddRoute(string name)`, `public void AddFolder(string name)`,
  `public bool TryGetRoute(string original, out string invented)`, `public bool TryGetFolder(string original, out string invented)`

Deleted: UndecodedMessage and its constructor; the private PipeMessageParser.ParseUndecoded is removed.

The parser's Parse and the anonymiser's TryCollect and Anonymise keep their signatures. Names checked against
*Writing code*: no nested type, no enum suffix the analyzers refuse, no parameter list over seven, no CA1716 keyword.

## Test files

`tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`, `PipeMessageReaderTests.cs`, `PipeTickBuilderTests.cs`,
`PipeRecordingAnonymiserTests.cs`, `PipeTickAssemblerTests.cs`, `FixtureDenyListTests.cs`, and
`tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` (all existing), the helper
`FixtureDenyList.cs`, and the new fixture `Fixtures/protocol-3-trade-routes.jsonl`.

Existing call sites the change breaks, all adapted by the Tester:
- `PipeFrameBuilder.UndecodedEntries` (`PipeFrameBuilder.cs:180`) writes a layout that is not the reference's (no
  Warnings, no LostShips). It is replaced by builders of the real layout (`TradeRoute…`, `ItemOffer…`).
- Its callers: `PipeMessageParserTests.cs:302-377`, `PipeMessageReaderTests.cs:479-514` and `:682`,
  `PipeRecordingAnonymiserTests.cs:985` (`…AreWrittenEmpty`, replaced by the AC9 tests), and the refusal test after
  it (kept as AC10).
- `new UndecodedMessage(…)` in `PipeTickBuilderTests.cs:629-686`.
- `UndecodedFrame` in `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs:2041-2044` (the linked builder),
  rebuilt from the new reference-layout builders, so `PipeConnectionTradeRoutesAndItemOffersRaiseNoEvent` sends
  frames that decode and not malformed ones (fact 12).

The Anonymiser tests link `PipeFrameBuilder` too but do not call `UndecodedEntries` (fact 12).

Files no unit test can cover: none.

## Areas

- `docs/areas/pipe.md`:
  - *Message types*: rows 4 and 5 with their layout; the paragraph on undecoded messages replaced by the rules (frame
    order, repeats kept, flags, string rules, trailing bytes, no state).
  - *Ticks and sessions*: "an undecoded message (type 4 or 5)" becomes "a trade-routes or item-offers message".
  - *Limits and errors*: the five caps, and the malformed causes for types 4 and 5 (missing or negative count at any
    level, count above its cap or the bytes left, short field).
  - *Deliberate differences from the reference*: missing fields as 0, the negative top-level count, the reference's
    exit on a negative nested count, last offer per trader, routes grouped by folder.
  - *Residual risks*: the type 4/5 bullet now rests on two recordings.
- `docs/areas/pipe-recording.md`:
  - *Anonymising*, *What changes*: the trade-routes bullet becomes route and folder names, amounts and the lost-ship
    rule, and the flags.
  - *What is kept*: IDs, keys, owners, GUIDs, counts and order.
  - *What is refused*: malformed types 4 and 5 at any level, and a written frame over 1 MiB.
  - *Fixtures*: the third fixture.
  - *Privacy* (line 112) stays true. *Anonymising* states the residual risk of repeated frames: amounts are drawn
    afresh per frame, so the copies of a repeated island or trade-route frame let a small original value be inferred.

## Documentation updates

- Dev: `docs/areas/pipe.md`, `docs/areas/pipe-recording.md` as *Areas* says.
- Dev: `docs/protocol.md`.
  - *Sources*: the second version 3 recording, as derived facts only (facts 7 to 11, counts, no names or values beyond
    those ranges).
  - *TradeRoutes and ItemOffers*: the layout as confirmed, the observations of facts 8 to 10, including that some
    route names contain `_` and none a non-ASCII byte.
- Dev: `docs/USER_GUIDE.md`, English *Anonymising a recording* (line 327: routes and folders become `Route <n>` and
  `Folder <n>`, their amounts change, the structure stays; line 329: the refusal reasons) and the German text of the
  same section (lines about 805 and 807) alike.
- Tester: `tests/Censio.Pipe.Tests/Fixtures/README.md`: the new fixture's row and facts (counts from the Tester's
  replay), the existing `protocol-3-session.jsonl` facts reworded ("5 trade-route and 4 item-offer frames without
  routes or offers"), and the deny-list section with routes, folders and the original check.
- Lead (done in this step): records 0094 and 0095 and the index.
- No change: `docs/ARCHITECTURE.md`, `docs/areas/README.md`, `docs/areas/pipe-connection.md` (line 42 stays true),
  `README.md`, `.squad/project.md` (its integration surface already names these files).

## Architecture check

The pipe format stays confined to `Censio.Pipe` (record 0018); no contract, server or Companion change. No guarantee
of `.squad/project.md` is touched; the tick buffer and history are unaffected because the new messages produce no
item.

## Security considerations

- [x] Limits: the caps and the bytes-left check apply to the counts on the wire, before decoding; the 1 MiB check of
  the anonymiser applies to the written bytes. A name of 255 invalid bytes decodes to 255 U+FFFD (510 bytes of
  UTF-16), so decoding is at most about twice the frame's bytes in characters.
- [x] Exceptions: none new; the parser returns typed results, and the anonymiser's refusal is the existing `frame
  cannot be anonymised in line <n>`.
- Names never reach a log or an error: `PipeError` has no text field, and `FixtureDenyList` findings name line and
  field only.

## Decision records

- `docs/decisions/0095-trade-routes-and-item-offers-decoded-strictly-and-anonymised-with-invented-names.md` (Proposed)
- `docs/decisions/0094-pipe-reader-accepts-protocol-versions-2-and-3.md`: unreleased, edited in place. It keeps the
  version decision; its type 4/5 parts move to 0095, and it is renamed from
  `0094-pipe-reader-accepts-protocol-versions-2-and-3-and-skips-trade-routes-and-item-offers.md`; the index row follows.

## Out of scope / follow-ups

- Follow-up feature issue (orchestrator): "Show trade routes and item offers of pipe protocol 3". Body: #221 decodes
  types 4 and 5 in `Censio.Pipe` only. Decide how they are shown, for example routes with problems or lost ships on a
  server page, which needs a contract message and the server's live state. Spec it against the decoded fixture of
  #221.

## Challenge

- 1 (major, `PipeConnectionTests` breaks or tests only the malformed path): accepted. I confirmed the call site at
  `PipeConnectionTests.cs:2041-2044`, used by `:457-473`. Fact 12 is corrected. *Test files*, the AC7 row and tasks
  row 3 now include `PipeConnectionTests.cs`, whose `UndecodedFrame` the Tester rebuilds in the reference layout.
- 2 (minor, the `int.MaxValue` seam shows only the cap): accepted. The seam is now the error offset of a count at most
  its cap that the bytes left cannot hold, and of a count above its cap in a frame that holds that many elements
  (Security note 1).
- 3 (minor, draw order): accepted. *Approach* G2 now fixes the order strictly by frame position, with two draws per
  value (a zero included) and none only for a negative goods amount.
- 4 (minor, repeated frames reveal small values): accepted as a documented residual risk, not prevented. Island frames
  already re-draw per tick under record 0042, whose goal is no exact value in public, not the secrecy of an amount.
  The values concerned are ship counts and load amounts. A per-run mapping keyed by route, station and good position
  would need state across frames and keys that shift when the player adds a route. The risk goes into
  `docs/areas/pipe-recording.md` (*Anonymising*) and record 0095's *Consequences*.
- 5 (minor, stale 0094 link in the spec): accepted. `spec.md` now links the renamed record.

## Security review

Security approved with no blocking finding. Its two notes were decided as follows:

- Note 1 (the count-limit tests do not show the bounds): fixed as recommended. *Approach* G1 names the error offset as
  the seam, and AC5's tests assert `PipeError.Offset`.
- Note 2 (the lost-ship value is "at least 0" only for a positive ship count): fixed as recommended. *Approach* G2 now
  says the value is never the original, and at least 0 when the original ship count is positive.
