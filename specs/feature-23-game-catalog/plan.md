# Plan: Game catalog of goods, buildings, workforce and regions with German and English names

Source: Issue #23 | [spec.md](spec.md)
Tier: standard — new behavior (a reader of the game's XML, tray line, diagnostics names, log lines) that creates, changes or removes no security control: refusing a document type declaration is a parser's validation, and no path is derived from file content.
Plan size: two projects, 13 new types, a new external format with its fixture and the shell wiring.
Security areas touched: Parsing of external input (new XML reader of the game's asset, data set and text files); File paths the Companion opens (the archives under the installation's `maindata`, read-only, and their listing); Logging and showing external data (game names in the diagnostics window, catalog log lines); Outbound calls (a UNC installation folder from the settings file: `config.rda` read and the `maindata` archives listed over SMB at every start)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

Sample = the Product Manager's four files of `config.rda` (outside the repository, scratchpad `sample/`), read by the Lead's read-only scratch scripts; fixture = `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl`.

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The fixture holds 3 session, 124 non-zero product, 11 non-zero workforce and 146 building GUIDs; `0` occurs as product and workforce GUID | Lead's decoder over the fixture; Devil's Advocate's `roles.json` agrees | confirmed |
| 2 | All 284 are defining assets of `assets.xml` with `Values/Text/OasisId` whose `LineId` has a non-empty English and German text | sample scan | confirmed |
| 3 | A definition is an `Asset` element directly under `Assets` with `Values/Standard/GUID` (42,359; none nested); other `Asset` elements are references inside `Item`; below a defining asset at most 46 levels and 3,740 elements, the longest text 2,568 characters, 43,567 `Text` elements per language | sample scan; Security's M4 | confirmed |
| 4 | Kinds: `Template` `Product` is a good, with `Values/Product/IsWorkforce` `1` a workforce (14); a building has a `Values/Building` section (all 146 fixture buildings, 11 templates); a session has `Values/Session/Region` naming a `Template` `Region` asset (4 region assets; sessions of `SessionModerate` and `SessionModerateRandom`; 240034 names `0`) | sample scan; `assets.xml` around line 87973 | confirmed |
| 5 | Region lists: `Values/Product/AssociatedRegion` (188 products), `Values/Building/AssociatedRegions` (515, 2 explicitly empty as `<AssociatedRegions />`), `;`-separated; every token is an item `Name` of the `DataSet` whose own `Name` is `Region`, directly under `DataSets/Group` (Meta, Roman, Celtic, Egyptian); another data set has an item named `Region` | sample scan, `datasets.xml` lines 1009 and 10662 | confirmed |
| 6 | Region assets 6626, 149678, 37136 have `Values/Region/RegionID`; 3225 has none | sample scan | confirmed |
| 7 | 962 assets have `BaseAssetGUID` directly under `Asset` and no `Template`; none in the fixture | sample scan, DA's `info.json` | confirmed |
| 8 | 75 `<GUID>` values are negative, one of them a defining `Standard/GUID`; no GUID is defined twice; 130 English `LineId`s repeat with one text each; no file has `<!DOCTYPE` or `<!ENTITY`; texts are UTF-8 without BOM or declaration, CRLF; texts hold `&lt;`/`&gt;` escapes but no fixture name has markup characters | sample scan, `file`, `grep -c` | confirmed |
| 9 | `TextExport/Texts/Text` holds `LineId` (signed 64-bit) and `Text` | sample, `head` | confirmed |
| 10 | No patch archive of the Product Manager's installation holds an asset or text file | Product Manager's statement 2026-11-09 | confirmed |
| 11 | `RdaArchive.Read` returns the whole entry as `ReadOnlyMemory<byte>`, bounded by `RdaLimits.MaxEntryBytes` (512 MiB default) | `src/Censio.GameData/RdaArchive.cs:182`, `RdaLimits.cs:37` | confirmed |
| 12 | Closed ticks arrive through `PipeConnection.TickReceived` and `PipeReplayRunner.TickReceived` (`EventHandler<PipeTick>`), one source per run | `PipeConnection.cs:120`, `PipeReplayRunner.cs:100`, `App.xaml.cs` | confirmed |
| 13 | `XmlReader` with `DtdProcessing.Prohibit` and no resolver throws `XmlException` on every document type form and on undeclared entities, invalid characters and bad encodings; `XmlException` and `int.Parse` messages quote content | Security's probe M1 (scratchpad `xmlprobe/`) | confirmed |
| 16 | `XNode.ReadFrom` is quadratic in depth (20,000 levels 1.04 s, 160,000 levels 101.8 s); a plain `XmlReader.Read` loop is linear (8 ms, 13 ms); a materialized wide asset costs 10–15 times its input | Security's probes M2, M3 | confirmed |
| 14 | A build on a real installation (40 archives listed, ~60 MB XML) takes seconds and peaks well below 512 MiB | to verify: the AC1 opt-in runs report elapsed time and peak working set — the Product Manager's installation run before step 9, the orchestrator's sample run after step 6 | unverified |
| 15 | A region asset without `RegionID` is the base game's region | to verify: the template default in the game data, by #130 after this change; the code assumes nothing | unverified |

## Tests per criterion

Every test carries `[TestCategory("ACn")]`. GameData tests read the anonymised fixture `Fixtures/GameCatalog/` (below) or synthetic XML; Companion tests build `GameCatalog` objects directly. The opt-in tests assert a name only with `Assert.IsFalse(string.IsNullOrEmpty(...))` and messages holding GUIDs, kinds and counts, never a name or text.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogReaderSampleResolvesFixtureGuids` (opt-in, `CENSIO_CATALOG_SAMPLE`: reads the four loose files, decodes the pipe fixture, asserts every non-zero GUID resolves with its expected kind and both names; writes counts, unresolved GUIDs, elapsed time and peak working set only); `GameCatalogReaderInstallationResolvesFixtureGuids` (opt-in, `CENSIO_ANNO_INSTALL`: the same through `Load`, also writes the copies found); manual: the Product Manager runs the second on Windows on the branch head before step 9, the orchestrator runs the first in the session | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC2 | `GameCatalogReaderReadResolvesSessionAsRegion`, `GameCatalogReaderReadResolvesRandomSessionAsRegion`, `GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutRegionId`, `GameCatalogReaderReadSkipsSessionWithoutRegionAsset`, `GameCatalogReaderReadResolvesGoodWithEveryRegionType`, `GameCatalogReaderReadResolvesWorkforce`, `GameCatalogReaderReadResolvesBuildingRegions`, `GameCatalogReaderReadKeepsMissingRegionListUnstated`, `GameCatalogReaderReadKeepsEmptyRegionListAsNone`, `GameCatalogReaderReadIgnoresProductionRegions`, `GameCatalogReaderReadDropsUnknownRegionType`, `GameCatalogReaderReadDropsUnknownRegionId`, `GameCatalogReaderReadFindsRegionDataSetByOwnName`, `GameCatalogReaderReadResolvesNegativeGuid`, `GameCatalogReaderReadResolvesFixtureEntries`, `GameCatalogReaderFixtureGuidsAreNotInPipeFixture` (always runs) | `GameCatalogReaderTests.cs` |
| AC3 | `GameCatalogReaderReadMarksDuplicateGuidAmbiguous`, `GameCatalogReaderReadMarksDifferingTextAmbiguous`, `GameCatalogReaderReadAcceptsRepeatedSameText`, `GameCatalogReaderReadAcceptsSharedTextId`, `GameCatalogReaderReadTreatsMissingLineIdAsNoName`, `GameCatalogReaderReadLeavesInheritedAssetOut` (the inherited asset has a `Values/Building` section and a text id); `GameCatalogCheckGivesProblemPerCase`; `GameCatalogReporterReportLogsUnknownBuildingAndWorkforce`, `GameCatalogReporterReportLogsUnknownRegionAndGood`, `GameCatalogReporterReportLogsIncompleteNamelessAndAmbiguous`, `GameCatalogReporterReportLogsOncePerGuidAndRole`, `GameCatalogReporterReportLogsNoName`, `GameCatalogReporterReportSkipsZero`, `GameCatalogReporterReportSkipsTickWithoutCatalog`, `GameCatalogReporterReportStopsAtMaxReports` (also asserts the remembered set stops growing at the cap) | `GameCatalogReaderTests.cs`, `tests/Censio.GameData.Tests/GameCatalogTests.cs`, `tests/Censio.Companion.Core.Tests/GameCatalogReporterTests.cs` |
| AC4 | `DiagnosticsViewCreateShowsBothNamesWithGuid`, `DiagnosticsViewCreateShowsAvailableNameOfIncompleteEntry`, `DiagnosticsViewCreateShowsNumberOfUnknownNamelessAndAmbiguous`, `DiagnosticsViewCreateShowsNumbersWithoutCatalog`, `DiagnosticsViewCreateShowsZeroAsNumber`, `DiagnosticsViewCreateSanitizesNames`, `DiagnosticsViewCreateCutsLongName`; `DiagnosticsWindowSessionOpenShowsCatalogNames` | `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs` |
| AC5 | `TrayMenuFormatCatalogGivesTextPerState`, `TrayMenuCreatePlacesCatalogBelowGame`; `GameCatalogLoaderRunGivesNoGameWithoutInstallation`, `GameCatalogLoaderRunGivesNoGameForSettingsError` (every kind but `Found`), `GameCatalogLoaderRunGivesReadyWithEntryCount`, `GameCatalogLoaderStatusIsReadingWhileBuilding` | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `GameCatalogLoaderTests.cs` |
| AC6 | `GameCatalogReaderLoadFailsWithoutConfigArchive`, `GameCatalogReaderLoadFailsForMissingFile`, `GameCatalogReaderLoadFailsAboveEntryBound` (lowered `RdaLimits`), `GameCatalogReaderReadFailsForMalformedXml` (the message equals the fixed text), `GameCatalogReaderReadFailsForDocumentTypeDeclaration`, `GameCatalogReaderReadFailsForNonIntegerGuid`, `GameCatalogReaderReadFailsWithoutRegionDataSet`, `GameCatalogReaderReadFailsForTwoRegionDataSets`, `GameCatalogReaderReadFailsForTooDeepAsset`, `GameCatalogReaderReadFailsForTooLargeAsset`, `GameCatalogReaderReadAcceptsNonSeekableStreams` (forward-only); `GameCatalogLoaderRunGivesFailedAndLogsKindOnly` | `GameCatalogReaderTests.cs`, `GameCatalogLoaderTests.cs` |
| AC7 | `GameCatalogReaderLoadWritesNoFile` (snapshot of the temporary installation before and after); `GameCatalogReaderFixtureTextsAreInvented` (always runs); `GameCatalogReaderFixtureHoldsNoSampleText` (opt-in, `CENSIO_CATALOG_SAMPLE`, counts only) | `GameCatalogReaderTests.cs` |

## Approach

**`Censio.GameData`.** One `XmlReader` per file (`DtdProcessing.Prohibit`, `XmlResolver` null, comments and whitespace ignored), forward-only, in four passes: the data set (the `DataSet` whose own `Name` child is `Region`, directly under `DataSets/Group`; none or two → `MalformedFile`), then `assets.xml`, then the English and German texts, keeping only the `LineId`s a catalog candidate uses (empty or missing in a language = no name there; one `LineId` with two different texts = ambiguous). An asset is walked with the reader itself, tracking `Depth` and collecting only the nodes of facts 3–7: a defining `Asset` (parent `Assets`) deeper than `GameCatalogReader.MaxAssetDepth` (256) levels or with more than `GameCatalogReader.MaxAssetElements` (65,536) elements, checked before each element, gives `MalformedFile`; the same bounds apply to each `Text` element of the text files. Rules in this order: no `Template` and a `BaseAssetGUID` → left out and counted (first, so its `Values/Building` section does not count); a `Values/Session/Region` naming a region asset → region (resolved after the pass; the entry gets `RegionGuid` and `[RegionID]`, or null when the region asset states none); `Template` `Product` → workforce if `IsWorkforce` is `1`, else good; a `Values/Building` section → building; anything else is not in the catalog. Region lists and `RegionID`: missing → null, empty (also `<AssociatedRegions />`) → empty list, tokens outside the data set dropped and counted (a dropped `RegionID` leaves an empty list). A GUID defined twice, or whose text id is ambiguous, is ambiguous. Integers are read with `TryParse` (`NumberStyles.AllowLeadingSign`, `CultureInfo.InvariantCulture`; GUIDs as `int`, text ids as `long`); a failed parse, a wrong root (`AssetList`, `DataSets`, `TextExport`) or an `XmlException` → `MalformedFile`. `GameCatalogError.Message` is a fixed text per kind, never an exception's message; every exception of `Read` and `Load` but `OutOfMemoryException` is caught at the reader boundary. `Load` opens `maindata/config.rda` read-only (`ArchiveUnreadable`), reads each entry only when its pass starts and releases it after (`FileMissing` on not found, `FileUnreadable` on any other read error, the bound included), so memory holds one entry, the candidates, the used texts and one asset's collected nodes; then it lists the other archives exactly as `RdaInstallationSurvey.FindArchives` does (recursive, links skipped, inaccessible folders ignored; `config.rda` skipped ignoring case), opens each (those that do not open counted) and records each entry with one of the four paths (`GameCatalogCopy`). Content is read through a read-only stream over the memory, never copied.

**`Censio.Companion.Core`.** `GameCatalogLoader.Run` takes the installation status: every kind but `Found` → `NoGame`; otherwise `build(folder)`, then `Ready` with the count or `Failed`, publishing the catalog with a volatile write and raising `StatusChanged`. `GameCatalogReporter.Report` reads the catalog (none → returns), walks every island's session GUID, product GUID and both maps' GUIDs (`0` skipped), and logs each (GUID, role) with a problem once, under a lock, up to `MaxReports`, then one cap line. `DiagnosticsView` formats the `Region` and `Good` cells (layout below), names through `LogLine.Sanitize(name, DiagnosticsView.MaxCatalogNameLength)` (4,096, cut with `…`); names are shown whatever the entry's kind.

**Shell (`Censio.Companion`).** `App.OnStartup` runs the loader in the same background task right after `gameSearch.Run`, hands `() => loader.Catalog` to the diagnostics session and the reporter, subscribes the reporter to the source's `TickReceived`, on `StatusChanged` to `Ready` reports the source's `LatestTick` once (`connection.LatestTick`, `runner.LatestTick`) and calls `_diagnostics?.Notify()` (dispatched), and gives `TrayIcon` `() => loader.Status`; `TrayIcon` treats `Catalog` as a greyed entry. All modes, the invalid command line included (it has no tick source).

**Layout of the new output.**
- Tray menu: `Catalog: <state>` directly below `Game:`, greyed out: `Catalog: reading` (also while the search runs), `Catalog: <n> entries` (`n` invariant, no grouping, plural also for 1), `Catalog: no game`, `Catalog: failed`.
- Diagnostics cells: both names `<English> / <German> (<GUID>)`; one name `<name> (<GUID>)`; otherwise, and without a catalog, the number as today.
- Log (all `Information` but failures and copies, `Warning`): `Catalog built: <n> entries; <i> inherited assets left out; <d> unknown region types dropped`; `Catalog failed: <kind>`; `Catalog: <archive> also lists <path>; the copy in config.rda is used`; `Catalog: <c> other archives could not be checked for copies` (only when `c` > 0); `Catalog cannot name <role> <GUID>: <problem>` with role `Region`, `Good`, `Building`, `Workforce` and problem `Unknown`, `Incomplete`, `Nameless`, `Ambiguous`; `Catalog: more than 1000 GUIDs cannot be named; further ones are not logged in this run`.

**Fixture (Tester).** `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` holds `assets.xml`, `datasets.xml`, `texts_english.xml` and `texts_german.xml` in the sample's structure (roots, nesting, CRLF, no declaration, element names of facts 3–9, neighbouring sections such as `Standard/Name`, `IconFilename`, `ProductionRegions` and a nested `Item/Asset` reference), with the variants: regions of both session templates and a region asset with, one without and one with an unknown `RegionID`, a session naming `0`, goods with one and three region types, a workforce, a building with a list, one without and one with `<AssociatedRegions />`, an inherited asset with a `Values/Building` section and a text id, an unknown type token, a negative GUID, a shared text id, a repeated same-text `LineId`, a `LineId` missing from one language, a name in one language only, a nameless asset, and in `datasets.xml` another data set with an item named `Region`. Every GUID, text id and `Region` item name is invented and occurs in no pipe fixture (asserted); every name, internal name and path text is invented (`Good <n>`, `Ware <n>` style), generated from nothing of the sample. The real GUIDs stay in the AC1 opt-in tests, which read them from the pipe fixture. The README lists the variants and the facts the tests assert. `Censio.GameData.Tests` references `Censio.Pipe` and links the pipe fixture for AC1.

## Options considered

- A document tree per file, generated schema types, skipping malformed assets, resolving inherited assets, a written-in region default: [0047](../../docs/decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md).
- A catalog file switch or no visible change: [0048](../../docs/decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md).
- `ProductionRegions` instead of the association lists: lost, it says where a good is produced (spec, *Challenge* 1).
- Building the catalog in `Censio.Companion.Core`: lost, `Censio.GameData` is the reader of the game's shape and must not leak it (`.squad/project.md`, *Integration surface*).
- Checking only the displayed cells: lost (spec, *Challenge* 2).
- Changing `DiagnosticsView.Create` instead of an overload: lost, the old call means "no catalog", a state the 26 existing call sites test correctly.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `GameCatalogKind`, `GameCatalogProblem`, `GameCatalogErrorKind`, `GameCatalogEntry`, `GameCatalogCopy`, `GameCatalog`, `GameCatalogError`, `GameCatalogResult`, `GameCatalogReader` | new |
| Censio.Companion.Core | `GameCatalogStatusKind`, `GameCatalogStatus`, `GameCatalogLoader`, `GameCatalogReporter` | new |
| Censio.Companion.Core | `TrayMenu`, `TrayMenuEntryKind`, `DiagnosticsView`, `DiagnosticsWindowSession`, `CompanionLog` | changed |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs` | changed (wiring) |

## Signatures (for the Dev's skeleton)

All public; properties are get-only unless "init" is named; "required" marks a required init property.

`Censio.GameData`:
- `GameCatalogKind` (enum): Region, Good, Building, Workforce.
- `GameCatalogProblem` (enum): None, Unknown, Incomplete, Nameless, Ambiguous.
- `GameCatalogErrorKind` (enum): ArchiveUnreadable, FileMissing, FileUnreadable, MalformedFile.
- `GameCatalogEntry` (sealed class): `GameCatalogEntry.AssetGuid` int, required init; `GameCatalogEntry.Kind` GameCatalogKind, required init; `GameCatalogEntry.EnglishName` and `GameCatalogEntry.GermanName` string?, init; `GameCatalogEntry.RegionGuid` int?, init; `GameCatalogEntry.RegionTypes` IReadOnlyList of string, nullable, init.
- `GameCatalogCopy` (sealed class): `public GameCatalogCopy(string archiveName, string path)`; `GameCatalogCopy.ArchiveName` and `GameCatalogCopy.Path` string.
- `GameCatalog` (sealed class): `public GameCatalog(IReadOnlyCollection<GameCatalogEntry> entries, IReadOnlyCollection<int> ambiguousGuids, int inheritedAssetCount, int droppedRegionTypeCount)`; `GameCatalog.Count`, `GameCatalog.InheritedAssetCount`, `GameCatalog.DroppedRegionTypeCount` int; `public GameCatalogEntry? Find(int assetGuid)`; `public GameCatalogProblem Check(int assetGuid)`.
- `GameCatalogError` (sealed class): `public GameCatalogError(GameCatalogErrorKind kind, string message)`; `GameCatalogError.Kind`, `GameCatalogError.Message`.
- `GameCatalogResult` (sealed class): `GameCatalogResult.Catalog` GameCatalog?; `GameCatalogResult.Error` GameCatalogError?; `GameCatalogResult.Copies` IReadOnlyList of GameCatalogCopy; `GameCatalogResult.UncheckedArchiveCount` int; `public static GameCatalogResult FromCatalog(GameCatalog catalog, IReadOnlyList<GameCatalogCopy> copies, int uncheckedArchiveCount)`; `public static GameCatalogResult FromError(GameCatalogError error)`.
- `GameCatalogReader` (static class): int constants `GameCatalogReader.MaxAssetDepth = 256` and `GameCatalogReader.MaxAssetElements = 65536`; string constants `GameCatalogReader.AssetsPath = "data/base/config/export/assets.xml"`, `GameCatalogReader.DataSetsPath = "data/base/config/game/datasets.xml"`, `GameCatalogReader.EnglishTextsPath = "data/base/config/gui/texts_english.xml"`, `GameCatalogReader.GermanTextsPath = "data/base/config/gui/texts_german.xml"`; `public static GameCatalogResult Read(Stream assets, Stream dataSets, Stream englishTexts, Stream germanTexts)`; `public static GameCatalogResult Load(string installationFolder, RdaLimits limits)`.

`Censio.Companion.Core`:
- `GameCatalogStatusKind` (enum): Reading, Ready, NoGame, Failed.
- `GameCatalogStatus` (sealed record): static `GameCatalogStatus.Reading`; `GameCatalogStatus.Kind` GameCatalogStatusKind, required init; `GameCatalogStatus.EntryCount` int, init.
- `GameCatalogLoader` (sealed class): `public GameCatalogLoader(Func<string, GameCatalogResult> build, ILogger logger)`; `GameCatalogLoader.Status` GameCatalogStatus; `GameCatalogLoader.Catalog` GameCatalog?; event `GameCatalogLoader.StatusChanged` (EventHandler of GameCatalogStatus); `public GameCatalogStatus Run(GameInstallationStatus installation)`.
- `GameCatalogReporter` (sealed class): int constant `GameCatalogReporter.MaxReports = 1000`; `public GameCatalogReporter(Func<GameCatalog?> readCatalog, ILogger logger)`; `public void Report(PipeTick tick)`.
- `TrayMenu`, changed: `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, GameInstallationStatus game, GameCatalogStatus catalog, bool isAutostartEnabled, TimeProvider clock)`; new `public static string FormatCatalog(GameCatalogStatus status)`.
- `TrayMenuEntryKind.Catalog`: new last member.
- `DiagnosticsView`: int constant `DiagnosticsView.MaxCatalogNameLength = 4096`; new overload: `public static DiagnosticsView Create(string statusText, PipeTick? tick, GameCatalog? catalog, TimeProvider clock)`; the existing overload delegates with a null catalog.
- `DiagnosticsWindowSession`, changed: `public DiagnosticsWindowSession(Func<string> readStatusText, Func<PipeTick?> readTick, Func<GameCatalog?> readCatalog, TimeProvider clock, Action<Action> post, Func<IDiagnosticsWindow> createWindow)`.
- `CompanionLog`, new static partial methods: `public static partial void CatalogBuilt(ILogger logger, int entryCount, int inheritedAssetCount, int droppedRegionTypeCount)`, `public static partial void CatalogFailed(ILogger logger, GameCatalogErrorKind kind)`, `public static partial void CatalogCopyFound(ILogger logger, string archive, string path)`, `public static partial void CatalogArchivesUnchecked(ILogger logger, int count)`, `public static partial void CatalogCannotName(ILogger logger, GameCatalogKind role, int assetGuid, GameCatalogProblem problem)`, `public static partial void CatalogReportsCapped(ILogger logger, int maxReports)`.

`Censio.Companion` (no test project), changed: `public TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<GameInstallationStatus> game, Func<GameCatalogStatus> catalog, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)`.

## Test files

`tests/Censio.GameData.Tests/GameCatalogReaderTests.cs`, `tests/Censio.GameData.Tests/GameCatalogTests.cs`, `tests/Censio.Companion.Core.Tests/GameCatalogLoaderTests.cs`, `tests/Censio.Companion.Core.Tests/GameCatalogReporterTests.cs`; added to `DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs`, `TrayMenuTests.cs`; fixture `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` with `README.md`; test double `tests/Censio.GameData.Tests/NonSeekableStream.cs` (a read-only, non-seekable wrapper); `tests/Censio.GameData.Tests/Censio.GameData.Tests.csproj` (reference `Censio.Pipe`, fixtures copied).

Call sites of changed signatures: 13 `TrayMenu.Create` calls in `TrayMenuTests.cs` and 2 `DiagnosticsWindowSession` constructions in `DiagnosticsWindowSessionTests.cs` — the Tester, in step 5; their inputs are unchanged by a new criterion apart from the extra entry, which `TrayMenuTests`' order and count assertions take up. `DiagnosticsView.Create` keeps its three-argument overload: 26 call sites unchanged. Production: `TrayIcon.cs`, `App.xaml.cs` — the Dev.

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs`, `src/Censio.Companion/TrayIcon.cs` — the Windows shell, already excluded (record 0004); no new exclusion.

## Areas

- New area `game-catalog` — scope: building the catalog from the game's asset, data set and text files: the files and nodes read, kinds, names, regions and region types, ambiguous, incomplete, nameless, inherited and zero GUIDs, copies in other archives, bounds, failure kinds, privacy. Not here: the archives (game-files), the tray line, the diagnostics cells and the log lines (companion), icons, cache and format (#24–#26).
- New area `game-catalog` also states the two asset bounds (*Limits*), the true memory bound, and that a UNC installation folder means reading `config.rda` and listing the archives over the network at every start.
- `game-files`: *Scope* and *Not here* point to game-catalog; *Locations* rows for goods/buildings/workforce, regions and texts state what #23 confirmed; the load-order bullet records the Product Manager's answer; *Limits*' "is for #23" sentence says the files fit the entry bound.
- `companion`: *Tray icon and menu* (the `Catalog:` entry), *Diagnostics window* (named cells; the "raw numbers until names" sentence), *Log* (the catalog lines).
- `game-installation`: *Not here* names the catalog's area; the *Folders from outside* bullet says that with a found UNC folder the catalog then reads `config.rda` and lists the `maindata` archives over the network.
- `companion` *Log* also says that ticks closed before the catalog is ready are not checked, except the latest one at that moment.

## Documentation updates

Dev, after the code is green:
- `docs/areas/game-catalog.md` (new, from `docs/areas/_template.md`) and its row in `docs/areas/README.md`; the `game-files`, `companion` and `game-installation` index rows' scope/not-here cells; the area edits above.
- `docs/ARCHITECTURE.md`: the `Censio.GameData` row links `areas/game-catalog.md`; the `Censio.Companion.Core` row adds "the catalog loading and the check of tick GUIDs".
- The `Catalog:` entry in the tray menu lists of `README.md` (English, line 105; German, line 270) and `docs/USER_GUIDE.md` (line 109 and its German counterpart).
- `README.md` *Status*, English and German: the "No names or icons" bullet becomes what the catalog shows (names in the diagnostics window, icons still missing); the diagnostics sentence of "What it can do".
- `docs/USER_GUIDE.md`, English and German: a *Catalog* subsection under *Companion* (what it reads, the four menu texts with what to do, the named cells, that unresolved GUIDs are in the log by number), and the diagnostics description in *Connecting to the game*.
- `.squad/project.md` (the Dev reports the exact text; written as `.squad/team.md`, *Files*, says): *Parsing of external input* adds `GameCatalogReader` (game asset, data set and text files) and links game-catalog; *File paths* adds `GameCatalogReader.Load` (the archives under `maindata`, read-only); *Logging and showing external data* adds the names in the diagnostics window and the catalog log lines; *Outbound calls* adds that a UNC folder from the settings file is also read (`config.rda`) and listed (`maindata`) over SMB at every start. The Tester reports the *Test doubles* line for `NonSeekableStream`.

Tester: `docs/UNIT_TESTS.md` names `CENSIO_CATALOG_SAMPLE` (the folder of the four sample files, never in the repository; unset, the tests are inconclusive) and that `CENSIO_ANNO_INSTALL` also runs the catalog test; the fixture `README.md`.

## Architecture check

No guarantee of `.squad/project.md` is touched. Record 0023 holds: nothing is written, no name reaches a log, a fixture or test output (AC7). Record 0004 holds: the reader and the rules are platform-neutral, the shell only wires. Record 0033's fail-closed reading is extended to the XML (0047). The external shape stays inside `Censio.GameData`; the Companion sees only catalog types.

## Security considerations

- [x] Limits: each file is bounded before decoding by the archive's per-entry bound on its stored and decompressed bytes (`RdaLimits.MaxEntryBytes`), one entry held at a time; no entity is resolved, so decoding adds no expansion beyond UTF-16. Per defining asset and per text entry: depth 256 and 65,536 elements, counted on the decoded element structure as it is read, before each element. Retained: one entry, the candidates, the texts they use, one asset's collected nodes. Diagnostics names are cut at 4,096 UTF-16 characters after sanitizing; the log cap counts lines (1,000 per run), and the log writer cuts each message at 2,048 characters.
- [x] Exceptions: `XmlException`, a failed integer parse and every other exception but `OutOfMemoryException` become a typed result with a fixed text, archive results their fixed kinds; the user sees only `Catalog: failed` and the log line `Catalog failed: <kind>`, never an exception text. An unexpected exception of the background task is logged by the existing `BackgroundTaskFailed`, and the status stays `Catalog: reading` — accepted, since every expected failure is typed.

## Challenge

1. Fixture would pass hard-coded regions (major) — accepted: every fixture GUID, text id and `Region` item name is invented and absent from the pipe fixture, asserted by `GameCatalogReaderFixtureGuidsAreNotInPipeFixture`; real GUIDs only in the AC1 opt-in tests.
2. Inherited order — accepted: the exclusion runs first; the fixture's inherited asset has a `Values/Building` section and a text id.
3. Negative GUIDs — accepted: signed `TryParse`, fact 8, a fixture case and `ResolvesNegativeGuid`.
4. Region data set by its own name — accepted: fact 5, `FindsRegionDataSetByOwnName`, `FailsForTwoRegionDataSets`.
5. `RegionID` checked — accepted: dropped and counted, `DropsUnknownRegionId`.
6. Only `Found` builds — accepted: `GivesNoGameForSettingsError`.
7. Archive listing — accepted: the survey's enumeration, `config.rda` skipped ignoring case.
8. Ticks before ready — accepted: the latest tick is reported at `Ready`; spec S3 and `companion.md` (*Log*) say so; record 0048 updated.
9. Forward-only untested — accepted: `AcceptsNonSeekableStreams` with `NonSeekableStream`.
10. Fixture shapes and missing `LineId` — accepted: `<AssociatedRegions />`, `TreatsMissingLineIdAsNoName`.
11. Tray menu lists — accepted: added to *Documentation updates*.

## Security review

- B1 (work per asset) — fixed: the asset is walked with the `XmlReader`, no `XNode.ReadFrom`; bounds 256 levels and 65,536 elements checked before each element, also per text entry; `FailsForTooDeepAsset`, `FailsForTooLargeAsset`; stated in `game-catalog.md` (*Limits*).
- B2 (memory) — fixed: each entry read when its pass starts and released after; texts kept only for used ids; the true bound is in *Security considerations*, spec N3 and record 0047; the AC1 runs report peak working set (fact 14).
- B3 (outbound calls) — fixed: area added, sentences in `game-catalog.md` and `game-installation.md`, `.squad/project.md` text.
- N1 — fixed: fixed texts, `TryParse`, exceptions caught at the boundary; `FailsForMalformedXml` asserts the text.
- N2 — fixed: `DiagnosticsView.MaxCatalogNameLength` (4,096), `CutsLongName`; spec E5 and `companion.md`.
- N3 — fixed: the opt-in assertion rule under *Tests per criterion*.
- The reporter's remembered set — fixed: `StopsAtMaxReports` asserts it.

## Decision records

- `docs/decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md` (Proposed)
- `docs/decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md` (Proposed)

Both name the new area `game-catalog`; `decision-check.py` reports the area as unlisted until the Dev adds its index row.

## Out of scope / follow-ups

- #130: the region type of a region asset without `RegionID`.
- Icons (#24), cache and measured duration (#25), catalog format (#26), upload (#42).
