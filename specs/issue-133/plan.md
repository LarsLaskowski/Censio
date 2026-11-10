# Plan: Template and property defaults for goods, buildings and sessions

Source: Issue #133
Tier: standard — the catalog's kinds and region lists change (more files' values read, passes reordered); no security control is created, changed or removed.
Security areas touched: Parsing of external input (`GameCatalogReader`: more nodes of `templates.xml` and `properties.xml` interpreted, a new integer value parsed from them, pass order changed, `.squad/project.md`)

## Problem / root cause

The asset pass reads `Product/AssociatedRegion`, `Building/AssociatedRegions`, `Session/Region` and the presence of a
`Building` section from the asset's own values only (`src/Censio.GameData/GameCatalogFiles.cs:260`, `:312-331`), and
the kind and candidacy are decided from them at once (`GameCatalogAssets.cs:73-93`); only `RegionID` is layered
(`GameCatalogBuilder.cs:24-28`, `:126-139`). The template and property defaults are read after the assets and texts
(`GameCatalogReader.cs:97-98`), too late to decide a kind: the texts are read only for the candidates
(`GameCatalogReader.cs:89`).

## Verified facts

Sample: the invented fixture `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` (structure of the Product Manager's
real files of game version 3.0.1733841) and the notes taken from the real files in #130 (plan at `d0a87cc`, facts
3–10, 14). The Tester adds the variants it lacks from those notes; AC4's installation run confirmed facts 4, 5 and 7.

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Goods, buildings and sessions take the four facts from their own values only; a building needs its own `Building` section | `GameCatalogFiles.cs:260`, `:312-331`; `GameCatalogAssets.cs:86-93`; `GameCatalogBuilder.cs:107-116` | confirmed |
| 2 | Kinds and candidates are fixed in the asset pass, before the texts, which are read for candidates only; properties and templates are read last | `GameCatalogAssets.cs:73-77`; `GameCatalogReader.cs:83-99` | confirmed |
| 3 | The `Building` property default lists region types (`Roman;Celtic;Egyptian`); building templates state their own `AssociatedRegions`; `SessionModerate` states a `Session/Region` (issue) | #130 plan facts 8 and 14 (real files) | confirmed |
| 4 | The node paths are `Template/Properties/<Property>/<Field>` and `Group/DefaultValues/<Property>/<Field>` for `Building/AssociatedRegions`, `Product/AssociatedRegion`, `Session/Region` as for `Region/RegionID` | AC4 installation run, Product Manager, 2026-11-10: `Building` lists not stated → list 338, sessions none → Region 2; `Product` by the same walk, no good changed (fact 7) | confirmed |
| 5 | `SessionModerate`'s `Session/Region` and the `Session` default are integers or empty | AC4 installation run, Product Manager, 2026-11-10: no *Malformed file* | confirmed |
| 6 | Each property has its defaults in exactly one group, so a second default of `Building`, `Product` or `Session` never occurs in the real file | #130 fact 4 (1,057 names, each once) | confirmed |
| 7 | Whether a template or default states `Product/AssociatedRegion`, and whether templates hold empty fields as placeholders | AC4 installation run, Product Manager, 2026-11-10: no good or workforce changed, no transition to *none*, kinds changed only none → Region 2: no effect in the real files | confirmed |
| 8 | On `main` the installation catalog has 1,038 entries and every pipe-fixture GUID resolves to its kind | #130 working record, Product Manager's run of 2026-11-10 | confirmed |
| 9 | The fixture lists no template `Product` or `ResidenceBuilding7`; its `SessionModerate` template states an empty `Session/Region`, its `Session` default `0`; every fixture and inline session states its own `Session/Region` | `Fixtures/GameCatalog/templates.xml`, `properties.xml`, `assets.xml`; `GameCatalogReaderTests.cs:578`, `:2057-2060` | confirmed |
| 10 | Tests use only `Read` and `Load`; `GameCatalog` cannot enumerate entries | no `InternalsVisibleTo`; `GameCatalog.cs:57-90` | confirmed |
| 11 | Records 0047 and 0049 are unreleased | `git tag --contains` of their commits: none (`v0.1.0` is `6d0384e`) | confirmed |

## Acceptance criteria

- AC1: An asset that is no inherited asset, no region asset, no region entry and not of template `Product` is a
  building when it has its own `Building` section or its template has a `Building` property in `templates.xml`;
  otherwise it is no building. The order of the kind rules is unchanged.
- AC2: `Product/AssociatedRegion` (goods, workforce), `Building/AssociatedRegions` (buildings), `Session/Region` and
  `Region/RegionID` are each the asset's own value; without one, when the asset's template has that property in
  `templates.xml`, the template's value; without one, the value of the property's `DefaultValues` in
  `properties.xml`. A template that is not listed or lacks the property gives only the asset's own value. A present
  empty element is stated at every level (an empty list means *none*, an empty `Session/Region` no region, like `0`).
  The resolved `Session/Region` decides the region entry, and `0` never makes one (also against a region asset of
  GUID `0`). Each source list (an asset's own, a template's, a default's) is split and checked once: the entries that
  resolve to one template or default list share one read-only list, and its dropped names are counted once per source
  list, as a dropped `RegionID` once per region asset.
- AC3: `properties.xml` and `templates.xml` are read before the assets: passes data sets, properties, templates,
  assets, English, German, one file open at a time, a failing pass ending the read with its kind and no later file
  opened. A second `DefaultValues` part `Building`, `Product`, `Session` or `Region` anywhere in the file (any
  group), or a `Session/Region` in a
  template or default that is neither empty nor a signed 32-bit integer, is a malformed file; `DefaultContainerValues`
  and nested templates stay unread.
- AC4: The opt-in sample and installation tests also build the catalog with empty `properties.xml` and
  `templates.xml` (the assets' own values) and write the difference as counts and GUIDs, never a name, and fail when
  an entry of that build is missing; the Product Manager runs the installation test and confirms the reported changes
  against his files.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogReaderReadMakesBuildingOfTemplateWithBuildingProperty` (fixture asset without own section); `GameCatalogReaderReadMakesNoBuildingWithoutBuildingProperty` (`DataRow`: template listed without it; not listed); `GameCatalogReaderReadKeepsKindOrderWithTemplateProperties` (`DataRow`: `Product` template with a `Building` property → good; template with `Building` and a `Session/Region` naming a region asset → region) | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC2 | `GameCatalogReaderReadTakesBuildingRegionsFromTemplate`, `...TakesBuildingRegionsFromPropertyDefault`, `...PrefersOwnBuildingRegionsOverTemplate` (fixture); `GameCatalogReaderReadLayersProductRegions` (`DataRow`: own, template, default; a good and a workforce); `GameCatalogReaderReadLayersSessionRegion` (`DataRow`: own wins, template, default, template `0` over a default region, default `0` with a region asset of GUID `0`); `GameCatalogReaderReadCountsEmptyLayerValueAsStated` (`DataRow` per fact and level); `GameCatalogReaderReadTakesNoDefaultWithoutProperty` (`DataRow` per fact: template listed without the property, not listed); `GameCatalogReaderReadCountsDroppedDefaultRegionTypes` (`DataRow`: an unknown name of a template list and of a default list used by two buildings, counted once); `GameCatalogReaderReadSharesLayeredRegionList` (two buildings of one template without own list: `ReferenceEquals` of their `RegionTypes` through `Entries`, and of two using the default); `GameCatalogReaderReadResolvesFixtureEntries` adapted | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC3 | `GameCatalogReaderReadFailsForSecondCatalogPropertyDefault` (`DataRow`: `Building`, `Product`, `Session`, each in one group and in two different groups); `GameCatalogReaderReadFailsForNonIntegerSessionRegionLayer` (`DataRow`: template `abc`, default `abc`, template `99999999999`); `GameCatalogReaderReadOpensOneFileAtATime` and `ExpectedLog` adapted to the new order; `GameCatalogReaderReadIgnoresContainerDefaultsAndNestedTemplates` extended with `Building` and `Session` decoys | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |
| AC4 | `GameCatalogEntriesHoldsEntriesWithoutAmbiguousGuids`; `GameCatalogReaderSampleResolvesFixtureGuids` and `GameCatalogReaderInstallationResolvesFixtureGuids` extended; manual: the Product Manager on Windows, local build of the branch head after step 8, *Single test* of `GameCatalogReaderInstallationResolvesFixtureGuids` with `CENSIO_ANNO_INSTALL` set, before step 9 | `tests/Censio.GameData.Tests/GameCatalogTests.cs`, `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |

## Approach

- **Values.** New `GameCatalogValues`: per property `Product`, `Building`, `Session`, `Region` whether it is present
  and its one field (`AssociatedRegion`, `AssociatedRegions`, `Region`, `RegionID`); `null` = not stated.
- **properties.xml.** `ReadPropertyDefaults` generalizes today's walk: a `DefaultValues` child (relative depth 1) named
  `Building`, `Product`, `Session` or `Region` is read as a bounded part (`CountElement`), its field at relative depth 1
  taken (last one wins), its presence flag set; a second part of the same name is malformed, counted across all
  `Group/DefaultValues` elements of the file (one `GameCatalogValues` for the file, as `regionSections` today), not
  per element. Inside a template a repeated property stays last-wins, as `RegionID`. `Session/Region` is
  parsed with `GameCatalogXml.ParseInt` after trimming, an empty one gives `0`. Everything else is skipped as now.
- **templates.xml.** `ReadTemplates` returns every named defining template with its `GameCatalogValues`:
  `Properties/<Property>` at relative depth 2 sets the flag, `Properties/<Property>/<Field>` at depth 3 the value
  (`Session/Region` parsed as above). Duplicate names and nested templates as today.
- **Assets.** `ReadOptionalGuid` gives `0` for an empty element (stated "no region"). `GameCatalogAssets.Add`, after
  the inherited check and before the region-asset check, looks the asset's template up and, for each property the
  template has, fills each field the asset left `null` from the template, else from the default; `HasBuildingSection`
  is also set by the template's `Building` property. Kind and candidacy then run unchanged on the resolved facts.
- **Builder.** `ResolveRegionId` goes. `Build` caches by the source string's reference (`ReferenceEqualityComparer`:
  no lookup hashes a long value): `RegionList` runs once per source string, its dropped names count once, and its
  entries share one read-only list. Layering copies the reference, so own strings behave as today.
  `CreateEntry` makes a region entry only for a `SessionRegion` other than `0`.
- **Reader.** `Read` keeps its signature (all parameters share one type, so a reorder would compile silently); its
  passes run data sets, properties, templates, assets (with templates and defaults), English, German. Its summary
  names the order.
- **`GameCatalog.Entries`** exposes the entries the constructor kept, for the report.
- **Report (AC4, test code).** A helper opens the six files (sample: loose files; installation: `config.rda` through
  `RdaArchive.Open`/`Read`) and runs `Read` twice, the second time with `<Properties />` and `<Templates />`. It writes
  per kind the entry counts of both builds; the kind changes as counts per pair (an entry new to the catalog as from
  `none`); per kind the region-list transitions between *not stated*, *none* and a list, and lists that changed; and
  each pipe-fixture GUID whose kind or region list changed with its kinds and numbers of region types. It asserts no
  entry of the own-values build is missing. Exact wording free for the Tester, approved in step 9.
- **Fixture** (Tester, invented): `templates.xml` gains a building template with `Properties/Building/AssociatedRegions`
  and one with an empty `Properties/Building`; `SessionModerate` names a region asset; `properties.xml` gains
  `DefaultValues/Building/AssociatedRegions` and decoys under `DefaultContainerValues/Building` and a nested group;
  `assets.xml` gains a building of each template without own section or list, and a `SessionModerate` session without
  own `Session/Region`, with texts. No `Product` template and no `ResidenceBuilding7` (facts 9: they stay the
  "template not listed" variant); `Product` layering is tested inline with `ReadCatalogWithDefaults`.

## Options considered

1. Option 3 of record 0049, files read before the assets — chosen; options 1, 4 and 5 of the record lost as it says
   (`RegionID` only; every value; kinds decided after the templates).
2. Reorder `Read`'s parameters to the pass order — loses: same types, call sites would compile with swapped files.
3. The effect only on the pipe-fixture GUIDs through `Find`, without `Entries` — loses: other buildings change unseen.
4. Dedupe in `RegionList`, or dropped names per entry — lose (*Security review*, B1).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `GameCatalogValues.cs` (new) | the layered facts |
| Censio.GameData | `GameCatalogDefaults.cs` | two readers generalized |
| Censio.GameData | `GameCatalogFiles.cs`, `GameCatalogAssets.cs`, `GameCatalogAsset.cs` | resolution in the asset pass; empty `Session/Region` → `0`; docs |
| Censio.GameData | `GameCatalogBuilder.cs`, `GameCatalogReader.cs`, `GameCatalog.cs` | `Build` narrowed, pass order, `Entries` |
| Censio.GameData.Tests | `GameCatalogReaderTests.cs`, `GameCatalogTests.cs`, `Fixtures/GameCatalog/` | tests, fixture, README |

## Signatures (for the Dev's skeleton)

- `internal sealed class GameCatalogValues` (new file `src/Censio.GameData/GameCatalogValues.cs`) with
  `internal bool HasProduct { get; set; }`, `internal bool HasBuilding { get; set; }`,
  `internal bool HasSession { get; set; }`, `internal bool HasRegion { get; set; }`,
  `internal string? ProductRegions { get; set; }`, `internal string? BuildingRegions { get; set; }`,
  `internal int? SessionRegion { get; set; }`, `internal string? RegionId { get; set; }`
- `internal static GameCatalogValues ReadPropertyDefaults(Stream stream)` (`GameCatalogDefaults`); `ReadDefaultRegionId` is deleted.
- `internal static Dictionary<string, GameCatalogValues> ReadTemplates(Stream stream)` (`GameCatalogDefaults`); `ReadTemplateRegionIds` is deleted.
- `internal static GameCatalogAssets ReadAssets(Stream stream, Dictionary<string, GameCatalogValues> templates, GameCatalogValues defaults)` (`GameCatalogFiles`, changed)
- `internal GameCatalogAssets(Dictionary<string, GameCatalogValues> templates, GameCatalogValues defaults)` (new constructor)
- `internal static GameCatalog Build(HashSet<string> regionTypes, GameCatalogAssets assets, GameCatalogTexts english, GameCatalogTexts german)` (`GameCatalogBuilder`, two parameters removed)
- `public IReadOnlyCollection<GameCatalogEntry> Entries => _entries;` (`GameCatalog`, new)

Names checked against *Writing code* (PascalCase, `Id` as already used, `Has` prefix as `HasBaseAsset`). The skeleton
rewrites `GameCatalogDefaults.cs`, `GameCatalogFiles.cs`, `GameCatalogAssets.cs`, `GameCatalogBuilder.cs`,
`GameCatalogReader.cs` and `GameCatalog.cs`.

## Test files

`tests/Censio.GameData.Tests/GameCatalogReaderTests.cs`, `tests/Censio.GameData.Tests/GameCatalogTests.cs`, fixtures
under `tests/Censio.GameData.Tests/Fixtures/GameCatalog/`.

Existing call sites: none (only internal signatures change; `Read` keeps its own). Existing tests the change touches,
adapted by the **Tester** in step 5: `GameCatalogReaderReadOpensOneFileAtATime` and the helper `ExpectedLog` (used by
`...EndsWithKindOfSourceError`, `...GivesFileUnreadableWhenOpenerThrows`, `...DisposesStreamOfFailedPass`) to the
pass order; `_fixtureEntries` and `...ResolvesFixtureEntries` for the new fixture assets;
`...IgnoresContainerDefaultsAndNestedTemplates`; `...FixtureTextsAreInvented` and `...FixtureHoldsNoSampleText` for
the new fixture values. Unchanged, inputs handled alike: `...KeepsMissingRegionListUnstated` (templates not listed),
`...SkipsSessionWithEmptyRegion` (own empty value stated), the `RegionID` tests of #130.

Files no unit test can cover: none.

## Areas

- `docs/areas/game-catalog.md` — *Files and nodes*: pass order, the nodes of both files, the second-default rule;
  *Kinds*: rule 3 on the resolved `Session/Region`, rule 5 with the template's `Building` property; *Region types*:
  the layering for all four facts in one paragraph, "Only `RegionID` is layered this way" removed; *Failures*: the two
  new malformed cases, the second default counted across groups; *Region types*: a shared list counted once per
  source list, `0` never a region; *Memory*: the templates' facts held from the templates pass on, one split list per
  source string shared by its entries; *Tests with the real files*:
  the report.
- `docs/areas/game-files.md` — the *Regions* row (line 110): the answer covers goods, buildings and sessions (#133).

## Documentation updates

- Dev: the two area documents above, against the code as built.
- Tester: `docs/UNIT_TESTS.md` lines 29–36 (the two opt-in tests also write the report); `Fixtures/GameCatalog/README.md`.
- None: `docs/ARCHITECTURE.md`, `.squad/project.md`, `README.md`, `docs/USER_GUIDE.md` (nothing they state changes); no
  changelog exists.

## Architecture check

Still `config.rda` only, forward-only, one file at a time, nothing written, fail closed (record 0047). Memory adds the
facts of about 1,046 templates during the asset and text passes; split lists stay bounded by the files' content (one
list per source string). No guarantee of `.squad/project.md` is weakened.

## Security considerations

- [x] Bounds of the read unchanged: per property part and per template, counted on elements as met after
  decoding; the archive's entry bound before decompression. Derived values: a template or default value reaches many
  assets by reference; it is split once and kept as one list shared by its entries, so the kept lists and the split
  time grow with the files' content, not with entries × value length, and the dropped count with the source lists
  (at most the tokens of the six files, within `int`).
- [x] No exception text reaches a result: the new integer parse throws the existing malformed exception, mapped to
  the fixed text of `GameCatalogReader.CreateError`.

## Decision records

- `docs/decisions/0049-region-type-of-a-region-asset-from-its-template-and-the-property-defaults.md` edited in place
  (unreleased, fact 11): title, context, options and decision now cover all four facts and the pass order (option 3
  chosen); the file name is kept so no link moves. Index row updated.
- `docs/decisions/0047-…` edited in place: "a missing `RegionID`" → "a value an asset does not state".

## Security review

- B1, accepted: each source string split once, its list shared and its dropped names counted once (AC2, *Builder*,
  *Security considerations*, *Areas*, record 0049; tests `...SharesLayeredRegionList`,
  `...CountsDroppedDefaultRegionTypes`). Dedupe rejected: bounds memory, not the repeated split, and changes own lists
  of repeated names; dropped names per entry rejected: multiplies with the entries, can overflow `int`.
- N1, fixed now: `CreateEntry` requires a `SessionRegion` other than `0`; `DataRow` in `...LayersSessionRegion`.
- N2, fixed now: the second-default count runs across the file (*properties.xml*, AC3); `DataRow`s with the parts in
  two groups in `...FailsForSecondCatalogPropertyDefault`.

## Out of scope / follow-ups

- `IsWorkforce`, `Text/OasisId` and inherited assets stay as they are (record 0049, option 4; record 0047).

## Security notes

From Security's delta confirmation (round 2, `APPROVED`), verbatim:

> One point for the diff review: the shared list must be read-only in fact, not only by type. Today `RegionList` returns a `List<string>`, which a caller can cast back to `List<string>` and change, and that would alter every entry sharing it. An array or a read-only wrapper avoids that. This is an implementation detail for the Dev and the diff review, not a plan finding.
