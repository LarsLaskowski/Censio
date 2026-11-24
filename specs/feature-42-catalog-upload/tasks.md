# Tasks: Catalog upload on connect and after re-extraction

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | PM | Answer whether he can build the image and the Companion from the branch head on his PC (plan fact 14; the orchestrator asks) | — | AC10 | — | Product Manager | [ ] |
| 2 | G1 | Skeleton: `LiveCatalog` (`Create`, `Identity`, `WriteUpload`, `WriteIcon`), `LiveCatalogMessage` (`Id`, `Utf8Json`), `MaxCatalogAttempts`, `SetCatalog`, `MaxCatalogReplyBytes`, `ServerCatalogIdentity`, `SendCatalogUploadAsync`, `Build`, `ToIconReference`, `CatalogSentToServer`, `CatalogAlreadyOnServer`, `CatalogRefusedByServer`, `CatalogUploadNotWritten`, `CatalogUploadAbandoned` | `src/Censio.Companion.Core/LiveCatalog.cs`, `src/Censio.Companion.Core/LiveCatalogMessage.cs`, `src/Censio.Companion.Core/LiveSender.cs`, `src/Censio.Companion.Core/IngestClient.cs`, `src/Censio.Companion.Core/GameCatalogLoader.cs`, `src/Censio.Companion.Core/GameCatalogDocumentConverter.cs`, `src/Censio.Companion.Core/CompanionLog.cs` | AC1–AC9, AC12, AC13 | — | Dev | [ ] |
| 3 | G1 | Tests first: the tests of *Tests per criterion*, the `ScriptedIngestServer` additions | `tests/Censio.Companion.Core.Tests/LiveSenderCatalogTests.cs`, `tests/Censio.Companion.Core.Tests/LiveCatalogTests.cs`, `tests/Censio.Companion.Core.Tests/IngestClientTests.cs`, `tests/Censio.Companion.Core.Tests/GameCatalogLoaderTests.cs`, `tests/Censio.Companion.Core.Tests/ScriptedIngestServer.cs` | AC1–AC9, AC12, AC13 | 2 | Tester | [ ] |
| 4 | G1 | Implement `LiveCatalog`, the reply bound and hello identity, the send loop's catalog phase with deferral and cap, `Build`, `ToIconReference`, the log lines, the App wiring | `src/Censio.Companion.Core/*.cs` of row 2, `src/Censio.Companion/App.xaml.cs` | AC1–AC9, AC12, AC13 | 3 | Dev | [ ] |
| 5 | G1 | Coverage pass | `tests/Censio.Companion.Core.Tests/` | AC1–AC9, AC12, AC13 | 4 | Tester | [ ] |
| 6 | G1 | Documentation: area documents, `docs/areas/README.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md` (English and German); report the `.squad/project.md` text | `docs/areas/live-sending.md`, `docs/areas/companion-log.md`, `docs/areas/local-mode.md`, `docs/areas/game-catalog-format.md`, `docs/areas/game-catalog-cache.md`, `docs/areas/contract.md`, `docs/areas/ingest.md`, `docs/areas/replay-sending.md`, `docs/areas/README.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md` | AC9, AC11 | 5 | Dev | [ ] |
| 7 | G1 | Write the reported `.squad/project.md` text | `.squad/project.md` | — | 6 | Orchestrator | [ ] |
| 8 | G1 | Manual check on local builds of the stamped head | — | AC10 | 7 | Product Manager | [ ] |
