# Plan: Companion settings window

Source: Issue #43 | [spec.md](spec.md)
Tier: security — the Companion starts writing the file that holds the ingest token (storage of a secret), gets a new
reader of an external file and starts its own exe again.
Security areas touched: Parsing of external input (`CompanionSettings`, new `CompanionSettingsDocument` and
`CompanionProfiles` readers); File paths the Companion writes (`settings.json`, `profiles.json`, their `.tmp` files);
Outbound calls (`serverAddress` and `serverToken` set from the window, one hello per profile from `LiveSender`); Windows
autostart (toggled from the window); Logging and showing external data (the figure name and profile names in tray
notifications, new log lines); a new process start (`CompanionRestart`, own exe with `--restarted`).

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | #39 and #40 are merged | `gh api …/issues/39`, `/40`: `closed`; PRs #238, #240 in `git log` | confirmed |
| 2 | The settings file is read once at start, never written | `App.xaml.cs` `OnStartup` (`CompanionSettings.ReadLocalServer(settingsFile)`, `gameSearch.Run(settingsFile)`); no write in `CompanionSettings.cs` | confirmed |
| 3 | An unparseable file yields mode Off and no game folder; a bad `gameFolder` alone does not stop the server keys | `CompanionSettings.ReadLocalServer(string)` returns `Off` on read, size, JSON and non-object failures; `Parse` invalidates only for `gameFolder` | confirmed |
| 4 | Live data carries one fixed profile | `LiveSender.cs:21` `ProfileName = "Live"`, `:176` `ProfileId`, `Convert` uses it, hello at `AttemptAsync` | confirmed |
| 5 | Only a hello names a profile; a second hello is closed with 1008; a tick of an unnamed identity creates a nameless profile | `docs/areas/contract.md:87-88`, `ingest.md:107-108`, `server-state.md:38-40`; `ContractHello.ProfileName` is `required string` | confirmed |
| 6 | Messages carry their own `profileId`, read with it, not with the hello's | `contract.md:87-88` | confirmed |
| 7 | The buffer stores the JSON as sent and no profile apart from it | `LiveBufferItem`, `LiveBufferEntry` (`Id`, `TakenAt`, `Utf8Json`); header format in `live-sending.md`, *Buffer* | confirmed |
| 8 | A session start carries the figure name | `PipeTickResult.Headline` (`PipeTickResult.cs:63`), `PipeTickBuilder.cs:156` | confirmed |
| 9 | The single-instance guard does not wait, and is released last in `Dispose`, after the hosted server stopped | `SingleInstanceGuard.TryAcquire` (`new Mutex(true, …, out createdNew)`); `App.Dispose` ends with `_guard?.Dispose()` after `localServer.StopAsync` | confirmed |
| 10 | A named mutex released by its owner is acquired by `WaitOne(timeout)` (unit test); one abandoned by an exited process throws `AbandonedMutexException` with ownership granted | to verify: the Product Manager's manual test kills the old Companion during a restart (step 7); release path by `SingleInstanceGuardWaitAcquiresAfterHolderReleases` | unverified |
| 11 | The log file is opened `FileShare.Read`, so a second process cannot append while the first holds it (E15's wait line can be missing while the old one runs; stated in `companion-log.md`) | `LogFileLoggerProvider.cs:273` | confirmed |
| 12 | Every tray text is an English constant or format of `TrayMenu`; `TraySnapshot.StatusText` is built when the snapshot is | `TrayMenu.cs:17-72`, `TraySnapshot.ForConnection`/`ForReplay` | confirmed |
| 13 | The same reason texts go to the log | `ReplaySender.Reason`, `LiveSender.Reason`, `PipeReplayRunner.cs:533` call `TrayMenu.FormatReplay*Reason` | confirmed |
| 14 | `TrayMenu.Create` already has seven parameters, so a texts parameter needs a bundle (S107) | `TrayMenu.cs:282` | confirmed |
| 15 | The installation check exists | `GameInstallation.IsInstallation(string)` | confirmed |
| 16 | CI builds the Companion ZIP as an artifact on pull requests | `.github/workflows/ci.yml:157-161` | confirmed |
| 17 | Record 0045 is released, 0102 is not | `git tag --contains`: 0045 in v0.2.0–v0.4.0, 0102 in none | confirmed |
| 18 | A balloon tip of `NotifyIcon` shows as a Windows 11 notification and raises `BalloonTipClicked` | to verify: Product Manager's manual test (step 7); WinForms `NotifyIcon` documentation | unverified |

## Tests per criterion

All tests in `tests/Censio.Companion.Core.Tests/`, each `[TestCategory("ACn")]`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `SettingsWindowSessionOpenShowsFileValuesAndDefaults`, `SettingsWindowSessionSecondOpenBringsWindowToFront`, `SettingsWindowSessionCloseWritesNothing`, `SettingsWindowSessionOpenChecksFolderOffTheCaller`, `CompanionSettingsDocumentLoadMissingFileGivesDefaults`, `TrayMenuCreateHasSettingsAboveOpenDiagnosticsInEveryMode`; manual: Product Manager, CI ZIP | `SettingsWindowSessionTests.cs`, `CompanionSettingsDocumentTests.cs`, `TrayMenuTests.cs` |
| AC2 | `CompanionSettingsDocumentSaveModeSwitchKeepsOtherKeys`, `…SaveKeepsUnknownKeysAndValues`, `…SaveOffRemovesMode`, `…SaveWritesEachKnownKeyOnceRemovingOtherCase`, `…SavedFileReadsBackAsSaved` (through `CompanionSettings.ReadLocalServer`, `Read`, `ReadLanguage`), `…SwitchBackRestoresEarlierValues` | `CompanionSettingsDocumentTests.cs` |
| AC3 | `CompanionSettingsValidatorRefusesEachInvalidValue` (data rows: E1–E6, a non-ASCII token, `5.0`, `"5"`, blank, 65536, 169, 10241, relative folder, missing folder, non-installation, blank/65-character/duplicate name, 1,000th profile), `CompanionSettingsValidatorAcceptsBoundaries`, `CompanionSettingsDocumentLoadMarksInvalidAndDuplicatedKeys`, `SettingsWindowSessionSaveBlockedUntilFileProblemFieldEdited`, `SettingsWindowSessionSaveWritesNothingWhileProblemShown` | `CompanionSettingsValidatorTests.cs`, `CompanionSettingsDocumentTests.cs`, `SettingsWindowSessionTests.cs` |
| AC4 | `SettingsWindowSessionSaveConnectionChangeRestarts`, `…SaveWithoutConnectionChangeDoesNotRestart`, `…WarnsAboutReplayRecordingAndMemoryBuffer`, `…RestartNotStartedKeepsRunningWithMessage`, `CompanionRestartStartsOwnExeWithRestartedSwitch`, `CompanionRestartLaunchFailureReturnsFalseAndLogs`, `CompanionCommandLineRestartedSwitchIsNormalRun`, `SingleInstanceGuardWaitAcquiresAfterHolderReleases`, `SingleInstanceGuardWaitTimesOutWhileHeld`, `SingleInstanceGuardTryAcquireWithoutWaitStillFailsAtOnce`; manual: Product Manager, local and forward mode | `SettingsWindowSessionTests.cs`, `CompanionRestartTests.cs`, `CompanionCommandLineTests.cs`, `SingleInstanceGuardTests.cs` |
| AC5 | `CompanionTextsGermanDiffersFromEnglishInEveryText`, `TrayMenuCreateGermanShowsGermanEntries`, `TrayMenuFormatToolTipLongestGermanFitsLimit`, `TraySnapshotForConnectionUsesGivenTexts`, `CompanionSettingsReadLanguage` (data rows `en`, `DE`, missing, `fr`, `5`, twice), `CompanionTextsLanguageOfCulture`, `SettingsWindowSessionSaveLanguageAppliesAtOnceWithoutRestart`, `SettingsWindowSessionAutostartFailureReported`; manual: Product Manager | `CompanionTextsTests.cs`, `TrayMenuTests.cs`, `TraySnapshotTests.cs`, `CompanionSettingsTests.cs`, `SettingsWindowSessionTests.cs` |
| AC6 | `LiveSenderProfileTests`: `…NewMessagesCarryActiveProfile`, `…SwitchReconnectsWithHelloOfNewProfile`, `…BufferedMessageOfOtherProfileReconnectsWithItsName` (created offline, switched away before the connection), `…UnknownProfileSentOnCurrentConnection`, `…SwitchReconnectHasNoDelayAndLogsNoFailure`, `…LocalServerSwitchReconnects`; `CompanionProfilesCreateSelectsAndKeepsLiveFirst` | `LiveSenderProfileTests.cs`, `CompanionProfilesTests.cs` |
| AC7 | `CompanionProfilesTests`: `…FigureOfOtherProfileGivesNotice`, `…NewFigureOnProfileWithFigureGivesNotice`, `…FirstFigureLinkedSilently`, `…IgnoredNoticeLinksToActive`, `…SwitchDuringSessionMovesLink`, `…SessionEndKeepsLink`; `LiveSenderProfileTests.SessionStartRaisesNoticeWithoutHoldingTicks`; `CompanionProfilesIsObservedOnlyWhenSendingLive` (replay, Off, invalid setting, invalid command line) | `CompanionProfilesTests.cs`, `LiveSenderProfileTests.cs` |
| AC8 | `CompanionSettingsDocumentLoadUnparseableGivesDefaultsAndReason`, `…SaveReplacesUnparseableFile`, `…SaveChangedOutsideWritesNothing`, `…SaveFailureKeepsOldFile`, `…SaveLeavesNoTmpFile`; `CompanionProfilesInvalidFileUsesLiveAndLinksNothing` (data rows per E16 case), `…InvalidFileReplacedOnlyByProfileSave` | `CompanionSettingsDocumentTests.cs`, `CompanionProfilesTests.cs` |
| AC9 | `CompanionProfilesFileAboveBoundIsInvalid`, `CompanionLogTests` rows for each new line (text, level, no value), `SettingsWindowSessionSaveLogsNoTokenOrName`, `LiveSenderProfileTests.FigureNameNeverLogged` | `CompanionProfilesTests.cs`, `CompanionLogTests.cs`, `SettingsWindowSessionTests.cs`, `LiveSenderProfileTests.cs` |
| AC10 | check: Reviewer reads README and the user guide against the spec (step 8); manual: Product Manager reads the German texts (step 7) | — |

## Approach

Early draft PR: yes — the Product Manager's manual test runs on the CI-built Companion ZIP of the draft pull request
(fact 16) before step 8: the window and its layout, the German texts, a save in local and forward mode that
restarts (also with the old Companion killed during the restart, fact 10), the notification and its click (fact 18), a
profile switch shown on the pages under its name. Logged as `7 Manual test`.

**Texts (G1).** `CompanionTexts` holds every Companion text: the tray's (today `TrayMenu`'s constants and formats),
`Settings…`, the window's labels, messages and outcomes, and the two notices; `English` and `German` are its two
instances, built with `required` init properties so a missing German text does not compile. Format patterns are
`CompositeFormat` properties (CA1863), formatted with `CultureInfo.InvariantCulture`; singular and plural are two
properties. `TrayMenu`'s `Format*` methods take `CompanionTexts texts` first, `Create` takes `(CompanionTexts texts,
TrayMenuState state)`; its text constants move to `CompanionTexts` (`MaxToolTipLength` stays). The log keeps English:
`ReplaySender.Reason`, `LiveSender.Reason`, `PipeReplayRunner` and `DiagnosticsView` pass `CompanionTexts.English`.
`CompanionSettings.ReadLanguage(path)` reads the key `language` (`en`/`de` ignoring case; any other value, a non-text
or a duplicate: `null` and the warning); without one `CompanionTexts.LanguageOf(CultureInfo.CurrentUICulture)` decides
(`de` → German). The shell holds the current texts in one field; a language change re-renders the snapshot
(`TraySnapshot.For*` take the texts), the tooltip and an open settings window at once. German wording: the Dev's,
listed in `companion-texts.md`, read by the Product Manager in the manual test.

**Settings file (G2).** `CompanionSettingsDocument.Load(path)` reads with `CompanionSettings`' own rules (byte order
mark, comments, trailing commas, 1 MiB + 1 read) and gives the window's `Values` (each field's text as found, the
default where missing), `FileProblem` (E7 reason, the same words `CompanionSettings.Read` logs) and `FieldProblems`
(E8: a wrong value or a duplicate of a known key, of any mode). It keeps the unknown root properties as written
(`JsonElement.WriteTo`) and the SHA-256 of the bytes read (missing file: no hash). `Save(values)` re-reads the file,
returns `ChangedOutside` when the hash differs, otherwise writes `settings.json.tmp` in the same folder (UTF-8 without
byte order mark, indented, keys in this order and spelling: `mode` (left out for Off), `localPort`, `serverAddress` and
`serverToken` (left out when empty; the token trimmed), `bufferHours`, `bufferMegabytes`, `gameFolder` (left out when
empty), `language` (`en`/`de`), then the unknown keys), flushes it to disk and moves it over the file
(`File.Move(…, overwrite: true)`); any `IOException`, `UnauthorizedAccessException`, `SecurityException` or
`NotSupportedException` deletes the `.tmp` file and returns `Failed` with the exception. Numeric fields are written as
JSON numbers of the validated text. `NeedsRestart(values)` compares the parsed mode, port, address, token, buffer
limits and game folder with those loaded. `CompanionSettingsValidator.Validate` gives the E1–E5 problems; the address
rule is `CompanionCommandLine.TryParseServerAddress`, the token rule `ReplayTokenFile.ReadToken`, whole numbers by the
same digits-only rule as `CompanionSettings` (one shared `internal static` helper in `CompanionSettings`, no copy); the
folder checks take `directoryExists` and `isInstallation` delegates and run only for a non-empty folder.

**Profiles (G3).** `CompanionProfiles` keeps `%LOCALAPPDATA%\Censio\profiles.json` (`CompanionFolders.Profiles`):
`{"version":1,"active":"<id>","profiles":[{"id":"<id>","name":"…"}],"figures":[{"name":"…","profile":"<id>"}]}`, read
strictly: at most 1 MiB (raw bytes, before decoding), version exactly 1, ids as `D`-format GUIDs, 1 to 1,000 profiles
with unique ids and names (ordinal ignoring case, 1 to 64 characters, not blank), `Live`'s entry with
`LiveSender.ProfileId`, `active` and every figure's profile in the list, at most 1,000 figures with unique names
(ordinal), no unknown or duplicated property. Anything else is E16: `IsFileInvalid`, `Live` alone, the warning with a
problem that quotes no value, and no write until `Create` or `Select` from the window. A missing file is `Live` alone,
valid. `ObserveSessionStart(figure)` runs under the lock and never waits: a figure linked to another profile gives a
`LastUsedWithOther` notice with that profile's name; an unknown figure on a profile that has a figure gives `NewFigure`
with the active profile's name; an unknown figure on a profile without one gives none; then the figure is linked to the
active profile (an ignored notice is a link), the oldest link dropped beyond 1,000, and the figure is remembered as
current until `ObserveSessionEnd`. `Select` while a figure is current moves its link. Writes go through `RunAsync`, a
background writer that writes the latest state as `.tmp` and moves it (failure: `Could not save the settings` with the
exception type, the state kept in memory). `IsObserved(mode, setting)` is `LiveSender.IsSending`. The figure name is
never logged; the notices name only profiles.

`LiveSender.UseProfiles(profiles)` (before `RunAsync`; without it the sender stays on `Live`, so existing tests and
callers keep their meaning): `Enqueue` stamps `profiles.Active.Id` and calls `ObserveSessionStart`/`ObserveSessionEnd`
for session items, raising `ProfileNotice`; the hello of each attempt names `_helloProfile` — the profile of the entry
that ended the previous connection, else the active one. In `SendLoopAsync` each entry's profile is read with
`ContractReader.Read` (own JSON, hash already checked); when its name is known (`profiles.NameOf`) and differs from the
hello's, the entry is released (`ReleaseInFlight`, resent first) and the attempt ends with a profile switch; an unknown
name is sent on. `ActiveChanged` completes a task `NextOrFailureAsync` also waits on, so an idle connection reconnects
at once. A profile switch in `RetryAsync`: status `Connecting`, no `Live sending failed` line, no delay, the delay step
unchanged. Local mode takes the same path.

**Restart (G4).** `--restarted` is one more switch of `CompanionCommandLine` (once, no value, combinable with nothing
else, `IsRestart`; mode `Normal`). `OnStartup` calls `TryAcquire(SingleInstanceGuard.RestartWait)` (10 seconds,
`new Mutex(false, name)` then `WaitOne`, `AbandonedMutexException` counting as acquired) for a restart, `TryAcquire()`
otherwise; a restart whose wait ran out logs `Restart failed: the previous Companion did not end` and shuts down.
`CompanionRestart.TryStart()` calls its launch delegate with `Environment.ProcessPath` and `--restarted`; a null or
non-full path, or a `Win32Exception`, `IOException`, `InvalidOperationException` or `UnauthorizedAccessException` from
the launch, logs `Restart failed: the Companion could not be started` with the exception type and returns `false`. The
shell's delegate is `Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, ArgumentList = { argument } })`.

**Window (G5).** `SettingsWindowSession` (Core) opens one window (`ISettingsWindow`), builds its `SettingsView` off the
caller (folder check by `Task.Run`, posted back with `post`), and on `SaveRequested` validates, refuses while a
problem or an unedited file-problem field remains, creates or selects the profile, toggles the autostart only when the
box differs from `CompanionAutostart.Refresh()` and reports `IsAutostartFailed` when the state read after `Toggle`
differs, applies the language, writes the file and, when `NeedsRestart`, calls `CompanionRestart.TryStart()` and then
`context.Exit` (`Restarting`), or reports `RestartFailed`. Outcomes map to the spec's texts. Warnings (E12) come from
`SettingsRunState` at open. The WPF `SettingsWindow` in `Censio.Companion` shows, top to bottom: the title `Censio
Companion settings`; a banner for E7, E16 and the E12 warnings (one line each, in that order, only when they apply);
`Mode` with the radio buttons `Off`, `Local`, `Forward`; `Local port` (enabled with Local); `Server address` and
`Server token` (a masked box, enabled with Forward); `Buffer hours` and `Buffer megabytes` (enabled with Local and
Forward); `Game folder` with `Browse…` (a folder dialog) and the hint `Empty: find automatically`; `Profile` (a
drop-down, `Live` first, then in creation order) with `New profile…` (shows a name box); `Start with Windows`;
`Language` (`English`, `Deutsch`); each field's message below it; the outcome line; `Save` and `Cancel`. Labels come
from `CompanionTexts`. A notification (`TrayIcon.ShowNotice`, title `Censio Companion`) opens the window on click. The
entry `Settings…` (`TrayMenuEntryKind.OpenSettings`, enabled in every mode) sits directly above `Open diagnostics`, so
in local mode `Open Censio` moves above it (after the first separator); `local-mode.md` (*Menu*) and `companion.md`
change their sentence on `Open Censio`'s place accordingly.

## Options considered

- Restart: reconfiguring the running Companion, or a helper exe — record 0112.
- Profiles: the server's list, swapping only the `profileId`, a contract message naming a profile — record 0111.
- Texts: resource files, a lookup of English texts — record 0113.
- File: in-place text edit keeping comments, a second override file — record 0110.
- The profile of a buffered entry: a header version 2 of the buffer file carrying it — lost: changes record 0105's
  format and its reader for a value the JSON already holds; the sender reads the JSON at send time anyway.
- `TrayMenu` with the texts: keeping the English API beside a texts overload — lost: two code paths for one menu and
  English constants twice; the test call sites are adapted instead (below).
- The figure links in `settings.json` — lost: the window would rewrite the hand-edited file at every session start.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `CompanionLanguage`, `CompanionTexts`, `TrayMenuState` | new |
| | `TrayMenu`, `TraySnapshot`, `TrayMenuEntryKind`, `DiagnosticsView`, `ReplaySender`, `PipeReplayRunner` | texts parameter; `OpenSettings` |
| | `CompanionSettings` | `ReadLanguage`, `LanguageKey`, shared whole-number helper |
| | `CompanionSettingsMode`, `CompanionSettingsValues`, `CompanionSettingsDocument`, `CompanionSettingsWrite`, `CompanionSettingsWriteKind`, `CompanionSettingsValidator`, `SettingsField`, `SettingsProblem`, `SettingsProblemKind` | new |
| | `CompanionProfile`, `CompanionProfiles`, `ProfileNotice`, `ProfileNoticeKind`, `CompanionFolders.Profiles` | new |
| | `LiveSender` | profiles, reconnect per profile, `ProfileNotice` |
| | `CompanionCommandLine`, `SingleInstanceGuard`, `CompanionRestart` | restart |
| | `ISettingsWindow`, `SettingsWindowSession`, `SettingsWindowContext`, `SettingsRunState`, `SettingsView`, `SettingsInput`, `SettingsSaveOutcome`, `SettingsSaveKind` | new |
| | `CompanionLog` | seven lines (below) |
| `Censio.Companion` | `SettingsWindow.xaml(.cs)` new; `App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` | wiring, notice, texts |

## Signatures (for the Dev's skeleton)

All in `Censio.Companion.Core` unless marked shell; records' properties are `{ get; init; }`.

G1:
- `public enum CompanionLanguage { English, German }`
- `public sealed class CompanionTexts`: `public static CompanionTexts English { get; }`, `public static CompanionTexts German { get; }`, `public static CompanionTexts For(CompanionLanguage language)`, `public static CompanionLanguage LanguageOf(CultureInfo culture)`, `public required CompanionLanguage Language { get; init; }`, `public required string OpenSettingsText { get; init; }`, `public string FormatProblem(SettingsProblemKind kind)`, `public string FormatNotice(ProfileNotice notice)`, `public string FormatOutcome(SettingsSaveOutcome outcome)`; besides these, one required text or format property per moved TrayMenu constant or format, named as the constant (data, read by tests only through the formatters)
- `public sealed record TrayMenuState`: `public required string Version`, `public required TraySnapshot Snapshot`, `public required GameInstallationStatus Game`, `public required GameCatalogStatus Catalog`, `public TrayServerStatus? Server`, `public bool IsAutostartEnabled`, `public required TimeProvider Clock`
- `public static IReadOnlyList<TrayMenuEntry> Create(CompanionTexts texts, TrayMenuState state)` replaces the seven-parameter one; every public TrayMenu formatter gains `CompanionTexts texts` as its first parameter
- `public static TraySnapshot ForConnection(CompanionTexts texts, PipeConnectionStatus status, PipeRecordingState recording, PipeTickSummary? lastTick)`, `public static TraySnapshot ForReplay(CompanionTexts texts, PipeReplayStatus status, PipeTickSummary? lastTick)`, `public static TraySnapshot ForInvalidCommandLine(CompanionTexts texts)`
- `TrayMenuEntryKind.OpenSettings`
- `public const string LanguageKey = "language";`, `public static CompanionLanguage? ReadLanguage(string path)` on CompanionSettings
- Deleted: TrayMenu's eleven text constants are removed (moved to CompanionTexts); MaxToolTipLength stays

G2:
- `public enum CompanionSettingsMode { Off, Local, Forward }`
- `public sealed record CompanionSettingsValues`: `public required CompanionSettingsMode Mode`, `public required string LocalPort`, `public required string ServerAddress`, `public required string ServerToken`, `public required string BufferHours`, `public required string BufferMegabytes`, `public required string GameFolder`, `public required CompanionLanguage Language`
- `public enum SettingsField { Mode, LocalPort, ServerAddress, ServerToken, BufferHours, BufferMegabytes, GameFolder, Language, ProfileName }`
- `public enum SettingsProblemKind { PortRange, AddressMissing, AddressInvalid, TokenInvalid, HoursRange, MegabytesRange, FolderNotFullPath, FolderMissing, FolderNotInstallation, NameBlank, NameTooLong, NameExists, ProfilesFull, WrittenTwice }`
- `public readonly record struct SettingsProblem`: `public required SettingsField Field`, `public required SettingsProblemKind Kind`
- `public sealed class CompanionSettingsDocument`: `public static CompanionSettingsDocument Load(string path)`, `public CompanionSettingsValues Values { get; }`, `public string? FileProblem { get; }`, `public IReadOnlyList<SettingsProblem> FieldProblems { get; }`, `public bool NeedsRestart(CompanionSettingsValues values)`, `public CompanionSettingsWrite Save(CompanionSettingsValues values)`
- `public enum CompanionSettingsWriteKind { Saved, ChangedOutside, Failed }`; `public sealed record CompanionSettingsWrite`: `public required CompanionSettingsWriteKind Kind`, `public Exception? Exception`
- `public static IReadOnlyList<SettingsProblem> Validate(CompanionSettingsValues values, Func<string, bool> directoryExists, Func<string, bool> isInstallation)` on `public static class CompanionSettingsValidator`

G3:
- `public sealed record CompanionProfile`: `public required Guid Id`, `public required string Name`, `public static CompanionProfile Live { get; }`
- `public enum ProfileNoticeKind { LastUsedWithOther, NewFigure }`; `public sealed record ProfileNotice`: `public required ProfileNoticeKind Kind`, `public required string ProfileName`
- `public sealed class CompanionProfiles`: `public const int MaxFileLength = 1048576;`, `public const int MaxProfiles = 1000;`, `public const int MaxFigures = 1000;`, `public const int MaxNameLength = 64;`, `public static CompanionProfiles Load(string path, ILogger logger)`, `public IReadOnlyList<CompanionProfile> Profiles { get; }`, `public CompanionProfile Active { get; }`, `public bool IsFileInvalid { get; }`, `public event EventHandler? ActiveChanged;`, `public SettingsProblemKind? CheckNewName(string name)`, `public CompanionProfile Create(string name)`, `public void Select(Guid id)`, `public string? NameOf(Guid id)`, `public ProfileNotice? ObserveSessionStart(string figure)`, `public void ObserveSessionEnd()`, `public Task RunAsync(CancellationToken cancellationToken)`, `public static bool IsObserved(CompanionMode mode, LocalServerSetting setting)`
- `public static string Profiles(string localApplicationData)` on CompanionFolders
- `public void UseProfiles(CompanionProfiles profiles)`, `public event EventHandler<ProfileNotice>? ProfileNoticeRaised;` on LiveSender
- CompanionLog (each `[LoggerMessage]`): `public static partial void SettingsSaved(ILogger logger);`, `public static partial void SettingsNotSaved(ILogger logger, string exceptionType);`, `public static partial void ProfileSwitched(ILogger logger);`, `public static partial void ProfilesFileInvalid(ILogger logger, string problem);`, `public static partial void LanguageSettingInvalid(ILogger logger);`

G4:
- `public const string RestartedSwitch = "--restarted";`, `public bool IsRestart { get; init; }` on CompanionCommandLine
- `public static TimeSpan RestartWait { get; }`, `public bool TryAcquire(TimeSpan wait)` on SingleInstanceGuard
- `public sealed class CompanionRestart`: `public CompanionRestart(Action<string, string> launch, string? exePath, ILogger logger)`, `public bool TryStart()`
- `public static partial void RestartNotStarted(ILogger logger, string exceptionType);`, `public static partial void RestartWaitTimedOut(ILogger logger);` on CompanionLog

G5:
- `public interface ISettingsWindow`: `event EventHandler? WindowClosed;`, `event EventHandler<SettingsInput>? SaveRequested;`, `void ShowView(SettingsView view);`, `void ShowOutcome(SettingsSaveOutcome outcome);`, `void BringToFront();`
- `public readonly record struct SettingsRunState`: `public bool IsReplay`, `public bool IsRecording`, `public bool IsBufferMemoryOnly`
- `public sealed record SettingsWindowContext`, all required: `public required string SettingsPath`, `public required CompanionProfiles Profiles`, `public required CompanionAutostart Autostart`, `public required CompanionRestart Restart`, `public required Func<CompanionTexts> Texts`, `public required Action<CompanionLanguage> ApplyLanguage`, `public required Func<SettingsRunState> RunState`, `public required Action Exit`, `public required Func<string, bool> DirectoryExists`, `public required Func<string, bool> IsInstallation`, `public required ILogger Logger`
- `public sealed record SettingsView`: `public required CompanionTexts Texts`, `public required CompanionSettingsValues Values`, `public string? FileProblem`, `public required IReadOnlyList<SettingsProblem> Problems`, `public required IReadOnlyList<CompanionProfile> ProfileList`, `public required Guid ActiveProfile`, `public bool IsProfilesFileInvalid`, `public bool IsAutostartEnabled`, `public required IReadOnlyList<string> Warnings`
- `public sealed record SettingsInput`: `public required CompanionSettingsValues Values`, `public required Guid SelectedProfile`, `public string? NewProfileName`, `public required bool IsAutostartEnabled`, `public required IReadOnlyCollection<SettingsField> EditedFields`
- `public enum SettingsSaveKind { Saved, Restarting, Refused, WriteFailed, ChangedOutside, RestartFailed }`; `public sealed record SettingsSaveOutcome`: `public required SettingsSaveKind Kind`, `public IReadOnlyList<SettingsProblem> Problems { get; init; } = [];`, `public bool IsAutostartFailed`
- `public sealed class SettingsWindowSession`: `public SettingsWindowSession(SettingsWindowContext context, Action<Action> post, Func<ISettingsWindow> createWindow)`, `public bool IsOpen { get; }`, `public void Open()`, `public void RefreshTexts()`
- Shell: `public void ShowNotice(string title, string text)`, `public event EventHandler? NoticeClicked;` on TrayIcon; `public required Func<CompanionTexts> Texts { get; init; }` on TrayIconSources

No nested types, no type name ending in Flags or Enum, at most seven parameters (the bundles TrayMenuState and
SettingsWindowContext), no keyword-named parameter (CA1716), one type per file.

## Test files

New: `CompanionTextsTests.cs`, `CompanionSettingsDocumentTests.cs`, `CompanionSettingsValidatorTests.cs`,
`CompanionProfilesTests.cs`, `LiveSenderProfileTests.cs`, `CompanionRestartTests.cs`, `SettingsWindowSessionTests.cs`,
`FakeSettingsWindow.cs` (an `ISettingsWindow`). Extended: `TrayMenuTests.cs`, `TraySnapshotTests.cs`,
`CompanionSettingsTests.cs`, `CompanionCommandLineTests.cs`, `SingleInstanceGuardTests.cs`, `CompanionLogTests.cs`.
All under `tests/Censio.Companion.Core.Tests/`.

Existing call sites of changed signatures: the Dev adapts them in step 4 (old signatures go away), mechanically — every
`TrayMenu.Create`/`Format*` call and text constant in `TrayMenuTests.cs` (78), `TraySnapshotTests.cs`,
`DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs`, `PipeReplayRunnerTests.cs`, `ReplaySenderTests.cs`
(insert `CompanionTexts.English`, constants to `CompanionTexts.English.<Name>`). `LiveSenderTests.cs` and
`LiveSenderCatalogTests.cs` are unchanged: without `UseProfiles` the sender keeps `Live` (no input they hold is handled
differently).

Files no unit test can cover: `SettingsWindow.xaml(.cs)`, `App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` — the
Windows shell, already excluded (`src/Censio.Companion/**`, record 0004); nothing new to exclude.

## Areas

- New `docs/areas/companion-settings.md` (+900 words): scope as the spec's *Area documents*; index row in
  `docs/areas/README.md`.
- New `docs/areas/companion-profiles.md` (+700 words): profiles, the file and its bounds, figure links, notices.
- New `docs/areas/companion-texts.md` (+1,200 words): the language choice and the English/German table.
- `docs/areas/companion.md` (+250 words): the menu entry, *Command line* (`--restarted`), *Settings file* (written by
  the window, `language`), *Folders* (`profiles.json`), *Limits and errors*.
- `docs/areas/live-sending.md` (+120 words): *Start and hello* and *Local mode* per profile, *Reconnect* (the
  profile switch), *Accepted risks* without *One profile*, *Related decisions* 0111 instead of 0102.
- `docs/areas/local-mode.md` (+20 words), `docs/areas/companion-log.md` (+90 words), `docs/areas/contract.md`
  (+0: no change), `docs/areas/companion-diagnostics.md` (+15 words: stays English).

## Documentation updates

Owner Dev, written once the code is green. Sweep terms: `never written`, `never writes`, `Live`, `a change counts after
a restart`, `settings.json`, `0102`, `0045`, the tray menu list.
- `README.md`: the menu list with `Settings…`; the settings section (window first, the file as alternative); the
  configuration keys with `language`; German part each text in German (AC10).
- `docs/USER_GUIDE.md`: a *Settings window* section (fields, restart, warnings), *Profiles* (create, select, the
  notice, the limits of record 0041), the language; *Local mode* and *Forward mode* without the fixed `Live` and
  "never writes"; the tooltip table with German texts in the German part.
- `docs/ARCHITECTURE.md`: line 66 (profile per connection, records 0111 instead of 0102), the `Censio.Companion.Core`
  row (texts, settings window, profiles), the settings flow (records 0110, 0112, 0113).
- The area documents of *Areas*; `docs/areas/README.md` index rows.
- `.squad/project.md` (+90 words, text reported by the Dev): *Parsing*: `CompanionSettingsDocument`,
  `CompanionProfiles`; *File paths*: the same plus `CompanionRestart`; *Windows autostart*: `SettingsWindowSession`;
  *Logging and showing*: `SettingsWindow`, `TrayIcon.ShowNotice`; integration surface: `language`,
  `CompanionSettingsDocumentTests`, `ISettingsWindow`, `CompanionTexts` for tray texts.
- `docs/UNIT_TESTS.md (Test doubles +2 words)`: `FakeSettingsWindow` on the line of `FakeDiagnosticsWindow` (Tester).

## Architecture check

Guarantees of `.squad/project.md`: the ingest token stays required and is never logged or shown (masked box, no log);
the buffer's order and bound hold — a profile reconnect resends the in-flight message first and drops nothing; a
switch never holds a tick. Windows-only code stays in `Censio.Companion` (record 0004): window, notification and
process start are behind `ISettingsWindow` and a delegate. The flow change (profile per connection, the Companion
writing its settings) is written into `docs/ARCHITECTURE.md` by the Dev, linking 0110–0113.

## Security considerations

- [x] Limits: `settings.json` 1 MiB on the raw bytes before decoding (as today); `profiles.json` 1 MiB raw, then 1,000
  profiles, 1,000 figures, names 1–64 UTF-16 characters after JSON unescaping; profile names reach the hello within the
  contract's 1,024 UTF-8 bytes (64 × 3 at most).
- [x] Exceptions reaching the user: a failed write shows `The settings could not be saved. Details are in the log.`; a
  failed restart E15's text; the log gets the exception type only. No exception text reaches the window.
- The `.tmp` files are created in the folder of their file, which lies in the player's private folder; a link at the
  target is replaced by the move, not followed (`File.Move` renames the entry). The profiles file is a new untrusted
  input read strictly (fail closed, E16).
- The restart starts only `Environment.ProcessPath` with one fixed argument, no shell; `--restarted` from a user only
  makes a second start wait.
- Figure names stay in `profiles.json` and memory; notices, logs and the contract never carry them.

## Decision records

- `docs/decisions/0110-settings-window-writes-the-settings-file-keeping-unknown-keys.md` (Proposed, supersedes 0045)
- `docs/decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md` (Proposed)
- `docs/decisions/0112-connection-settings-applied-by-a-restart-that-waits-for-the-old-companion.md` (Proposed)
- `docs/decisions/0113-companion-texts-in-english-and-german-from-one-table.md` (Proposed)
- Deleted: `0102` (unreleased, replaced by 0111); its links in `live-sending.md` and `ARCHITECTURE.md` go in the Dev's
  documentation updates.

## Out of scope / follow-ups

Renaming or deleting profiles in the Companion, a German diagnostics window and log, editing the hosted server's
settings (spec *Out of scope*).
