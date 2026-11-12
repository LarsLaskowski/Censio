# Tasks: JSON contract v1 between Companion and server

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton (sealed classes unless abstract or static): ContractMessage, ContractMessageType, ContractHello, ContractHelloAccepted, ContractHelloRefused, ContractHelloRefusal, ContractCatalogUpload with ComputeIdentity, ContractIconImage, ContractTick, ContractTickIsland, ContractTickEntry, ContractSessionStart, ContractSessionEnd, ContractStatus, ContractPipeState, ContractAcknowledgement with GetRefusalText, ContractAcknowledgementRefusal, ContractErrorKind, ContractError, ContractReadResult, ContractWriteResult, ContractReader with Read, ContractWriter with Write | `src/Censio.Contracts/Contract*.cs` | AC1, AC2, AC4–AC8 | — | Dev | [ ] |
| 2 | G1 | Tests first: reader and writer | `tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `ContractWriterTests.cs` | AC1, AC2, AC4–AC9 | 1 | Tester | [ ] |
| 3 | G1 | Implement the syntax pass, version step, frozen refusal, strict deserialization, value rules, catalog check, writer and refusal texts | `src/Censio.Contracts/Contract*.cs` | AC1, AC2, AC4–AC8 | 2 | Dev | [ ] |
| 4 | G1 | Measure managed memory and peak working set of `ContractReader.Read` on the three cases of fact 8 (catalog upload at the bound, minimal-entry tick at the bound, 524,288-pair tick), with commands and output | scratch only; result for `contract.md` | AC7 | 3 | Dev | [ ] |
| 5 | G2 | Skeleton: ContractMessageConverter with ToTick, ToSessionStart, ToSessionEnd, ToStatus | `src/Censio.Companion.Core/ContractMessageConverter.cs` | AC1, AC2, AC3, AC9 | 3 | Dev | [ ] |
| 6 | G2 | Tests first: converter | `tests/Censio.Companion.Core.Tests/ContractMessageConverterTests.cs` | AC1, AC2, AC3, AC9 | 5 | Tester | [ ] |
| 7 | G2 | Implement the converter | `src/Censio.Companion.Core/ContractMessageConverter.cs` | AC1, AC2, AC3, AC9 | 6 | Dev | [ ] |
| 8 | G2 | Documentation: `contract.md`, areas index row, `game-catalog-format.md` *Not here*, `ARCHITECTURE.md` (project table, *Data flow*), the `.squad/project.md` text reported to the orchestrator | `docs/areas/contract.md`, `docs/areas/README.md`, `docs/areas/game-catalog-format.md`, `docs/ARCHITECTURE.md` | — | 4, 7 | Dev | [ ] |
