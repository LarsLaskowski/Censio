# Game catalog fixture

Four small XML files in the structure of the Anno 117 files `assets.xml`, `datasets.xml`, `texts_english.xml` and
`texts_german.xml` (their paths in `config.rda` are the constants of `GameCatalogReader`), read by
`GameCatalogReaderTests`. The structure (roots, nesting, element names, no XML declaration) follows the game's files;
everything else is invented. Every GUID (around 2,000,000,000), text identifier (around 7.1e18), region type name
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
| 203, 204 | sessions naming the region asset without and with the unknown `RegionID` |
| 205, 206 | a session naming region `0`; a session naming a good (no region asset) |
| 301, 302, 303, 304 | goods with one region type (and `IsWorkforce` 0), with three, with none stated (only `ProductionRegions`), with an unknown type token `Vexan` |
| 305, 308 | a good whose text identifier is missing from the German file; one whose English text is empty |
| 306, 307 | two goods sharing one text identifier |
| 309 | a good whose text identifier is repeated with the same text |
| 311, 312, 313 | goods without `Text`; with both texts empty; with a text identifier in neither file |
| -401 | a good with a negative GUID |
| 351 | a workforce (`IsWorkforce` 1) |
| 501, 502, 503 | buildings with a list, without one, with `<AssociatedRegions />` |
| 601 | an asset with `BaseAssetGUID`, no `Template`, a `Building` section and a text identifier (inherited) |
| 701, 702 | assets of another template with a nested `Item/Asset` reference to a good and a building |

`datasets.xml` holds the data set `FixtureKinds` (with an item named `Region` and the item `Vexan`) before the data set
`Region` (`Zorlan`, `Yultic`, `Xanthi`). `ProductionRegions` of the goods name `Xanthi`.

## Facts the tests assert

The catalog of the fixture holds 21 entries: the sessions 201 to 204 (regions), the goods 301 to 309, 311 to 313 and
-401, the workforce 351 and the buildings 501 to 503. The sessions 205 and 206, the region assets, the inherited asset
601 and the other assets are no entries; one asset is counted as inherited; two region types are dropped and counted
(`Vexan` of 304 and the `RegionID` of 103). The problems: 305 and 308 are incomplete, 311 to 313 nameless, all others
none. The region types of every entry are asserted by the `GameCatalogReaderRead...` tests of `GameCatalogReaderTests`.
