# Plan: Region type of the base game's region asset

Source: Issue #130
Tier: standard — a behavior change of the catalog reader (two more files read, a region type resolved through defaults) that creates, changes or removes no security control.
Security areas touched: Parsing of external input (`GameCatalogReader`: two more XML files read from `config.rda`, `.squad/project.md`)

## Problem / root cause

A region asset's region type is read from its own `Values/Region/RegionID` only
(`src/Censio.GameData/GameCatalogFiles.cs:333-335`), and a region asset without it gives its sessions no region type:
`RegionIdList` returns `null` for a missing value (`src/Censio.GameData/GameCatalogBuilder.cs:124-130`, called at line
25). The game supplies the missing value through its property defaults in `data/base/config/export/properties.xml`,
which the catalog does not read; `templates.xml` next to it states the properties each template has.

## Verified facts

Sample: the Product Manager's `templates.xml` (903,456 bytes) and `properties.xml` (432,889 bytes), kept outside the
repository, read with a scratch script in the session (counts and node names only).

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The reader takes `RegionID` only from the asset; a missing one gives *not stated* | `GameCatalogFiles.cs:333-335`, `GameCatalogBuilder.cs:124-130` | confirmed |
| 2 | Region asset 3225 states no `RegionID`; sessions 3245 and 99273 name it; 6626 and 149678 state `Celtic` and `Egyptian` | to verify: the Product Manager's installation run of the extended opt-in test (AC1), before step 9; evidence so far: Issue, from the #23 files; `assets.xml` not supplied | unverified |
| 3 | `properties.xml`: root `Properties/Groups/Group`, groups nest through `Groups/Group` (at most 4 levels); a group holds `DefaultContainerValues`, `DefaultValues` and `Name`, the `Name` **after** the values; two groups have no values | sample | confirmed |
| 4 | Defaults belong to the property, not the group: each of 1,057 property names appears under `DefaultValues` of exactly one group; `Region` appears once, in the group named `Session`, with `RegionID` `Roman`. The coordinator's "keyed by the template's group" is refuted: the `Region` template takes `Standard`, `Region`, `Text`, `Locked`, `RequiredSoundBanks` from different groups | sample | confirmed |
| 5 | `DefaultContainerValues/Region` holds no `RegionID` (only `PathLayerDefinitions`) | sample | confirmed |
| 6 | `templates.xml`: root `Templates`, groups `Group/Group` (at most 4 levels), 1,046 defining `Template` elements (directly under a `Group`, with `Name`, `Properties`, sometimes `Description`), names unique; 234 more `Template` elements are nested inside properties (not definitions) | sample | confirmed |
| 7 | The `Region` template (in group `Session`) has a `Properties/Region` section without `RegionID`; `RegionID` occurs nowhere in `templates.xml`; only this template has a `Region` property | sample | confirmed |
| 8 | Precedence asset > template > property default | to verify: not measurable on game version 3.0.1733841 (no template states `RegionID`); residual risk in record 0049, owner Lead, step 2; evidence so far: asset > default: required for `Celtic`/`Egyptian` to hold (structure, confirmed); template > default: building templates state `AssociatedRegions` values that differ from the `Building` default `Roman;Celtic;Egyptian` (inferred) | unverified |
| 9 | Both files start with a UTF-8 BOM and an XML declaration (unlike the fixtures of #23) | sample | confirmed |
| 10 | Sizes: properties.xml depth 19, 7,921 elements, largest property 588 elements / 13 levels; templates.xml depth 19, 18,367 elements, largest template 1,352 elements / 16 levels — within the existing per-part bounds (256 / 65,536) | sample | confirmed |
| 11 | Paths `data/base/config/export/templates.xml` and `.../properties.xml`; `RdaArchive.Read` compares paths ordinally (`RdaArchive.cs:191`) | to verify: the installation run (AC1, a wrong path fails as *File missing*), Product Manager, before step 9; evidence so far: Product Manager's screenshot (folder `export`), file names of the upload | unverified |
| 12 | No other archive carries a copy of the two files | to verify: the installation run writes the copies found, Product Manager, before step 9; evidence so far: not checked | unverified |
| 13 | Every inline region asset of the tests states a `RegionID` (`RegionAsset`, `GameCatalogReaderTests.cs:1570-1573`), so the inline tests keep their results when the fixture defaults are added | test code | confirmed |
| 14 | Related defect: goods, buildings and sessions also inherit template and property defaults the catalog ignores (`Building` default `AssociatedRegions` `Roman;Celtic;Egyptian`, templates stating `AssociatedRegions`, `SessionModerate` stating `Session/Region`) | sample; follow-up issue (below) | confirmed |

## Acceptance criteria

- AC1: A region asset's region type is the `RegionID` it states; without one, the `RegionID` its template's `Region`
  property states in `templates.xml`; without one, the `RegionID` of the `Region` property's `DefaultValues` in
  `properties.xml`. Only when none of the three states one, or the asset's template has no `Region` property, does it
  stay *not stated*. A resolved value not in the `Region` data set leaves an empty list and is counted once per region
  asset, as today. So the sessions of region asset 3225 carry the type the data assigns.
- AC2: No region type is written into the code: the type comes from the data whatever its name.
- AC3: The two files are read like the other catalog files: each in its own forward-only pass after the German texts
  (order data sets, assets, English, German, properties, templates); a missing, unreadable, malformed (wrong root,
  document type declaration) or over-bound file fails the catalog with its kind; a second `DefaultValues/Region`, or a
  second defining template of a name that has a `Region` property, is a malformed file; only `DefaultValues` children of
  a `Group` and `Template` elements directly under a `Group` count (not `DefaultContainerValues`, not a nested
  `Template`); a copy of either path in another archive is reported.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogReaderReadResolvesRegionTypeFromPropertyDefault` (replaces `GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutRegionId`: fixture session of region asset 102 → `Xanthi`); `GameCatalogReaderReadPrefersTemplateRegionIdOverDefault`; `GameCatalogReaderReadPrefersAssetRegionIdOverTemplate`; `GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutAnyRegionId`; `GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutRegionProperty`; `GameCatalogReaderReadDropsUnknownDefaultRegionId` (counted once per region asset); `GameCatalogReaderSampleResolvesFixtureGuids` and `GameCatalogReaderInstallationResolvesFixtureGuids` extended: each fixture region carries exactly one region type, and the test writes per fixture region its GUID, its region asset GUID and its number of region types; manual: the Product Manager runs the installation test on Windows on the branch head before step 9, and the output lists region asset 3225 with one region type | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC2 | `GameCatalogReaderReadTakesDefaultRegionIdFromData` (`DataRow` of two invented names, each in the data set and the property default, each resolved); `GameCatalogReaderFixtureTextsAreInvented` extended to the two new fixture files; check: the Reviewer finds no region type name of the game (`Roman`, `Celtic`, `Egyptian`) under `src/` (step 8) | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC3 | `GameCatalogReaderReadIgnoresContainerDefaultsAndNestedTemplates` (fixture decoys); `GameCatalogReaderReadFailsForSecondRegionDefault`; `GameCatalogReaderReadFailsForSecondRegionTemplate`; the file-index `DataRow` tests extended with rows 4 and 5: `GameCatalogReaderLoadFailsForMissingFile`, `GameCatalogReaderReadFailsForDocumentTypeDeclaration`, `GameCatalogReaderReadEndsWithKindOfSourceError`, `GameCatalogReaderReadGivesFileUnreadableWhenOpenerThrows`, `GameCatalogReaderReadDisposesStreamOfFailedPass`, `GameCatalogReaderReadAcceptsPartAtElementBound`, `GameCatalogReaderReadFailsOneAboveElementBound`, `GameCatalogReaderReadAcceptsPartAtDepthBound`, `GameCatalogReaderReadFailsOneAboveDepthBound`, `GameCatalogReaderReadBoundsDepthOutsideDefinitions`; `GameCatalogReaderReadOpensOneFileAtATime` (six passes in order); `GameCatalogReaderLoadReportsCopyOfTemplates` (an other archive listing `TemplatesPath` is reported) | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |

## Approach

- **Passes.** `GameCatalogReader.Read` reads, after the German texts, `properties.xml` with
  `GameCatalogDefaults.ReadDefaultRegionId` and `templates.xml` with `GameCatalogDefaults.ReadTemplateRegionIds`, each
  through the existing `TryReadPass` (one file open at a time, the source's error kind kept). `Load` opens them from
  `config.rda` by the two new path constants; `FindCopies` checks all six paths.
- **properties.xml** (root `Properties`): walk with `GameCatalogXml.Enter`/`HasParent` like the other files. Each child
  of a `DefaultValues` or `DefaultContainerValues` element whose parent is `Group` is a bounded part
  (`CountElement`, the existing 256 / 65,536 bounds); only a `DefaultValues` child named `Region` is interpreted: its
  child `RegionID` (relative depth 1) is the default, the content as read (trimmed later as today). A second
  `DefaultValues/Region` is malformed. The group's `Name` is not used (fact 4).
- **templates.xml** (root `Templates`): each `Template` whose parent is `Group` is a defining template and a bounded
  part; inside it `Name` (relative 1), the presence of `Properties/Region` (relative 2) and `Properties/Region/RegionID`
  (relative 3) are read; a nested `Template` is consumed as part of its defining template. Result: a dictionary from
  the name of each defining template with a `Region` property to its stated `RegionID` or `null`; a defining template
  without `Name` is ignored; a second one with a name already in the dictionary is malformed.
- **Builder.** For each region asset: `RegionId` of the asset, else — when its `Template` is in the dictionary — the
  template's value, else the default; when the template is not in the dictionary, only the asset's own value. The
  result goes through the existing `RegionIdList` (trim, data-set check, count). A stated empty `<RegionID />` at any
  level counts as stated (as today for the asset).
- **Fixture** (Tester, invented, BOM and XML declaration as the real files, CRLF like the others):
  `Fixtures/GameCatalog/properties.xml` — a `Default` group with `Standard`/`Text` values; nested `Groups/Group` holding
  `DefaultValues/Region/RegionID` `Xanthi` with sibling nodes and its `Name` after the values; `DefaultContainerValues/Region/RegionID`
  `Zorlan` (decoy); a group without values; a `DefaultValues/Session/Region` (another property's field named `Region`).
  `Fixtures/GameCatalog/templates.xml` — nested groups; the `Region` template in a group `Session` with a
  `Properties/Region` section without `RegionID`; a `SessionModerate` template; a template with `Description`; a
  template holding a nested `Template` named `Region` with `Properties/Region/RegionID` `Zorlan` (decoy). Fixture
  effect: sessions of region asset 102 resolve to `Xanthi`; 101, 103, 104 keep their own values; the dropped count stays 2.
- No user-visible text changes; layout: none.

## Options considered

1. Asset, then template, then property default, for `RegionID` only — chosen (record 0049).
2. Asset, then property default, without `templates.xml` — loses: a template stating its own `RegionID` after an
   update would be overridden by the default.
3. The same layering for every catalog fact — loses: changes kinds and region lists of goods, buildings and sessions
   beyond the issue; follow-up issue.
4. Defaults keyed by the property group's name (the coordinator's reading) — loses: refuted by fact 4.
5. A region type written into the code — forbidden by the issue and record 0047.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `GameCatalogReader.cs` | two path constants, `Read` with six openers, `Load` and `FindCopies` with six paths, bound docs "asset, text, template or property" |
| Censio.GameData | `GameCatalogDefaults.cs` (new) | reads the two files |
| Censio.GameData | `GameCatalogBuilder.cs` | `Build` resolves the region asset's `RegionID` through template and default |
| Censio.GameData.Tests | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs`, `Fixtures/GameCatalog/` | tests, two fixture files, README |

## Signatures (for the Dev's skeleton)

- `public const string PropertiesPath = "data/base/config/export/properties.xml";` (`GameCatalogReader`)
- `public const string TemplatesPath = "data/base/config/export/templates.xml";` (`GameCatalogReader`)
- `public static GameCatalogResult Read(Func<GameCatalogSource> openDataSets, Func<GameCatalogSource> openAssets, Func<GameCatalogSource> openEnglishTexts, Func<GameCatalogSource> openGermanTexts, Func<GameCatalogSource> openProperties, Func<GameCatalogSource> openTemplates)` — replaces the four-opener `Read`, which is removed.
- `internal static class GameCatalogDefaults` (new file `src/Censio.GameData/GameCatalogDefaults.cs`)
- `internal static string? ReadDefaultRegionId(Stream stream)` (`GameCatalogDefaults`)
- `internal static Dictionary<string, string?> ReadTemplateRegionIds(Stream stream)` (`GameCatalogDefaults`)
- `internal static GameCatalog Build(HashSet<string> regionTypes, GameCatalogAssets assets, GameCatalogTexts english, GameCatalogTexts german, Dictionary<string, string?> templateRegionIds, string? defaultRegionId)` (`GameCatalogBuilder`, changed)

Names checked against *Writing code* (PascalCase members, camelCase parameters, no abbreviations beyond `Id` as used
already). The skeleton rewrites `GameCatalogReader.cs` and `GameCatalogBuilder.cs`.

## Test files

`tests/Censio.GameData.Tests/GameCatalogReaderTests.cs`; fixtures `tests/Censio.GameData.Tests/Fixtures/GameCatalog/properties.xml`
and `templates.xml`.

Existing call sites of `Read` (old signature removed, so the **Dev** adapts them in step 4, mechanically): in
`GameCatalogReaderTests.cs` lines 173-176, 1025-1028, 1058, 1475, 1489-1492, 1513, 1763 — append `"properties.xml"`,
`"templates.xml"` to `_fileNames` and `GameCatalogReader.PropertiesPath`, `GameCatalogReader.TemplatesPath` to
`_filePaths`, and pass the openers of indices 4 and 5 the way each call site opens the others (`ReadCatalog` passes the
fixture's two files). Existing tests fail for the missing fixture files until the Tester adds them in step 5, first.
The **Tester** adapts in step 5: `GameCatalogReaderFixtureHoldsNoSampleText` (`names` gets entries for the two files),
`BoundedContent` and `BaseElements` (cases 4 and 5), `GameCatalogReaderFixtureTextsAreInvented`, and replaces
`GameCatalogReaderReadLeavesRegionTypeUnstatedWithoutRegionId`. `GameCatalogReaderReadResolvesSessionAsRegion` and
`...RandomSessionAsRegion` keep their expectations: their region assets state a `RegionID`, which wins over the new
default. No call site in `src/Censio.Companion*` (they call `Load`).

Files no unit test can cover: none.

## Areas

- `docs/areas/game-catalog.md` — *Files and nodes*: six files, pass order, the nodes of the two new files; *Region
  types*: the precedence and the template-without-`Region`-property rule (remove "the template default is #130");
  *Copies*: six paths; *Limits*: bounds per template and property, the real sizes (fact 10); *Failures*: the two
  duplicate cases.
- `docs/areas/game-files.md` — line 54 and 121 "four files" → six; the *Regions* row (line 110): the open question of
  #130 is answered (`templates.xml`, `properties.xml`).

## Documentation updates

- Dev: the two area documents above, against the code as built.
- Tester: `docs/UNIT_TESTS.md` line 33 (`CENSIO_CATALOG_SAMPLE` holds six loose files); `Fixtures/GameCatalog/README.md`
  (six files, the new variants, region type names stay invented).
- Orchestrator, from the Dev's text: `.squad/project.md`, *Security areas*, parsing line: "the asset, data set and
  text files" → also the template and property files.
- `docs/ARCHITECTURE.md`: none (line 59 stays true). README and user guide: none (the files read are named only as
  `config.rda`).

## Architecture check

The catalog still reads only `config.rda`, forward-only, one file at a time, writes nothing, and fails closed (record
0047); no guarantee of `.squad/project.md` is weakened. Memory grows by one 0.9 MB entry at a time and a dictionary of
one entry.

## Security considerations

- [x] The bounds are counted on elements as the reader meets them, after decoding, per template and per property,
  with the archive's per-entry bound before decompression (unchanged).
- [x] No exception text reaches a result: the two passes use the existing boundary mapping to the fixed texts of
  `GameCatalogReader.CreateError`.

## Decision records

- `docs/decisions/0049-region-type-of-a-region-asset-from-its-template-and-the-property-defaults.md` (Proposed, indexed).
- `docs/decisions/0047-…` edited in place (unreleased: `git tag --contains` of its commit is empty): the decision
  points to 0049, the #130 consequence is removed.

## Out of scope / follow-ups

- Follow-up issue — title: "[GameData] Template and property defaults for goods, buildings and sessions"; body:
  "The catalog reads `AssociatedRegion`, `AssociatedRegions`, `Session/Region` and the `Building` section only from an
  asset's own values. The game's `templates.xml` and `properties.xml` supply defaults for them too (the `Building`
  default lists region types, building templates state their own `AssociatedRegions`, `SessionModerate` names a region
  asset). Resolve them the way #130 resolves `RegionID`, and check the change of kinds and region lists against the
  installation. Follow-up of #130."
