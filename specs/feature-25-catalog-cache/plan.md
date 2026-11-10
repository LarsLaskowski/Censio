# Plan: Catalog cache and re-extraction after game updates

Source: Issue #25 | [spec.md](spec.md)
Tier: standard — new behavior (a cache, checks, tray texts) that reads a new local file as untrusted input with bounds and validation (input handling) but creates no guard on what an attacker may reach.
Security areas touched: Parsing of external input (the cache file, `GameCatalogCacheFile.Read`); File paths the Companion writes, prunes or opens (the cache folder, its temporary files); Logging and showing external data (new catalog log lines; cached PNG data reaching WPF in the diagnostics window); Outbound calls (the `maindata` listing over SMB at each check)

## Problem / root cause

See [spec.md](spec.md).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The catalog and icons are built at every start, never cached | `App.xaml.cs:427`, `GameCatalogLoader.Run`; `game-catalog.md` *Memory only* | confirmed |
| 2 | Without a catalog the window shows bare GUIDs and the tray `Catalog: failed` (the issue's fallback exists) | `DiagnosticsView` (`_catalog` null), `TrayMenu.FormatCatalog` | confirmed |
| 3 | An icon failure is only logged, not shown in the tray | `GameCatalogLoader.BuildIcons`; `TrayMenu.FormatCatalog` has no icon state | confirmed |
| 4 | Each new pipe connection raises `StatusChanged` with `Connected` before any read | `PipeConnection.RunSessionAsync` (`SetStatus(Connected)`), events only on change; `pipe-connection.md` *States* | confirmed |
| 5 | The archives are listed recursively, links skipped, inaccessible folders ignored, ordered ignoring case | `RdaInstallationSurvey.FindArchives` (internal) | confirmed |
| 6 | `GameCatalog` does not expose its ambiguous GUIDs, `GameIconSet` not its icon and problem maps; both constructors take them | `GameCatalog.cs:38`, `GameIconSet.cs:31` | confirmed |
| 7 | `PngWriter` writes signature, one `IHDR` (8 bit, colour type 6, 0/0/0), one `IDAT`, `IEND`, each with CRC | `PngWriter.Write` | confirmed |
| 8 | The product version is `0.0.0` on `main` and the informational version carries no commit | `Directory.Build.props:7-8` | confirmed |
| 9 | `Module.ModuleVersionId` of `Censio.GameData` and `Censio.Companion.Core` is equal for two builds of one source, differs after a code change, and is readable in the single-file release exe | to verify: Dev, step 6, in a scratch worktree — build twice and compare, change one statement and compare; the single-file exe by the Product Manager's AC7 run (a second start that loads the cache) | unverified |
| 10 | A game update through Ubisoft Connect changes the size or modification time of at least one archive below `maindata` | to verify: Product Manager, after merge, at the next game update (log shows `game changed`) | unverified |
| 11 | The log's `Catalog cannot name` once-per-run set and 1,000 cap are per process | `GameCatalogReporter` `_reported`, `_isCapped` | confirmed |
| 12 | Record 0023 (released in v0.1.0) already decides a cache and re-extraction; 0047, 0050 point to #25 for the cache and need no edit | records read; `git tag --contains` | confirmed |

`File.Move` atomicity is not relied upon: a torn or mixed file fails the checksum (record 0053).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogLoaderRunWithMatchingCacheBuildsNothing`, `GameCatalogLoaderRunWithMatchingCacheLogsLoadLineOnly`; `GameCatalogCacheFileReadRoundTripsCatalogAndIcons`, `GameCatalogCacheFileReadSharedIconStaysShared`, `GameCatalogCacheFileReadNonSeekableStreamLoads` | `tests/Censio.Companion.Core.Tests/GameCatalogLoaderTests.cs`, `tests/Censio.GameData.Tests/GameCatalogCacheFileTests.cs` |
| AC2 | `GameCatalogKeyIsSameGameDiffers` (data rows: build, added, removed, length, time, folder; `unknown` equals `unknown`; case of a path ignored), `GameCatalogCacheFileReadOtherIdentityReturnsCompanionChanged`, `GameCatalogCacheFileReadOtherGameReturnsGameChanged`, `GameCatalogLoaderRunWithChangedGameExtractsAndLogsReason` (rows `missing`, `game changed`, `Companion changed`), `GameCatalogLoaderRunWithUnreadableKeyExtracts`, `GameCatalogLoaderRunStoresKeyTakenBeforeExtraction`; `GameArchiveStateReaderReadListsSizeAndTime`, `GameArchiveStateReaderReadMissingFileThrows`; `GameCatalogKeyReaderReadUnreadableReturnsNull`; `CompanionIdentityCurrentHoldsBothModuleIds` | `GameCatalogLoaderTests.cs`, `GameCatalogKeyTests.cs`, `GameCatalogCacheFileTests.cs` (GameData), `GameArchiveStateReaderTests.cs`, `tests/Censio.Companion.Core.Tests/GameCatalogKeyReaderTests.cs`, `CompanionIdentityTests.cs` |
| AC3 | `GameCatalogLoaderConnectedUnchangedDoesNothing` (after success and after each of E1 to E3), `GameCatalogLoaderConnectedChangedKeepsNamesUntilPublished`, `GameCatalogLoaderConnectedChangedRefreshesBuild`, `GameCatalogLoaderConnectedUnreadableKeepsCatalogAndWarns`, `GameCatalogLoaderConnectedDuringRunIsMerged`, `GameCatalogLoaderConnectedBeforeStartRunIsIgnored`, `GameCatalogLoaderOtherStatesDoNotCheck`, `GameCatalogLoaderConnectedNoGameDoesNothing`; `GameInstallationSearchRefreshBuildRereadsVersion`, `GameInstallationSearchRefreshBuildNotFoundKeepsStatus` | `GameCatalogLoaderTests.cs`, `GameInstallationSearchTests.cs` |
| AC4 | `TrayMenuFormatCatalogNewStates` (one row per text), `GameCatalogLoaderRunReportsStatusSequence` (no cache, cache, icons failed, not cached) | `TrayMenuTests.cs`, `GameCatalogLoaderTests.cs` |
| AC5 | `GameCatalogLoaderRunCatalogFailedCachesNothing`, `GameCatalogLoaderRunIconsFailedCachesNothing`, `GameCatalogLoaderRunCacheNotWritableKeepsCatalog`, `GameCatalogLoaderRunNoGameLeavesCacheFile`; `GameCatalogCacheStoreSaveFolderIsFileReturnsFailure`, `GameCatalogCacheStoreSaveNotFullPathReturnsFailure`, `GameCatalogCacheFileWriteAboveBoundReturnsFalse` | `GameCatalogLoaderTests.cs`, `GameCatalogCacheStoreTests.cs`, `GameCatalogCacheFileTests.cs` |
| AC6 | `GameCatalogCacheFileReadInvalidReturnsInvalid` (rows: truncated at every section, flipped byte (checksum), trailing byte, wrong magic, other format version, length above 320 MiB refused unread, catalog section above 64 MiB, string above its bound, invalid UTF-8, undefined kind or problem, duplicate GUID, icon index out of range, PNG bytes above 256 MiB; per icon: signature, `IHDR` size, side 0 and 2,049, bit depth, colour type, extra chunk, chunk order, CRC, size different from the stored size); `GameCatalogCacheStoreLoadMissingReturnsMissing`, `GameCatalogCacheStoreSaveLeavesNoTemporaryFile`, `GameCatalogCacheStoreSaveTwiceKeepsOneCompleteFile`, `GameCatalogCacheStoreSavePrunesOnlyOldTemporaryFiles`; `GameCatalogLoaderRunInvalidCacheWarnsAndExtracts` | `GameCatalogCacheFileTests.cs`, `GameCatalogCacheStoreTests.cs`, `GameCatalogLoaderTests.cs` |
| AC7 | `GameCatalogLoaderRunLogsExtractionDurations`, `GameCatalogLoaderRunLogsCacheLoadDuration` (`FakeTimeProvider` advanced inside the fakes, one decimal, decimal point under `de-DE`); `GameCatalogReporterNewCatalogReportsAgain`, `GameCatalogReporterNewCatalogResetsCap`; `CompanionLogCatalogReportsCappedText`; manual: the Product Manager runs a local build (`dotnet publish` command of the user guide) of the branch head on his installation before step 9 — first start (extraction), second start (cache load, which also shows fact 9 for the single-file exe), the two duration lines copied from the log | `GameCatalogLoaderTests.cs`, `GameCatalogReporterTests.cs`, `CompanionLogTests.cs` |
| AC8 | check: the Reviewer reads the README and user-guide passages listed under *Documentation updates* against the code, in English and German; manual: the measured durations of the AC7 run, written in by the Dev | — |

## Approach

**Key.** `GameCatalogKeyReader.Read` builds a `GameCatalogKey` from the found installation: `CompanionIdentity.Current`
(the two module version ids, `D` format, joined by `;`), the folder, the build of the status, and
`GameArchiveStateReader.Read` (the archives of `FindArchives`, each relative path, `FileInfo.Length` and
`LastWriteTimeUtc`). Any exception of the listing or a vanished file gives `null` (unreadable). `IsSameGame` compares
folder and paths ignoring case, the build as text (`null` equals `null`), lengths and times exactly, in order.

**Loader.** One background flow per check, guarded by one flag: busy from construction until the start `Run` ends, and
during each later check; `OnConnectionStatusChangedAsync` returns a completed task unless the state is `Connected`, the
flag is free and the last installation was found, takes the flag synchronously, then runs the check on `Task.Run`.
- Start (`Run`): `NoGame` as today. Else `Checking`; read the key; null → log `game changed`, extract; else
  `cache.Load(key)`: `Loaded` → publish catalog, then icons, status `Ready`, log the load line; `Missing`, `GameChanged`,
  `CompanionChanged` → log the reason, extract; `Invalid` → Warning, extract.
- Later check: refresh the build through the delegate (the `Game:` line follows), read the key; null → keep everything,
  Warning `Catalog check failed; the current catalog is kept`; same game as the last key (successful or failed) →
  nothing; else extract (the cache is not consulted: it holds the last key at most).
- Extract: keep the key read before; status `ExtractingNames` (the shown catalog and icons stay); build; failure →
  publish `Failed` with no catalog (icons cleared); success → publish the catalog with `ExtractingIcons` (icons cleared,
  as today), log the existing catalog lines, build icons; failure → `IconsFailed`, no cache; success → publish icons,
  log the icon line, `cache.Save`; failure → `NotCached` and the Warning with its exception; else `Ready`. The
  duration line follows the icon line whenever a catalog was built; times from `TimeProvider.GetTimestamp`, formatted
  `0.0` with the invariant culture before they reach the logger.
- The last key and result are replaced at each extraction or load, whatever its outcome.

**Cache file** (`GameCatalogCacheFile`, record 0053), little-endian, read forward through a SHA-256 incremental hash:
magic `CENSIOCC`, format version 1, identity, key, the catalog section (entry count, per entry the GUID, kind, the two
names, region GUID, region types, icon file name; ambiguous GUIDs; the two counts), the icon section (distinct PNGs
with width and height, then per GUID an icon index or a problem; the two counts), then the 32-byte hash. Bounds,
checked before each allocation: file at most `MaxFileBytes` (checked against the length before opening the reader),
everything but PNG data at most `MaxCatalogBytes`, PNG data together at most `GameIconReader.MaxIconSetBytes`, each
string at most 65,536 bytes before decoding (strict UTF-8), each count at most the bytes left. Identity and game are
compared right after the key; a mismatch stops the read with its kind. Nothing is returned before the hash matched and
nothing follows it. Per icon the checks of spec N3 plus every chunk CRC. `Write` returns false, writing nothing more,
when a bound would be exceeded.

**Store** (`GameCatalogCacheStore`): folder from `CompanionFolders.Cache`; not fully qualified → every save fails with
no exception, every load is `Missing`. Load opens `catalog.bin` with `FileShare.Read | FileShare.Delete`. Save creates
the folder, deletes files named exactly `catalog-<32 lowercase hex>.tmp` older than 24 hours, writes
`catalog-<new GUID N>.tmp`, flushes to disk, moves it over `catalog.bin`, and deletes its own temporary file on any
failure. A file of any other name is never deleted.

**Reporter.** Remembers the catalog instance it last reported against; a different instance clears the set and the cap.
The cap line becomes `Catalog: more than 1000 GUIDs cannot be named; further ones are not logged for this catalog`.

**Layout of new user-visible text:** the menu texts as in spec AC4 (`Catalog: extracting icons, 412 entries`); log
lines as in the spec: `Catalog cache not used: <missing|game changed|Companion changed>` (Information), `Catalog cache
not used: invalid`, `Catalog cache not written`, `Catalog check failed; the current catalog is kept` (Warnings),
`Catalog extracted in 12.3 s (names 1.4 s, icons 10.9 s)`, `Catalog loaded from cache in 0.8 s: 1234 entries, 300
goods and 12 regions with an icon` (Information).

## Options considered

The key, the identity and the trigger: record 0052 (hashes, build only, product version, exe hash, file watching,
process watching lost). The file: record 0053 (JSON, a PNG folder, #26's format lost). Removing the names at once
during a re-extraction lost in the spec challenge (objection 2). An interface over the store lost: a temporary
folder serves the tests, and a folder that is a file gives a real write failure.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.GameData | `GameArchiveState`, `GameArchiveStateReader`, `GameCatalogKey`, `GameCatalogCacheFile`, `GameCatalogCacheResult`, `GameCatalogCacheResultKind` | new |
| Censio.GameData | `GameCatalog`, `GameIconSet` | expose ambiguous GUIDs, icon and problem maps |
| Censio.Companion.Core | `CompanionIdentity`, `GameCatalogKeyReader`, `GameCatalogCacheStore`, `GameCatalogCacheWriteResult` | new |
| Censio.Companion.Core | `GameCatalogLoader`, `GameCatalogStatusKind`, `TrayMenu`, `CompanionLog`, `CompanionFolders`, `GameCatalogReporter`, `GameInstallationSearch` | changed |
| Censio.Companion | `App.xaml.cs` | wiring: store, key reader, clock, `connection.StatusChanged` → `OnConnectionStatusChangedAsync` through `Run` |

## Signatures (for the Dev's skeleton)

Properties are get-only unless marked init; types in parentheses.

Censio.GameData:
- `GameArchiveState` (a sealed record) with required init properties `GameArchiveState.Path` (string), `GameArchiveState.Length` (long), `GameArchiveState.LastWriteTimeUtc` (DateTime)
- `public static class GameArchiveStateReader`, `public static IReadOnlyList<GameArchiveState> Read(string installationFolder)`
- `GameCatalogKey` (sealed class), `public GameCatalogKey(string identity, string folder, string? build, IReadOnlyList<GameArchiveState> archives)`, properties `GameCatalogKey.Identity` (string), `GameCatalogKey.Folder` (string), `GameCatalogKey.Build` (string?), `GameCatalogKey.Archives` (IReadOnlyList of GameArchiveState), `public bool IsSameGame(GameCatalogKey other)`
- `public enum GameCatalogCacheResultKind` with `GameCatalogCacheResultKind.Loaded`, `GameCatalogCacheResultKind.Missing`, `GameCatalogCacheResultKind.CompanionChanged`, `GameCatalogCacheResultKind.GameChanged`, `GameCatalogCacheResultKind.Invalid`
- `GameCatalogCacheResult` (sealed class), properties `GameCatalogCacheResult.Kind`, `GameCatalogCacheResult.Catalog` (GameCatalog?), `GameCatalogCacheResult.Icons` (GameIconSet?), `public static GameCatalogCacheResult FromContent(GameCatalog catalog, GameIconSet icons)`, `public static GameCatalogCacheResult FromKind(GameCatalogCacheResultKind kind)`
- `public static class GameCatalogCacheFile`, int or long constants `GameCatalogCacheFile.FormatVersion` = 1, `GameCatalogCacheFile.MaxFileBytes` = 335544320, `GameCatalogCacheFile.MaxCatalogBytes` = 67108864, `GameCatalogCacheFile.MaxStringBytes` = 65536, `public static bool Write(Stream destination, GameCatalogKey key, GameCatalog catalog, GameIconSet icons)`, `public static GameCatalogCacheResult Read(Stream source, long length, GameCatalogKey expected)`
- `GameCatalog.AmbiguousGuids` (IReadOnlyCollection of int); `GameIconSet.Icons` (IReadOnlyDictionary of int to GameIcon), `GameIconSet.Problems` (IReadOnlyDictionary of int to GameIconProblem)

Censio.Companion.Core:
- `public static string Cache(string localApplicationData)` in `CompanionFolders`
- `public static class CompanionIdentity`, property `CompanionIdentity.Current` (string)
- `public static class GameCatalogKeyReader`, `public static GameCatalogKey? Read(GameInstallationStatus installation, string identity)` (named `GameCatalogKeyReader.Read` in the skeleton row)
- `GameCatalogCacheWriteResult` (sealed class), required init `GameCatalogCacheWriteResult.IsWritten` (bool), init `GameCatalogCacheWriteResult.Failure` (Exception?)
- `GameCatalogCacheStore` (sealed class), const `GameCatalogCacheStore.FileName` = "catalog.bin", `public GameCatalogCacheStore(string folder)`, `public GameCatalogCacheResult Load(GameCatalogKey expected)`, `public GameCatalogCacheWriteResult Save(GameCatalogKey key, GameCatalog catalog, GameIconSet icons)`
- `public GameCatalogLoader(Func<string, GameCatalogResult> build, Func<string, GameCatalog, GameIconResult> buildIcons, Func<GameInstallationStatus, GameCatalogKey?> readKey, GameCatalogCacheStore cache, TimeProvider clock, ILogger logger)`, `public Task OnConnectionStatusChangedAsync(PipeConnectionStatus status, Func<GameInstallationStatus> refreshInstallation)`
- Deleted: both existing `GameCatalogLoader` constructors are removed.
- `GameCatalogStatusKind.Checking`, `GameCatalogStatusKind.ExtractingNames`, `GameCatalogStatusKind.ExtractingIcons`, `GameCatalogStatusKind.IconsFailed`, `GameCatalogStatusKind.NotCached`
- `public GameInstallationStatus RefreshBuild()` in `GameInstallationSearch`
- In `CompanionLog`: `CatalogCacheNotUsed(ILogger logger, string reason)`, `CatalogCacheInvalid(ILogger logger)`, `CatalogCacheNotWritten(ILogger logger, Exception? exception)`, `CatalogCheckFailed(ILogger logger)`, `CatalogExtracted(ILogger logger, string seconds, string nameSeconds, string iconSeconds)`, `CatalogLoadedFromCache(ILogger logger, string seconds, int count, int goodCount, int regionCount)`, each a public static partial void method

No new name ends in Flags or Enum, and none is nested.

## Test files

New: `tests/Censio.GameData.Tests/GameArchiveStateReaderTests.cs`, `GameCatalogKeyTests.cs`, `GameCatalogCacheFileTests.cs`;
`tests/Censio.Companion.Core.Tests/GameCatalogCacheStoreTests.cs`, `GameCatalogKeyReaderTests.cs`, `CompanionIdentityTests.cs`.
Extended: `GameCatalogLoaderTests.cs`, `TrayMenuTests.cs`, `GameCatalogReporterTests.cs`, `CompanionLogTests.cs`,
`GameInstallationSearchTests.cs`, `CompanionFoldersTests.cs` (`CompanionFoldersCacheIsUnderCensio`). Icons for the
cache tests are `PngWriter` output of invented pixels; names are invented.

Existing call sites: the 11 `new GameCatalogLoader(...)` in `GameCatalogLoaderTests.cs` — the Tester adapts them in step
5 (a store in a temporary folder, `FakeTimeProvider`); their expectations stay except the status sequence, which the
new criteria cover. `TrayMenuTests` rows for `Catalog: reading`, `<n> entries`, `no game`, `failed` stay.

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs` (the Windows shell, no test project, record 0004) —
its wiring only; the decisions sit in Core.

## Areas

- `game-catalog.md`: new *Cache* section (key, identity, checks, last result, complete results only, file layout in
  outline, bounds, checks, failures, writes and pruning); *Memory only*, *Privacy*, *Time* (measured durations),
  *Network* and *Not here* revised; scope row in `docs/areas/README.md`.
- `companion.md`: `Catalog:` texts, log lines (new ones, the cap text, per-catalog reporting), the
  cache folder under *Folders*, *Not here*.
- `game-installation.md`: the build is read again at each later check; *Search order*'s "counts only after a restart"
  narrowed.

## Documentation updates

All by the Dev, after the code is green:
- The three area documents above and the index row of `game-catalog` (drop "the icon cache" from *Not here*).
- `docs/ARCHITECTURE.md`: line 59 ("in memory (never saved or sent)") → cached, with records 0052 and 0053; the
  `Censio.Companion.Core` row adds the catalog cache.
- `README.md`: the Companion bullet (line 13) and the names-and-icons bullet (19), the quick-start menu line (105); the
  German counterparts (178, 184, 270).
- `docs/USER_GUIDE.md`: *Game catalog* table and text (197–208) with the texts, the cache folder, deleting it (E10),
  private, stays after removal; storage paragraph (310); log contents (310); *Reporting problems* (318: do not attach
  the cache); German 518–529, 631 and the German *Probleme melden*; the durations from the AC7 run in *Game catalog*.
- `.squad/project.md` (orchestrator, text from the Dev): *Security areas* — "later the cache" becomes the cache folder
  (`CompanionFolders.Cache`, `GameCatalogCacheStore`) and the cache file under parsing (`GameCatalogCacheFile`);
  *Outbound calls* — the SMB reads now happen at an extraction, the listing at each check; *Integration surface* — no
  new item.
- The PR template asks for no further document (no changelog).

## Architecture check

No guarantee of `.squad/project.md` changes. The game files stay read-only; names and icons leave memory only for the
player's own local app data (record 0023 allows the cache), never the network. Platform-neutral logic stays in Core
(record 0004); the cache reader fails closed (record 0033).

## Security considerations

- [x] Limits apply to raw bytes before decoding: the file length before opening the reader, each string's byte length
  before UTF-8 decoding, the catalog and PNG totals as read.
- [x] Exceptions: none reaches a user-visible text. Read exceptions map to `Invalid`; write exceptions reach only the
  Warning `Catalog cache not written` with the exception in the log (as other warnings; it may hold the profile path).

## Decision records

- `docs/decisions/0052-catalog-cache-keyed-by-file-properties-and-companion-identity.md` (Proposed)
- `docs/decisions/0053-catalog-cache-one-binary-file-validated-whole-before-use.md` (Proposed)

## Out of scope / follow-ups

As in the spec. No follow-up issue.
