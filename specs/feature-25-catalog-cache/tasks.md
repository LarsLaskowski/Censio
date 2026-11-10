# Tasks: Catalog cache and re-extraction after game updates

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `GameArchiveState` (Path, Length, LastWriteTimeUtc), `GameArchiveStateReader` (Read), `GameCatalogKey` (Identity, Folder, Build, Archives, IsSameGame), `GameCatalogCacheResultKind` (Loaded, Missing, CompanionChanged, GameChanged, Invalid), `GameCatalogCacheResult` (Kind, Catalog, Icons, FromContent, FromKind), `GameCatalogCacheFile` (FormatVersion, MaxFileBytes, MaxCatalogBytes, MaxStringBytes, Write, Read), `GameCatalog.AmbiguousGuids`, `GameIconSet` (Icons, Problems) | `src/Censio.GameData/` | AC1, AC2, AC6 | — | Dev | [ ] |
| 2 | G1 | Tests first: key, archive listing, cache file round trip and every invalid row | `tests/Censio.GameData.Tests/GameCatalogKeyTests.cs`, `GameArchiveStateReaderTests.cs`, `GameCatalogCacheFileTests.cs` | AC1, AC2, AC5, AC6 | 1 | Tester | [ ] |
| 3 | G1 | Implement key, listing, cache file reader and writer | `src/Censio.GameData/` | AC1, AC2, AC5, AC6 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `CompanionFolders.Cache`, `CompanionIdentity.Current`, `GameCatalogKeyReader.Read`, `GameCatalogCacheWriteResult` (IsWritten, Failure), `GameCatalogCacheStore` (FileName, constructor, Load, Save), `GameCatalogLoader` (new constructor, OnConnectionStatusChangedAsync, CatalogChanged), `GameCatalogStatusKind` (Checking, ExtractingNames, ExtractingIcons, IconsFailed, NotCached), `GameInstallationSearch.RefreshBuild`, `CompanionLog` (CatalogCacheNotUsed, CatalogCacheInvalid, CatalogCacheNotWritten, CatalogCheckFailed, CatalogExtracted, CatalogLoadedFromCache) | `src/Censio.Companion.Core/` | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | 3 | Dev | [ ] |
| 5 | G2 | Tests first: store, key reader, identity, loader (start, later checks, failures, durations, status sequence), tray texts, reporter reset, log text, refresh build, cache folder; adapt the 11 existing loader call sites | `tests/Censio.Companion.Core.Tests/` | AC1–AC7 | 4 | Tester | [ ] |
| 6 | G2 | Implement the Core changes and the wiring in `App.xaml.cs`; measure fact 9 in a scratch worktree | `src/Censio.Companion.Core/`, `src/Censio.Companion/App.xaml.cs` | AC1–AC7 | 5 | Dev | [ ] |
| 7 | PM | Manual run on a local build of the branch head: first start, second start, the duration lines | — | AC7, AC8 | 6 | Product Manager | [ ] |
| 8 | G3 | Area documents, `docs/areas/README.md` row, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md` (English and German, with the measured durations) | `docs/areas/game-catalog.md`, `docs/areas/companion.md`, `docs/areas/game-installation.md`, `docs/areas/README.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md`, `SECURITY.md` | AC8 | 6, 7 | Dev | [ ] |
| 9 | G3 | `.squad/project.md` *Security areas* lines, from the Dev's text | `.squad/project.md` | AC8 | 8 | Orchestrator | [ ] |
