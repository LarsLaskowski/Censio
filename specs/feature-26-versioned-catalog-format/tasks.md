# Tasks: Versioned catalog format

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: CatalogDocument (CurrentSchemaVersion, SchemaVersion, GameVersion, Entries), CatalogDocumentEntry (AssetGuid, Kind, Names, RegionGuid, RegionTypes, IconReference), CatalogEntryKind, CatalogDocumentErrorKind, CatalogDocumentError (Kind, Text), CatalogDocumentReadResult (Document, Error), CatalogDocumentWriteResult (Utf8Json, Error), CatalogDocumentReader (MaxDocumentBytes, MaxStringBytes, Read), CatalogDocumentWriter (Write), as sealed classes with required init properties of int, string and the collection types, Utf8Json a ReadOnlyMemory of bytes; the internal context, kind converter and rules; reflection switch in the Contracts test project | `src/Censio.Contracts/*.cs`, `tests/Censio.Contracts.Tests/Censio.Contracts.Tests.csproj` | AC2, AC4–AC8 | — | Dev | [ ] |
| 2 | G1 | Tests first: reader and writer | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs`, `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs` | AC2, AC4–AC8 | 1 | Tester | [ ] |
| 3 | G1 | Implement reader (five steps), writer and rules | `src/Censio.Contracts/*.cs` | AC2, AC4–AC8 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: GameCatalogDocumentConverter (ToDocument); fixture link in the Companion test project | `src/Censio.Companion.Core/GameCatalogDocumentConverter.cs`, `tests/Censio.Companion.Core.Tests/Censio.Companion.Core.Tests.csproj` | AC1, AC3 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: converter and fixture round trip | `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs` | AC1, AC3 | 4 | Tester | [ ] |
| 6 | G2 | Implement the converter | `src/Censio.Companion.Core/GameCatalogDocumentConverter.cs` | AC1, AC3 | 5 | Dev | [ ] |
| 7 | G3 | Area document, areas index, *Not here* pointers, `ARCHITECTURE.md` (plan, *Documentation updates*) | `docs/areas/game-catalog-format.md`, `docs/areas/README.md`, `docs/areas/game-catalog.md`, `docs/areas/game-catalog-cache.md`, `docs/areas/game-files.md`, `docs/ARCHITECTURE.md` | AC1–AC8 | 6 | Dev | [ ] |
| 8 | G3 | Fixture README of the Companion tests | `tests/Censio.Companion.Core.Tests/Fixtures/README.md` | AC3 | 5 | Tester | [ ] |
| 9 | G3 | `.squad/project.md` entries, text from plan *Documentation updates* | `.squad/project.md` | — | 3 | Orchestrator | [ ] |
