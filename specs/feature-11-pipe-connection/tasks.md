# Tasks: Pipe connection: discovery, reconnect and single-reader handling

Plan: [plan.md](plan.md) — Status: Draft

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `PipeEndpoint` (with `PipePath`, `IsListed`), `PipeRetryDelay`, `PipeConnectResultKind`, `PipeConnectResult`, `FormatToolTip` | `src/Censio.Companion.Core/PipeEndpoint.cs`, `PipeRetryDelay.cs`, `PipeConnectResultKind.cs`, `PipeConnectResult.cs`, `TrayMenu.cs` | AC1, AC2, AC6, AC10, AC13, AC15 | — | Dev | [x] |
| 2 | G1 | Tests first: endpoint constants, delays, classification, tooltip texts | `tests/Censio.Companion.Core.Tests/PipeEndpointTests.cs`, `PipeRetryDelayTests.cs`, `PipeConnectResultTests.cs`, `TrayMenuTests.cs` | AC1, AC2, AC6, AC10, AC13, AC15 | 1 | Tester | [x] |
| 3 | G1 | Implement until green | the G1 files | AC1, AC2, AC6, AC10, AC13, AC15 | 2 | Dev | [x] |
| 4 | G2 | Skeleton: `PipeConnectionState`, `PipeConnectionStatus`, `IPipeConnector`, `PipeConnection`, `TrayStatusUpdater` | `src/Censio.Companion.Core/PipeConnectionState.cs`, `PipeConnectionStatus.cs`, `IPipeConnector.cs`, `PipeConnection.cs`, `TrayStatusUpdater.cs` | AC1–AC16 | 3 | Dev | [x] |
| 5 | G2 | Test doubles and project file: `ScriptedPipeConnector`, `ControlledPipeStream`, TimeProvider.Testing reference, linked `PipeFrameBuilder` | `tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`, `ControlledPipeStream.cs`, `Censio.Companion.Core.Tests.csproj` | AC1–AC16 | 4 | Tester | [x] |
| 6 | G2 | Tests first: the state machine and the tray hand-off | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs`, `TrayStatusUpdaterTests.cs` | AC1–AC16 | 5 | Tester | [x] |
| 7 | G2 | Implement until green and the *Coverage gate* passes | the G2 files | AC1–AC16 | 6 | Dev | [x] |
| 8 | G3 | Skeleton: `NamedPipeConnector`, `SetToolTip` | `src/Censio.Companion/NamedPipeConnector.cs`, `TrayIcon.cs` | AC11, AC13, AC15, AC17 | 7 | Dev | [x] |
| 9 | G3 | Implement the Windows connector and wire the shell (start, tooltip through `TrayStatusUpdater` and the dispatcher, cancel on exit without waiting); no unit tests (Windows shell) | `src/Censio.Companion/NamedPipeConnector.cs`, `TrayIcon.cs`, `App.xaml.cs` | AC11, AC13, AC15, AC17 | 8 | Dev | [x] |
| 10 | G4 | Area document `pipe.md` and the area index | `docs/areas/pipe.md`, `docs/areas/README.md` | AC18 | 9 | Dev | [x] |
| 11 | G4 | Area document `companion.md` | `docs/areas/companion.md` | AC18 | 9 | Dev | [x] |
| 12 | G4 | `ARCHITECTURE.md`, `SECURITY.md` and `.squad/project.md` *Security areas* | `docs/ARCHITECTURE.md`, `SECURITY.md`, `.squad/project.md` | AC13 | 9 | Dev | [x] |
| 13 | G4 | `README.md` and `docs/USER_GUIDE.md`, English then German | `README.md`, `docs/USER_GUIDE.md` | AC18 | 9 | Dev | [x] |
| 14 | G4 | `.squad/project.md` *Test doubles* | `.squad/project.md` | AC1–AC16 | 7 | Tester | [x] |
| 15 | G5 | Manual test on Windows on the PR head; no new API | — | AC17 | 13 | Product Manager | [ ] |
