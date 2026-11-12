# Tasks: WebSocket ingest endpoint with shared token

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `RefusedId` (`Guid`), `CatalogDocument` on `ContractReadResult`, `Version` (`int`) on `CatalogDocumentError` | `src/Censio.Contracts/ContractReadResult.cs`, `CatalogDocumentError.cs` | AC10 | — | Dev | [ ] |
| 2 | G1 | Tests first: refused `id`, no `id` for malformed, catalog document, catalog error version | `tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `CatalogDocumentReaderTests.cs` | AC10 | 1 | Tester | [ ] |
| 3 | G1 | Reader keeps the built message's `id` and the catalog; catalog error carries its version | `src/Censio.Contracts/ContractReader.cs`, `ContractReadResult.cs`, `CatalogDocumentError.cs`, `CatalogDocumentReader.cs` | AC10 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `static` `partial` `IngestTokenSetup` (`string` constants, `TryCreateAccess`, `GenerateToken`), `sealed` `IngestAccess` (`CreateOpen`, `CreateRefusingAll`, `CreateForToken`, `bool` `IsTokenRequired`, `IsTokenAccepted`) | `src/Censio.Server/IngestTokenSetup.cs`, `IngestAccess.cs` | AC1, AC7, AC8 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: token file, switch, failures, comparison, no token in logs; `Program` exit code | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs`, `IngestAccessTests.cs`, `ProgramTests.cs` | AC1, AC3, AC4, AC7, AC8 | 4 | Tester | [ ] |
| 6 | G2 | Token setup and access; `Program.cs` calls the setup after the key ring | `src/Censio.Server/IngestTokenSetup.cs`, `IngestAccess.cs`, `Program.cs` | AC1, AC3, AC4, AC7, AC8 | 5 | Dev | [ ] |
| 7 | G3 | Skeleton: `IngestConnections` (`TryEnterPendingHello`, `LeavePendingHello`, `TryTakeCompanion`, `ReleaseCompanion`), `IngestSession` (`TimeSpan` `HelloTimeout`, `RunAsync`), `IngestEndpoint` (`CreateAcceptContext`, `AddCensioIngest`, `MapCensioIngest`); test double `InMemoryDuplexStream` (`long` `BytesRead`) | `src/Censio.Server/IngestConnections.cs`, `IngestSession.cs`, `IngestEndpoint.cs` | AC2, AC5, AC6, AC9, AC10, AC11 | 6 | Dev | [ ] |
| 8 | G3 | Test double `InMemoryDuplexStream` and tests first for session, connections and endpoint | `tests/Censio.Server.Tests/InMemoryDuplexStream.cs`, `IngestSessionTests.cs`, `IngestConnectionsTests.cs`, `IngestEndpointTests.cs` | AC2, AC3, AC4, AC5, AC6, AC9, AC10, AC11 | 7 | Tester | [ ] |
| 9 | G3 | Session, connections, endpoint; `Program.cs` registers and maps them with `UseWebSockets` | `src/Censio.Server/IngestConnections.cs`, `IngestSession.cs`, `IngestEndpoint.cs`, `Program.cs` | AC2, AC3, AC5, AC6, AC9, AC10, AC11 | 8 | Dev | [ ] |
| 10 | G4 | No new API. Scratch measurements of plan facts 9 (keep-alive, three cases) and 15 (catalog-upload peak), results for `ingest.md` | scratch worktree only | AC6 | 9 | Dev | [ ] |
| 11 | G4 | Documentation: `ingest.md` and index row, `server.md`, `contract.md`, `README.md`, `docs/USER_GUIDE.md`, `docs/ARCHITECTURE.md`, `SECURITY.md`; exact text for `.squad/project.md` | `docs/areas/ingest.md`, `docs/areas/README.md`, `docs/areas/server.md`, `docs/areas/contract.md`, `README.md`, `docs/USER_GUIDE.md`, `docs/ARCHITECTURE.md`, `SECURITY.md` | AC12, AC6 | 10 | Dev | [ ] |
| 12 | G4 | `.squad/project.md` entries from the Dev's and the Tester's reported text (*Test doubles*: `InMemoryDuplexStream`) | `.squad/project.md` | AC12 | 11 | Orchestrator | [ ] |
