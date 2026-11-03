# Tasks: Diagnostics window for the last tick

Plan: [plan.md](plan.md) — Status: Draft

Each task names its criteria, the tasks it waits for and its group (steps 4–6 run per group, one agent per member
and mode; rows consecutive). A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row, owned by the Dev (test documentation: the Tester), in the last group.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `LatestTick` on connection and replay, `OpenDiagnostics` entry and text, `DiagnosticsView`, `DiagnosticsRow`, `IDiagnosticsWindow`, `DiagnosticsWindowSession` as in *Signatures* (the shell's `DiagnosticsWindow` is built in task 4, not here) | `src/Censio.Companion.Core/PipeConnection.cs`, `PipeReplayRunner.cs`, `TrayMenu.cs`, `TrayMenuEntryKind.cs`, `DiagnosticsView.cs`, `DiagnosticsRow.cs`, `IDiagnosticsWindow.cs`, `DiagnosticsWindowSession.cs` | AC1, AC2, AC3, AC4, AC5, AC7, AC8, AC9, AC10, AC12 | — | Dev | [ ] |
| 2 | G1 | Tests first: the tests of *Tests per criterion*, the fake window and the frame helper | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs`, `FakeDiagnosticsWindow.cs`, `PipeConnectionTests.cs`, `PipeReplayRunnerTests.cs`, `PipeTestFrames.cs` | AC1, AC2, AC3, AC4, AC5, AC7, AC8, AC9, AC10, AC12 | 1 | Tester | [ ] |
| 3 | G1 | Implement the Core types until the tests pass and the *Coverage gate* passes; report fact 11 (WPF `ListCollectionView` / `ItemContainerGenerator` source read) | the Core files of task 1 | AC1, AC2, AC3, AC4, AC5, AC7, AC8, AC9, AC10, AC12 | 2 | Dev | [ ] |
| 4 | G2 | Shell: `DiagnosticsWindow` (WPF), session created in `App.OnStartup` for every mode, `Notify` wiring (status, tick, recording, midnight), `Dispatch` and `TrayIcon` click — no new API | `src/Censio.Companion/DiagnosticsWindow.xaml`, `DiagnosticsWindow.xaml.cs`, `App.xaml.cs`, `TrayIcon.cs` | AC1, AC5, AC6, AC8, AC10 | 3 | Dev | [ ] |
| 5 | G3 | Area document `companion.md` (scope, menu, *Diagnostics window*, replay, not verified yet, record 0038) — no new API | `docs/areas/companion.md` | AC5, AC6, AC8, AC10 | 4 | Dev | [ ] |
| 6 | G3 | Area document `pipe.md` *Order and end* and the areas index row | `docs/areas/pipe.md`, `docs/areas/README.md` | AC5, AC12 | 4 | Dev | [ ] |
| 7 | G3 | `ARCHITECTURE.md` project table and *Data flow* | `docs/ARCHITECTURE.md` | AC1 | 4 | Dev | [ ] |
| 8 | G3 | README, EN and DE | `README.md` | AC11 | 4 | Dev | [ ] |
| 9 | G3 | User guide, EN and DE, including the screenshot note in *Reporting problems* / *Probleme melden* | `docs/USER_GUIDE.md` | AC11 | 4 | Dev | [ ] |
| 10 | G3 | `.squad/project.md` *Security areas*: the display sentence | `.squad/project.md` | AC9 | 4 | Dev | [ ] |
| 11 | G3 | `.squad/project.md` *Test doubles*: `FakeDiagnosticsWindow` | `.squad/project.md` | AC4, AC8 | 2 | Tester | [ ] |
