# Tasks: Game catalog of goods, buildings, workforce and regions with German and English names

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: GameCatalogKind, GameCatalogProblem, GameCatalogErrorKind, GameCatalogEntry (AssetGuid, Kind, EnglishName, GermanName, RegionGuid, RegionTypes), GameCatalogCopy (ArchiveName, Path), GameCatalog (Count, InheritedAssetCount, DroppedRegionTypeCount, Find, Check), GameCatalogError (Kind, Message), GameCatalogResult (Catalog, Error, Copies, UncheckedArchiveCount, FromCatalog, FromError), GameCatalogSource (FromStream, FromError, Stream, ErrorKind), GameCatalogReader (MaxAssetDepth, MaxAssetElements, AssetsPath, DataSetsPath, EnglishTextsPath, GermanTextsPath, Read, Load) | `src/Censio.GameData/GameCatalog*.cs` | AC1, AC2, AC3, AC6, AC7 | — | Dev | [ ] |
| 2 | G1 | Anonymised fixture `Fixtures/GameCatalog/` with README, test project reference to `Censio.Pipe` and the linked pipe fixture | `tests/Censio.GameData.Tests/Fixtures/GameCatalog/`, `tests/Censio.GameData.Tests/Censio.GameData.Tests.csproj` | AC1, AC2, AC7 | 1 | Tester | [ ] |
| 3 | G1 | Tests first: `GameCatalogReaderTests`, `GameCatalogTests`, the double `NonSeekableStream` | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs`, `GameCatalogTests.cs`, `NonSeekableStream.cs` | AC1, AC2, AC3, AC6, AC7 | 2 | Tester | [ ] |
| 4 | G1 | Implement the reader, the catalog and `Load` | `src/Censio.GameData/GameCatalog*.cs` | AC1, AC2, AC3, AC6, AC7 | 3 | Dev | [ ] |
| 5 | G2 | Skeleton: GameCatalogStatusKind, GameCatalogStatus (Reading, Kind, EntryCount), GameCatalogLoader (Status, Catalog, StatusChanged, Run), GameCatalogReporter (MaxReports, Report), TrayMenu.Create and FormatCatalog, TrayMenuEntryKind.Catalog, DiagnosticsView.MaxCatalogNameLength and the DiagnosticsView.Create overload, the DiagnosticsWindowSession constructor, CompanionLog (CatalogBuilt, CatalogFailed, CatalogCopyFound, CatalogArchivesUnchecked, CatalogCannotName, CatalogReportsCapped), the TrayIcon constructor | `src/Censio.Companion.Core/` | AC3, AC4, AC5, AC6 | 4 | Dev | [ ] |
| 6 | G2 | Tests first: `GameCatalogLoaderTests`, `GameCatalogReporterTests`, additions to `DiagnosticsViewTests`, `DiagnosticsWindowSessionTests`, `TrayMenuTests`; adapt the 13 `TrayMenu.Create` and 2 session call sites | `tests/Censio.Companion.Core.Tests/` | AC3, AC4, AC5, AC6 | 5 | Tester | [ ] |
| 7 | G2 | Implement the Companion.Core types and wire `App.xaml.cs` and `TrayIcon.cs` | `src/Censio.Companion.Core/`, `src/Censio.Companion/App.xaml.cs`, `src/Censio.Companion/TrayIcon.cs` | AC3, AC4, AC5, AC6 | 6 | Dev | [ ] |
| 8 | G3 | (no new API) Area documents (`game-catalog` new with index row, `game-files`, `companion`, `game-installation`), `docs/ARCHITECTURE.md`, `README.md` and `docs/USER_GUIDE.md` (English and German), the text for `.squad/project.md` | `docs/`, `README.md` | AC1–AC7 | 7 | Dev | [ ] |
| 9 | G3 | `docs/UNIT_TESTS.md`: `CENSIO_CATALOG_SAMPLE` and the catalog run of `CENSIO_ANNO_INSTALL` | `docs/UNIT_TESTS.md` | AC1, AC7 | 7 | Tester | [ ] |
| 10 | G3 | Report the exact `.squad/project.md` text (security areas; the Tester's *Test doubles* line), written as `.squad/team.md` *Files* says | `.squad/project.md` | — | 8 | Dev | [ ] |
| 11 | PM | Run `GameCatalogReaderInstallationResolvesFixtureGuids` on Windows on the branch head with `CENSIO_ANNO_INSTALL`; post counts, copies, elapsed time and peak working set only (plan facts 13, 14) | — | AC1 | 7 | Product Manager | [ ] |
| 12 | G3 | Run the `CENSIO_CATALOG_SAMPLE` tests on the scratchpad sample and log the counts | — | AC1, AC7 | 7 | Orchestrator | [ ] |

