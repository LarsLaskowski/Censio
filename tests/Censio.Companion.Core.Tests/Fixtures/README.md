# Fixtures of the Companion tests

Each subfolder holds the fixtures of one area and, where it has its own source files, a `README.md` that describes
them (`GameInstallation/`).

## GameCatalog (linked)

The six XML files `Fixtures/GameCatalog/*.xml` in the test output are not stored here: the project file links them from
`tests/Censio.GameData.Tests/Fixtures/GameCatalog/` (a `None` item with `Link`), so there is one copy of them. There is
no `Fixtures/GameCatalog` source folder in this project. The files, their invented values and the facts the tests assert
(25 catalog entries, the variants of names, region types and GUIDs) are described in
[`tests/Censio.GameData.Tests/Fixtures/GameCatalog/README.md`](../../Censio.GameData.Tests/Fixtures/GameCatalog/README.md).
`GameCatalogDocumentConverterTests` reads them to convert the catalog into the catalog document and to write and read it
back.
