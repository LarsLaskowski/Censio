# Game files

## Scope

Reading the game's archives in `Censio.GameData`: the archive format version that is read, the block and entry
kinds, the bounds, the failure kinds, the read-only access shared with the running game, where the game keeps its
names, texts and icons, and the installation survey that found them. Finding the installation ([`game-installation.md`](game-installation.md)) and building the
catalog from these files ([`game-catalog.md`](game-catalog.md)) are not part of it; they rely on this document for what a reader returns and
where to look. The archives are untrusted input: the layout is built from the community's description of the format
and confirmed on a real installation by the layout probe and the survey run (record 0043). A game update may change
it without notice.

## Behavior

### Format as read

- An archive is a file that starts with the text `Resource File V2.2` in a header region of 792 bytes. Any other
  file is refused as an unsupported archive. All numbers are little-endian; offsets and sizes are 64-bit unsigned.
- The header region holds the offset of the first block header. Blocks form a chain: each block header (32 bytes)
  holds the block's flags, its entry count, the stored and the declared decompressed size of its directory and the
  offset of the next block header. The chain ends where the next offset equals the file length.
- A block's directory lists its entries, 560 bytes each, with a fixed path field of 520 bytes. An entry holds the
  location and the stored and declared sizes of its data. A block may pack the data of several entries.
- The reader lists the entries of all blocks in archive order and reads one entry by its exact path (ordinal
  comparison) as its decompressed content.
- Block flags: `1` compressed (zlib), `2` encrypted, `4` memory-resident, `8` deleted. Both the probe and the survey
  run met only the flags `0` and `8` on the 40 archives of the Product Manager's installation: the compressed,
  encrypted and memory-resident layouts have been exercised by synthetic test archives only, not by a real archive.
  A block flagged deleted and an encrypted block contribute no entries; encrypted blocks are counted and reported
  with the archive. A deleted block in a patch archive names paths with no content in that archive; whether it
  removes the file of an earlier archive is part of the load order, which is unknown (see *Locations*).
- An entry whose declared size is above the largest signed 64-bit number is listed with the size `-1` and cannot be
  read.

### Access

- A file is opened read-only, sharing read, write and delete with other processes, so the archives can be read
  while the game runs. Nothing is written into the installation.
- Reading an archive needs memory for its entry list and for one directory or entry at a time, never the whole file.

### Limits

- Four bounds apply, with these defaults: 1,048,576 blocks per archive, 1,048,576 entries per archive, 64 MiB for
  one block's directory (stored and declared decompressed size) and 512 MiB for one entry's content (stored and
  decompressed size). A caller may name other bounds; a bound of zero or less is refused.
- Every block counts toward the block bound, also deleted and encrypted ones. The entry bound counts the running
  total and is checked before a directory is read. A directory's sizes are checked before it is read or allocated,
  and the stored directory bytes read in all stay within the length of the file. An entry's stored and declared
  sizes are checked before its data is allocated, and decompression stops one byte past the declared size.
- No offset or size above the largest signed 64-bit number reaches arithmetic or an allocation: it is refused as
  corrupt first.
- An archive that is opened retains at most the bound on entries, each with a path of at most 260 characters, plus
  the open file; directory buffers are freed per block. A read holds one entry's decompressed content on top, and for an entry of a memory-resident block also that block's decompressed data while it is read.
- An entry above the entry bound cannot be read. The catalog's four files fit it (the largest, `assets.xml`, is about
  44 MiB of 512 MiB); a file above it fails the catalog ([`game-catalog.md`](game-catalog.md), *Failures*).

### Typed results

- Opening and reading return a result that holds either the archive or content, or one error with a kind and a
  fixed text; nothing is thrown for any byte sequence in the file. A reader fails closed: the first contradiction
  ends the open.

| Kind | When |
| ---- | ---- |
| Unsupported archive | The file does not start with the version text above, or is too short to do so |
| Corrupt archive | The file is shorter than its header region, a block or directory lies outside the file, the chain reaches a block twice, a directory's sizes contradict its entry count or each other, a compressed directory is damaged or decompresses to another size, or a bound on blocks, entries or directory size is exceeded |
| Corrupt entry | An entry's location, stored size or declared size contradicts the file or exceeds the entry bound, or the decompressed size differs from the declared one |
| Unsupported entry | The entry's block carries a flag the reader does not know; the other entries stay readable (encrypted blocks list no entries at all) |
| Not found | No entry has the requested path |
| Duplicate entry | More than one entry of the archive has the requested path; neither is returned, and the listing shows both |
| Unreadable | The file is missing or cannot be opened or read; the text names the type of the I/O problem, not the content |

- An error text is fixed and holds no content of the archive and no text of a runtime exception.

### Entry paths are untrusted

- A path comes from the file and is untrusted text. A consumer that logs one follows the rule of the Companion's log
  ([`companion.md`](companion.md), *Log*): control characters, format characters, line and paragraph separators
  and unpaired surrogates are replaced, and the message is cut.
- A consumer that derives a file system path from an entry path (the icon cache of #24 and #25) confines the result
  to its own folder: it does not use `..`, a rooted path or a drive letter from the entry.

### Privacy

- Game content stays on the machine of the user: no extracted entry, text or icon enters the repository, a fixture,
  a log or test output (records [0023](../decisions/0023-game-names-and-icons-extracted-locally.md) and
  [0029](../decisions/0029-anonymised-test-fixtures-only.md)). Test archives are synthetic with invented content.
  Entry paths, sizes, flags and header markers are not content.

### Installation survey

- The survey lists the `.rda` files below the `maindata` folder of an installation (symbolic links and folders it
  cannot read are skipped), opens each one, and reports per archive the number of entries, the flags met, the encrypted
  blocks, duplicate paths and the file extensions and folders. It also reads one asset file and one icon and reports
  the archive, the path, the size and the format marker; it reports no content.
- The unit test that runs it takes the installation folder from the environment variable `CENSIO_ANNO_INSTALL`;
  unset, the test is inconclusive. Output posted from a run holds paths, sizes and counts only.

## Locations

From the Product Manager's survey run of 2026-11-08 on the installation of the branch head (Windows, 40 archives
under `maindata`, no archive hit a bound, no open error). Every
archive is of the format above; all entries have the flags `0`, with no encrypted block and no duplicate path inside
an archive. The largest entry counts are 21,028 (`zz_patchfiles_300`), 46,110 (`shared_configs`), 52,878
(`dlc03_provinces_02`) and 50,081 (`dlc03_provinces_01`).

| Data kind | What the run confirms | What it does not |
| --------- | --------------------- | ---------------- |
| Goods, buildings, workforce | Two configuration files exist in `config.rda` (194 entries, folder `data/base`): `data/base/config/export/assets.xml` (46,440,056 bytes) and `data/base/config/game/datasets.xml` (615,716 bytes). Issue #23 confirmed that every good, workforce and building the pipe fixture names is a defining asset of `assets.xml` with names in both texts; the nodes are in [`game-catalog.md`](game-catalog.md) | Whether a patch archive may replace them: none holds a copy on the Product Manager's installation |
| Regions | The province data archives exist (`provinces_roman`, `provinces_celtic`, `dlc01_provinces`, `dlc03_provinces_01`, `dlc03_provinces_02`; entries of the kinds `.ctt`, `.tmc`, `.dds`, `.png`, `.a7m`, `.a7t`); no entry path outside `config.rda`'s assets, datasets and texts matched `province`. Issue #23 confirmed that the regions the pipe names are sessions of `assets.xml` whose `Session/Region` names a region asset, and that region types are the `Region` data set of `datasets.xml` ([`game-catalog.md`](game-catalog.md)) | The region type of a region asset without a `RegionID` (#130) |
| Texts (German, English) | `data/base/config/gui/texts_<language>.xml` in `config.rda`, for brazilian, english (6,784,986 bytes), french, german (7,071,912 bytes), italian, japanese, korean, metadata, polish, russian, simplified_chinese, spanish and traditional_chinese | The nodes (`TextExport/Texts/Text` with `LineId` and `Text`) are confirmed by #23. The language archives `de_de0.rda` and `en_us0.rda` hold only sound (`data/sound/generatedsoundbanks/windows/<de_de or en_us>/` with `.json`, `.txt`, `.xml`, `.wem` and `.bnk` entries), no texts |
| Icons | `.dds` files under `data/ui/` in `ui.rda` (19,027 entries) and in the patch archives `zz_patchfiles_*`; one icon was read (a DDS file of 260 bytes from `ui.rda`). Folders, by group: `data/ui/4k/base/icon_content/<group>` (`production_goods`, `building`, `workforce`, `generic`, `items_specialist`, `ornaments`, `trade_route`, ship icons and others), `data/ui/4k/dlc01`, `dlc02` and `dlc03` with the same `icon_content/<group>` layout, `data/ui/2kimages/main/3dicons/<group>`, `data/ui/2kimages/main/icons/` (`workforce`, `limestone_workforce`) and `data/ui/2kimages/fpp/icons/` (`provinces`, `workforce_tiers`, `tech_tree`) | Which icon belongs to which good, building or workforce tier: the file names are not matched to identifiers; for #24 |

- The repeat of the survey on the final head was made on game version 3.0.1733841 (reported by the Product Manager; store not stated).
- The survey was repeated on the final head while the game was running, with the same result: 40 archives opened without an error line, with the same entry counts and folders.
- The asset file was read from `config.rda`: `data/base/config/export/assets.xml`, 46,440,056 bytes, XML.
- The same icon folder occurs in several archives (`ui.rda` and `zz_patchfiles_150`, `_160`, `_200`, `_210`, `_300`;
  for example the group `production_goods` of the base folder in `ui.rda`, `zz_patchfiles_150` and
  `zz_patchfiles_300`). **Load order and which copy wins: unknown** (record
  [0043](../decisions/0043-rda-archives-read-by-an-own-bounded-reader.md)); the catalog uses `config.rda` and reports a
  copy of its four files in another archive ([`game-catalog.md`](game-catalog.md)), and none exists on the Product
  Manager's installation (his statement of 2026-11-09); #24 depends on the order for icons. The Product Manager says the number in `zz_patchfiles_<n>` is the game
  version (higher is newer), but that probably all of these archives are loaded because a newer one does not contain
  the contents of the older ones; no evidence was found (Product Manager's statement, unverified), input for the load-order question. It is no reader error: a path in several archives is returned by each.
- The run confirms these locations on one installation; the repeat on the final head confirms them again.

## Related decisions

- [`decisions/0043-rda-archives-read-by-an-own-bounded-reader.md`](../decisions/0043-rda-archives-read-by-an-own-bounded-reader.md)
  — an own bounded reader for format V2.2, amending 0024.
- [`decisions/0023-game-names-and-icons-extracted-locally.md`](../decisions/0023-game-names-and-icons-extracted-locally.md)
  — game content is extracted on the user's machine and does not enter the repository.

## Not here

- Finding the installation folder ([`game-installation.md`](game-installation.md)) and building the catalog
  ([`game-catalog.md`](game-catalog.md)); converting icons, the cache and the catalog format (#24 to #26).
- The log of the Companion and what may be logged: [`companion.md`](companion.md).
