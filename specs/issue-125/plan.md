# Plan: Accept pipe protocol version 3 after the game update

Source: Issue #125
Tier: standard — the version gate and the message types of the pipe reader change behavior (input handling inside a parser), and no security control is created, changed or removed.
Security areas touched: Parsing of external input (`src/Censio.Pipe`: `PipeMessageReader`, `PipeMessageParser`, `PipeProtocol`, `PipeFrameAnonymiser`)

## Problem / root cause

Issue #125 asks to check after a game update that the Companion's `Game: build` changes (record 0046). The Product
Manager did so on build `26.39954.4.58957` (main menu `3.0.1.1743890`): the build changed, and the Companion showed
`Protocol mismatch`. Root cause: the game now speaks protocol version 3, and the reader decodes only version 2:
`src/Censio.Pipe/PipeProtocol.cs:13` (`SupportedVersion = 2`), compared in `PipeMessageReader.ApplyVersion`
(`src/Censio.Pipe/PipeMessageReader.cs:56`), which gives the protocol-mismatch error and gates every later frame
(`PipeMessageReader.cs:94`); `PipeStatusRule.ForRead` (`src/Censio.Companion.Core/PipeStatusRule.cs:37`) turns that
error into the tray state `Protocol mismatch`. Version 3 adds message types 4 and 5, which `PipeMessageParser.Parse`
(`src/Censio.Pipe/PipeMessageParser.cs:57`) rejects as unknown. Related defect on the same path: the anonymiser has its
own gate on version 2 (`src/Censio.Pipe/PipeFrameAnonymiser.cs:496`) and refuses every undecodable frame, so no
version 3 recording could be anonymised.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The game sends protocol version 3 since build `26.39954.4.58957` | Product Manager's Companion log: `Connection state ProtocolMismatch, error kind (null), Windows error code (null), received version 3` (step 2 answer) | confirmed |
| 2 | The build changed with the update (`26.29905.4.51797` / `3.0.1733841` before, `26.39954.4.58957` / `3.0.1.1743890` after) — the open point of record 0046 and the issue's expectation | Issue #125 body (value at merge); Product Manager's report (intake row of `log.md`) | confirmed |
| 3 | The reference at commit `4cc8ad78d63b33855b4b35dd002380fc466e4b1d` names `PROTOCOL_VERSION = 3` and types `TradeRoutes` (4) and `ItemOffers` (5) | `src/pipe.h` of that commit, fetched from raw.githubusercontent.com on 2026-11-18 | confirmed |
| 4 | The reference decodes Version, SessionStart, SessionEnd and AreaProductionStatistics field for field as `PipeMessageParser` does | `src/pipe.cpp` of that commit (`ProductionEntryData::read`, the type-3 branch of `RunPipe`) read against `PipeMessageParser.cs:115-140` and `docs/areas/pipe.md`, *Message types* | confirmed |
| 5 | Type 4 and type 5 payloads start with an int32 entry count (routes; traders), then nested data with strings (route name, folder) and further counts | `src/pipe.cpp`, branches `TradeRoutes` / `ItemOffers`, `TradeRouteEntryData::read`, `ItemOfferEntryData::read` | confirmed |
| 6 | The game's version 3 island frames keep that layout, its type 4/5 frames start with that count and stay within 1 MiB | to verify: a real version 3 recording read by the reader and the anonymiser; Product Manager supplies it, Lead checks it after merge (AC7) | unverified |
| 7 | The reference's buffer is still 1 MiB for every frame | `src/pipe.cpp`, `maxMessageSize{ 1024 * 1024 }` | confirmed |
| 8 | A message type the tick builder does not match produces no item, closes no tick and does not restart the quiet period | `src/Censio.Pipe/PipeTickBuilder.cs:142-173` (`AddMessage` switch without default); `QuietDeadline` follows islands only | confirmed |
| 9 | A message result in `Receiving` raises no event and keeps the receiving time | `PipeStatusRule.cs:19-25`; `PipeConnection.EnterReceiving` (`??=`, `PipeConnection.cs:445`) and `SetStatus` (unchanged status raises nothing) | confirmed |
| 10 | Skipped-frame errors are not logged | `CompanionLog.cs` has no per-frame message; `docs/areas/pipe-connection.md` | confirmed |
| 11 | The recorder writes every frame, also under a mismatch, so the released Companion can already record version 3 | `PipeRecorder.cs:112` (any result with a frame); `docs/areas/pipe-recording.md` line 18; `docs/areas/companion.md` line 96 (start allowed in `Protocol mismatch`) | confirmed |
| 12 | Records 0033, 0042 and 0046 are released | `git ls-tree v0.2.0 docs/decisions/` lists all three | confirmed |

## Acceptance criteria

- AC1: A `Version` frame of 2 or 3 is delivered as a version message, and after either the frames of types 1 to 3
  are decoded as before. Any other version gives the protocol-mismatch error with the received version and the
  expected version 3 (`PipeProtocol.NewestSupportedVersion`), and gates later frames as today.
- AC2: A frame of type 4 or 5 is delivered as an `UndecodedMessage` of its type when its payload holds at least an
  int32 count that is not negative, whatever follows; with fewer than 4 payload bytes or a negative count it is a
  malformed-message error; type 6 and above stay unknown types. Before a supported `Version` such a frame gives the
  protocol-mismatch error, as every non-version frame does.
- AC3: An undecoded message changes no tick state: it produces no item, closes no tick, does not restart the quiet
  period and does not change whether a following session end is a repeated one.
- AC4: The tray state follows from `PipeStatusRule.ForRead` on the reader's results: on a connection that sends
  `Version 3` the state becomes `Receiving`, not `Protocol mismatch`, and type 4 and 5 frames raise no further event.
- AC5: The anonymiser treats `Version 3` as `Version 2`; a type 4 or 5 frame is written as its type byte followed by a
  count of 0 (five bytes), none of its bytes copied; a malformed type 4 or 5 frame, or one before a supported
  `Version`, is refused as `frame cannot be anonymised in line <n>`.
- AC6: `docs/areas/game-installation.md`, *Build*, states the observed build change instead of "Not verified".
- AC7: A real version 3 recording replays with `Receiving` and ticks, and the anonymiser accepts it.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeMessageReaderSupportedVersionDecodesFollowingFrames` (DataRow 2, 3: version, session start, island, session end), `PipeMessageReaderUnsupportedVersionReturnsMismatchWithNewestVersion` (DataRow 1, 4, -1); `PipeProtocolIsSupportedReturnsTrueForSupportedVersions` (2, 3), `PipeProtocolIsSupportedReturnsFalseForOtherVersions` (0, 1, 4, `int.MinValue`, `int.MaxValue`) | `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`, `tests/Censio.Pipe.Tests/PipeProtocolTests.cs` |
| AC2 | `PipeMessageParserTradeRoutesOrItemOffersReturnsUndecodedMessage` (DataRow 4, 5; count 0, and count 1 with a reference-layout entry), `PipeMessageParserTradeRoutesOrItemOffersReportsConsumedAfterCount`, `PipeMessageParserTradeRoutesOrItemOffersWithoutCountReturnsMalformed` (0 to 3 payload bytes), `PipeMessageParserTradeRoutesOrItemOffersNegativeCountReturnsMalformed`, existing `PipeMessageParserUnknownTypeReturnsUnknownType` with DataRow 6 instead of 4; `PipeMessageReaderTradeRoutesAfterVersionThreeReturnsUndecodedMessage`, existing `PipeMessageReaderUnknownTypeBeforeVersionReturnsMismatch` kept (type 4) | `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`, `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs` |
| AC3 | `PipeTickBuilderUndecodedMessageKeepsOpenTick`, `PipeTickBuilderUndecodedMessageDoesNotRestartQuietPeriod`, `PipeTickBuilderUndecodedMessageKeepsRepeatedSessionEnd` | `tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs` |
| AC4 | `PipeConnectionVersionThreeRaisesReceiving`, `PipeConnectionTradeRoutesAndItemOffersRaiseNoEvent` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs` |
| AC5 | `PipeRecordingAnonymiserVersionThreeRecordingIsAnonymised`, `PipeRecordingAnonymiserTradeRoutesOrItemOffersAreWrittenEmpty` (DataRow 4, 5; payload with a route name that must not appear in the output), `PipeRecordingAnonymiserMalformedTradeRoutesIsRefused`; `AnonymiserCommandUndecodableFramePrintsLine` with the new row `trade routes before Version` and the row `island after Version 4` (adapted from `Version 3`), the method carrying `AC5` | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs`, `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs` |
| AC6 | check: the Reviewer reads the *Build* section against `log.md` and the issue | — |
| AC7 | after merge: the Product Manager records a session on build `26.39954.4.58957`; the Lead replays it and runs `Censio.Anonymiser` on it (verification issue, step 10) | — |

Synthetic type 4 and 5 frames follow the reference layout of fact 5, built with `PipeFrameBuilder`'s `Byte`,
`Int32Value` and `StringValue`; no real version 3 sample exists (the Product Manager's answer in step 2).

## Approach

- `PipeProtocol`: `SupportedVersion` is replaced by `OldestSupportedVersion` (2), `NewestSupportedVersion` (3) and
  `IsSupported` (true for the versions between them, inclusive).
- `PipeMessageReader.ApplyVersion` uses `IsSupported`; both mismatch errors carry `NewestSupportedVersion` as
  expected version. Nothing else of the gate changes; a switch between 2 and 3 on one connection is no mismatch.
- `PipeMessageType` gains `TradeRoutes = 4` and `ItemOffers = 5`. `PipeMessageParser.Parse` treats types above 5
  as unknown; for 4 and 5 a private `ParseUndecoded` reads the int32 count (a short frame or a negative count is
  malformed, `Offset` at the count) and returns an `UndecodedMessage` of the type; `consumed` is the position behind
  the count. The count allocates nothing and the rest of the frame is not read; the message holds no payload.
- `PipeTickBuilder`, `PipeStatusRule`, `PipeConnection`, `PipeReplayRunner` and the recorder need no change
  (facts 8, 9, 11); the tick builder's `AddMessage` switch gets no case for the new message.
- `PipeFrameAnonymiser`: `TryAccept` uses `IsSupported`; `Anonymise` writes an `UndecodedMessage` as
  `[type, 0, 0, 0, 0]` (a case before the session-end default); `TryCollect` collects no name from it; `IsReadBack`
  accepts it as is (type equal, parser ends at the written length).
- No new user-visible text; the tray simply stops showing `Protocol mismatch` for version 3.

## Options considered

- Accept only version 3: lost, a replay of a version 2 recording (the committed fixture) would show a mismatch.
- Decode trade routes and item offers now: lost, no feature uses them, and their strings and nested counts need
  rules of their own (follow-up issue below).
- Leave types 4 and 5 unknown: lost, every version 3 recording would be refused by the anonymiser.
- Anonymiser drops or copies type 4/5 frames: lost, dropping changes the line count the area document promises,
  copying leaks route names.
- Parse types 4/5 with no check at all (type byte only): lost, the anonymised five-byte frame would not read back as
  "parser ends where the frame ends", and a frame without even its count is evidence of a format change.

Record 0094 holds them.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `PipeProtocol.cs` | two constants and `IsSupported` instead of `SupportedVersion` |
| Censio.Pipe | `PipeMessageType.cs` | `TradeRoutes`, `ItemOffers` |
| Censio.Pipe | `UndecodedMessage.cs` (new) | message of a type 4 or 5 frame |
| Censio.Pipe | `PipeMessageParser.cs` | types 4 and 5 |
| Censio.Pipe | `PipeMessageReader.cs` | gate on `IsSupported` |
| Censio.Pipe | `PipeFrameAnonymiser.cs` | gate on `IsSupported`, empty type 4/5 frames |

## Signatures (for the Dev's skeleton)

- Deleted: `PipeProtocol.SupportedVersion` is removed.
- `public const int OldestSupportedVersion = 2;` (`PipeProtocol`)
- `public const int NewestSupportedVersion = 3;` (`PipeProtocol`)
- `public static bool IsSupported(int version)` (`PipeProtocol`, new `Methods` region)
- `TradeRoutes = 4` and `ItemOffers = 5` (`PipeMessageType`)
- `public sealed class UndecodedMessage : PipeMessage`
- `public UndecodedMessage(PipeMessageType messageType)`
- `public override PipeMessageType MessageType { get; }` (`UndecodedMessage`, region `PipeMessage`)

The skeleton rewrites `PipeProtocol.cs` and `PipeMessageType.cs` whole (both read, final with these members) and adds
`UndecodedMessage.cs`.

## Test files

- `tests/Censio.Pipe.Tests/PipeProtocolTests.cs` (new), `PipeMessageParserTests.cs`, `PipeMessageReaderTests.cs`,
  `PipeTickBuilderTests.cs`, `PipeRecordingAnonymiserTests.cs` in `tests/Censio.Pipe.Tests/`;
  `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs`; `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs`.

Existing test code the change affects, adapted by the Tester in step 5:

- `PipeProtocol.SupportedVersion` in `PipeMessageReaderTests.cs` lines 325, 347, 390 becomes `NewestSupportedVersion`.
- Version 3 used as the unsupported version becomes 4: `PipeMessageReaderTests.cs` lines 55, 316, 336, 359;
  `PipeReplayStreamTests.cs` line 403; `PipeReplayRunnerTests.cs` line 171; `PipeConnectionTests.cs` lines 419, 458,
  482, 500, 931, 1050, 1304, 1732, 1807, 1808 (with their assert messages); `AnonymiserCommandTests.cs` line 96.
- Type 4 used as an unknown type after a version becomes 6: `PipeMessageReaderTests.cs` lines 250 and 261
  (`PipeMessageReaderUnknownTypeThenFrameReturnsNextMessage`), `PipeMessageParserTests.cs` line 273.
- `PipeStatusRuleTests.cs` and `PipeErrorTests.cs` set `ExpectedVersion = 2` as plain data; kept unchanged (no input a
  criterion handles differently).

Files no unit test can cover: none.

## Areas

- `docs/areas/pipe.md`: versions 2 and 3, types 4 and 5, the gate and error table, the risks.
- `docs/areas/pipe-connection.md`: the states and rules that say `Version 2`.
- `docs/areas/pipe-recording.md`, *Anonymising*: types 4 and 5 emptied; refusals by supported version.
- `docs/areas/game-installation.md`, *Build*: the observed change.

## Documentation updates

All by the Dev, once the code is green:

- `docs/areas/pipe.md`: *Scope* "the message types of protocol version 2" → versions 2 and 3, and the reference read
  at commit `4cc8ad7` on 2026-11-18; *Message types* rows 4 (`TradeRoutes`) and 5 (`ItemOffers`): int32 count, the
  rest not decoded, delivered as an undecoded message that changes no state (with the tick rule in *Ticks and
  sessions*: an undecoded message, like a version message, does not close a tick); *Version gate*: every "`2`" /
  "`Version 2`" → a supported version (2 or 3), expected version 3; *Limits and errors*: unknown type "other than 0
  to 5", malformed adds "type 4 or 5 without its count or with a negative one", protocol mismatch "other than 2 or
  3"; *Deliberate differences*, *Wrong version*: "until a supported `Version`"; *Residual risks*: fact 6.
- `docs/areas/README.md`, the pipe row: "protocol version 2" → "protocol versions 2 and 3".
- `docs/areas/pipe-connection.md`: states table rows `Connected`, `Receiving`, `Protocol mismatch`; the bullets
  "a skipped frame … changes nothing" (add: and a trade-route or item-offer frame), "A `Version 2` on that
  connection", and *Retry* "a pipe that sends `Version 2`" → a supported version.
- `docs/areas/pipe-recording.md`, *Anonymising*: *What changes* gains trade routes and item offers (type byte and a
  count of 0, nothing of the content); *What is kept* unchanged but for "frame types"; *What is refused*: "before a
  `Version 2`" → "before a `Version 2` or `3`", and a malformed type 4 or 5 frame.
- `docs/protocol.md`: *Sources* adds the reference commit; *Version*: version 3 confirmed by the Companion log on
  build `26.39954.4.58957` (2026-11-18, not in a recording); a short section for types 4 and 5, *assumed* from the
  reference, not yet seen.
- `docs/USER_GUIDE.md`, English and German: the `Protocol mismatch` row (lines 227, 705: "another protocol version
  than 2 or 3 … continues when version 2 or 3 arrives"); the anonymiser's *What changes* (line 327 and its German
  counterpart: trade routes and item offers are emptied); its error list (lines 329, 807: "before a `Version 2` or
  `3` frame").
- `docs/areas/game-installation.md`, *Build*: "Not verified: that the file version changes with every game update."
  → the observation of fact 2 (the build changed from `26.29905.4.51797` to `26.39954.4.58957` with the main-menu
  version `3.0.1.1743890`, Product Manager, 2026-11-18).
- `docs/ARCHITECTURE.md`, `README.md`, `.squad/project.md`: none (sweep for "version 2", "protocol", "0 to 3" and the
  message type names found no statement there that the change makes untrue).

## Architecture check

The data flow (`docs/ARCHITECTURE.md`, *Data flow*) and the guarantees of `.squad/project.md` are untouched: the
format stays confined to `Censio.Pipe`, the reader stays strict and fails closed (a type 4/5 frame without its count
is rejected, nothing of its content is decoded), the contract to the server does not change.

## Security considerations

- [x] Limits: the 1 MiB frame limit applies to the raw frame before any decoding, as today; the type 4/5 count is
  read but drives no allocation or loop, and no string of those frames is decoded.
- [x] Exceptions reaching a user: none new; malformed type 4/5 frames are typed errors without payload text.

## Decision records

- `docs/decisions/0094-pipe-reader-accepts-protocol-versions-2-and-3-and-skips-trade-routes-and-item-offers.md`
  (Proposed), amending the released records 0033 and 0042. Record 0046 is released and stays unedited; its open
  point is closed in its area document (AC6) and in the Context of 0094.

## Out of scope / follow-ups

- Follow-up feature issue, title "Decode trade routes and item offers of pipe protocol 3", body: decode types 4 and
  5 (reference commit `4cc8ad7`), limits for their nested counts, anonymising route and folder names, and a use in
  the Companion or server; built from a real version 3 recording.
- Verification issue (AC7), title "Verify pipe protocol 3 with a real recording", body: record a session on build
  `26.39954.4.58957` or later (the released Companion already records under `Protocol mismatch`), replay it, run
  `Censio.Anonymiser`, and confirm fact 6.
