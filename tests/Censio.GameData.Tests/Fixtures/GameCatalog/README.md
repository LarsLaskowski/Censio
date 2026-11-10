# Game catalog fixture

Six small XML files in the structure of the Anno 117 files `assets.xml`, `datasets.xml`, `texts_english.xml`,
`texts_german.xml`, `properties.xml` and `templates.xml` (their paths in `config.rda` are the constants of
`GameCatalogReader`), read by `GameCatalogReaderTests`. The structure (roots, nesting, element names) follows the
game's files; the first four have no XML declaration, `properties.xml` and `templates.xml` start with a UTF-8 BOM and an
XML declaration as the game's do. Everything else is invented. Every GUID (around 2,000,000,000), text identifier (around 7.1e18), region type name
(`Zorlan`, `Yultic`, `Xanthi`, `Qwertz`, `Vexan`), internal name, text and icon path is invented (`Good <n>`,
`Ware <n>` style) and generated from nothing of the game's files. No GUID occurs in a pipe fixture; no name or text of
the game's files is in the repository ([`game-files.md`](../../../../docs/areas/game-files.md), record 0023). The files are
written with CRLF; no test depends on the line ending.

The real GUIDs stay in the opt-in tests (`CENSIO_CATALOG_SAMPLE`, `CENSIO_ANNO_INSTALL`), which read them from the pipe
fixture `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl`, linked into this project as `Fixtures/Pipe/`.

## Variants

Offsets are added to 2,000,000,000.

| Asset (offset) | Variant |
| -------------- | ------- |
| 101, 102, 103, 104 | region assets (`Template` `Region`): with `RegionID` `Zorlan`; without; with an unknown `RegionID`; with `Yultic` |
| 201, 202 | sessions of `SessionModerate` and `SessionModerateRandom` naming region assets 101 and 104, sharing their text identifiers |
| 207, 208 | a session of `SessionModerate` without a `Session` section (its region is the template's, region asset 101); a session of `FixtureSessionBare` without one (its region is the default `0`, so no entry) |
| 203, 204 | sessions naming the region asset without and with the unknown `RegionID` |
| 205, 206 | a session naming region `0`; a session naming a good (no region asset) |
| 301, 302, 303, 304 | goods with one region type (and `IsWorkforce` 0), with three, with none stated (only `ProductionRegions`), with an unknown type token `Vexan` |
| 305, 308 | a good whose text identifier is missing from the German file; one whose English text is empty |
| 306, 307 | two goods sharing one text identifier |
| 309 | a good whose text identifier is repeated with the same text |
| 311, 312, 313 | goods without `Text`; with both texts empty; with a text identifier in neither file |
| -401 | a good with a negative GUID |
| 351 | a workforce (`IsWorkforce` 1) |
| 501, 502, 503 | buildings with a list, without one, with `<AssociatedRegions />` (template `ResidenceBuilding7`, not listed in `templates.xml`) |
| 504, 505, 506 | buildings without a `Building` section whose template lists a `Building` property with a list (`FixtureBuildingListed`) and with an empty one (`FixtureBuildingBare`, so the default list), and one of `FixtureBuildingListed` with a list of its own |
| 601 | an asset with `BaseAssetGUID`, no `Template`, a `Building` section and a text identifier (inherited) |
| 701, 702 | assets of another template with a nested `Item/Asset` reference to a good and a building |

`properties.xml` holds a `Default` group with `Standard` and `Text` values, a group `Outer` with a nested group `Session`
whose `DefaultValues/Region/RegionID` is `Xanthi` (between sibling properties, its `Name` after the values), and a group
without values. The same `DefaultValues` also state the `Session` default `Session/Region` `0`, which the session 208
takes. Decoys that must not be read: a `DefaultContainerValues/Region/RegionID` `Zorlan` and a
`Group/DefaultValues/Region/RegionID` `Zorlan` nested inside the `BenchmarkScene` property.
A group `Construction` states `DefaultValues/Building/AssociatedRegions` (`Xanthi;Zorlan`); decoys that must not be read
are also a `Building` and a `Session` under the `DefaultContainerValues` and under the nested group of `BenchmarkScene`.
`templates.xml` holds nested groups with the template `Region` (in the group `Session`, a `Properties/Region` section
without `RegionID`), `SessionModerate` (a `Session/Region` property naming region asset 101, no `Region` property),
`FixtureBuildingListed` (`Building/AssociatedRegions` `Yultic;Zorlan`), `FixtureBuildingBare` (an empty `Building`),
`FixtureSessionBare` (an empty `Session`), a template with a
`Description`, and the template `Holder` with a nested `Template` named `Region` whose `RegionID` `Zorlan`, `Building` and `Session` are decoys. No template
`Product` and none for `ResidenceBuilding7` is listed: those stay the "template not listed" variant.
The region type names stay invented. Region asset 102 resolves to the default `Xanthi`; 101, 103 and 104 keep their own.

`datasets.xml` holds the data set `FixtureKinds` (with an item named `Region` and the item `Vexan`) before the data set
`Region` (`Zorlan`, `Yultic`, `Xanthi`). `ProductionRegions` of the goods name `Xanthi`.

## Facts the tests assert

The catalog of the fixture holds 25 entries: the sessions 201 to 204 and 207 (regions), the goods 301 to 309, 311 to 313
and -401, the workforce 351 and the buildings 501 to 506. The sessions 205, 206 and 208, the region assets, the inherited asset
601 and the other assets are no entries; one asset is counted as inherited; two region types are dropped and counted
(`Vexan` of 304 and the `RegionID` of 103). The problems: 305 and 308 are incomplete, 311 to 313 nameless, all others
none. The session 203 carries `Xanthi` from the property default, the session 207 region asset 101 from its template, the building 504 `Yultic;Zorlan` from its template, 505 `Xanthi;Zorlan` from the property default and 506 its own `Xanthi`. The region types of every entry are asserted by the `GameCatalogReaderRead...` tests of `GameCatalogReaderTests`.
