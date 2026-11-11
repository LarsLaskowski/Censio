# Game catalog: icons

## Scope

The icons of the goods and regions of the game catalog ([`game-catalog.md`](game-catalog.md)), found in the game's UI
archives and converted to PNG: which entries get one, how the archive entry is found, the archives and their order, the
formats read, the reasons for a missing icon, the failure kinds, the bounds, the test with the real files and the privacy
rules. The Companion builds the icons after the names, in the background, unless the cache supplies them
([`game-catalog-cache.md`](game-catalog-cache.md)); the diagnostics window and the log use them. The player relies on
the icons it sees; the later web UI relies on the same icons. The archives are untrusted input.

## Behavior

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

- **Kept and sent nowhere:** the icons are held in memory with the catalog, as PNG data, until the next build or the end of
  the Companion. They are written only to the cache file ([`game-catalog-cache.md`](game-catalog-cache.md)) in the player's own local app data, after a
  complete extraction; nothing is written into the installation, and nothing is sent. The PNG data of an icon enters no log,
  repository, fixture or release.

## Limits and errors

- **Icons:** an icon entry above 8 MiB (stored or decompressed) is not read and its icon is *unreadable*; the check is made
  before the entry is allocated. The header is checked before any pixel is decoded: at most 2,048 by 2,048 pixels, so a
  decoded image takes at most 16 MiB. The PNG files kept take at most 256 MiB together, an icon that several entries share counted once; at
  exactly 256 MiB the icons are kept, above it the step fails with *icon set too large* and no icon is kept. The path of
  an icon file name is at most 1,024 characters.
- **Memory:** building the icons holds, on top of the catalog build ([`game-catalog.md`](game-catalog.md), *Limits and
  errors*), one icon archive's entry list, the entry being read and decoded, and the PNG files kept (at most 256 MiB,
  *Icons*) for the whole run.

## Tests with the real files

- The icon test (`CENSIO_ANNO_INSTALL`) loads the icons of that installation, fails when a good of the pipe fixture
  that the catalog names has no icon or a region entry that names a path of the rule is unreadable, and writes counts,
  GUIDs, the problem of each GUID without an icon, the size and PNG bytes of the largest icon and of each region icon,
  the number of icon paths that several archives hold and, of them, those whose entries differ in size, the elapsed time
  and the peak working set, never a name or an entry path.

## Privacy

- No icon file, PNG or image of the game enters the repository, a fixture, a release, a log or test output (record
  [0023](../decisions/0023-game-names-and-icons-extracted-locally.md)); the tests build their DDS files from invented
  pixels. Logs and reports hold GUIDs, kinds, archive paths and counts only.

## Related decisions

- [`decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md`](../decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md)
  — the path rule, the archives and their order, withholding every icon on an archive problem.
- [`decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md`](../decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md)
  — BC7 decoded and PNG written by own code.
- [`decisions/0023-game-names-and-icons-extracted-locally.md`](../decisions/0023-game-names-and-icons-extracted-locally.md)
  — names are read from the player's installation and never shipped.

## Not here

- The archives, their bounds and failure kinds: [`game-files.md`](game-files.md); finding the installation:
  [`game-installation.md`](game-installation.md).
- The catalog the icons belong to (kinds, names, the `IconFilename` read from the assets): [`game-catalog.md`](game-catalog.md);
  the cache that holds the icons across starts: [`game-catalog-cache.md`](game-catalog-cache.md).
- The diagnostics cells and icons and the log lines: [`companion.md`](companion.md).
