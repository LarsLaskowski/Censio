# Plan: Extract and convert icons

Source: [spec.md](spec.md)
Tier: standard — new behavior (icons read, decoded and shown) that creates, changes or removes no security control; the new parsers are input handling.
Security areas touched: Parsing of external input (a new DDS/BC7 reader; `IconFilename` read by `GameCatalogReader`); File paths the Companion opens (`ui.rda` and the patch archives, read-only); Logging and showing external data (two new catalog log lines; icons in the diagnostics window); Outbound calls (with a UNC game folder the icon archives are listed and read over SMB)
Plan size: three parsers of external formats (BC7, DDS, the icon archives' path rule), each with the accepted forms its guard tests, plus the PNG writer and the Companion display in one feature.

## Problem / root cause

Feature: see [spec.md](spec.md).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 3 | Real values: `data/ui/fhd/<rest>.png`; archives hold `data/ui/4k/<rest>_0.dds` (`ui.rda`, `zz_patchfiles_300`); sessions name none | PM samples, `log.md` 11:38 (five assets, one installation) | confirmed |
| 4 | Real icons: DDS, header size 124, flags `0xa1007`, 512×512, 10 levels, FourCC `DX10`, DXGI 99, dimension 3, array size 1; 349,700 bytes = 148 + the full BC7 chain | Lead read the four headers in the scratchpad `icon-samples` | confirmed |
| 6 | `RdaArchive.Read` matches the path ordinally; two equal paths give `DuplicateEntry` | `RdaArchive.cs` 182–205 | confirmed |
| 7 | The entry bound is checked before an entry's data is allocated; a caller may pass its own `RdaLimits` | `RdaArchive.cs` 571, 586, 643; `RdaLimits.cs` 17 | confirmed |
| 8 | `FindArchives` lists `.rda` files recursively, links skipped, as relative paths | `RdaInstallationSurvey.cs` 77–97 | confirmed |
| 11 | BCnEncoder.NET 2.3.0 is `MIT OR Unlicense`, maintained, and decodes the reserved BC7 mode to opaque magenta | tag `RELEASE-2.3.0`: `LICENSE-MIT`, `UNLICENSE`, `BCnEncoder.csproj` 12, `Bc7Block.cs` 195, 771 | confirmed |
| 12 | The BC7 format description returns all zeros for the reserved mode | to verify: Tester reads Microsoft's *BC7 format* page (or the Khronos BPTC section) before writing the test, step 5 | unverified |
| 13 | The own decoder equals the reference on the four real samples, every pixel of level 0 | to verify: the Tester builds a scratch project outside the repository with BCnEncoder.NET 2.3.0 in task 2 (it pins AC1's expected pixels) and re-runs it on the samples in task 4, after the Dev's green; differing pixel counts only | unverified |
| 14 | `ui.rda` lies directly in `maindata`, and a higher `<n>` is newer | to verify: AC4 run on the PM's installation (step 9); `ArchiveMissing` or differing copies show it | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | Bc7DecoderDecodeBlockModeMatchesReference (DataRow per mode 0–7, expected RGBA pinned from fact 13's project), Bc7DecoderDecodeBlockPartitionsMatchReference (within each mode's partition field: mode 0 partitions 0, 7, 15; modes 1, 3, 7 partitions 0, 17, 18, 63, whose second anchor is not 15 for 17 and 18; mode 2 one partition each whose second and third anchors differ from 3 and 15; each anchor confirmed against the description's tables), Bc7DecoderDecodeBlockMode4RotationAndIndexSelectionMatchReference (rotations 0–3, both selections), Bc7DecoderDecodeBlockMode5RotationMatchesReference, Bc7DecoderDecodeBlockMode6SolidWorkedByHand, Bc7DecoderDecodeBlockReservedModeGivesTransparentBlack | `tests/Censio.GameData.Tests/Bc7DecoderTests.cs` |
| AC1 | DdsReaderReadSrgbAndUnormGiveSameBytes, DdsReaderReadSizeNotMultipleOfFourIsCropped, DdsReaderReadUsesLargestLevel | `tests/Censio.GameData.Tests/DdsReaderTests.cs` |
| AC1 | PngWriterWriteRoundTripsRgbaWithAlpha (test-side chunk reader and `ZLibStream`), PngWriterWriteHasOnlyHeaderDataAndEndChunks, PngWriterWriteChunkCrcsAreValid (IEND CRC `AE426082`) | `tests/Censio.GameData.Tests/PngWriterTests.cs` |
| AC2 | GameIconReaderLoadHighestPatchNumberWins, GameIconReaderLoadUiArchiveUsedWithoutPatchCopy, GameIconReaderLoadPatchNumbersOrderedNumerically (9 before 10), GameIconReaderLoadPatchArchiveInSubfolderCounts, GameIconReaderPatchNumberOfRecognisesForms (forms in *Approach*) | `tests/Censio.GameData.Tests/GameIconReaderTests.cs` |
| AC3 | GameIconReaderLoadUiArchiveMissingFails, GameIconReaderLoadPatchArchiveUnreadableFails, GameIconReaderLoadDuplicatePatchNumberFails (`300` and `0300`), GameIconReaderLoadIconNotNamedCounted, GameIconReaderLoadIconMissingCounted, GameIconReaderLoadIconOverEntryBoundUnreadable, GameIconReaderLoadIconListedTwiceUnreadable, GameIconReaderLoadParentPathOnlyComparedWithEntries, GameIconReaderLoadAboveTotalIconBytesBoundFails and GameIconReaderLoadAtTotalIconBytesBoundKeepsIcons (small icons, one shared-data entry listed under several paths, `RdaArchiveBuilder`; `maxIconSetBytes` the `PngBytes` of a run with the default bound, minus 1 and equal), GameIconReaderMaxIconSetBytesIsQuarterGibibyte (the constant pinned at 268435456); E1's failing listing has no test: `FindArchives` reports no failure (a missing `maindata` is `ArchiveMissing`, inaccessible folders are skipped), so only the boundary catch reaches it, as in record 0047 | `tests/Censio.GameData.Tests/GameIconReaderTests.cs` |
| AC3 | DdsReaderReadRejectsMalformedHeader (DataRows: under 148 bytes, magic, header size, pixel format size, no FourCC flag, FourCC `DXT5`, DXGI 71 and 100, dimension 4, array size 2, cube flag, width 0, width and height above the bound, length one byte short and one long, level count above the chain) | `tests/Censio.GameData.Tests/DdsReaderTests.cs` |
| AC3 | GameCatalogLoaderRunLogsIconCounts, GameCatalogLoaderRunLogsIconFailureAndKeepsNames, GameCatalogLoaderRunPublishesNamesBeforeIcons, GameCatalogLoaderRunWithoutGameBuildsNoIcons | `tests/Censio.Companion.Core.Tests/GameCatalogLoaderTests.cs` |
| AC4 | GameIconReaderRealInstallationGivesEveryFixtureGoodAnIcon (opt-in, `CENSIO_ANNO_INSTALL`; inconclusive unset); manual: PM, Windows, PR head, step 9 | `tests/Censio.GameData.Tests/GameIconReaderTests.cs` |
| AC5 | DiagnosticsViewCreateSetsRegionAndGoodIcons, DiagnosticsViewCreateWithoutIconsLeavesRowsAsToday; manual: PM, Windows build of the PR head, step 9 | `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs` |
| AC6 | GameIconReaderLoadWritesNothing (the temporary installation tree is byte-identical after `Load`); check: Dev, step 7: `git ls-files` lists no `.dds` or `.png`, a `dotnet publish` of `Censio.Companion` lists none, `Directory.Packages.props` unchanged | `tests/Censio.GameData.Tests/GameIconReaderTests.cs` |
| AC7 | GameIconReaderEntryPathOfMapsFhdToFourK (forms in *Approach*), GameIconReaderLoadLookupIgnoresCase (the entry's case differs from the asset value, so a read with the mapped path fails), GameIconReaderLoadSharesIconBetweenEntries, GameIconReaderLoadSkipsWorkforceAndBuildings (`Check` gives `NoEntry`), GameIconReaderLoadAmbiguousGuidHasNoIcon | `tests/Censio.GameData.Tests/GameIconReaderTests.cs` |
| AC7 | GameCatalogReaderReadEntryCarriesIconFilename, GameCatalogReaderReadRegionEntryTakesRegionAssetIcon, GameCatalogReaderReadSessionIconBeforeRegionAssetIcon (inline XML, fixture unchanged) | `tests/Censio.GameData.Tests/GameCatalogReaderTests.cs` |

## Approach

**Catalog.** `ReadAsset` reads `Values/Standard/IconFilename` (last one wins, trimmed, empty means none) for every
asset, region assets included. The builder sets `GameCatalogEntry.IconFilename`: an entry's own value; a region entry
without one takes its region asset's.

**Icon step** (`GameIconReader.Load`, never throws; a boundary catch outside `OutOfMemoryException` gives
`ArchiveUnreadable` with the archive in progress, `maindata` before the first; the two-parameter `Load` passes
`MaxIconSetBytes` to the three-parameter one). `GameIconError.Archive` is a relative path as `FindArchives` gives it:
`UiArchiveName` for `ArchiveMissing`, the failing archive for `ArchiveUnreadable`, the second of the two in
`FindArchives` order for `DuplicateArchiveNumber` (`zz_patchfiles_300.rda` beside `zz_patchfiles_0300.rda`), the
archive being read for `IconSetTooLarge`.
1. `FindArchives(maindata)` (a throw: the boundary catch). Icon archives: the relative path `ui.rda`
   (ignoring case; absent: `ArchiveMissing`) and every file whose name `PatchNumberOf` accepts; two equal numbers:
   `DuplicateArchiveNumber` with the second path. Rank: `ui.rda` 0, then patches by number. Every comparison
   "ignoring case" in this plan is `StringComparison.OrdinalIgnoreCase` / `StringComparer.OrdinalIgnoreCase`.
2. Wanted paths: `EntryPathOf` of each good and region entry's `IconFilename`; none named: *not named*; no
   mapping: *missing*.
3. Listing pass: each archive opened in rank order with `RdaLimits(Default.MaxBlocks, Default.MaxEntries,
   Default.MaxDirectoryBytes, MaxIconEntryBytes)`, its matches per wanted path (ignoring case) recorded, closed; an open
   failure is `ArchiveUnreadable`. The winner of a path is the highest-ranked archive with a match.
4. Read pass, one archive open at a time, `Read` called with the matched `RdaEntry.Path`, never the mapped path: two
   matches in the winner, a read error, or `DdsReader.Read` returning `null` make the path *unreadable*; otherwise
   `PngWriter.Write` gives one `GameIcon` shared by every entry naming the path. An open failure here is
   `ArchiveUnreadable`; a PNG that takes the kept PNG bytes above `maxIconSetBytes` (greater than; equal keeps) ends
   the step with `IconSetTooLarge` (no icons).

`GameIconSet.Check` gives `NoEntry` for every GUID that is no good or region entry of the catalog (workforce, building,
ambiguous, unknown); `Count` counts good and region entries only.

`EntryPathOf`: trimmed, at most `MaxIconPathLength` characters, starts with `data/ui/fhd/` and ends with `.png`
(ignoring case), a non-empty rest; result `data/ui/4k/` + rest as written + `_0.dds`. Test forms: the sample form,
mixed case, surrounding white space, another prefix, a `.dds` end, `data/ui/fhd/.png`, 1,025 characters,
backslashes (no match), and `data/ui/fhd/../x.png` (maps, matches no entry). `PatchNumberOf`: name
`zz_patchfiles_<digits>.rda` ignoring case, digits only ASCII `0`–`9`, parsed as `uint` with `NumberStyles.None`.
Forms: `zz_patchfiles_300.rda` 300, `ZZ_PatchFiles_7.RDA` 7, `…_007.rda` 7, `…_4294967295.rda`, and `null` for
`…_4294967296.rda`, an empty number, `-1`, `+1`, ` 1`, `1x`, Arabic-Indic digit one (U+0661), fullwidth one (U+FF11).

`DdsReader.Read` accepts exactly: magic `DDS `, header size 124, pixel format size 32 with the FourCC flag (`0x4`),
FourCC `DX10`, DXGI 98 or 99, dimension 3, array size 1, no cube flag (`0x4` of the misc flags), width and height 1 to
`maxSide`; levels from the count when the flag `0x20000` is set (0 read as 1, above the full chain refused), else 1;
the file length exactly 148 plus the sum of every level's `ceil(w/4)·ceil(h/4)·16`, computed in `long`. Level 0 is
decoded block by block into RGBA and cropped; DXGI 98 and 99 give identical bytes. `Bc7Decoder.DecodeBlock` follows
the BC7 format description: the mode is the lowest set bit of byte 0, byte 0 of zero gives 16 transparent black
pixels; tables are typed from the description, never copied from a library's source. `PngWriter.Write`: signature,
`IHDR` (8-bit, colour type 6, no interlace), one `IDAT` (`ZLibStream`, filter 0 per row), `IEND`, an own CRC-32; no
other chunk.

**Companion.** `Run` publishes `Ready` as today, then calls `buildIcons`, logs, stores `Icons`, raises
`IconsChanged`. `App` passes `GameIconReader.Load` and calls `_diagnostics?.Notify()` on
`IconsChanged`. `DiagnosticsView` puts `icons.Find(guid)` of the session GUID and the product GUID on each row (not for
GUID `0`). The window's `Region` and `Good` columns become template columns: an image of 20 by 20
device-independent pixels, a 4-pixel gap, then the text, vertically centred; without an icon only the text, no gap. In
the `Region` column the image lies on a dark neutral disc of the same 20 by 20 (the game's region icons are light glyphs
on transparent, drawn on coloured discs in game; step 9), collapsed with the image when there is none. An
internal converter in `Censio.Companion` decodes each `GameIcon` once (decoded at 40 pixels, frozen) into a
dictionary of that window.

**Log lines** (exact): Information `Catalog icons: {GoodCount} goods and {RegionCount} regions with an icon; without
one: {NotNamedCount} not named, {MissingCount} missing, {UnreadableCount} unreadable`; Warning `Catalog icons
failed: {Kind} ({Archive})`.

## Options considered

- Decoder: BCnEncoder.NET with ImageSharp, or with an own PNG writer — lost to own code (record 0051).
- Lookup: the path as written, a search by file name, fallback to an older copy, failing the catalog on a bad icon
  (record 0050).
- Icons inside `GameCatalog` instead of a separate `GameIconSet`: lost, the names are published first and the
  catalog is immutable.
- A new area `game-icons`: lost to a section *Icons* in `game-catalog.md`, under its size limit.
- Testing the total bound at 256 MiB: lost (13.7 s, 7 % above the bound); a lower bound: lost (N2, AC4 unmeasured);
  an internal overload: lost, no project exposes internals to tests, and limits are public parameters already
  (`RdaLimits`, `DdsReader.Read`'s `maxSide`).
- Keeping `ListingFailed`: lost, nothing reaches it (record 0050).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `GameCatalogAsset`, `GameCatalogFiles`, `GameCatalogBuilder`, `GameCatalogEntry` | `IconFilename` read and resolved |
| Censio.GameData | new `GameIconReader`, `GameIcon`, `GameIconSet`, `GameIconProblem`, `GameIconError`, `GameIconErrorKind`, `GameIconResult`, `DdsReader`, `DdsImage`, `Bc7Decoder`, `PngWriter` | icon step |
| Censio.Companion.Core | `GameCatalogLoader`, `DiagnosticsRow`, `DiagnosticsView`, `DiagnosticsWindowSession`, `CompanionLog` | icons published, shown, logged |
| Censio.Companion | `App.xaml.cs`, `DiagnosticsWindow.xaml(.cs)`, new `GameIconImageConverter.cs` | wiring and display |

## Signatures (for the Dev's skeleton)

Censio.GameData, all public:
- `GameCatalogEntry.IconFilename { get; init; }` of type string?
- `public static class GameIconReader`
- `GameIconReader.MaxIconSide = 2048` (const int)
- `GameIconReader.MaxIconEntryBytes = 8388608` (const int)
- `GameIconReader.MaxIconPathLength = 1024` (const int)
- `GameIconReader.MaxIconSetBytes = 268435456` (const int, 256 MiB of kept PNG bytes)
- `GameIconReader.UiArchiveName = "ui.rda"` (const string)
- `public static GameIconResult Load(string installationFolder, GameCatalog catalog)`
- `public static GameIconResult Load(string installationFolder, GameCatalog catalog, long maxIconSetBytes)`
- `public static string? EntryPathOf(string? iconFilename)`
- `public static uint? PatchNumberOf(string fileName)`
- `public GameIcon(int width, int height, ReadOnlyMemory<byte> png)`, a sealed class
- `GameIcon.Width { get; }`, `GameIcon.Height { get; }` (int) and `GameIcon.Png { get; }` (ReadOnlyMemory of byte)
- `public GameIconSet(IReadOnlyDictionary<int, GameIcon> icons, IReadOnlyDictionary<int, GameIconProblem> problems, int goodsWithIcon, int regionsWithIcon)`, a sealed class
- `public GameIcon? Find(int assetGuid)`
- `public GameIconProblem Check(int assetGuid)`
- `public int Count(GameIconProblem problem)`
- `GameIconSet.GoodsWithIcon { get; }`, `GameIconSet.RegionsWithIcon { get; }` (int) and `GameIconSet.PngBytes { get; }` (long, the bytes of the distinct PNGs)
- `public enum GameIconProblem { None, NoEntry, NotNamed, Missing, Unreadable }` (its None-to-Unreadable order unchanged; the second member now means no good or region entry)
- `public enum GameIconErrorKind { ArchiveMissing, ArchiveUnreadable, DuplicateArchiveNumber, IconSetTooLarge }`
- `public GameIconError(GameIconErrorKind kind, string archive)`, a sealed class with `GameIconError.Kind { get; }` and `GameIconError.Archive { get; }`
- `GameIconResult.Icons { get; }` (GameIconSet?) and `GameIconResult.Error { get; }` (GameIconError?), a sealed class
- `public static GameIconResult FromIcons(GameIconSet icons)`
- `public static GameIconResult FromError(GameIconError error)`
- `public static DdsImage? Read(ReadOnlySpan<byte> file, int maxSide)`, on the static class `DdsReader`
- `public DdsImage(int width, int height, byte[] rgba)`, a sealed class with `DdsImage.Width { get; }`, `DdsImage.Height { get; }` (int) and `DdsImage.Rgba { get; }` (ReadOnlyMemory of byte)
- `Bc7Decoder.BlockBytes = 16` (const int, on the static class `Bc7Decoder`)
- `public static void DecodeBlock(ReadOnlySpan<byte> block, Span<byte> rgba)`
- `public static byte[] Write(int width, int height, ReadOnlySpan<byte> rgba)`, on the static class `PngWriter`

Censio.Companion.Core (existing constructors and overloads stay and pass no icons):
- `public GameCatalogLoader(Func<string, GameCatalogResult> build, Func<string, GameCatalog, GameIconResult>? buildIcons, ILogger logger)`
- `GameCatalogLoader.Icons { get; }` (GameIconSet?) and the event `GameCatalogLoader.IconsChanged` (EventHandler?)
- `public DiagnosticsRow(int index, IReadOnlyList<string> cells, GameIcon? regionIcon, GameIcon? goodIcon)`
- `DiagnosticsRow.RegionIcon { get; }` and `DiagnosticsRow.GoodIcon { get; }` (GameIcon?)
- `public static DiagnosticsView Create(string statusText, PipeTick? tick, GameCatalog? catalog, GameIconSet? icons, TimeProvider clock)`
- `public DiagnosticsWindowSession(Func<string> readStatusText, Func<PipeTick?> readTick, Func<GameCatalog?> readCatalog, Func<GameIconSet?> readIcons, TimeProvider clock, Action<Action> post, Func<IDiagnosticsWindow> createWindow)`
- `public static partial void CatalogIconsBuilt(ILogger logger, int goodCount, int regionCount, int notNamedCount, int missingCount, int unreadableCount)`
- `public static partial void CatalogIconsFailed(ILogger logger, GameIconErrorKind kind, string archive)`

No new name ends in Flags or Enum, and no type is nested.

## Test files

`tests/Censio.GameData.Tests/Bc7DecoderTests.cs`, `DdsReaderTests.cs`, `PngWriterTests.cs`, `GameIconReaderTests.cs`,
`GameCatalogReaderTests.cs`; `tests/Censio.Companion.Core.Tests/GameCatalogLoaderTests.cs`, `DiagnosticsViewTests.cs`.
A new helper `tests/Censio.GameData.Tests/DdsFileBuilder.cs` (Tester) wraps BC7 blocks in a DDS file; archives come
from `RdaArchiveBuilder` in a temporary `maindata`.

Existing call sites: none adapted (old constructors and overloads remain); no existing test's input is handled
differently.

Not coverable by a unit test: `src/Censio.Companion/` (`App.xaml.cs`, `DiagnosticsWindow.xaml(.cs)`,
`GameIconImageConverter.cs`) — the Windows shell, excluded already (record 0004); AC5's manual check covers it.
In `GameIconReader.cs`: the boundary catch of `Load` (no input makes `LoadCore` throw: the listing skips inaccessible
folders, `RdaArchive.Open` returns results; kept as in record 0047, record 0050) and the read pass's failed reopen of
an archive the listing pass opened (only a file changed between the passes reaches it). Entries whose GUID
`GameCatalog.Find` does not return (ambiguous, duplicate) are skipped, so icons follow the entry the window shows.

## Areas

`game-catalog` (new section *Icons*), `game-files`, `companion`; no new area.

## Documentation updates

All Dev, after the code is green, unless named otherwise:
- `docs/areas/game-catalog.md`: *Scope*; *Files and nodes* (`IconFilename` among the facts; the icon archives are
  read too); new *Icons* (entries, path rule, archives and order, formats, bounds including the total, reasons and log counts, failure
  kinds, names first, memory, privacy); *Limits and errors* (*Memory*: the total PNG bound; *Network*: icon archives over SMB); *Tests with the real
  files* (AC4's report); *Not here*; records 0050 and 0051.
- `docs/areas/game-files.md`: *Locations* row *Icons* answered (link); "the icon cache of #24 and #25" becomes #25;
  *Not here*.
- `docs/areas/companion.md`: *Diagnostics window* (icon layout; drop "icons do not exist yet"); *Log* (both lines).
- `docs/areas/README.md`: index rows of `game-catalog` (icons in scope) and `game-files` (*Not here*).
- `docs/ARCHITECTURE.md`: the `Censio.GameData` and `Censio.Companion` rows and the catalog paragraph (icons, records
  0050, 0051).
- `README.md` and `docs/USER_GUIDE.md`, English then German: icons in the diagnostics window, the two log lines, icons
  held in memory only and never saved or sent.
- `.squad/project.md` (orchestrator, from the Dev's text): *Parsing of external input* (`DdsReader`, `Bc7Decoder`,
  `GameIconReader`, `IconFilename`), *File paths* (`GameIconReader.Load`, read-only), *Logging and showing external data* (the icon lines; "the diagnostics window (which shows game names and icons)"),
  *Outbound calls* (icon archives over SMB); *Test doubles* `DdsFileBuilder` (from the Tester's text).
- `tests/Censio.GameData.Tests/Fixtures/GameCatalog/README.md` unchanged.

## Architecture check

No guarantee of `.squad/project.md` is touched: nothing is sent, stored or written; the Companion stays read-only on
the installation; Windows code stays in the shell.

## Security considerations

- [x] Limits: the icon entry bound (8 MiB stored and decompressed) applies before the entry is allocated; width and
  height (2,048) and the exact file length are checked from the header before decoding, so a decoded image holds at
  most 16 MiB; the PNGs kept stay within `MaxIconSetBytes` (256 MiB, against entries sharing one image's data); `MaxIconPathLength` applies after the XML reader read the value (bounded by the assets entry).
- [x] Exceptions: none reaches a text; the log holds the kind and the archive's relative path only.

## Decision records

- `docs/decisions/0050-icons-found-by-a-path-rule-in-the-ui-and-patch-archives-newest-copy-wins.md` (Proposed)
- `docs/decisions/0051-dds-icons-decoded-by-own-bc7-and-png-code.md` (Proposed)

## Challenge

All three minor objections accepted and built in (AC1 partitions, task 2 reference project, `Read` with the listed
path).

## Security review

- B1 accepted: `MaxIconSetBytes` (256 MiB) bounds the kept PNGs; reaching it withholds every icon
  (`IconSetTooLarge`; why in record 0050; spec E1, N2; AC3 test). An AC4 `PngBytes` above a quarter of it reopens it.
- N1, N3, N4 accepted: spec AC4 (GUIDs and counts, no entry path); *Documentation updates*; *Approach*.
- Note on `IconSetTooLarge` accepted: its `Archive` is the archive being read (*Approach*). The bound became a
  parameter of `Load` for its test; the two-parameter `Load` keeps 256 MiB (pinned).

## Challenge notes

1. **minor (AC1 partition tests):** The plan's test `Bc7DecoderDecodeBlockPartitionsMatchReference` uses partitions 0, 13 and 63 for modes 0, 1, 2, 3 and 7. That choice has two gaps that the in-repository tests would not catch: Partition 63 does not exist in mode 0. Mode 0 has a 4-bit partition field, so its partitions are 0–15. In the BC7 format description's table of anchor indices for two-subset partitions, the second anchor of partitions 0, 13 and 63 is 15, as it is for every partition below 16. So modes 1, 3 and 7 never test an anchor that is not 15, which is the entry most likely to be typed wrong. Instruction to the Tester: choose partitions per mode within that mode's partition field (mode 0: 0–15). For modes 1, 3 and 7, add at least one partition whose second anchor is not 15 (for example 17 or 18). For mode 2, add at least one partition whose second and third anchors differ from the most common values. Confirm each choice against the description's anchor tables before pinning.
2. **minor (fact 13 versus tasks 2 and 4):** The AC1 rows pin expected pixels from "fact 13's project". Fact 13 places that project in the step after the Dev's tests pass ("step 6 (after the Dev's green)"), which is task 4. But task 2, which writes the tests first, already needs those expected values. Instruction: the Tester builds the BCnEncoder.NET 2.3.0 scratch project outside the repository in task 2 and pins the synthetic-block expectations there. Task 4 only re-runs it on the four real samples and reports counts only. Nothing from that project enters the repository.
3. **minor (read pass, Approach step 4):** The lookup ignores case, but `RdaArchive.Read` (`RdaArchive.cs` 182–205) compares paths ordinally. If `Read` is called with the mapped path (`data/ui/4k/<rest>_0.dds` as `EntryPathOf` writes it), any difference in case returns `NotFound`. Instruction to the Dev: call `Read` with the matched entry's own `RdaEntry.Path` from the listing pass, never with the mapped path. `GameIconReaderLoadLookupIgnoresCase` should give the archive entry a case that differs from the asset value, so the test fails if the mapped path is used.

## Security notes

- (Security, plan delta, non-blocking) The plan does not say what `GameIconError.Archive` holds for `IconSetTooLarge`. That value fills `{Archive}` in the warning "Catalog icons failed: {Kind} ({Archive})". Smallest fix: add to step 4 of *Approach* the sentence "for `IconSetTooLarge`, `Archive` is the relative path of the archive being read", so the log line still contains only a relative archive path.

## Out of scope / follow-ups

As the spec; the cache (#25) and the format (#26) carry `GameIconSet` on.
