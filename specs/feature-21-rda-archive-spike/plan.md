# Plan: Spike — reading RDA archives (own V2.2 reader and installation survey)

Source: Issue #21 | [spec.md](spec.md)
Tier: standard — a new parser of external input (game archives) with size bounds and validation, which is input handling, not a security control.
Security areas touched: Parsing of external input (game files, new in `Censio.GameData`); files the Companion's code opens (the archives, read-only, from a folder the caller names). No dependency, no Docker/CI or build configuration.

## Problem / root cause

The feature of [spec.md](spec.md): no library reads Anno 117 archives, so `Censio.GameData` gets an own reader for
`Resource File V2.2` and a survey that the Product Manager runs on the installation.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | No NuGet package reads Anno RDA archives | nuget.org website search, 2026-11-08, 13 terms (record 0043): only unrelated packages; the Product Manager repeats it before step 9 (N7) | confirmed |
| 2 | The NuGet search API is refused, the website search and `api.nuget.org` open | `curl` to `azuresearch-usnc.nuget.org` → proxy 403; `www.nuget.org/packages?q=rda` → 200 | confirmed |
| 3 | All 36 archives under `maindata` start with `Resource File V2.2`, bytes 18–63 zero | `log.md`, row "2a Discovery listing (AC6)" | confirmed |
| 4 | Archives reach 28 GB; many exceed 4 GiB, so offsets need 64 bits | same row (e.g. `graphics_roman` 28078532503 bytes) | confirmed |
| 5 | The game executables carry no file version | same row; the Product Manager reads the game version in the game or launcher instead | confirmed |
| 6 | Record 0024 is released and must not be edited | `git tag --contains` of its adding commit → `v0.1.0` | confirmed |
| 7 | V2.2 layout: 18-byte magic, first block offset as `uint64` at byte 784; block header of 32 bytes (`uint32` flags, `uint32` file count, `uint64` stored and decompressed directory size, `uint64` next block offset) with its directory right before it; directory entries of 560 bytes (path as UTF-16LE in 520 bytes, NUL-padded; `uint64` offset, compressed size, uncompressed size, timestamp, unknown); flags 1 compressed (zlib), 2 encrypted, 4 memory-resident, 8 deleted; a memory-resident block keeps `uint64` compressed and decompressed data size in the 16 bytes before its directory, its data before that, entry offsets relative to the decompressed data; the chain ends at a next offset equal to the file length | to verify: the Product Manager's survey run (AC5) after step 7 and on the final head before step 9 (source: the Lead's knowledge of the community's description, pages not openable, record 0043) | unverified |
| 8 | Censio.GameData holds only `GameDataAssembly`; `Censio.Companion.Core` references it | `src/Censio.GameData/`; `docs/ARCHITECTURE.md` l. 24 | confirmed |
| 9 | No source says which archive wins for a path in several archives | `log.md` row 2a: step 2 not done | confirmed |
| 10 | On Linux, .NET's `FileShare` does not tell a shared-write open from a shared-read one, so the E7 unit test only discriminates on Windows | to verify: E7 by the Product Manager's run with the game running (source: the Lead's knowledge of .NET's advisory locking on Unix) | unverified |
| 11 | The decision check fails until the area game-files is in the areas index | `decision-check.py` on this branch: "area 'game-files' is not listed"; task 8 adds the row | confirmed |

## Acceptance criteria

AC1–AC6 of [spec.md](spec.md).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | manual: the Product Manager repeats the record's search and confirms the pages the session could not open, before step 9; the Reviewer checks record 0043 against S1 in step 8 | `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` |
| AC2 | manual: the Product Manager runs the survey on a local build of the branch head once step 7 is green and posts its output; the Dev writes the locations from it; the run on the final head before step 9 confirms them | `docs/areas/game-files.md` |
| AC3 | `RdaArchiveOpenListsEntriesWithPathsSizesAndFlags`, `RdaArchiveReadStoredEntryReturnsContent`, `RdaArchiveReadCompressedEntryReturnsDecompressedContent`, `RdaArchiveReadEntryOfMemoryResidentBlockReturnsContent`, `RdaArchiveReadEntriesAcrossSeveralBlocksReturnsEach`, `RdaArchiveReadEntryBeyondFourGibibytesReturnsContent`, `RdaArchiveOpenSkipsDeletedBlock`, `RdaArchiveOpenPathOpensFileOnDisk` | `RdaArchiveTests.cs` |
| AC4 | `RdaArchiveOpenWrongMagicReturnsUnsupportedArchive`, `RdaArchiveOpenOtherVersionReturnsUnsupportedArchive`, `RdaArchiveOpenTruncatedHeaderReturnsCorruptArchive`, `RdaArchiveOpenBlockOutsideFileReturnsCorruptArchive`, `RdaArchiveOpenDirectoryOutsideFileReturnsCorruptArchive`, `RdaArchiveOpenRevisitedBlockOffsetReturnsCorruptArchive`, `RdaArchiveOpenTooManyBlocksReturnsCorruptArchive`, `RdaArchiveOpenTooManyEntriesReturnsCorruptArchive`, `RdaArchiveOpenDirectoryAboveBoundReturnsCorruptArchive`, `RdaArchiveOpenDirectorySizeMismatchReturnsCorruptArchive`, `RdaArchiveOpenCorruptCompressedDirectoryReturnsCorruptArchive`, `RdaArchiveReadEntryOutsideFileReturnsCorruptEntry`, `RdaArchiveReadEntryAboveBoundReturnsCorruptEntry`, `RdaArchiveReadDecompressedShorterThanDeclaredReturnsCorruptEntry`, `RdaArchiveReadDecompressedLongerThanDeclaredReturnsCorruptEntry`, `RdaArchiveReadCorruptCompressedEntryReturnsCorruptEntry`, `RdaArchiveReadMemoryResidentDataAboveBoundReturnsCorruptEntry`, `RdaArchiveReadUnknownFlagReturnsUnsupportedEntryAndOthersRead`, `RdaArchiveOpenEncryptedBlockCountsItAndOthersRead`, `RdaArchiveReadMissingPathReturnsNotFound`, `RdaArchiveReadDuplicatePathReturnsDuplicateEntry`, `RdaArchiveOpenMissingFileReturnsUnreadable`, `RdaArchiveReadStreamFailureReturnsUnreadable` | `RdaArchiveTests.cs` |
| AC5 | `RdaInstallationSurveyRunReadsAssetAndIconWithMarkers`, `RdaInstallationSurveyRunPrefersConfigAndUiArchives`, `RdaInstallationSurveyRunReportsPathInEveryArchive`, `RdaInstallationSurveyRunReportsArchiveErrorAndContinues`, `RdaInstallationSurveyRunReportsNoContent`, `RdaInstallationSurveyRunWithoutArchivesReportsNone`, `RdaInstallationSurveyRunWrongMarkerIsNotRead`, `RdaArchiveOpenWhileFileHeldOpenForWritingSucceeds` (E7; discriminates on Windows only, fact 10); `RdaInstallationSurveyRealInstallationReadsAssetAndIcon` (inconclusive without `CENSIO_ANNO_INSTALL`); manual: the Product Manager runs that test on a local build of the branch head, once with the game running, before step 9 | `RdaInstallationSurveyTests.cs`, `RdaArchiveTests.cs` |
| AC6 | check: `log.md` row "2a Discovery listing (AC6)" holds only paths, sizes and header markers | `specs/feature-21-rda-archive-spike/log.md` |

The manual runs use `dotnet test tests/Censio.GameData.Tests -c Release --filter TestCategory=AC5 --logger "console;verbosity=detailed"` on Windows with `CENSIO_ANNO_INSTALL` set to the installation folder; the orchestrator hands over this line and posts the output (paths, sizes, counts, markers only) to `log.md`.

## Approach

**Reader (`RdaArchive`).** `Open` reads the header and walks the block chain from the first block offset (fact 7),
reading each block's directory into `RdaEntry` items in archive order; it keeps the stream open for `Read` until
`Dispose`. Opening a path uses read access with `FileShare.ReadWrite | FileShare.Delete` and seeks; nothing is
written. Every value is checked before it is used, in this order, failing closed with the first kind that applies:

- magic not `Resource File V2.2` → `UnsupportedArchive` (a UTF-16 `Resource File V2.0` too); a file shorter than
  792 bytes → `CorruptArchive`;
- a block offset below 792, beyond length − 32, or seen before; more blocks than `MaxBlocks`; a next offset that is
  neither the file length nor a valid block offset → `CorruptArchive`;
- a directory not inside the file, stored or decompressed size above `MaxDirectoryBytes`, a decompressed size
  other than file count × 560, a zlib failure, or a running entry total above `MaxEntries` → `CorruptArchive`;
- a block with flag 2 adds no entries and raises `EncryptedBlockCount`; a block with flag 8 adds none; other
  flags beyond 1, 2, 4 and 8 are kept on its entries, which `Read` refuses as `UnsupportedEntry`.

`Read(path)` finds the entry by ordinal path match: none → `NotFound`, more than one → `DuplicateEntry`. A stored
entry needs compressed size = uncompressed size; a compressed entry decompresses with zlib into a buffer of the
declared size, checked against `MaxEntryBytes` before allocating; offset + stored size must lie inside the file;
fewer or more decompressed bytes than declared, or a zlib failure → `CorruptEntry`. A memory-resident entry reads
the block's 16-byte size pair, checks the decompressed data size against `MaxEntryBytes` and its location in the
file, decompresses the block's data once per `Read` (no cache in the spike) and returns the slice; a slice outside
the data → `CorruptEntry`. An `IOException` while opening or reading → `Unreadable`, likewise
`UnauthorizedAccessException`, `FileNotFoundException` and `DirectoryNotFoundException`; `Open(Stream)` refuses a
stream that cannot read and seek the same way. No other exception leaves `Open` or `Read` for archive content;
argument nulls throw `ArgumentNullException`.

Each `RdaError.Message` is a fixed English sentence naming the kind and, where useful, the block or entry index and
the bound — never archive bytes, a path inside the archive or an exception message (an exception's type name only,
for `Unreadable`). Exact wording is free for the Dev; the tests compare kinds only.

`RdaLimits.Default`: `MaxBlocks` 1,048,576; `MaxEntries` 1,048,576; `MaxDirectoryBytes` 64 MiB; `MaxEntryBytes`
512 MiB. All limits apply to sizes declared in the archive before allocation and to decompressed output while
decompressing (the stream is cut one byte past the declared size).

**Survey (`RdaInstallationSurvey.Run`).** Enumerates `*.rda` under `<installation>/maindata` recursively, ordered
by relative path (ordinal, ignoring case), and opens one archive at a time. Output lines, in this order:

1. `Archives: <n>` (or `Archives: none under maindata`, then both reads false and nothing more).
2. One line per archive: `<relative path> | entries <n> | flags <name>=<count>, … | encrypted blocks <n> | duplicate paths <n>`, or `<relative path> | error <kind>`.
3. `Matching entries:` then one line per `.xml` entry whose path contains, ignoring case, `asset`, `text`, `guid`, `region` or `session`: `<entry path> | <size> | <archive>, <archive>…` (every archive holding it, survey order), sorted by entry path.
4. `Icon folders:` then one line per folder of `.dds` entries whose path contains `icon`: `<folder> | <count> | <archive>, …`.
5. `Asset read: <archive> | <entry path> | <size> | XML` — the smallest such `.xml` entry with `asset` in its path in `config.rda`, else in any archive (ties: ordinal path); `XML` when the content starts, after an optional UTF-8 or UTF-16 byte order mark and whitespace, with `<`; otherwise `unknown marker`, or `error <kind>`, or `Asset read: none found`.
6. `Icon read: …` the same for the smallest `.dds` entry with `icon` in its path, `ui.rda` first; `DDS` when the content starts with `DDS ` (44 44 53 20).

`AssetRead`/`IconRead` are true only for the markers `XML`/`DDS`. No line holds entry content.

**Installation test.** `RdaInstallationSurveyRealInstallationReadsAssetAndIcon` reads `CENSIO_ANNO_INSTALL`; unset,
`Assert.Inconclusive`; set, it runs the survey with `RdaLimits.Default`, writes every line to `TestContext`, and
asserts both reads.

**Test helpers.** `RdaArchiveBuilder` writes synthetic V2.2 archives with invented content (blocks stored,
compressed, memory-resident, encrypted, deleted, with an unknown flag; and every malformation of AC4 as an option).
`SparseStream` is a seekable read-only stream of a given length that returns zeros outside the regions written into
it and can throw `IOException` at an offset — the 4 GiB test places a block above 4 GiB without memory for it.
Survey tests write archives into a temporary folder they delete.

## Options considered

1. **A NuGet library** — none exists (fact 1).
2. **Vendor a community reader's source** — licence and Anno 117 support could not be checked; still needs our bounds.
3. **Own V2.2 reader** — chosen (record 0043).
4. **The installation check as test code only** — its walking and picking logic would be untested and uncounted by
   coverage; a production `RdaInstallationSurvey` is tested on synthetic folders and run for real by one test.
5. **Entries read as streams instead of bounded buffers** — needed only if a needed file exceeds `MaxEntryBytes`;
   left to #23 (record 0043, *Consequences*).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `RdaArchive`, `RdaEntry`, `RdaBlockFlags`, `RdaLimits`, `RdaOpenResult`, `RdaReadResult`, `RdaError`, `RdaErrorKind`, `RdaInstallationSurvey`, `RdaSurveyReport` | new |
| Censio.GameData.Tests | `RdaArchiveTests`, `RdaInstallationSurveyTests`, `RdaArchiveBuilder`, `SparseStream` | new |

## Signatures (for the Dev's skeleton)

Namespace `Censio.GameData`. `RdaEntry` gets an internal constructor with path, size and flags plus the internal
location fields the reader needs (offset, stored size, block index); it is no API.

- `[Flags] public enum RdaBlockFlags { None = 0, Compressed = 1, Encrypted = 2, MemoryResident = 4, Deleted = 8 }`
- `public enum RdaErrorKind { UnsupportedArchive, CorruptArchive, CorruptEntry, UnsupportedEntry, NotFound, DuplicateEntry, Unreadable }`
- `public sealed class RdaError` with `public RdaError(RdaErrorKind kind, string message)`, `public RdaErrorKind Kind { get; }`, `public string Message { get; }`
- `public sealed class RdaLimits` with `public RdaLimits(int maxBlocks, int maxEntries, int maxDirectoryBytes, int maxEntryBytes)`, `public int MaxBlocks { get; }`, `public int MaxEntries { get; }`, `public int MaxDirectoryBytes { get; }`, `public int MaxEntryBytes { get; }`, `public static RdaLimits Default { get; }`
- `public sealed class RdaEntry` with `public string Path { get; }`, `public long Size { get; }`, `public RdaBlockFlags Flags { get; }`
- `public sealed class RdaArchive : IDisposable` with `public static RdaOpenResult Open(string path, RdaLimits limits)`, `public static RdaOpenResult Open(Stream stream, RdaLimits limits)`, `public IReadOnlyList<RdaEntry> Entries { get; }`, `public int EncryptedBlockCount { get; }`, `public RdaReadResult Read(string path)`, `public void Dispose()`
- `public sealed class RdaOpenResult` with `public RdaArchive? Archive { get; }`, `public RdaError? Error { get; }`, `public static RdaOpenResult FromArchive(RdaArchive archive)`, `public static RdaOpenResult FromError(RdaError error)`
- `public sealed class RdaReadResult` with `public ReadOnlyMemory<byte> Content { get; }`, `public RdaError? Error { get; }`, `public static RdaReadResult FromContent(ReadOnlyMemory<byte> content)`, `public static RdaReadResult FromError(RdaError error)`
- `public static class RdaInstallationSurvey` with `public static RdaSurveyReport Run(string installationFolder, RdaLimits limits)`
- `public sealed class RdaSurveyReport` with `public RdaSurveyReport(IReadOnlyList<string> lines, bool assetRead, bool iconRead)`, `public IReadOnlyList<string> Lines { get; }`, `public bool AssetRead { get; }`, `public bool IconRead { get; }`

`Open(Stream)` takes ownership of the stream (disposed with the archive, or on a failed open).

## Test files

`tests/Censio.GameData.Tests/RdaArchiveTests.cs`, `tests/Censio.GameData.Tests/RdaInstallationSurveyTests.cs`;
helpers `tests/Censio.GameData.Tests/RdaArchiveBuilder.cs`, `tests/Censio.GameData.Tests/SparseStream.cs`.
`RdaError`, `RdaLimits` and the result types are covered through `RdaArchiveTests`.

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

New area `game-files` (Lead decision): *Reading the game's archives in `Censio.GameData`: the archive format version
read, block and entry kinds, bounds, failure kinds, read-only shared access, where the game keeps names, texts and
icons, the installation survey* — not here: finding the installation (#22), the catalog (#23–#26).

## Documentation updates

Owned by the Dev unless named, written once the code is green:

- `docs/areas/game-files.md` (new, from `_template.md`): format V2.2 as read (fact 7, stated as the reader's
  contract), flags and what each does, the `RdaLimits.Default` values and when they apply, the failure kinds of
  E1–E6 and E8 with the encrypted-block count, read-only shared access, no content leaves the machine (link 0023);
  *Locations*: from the Product Manager's survey output (AC2) — per data kind of S2 the entry path(s) and every
  archive holding them, the game version, and "load order and winning copy: unknown (record 0043)"; the survey's
  output and `CENSIO_ANNO_INSTALL`. Related decisions 0023, 0043.
- `docs/areas/README.md`: the index row of `game-files` (fixes fact 11).
- `docs/ARCHITECTURE.md`: the `Censio.GameData` row links `areas/game-files.md`; *Security model* adds that game
  archives are opened read-only, shared with the game, and read within the reader's limits (link record 0043).
- `.squad/project.md`: *Security areas*, "Parsing of external input": replace "later game files" with the game
  archives (`RdaArchive`, `RdaInstallationSurvey`) and link `game-files`; "File paths the Companion … opens": add the
  game archives under the installation folder. *Test doubles* (Tester): `SparseStream`.
- `docs/UNIT_TESTS.md` (Tester), *Where tests live*: `CENSIO_ANNO_INSTALL`, the installation folder the survey test
  reads; unset, inconclusive.
- `docs/decisions/0043-…` (Lead): after the survey run, add what it showed if it changes a consequence.
- `README.md`, `docs/USER_GUIDE.md`, `SECURITY.md` (already lists game-file parsing): unchanged (N6).

## Architecture check

No guarantee of `.squad/project.md` changes. Game files stay local (record 0023): synthetic test archives, no
content in the survey. `Censio.GameData` stays platform-neutral `net10.0` (record 0004).

## Security considerations

- [x] Limits: `MaxBlocks`, `MaxEntries` count blocks and entries; `MaxDirectoryBytes` applies to the stored and the
  decompressed directory; `MaxEntryBytes` to the declared decompressed size before allocation and to the bytes
  decompressed (output cut one byte past the declared size). Path decoding (UTF-16, 520 bytes fixed) has no
  separate limit.
- [x] Exceptions reaching a message: `IOException`, `UnauthorizedAccessException`, `FileNotFoundException`,
  `DirectoryNotFoundException` → `Unreadable` with the exception's type name only; `InvalidDataException` from zlib
  → `CorruptArchive`/`CorruptEntry` with a fixed text. No runtime message reaches a line.

## Decision records

- `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` (Proposed), amending 0024.

## Out of scope / follow-ups

Spec *Out of scope*; #23 settles load order and streamed entries (record 0043).
