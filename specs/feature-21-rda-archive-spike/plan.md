# Plan: Spike — reading RDA archives (own V2.2 reader and installation survey)

Source: Issue #21 | [spec.md](spec.md)
Tier: standard — a new parser of external input (game archives) with size bounds and validation, which is input handling, not a security control.
Security areas touched: Parsing of external input (game files, new in `Censio.GameData`); files the Companion's code opens (the archives, read-only, from a folder the caller names). No dependency, no Docker/CI or build configuration.
Plan size: a parser of an unverified external format with Security's overflow and work bounds, a layout probe and a survey whose output layout is fixed here.

## Problem / root cause

The feature of [spec.md](spec.md): no library reads Anno 117 archives, so `Censio.GameData` gets an own reader for
`Resource File V2.2` and a survey that the Product Manager runs on the installation.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | No NuGet package reads Anno RDA archives | nuget.org website search, 2026-11-08, 13 terms (record 0043); the Product Manager repeats it before step 9 (N7) | confirmed |
| 2 | The NuGet search API is refused; the website search and `api.nuget.org` open | `curl`: `azuresearch-usnc.nuget.org` → proxy 403; `www.nuget.org/packages?q=rda` → 200 | confirmed |
| 3 | All 40 archives under `maindata` start with `Resource File V2.2`, bytes 18–63 zero | `log.md` row "2a Discovery listing (AC6)" (40 name–size pairs) | confirmed |
| 4 | Archives reach 28 GB, many exceed 4 GiB: offsets need 64 bits | same row (`graphics_roman` 28078532503 bytes) | confirmed |
| 5 | The game executables carry no file version | same row; the Product Manager reads the version in the game or launcher — not reported in task 9, due with the run before step 9 (AC2) | confirmed |
| 6 | Record 0024 is released | `git tag --contains` of its adding commit → `v0.1.0` | confirmed |
| 7 | V2.2 layout, as far as real archives show it: first block offset `uint64` at byte 784, header region 792 bytes; 32-byte block header (`uint32` flags, `uint32` file count, `uint64` stored and decompressed directory size, `uint64` next block offset), directory right before it; 560-byte entries (UTF-16LE path in 520 bytes, NUL-padded; `uint64` offset, stored size, size; then timestamp and an unknown field, unread); the chain ends at a next offset equal to the file length; block flags met only 0 and 8, a flag-8 directory plain and its entries with offset, stored size and size 0; at most 20,478 blocks per archive | task 1, [probe-output.txt](probe-output.txt) (`log.md` row "G1 task 1 PM layout probe"): chain to the file length in all 40 archives, no error; flags only 0 and 8 (`zz_patchfiles_200` 20,405 of 20,478 blocks, `_300` 2,603 of 2,698); header, directory (stored = decompressed = files × 560, path bytes `64 00`) and first entry (stored = size, data `3C 47`, `42 4B`, `4F 54`) of the first flag-0 block of `config.rda`, `en_us0.rda`, `zz_patchfiles_200.rda` and that archive's first flag-8 block | confirmed |
| 13 | Every block of all 40 archives passes the reader's checks within `RdaLimits.Default` (largest entry count 52,878); no block that is not deleted carries flag 1, 2 or 4, so every listed entry is plain; the block of file count 0 in `zz_patchfiles_200` passes | task 9, `log.md` row "G3 task 9 PM survey run": every archive opened, no error line, entry flags only `None`, encrypted blocks 0 | confirmed |
| 14 | Flags 1 compressed (zlib), 2 encrypted, 4 memory-resident (`uint64` stored and decompressed data size in the 16 bytes before the directory, the data before that, entry offsets relative to the decompressed data) as the community describes them; flag 8 means deleted | no real block carries flag 1, 2 or 4 (fact 13): those layouts stay tested on synthetic archives only, and the area document says so; what flag 8 means for load order is #23's (record 0043) | unverified |
| 8 | `Censio.GameData` holds only `GameDataAssembly`; `Censio.Companion.Core` references it | `src/Censio.GameData/`; `docs/ARCHITECTURE.md` l. 24 | confirmed |
| 9 | Locations: in `config.rda` `data/base/config/export/assets.xml`, `data/base/config/game/datasets.xml`, `data/base/config/gui/texts_<language>.xml` (German and English among 13); `de_de0.rda` and `en_us0.rda` hold sound only; `.dds` icons under `data/ui/4k/<base,dlc01,dlc02,dlc03>/icon_content/<group>/` (`production_goods`, `building`, `workforce`, …) and `data/ui/2kimages/…` in `ui.rda` and `zz_patchfiles_*`; province data in `provinces_roman`, `provinces_celtic`, `dlc01_provinces`, `dlc03_provinces_01/02`. Which file and node holds goods, buildings, workforce and regions is not shown (no content read) | task 9, `log.md` row "G3 task 9 PM survey run" (survey lines; corrects rows 2b: icons also in `ui.rda`, provinces in their own archives) | confirmed |
| 10 | Icon folders recur in several archives (`ui.rda`, `zz_patchfiles_150/160/200/210/300`); no source says which copy wins | `log.md` rows 2b and "G3 task 9 PM survey run"; load order not reported | confirmed |
| 11 | On Linux, .NET's `FileShare` does not tell a shared-write open from a shared-read one, so the E7 unit test discriminates on Windows only | to verify: E7 by the Product Manager's run with the game running (source: the Lead's knowledge of .NET's advisory locking on Unix) | unverified |
| 12 | The decision check fails until `game-files` is in the areas index | `decision-check.py`: "area 'game-files' is not listed"; task 11 adds the row | confirmed |

## Acceptance criteria

AC1–AC6 of [spec.md](spec.md).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | manual: the Product Manager repeats the record's search and confirms the pages the session could not open, before step 9; the Reviewer checks record 0043 against S1 in step 8 | `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` |
| AC2 | manual: the Product Manager's survey run (task 9) once step 7 is green; the Dev writes the locations from it; the run on the final head before step 9 confirms them | `docs/areas/game-files.md` |
| AC3 | `RdaArchiveOpenListsEntriesWithPathsSizesAndFlags`, `RdaArchiveReadStoredEntryReturnsContent`, `RdaArchiveReadCompressedEntryReturnsDecompressedContent`, `RdaArchiveReadEntryOfMemoryResidentBlockReturnsContent`, `RdaArchiveReadEntriesAcrossSeveralBlocksReturnsEach`, `RdaArchiveReadEntryBeyondFourGibibytesReturnsContent`, `RdaArchiveOpenSkipsDeletedBlock`, `RdaArchiveOpenBlockWithoutFilesAddsNoEntries`, `RdaArchiveOpenPathOpensFileOnDisk` | `RdaArchiveTests.cs` |
| AC4 | `RdaArchiveOpenWrongMagicReturnsUnsupportedArchive`, `RdaArchiveOpenOtherVersionReturnsUnsupportedArchive`, `RdaArchiveOpenTruncatedHeaderReturnsCorruptArchive`, `RdaArchiveOpenBlockOutsideFileReturnsCorruptArchive`, `RdaArchiveOpenBlockHeaderValueAboveInt64MaxReturnsCorruptArchive`, `RdaArchiveOpenDirectoryOutsideFileReturnsCorruptArchive`, `RdaArchiveOpenRevisitedBlockOffsetReturnsCorruptArchive`, `RdaArchiveOpenTooManyBlocksReturnsCorruptArchive`, `RdaArchiveOpenTooManyEntriesReturnsCorruptArchive`, `RdaArchiveOpenFileCountOverflowReturnsCorruptArchive`, `RdaArchiveOpenDirectoryAboveBoundReturnsCorruptArchive`, `RdaArchiveOpenDirectorySizeMismatchReturnsCorruptArchive`, `RdaArchiveOpenTruncatedCompressedDirectoryReturnsCorruptArchive`, `RdaArchiveOpenCorruptCompressedDirectoryReturnsCorruptArchive`, `RdaArchiveOpenOverlappingDirectoriesAboveTotalBoundReturnsCorruptArchive` (the blocks have file count 0, flag 1, and each points its stored directory over the same region), `RdaArchiveReadEntryOutsideFileReturnsCorruptEntry`, `RdaArchiveReadEntrySizeAboveInt64MaxReturnsCorruptEntry`, `RdaArchiveReadEntryOffsetPlusSizeOverflowReturnsCorruptEntry`, `RdaArchiveReadEntryAboveBoundReturnsCorruptEntry`, `RdaArchiveReadCompressedStoredSizeAboveBoundReturnsCorruptEntry`, `RdaArchiveReadDecompressedShorterThanDeclaredReturnsCorruptEntry`, `RdaArchiveReadDecompressedLongerThanDeclaredReturnsCorruptEntry`, `RdaArchiveReadCorruptCompressedEntryReturnsCorruptEntry`, `RdaArchiveReadMemoryResidentDataAboveBoundReturnsCorruptEntry`, `RdaArchiveReadMemoryResidentStoredSizeAboveBoundReturnsCorruptEntry`, `RdaArchiveReadMemoryResidentSliceOverflowReturnsCorruptEntry`, `RdaArchiveReadUnknownFlagReturnsUnsupportedEntryAndOthersRead`, `RdaArchiveOpenEncryptedBlockCountsItAndOthersRead`, `RdaArchiveReadMissingPathReturnsNotFound`, `RdaArchiveReadDuplicatePathReturnsDuplicateEntry`, `RdaArchiveOpenMissingFileReturnsUnreadable`, `RdaArchiveReadStreamFailureReturnsUnreadable`; `RdaLimitsConstructorNonPositiveValueThrows` | `RdaArchiveTests.cs`, `RdaLimitsTests.cs` |
| AC5 | `RdaInstallationSurveyRunReadsAssetAndIconWithMarkers`, `RdaInstallationSurveyRunPrefersConfigAndUiArchives`, `RdaInstallationSurveyRunFallsBackToAnyConfigXml`, `RdaInstallationSurveyRunReportsPathInEveryArchive`, `RdaInstallationSurveyRunReportsExtensionAndFolderCounts`, `RdaInstallationSurveyRunListsLanguageArchiveEntriesWithoutMedia`, `RdaInstallationSurveyRunReportsArchiveErrorWithMessageAndContinues`, `RdaInstallationSurveyRunSkipsLinkedFolder`, `RdaInstallationSurveyRunReportsNoContent`, `RdaInstallationSurveyRunWithoutArchivesReportsNone`, `RdaInstallationSurveyRunWrongMarkerIsNotRead`, `RdaArchiveOpenWhileFileHeldOpenForWritingSucceeds` (E7, fact 11); `RdaInstallationSurveyRealInstallationReadsAssetAndIcon` (inconclusive without `CENSIO_ANNO_INSTALL`); manual: the Product Manager runs that test on a local build of the branch head, once with the game running, before step 9 | `RdaInstallationSurveyTests.cs`, `RdaArchiveTests.cs` |
| AC6 | check: `log.md` rows "2a Discovery listing (AC6)" and "2b" hold only paths, sizes and header markers | `specs/feature-21-rda-archive-spike/log.md` |

The Product Manager's runs (tasks 1 and 9, and before step 9) are handed over by the orchestrator: the probe of
*Layout probe* below, then `dotnet test tests/Censio.GameData.Tests -c Release --filter TestCategory=AC5 --logger "console;verbosity=detailed"`
on Windows with `CENSIO_ANNO_INSTALL` set to the installation folder. The orchestrator posts each output to `log.md`
inside a fenced code block: entry paths are untrusted archive strings.

## Approach

**Layout probe (task 1, done).** `probe.ps1` in this folder walked every archive's block chain read-only and
sampled the first block per flag value of three archives; its output, [probe-output.txt](probe-output.txt), confirmed
the layout of fact 7 without change to the reader's design. The survey run of task 9 opened every archive without
an error line and found no compressed, encrypted or memory-resident block (fact 13). What the run before step 9 must
still show: the same with the game running (E7, fact 11), and the game version (AC2).

**Reader (`RdaArchive`).** `Open` reads the header and walks the block chain from the first block offset (fact 7),
reading each block's directory into `RdaEntry` items in archive order; it keeps the stream open for `Read` until
`Dispose`. Opening a path uses read access with `FileShare.ReadWrite | FileShare.Delete`; nothing is written.

Every `uint64` is read as `ulong`; above `long.MaxValue` it is corrupt (`CorruptArchive` in `Open`, `CorruptEntry`
in `Read`). Every range check is written `offset <= length && size <= length - offset`, and file count × 560 is
computed in 64 bits. Checks, failing closed with the first kind that applies:

- magic not `Resource File V2.2` → `UnsupportedArchive` (a UTF-16 `Resource File V2.0` too); a file shorter than
  792 bytes → `CorruptArchive`;
- a block offset below 792, outside the file for 32 bytes, or seen before; more blocks than `MaxBlocks` (every
  block counts, encrypted and deleted ones too); a next offset that is neither the file length nor a valid block
  offset → `CorruptArchive`;
- a block with flag 2 adds no entries and raises `EncryptedBlockCount`; a block with flag 8 adds none; neither
  directory is read. A flag-8 entry has no content whether flag 8 means deleted or placeholder (fact 7), so skipping
  loses nothing to read; what it means for load order is #23's (record 0043). Flags beyond 1, 2, 4 and 8 are kept on
  the block's entries, which `Read` refuses as `UnsupportedEntry`;
- a block with file count 0 passes the checks below with directory sizes 0 and adds no entries (fact 13);
- for every other block, before reading its directory: running entry total plus file count above `MaxEntries`;
  declared decompressed size other than file count × 560 or above `MaxDirectoryBytes`; stored size above
  `MaxDirectoryBytes`, the directory not inside the file, or the running total of stored directory bytes read above
  the file length (overlapping directories cannot multiply the work); without flag 1, stored size other than the
  decompressed size → `CorruptArchive`;
- the directory is decompressed from a bounded window over the stream, output cut one byte past the declared size;
  a zlib failure or a byte count other than the declared size (a truncated stream returns fewer bytes without
  throwing) → `CorruptArchive`. The directory buffer is released after each block.

`Open` thus retains at most `MaxEntries` entries with paths of at most 260 characters.

`Read(path)` finds the entry by ordinal path match: none → `NotFound`, more than one → `DuplicateEntry`. Before
reading: size or stored size above `MaxEntryBytes`, or the stored range outside the file → `CorruptEntry`; a stored
entry needs stored size = size. A compressed entry decompresses with zlib from a bounded window over the stored range
into a buffer of the declared size, output cut one byte past it; fewer or more bytes, or a zlib failure →
`CorruptEntry`. A memory-resident entry reads the block's size pair; stored or decompressed data size above
`MaxEntryBytes`, the data range outside the file, or the entry's slice outside the decompressed data → `CorruptEntry`;
the data is decompressed the same way once per `Read` (no cache in the spike) and the slice returned. `IOException`,
`UnauthorizedAccessException`, `FileNotFoundException`, `DirectoryNotFoundException` → `Unreadable`; `Open(Stream)`
refuses a stream that cannot read and seek the same way. No other exception leaves `Open` or `Read` for archive
content; null arguments throw `ArgumentNullException`.

Each `RdaError.Message` is a fixed English sentence naming the kind and, where useful, the block or entry index and
the bound — never archive bytes, a path inside the archive or an exception message (only the type name, for
`Unreadable`). Wording free for the Dev; tests compare kinds.

`RdaLimits` throws `ArgumentOutOfRangeException` for a value ≤ 0. `RdaLimits.Default`: `MaxBlocks` 1,048,576;
`MaxEntries` 1,048,576; `MaxDirectoryBytes` 64 MiB; `MaxEntryBytes` 512 MiB. Against the probe: `MaxBlocks` is
51 times the largest count (20,478); the sampled directories (at most 194 files, 108,640 bytes) and entries (at most
17 MB) are far below their bounds, and task 9 hit no limit on any archive (largest entry count 52,878, fact 13), so
task 10 raised nothing.

**Survey (`RdaInstallationSurvey.Run`).** Enumerates `*.rda` under `<installation>/maindata` with
`EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = ReparsePoint }`,
ordered by relative path (ordinal, ignoring case), one archive open at a time. Output lines, in this order:

1. `Archives: <n>` (or `Archives: none under maindata`, then both reads false and nothing more).
2. Per archive: `<relative path> | entries <n> | flags <name>=<count>, … | encrypted blocks <n> | duplicate paths <n>`, then `  extensions: <ext>=<n>, …` and `  folders: <first two path segments>=<n>, …` (descending count); or `<relative path> | error <kind>: <message>`.
3. `Matching entries:` then one line per `.xml` entry whose path contains, ignoring case, `asset`, `text`, `guid`, `region`, `province`, `session` or `dataset`: `<entry path> | <size> | <archive>, …` (every archive holding it, survey order), sorted by path.
4. `Icon folders:` then one line per folder of `.dds` entries whose path contains `icon`: `<folder> | <count> | <archive>, …`.
5. `Language archive entries:` then, for `de_de0.rda` and `en_us0.rda`, every entry whose extension is not `.wem`, `.bnk`, `.wav`, `.ogg`, `.bik`, `.webm`, `.mp4` or `.usm`: `<archive> | <entry path> | <size>`.
6. `Asset read: <archive> | <entry path> | <size> | XML` — the smallest `.xml` entry with `asset` in its path in `config.rda`, else the smallest `.xml` in `config.rda` (ties: ordinal path); `XML` when the content starts, after an optional UTF-8 or UTF-16 byte order mark and whitespace, with `<`; otherwise `unknown marker`, `error <kind>: <message>`, or `Asset read: none found`.
7. `Icon read: …` the same for the smallest `.dds` entry with `icon` in its path, in `ui.rda`, else in any archive in survey order; `DDS` when the content starts with `DDS ` (44 44 53 20).

`AssetRead`/`IconRead` are true only for the markers `XML`/`DDS`. No line holds entry content.

**Installation test.** `RdaInstallationSurveyRealInstallationReadsAssetAndIcon` reads `CENSIO_ANNO_INSTALL`; unset,
`Assert.Inconclusive`; set, it runs the survey with `RdaLimits.Default`, writes every line to `TestContext`, and
asserts both reads.

**Test helpers.** `RdaArchiveBuilder` writes synthetic V2.2 archives with invented content (blocks stored,
compressed, memory-resident, encrypted, deleted, empty, with an unknown flag, and every malformation of AC4) from
fact 7, the layouts of flags 1, 2 and 4 from fact 14; deleted blocks hold entries with offset and sizes 0, as on the
installation. `SparseStream` is a seekable read-only stream of a given length, zeros outside the regions
written into it, optionally throwing `IOException` at an offset — for the 4 GiB, `long.MaxValue` and overlap tests.
Survey tests use a temporary folder they delete; the linked-folder test skips itself where symbolic links cannot be
created.

## Options considered

1. **A NuGet library** — none exists (fact 1).
2. **Vendor a community reader's source** — licence and Anno 117 support could not be checked; still needs our bounds.
3. **Own V2.2 reader** — chosen (record 0043).
4. **The installation check as test code only** — its walking and picking would be untested and uncounted by
   coverage; a production `RdaInstallationSurvey` is tested on synthetic folders and run for real by one test.
5. **Entries read as streams instead of bounded buffers** — needed only if a needed file exceeds `MaxEntryBytes`;
   left to #23 (record 0043).
6. **Build without the probe, the step-9 run as the only real check** (the Product Manager's option a) — a wrong
   layout would surface after all code and tests; the probe costs one short run.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `RdaArchive`, `RdaEntry`, `RdaBlockAttributes`, `RdaLimits`, `RdaOpenResult`, `RdaReadResult`, `RdaError`, `RdaErrorKind`, `RdaInstallationSurvey`, `RdaSurveyReport` | new |
| Censio.GameData.Tests | `RdaArchiveTests`, `RdaLimitsTests`, `RdaInstallationSurveyTests`, `RdaArchiveBuilder`, `SparseStream` | new |

## Signatures (for the Dev's skeleton)

Namespace `Censio.GameData`. `RdaEntry` gets an internal constructor with path, size and flags plus the internal
location fields the reader needs (offset, stored size, block index); it is no API.

- `[Flags] public enum RdaBlockAttributes { None = 0, Compressed = 1, Encrypted = 2, MemoryResident = 4, Deleted = 8 }`
- `public enum RdaErrorKind { UnsupportedArchive, CorruptArchive, CorruptEntry, UnsupportedEntry, NotFound, DuplicateEntry, Unreadable }`
- `public sealed class RdaError` with `public RdaError(RdaErrorKind kind, string message)`, `public RdaErrorKind Kind { get; }`, `public string Message { get; }`
- `public sealed class RdaLimits` with `public RdaLimits(int maxBlocks, int maxEntries, int maxDirectoryBytes, int maxEntryBytes)`, `public int MaxBlocks { get; }`, `public int MaxEntries { get; }`, `public int MaxDirectoryBytes { get; }`, `public int MaxEntryBytes { get; }`, `public static RdaLimits Default { get; }`
- `public sealed class RdaEntry` with `public string Path { get; }`, `public long Size { get; }`, `public RdaBlockAttributes Flags { get; }` (the enum avoids the `Flags` suffix of S2344; the property keeps the format's term)
- `public sealed class RdaArchive : IDisposable` with `public static RdaOpenResult Open(string path, RdaLimits limits)`, `public static RdaOpenResult Open(Stream stream, RdaLimits limits)`, `public IReadOnlyList<RdaEntry> Entries { get; }`, `public int EncryptedBlockCount { get; }`, `public RdaReadResult Read(string path)`, `public void Dispose()`
- `public sealed class RdaOpenResult` with `public RdaArchive? Archive { get; }`, `public RdaError? Error { get; }`, `public static RdaOpenResult FromArchive(RdaArchive archive)`, `public static RdaOpenResult FromError(RdaError error)`
- `public sealed class RdaReadResult` with `public ReadOnlyMemory<byte> Content { get; }`, `public RdaError? Error { get; }`, `public static RdaReadResult FromContent(ReadOnlyMemory<byte> content)`, `public static RdaReadResult FromError(RdaError error)`
- `public static class RdaInstallationSurvey` with `public static RdaSurveyReport Run(string installationFolder, RdaLimits limits)`
- `public sealed class RdaSurveyReport` with `public RdaSurveyReport(IReadOnlyList<string> lines, bool assetRead, bool iconRead)`, `public IReadOnlyList<string> Lines { get; }`, `public bool AssetRead { get; }`, `public bool IconRead { get; }`

`Open(Stream)` takes ownership of the stream (disposed with the archive, or on a failed open). The skeleton's
`RdaLimits` constructor stores its arguments; the range check comes with the implementation.

## Test files

`tests/Censio.GameData.Tests/RdaArchiveTests.cs`, `tests/Censio.GameData.Tests/RdaLimitsTests.cs`,
`tests/Censio.GameData.Tests/RdaInstallationSurveyTests.cs`; helpers `tests/Censio.GameData.Tests/RdaArchiveBuilder.cs`,
`tests/Censio.GameData.Tests/SparseStream.cs`. `RdaError` and the result types are covered through `RdaArchiveTests`.

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

New area `game-files` (Lead decision): *Reading the game's archives in `Censio.GameData`: the archive format version
read, block and entry kinds, bounds, failure kinds, read-only shared access, where the game keeps names, texts and
icons, the installation survey* — not here: finding the installation (#22), the catalog (#23–#26).

## Documentation updates

Owned by the Dev unless named, written once the code is green:

- `docs/areas/game-files.md` (new, from `_template.md`): format V2.2 as read (fact 7, the reader's
  contract), flags (1, 2 and 4 met on no real archive, fact 13; their layouts unverified, fact 14), the `RdaLimits.Default` values, when they apply and the memory `Open` retains, the failure kinds
  of E1–E6 and E8 with the encrypted-block count, read-only shared access, no content leaves the machine (link 0023);
  entry paths are untrusted strings: a consumer that logs them follows `companion.md`
  (*Log*), one that derives a file system path from them (#24, #25) confines it. *Locations* from task 9 — per data
  kind of S2 the entry paths and every archive holding them, the game version; fact 9's candidates only where the run
  confirms them (fact 9 as task 9 corrected it); "which file and node holds goods, buildings, workforce and regions,
load order and winning copy: unknown (record 0043)". The survey and `CENSIO_ANNO_INSTALL`.
- `docs/areas/README.md`: the index row of `game-files` (fact 12).
- `docs/ARCHITECTURE.md`: the `Censio.GameData` row links `areas/game-files.md`; *Security model*: game archives are
  opened read-only, shared with the game, and read within the reader's limits (record 0043).
- `.squad/project.md`: *Security areas*, "Parsing of external input": the game archives (`RdaArchive`,
  `RdaInstallationSurvey`) instead of "later game files", linking `game-files`; "File paths the Companion … opens":
  the game archives under the installation folder. *Test doubles* (Tester): `SparseStream`.
- `docs/UNIT_TESTS.md` (Tester), *Where tests live*: `CENSIO_ANNO_INSTALL`; unset, the survey test is inconclusive.
- Record 0043 (Lead): the probe's findings (task 2) and the survey's (task 15), done.
- `README.md`, `docs/USER_GUIDE.md`, `SECURITY.md` (already lists game-file parsing): unchanged (N6).

## Architecture check

No guarantee of `.squad/project.md` changes. Game files stay local (record 0023): synthetic test archives, no
content in the survey or the probe. `Censio.GameData` stays platform-neutral `net10.0` (record 0004).

## Security considerations

- [x] Limits: `MaxBlocks` counts every block; `MaxEntries` the running entry total, checked before a directory is
  read; `MaxDirectoryBytes` the stored and the declared decompressed directory, the stored directory bytes read
  together at most the file length; `MaxEntryBytes` an entry's or memory-resident block's stored and declared sizes
  before allocation, and the decompressed output (cut one byte past the declared size). Paths: 520 bytes fixed.
  Retained: at most `MaxEntries` entries, paths ≤ 260 characters; directory buffers freed per block.
- [x] Exceptions reaching a message: `IOException`, `UnauthorizedAccessException`, `FileNotFoundException`,
  `DirectoryNotFoundException` → `Unreadable` with the type name only; zlib's `InvalidDataException` →
  `CorruptArchive`/`CorruptEntry` with a fixed text. No runtime message reaches a line; no arithmetic on a `ulong`
  above `long.MaxValue` and no negative length can reach an allocation.

## Decision records

- `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` (Proposed), amending 0024.

## Challenge

- M1 (reader and builder from memory, no real sample): accepted in part — the Product Manager's confirmation to
  build without a sample exists (spec *Open questions*, `log.md` 14:00–14:01, option a), so no escalation; still,
  task 1 adds a layout probe before the builder (it confirmed fact 7; task 9 fact 13; fact 14 stays open), and option 6 records why.
- M2 (survey filter a guess): accepted — extension and folder counts per archive, `province` and `dataset` added,
  the language archives' non-media entries listed, the asset fallback to any `config.rda` XML, icons of every kind
  (fact 9).
- m1 (40 archives, not 36): accepted — fact 3 and record 0043 corrected (40 pairs in row 2a).
- m2 (error message on a failed archive): accepted — `error <kind>: <message>`; task 10 raises a default hit.

## Security review

- B1 (uint64 sign and overflow): `ulong` reads, `long.MaxValue` refusal, subtraction-form range checks, 64-bit
  count × 560; tests named in AC4.
- B2 (stored size unbounded): stored sizes of entries and memory-resident data checked against `MaxEntryBytes`
  before reading, decompression from a bounded window; two tests.
- B3 (total work in `Open`): every block counts toward `MaxBlocks`; stored directory bytes read in total at most the
  file length; encrypted and deleted directories not read; overlap test.
- B4 (truncated directory): decompressed byte count must equal the declared size; stored = decompressed without
  flag 1; test.
- N1: retained memory stated (*Security considerations*, area document). N2: `EnumerationOptions` skipping reparse
  points and inaccessible folders; test. N3: fenced code block for posted output; area document sentence.
  N4: `RdaLimits` range check; test.

## Out of scope / follow-ups

Spec *Out of scope*; #23 settles load order (icons recur across archives too, so #24 depends on it) and streamed
entries (record 0043).
