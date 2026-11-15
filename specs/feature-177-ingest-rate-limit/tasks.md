# Tasks: Limit ingest connections per host

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: IngestHosts (MaxPendingHellosPerHost, MaxFailedHellos, MaxTrackedHosts, FailureWindow, BlockDuration, constructor, TryEnterPendingHello, LeavePendingHello, RecordFailedHello, RecordAcceptedHello, Dispose) and IngestHost as in *Signatures* | src/Censio.Server/IngestHosts.cs, src/Censio.Server/IngestHost.cs | AC1, AC3, AC5, AC6, AC7 | — | Dev | [x] |
| 2 | G1 | Tests first: the `IngestHostsTests` of *Tests per criterion* | tests/Censio.Server.Tests/IngestHostsTests.cs | AC1, AC3, AC4, AC5, AC6, AC7 | 1 | Tester | [x] |
| 3 | G1 | Implement the hosts, the sliding window, the block with its timer and lines, the table with shared and protected entry | src/Censio.Server/IngestHosts.cs, src/Censio.Server/IngestHost.cs | AC1, AC3, AC4, AC5, AC6, AC7 | 2 | Dev | [x] |
| 4 | G2 | Skeleton: IngestAdmission (constructor, LeavePendingHello, TryTakeCompanion, ReleaseCompanion, RecordFailedHello, RecordAcceptedHello, RecordUncountedHello); the changed IngestSession constructor and SendTimeout; adapt `IngestSessionTests.StartSession` mechanically | src/Censio.Server/IngestAdmission.cs, src/Censio.Server/IngestSession.cs, tests/Censio.Server.Tests/IngestSessionTests.cs | AC1, AC4, AC8 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: the endpoint and session tests of *Tests per criterion*; rewrite `IngestEndpointSeventeenthPendingConnectionReturns503`, adapt `IngestEndpointAcceptFailureLeavesPendingPlace` | tests/Censio.Server.Tests/IngestEndpointTests.cs, tests/Censio.Server.Tests/IngestSessionTests.cs | AC1, AC2, AC3, AC4, AC8, AC9 | 4 | Tester | [ ] |
| 6 | G2 | Implement the endpoint's `429`, the admission, the registration and the forwarded-headers post-configuration; the session's outcome reports and send bound | src/Censio.Server/IngestEndpoint.cs, src/Censio.Server/IngestAdmission.cs, src/Censio.Server/IngestSession.cs | AC1, AC2, AC3, AC4, AC8, AC9 | 5 | Dev | [ ] |
| 7 | G2 | Mutation check: AC9's tests fail with the `PostConfigure` removed, then restored | — | AC9 | 6 | Tester | [ ] |
| 8 | G2 | Measurements *noread-send*, *noread-then-idle*, *idle* (optionally *ping-flood*) and *client-address* | — (results into docs/areas/ingest.md, row 9) | AC1, AC8 | 6 | Dev | [ ] |
| 9 | G2 | Documentation updates of the plan; report the `.squad/project.md` text | docs/areas/ingest.md, docs/areas/server.md, docs/ARCHITECTURE.md, SECURITY.md, docs/USER_GUIDE.md | AC1, AC2, AC3, AC5, AC6, AC7, AC8, AC9 | 7, 8 | Dev | [ ] |
| 10 | G2 | Write in the `.squad/project.md` type list the Dev reports | .squad/project.md | AC1 | 9 | Orchestrator | [ ] |
