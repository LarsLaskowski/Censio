# Game catalog

## Scope

Building the catalog of regions, goods, workforce and buildings, each GUID with its kind, its English and German name
and its region types, from the asset, data set and text files in the game's `config.rda`: the files and nodes read,
the kinds, the names, the reported cases, the copies in other archives, the bounds, the failure kinds and the privacy
rules. The Companion builds it in the background at every start; the diagnostics window and the log use it. The
player relies on the names it shows; the later web UI relies on the same catalog. The files are untrusted input.

## Behavior

### Files and nodes

- The catalog is built from four files of `maindata/config.rda` of the installation ([`game-files.md`](game-files.md)):
  `data/base/config/export/assets.xml`, `data/base/config/game/datasets.xml`, `data/base/config/gui/texts_english.xml`
  and `data/base/config/gui/texts_german.xml`. Another language needs only another text file (record
  [0023](../decisions/0023-game-names-and-icons-extracted-locally.md)). Nothing else is read for the catalog, and nothing
  is written anywhere, no cache and nothing into the installation.
- Each file is read in one forward-only pass with an XML reader that processes no document type declaration, resolves
  no entity and makes no network access; comments, processing instructions and white space between elements are
  ignored. The passes run in the order data sets, assets, English texts, German texts; each file is opened when its pass
  starts and released when the pass ends, before the next is opened (record
  [0047](../decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)).
- **Data sets:** the region types are the `Item` names of the `DataSet` whose own `Name` is `Region`, directly under
  `DataSets/Group`. A data set that merely has an item called `Region` is not it. No such data set, or more than one,
  is a malformed file.
- **Assets:** a defining asset is an `Asset` element directly under `Assets` with a `Values/Standard/GUID`; an `Asset`
  elsewhere (a reference inside an `Item`) is not a definition. The facts used, all below `Values` unless noted:
  `Template` and `BaseAssetGUID` (directly below the asset), `Text/OasisId`, `Product/IsWorkforce`,
  `Product/AssociatedRegion`, the presence of a `Building` section, `Building/AssociatedRegions`, `Session/Region` and
  `Region/RegionID`. Other nodes, `ProductionRegions` included, are not used.
- **Texts:** `TextExport/Texts/Text` holds a `LineId` (signed 64-bit) and a `Text`. Only the identifiers a catalog
  candidate uses are kept.
- GUIDs are read as signed 32-bit and text identifiers as signed 64-bit integers, after white space around the value
  is removed (the game's file writes some GUIDs on their own line). A value that is not such an integer is a
  malformed file; so is an empty `Standard/GUID` or `Text/OasisId`. An empty `Session/Region` means "no region" and
  is not an error (the game's file has such sessions).

### Kinds

Rules apply in this order to every defining asset; the first that fits decides.

1. No `Template` and a `BaseAssetGUID`: an *inherited* asset. It is left out of the catalog and counted; seen in a
   tick it is reported as unknown. Its sections are not looked at, so a `Building` section does not make it a building.
   Resolving inherited assets is out of scope. (The Product Manager's installation has 962 of them; the fixture, which
   stands for the structure only, has one.)
2. `Template` `Region`: a region asset. It is no entry itself; sessions refer to it.
3. A `Session/Region` that is not `0` and names a region asset: a **region entry**, any session template counts. It
   carries the GUID of the region asset and, where that asset states a `RegionID`, that region type. Its names are those
   of its own `Text/OasisId` (a session and its region asset normally share one). A session that names `0` or an
   asset that is no region asset falls through to the next rules and is no region entry. If two region assets have one
   GUID, the first is used.
4. `Template` `Product`: a **workforce** when `IsWorkforce` is `1`, otherwise a **good**.
5. A `Building` section: a **building**.
6. Anything else is not in the catalog.

### Region types

- A good or workforce carries the region types of its `AssociatedRegion`, a building those of its
  `AssociatedRegions`: names of the `Region` data set separated by `;`, each trimmed, empty parts ignored, in the
  order written.
- A missing element means *not stated*; an element that is present and empty (also `<AssociatedRegions />`) means
  *none*, an empty list. The two stay distinct.
- A name that the `Region` data set does not hold is dropped from the entry and counted. A `RegionID` that is not in
  the data set leaves the region entry with an empty list and is counted once per region asset, whether or not a session
  uses it; a region asset without a `RegionID` gives its region entries no region types (*not stated*; the template
  default is #130). The count is the sum of dropped names and dropped `RegionID`s over the catalog.
- No region, region type or name is written into the code: a region the code has never seen resolves like the others.

### Names and the problem of a GUID

- An entry's English and German names are the texts of its text identifier in the two files. A missing identifier, an
  identifier in neither file, a missing `LineId` or an empty text gives no name in that language.
- The catalog answers each GUID with one problem:

| Problem | When |
| ------- | ---- |
| None | the GUID has an entry with both names |
| Incomplete | the entry has one name only; the cell and the name shown are the one it has |
| Nameless | the entry has no name in either language (an asset without a text identifier included) |
| Unknown | no entry: not in the files, an inherited asset, a session without a region asset |
| Ambiguous | the GUID is defined by more than one defining asset (any asset of the file, in any kind), or its text identifier has two different texts in one language |

- A text identifier repeated with the same text, or shared by several assets, is no ambiguity.
- An ambiguous GUID is never named: it has no entry (it is not counted in the entries and a lookup finds none), and the
  catalog reports it as ambiguous even when one of its assets has a name.
- GUID `0` means "none" and is never looked up or reported.
- The entry count includes entries without names or with one name.

### Copies in other archives

- After the catalog is built from `config.rda`, the other archives below `maindata` are listed the way the survey does
  it ([`game-files.md`](game-files.md), *Installation survey*: recursive, links skipped, inaccessible folders ignored;
  `config.rda` skipped ignoring case) and opened. Each archive that lists one of the four paths (compared ignoring case)
  is reported with the archive and the path; the catalog still uses `config.rda`. An archive that cannot be opened is
  counted as unchecked. The Product Manager's installation has no copies (statement of 2026-11-09, unverified for other
  installations; the load order of the archives stays unknown).

### Use by the Companion

- The Companion checks the GUIDs of ticks closed after the catalog is ready, and the latest tick at that moment, unless
  no tick source (connection or replay) has started yet; ticks closed earlier are not checked
  ([`companion.md`](companion.md), *Log*).

### Failures

- A catalog is built completely or not at all (record
  [0033](../decisions/0033-pipe-reader-strict-from-the-reference-source.md), fails closed): no partial catalog is used.
  The result holds an error with a kind and a fixed text per kind, never an exception text or any content:

| Kind | When |
| ---- | ---- |
| Archive unreadable | `config.rda` is missing or cannot be opened, or the listing of the installation fails |
| File missing | the archive lists none of a required path |
| File unreadable | an entry cannot be read (the per-entry bound included), the stream fails, or opening the source throws |
| Malformed file | the XML is not well-formed, has a document type declaration or the wrong root element, a value is not an integer of its type, the `Region` data set is not exactly one, or a bound of *Limits* is exceeded |

- A source that reports an error kind ends the read with that same kind and no later file is opened. Exceptions of
  the XML reader and of the streams are standard exceptions mapped to a kind at the boundary of the reader; only an
  out-of-memory condition is not caught.

## Limits and errors

- **Per file:** the archive's per-entry bound ([`game-files.md`](game-files.md), *Limits*, 512 MiB by default),
  checked before the entry is decompressed; the entry is read from memory through a read-only stream, not copied.
- **Per asset:** at most 256 levels of nesting and at most 65,536 elements, counted on the elements as the reader meets
  them, before each one. The same two bounds apply to each `Text` element of the text files and to each `DataSet`; and
  the structure above the assets, texts and data sets may be at most 256 levels deep. A file above any of them is a
  malformed file. (On the Product Manager's four files the deepest asset has 46 levels and the largest 3,740 elements.)
- **Memory:** at one time the catalog build holds one file's entry content, the open `config.rda` with its entry
  list, the candidate assets (the few hundred to some thousand goods, buildings, workforce and sessions), the texts they
  use and the facts of the asset being read; while the other archives are listed, one archive's entry list on top. A
  file is never held as a document tree.
- **Time:** reading the four files of the Product Manager's installation took 1.3 s with a peak working set of 108 MiB
  in the test process; the whole build including the archive listing is measured by the opt-in installation test
  (below) and is not stated here.
- **Network:** when the installation folder is a UNC path from the settings file ([`game-installation.md`](game-installation.md),
  *Folders from outside*), `config.rda` is read and every archive below `maindata` is opened and listed over the
  network (SMB) at every start.

## Tests with the real files

- `CENSIO_CATALOG_SAMPLE` names a folder with the four loose files; `CENSIO_ANNO_INSTALL` names an installation. Unset,
  the opt-in tests are inconclusive. They check every non-zero GUID of the pipe fixture `long-session.jsonl` against the
  catalog and write counts, unresolved GUIDs, elapsed time and peak working set, never a name ([`UNIT_TESTS.md`](../UNIT_TESTS.md)).

## Privacy

- Names and texts stay on the machine of the user: none enters the repository, a fixture, a release, a log or test
  output (records [0023](../decisions/0023-game-names-and-icons-extracted-locally.md) and
  [0029](../decisions/0029-anonymised-test-fixtures-only.md)). The test fixture has the structure of the game's files
  and invented GUIDs, identifiers, names and paths; a test asserts that none of its GUIDs occurs in the pipe fixture.
  Logs and reports hold GUIDs, kinds, archive paths and counts only.

## Related decisions

- [`decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md`](../decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)
  — forward-only reading, kinds and regions from the data, fail closed.
- [`decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md`](../decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md)
  — names in the diagnostics window, unresolved GUIDs logged once.
- [`decisions/0023-game-names-and-icons-extracted-locally.md`](../decisions/0023-game-names-and-icons-extracted-locally.md)
  — names are read from the player's installation and never shipped.

## Not here

- The archives, their bounds and failure kinds: [`game-files.md`](game-files.md); finding the installation:
  [`game-installation.md`](game-installation.md).
- The tray line, the diagnostics cells, the check of tick GUIDs and the log lines: [`companion.md`](companion.md).
- Icons, the cache, the catalog format and the upload (#24 to #26, #42).
