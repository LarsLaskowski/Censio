# Tasks: Send recordings to a server for testing

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `CompanionCommandLine` with `ServerAddress`, `TokenFilePath`; `ReplayTokenFile` with `MaxFileBytes`, `ReadFile`; `ReplayTokenFileResult` with `Kind`, `Token`; `ReplayTokenFileResultKind` | `src/Censio.Companion.Core/CompanionCommandLine.cs`, `ReplayTokenFile.cs`, `ReplayTokenFileResult.cs`, `ReplayTokenFileResultKind.cs` | AC4, AC5 | — | Dev | [x] |
| 2 | G1 | Tests first: command line and token file | `tests/Censio.Companion.Core.Tests/CompanionCommandLineTests.cs`, `ReplayTokenFileTests.cs` | AC4, AC5 | 1 | Tester | [x] |
| 3 | G1 | Implement parsing and the token file reader; coverage | the files of 1 | AC4, AC5 | 2 | Dev | [x] |
| 4 | G2 | Skeleton: `IReplayServerConnector` with `ConnectAsync`; `ClientWebSocketConnector` with `ConnectTimeout`; `ReplaySender` with `MaxReplyBytes`, `ReplyTimeout`, `ProfileId`, `ProfileName`, `Counts`, `Prepare`, `ConnectServerAsync`, `NotifyState`, `SendTickAsync`, `SendSessionStartAsync`, `SendSessionEndAsync`, `CloseAsync`, `DisposeAsync`; `ReplaySendProblemKind`; `ReplaySendCounts` with `Ticks`, `Refused`; `CompanionLog` `ReplaySendConnected`, `ReplaySendRefused`, `ReplaySendFailed`, `ReplaySendFinished`; test project framework reference and links | `src/Censio.Companion.Core/*`, `tests/Censio.Companion.Core.Tests/Censio.Companion.Core.Tests.csproj` | AC3, AC7, AC8, AC9, AC11, AC13 | 3 | Dev | [x] |
| 5 | G2 | Tests first: sender and connector, with `ScriptedIngestServer`, `DuplexReplayConnector`, `LoopbackWebSocketHost`; verify plan facts 10 and 11 | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs`, `ClientWebSocketConnectorTests.cs` and the doubles | AC3, AC7, AC8, AC9, AC11, AC13 | 4 | Tester | [x] |
| 6 | G2 | Implement connector and sender; coverage | the files of 4 | AC3, AC7, AC8, AC9, AC11, AC13 | 5 | Dev | [x] |
| 7 | G3 | Skeleton: `PipeReplayRunner` constructor; `PipeReplayStatus` `IsConnecting`, `SendProblem`, `Sent`; `TrayMenu` `FormatReplaySendReason` | `src/Censio.Companion.Core/PipeReplayRunner.cs`, `PipeReplayStatus.cs`, `TrayMenu.cs`, `TraySnapshot.cs` | AC1, AC2, AC5, AC6, AC7, AC9, AC10 | 6 | Dev | [x] |
| 8 | G3 | Tests first: runner flow, tray texts, end-to-end into the server with `TestServerReplayConnector` | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs`, `TrayMenuTests.cs`, `tests/Censio.Server.Tests/IngestEndpointTests.cs`, `TestServerReplayConnector.cs` | AC1, AC2, AC3, AC5, AC6, AC7, AC9, AC10 | 7 | Tester | [x] |
| 9 | G3 | Implement runner flow, tray texts and looks, shell wiring; coverage | the files of 7, `src/Censio.Companion/App.xaml.cs` | AC1, AC2, AC3, AC5, AC6, AC7, AC9, AC10 | 8 | Dev | [ ] |
| 10 | G3 | Keep-alive measurement on the published server, written into the area document | `docs/areas/replay-sending.md`, *Measurements* | AC13 | 9 | Dev | [ ] |
| 11 | G3 | Area documents, index, architecture, SECURITY, README and user guide (English and German), report the `.squad/project.md` text | the files of plan *Documentation updates* | AC12 | 10 | Dev | [ ] |
| 12 | G3 | Fixture README and the *Test doubles* lines for `.squad/project.md` | `tests/Censio.Pipe.Tests/Fixtures/README.md` | AC1 | 8 | Tester | [x] |
| 13 | G3 | Write the reported `.squad/project.md` text | `.squad/project.md` | AC12 | 11, 12 | Orchestrator | [ ] |
| 14 | PM | Manual run of the documented steps on Windows at `--speed 10`, Docker and from source; confirm plan facts 12 and 13 | — | AC12 | 11 | Product Manager | [ ] |
