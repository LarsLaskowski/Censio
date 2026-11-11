# Game catalog: cache

## Scope

The cache of the game catalog ([`game-catalog.md`](game-catalog.md)) and its icons
([`game-catalog-icons.md`](game-catalog-icons.md)): the key and the identity, where the file lies, the check at start and
at each pipe connection, what is cached, the file layout, the bounds, the validation, the writes and the pruning, and the
privacy of the file. The Companion relies on it so that a start does not read the archives again; the cache file is
untrusted input when it is read.

## Behavior

### Cache

Records [0052](../decisions/0052-catalog-cache-keyed-by-file-properties-and-companion-identity.md) (the key and the
trigger) and [0053](../decisions/0053-catalog-cache-one-binary-file-validated-whole-before-use.md) (the file) hold the
reasoning. The cache holds the catalog and the icons of the last complete extraction, so that a start does not read the
archives again.

- **Terms:** the *game state* is the installation folder, the build (*unknown* equals *unknown*) and, of every `.rda`
  file below `maindata` (subfolders included, links not followed), its path relative to `maindata`, its size and its
  modification time. The *Companion identity* is the module version ids of the assemblies that extract and write the
  cache (`Censio.GameData`, `Censio.Companion.Core`), which change with their code even when the version number stays the same, and with the version number too. A *key* is
  the identity plus the game state; folder and paths are compared ignoring case, sizes and times exactly.
- **Where:** one file, `catalog.bin`, in `%LOCALAPPDATA%\Censio\Cache` ([`companion.md`](companion.md), *Folders*): one
  cache per Windows account, for the installation extracted last. A folder that is no full path is never read or written.
- **Start:** after the game search the Companion reads the key (`Catalog: checking`). A cache of the same identity and
  game state is loaded and published, names, then icons, without reading any archive's content; the other catalog and icon
  lines are not written. Otherwise the reason is logged (`missing`, `game changed`, `Companion changed`; when both differ,
  `Companion changed`) and the catalog and icons are extracted. A key that cannot be read (the build file or
  the listing of `maindata`, an archive's properties, a missing program file) counts as `game changed`. A cache that fails
  a validation is not used in part, is logged as a warning (`invalid`) and replaced by the extraction.
- **Later checks:** each new connection to the game's pipe (state `Connected`), once the start has ended and a game was found, runs a
  background check: it reads the build again ([`game-installation.md`](game-installation.md), *Build*) and the key, and
  compares the game state with that of the last extraction or cache load, whatever its outcome (held in memory). The same
  state does nothing: no status, no log line, no new extraction, also after a failed extraction. A different state extracts
  again (the cache is not consulted) while the previous names and icons stay; the new names replace them when
  published, the icons go until the new ones are built. A key that cannot be read keeps everything and logs the warning `Catalog check failed; the current catalog is kept`. A connection while a start or a check
  runs starts no second check. A replay, an invalid command line or a game started without `/pipe` has no connection: only the start checks.
  An archive rewritten with the same name, size and modification time goes undetected; deleting the cache folder extracts it.
- **What is cached:** only a complete result, catalog and icons built. A failed catalog, failed icons, an unreadable key and a failed write
  leave the cache as it was; an old cache is never used for another state. The key stored is the one read before the
  extraction, so a change during it is found at the next check. Without a game the cache is neither used nor deleted.
- **Layout** (little-endian; strings are a 4-byte length and UTF-8; a nullable value has a presence byte): magic
  `CENSIOCC`, format version 1, the identity, the key (folder, build, archives), the catalog section (entries with
  GUID, kind, both names, region GUID, region types and icon file name; the ambiguous GUIDs; the two counts), the icon
  section (the distinct PNG files with width and height; per GUID an icon index or a problem; the two counts) and the
  SHA-256 of everything before it. A change of the format raises the version, and an older file is extracted again.
- **Bounds** (checked before an allocation, with no collection sized from a count): the file at most 320 MiB
  (335,544,320 bytes), checked against the length of the opened handle before it is read; everything but PNG data at most
  64 MiB; the PNG data at most 256 MiB; a string at most 65,536 bytes; every count at most the bytes left divided by the
  least encoded size of its element.
- **Validation:** the file is read in one forward pass through an incremental hash and used only when the whole of it
  was valid; nothing follows the hash. The identity is compared first, then the game state; the first difference ends the
  read with its kind. Strings are strict UTF-8, kinds and problems must be defined, an asset GUID occurs once. Each icon is
  checked when it is read, and the first invalid one stops the read: its sides are 1 to 2,048 and equal those of its
  PNG header, the one data chunk inflates within height x (1 + 4 x width) bytes plus one, without a preset dictionary, every
  row starts with filter byte 0 and the length is exact, and the pixels written again by the Companion's own PNG writer give
  exactly the stored bytes, which leaves no other chunk, field or trailing byte. The decoded sizes of all distinct icons together may not exceed four times the icon set bound ([`game-catalog-icons.md`](game-catalog-icons.md), *Limits and errors*)
  (1 GiB), checked from the stored sides before an icon is read, and a PNG that no GUID uses is refused. Anything
  else, a truncated file, a flipped byte, a trailing byte, another magic or version, is `invalid`. Another compression library invalidates it once.
- **Writes:** the Companion writes `catalog-<32 lowercase hex>.tmp` in the cache folder, created new, flushes it to
  disk and moves it over `catalog.bin`; it deletes its own temporary file on any failure. A torn or mixed file fails the
  checksum: the next load finds the previous complete file, one writer's, or none.
  Before it writes, it deletes the top-level files in the folder whose whole name is exactly that of a temporary file
  (lowercase hex, case-sensitive) and which are more than 24 hours old, a link as a link; a file of any other name,
  a folder, and a fresh file of a concurrent writer are never deleted. The writer refuses content above a bound (the summed decoded size
  included). A failure to write (no folder, disk full, access denied, a bound) leaves names and icons in use and shows
  `Catalog: <n> entries, not cached` and the warning `Catalog cache not written` with the exception.

## Limits and errors

- **Memory:** a cache load holds the catalog and the icons it publishes and, for the icon being checked, its inflated data,
  its pixels (each at most 16 MiB, [`game-catalog-icons.md`](game-catalog-icons.md), *Limits and errors*) and its
  re-encoded PNG, but no copy of the file; a write streams to the temporary file.

## Privacy

- The cache file is private like a recording: never sent, logged or released; it stays when the Companion is removed.

## Related decisions

- [`decisions/0052-catalog-cache-keyed-by-file-properties-and-companion-identity.md`](../decisions/0052-catalog-cache-keyed-by-file-properties-and-companion-identity.md)
  — the cache is keyed by the Companion identity and the game's file properties, checked at the pipe connection.
- [`decisions/0053-catalog-cache-one-binary-file-validated-whole-before-use.md`](../decisions/0053-catalog-cache-one-binary-file-validated-whole-before-use.md)
  — one binary file with a checksum, validated whole, each icon written again by the Companion's PNG writer.

## Not here

- The cache folder: [`companion.md`](companion.md); the cache log lines: [`companion-log.md`](companion-log.md); the build of the game, which is part of the key:
  [`game-installation.md`](game-installation.md).
- Building the catalog and the check of tick GUIDs against it: [`game-catalog.md`](game-catalog.md); finding and converting
  the icons: [`game-catalog-icons.md`](game-catalog-icons.md).
- The catalog format for the server: [`game-catalog-format.md`](game-catalog-format.md); the upload (#42).
