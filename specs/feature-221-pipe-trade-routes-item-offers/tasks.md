# Tasks: Decode trade routes and item offers of pipe protocol 3

Plan: [plan.md](plan.md)

Each task names its criteria, the tasks it waits for and its group (steps 4–6 run per group, one agent per member
and mode; rows consecutive). *Files* names repository-relative paths, a directory or a glob where a row writes
several files. A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row, owned by the Dev (test documentation: the Tester; an edit under `.squad/`: the
Orchestrator, from the text its member reports), in the last group. Tasks of the Product Manager and the Lead alone
(a probe, a survey run, a fact correction) go to group `PM`, which steps 4–6 skip.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | PM | AC12 check of the supplied recording (counts only), records 0094 and 0095, index | `docs/decisions/0094-pipe-reader-accepts-protocol-versions-2-and-3.md`, `docs/decisions/0095-trade-routes-and-item-offers-decoded-strictly-and-anonymised-with-invented-names.md`, `docs/decisions/README.md` | AC12 | — | Lead | [x] |
| 2 | G1 | Skeleton: the six message and data types (`sealed` classes of `required` properties, the two `MessageType` overrides returning `PipeMessageType`), the five `int` caps of `PipeProtocol`, `UndecodedMessage` deleted | `src/Censio.Pipe/TradeRoutesMessage.cs`, `src/Censio.Pipe/TradeRoute.cs`, `src/Censio.Pipe/TradeRouteStation.cs`, `src/Censio.Pipe/TradeRouteGood.cs`, `src/Censio.Pipe/ItemOffersMessage.cs`, `src/Censio.Pipe/ItemOffer.cs`, `src/Censio.Pipe/PipeProtocol.cs`, `src/Censio.Pipe/UndecodedMessage.cs` | AC1, AC2, AC5 | 1 | Dev | [ ] |
| 3 | G1 | Builders of the reference layout replacing `UndecodedEntries`; failing reader and tick tests | `tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, `tests/Censio.Pipe.Tests/PipeMessageParserTests.cs`, `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`, `tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | 2 | Tester | [ ] |
| 4 | G1 | Decode types 4 and 5 with counts, caps and short-field checks | `src/Censio.Pipe/PipeMessageParser.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | 3 | Dev | [ ] |
| 5 | G2 | Skeleton: `PipeAnonymousNames` members `AddRoute`, `AddFolder`, `TryGetRoute`, `TryGetFolder` | `src/Censio.Pipe/PipeAnonymousNames.cs` | AC8 | 4 | Dev | [ ] |
| 6 | G2 | Failing anonymiser tests | `tests/Censio.Pipe.Tests/PipeRecordingAnonymiserTests.cs` | AC8, AC9, AC10 | 5 | Tester | [ ] |
| 7 | G2 | Invented names, written frames, amounts and lost-ship rule, read-back, 1 MiB refusal | `src/Censio.Pipe/PipeAnonymousNames.cs`, `src/Censio.Pipe/PipeFrameAnonymiser.cs` | AC8, AC9, AC10 | 6 | Dev | [ ] |
| 8 | G3 | No new API. Deny-list checks for routes, folders, amounts and the original's type 4/5 frames; tests | `tests/Censio.Pipe.Tests/FixtureDenyList.cs`, `tests/Censio.Pipe.Tests/FixtureDenyListTests.cs` | AC8, AC12 | 7 | Tester | [ ] |
| 9 | G3 | Make `protocol-3-trade-routes.jsonl` from the supplied recording; `DataRow`s in both fixture tests; manual runs of AC11 and AC12 (counts only); fixture README | `tests/Censio.Pipe.Tests/Fixtures/protocol-3-trade-routes.jsonl`, `tests/Censio.Pipe.Tests/Fixtures/README.md`, `tests/Censio.Pipe.Tests/PipeMessageReaderTests.cs`, `tests/Censio.Pipe.Tests/PipeTickAssemblerTests.cs` | AC11, AC12 | 8 | Tester | [ ] |
| 10 | G3 | Area documents, protocol document and user guide (English and German) | `docs/areas/pipe.md`, `docs/areas/pipe-recording.md`, `docs/protocol.md`, `docs/USER_GUIDE.md` | AC1, AC9, AC11 | 9 | Dev | [ ] |
