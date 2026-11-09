# Plan: Locate the game installation

Source: [spec.md](spec.md)
Tier: security — besides input handling (bounds, parser validation), skipping network and device paths from Ubisoft Connect's records confines what a path reaches, a guard (raised from `standard` in the revision for Security's N1).
Security areas touched: Parsing of external input (the settings file, Ubisoft Connect's install records); File paths the Companion opens (the settings file, the probed installation files); Logging external data (the game folder in the log); `.squad/project.md` *Security areas* extended (not narrowed)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Nothing finds the installation; only the survey takes a folder, from `CENSIO_ANNO_INSTALL` | `src/Censio.GameData/RdaInstallationSurvey.cs:37` `Run(string installationFolder, …)`; `docs/UNIT_TESTS.md`, layout block | confirmed |
| 2 | Ubisoft Connect's `InstallDir` value has the form `E:/Anno 117 - Pax Romana/`: forward slashes, trailing slash | Samples `game-installation.md`, line 1, and `ubisoft-connect-installs.reg` (both under `tests/Censio.Companion.Core.Tests/Fixtures/GameInstallation/` once the Tester moved them, *Test files*); no private value (checked field by field: drives, the games' folder names, `de-DE`, Ubisoft's game ids, a version, the Windows build) | confirmed |
| 3 | The records are subkeys of `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs` named by a number, each with string values `InstallDir` and `Language`; one subkey per Ubisoft game, not only Anno 117 (`4553` is `E:/Anno 1800/`, `921` is `E:/Anno 117 - Pax Romana/`) | `ubisoft-connect-installs.reg` (`reg export`, UTF-16 LE with byte order mark) | confirmed |
| 3a | Record names compare as numbers: by text `4553` sorts before `921`, by number after | the sample's two names; *Approach*, step 2 orders digit-only names by length, then ordinal | confirmed |
| 3b | Windows treats a path that starts with `\` or `/` as fully qualified when its second character is `\`, `/` or `?`; `\??\` is the same prefix as `\\?\` | .NET `PathInternal.Windows.IsPartiallyQualified` and `IsExtended` (Security, B3) | confirmed |
| 4 | Ubisoft Connect's default games folder is `C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games`, changeable by the user | Sample, line 3 (the Product Manager's statement) | confirmed |
| 5 | The program file is `bin\win64\anno117.exe`; its fixed file version is `26.29905.4.51797`, its version strings are empty | Sample: path line and `VersionInfo` output (`FileMajorPart` … `FilePrivatePart`, `FileVersion :` empty) | confirmed |
| 6 | The main-menu version `3.0.1733841` is shown in the main menu only, not in Ubisoft Connect or the program file | Sample, lines 5 and last | confirmed |
| 7 | The program file's version changes with every game update | to verify: after merge, the Product Manager compares the menu line before and after the next game update (record 0046) | unverified |
| 8 | An installation holds `maindata\config.rda` (lower case) | `docs/areas/game-files.md`, *Locations* (survey run) | confirmed |
| 9 | The log writer sanitizes and cuts every message, so a logged folder needs no own sanitizing | `src/Censio.Companion.Core/LogLine.cs:50` (`Sanitize(message, MaxMessageLength)`) | confirmed |
| 10 | The Companion has no settings file today | `README.md:144` "The Companion has no settings yet."; no JSON read in `src/Censio.Companion*` | confirmed |
| 11 | `TrayMenu.Create` builds the menu; the shell calls it once, the tests twelve times | `src/Censio.Companion.Core/TrayMenu.cs` `Create`; `src/Censio.Companion/TrayIcon.cs:133`; `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` lines 97, 122, 143, 162, 188, 209, 228, 251, 280, 299, 315, 331 | confirmed |
| 12 | The shell builds a disabled menu item for every entry with `IsEnabled == false` and wires clicks by kind only | `src/Censio.Companion/TrayIcon.cs:139-150` | confirmed |
| 13 | `App.OnStartup` runs before the mode switch in every mode, and `Run` starts a background task on the exit token without awaiting it | `src/Censio.Companion/App.xaml.cs:326-332`, `:339-385` | confirmed |
| 14 | The autostart command has no arguments, so a command-line switch would be lost | `docs/areas/companion.md`, *Start with Windows* | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameInstallationSearchUbisoftRecordReturnsFound` (a temporary path with a trailing separator, the part of the sample's form Linux can show; the `E:/…/` form stays with AC8), `GameInstallationSearchSeveralRecordsTakesFirstValidByNumber` (names `9`, `10`, `abc`, `2` with `2` invalid), `GameInstallationSearchNoValidRecordFindsDefaultGamesFolder`, `GameInstallationSearchNoGameFolderRunsSearch` (DataRow: no file, `""`, `"  "`, no key) | `tests/Censio.Companion.Core.Tests/GameInstallationSearchTests.cs` |
| AC1 | `TrayMenuCreateGameLineBelowLastTick`, `TrayMenuFormatGameFoundShowsBuildAndSource` (both sources) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC1 | `GameInstallationIsInstallationNeedsConfigAndProgramFile` (DataRow: both, only one, none), `GameInstallationDefaultUbisoftGamesFolderIsUnderProgramFiles` | `tests/Censio.Companion.Core.Tests/GameInstallationTests.cs` |
| AC2 | `GameInstallationSearchSettingsFolderSkipsRecords` (records double counts no read) | `GameInstallationSearchTests.cs` |
| AC2 | `CompanionSettingsReadReturnsGameFolder`, `CompanionSettingsReadAcceptsBomCommentsAndTrailingCommas`, `CompanionSettingsReadKeyIgnoresCase`, `CompanionSettingsReadIgnoresOtherKeys` | `tests/Censio.Companion.Core.Tests/CompanionSettingsTests.cs` |
| AC2 | `CompanionFoldersSettingsIsUnderCensio` | `tests/Censio.Companion.Core.Tests/CompanionFoldersTests.cs` |
| AC3 | `GameInstallationSearchSettingsFolderProblemReturnsKind` (DataRow: missing folder, relative, `C:`-style drive only, `%USERPROFILE%\x`, folder without the two files), `GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord`, `GameInstallationSearchInvalidSettingsFileReturnsInvalid` | `GameInstallationSearchTests.cs` |
| AC3 | `CompanionSettingsReadInvalidReturnsInvalid` (DataRow: broken JSON, root array, number, `null`, key twice, key twice in other case, byte `0xFF` inside the `gameFolder` value, inside the `gameFolder` key, inside another key, `"\uD800"` as the value), `CompanionSettingsReadAcceptsInvalidBytesOutsideNames` (`0xFF` inside a comment and inside another key's value), `CompanionSettingsReadTooLargeReturnsInvalid` (DataRow: 1,048,577 bytes, a sparse file of 3 GiB made with `FileStream.SetLength`; 1,048,576 accepted), `CompanionSettingsReadUnreadableReturnsInvalid` (a folder named `settings.json`), `CompanionSettingsReadMissingFileReturnsNoGameFolder` | `CompanionSettingsTests.cs` |
| AC3 | `TrayMenuFormatGameProblemShowsText` (DataRow per problem kind, exact texts of spec E1–E4) | `TrayMenuTests.cs` |
| AC4 | `GameInstallationSearchRecordsThrowSkipsToDefaultFolder`, `GameInstallationSearchUnusableRecordIsSkipped` (DataRow: `InstallDir` null, relative, missing folder, no installation, `/` + a temporary installation's path, so it starts with `//`), `GameInstallationSearchSampleRecordsTakeNumericFirst` (the sample's records through `RegExportUbisoftInstallRecords`, each `InstallDir` replaced by a temporary folder kept by record name: both installations → `921`'s folder; only `4553`'s → `4553`'s), `GameInstallationSearchNothingFoundReturnsNotFoundAndLogsWarning`, `GameInstallationSearchDefaultFolderUnreadableReturnsNotFound` | `GameInstallationSearchTests.cs` |
| AC4 | `GameInstallationIsNetworkOrDevicePathDetectsSeparatorPairs` (DataRow true: `\\srv\share\`, `//srv/share/`, `\/srv/share/`, `/\srv\share\`, `\??\UNC\srv\share\`, `\\?\C:\x`, `\\.\pipe\x`, `/?/x`; false: `C:\x`, `E:/Anno 117 - Pax Romana/`, `/tmp/x`, `\x`, empty) | `GameInstallationTests.cs` |
| AC4 | `RegExportUbisoftInstallRecordsReadsSample` (the sample gives exactly `4553` → `E:/Anno 1800/` and `921` → `E:/Anno 117 - Pax Romana/`, values verbatim) | `tests/Censio.Companion.Core.Tests/RegExportUbisoftInstallRecordsTests.cs` |
| AC4 | `TrayMenuFormatGameNotFoundShowsText` | `TrayMenuTests.cs` |
| AC5 | `GameInstallationSearchBuildIsFileVersion` (two versions give two texts, the same version the same text), `GameInstallationSearchUnusableVersionIsUnknown` (DataRow: null, `0.0.0.0`, reader throws) | `GameInstallationSearchTests.cs` |
| AC5 | `GameInstallationFormatBuildWritesFourNumbers` | `GameInstallationTests.cs` |
| AC5 | `TrayMenuFormatGameUnknownBuildShowsText` | `TrayMenuTests.cs` |
| AC6 | `GameInstallationSearchStatusIsSearchingBeforeRun`, `GameInstallationSearchRunWritesNothing` (file listing, lengths, contents and `LastWriteTimeUtc` of the temporary installation and settings file unchanged; never the access time), `GameInstallationSearchUnexpectedExceptionEndsNotFound` (the records double throws `NotSupportedException`, the default folder is missing: final status `NotFound`, never `Searching`) | `GameInstallationSearchTests.cs` |
| AC6 | `TrayMenuFormatGameSearchingShowsText` | `TrayMenuTests.cs` |
| AC6 | manual: the Product Manager, within AC8's run — menu usable and `Exit` at once while `Game: searching` would show, every mode starts the search (a start with `--replay` and with an invalid switch show a `Game:` line) | — |
| AC7 | `GameInstallationSearchFoundLogLineIsSanitized` (folder name with `\u0007`, the logged message passed through `LogLine.Format` holds `?`), `GameInstallationSearchLogHoldsNoFileContent` | `GameInstallationSearchTests.cs` |
| AC8 | manual: end to end through the registry reader, the version reader and the shell (the key itself is fact 3); the Product Manager on Windows, local publish of the branch head with the command of `README.md` (*Build the Companion from source*): without a settings file the menu shows `Game: build 26.29905.4.51797 (Ubisoft Connect)` (or the build of the day) and the log names `E:/Anno 117 - Pax Romana/`; with `{"gameFolder": "E:\\missing"}` it shows `Game: folder in settings not found`, with `"C:"` and with `"%USERPROFILE%\\x"` `Game: folder in settings is not a full path`; and `Get-Acl 'HKLM:\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs'` is run and its owners and write rights reported | — |
| AC9 | manual: the Reviewer, step 8 — README and user guide, English then German, hold every item of spec N7 | — |

## Approach

**Settings file** (`CompanionSettings.Read`): a path that is not fully qualified (local app data unresolved), a
missing file or folder: no game folder. Opened read-only, sharing read, write and delete; at most 1,048,577 bytes are
read, more than 1,048,576 is invalid (raw bytes, before decoding). A leading `EF BB BF` is stripped by the code (not
left to the parser). `JsonDocument` with comments skipped and trailing commas allowed. It decodes names and strings
only when they are read, and then throws `InvalidOperationException` for invalid UTF-8 or a lone surrogate (measured by
the Devil's Advocate on .NET 10): parsing, reading every property name and reading the `gameFolder` value happen in one
`try` that turns `JsonException` and `InvalidOperationException` into `not valid JSON`. So a name that cannot be
decoded makes the file invalid under any key, while invalid bytes in a comment or in another key's value are
accepted. Root not an object: invalid. Properties named `gameFolder` ignoring case (ordinal): more than one invalid, a non-string invalid (`null` included), a blank string no game folder,
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
   first, by length then ordinal — numeric without overflow, `921` before `4553` (fact 3a) —, the others after them,
   ordinal. The first record whose `InstallDir` is fully qualified, is no network or device path, and is an
   installation is found. `GameInstallation.IsNetworkOrDevicePath(path)` is true when the first character is `\` or `/`
   and the second is `\`, `/` or `?`, both counted as separators on every system (fact 3b); after the fully-qualified
   check only the drive form `X:\` / `X:/` is left on Windows, while `/tmp/…` passes on the Linux test host.
3. Default games folder (fully qualified only): its top-level folders, ordinal ignoring case; `IOException`,
   `UnauthorizedAccessException` and `SecurityException` count as none. The first installation is found.
4. Otherwise `NotFound`, warning.
On found, the version reader is asked for the program file (any exception → unknown) and the information line is
logged. The status is published through a `volatile` field. `Run` always ends in a final status: any exception that
escapes the steps is caught at the top, logged with `GameInstallationNotFound` and its exception, and gives `NotFound`.

**Wiring** (shell): `App.OnStartup`, before the mode switch, creates the search with `RegistryUbisoftInstallRecords`,
`WindowsFileVersionReader`, `GameInstallation.DefaultUbisoftGamesFolder(ProgramFilesX86)` and the logger, and starts
`search.Run(CompanionFolders.Settings(localAppData))` through `Run`; `TrayIcon` reads `search.Status` each time the
menu opens. `RegistryUbisoftInstallRecords` opens `HKLM` in the 32-bit view at `SOFTWARE\Ubisoft\Launcher\Installs`
(the same key as fact 3 from a 64-bit process), read-only, and returns each subkey's name with `InstallDir` when it is
a plain string (`REG_SZ`, as in the sample; read unexpanded), else null — an expandable string included (Lead decision,
step 8: fail closed as built and approved by Security); a subkey that throws gives null. `WindowsFileVersionReader`
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
  `GameInstallation.IsNetworkOrDevicePath(string path)` (bool),
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
  `CompanionLog.GameInstallationNotFound(ILogger logger, Exception? exception)`, `CompanionLog.GameSettingsFolderMissing(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFolderNotFullPath(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFolderNotInstallation(ILogger logger, string folder)`,
  `CompanionLog.GameSettingsFileInvalid(ILogger logger, string file, string problem, Exception? exception)`.

`Censio.Companion` (internal sealed): `RegistryUbisoftInstallRecords`, `WindowsFileVersionReader`; the constructor
`TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<GameInstallationStatus> game, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)`.

Files the skeleton rewrites: `src/Censio.Companion.Core/TrayMenu.cs`, `TrayMenuEntryKind.cs`, `CompanionFolders.cs`, `CompanionLog.cs`, `src/Censio.Companion/TrayIcon.cs`, `App.xaml.cs`.

## Test files

`tests/Censio.Companion.Core.Tests/CompanionSettingsTests.cs`, `GameInstallationTests.cs`, `GameInstallationSearchTests.cs` (new); `TrayMenuTests.cs`, `CompanionFoldersTests.cs` (extended). New doubles: `FakeUbisoftInstallRecords.cs` (queued records or an exception, counts reads), `FakeFileVersionReader.cs` (a version per path or an exception) and `RegExportUbisoftInstallRecords.cs` (an `IUbisoftInstallRecords` over a `reg export` file: byte order mark detected, one record per subkey of `…\Ubisoft\Launcher\Installs`, its `"InstallDir"="…"` string with `\\` and `\"` unescaped, other values ignored; tested by `RegExportUbisoftInstallRecordsTests.cs`), all in `tests/Censio.Companion.Core.Tests/`. Installations are built in a temporary folder and deleted afterwards.

Fixtures: the Tester moves both samples from `tests/Censio.GameData.Tests/Fixtures/GameInstallation/` to `tests/Censio.Companion.Core.Tests/Fixtures/GameInstallation/` (the layout keeps a fixture in the project whose tests read it; the orchestrator stages both paths), adds `<None Update="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />` to `Censio.Companion.Core.Tests.csproj` as in `Censio.Pipe.Tests.csproj`, and the tests read the `.reg` file unchanged.

Existing call sites of a changed signature: the twelve `TrayMenu.Create` calls in `TrayMenuTests.cs` — the Tester adapts them (pass `GameInstallationStatus.Searching`), and the entry-order assertions gain the `Game` line; `TrayIcon.cs:133` — the Dev.

Code a unit test cannot cover: `RegistryUbisoftInstallRecords.cs`, `WindowsFileVersionReader.cs` and the changes to `App.xaml.cs` and `TrayIcon.cs` — Windows shell without a test project, already outside the coverage measure (`.github/workflows/ci.yml:71`); covered by AC8. No new exclusion.

## Areas

- New area `game-installation` (`docs/areas/game-installation.md`, Lead decision): scope "Finding the Anno 117
  installation folder and its build: the sources and their order, what counts as an installation, the build, the
  failures; not the settings file itself (companion) or the archives (game-files)". Content: *Approach* above, UNC
  folders from the settings file probed over the network, "no UNC or device path written in a record is followed"
  (mapped drive letters and directory symlinks still are), the record key and the numeric order (facts 3, 3a),
  fact 7 as not verified, records 0044 and 0046.
- `docs/areas/companion.md`: *Tray icon and menu* (the `Game:` entry, position, texts), new *Settings file* section
  (path, format, tolerated forms, bound, invalid cases — a name that cannot be decoded makes the whole file invalid
  under any key, invalid bytes in a comment or another key's value are accepted —, never written; record 0045), *Log* (the new lines, the
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
- `.squad/project.md`: *Security areas* — *Parsing of external input* gains the settings file (`CompanionSettings`)
  and Ubisoft Connect's records (`RegistryUbisoftInstallRecords`, `GameInstallationSearch`) with links to
  `game-installation.md` and `companion.md` (*Settings file*); *File paths the Companion writes, prunes or opens*
  gains the settings file (`CompanionFolders.Settings`, `CompanionSettings`) and the installation probes and program
  file (`GameInstallation`, `WindowsFileVersionReader`, read-only) with a link; *Logging and showing external data*
  gains the game folder in `CompanionLog`; *Outbound calls* says that a UNC folder the player names in the settings
  file is probed over the network; *Integration surface* —
  the option `gameFolder` in `CompanionSettings` with `CompanionSettingsTests`, the interfaces `IUbisoftInstallRecords`
  and `IFileVersionReader`.

Tester: `.squad/project.md` *Test doubles* (the three doubles); `tests/Censio.Companion.Core.Tests/Fixtures/GameInstallation/README.md`
describing both samples in English (source, date, what each line shows, no private value; the `.reg` read by
`RegExportUbisoftInstallRecordsTests` and the search tests, `game-installation.md` by no test).

## Architecture check

Windows APIs (registry, `FileVersionInfo`) stay in `Censio.Companion` behind interfaces in `Censio.Companion.Core`
(record 0004). No guarantee of `.squad/project.md` is touched: nothing is sent, stored or written; `Exit` stays within
2 seconds because the search is never awaited. The architecture's component flow is unchanged; the rows and two
sentences above are updated by the Dev.

## Security considerations

- [x] The 1 MiB bound applies to the raw bytes before decoding; `JsonDocument`'s default depth of 64 applies.
- [x] No exception text reaches the menu: every menu text is fixed. The settings file's read failure is logged with its
  exception (may name a path, as `companion.md` *Log* already accepts).
- Accepted forms: from the settings file every fully qualified path, the player's own choice; a UNC path there is
  probed as written over the network (SMB), which `game-installation.md` and `.squad/project.md` (*Outbound calls*)
  state. From a record no UNC or device path: every form of fact 3b (`\\`, `//`, `\/`, `/\`, `\\?\`, `\??\`, `\\.\`, `/?`) is
  skipped by `GameInstallation.IsNetworkOrDevicePath`, so only a drive path is left; a mapped drive letter or a
  directory symlink is still followed. Who can
  write the records key is checked in AC8 (`Get-Acl`). Nothing found is written or executed; only `anno117.exe` is
  opened, by Windows' version reader, to read its version resource.

## Decision records

- `docs/decisions/0044-game-installation-found-through-ubisoft-connect-or-the-settings-file.md` (Proposed)
- `docs/decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md` (Proposed)
- `docs/decisions/0046-game-build-is-the-program-files-version.md` (Proposed)

`decision-check.py` reports the area `game-installation` as unknown until the Dev adds its index row (G4).

## Out of scope / follow-ups

- After merge: the Product Manager compares the `Game:` line before and after the next game update (fact 7, record
  0046); a verification issue in step 10.

## Challenge

Devil's Advocate (1 major, 4 minor):
- M1 (wrong exception for undecodable names and values): accepted. *Approach* catches `InvalidOperationException`
  with `JsonException` in one `try` over parsing, names and value; `Run` always ends in a final status; DataRows and
  `GameInstallationSearchUnexpectedExceptionEndsNotFound` added; `GameInstallationNotFound` takes the exception.
- m1 (access time): accepted; `GameInstallationSearchRunWritesNothing` compares contents and `LastWriteTimeUtc` only.
- m2 (`%NAME%` inside a fully qualified path): narrowed spec E2 — a variable is never expanded; a path that starts with
  one is not fully qualified (E2), one elsewhere is taken literally and is usually not found (E1); the user guide
  says variables are not expanded. No new rule.
- m3 (twelve calls): accepted; fact 11, *Test files* and task 8.
- m4 (drive-letter form on Linux): accepted; the AC1 test shows the trailing separator, the `E:/…/` form stays with AC8.

## Security review

- B1: accepted, as M1, plus the `companion.md` wording on undecodable names and accepted bytes in comments and other
  values, and the DataRows for another key.
- B2: accepted; *Documentation updates* names the concrete types per security area, and *Security considerations*
  says that only `anno117.exe` is opened, by Windows' version reader.
- N1: taken. Records: paths starting with `\\` or `//` are skipped (test DataRow). Settings file: UNC accepted as the
  player's choice and documented as a network probe in `game-installation.md` and `.squad/project.md`. The unevidenced
  "administrator-writable" is dropped; AC8 runs `Get-Acl` on the key.
- N2: taken; a 3 GiB sparse-file DataRow.
- N3: taken; AC8 tries `"C:"` and `"%USERPROFILE%\\x"`.
- B3 (2nd `CHANGES_REQUIRED`; Lead decision: accepted and fixed as Security supplied it, one delta confirmation):
  the rule is a separator followed by a separator or `?` (fact 3b), in `GameInstallation.IsNetworkOrDevicePath` with
  its own DataRows (AC4); "no UNC or device path written in a record" replaces "local". The real `.reg` export
  arrived in the same round: fact 3 confirmed, fact 3a and the numeric-order test from the sample added.
