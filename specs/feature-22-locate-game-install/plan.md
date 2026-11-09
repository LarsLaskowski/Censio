# Plan: Locate the game installation

Source: [spec.md](spec.md)
Tier: standard — a new behavior (search, settings file, menu line) that creates no security control; its bounds and validation are input handling.
Security areas touched: Parsing of external input (the settings file, Ubisoft Connect's install records); File paths the Companion opens (the settings file, the probed installation files); Logging external data (the game folder in the log); `.squad/project.md` *Security areas* extended (not narrowed)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Nothing finds the installation; only the survey takes a folder, from `CENSIO_ANNO_INSTALL` | `src/Censio.GameData/RdaInstallationSurvey.cs:37` `Run(string installationFolder, …)`; `docs/UNIT_TESTS.md`, layout block | confirmed |
| 2 | Ubisoft Connect's `InstallDir` value has the form `E:/Anno 117 - Pax Romana/`: forward slashes, trailing slash | Sample `tests/Censio.GameData.Tests/Fixtures/GameInstallation/game-installation.md`, line 1; no private value in it (checked field by field: a drive, the game's folder name, a version, the Windows build) | confirmed |
| 3 | The records are subkeys of `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs`, each with a string value `InstallDir` | to verify: AC8, the Product Manager on his machine, step 9 (the sample quotes the value line in `reg export` form but no key; the key is the one the escalation asked to export) | unverified |
| 4 | Ubisoft Connect's default games folder is `C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games`, changeable by the user | Sample, line 3 (the Product Manager's statement) | confirmed |
| 5 | The program file is `bin\win64\anno117.exe`; its fixed file version is `26.29905.4.51797`, its version strings are empty | Sample: path line and `VersionInfo` output (`FileMajorPart` … `FilePrivatePart`, `FileVersion :` empty) | confirmed |
| 6 | The main-menu version `3.0.1733841` is shown in the main menu only, not in Ubisoft Connect or the program file | Sample, lines 5 and last | confirmed |
| 7 | The program file's version changes with every game update | to verify: after merge, the Product Manager compares the menu line before and after the next game update (record 0046) | unverified |
| 8 | An installation holds `maindata\config.rda` (lower case) | `docs/areas/game-files.md`, *Locations* (survey run) | confirmed |
| 9 | The log writer sanitizes and cuts every message, so a logged folder needs no own sanitizing | `src/Censio.Companion.Core/LogLine.cs:50` (`Sanitize(message, MaxMessageLength)`) | confirmed |
| 10 | The Companion has no settings file today | `README.md:144` "The Companion has no settings yet."; no JSON read in `src/Censio.Companion*` | confirmed |
| 11 | `TrayMenu.Create` builds the menu; the shell calls it once, the tests ten times | `src/Censio.Companion.Core/TrayMenu.cs` `Create`; `src/Censio.Companion/TrayIcon.cs:133`; `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs:97…299` | confirmed |
| 12 | The shell builds a disabled menu item for every entry with `IsEnabled == false` and wires clicks by kind only | `src/Censio.Companion/TrayIcon.cs:139-150` | confirmed |
| 13 | `App.OnStartup` runs before the mode switch in every mode, and `Run` starts a background task on the exit token without awaiting it | `src/Censio.Companion/App.xaml.cs:326-332`, `:339-385` | confirmed |
| 14 | The autostart command has no arguments, so a command-line switch would be lost | `docs/areas/companion.md`, *Start with Windows* | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameInstallationSearchUbisoftRecordReturnsFound` (record `InstallDir` in the sample's form: forward slashes, trailing separator), `GameInstallationSearchSeveralRecordsTakesFirstValidByNumber` (names `9`, `10`, `abc`, `2` with `2` invalid), `GameInstallationSearchNoValidRecordFindsDefaultGamesFolder`, `GameInstallationSearchNoGameFolderRunsSearch` (DataRow: no file, `""`, `"  "`, no key) | `tests/Censio.Companion.Core.Tests/GameInstallationSearchTests.cs` |
| AC1 | `TrayMenuCreateGameLineBelowLastTick`, `TrayMenuFormatGameFoundShowsBuildAndSource` (both sources) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC1 | `GameInstallationIsInstallationNeedsConfigAndProgramFile` (DataRow: both, only one, none), `GameInstallationDefaultUbisoftGamesFolderIsUnderProgramFiles` | `tests/Censio.Companion.Core.Tests/GameInstallationTests.cs` |
| AC2 | `GameInstallationSearchSettingsFolderSkipsRecords` (records double counts no read) | `GameInstallationSearchTests.cs` |
| AC2 | `CompanionSettingsReadReturnsGameFolder`, `CompanionSettingsReadAcceptsBomCommentsAndTrailingCommas`, `CompanionSettingsReadKeyIgnoresCase`, `CompanionSettingsReadIgnoresOtherKeys` | `tests/Censio.Companion.Core.Tests/CompanionSettingsTests.cs` |
| AC2 | `CompanionFoldersSettingsIsUnderCensio` | `tests/Censio.Companion.Core.Tests/CompanionFoldersTests.cs` |
| AC3 | `GameInstallationSearchSettingsFolderProblemReturnsKind` (DataRow: missing folder, relative, `C:`-style drive only, `%USERPROFILE%\x`, folder without the two files), `GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord`, `GameInstallationSearchInvalidSettingsFileReturnsInvalid` | `GameInstallationSearchTests.cs` |
| AC3 | `CompanionSettingsReadInvalidReturnsInvalid` (DataRow: broken JSON, root array, number, `null`, key twice, key twice in other case, invalid UTF-8 bytes `\xFF`), `CompanionSettingsReadTooLargeReturnsInvalid` (1,048,577 bytes; 1,048,576 accepted), `CompanionSettingsReadUnreadableReturnsInvalid` (a folder named `settings.json`), `CompanionSettingsReadMissingFileReturnsNoGameFolder` | `CompanionSettingsTests.cs` |
| AC3 | `TrayMenuFormatGameProblemShowsText` (DataRow per problem kind, exact texts of spec E1–E4) | `TrayMenuTests.cs` |
| AC4 | `GameInstallationSearchRecordsThrowSkipsToDefaultFolder`, `GameInstallationSearchUnusableRecordIsSkipped` (DataRow: `InstallDir` null, relative, missing folder, no installation), `GameInstallationSearchNothingFoundReturnsNotFoundAndLogsWarning`, `GameInstallationSearchDefaultFolderUnreadableReturnsNotFound` | `GameInstallationSearchTests.cs` |
| AC4 | `TrayMenuFormatGameNotFoundShowsText` | `TrayMenuTests.cs` |
| AC5 | `GameInstallationSearchBuildIsFileVersion` (two versions give two texts, the same version the same text), `GameInstallationSearchUnusableVersionIsUnknown` (DataRow: null, `0.0.0.0`, reader throws) | `GameInstallationSearchTests.cs` |
| AC5 | `GameInstallationFormatBuildWritesFourNumbers` | `GameInstallationTests.cs` |
| AC5 | `TrayMenuFormatGameUnknownBuildShowsText` | `TrayMenuTests.cs` |
| AC6 | `GameInstallationSearchStatusIsSearchingBeforeRun`, `GameInstallationSearchRunWritesNothing` (file listing and timestamps of the temporary installation and settings file unchanged) | `GameInstallationSearchTests.cs` |
| AC6 | `TrayMenuFormatGameSearchingShowsText` | `TrayMenuTests.cs` |
| AC6 | manual: the Product Manager, within AC8's run — menu usable and `Exit` at once while `Game: searching` would show, every mode starts the search (a start with `--replay` and with an invalid switch show a `Game:` line) | — |
| AC7 | `GameInstallationSearchFoundLogLineIsSanitized` (folder name with `\u0007`, the logged message passed through `LogLine.Format` holds `?`), `GameInstallationSearchLogHoldsNoFileContent` | `GameInstallationSearchTests.cs` |
| AC8 | manual: the Product Manager on Windows, local publish of the branch head with the command of `README.md` (*Build the Companion from source*): without a settings file the menu shows `Game: build 26.29905.4.51797 (Ubisoft Connect)` (or the build of the day) and the log names `E:/Anno 117 - Pax Romana/`; with `{"gameFolder": "E:\\missing"}` it shows `Game: folder in settings not found` | — |
| AC9 | manual: the Reviewer, step 8 — README and user guide, English then German, hold every item of spec N7 | — |

## Approach

**Settings file** (`CompanionSettings.Read`): a path that is not fully qualified (local app data unresolved), a
missing file or folder: no game folder. Opened read-only, sharing read, write and delete; at most 1,048,577 bytes are
read, more than 1,048,576 is invalid (raw bytes, before decoding). A leading `EF BB BF` is stripped by the code (not
left to the parser). `JsonDocument` with comments skipped and trailing commas allowed; `JsonException` and
`ArgumentException` (invalid UTF-8) are invalid. Root not an object: invalid. Properties named `gameFolder` ignoring
case (ordinal): more than one invalid, a non-string invalid (`null` included), a blank string no game folder,
otherwise the value as written. Other I/O failures: invalid, with the exception. The problem texts are fixed:
`not valid JSON`, `not a JSON object`, `gameFolder is not a text`, `gameFolder occurs more than once`, `larger than
1 MiB`, `cannot be read`.

**Installation**: a folder `f` is one when `f/maindata/config.rda` and `f/bin/win64/anno117.exe` exist as files
(`Path.Combine` with the segments, so a trailing separator and forward slashes work). The build is
`Major.Minor.Build.Revision` of the reader's `Version`, invariant culture; null or all zero is unknown.

**Search** (`GameInstallationSearch.Run(settingsFile)`, synchronous; `Status` is `Searching` until it returns):
1. Settings invalid: `SettingsFileInvalid`, warning. A game folder: not fully qualified → `SettingsFolderNotFullPath`;
   `Directory.Exists` false → `SettingsFolderNotFound`; not an installation → `SettingsFolderNotInstallation`;
   each with a warning and no further step. Otherwise found, source `SettingsFile`.
2. Records: `IUbisoftInstallRecords.Read()`; any exception counts as no records. Order: names of ASCII digits only
   first, by length then ordinal (no overflow), the others after them, ordinal. The first record whose `InstallDir` is
   fully qualified and an installation is found.
3. Default games folder (fully qualified only): its top-level folders, ordinal ignoring case; `IOException`,
   `UnauthorizedAccessException` and `SecurityException` count as none. The first installation is found.
4. Otherwise `NotFound`, warning.
On found, the version reader is asked for the program file (any exception → unknown) and the information line is
logged. The status is published through a `volatile` field.

**Wiring** (shell): `App.OnStartup`, before the mode switch, creates the search with `RegistryUbisoftInstallRecords`,
`WindowsFileVersionReader`, `GameInstallation.DefaultUbisoftGamesFolder(ProgramFilesX86)` and the logger, and starts
`search.Run(CompanionFolders.Settings(localAppData))` through `Run`; `TrayIcon` reads `search.Status` each time the
menu opens. `RegistryUbisoftInstallRecords` opens `HKLM` in the 32-bit view at `SOFTWARE\Ubisoft\Launcher\Installs`
(the same key as fact 3 from a 64-bit process), read-only, and returns each subkey's name with `InstallDir` when it is
a string or expandable string (not expanded), else null; a subkey that throws gives null. `WindowsFileVersionReader`
returns `new Version(FileMajorPart, FileMinorPart, FileBuildPart, FilePrivatePart)` of `FileVersionInfo`.

**Menu layout**: one disabled entry of kind `Game` directly after `LastTick`, before the first separator. Texts:
`Game: searching`, `Game: build <build> (Ubisoft Connect)`, `Game: build <build> (settings file)`, `Game: build
unknown (<source>)`, `Game: not found`, and the four texts of spec E1–E4.

**Log lines** (`CompanionLog`): information `Game installation found through {Source}, build {Build}, folder
{Folder}` (`Ubisoft Connect` / `settings file`, `unknown`); warnings `Game installation not found; checked the
Ubisoft Connect records and its default games folder`, `The game folder {Folder} in the settings file does not
exist`, `… is not a full path`, `… is not an Anno 117 installation; expected the folder that holds
maindata\config.rda and bin\win64\anno117.exe`, and `The settings file {File} is invalid: {Problem}` with the
exception where there is one.

## Options considered

- Stores, override and message surface: record 0044 (Steam, only a typed folder, a dialog).
- Settings in a command-line switch or the registry instead of a file: record 0045.
- The main-menu version or no version instead of the program file's version: record 0046.
- The search in `Censio.GameData` instead of `Censio.Companion.Core`: lost — it needs the Companion's settings file,
  log and tray, and the integration surface keeps the interfaces to Windows in `Censio.Companion.Core`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `CompanionSettings`, `CompanionSettingsResult`, `CompanionSettingsResultKind` | new |
| Censio.Companion.Core | `GameInstallation`, `GameInstallationSearch`, `GameInstallationStatus`, `GameInstallationStatusKind`, `GameInstallationSource`, `UbisoftInstallRecord`, `IUbisoftInstallRecords`, `IFileVersionReader` | new |
| Censio.Companion.Core | `CompanionFolders`, `CompanionLog`, `TrayMenu`, `TrayMenuEntryKind` | changed |
| Censio.Companion | `RegistryUbisoftInstallRecords`, `WindowsFileVersionReader` | new |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs` | changed |

## Signatures (for the Dev's skeleton)

Types in plain words, one declaration per code span; all public unless marked.

`Censio.Companion.Core`:
- `CompanionFolders.Settings(string localApplicationData)` returns string.
- enum `CompanionSettingsResultKind` (NoGameFolder, GameFolder, Invalid).
- sealed class `CompanionSettingsResult`: get-only `CompanionSettingsResult.Kind`, `CompanionSettingsResult.GameFolder`
  (string?), `CompanionSettingsResult.Problem` (string?), `CompanionSettingsResult.Exception` (Exception?); factories
  `CompanionSettingsResult.NoGameFolder()`, `CompanionSettingsResult.FromGameFolder(string folder)`,
  `CompanionSettingsResult.FromProblem(string problem, Exception? exception)`.
- static class `CompanionSettings`: constants `CompanionSettings.MaxFileLength` (int, 1048576) and
  `CompanionSettings.GameFolderKey` (value gameFolder); `CompanionSettings.Read(string path)` returns
  CompanionSettingsResult.
- enum `GameInstallationSource` (SettingsFile, UbisoftConnect); enum `GameInstallationStatusKind` (Searching, Found,
  NotFound, SettingsFolderNotFound, SettingsFolderNotFullPath, SettingsFolderNotInstallation, SettingsFileInvalid).
- sealed record `GameInstallationStatus`: init properties `GameInstallationStatus.Kind` (required),
  `GameInstallationStatus.Source` (GameInstallationSource?), `GameInstallationStatus.Folder` (string?),
  `GameInstallationStatus.Build` (string?); static get-only `GameInstallationStatus.Searching`.
- static class `GameInstallation`: `GameInstallation.IsInstallation(string folder)` (bool),
  `GameInstallation.ProgramFilePath(string folder)` (string), `GameInstallation.DefaultUbisoftGamesFolder(string programFilesX86)`
  (string), `GameInstallation.FormatBuild(Version? version)` (string?).
- sealed record `UbisoftInstallRecord`: init properties `UbisoftInstallRecord.Name` (required string),
  `UbisoftInstallRecord.InstallDir` (string?).
- interface `IUbisoftInstallRecords` with `IUbisoftInstallRecords.Read()` returning IReadOnlyList of UbisoftInstallRecord.
- interface `IFileVersionReader` with `IFileVersionReader.ReadVersion(string filePath)` returning Version?.
- sealed class `GameInstallationSearch`: constructor `GameInstallationSearch(IUbisoftInstallRecords records, IFileVersionReader versions, string defaultGamesFolder, ILogger logger)`,
  get-only `GameInstallationSearch.Status`, `GameInstallationSearch.Run(string settingsFile)` returning
  GameInstallationStatus.
- `TrayMenuEntryKind.Game` (new member); `TrayMenu.FormatGame(GameInstallationStatus status)` (string); the
  replacement of the current `Create`: `TrayMenu.Create(string version, TraySnapshot snapshot, GameInstallationStatus game, bool isAutostartEnabled, TimeProvider clock)`.
- `CompanionLog` partial methods (logger first): `CompanionLog.GameInstallationFound(ILogger logger, string source, string build, string folder)`,
  `CompanionLog.GameInstallationNotFound(ILogger logger)`, `CompanionLog.GameSettingsFolderMissing(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFolderNotFullPath(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFolderNotInstallation(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFileInvalid(ILogger logger, string file, string problem, Exception? exception)`.

`Censio.Companion` (internal sealed): `RegistryUbisoftInstallRecords`, `WindowsFileVersionReader`; the constructor
`TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<GameInstallationStatus> game, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)`.

Files the skeleton rewrites: `src/Censio.Companion.Core/TrayMenu.cs`, `TrayMenuEntryKind.cs`, `CompanionFolders.cs`, `CompanionLog.cs`, `src/Censio.Companion/TrayIcon.cs`, `App.xaml.cs`.

## Test files

`tests/Censio.Companion.Core.Tests/CompanionSettingsTests.cs`, `GameInstallationTests.cs`, `GameInstallationSearchTests.cs` (new); `TrayMenuTests.cs`, `CompanionFoldersTests.cs` (extended). New doubles: `FakeUbisoftInstallRecords.cs` (queued records or an exception, counts reads) and `FakeFileVersionReader.cs` (a version per path or an exception), both in `tests/Censio.Companion.Core.Tests/`. Installations are built in a temporary folder and deleted afterwards.

Existing call sites of a changed signature: the ten `TrayMenu.Create` calls in `TrayMenuTests.cs` — the Tester adapts them (pass `GameInstallationStatus.Searching`), and the entry-order assertions gain the `Game` line; `TrayIcon.cs:133` — the Dev.

Code a unit test cannot cover: `RegistryUbisoftInstallRecords.cs`, `WindowsFileVersionReader.cs` and the changes to `App.xaml.cs` and `TrayIcon.cs` — Windows shell without a test project, already outside the coverage measure (`.github/workflows/ci.yml:71`); covered by AC8. No new exclusion.

## Areas

- New area `game-installation` (`docs/areas/game-installation.md`, Lead decision): scope "Finding the Anno 117
  installation folder and its build: the sources and their order, what counts as an installation, the build, the
  failures; not the settings file itself (companion) or the archives (game-files)". Content: *Approach* above, the
  facts 3 and 7 as not verified, records 0044 and 0046.
- `docs/areas/companion.md`: *Tray icon and menu* (the `Game:` entry, position, texts), new *Settings file* section
  (path, format, tolerated forms, bound, invalid cases, never written; record 0045), *Log* (the new lines, the
  folder at every start), *Limits and errors* (the menu line is the only message), *Related decisions* 0044–0046,
  *Not here* (settings no longer "later"; link game-installation).
- `docs/areas/game-files.md`: *Scope* and *Not here* link `game-installation.md` instead of "#22".
- `docs/areas/README.md`: index row of `game-installation`; the companion row's scope gains the settings file and the
  game line.

## Documentation updates

Dev, once the code is green:
- `README.md` (English and German): the menu list of *Companion* step 2 gains the `Game:` line; *Configuration*
  replaces "The Companion has no settings yet" with the settings file, its key, an example and a link to the user
  guide.
- `docs/USER_GUIDE.md` (English and German): the menu lists in *Companion* and *First start*; a new section *Game
  installation* after *Connecting to the game*: search order, the settings file with an example (backslashes doubled),
  the Steam note, a table of every `Game:` text with what to do, that the build differs from the main-menu version;
  *Data and logs*: the log holds the game folder at every start.
- `docs/ARCHITECTURE.md`: the `Censio.Companion.Core` row (game installation search, settings file) and the
  `Censio.Companion` row (registry reader of Ubisoft Connect's records, file version reader); *Configuration*: the
  Companion's settings file (record 0045); *Security model*: the settings file and the records are untrusted input,
  read-only (record 0044).
- `SECURITY.md`: the scope list gains the settings file and Ubisoft Connect's install records (read only).
- `.squad/project.md`: *Security areas* — parsing gains `CompanionSettings` and `GameInstallationSearch`, file paths
  gain the settings file and the probed installation files, logging gains the game folder; *Integration surface* —
  the option `gameFolder` in `CompanionSettings` with `CompanionSettingsTests`, the interfaces `IUbisoftInstallRecords`
  and `IFileVersionReader`.

Tester: `.squad/project.md` *Test doubles* (the two fakes); `tests/Censio.GameData.Tests/Fixtures/GameInstallation/README.md`
describing the sample in English (source, date, what each line shows, no private value; read by no test).

## Architecture check

Windows APIs (registry, `FileVersionInfo`) stay in `Censio.Companion` behind interfaces in `Censio.Companion.Core`
(record 0004). No guarantee of `.squad/project.md` is touched: nothing is sent, stored or written; `Exit` stays within
2 seconds because the search is never awaited. The architecture's component flow is unchanged; the rows and two
sentences above are updated by the Dev.

## Security considerations

- [x] The 1 MiB bound applies to the raw bytes before decoding; `JsonDocument`'s default depth of 64 applies.
- [x] No exception text reaches the menu: every menu text is fixed. The settings file's read failure is logged with its
  exception (may name a path, as `companion.md` *Log* already accepts).
- Accepted forms: the settings folder is the player's own; UNC and `\\?\` paths pass as fully qualified and are only probed
  with `File.Exists`/`Directory.Exists`. Records come from `HKLM` (administrator-writable). Nothing found is
  opened, written or executed by this feature.

## Decision records

- `docs/decisions/0044-game-installation-found-through-ubisoft-connect-or-the-settings-file.md` (Proposed)
- `docs/decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md` (Proposed)
- `docs/decisions/0046-game-build-is-the-program-files-version.md` (Proposed)

`decision-check.py` reports the area `game-installation` as unknown until the Dev adds its index row (G4).

## Out of scope / follow-ups

- After merge: the Product Manager compares the `Game:` line before and after the next game update (fact 7, record
  0046); a verification issue in step 10.
