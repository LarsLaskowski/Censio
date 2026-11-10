# Tasks: Extract and convert icons

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `Bc7Decoder` (BlockBytes, DecodeBlock), `DdsReader` (Read), `DdsImage` (Width, Height, Rgba), `PngWriter` (Write) | `src/Censio.GameData/` | AC1 | — | Dev | [x] |
| 2 | G1 | Tests first; build the reference scratch project outside the repository and pin the expected BC7 pixels from it (plan facts 12, 13) | `Bc7DecoderTests.cs`, `DdsReaderTests.cs`, `PngWriterTests.cs`, `DdsFileBuilder.cs` | AC1, AC3 | 1 | Tester | [x] |
| 3 | G1 | Implement decoder, header check, PNG writer | `src/Censio.GameData/` | AC1, AC3 | 2 | Dev | [x] |
| 4 | G1 | Re-run the reference project on the four real samples, counts only (fact 13) | scratch, outside the repository | AC1 | 3 | Tester | [ ] |
| 5 | G2 | Skeleton: `GameCatalogEntry.IconFilename`; `GameIconReader` (MaxIconSide, MaxIconEntryBytes, MaxIconPathLength, MaxIconSetBytes, UiArchiveName, Load, EntryPathOf, PatchNumberOf); `GameIcon` (Width, Height, Png); `GameIconSet` (Find, Check, Count, GoodsWithIcon, RegionsWithIcon, PngBytes); `GameIconProblem`; `GameIconError` (Kind, Archive); `GameIconErrorKind`; `GameIconResult` (Icons, Error, FromIcons, FromError) | `src/Censio.GameData/` | AC2, AC3, AC6, AC7 | 3 | Dev | [ ] |
| 6 | G2 | Tests first, including the opt-in installation test | `GameIconReaderTests.cs`, `GameCatalogReaderTests.cs` | AC2, AC3, AC4, AC6, AC7 | 5 | Tester | [ ] |
| 7 | G2 | Implement `IconFilename` reading and the icon step | `GameCatalogAsset.cs`, `GameCatalogFiles.cs`, `GameCatalogBuilder.cs`, new types | AC2, AC3, AC6, AC7 | 6 | Dev | [ ] |
| 8 | G3 | Skeleton: `GameCatalogLoader` constructor, Icons, IconsChanged; `DiagnosticsRow` constructor, RegionIcon, GoodIcon; `DiagnosticsView.Create` overload; `DiagnosticsWindowSession` constructor; `CompanionLog` CatalogIconsBuilt, CatalogIconsFailed | `src/Censio.Companion.Core/` | AC3, AC5 | 7 | Dev | [ ] |
| 9 | G3 | Tests first | `GameCatalogLoaderTests.cs`, `DiagnosticsViewTests.cs` | AC3, AC5 | 8 | Tester | [ ] |
| 10 | G3 | Implement Core, wire `App`, template columns and `GameIconImageConverter` (no new API) | `src/Censio.Companion.Core/`, `src/Censio.Companion/` | AC3, AC5 | 9 | Dev | [ ] |
| 11 | G3 | Check of AC6: `git ls-files`, Companion publish listing, `Directory.Packages.props` | — | AC6 | 10 | Dev | [ ] |
| 12 | G3 | Documentation updates of the plan | `docs/areas/game-catalog.md`, `game-files.md`, `companion.md`, `README.md` (areas), `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md` | AC5 | 10 | Dev | [ ] |
| 13 | G3 | `.squad/project.md` entries from the Dev's and the Tester's text | `.squad/project.md` | — | 12 | Orchestrator | [ ] |
| 14 | PM | Opt-in installation run and window check on Windows | — | AC4, AC5 | 12 | Product Manager | [ ] |
