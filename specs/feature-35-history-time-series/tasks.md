# Tasks: History time series storage and retention

Plan: [plan.md](plan.md)

Each task names its criteria, the tasks it waits for and its group (steps 4–6 run per group, one agent per member
and mode; rows consecutive). *Files* names repository-relative paths, a directory or a glob where a row writes
several files. A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row, owned by the Dev (test documentation: the Tester; an edit under `.squad/`: the
Orchestrator, from the text its member reports), in the last group. Tasks of the Product Manager and the Lead alone
(a probe, a survey run, a fact correction) go to group `PM`, which steps 4–6 skip.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `HistoryStore` (constructor, `FullResolution`, `FoldNextTickAsync`, `SweepNextAsync`, `ClearDeletedAsync`, `ReadSeriesAsync`), `HistoryOutcome`, `HistoryResult`, `HistorySeriesQuery`, `HistoryPoint`, `HistoryAmount`, `ServerStore.DeleteProfileAsync`; the row classes, model and generated migration `AddHistory` | src/Censio.Server.Data/ | AC1, AC2, AC3, AC4, AC6, AC8, AC11, AC12 | — | Dev | [ ] |
| 2 | G1 | Tests first for the store: fold, series, identities, deletion, sweep, clean-up, gate order, repeated goods | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs, tests/Censio.Server.Data.Tests/ServerStoreTests.cs | AC1, AC2, AC3, AC4, AC6, AC8, AC11, AC12 | 1 | Tester | [x] |
| 3 | G1 | Implement: `StoreAccess` moved out of `ServerStore`, secure delete in `ServerDatabaseOpener`, already stored with `FoldedTicks`, `HistoryFolder` and the fold step, the series read, the deletion with its tombstone, the sweep and the checkpoint | src/Censio.Server.Data/ | AC1, AC2, AC3, AC4, AC6, AC8, AC11, AC12 | 2 | Dev | [x] |
| 4 | G2 | Skeleton: `ServerState.DeleteProfileAsync`; `HistoryRetention` (constructor, `RunInterval`, `Start`, `RunAsync`, `DeleteProfileAsync`, `Dispose`) | src/Censio.Server/ServerState.cs, src/Censio.Server/HistoryRetention.cs | AC5, AC7, AC8, AC10 | 3 | Dev | [ ] |
| 5 | G2 | Tests first for the state and the job | tests/Censio.Server.Tests/ServerStateTests.cs, tests/Censio.Server.Tests/HistoryRetentionTests.cs | AC5, AC6, AC7, AC8, AC10 | 4 | Tester | [ ] |
| 6 | G2 | Implement the deletion in the state, the job and its log lines, its stop, and its wiring in `Program.cs` (late-bound registration, disposed before the database) | src/Censio.Server/ServerState.cs, src/Censio.Server/HistoryRetention.cs, src/Censio.Server/Program.cs | AC5, AC6, AC7, AC8, AC10 | 5 | Dev | [ ] |
| 7 | G3 | Skeleton: `DeletionHostRule.IsTrusted`, `Routes.DeletionAllowed`, `App.Configuration`, `Regions.DeletionAllowed` and `Regions.Retention`, the seven `UiTexts` properties (`DeleteHistory`, `DeleteHistoryTitle`, `DeleteHistoryTextFormat`, `DeleteHistoryConfirm`, `DeleteHistoryCancel`, `DeleteHistoryFailed`, `DeleteHistoryHostRefused`) | src/Censio.Server/DeletionHostRule.cs, src/Censio.Server/Components/, src/Censio.Server/UiTexts.cs | AC7, AC9 | 6 | Dev | [ ] |
| 8 | G3 | Tests first for the page, the host rule, the app, the texts and the framing header; adapt `PageTestHost` | tests/Censio.Server.Tests/RegionsTests.cs, tests/Censio.Server.Tests/DeletionHostRuleTests.cs, tests/Censio.Server.Tests/AppTests.cs, tests/Censio.Server.Tests/UiTextsTests.cs, tests/Censio.Server.Tests/ProgramTests.cs, tests/Censio.Server.Tests/PageTestHost.cs | AC7, AC9 | 7 | Tester | [ ] |
| 9 | G3 | Implement the host rule, the root parameter and its cascade, the button, the dialog, the texts and the dialog provider | src/Censio.Server/DeletionHostRule.cs, src/Censio.Server/Components/, src/Censio.Server/UiTexts.cs | AC7, AC9 | 8 | Dev | [ ] |
| 10 | G4 | Measurement of plan *Approach*: fold step, sweep step, the deletion's first transaction (7 days; a year of hourly points plus weeks of unfolded ticks), a store during a step at the bounds, the bytes of a folded hour and of `FoldedTicks` per tick, the series read; no new API | — (scratch program outside the repository) | AC11 | 9 | Dev | [ ] |
| 11 | G4 | Area documents: new `history.md` with the measurements, `server-data.md`, `server-state.md`, `web-pages.md`, `web-ui.md`, `server.md`, `replay-sending.md`, `ingest.md`, `pipe.md`, the index | docs/areas/ | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC11, AC12 | 10 | Dev | [ ] |
| 12 | G4 | `docs/ARCHITECTURE.md`, `README.md` and `docs/USER_GUIDE.md` (English and German), `SECURITY.md` | docs/ARCHITECTURE.md, README.md, docs/USER_GUIDE.md, SECURITY.md | AC9 | 11 | Dev | [ ] |
| 13 | G4 | Exact text for `.squad/project.md` (*Security areas*, *Guarantees*, *Integration surface*) reported to the orchestrator | .squad/project.md | AC9 | 11 | Dev | [ ] |
