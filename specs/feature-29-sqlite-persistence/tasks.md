# Tasks: SQLite persistence for the server

Plan: [plan.md](plan.md)

Each task names its criteria, the tasks it waits for and its group (steps 4–6 run per group, one agent per member
and mode; rows consecutive). A group has a "Skeleton" row for its API in *Signatures*, or says "no new API". Each
documentation edit is in one row, owned by the Dev (test documentation: the Tester; an edit under `.squad/`: the
Orchestrator, from the text its member reports), in the last group.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: packages, tool `dotnet-ef`, `.editorconfig` migrations section, internal context, rows, design-time factory, generated `InitialSchema` migration; the sealed classes ServerDatabaseOpenResult (constructor; Kind, DatabaseFile as string, Database, Failure as Exception) and ServerDatabase (OpenAsync, both overloads; DatabaseFile; Dispose), the enum ServerDatabaseOpenKind (plan *Signatures*) | `Directory.Packages.props`, `.config/dotnet-tools.json`, `.editorconfig`, `src/Censio.Server.Data/**` | AC1, AC2, AC5, AC6 | — | Dev | [x] |
| 2 | G1 | Tests first: `ServerDatabaseTests` with `ExtendedMigrationsAssembly`, the three test migrations, `RecordingLoggerProvider`, the lock-row tests (facts 9 to 14) | `tests/Censio.Server.Data.Tests/*` | AC1, AC2, AC5, AC6 | 1 | Tester | [x] |
| 3 | G1 | Implement opening as *Approach* steps 1 to 7; report facts 9 and 10 | `src/Censio.Server.Data/ServerDatabase.cs` and internals | AC1, AC2, AC5, AC6 | 2 | Dev | [x] |
| 3a | G1 | Decided in step 6: lock-row test creates the lock table first; `ServerDatabaseOpenNewFileWithFailingStepReportsUpdateFailedAndLeavesNoTable`; `CensioDbContextFactoryTests` (after 3b) | `tests/Censio.Server.Data.Tests/ServerDatabaseTests.cs`, `CensioDbContextFactoryTests.cs` | AC2, AC5 | 3b | Tester | [x] |
| 3b | G1 | `InternalsVisibleTo` `Censio.Server.Data.Tests` | `src/Censio.Server.Data/Censio.Server.Data.csproj` | AC2 | 3 | Dev | [x] |
| 4 | G2 | Skeleton: ServerStore (constructor; StoreProfileAsync, ListProfilesAsync, StoreTickAsync, ReadTickAsync, ReadLatestTickAsync, ListIslandsAsync, StoreCatalogAsync, ReadCatalogAsync, StoreIconAsync, ReadIconAsync, FindMissingIconsAsync), ServerStoreOutcome, ServerStoreResult (constructor; Outcome, Failure), and StoredProfile, StoredIsland, StoredCatalog with required init properties | `src/Censio.Server.Data/*` | AC4, AC6, AC7, AC8, AC9, AC10, AC11 | 3 | Dev | [x] |
| 5 | G2 | Tests first: `ServerStoreTests` | `tests/Censio.Server.Data.Tests/ServerStoreTests.cs` | AC4, AC6, AC7, AC8, AC9, AC10, AC11 | 4 | Tester | [x] |
| 6 | G2 | Implement the store (write gate, prepared commands, reads) | `src/Censio.Server.Data/ServerStore.cs` and internals | AC4, AC6, AC7, AC8, AC9, AC10, AC11 | 5 | Dev | [x] |
| 7 | G3 | Skeleton: the static partial class DatabaseSetup (DatabaseFileKey, DatabaseFileName, TryOpenAsync, ReportOpenResult) | `src/Censio.Server/DatabaseSetup.cs` | AC1, AC2, AC3, AC4, AC5 | 3 | Dev | [x] |
| 8 | G3 | Tests first: `DatabaseSetupTests`, new `ProgramTests` | `tests/Censio.Server.Tests/DatabaseSetupTests.cs`, `tests/Censio.Server.Tests/ProgramTests.cs` | AC1, AC2, AC3, AC4, AC5 | 7 | Tester | [x] |
| 9 | G3 | Implement `DatabaseSetup`; call it in `Program.cs` after the ingest token; EF log level in `appsettings.json` | `src/Censio.Server/DatabaseSetup.cs`, `Program.cs`, `appsettings.json` | AC1, AC2, AC3, AC4, AC5 | 8 | Dev | [ ] |
| 10 | G4 | No new API. Dockerfile restore and publish with `-r linux-musl-x64 --self-contained false`, the publish also `-p:UseAppHost=false`; *Image build check* with the publish size before and after | `src/Censio.Server/Dockerfile` | AC1 | 9 | Dev | [ ] |
| 11 | G4 | `image-check.sh` cases `I29-AC1`, `I29-AC2`, `I29-AC6` (CI only) | `.github/scripts/image-check.sh` | AC1, AC2, AC6 | 10 | Dev | [ ] |
| 12 | G4 | Measure N4 (a tick at the bounds and a fixture tick: store time, file growth per fixture tick) with a scratch program outside the repository | — | AC12 | 6 | Dev | [ ] |
| 13 | G5 | No new API. `docs/areas/server-data.md` and its index row; `docs/areas/server.md` and the server row of the index | `docs/areas/*` | AC12 | 6, 9, 12 | Dev | [ ] |
| 14 | G5 | `docs/ARCHITECTURE.md`, `SECURITY.md`, `docs/CONTRIBUTING.md` | as named | AC12 | 13 | Dev | [ ] |
| 15 | G5 | `README.md` and `docs/USER_GUIDE.md`, English then German | as named | AC12 | 13 | Dev | [ ] |
| 16 | G5 | `.squad/project.md` (security area, integration surface, guarantee, test doubles) and `.squad/stack.md` (tool, *Image build check*), from the reported text | `.squad/project.md`, `.squad/stack.md` | AC12 | 13 | Orchestrator | [ ] |
