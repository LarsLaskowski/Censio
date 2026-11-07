# Tasks: Anonymiser for recordings and test fixtures

Plan: [plan.md](plan.md)

Each task names its criteria, the tasks it waits for and its group (steps 4–6 run per group, one agent per member
and mode; rows consecutive). A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row, owned by the Dev (test documentation: the Tester), in the last group.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `PipeRecordingLineKind`; `PipeRecordingLine` (Kind, LineNumber, ReceiveTime, Frame, Problem); `PipeRecordingReader` (Started, ReadAsync); `PipeRecordingFormat.ReadStarted` | `src/Censio.Pipe/` | AC1, AC5 | — | Dev | [x] |
| 2 | G1 | Tests first: `PipeRecordingReaderTests`, `PipeRecordingFormatTests` (`ReadStarted`); `PipeReplayStreamTests` unchanged | `tests/Censio.Pipe.Tests/` | AC1, AC5 | 1 | Tester | [ ] |
| 3 | G1 | Move the line rules into `PipeRecordingReader`; `PipeReplayStream` reads through it | `src/Censio.Pipe/PipeRecordingReader.cs`, `PipeReplayStream.cs` | AC1, AC5 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `IPipeAnonymiserRandom` (NextDouble), `PipeAnonymiserRandom`; `PipeAnonymiseResultKind`; `PipeAnonymiseResult` (Kind, FrameCount, LineNumber, Problem); `PipeRecordingAnonymiser` (MinShiftDays, MaxShiftDays, AnonymiseAsync); `PipeMessageParser.Parse` overload with `consumed`; internal `PipeAnonymousNames` (AddIsland, AddFigure, Complete, TryGetIsland, TryGetFigure) and `PipeFrameAnonymiser` (TryCollect, Anonymise, Reset) | `src/Censio.Pipe/` | AC1–AC4 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: `PipeRecordingAnonymiserTests`, `PipeAnonymiserRandomTests`, double `ScriptedPipeAnonymiserRandom` | `tests/Censio.Pipe.Tests/` | AC1–AC5 | 4 | Tester | [ ] |
| 6 | G2 | Implement the two passes, names, rewriting and random source | `src/Censio.Pipe/` | AC1–AC5 | 5 | Dev | [ ] |
| 7 | G3 | Skeleton: project `Censio.Anonymiser` with `Program` (Main) and `AnonymiserCommand` (SuccessCode, ErrorCode, UsageCode, RunAsync) and its entry in `Censio.slnx`; confirm verified fact 12 | `src/Censio.Anonymiser/`, `Censio.slnx` | AC4, AC5 | 6 | Dev | [ ] |
| 8 | G3 | Tests first: test project `Censio.Anonymiser.Tests` (its `.slnx` entry by the Dev in row 7), `AnonymiserCommandTests`, `ProgramTests` | `tests/Censio.Anonymiser.Tests/` | AC4, AC5 | 7 | Tester | [ ] |
| 9 | G3 | Implement the command: arguments, input, output checks, temp file and rename, messages, Ctrl+C | `src/Censio.Anonymiser/` | AC4, AC5 | 8 | Dev | [ ] |
| 10 | Pause | No new API. Product Manager runs the anonymiser on the chosen #17 recordings on the branch head and hands in the fixtures with their variants (plan, *Fixtures*); a refusal goes to the Lead | — | AC6 | 9 | Product Manager (orchestrator asks) | [ ] |
| 11 | G4 | No new API. Fixtures into `tests/Censio.Pipe.Tests/Fixtures/`, copied to output; AC6 tests in `PipeMessageReaderTests` and `PipeTickAssemblerTests`; `FixtureDenyList` and `FixtureDenyListTests` (deferred to step 6: they test existing code) | `tests/Censio.Pipe.Tests/` | AC6, AC7 | 10 | Tester | [ ] |
| 12 | G4 | Product Manager runs *Test* with `CENSIO_FIXTURE_DENY_LIST` on the branch head, after remaking the fixtures if an anonymiser type changed since task 10; result (counts only) to the log | — | AC7 | 11 | Product Manager (orchestrator asks) | [ ] |
| 13 | G4 | Fixtures README and `docs/UNIT_TESTS.md` layout block | `tests/Censio.Pipe.Tests/Fixtures/README.md`, `docs/UNIT_TESTS.md` | AC6, AC7 | 11 | Tester | [ ] |
| 14 | G4 | Area documents, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md`, `docs/protocol.md`, `.squad/stack.md`, `.squad/project.md`, `.gitignore` (plan, *Documentation updates*) | as listed | AC8 | 9, 11 | Dev | [ ] |
