# Game catalog

## Scope

Building the catalog of regions, goods, workforce and buildings, each GUID with its kind, its English and German name
and its region types, from the asset, data set, text, template and property default files in the game's `config.rda`, and the icons of its goods and regions from the game's UI
archives: the files and nodes read, the kinds, the names, the reported cases, the copies in other archives, the icons
(how they are found, converted and bounded), the bounds, the failure kinds and the privacy rules. The Companion builds
the catalog in the background at every start and the icons after it; the diagnostics window and the log use both. The
player relies on the names and icons it shows; the later web UI relies on the same catalog. The files are untrusted
input.

## Behavior

### Files and nodes

- The catalog is built from six files of `maindata/config.rda` of the installation ([`game-files.md`](game-files.md)):
  `data/base/config/export/assets.xml`, `data/base/config/game/datasets.xml`, `data/base/config/gui/texts_english.xml`,
  `data/base/config/gui/texts_german.xml`, `data/base/config/export/properties.xml` and
  `data/base/config/export/templates.xml`. Another language needs only another text file (record
  [0023](../decisions/0023-game-names-and-icons-extracted-locally.md)). Nothing else is read for the catalog, and nothing
  is written anywhere, no cache and nothing into the installation.
- Each file is read in one forward-only pass with an XML reader that processes no document type declaration, resolves
  no entity and makes no network access; comments, processing instructions and white space between elements are
  ignored. The passes run in the order data sets, properties, templates, assets, English texts, German texts; each file is opened when its pass
  starts and released when the pass ends, before the next is opened (record
  [0047](../decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)).
- **Data sets:** the region types are the `Item` names of the `DataSet` whose own `Name` is `Region`, directly under
  `DataSets/Group`. A data set that merely has an item called `Region` is not it. No such data set, or more than one,
  is a malformed file.
- **Assets:** a defining asset is an `Asset` element directly under `Assets` with a `Values/Standard/GUID`; an `Asset`
  elsewhere (a reference inside an `Item`) is not a definition. The facts used, all below `Values` unless noted:
  `Template` and `BaseAssetGUID` (directly below the asset), `Text/OasisId`, `Product/IsWorkforce`,
  `Product/AssociatedRegion`, the presence of a `Building` section, `Building/AssociatedRegions`, `Session/Region`,
  `Region/RegionID` and `Standard/IconFilename` (trimmed; empty means none; a second one in one asset is resolved as
  the last one; read for every asset, region assets included, and used for the icons, *Icons*). Other nodes, `ProductionRegions` included, are not used. The four values and the presence of the
  `Building` section are layered with the template and the property defaults (*Region types*).
- **Texts:** `TextExport/Texts/Text` holds a `LineId` (signed 64-bit) and a `Text`. Only the identifiers a catalog
  candidate uses are kept.
- **Properties** (root `Properties`): the default of a property is its field in the child of a `DefaultValues`
  element directly under a `Group` (groups nest through `Groups/Group`) that is named after the property:
  `Product/AssociatedRegion`, `Building/AssociatedRegions`, `Session/Region` and `Region/RegionID`. Defaults belong to
  the property, not to the group, and the group's `Name` is not used. `DefaultContainerValues` and a `DefaultValues`
  nested deeper inside another property's part are not read. A second `DefaultValues` part `Building`, `Product`,
  `Session` or `Region` anywhere in the file, in the same or in another group, is a malformed file; a default
  without its field is *not stated*, but the property counts as present.
- **Templates** (root `Templates`): a defining template is a `Template` element directly under a `Group`; a `Template`
  nested inside its properties is part of it and no definition. For each defining template with a `Name` the file is
  checked for the sections `Properties/Product`, `Properties/Building`, `Properties/Session` and `Properties/Region`
  and their fields as above; a second field in one section is resolved as the last one. A second defining template of
  the same name is a malformed file, whether or not it has such a property; a template without `Name` is ignored.
  Because the templates and defaults are read before the assets, an asset's kind and region lists are decided from
  the resolved values in the asset pass.
- GUIDs are read as signed 32-bit and text identifiers as signed 64-bit integers, after white space around the value
  is removed (the game's file writes some GUIDs on their own line). A value that is not such an integer is a
  malformed file; so is an empty `Standard/GUID` or `Text/OasisId`. An empty `Session/Region`, in an asset, a template
  or a default, means "no region" (it counts as `0`) and is not an error (the game's file has such sessions); a
  `Session/Region` of a template or default that is neither empty nor such an integer is a malformed file.

### Kinds

Rules apply in this order to every defining asset; the first that fits decides.

1. No `Template` and a `BaseAssetGUID`: an *inherited* asset. It is left out of the catalog and counted; seen in a
   tick it is reported as unknown. Its sections are not looked at, so a `Building` section does not make it a building.
   Resolving inherited assets is out of scope. (The Product Manager's installation has 962 of them; the fixture, which
   stands for the structure only, has one.)
2. `Template` `Region`: a region asset. It is no entry itself; sessions refer to it.
3. A resolved `Session/Region` (*Region types*) that is not `0` and names a region asset: a **region entry**, any
   session template counts. It carries the GUID of the region asset and, where a `RegionID` resolves for that asset
   (*Region types*), that region type. Its names are those
   of its own `Text/OasisId` (a session and its region asset normally share one). A session that names `0` or an
   asset that is no region asset falls through to the next rules and is no region entry. If two region assets have one
   GUID, the first is used.
4. `Template` `Product`: a **workforce** when `IsWorkforce` is `1`, otherwise a **good**.
5. A `Building` section of the asset, or a `Building` property of its template in `templates.xml`: a **building**.
6. Anything else is not in the catalog.

### Region types

- A good or workforce carries the region types of its `AssociatedRegion`, a building those of its
  `AssociatedRegions`: names of the `Region` data set separated by `;`, each trimmed, empty parts ignored, in the
  order written. The lists are read-only.
- A missing element means *not stated*; an element that is present and empty (also `<AssociatedRegions />`) means
  *none*, an empty list. The two stay distinct.
- A name that the `Region` data set does not hold is dropped from the entry and counted. A `RegionID` that is not in
  the data set leaves the region entry with an empty list and is counted once per region asset, whether or not a session
  uses it. The count is the sum of dropped names and dropped `RegionID`s over the catalog.
- **Layering** (records [0047](../decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)
  and [0049](../decisions/0049-region-type-of-a-region-asset-from-its-template-and-the-property-defaults.md)):
  `Product/AssociatedRegion` (goods, workforce), `Building/AssociatedRegions` (buildings), `Session/Region` and
  `Region/RegionID` are each the asset's own value; without one, when the asset's template has that property in
  `templates.xml`, the template's value; without one, the value of the property's `DefaultValues` in
  `properties.xml`. A template that is not listed, or that lacks the property, gives only the asset's own value, never
  the default. A present empty element is stated at every level: an empty list means *none*, an empty `RegionID` is
  stated, an empty `Session/Region` means no region. When no level states a value it is *not stated*. A second field
  in one section is resolved as the last one.
- A template's `Building` property also makes an asset a building (*Kinds*, rule 5); a `Product` template stays a good
  or workforce whatever its properties. The resolved `Session/Region` decides the region entry, and `0` never makes
  one, also against a region asset of GUID `0`.
- **Region type of a region asset:** its layered `RegionID`. When none is stated the region entries carry no region
  types (*not stated*).
- A template's or a default's list is split and checked once, and the entries that resolve to it share one read-only
  list; its dropped names are counted once per source list, as a dropped `RegionID` once per region asset.
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
  `config.rda` skipped ignoring case) and opened. Each archive that lists one of the six paths (compared ignoring case)
  is reported with the archive and the path; the catalog still uses `config.rda`. An archive that cannot be opened is
  counted as unchecked. The Product Manager's installation has no copies (statement of 2026-11-09, unverified for other
  installations; the load order of the archives stays unknown).

### Icons

Record [0050](../decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md) (where the
icons are and which copy wins) and record [0051](../decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md) (the
conversion) hold the reasoning.

- **Entries:** only goods and region entries get an icon; workforce, buildings and every GUID that is no good or region
  entry of the catalog (unknown or ambiguous GUIDs included) have none, and no problem is counted for them. An entry's
  icon file name is the `IconFilename` of its asset; a region entry takes the one of its session and, if the session has
  none, the one of its region asset. Several entries that name one file share one icon.
- **Names first:** the icons are built after the catalog is published. No icon problem changes a name, the catalog or
  its status; a catalog that fails builds no icons.
- **Path rule:** the files the assets name are `data/ui/fhd/<rest>.png`, but the archives hold `data/ui/4k/<rest>_0.dds`.
  A value is trimmed and must start with `data/ui/fhd/` and end with `.png` (both ignoring case), with a non-empty
  `<rest>` and at most 1,024 characters; the entry sought is `data/ui/4k/<rest>_0.dds`, compared with the archive's
  paths ignoring case, and read under the path the archive lists. A named value that gives no path (another folder, another
  extension, too long) counts as *missing*; the path is only compared with entry paths, never used as a file path.
- **Archives and order:** only `maindata/ui.rda` (directly in `maindata`, ignoring case) and the patch archives
  `zz_patchfiles_<n>.rda` (ignoring case, `<n>` ASCII digits only and at most 4,294,967,295, found by the listing of the
  survey, subfolders of `maindata` included) are read for icons. Their order is `ui.rda`, then the patch archives by
  `<n>` as a number (9 before 10, `007` is 7); an icon is taken from the archive latest in that order that lists its path, and an older copy is never used
  instead (record 0050). That a higher `<n>` is newer is the Product Manager's statement for game version 3.0.1733841 (not
  verified); the opt-in test below reports the paths that several archives hold.
- **Formats:** the entry is a DDS file with the extended (`DX10`) header, one 2D texture, one array element, no cube
  map, in BC7 (UNORM or sRGB), 1 to 2,048 pixels per side, and exactly as long as its header and the levels the header
  states need (a chain of one level is allowed). Only the largest level is used: all eight BC7 modes are decoded, a
  block of the reserved mode gives transparent black pixels, a size that is no multiple of four is cropped, and the sRGB
  and UNORM forms give the same colour values (the values are not converted). The result is a PNG of 8 bits per channel
  with alpha that holds the image data only (no colour-space or other chunk). Any other header, size or length makes
  the icon *unreadable*.
- **Reasons** (per good or region entry without an icon): *not named* (the asset has no `IconFilename`), *missing* (the
  value gives no path, or no icon archive lists it) and *unreadable* (the archive that wins lists the path more than
  once, the entry cannot be read or is above its bound, or the DDS file is refused). Each reason is counted and logged
  as a number of entries ([`companion.md`](companion.md), *Log*), with the goods and regions that have an icon.
- **Failure kinds** (the icons are then withheld as a whole; the names are not touched). The result holds the kind and
  the relative path of the archive concerned, never an exception text or content:

| Kind | When |
| ---- | ---- |
| Archive missing | `maindata/ui.rda` does not exist (also when `maindata` itself does not) |
| Archive unreadable | an icon archive cannot be opened, or the listing of `maindata` or any other step fails (`maindata` named then) |
| Duplicate archive number | two patch archives have the same number (`zz_patchfiles_300.rda` and `zz_patchfiles_0300.rda`); the second is named |
| Icon set too large | the PNG files kept would together take more than 256 MiB; the archive being read is named |

- **Memory only:** the icons are held in memory with the catalog, as PNG data, until the next build or the end of the
  Companion. Nothing is written: no file in the installation, no cache (the cache is #25), no copy on the disk; nothing
  is sent. The PNG data of an icon enters no log, repository, fixture or release.

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
| Malformed file | the XML is not well-formed, has a document type declaration or the wrong root element, a value is not an integer of its type (a `Session/Region` of a template or default included), the `Region` data set is not exactly one, a second `Building`, `Product`, `Session` or `Region` default anywhere in `properties.xml` or a second defining template of one name, or a bound of *Limits* is exceeded |

- A source that reports an error kind ends the read with that same kind and no later file is opened. Exceptions of
  the XML reader and of the streams are standard exceptions mapped to a kind at the boundary of the reader; only an
  out-of-memory condition is not caught.

## Limits and errors

- **Per file:** the archive's per-entry bound ([`game-files.md`](game-files.md), *Limits*, 512 MiB by default),
  checked before the entry is decompressed; the entry is read from memory through a read-only stream, not copied.
- **Per asset:** at most 256 levels of nesting and at most 65,536 elements, counted on the elements as the reader meets
  them, before each one. The same two bounds apply to each `Text` element of the text files and to each `DataSet`; and the structure above the assets, texts, data sets, property parts and defining templates may be at most 256 levels deep. A file above any of them is a
  malformed file. The same two bounds apply to each part of `properties.xml` (a child of a `DefaultValues` or
  `DefaultContainerValues` element) and to each defining template of `templates.xml`. (On the four files of the Product Manager's installation measured for #23 the deepest asset has 46 levels and the largest 3,740 elements; in his `properties.xml` and `templates.xml` of game version 3.0.1733841 the largest property has 588 elements and 13 levels, the largest template 1,352 elements and 16 levels.)
- **Icons:** an icon entry above 8 MiB (stored or decompressed) is not read and its icon is *unreadable*; the check is made
  before the entry is allocated. The header is checked before any pixel is decoded: at most 2,048 by 2,048 pixels, so a
  decoded image takes at most 16 MiB. The PNG files kept take at most 256 MiB together, an icon that several entries share counted once; at
  exactly 256 MiB the icons are kept, above it the step fails with *icon set too large* and no icon is kept. The path of
  an icon file name is at most 1,024 characters.
- **Memory:** at one time the catalog build holds one file's entry content, the open `config.rda` with its entry
  list, the facts of every defining template (about a thousand, held from the templates pass until the build ends), the
  candidate assets (the few hundred to some thousand goods, buildings, workforce and sessions), the texts they use and
  the facts of the asset being read; each distinct source list of region types is split once and kept once, shared by
  the entries that use it; while the other archives are listed, one archive's entry list on top. A
  file is never held as a document tree. Building the icons holds, on top, one icon archive's entry list, the entry being
  read and decoded, and the PNG files kept (at most 256 MiB, *Icons*) for the whole run.
- **Time:** reading the four files of #23 of the Product Manager's installation took 1.3 s with a peak working set of 108 MiB
  in the test process; the whole build including the archive listing is measured by the opt-in installation test
  (below) and is not stated here.
- **Network:** when the installation folder is a UNC path from the settings file ([`game-installation.md`](game-installation.md),
  *Folders from outside*), `config.rda` is read and every archive below `maindata` is opened and listed over the
  network (SMB) at every start, and the icon archives (`ui.rda` and the patch archives) are listed a second time and
  the icon entries read over it, until the cache of #25.

## Tests with the real files

- `CENSIO_CATALOG_SAMPLE` names a folder with the six loose files; `CENSIO_ANNO_INSTALL` names an installation. Unset,
  the opt-in tests are inconclusive. They check every non-zero GUID of the pipe fixture `long-session.jsonl` against the
  catalog and write counts, unresolved GUIDs, elapsed time and peak working set, never a name ([`UNIT_TESTS.md`](../UNIT_TESTS.md)).
  They also build the catalog a second time with empty `properties.xml` and `templates.xml` (the assets' own values)
  and report the difference as counts and GUIDs: the entries per kind of both builds, the kind changes, the
  transitions of the region lists and the pipe-fixture GUIDs whose kind or region list changed. They fail when an
  entry of that build is missing from the layered one.
- The icon test (`CENSIO_ANNO_INSTALL`) loads the icons of that installation, fails when a good of the pipe fixture
  that the catalog names has no icon or a region entry that names a path of the rule is unreadable, and writes counts,
  GUIDs, the problem of each GUID without an icon, the size and PNG bytes of the largest icon and of each region icon,
  the number of icon paths that several archives hold and, of them, those whose entries differ in size, the elapsed time
  and the peak working set, never a name or an entry path.

## Privacy

- Names and texts stay on the machine of the user: none enters the repository, a fixture, a release, a log or test
  output (records [0023](../decisions/0023-game-names-and-icons-extracted-locally.md) and
  [0029](../decisions/0029-anonymised-test-fixtures-only.md)). The test fixture has the structure of the game's files
  and invented GUIDs, identifiers, names and paths; a test asserts that none of its GUIDs occurs in the pipe fixture.
  Logs and reports hold GUIDs, kinds, archive paths and counts only. The same holds for the icons: no icon file, PNG
  or image of the game enters the repository, a fixture, a release or a log, and the tests build their DDS files
  from invented pixels.

## Related decisions

- [`decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md`](../decisions/0047-game-catalog-read-forward-only-from-config-rda-kinds-and-regions-from-the-data.md)
  — forward-only reading, kinds and regions from the data, fail closed.
- [`decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md`](../decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md)
  — names in the diagnostics window, unresolved GUIDs logged once.
- [`decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md`](../decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md)
  — the path rule, the archives and their order, withholding every icon on an archive problem.
- [`decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md`](../decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md)
  — BC7 decoded and PNG written by own code.
- [`decisions/0023-game-names-and-icons-extracted-locally.md`](../decisions/0023-game-names-and-icons-extracted-locally.md)
  — names are read from the player's installation and never shipped.

## Not here

- The archives, their bounds and failure kinds: [`game-files.md`](game-files.md); finding the installation:
  [`game-installation.md`](game-installation.md).
- The tray line, the diagnostics cells and icons, the check of tick GUIDs and the log lines: [`companion.md`](companion.md).
- The icon cache, the catalog format and the upload (#25, #26, #42).
