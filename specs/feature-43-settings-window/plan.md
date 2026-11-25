# Plan: Companion settings window

Source: Issue #43 | [spec.md](spec.md)
Tier: security — the Companion starts writing the file that holds the ingest token (storage of a secret), gets a new
reader of an external file and starts its own exe again.
Plan size: five build groups over three new files of the Companion (settings, profiles, texts) whose readers and writers
each need their accepted input forms enumerated (Security's plan review), plus the restart and the profile reconnect.
Security areas touched: Parsing of external input (`CompanionSettings`, new `CompanionSettingsDocument` and
`CompanionProfiles` readers); File paths the Companion writes (`settings.json`, `profiles.json`, their `.tmp` files);
Outbound calls (`serverAddress` and `serverToken` set from the window, one hello per profile from `LiveSender`, a typed
UNC game folder probed by the window); Windows autostart (toggled from the window); Logging and showing external data
(profile names in tray notifications, new log lines); a new process start (`CompanionRestart`, own exe with
`--restarted`).

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | #39 and #40 are merged | `gh api …/issues/39`, `/40`: `closed`; PRs #238, #240 in `git log` | confirmed |
| 2 | The settings file is read once at start, never written | `App.xaml.cs:646` `CompanionSettings.ReadLocalServer(settingsFile)`, `:662` `gameSearch.Run(settingsFile)`; no write in `CompanionSettings.cs` | confirmed |
| 3 | An unparseable file yields mode Off and no game folder; a bad `gameFolder` alone does not stop the server keys | `CompanionSettings.ReadLocalServer(string)` returns `Off` on read, size, JSON, `InvalidOperationException` and non-object failures; `Parse` invalidates only for `gameFolder` | confirmed |
| 4 | Live data carries one fixed profile | `LiveSender.cs:21` `ProfileName = "Live"`, `:176` `ProfileId`, hello at `AttemptAsync:703-709` | confirmed |
| 5 | Only a hello names a profile; a second hello is closed with 1008; a tick of an unnamed identity creates a nameless profile | `docs/areas/contract.md:87-88`, `ingest.md:107-108`, `server-state.md:38-40` | confirmed |
| 6 | Messages carry their own `profileId`, read with it, not with the hello's | `contract.md:87-88` | confirmed |
| 7 | The buffer stores the JSON as sent and no profile apart from it | `LiveBufferItem`, `LiveBufferEntry` (`Id`, `TakenAt`, `Utf8Json`) | confirmed |
| 8 | A session start carries the figure name | `PipeTickResult.Headline` (`PipeTickResult.cs:63`), `PipeTickBuilder.cs:156` | confirmed |
| 9 | `TryAcquire` does not wait; `App.Dispose` releases the guard last but after bounded waits whether or not they completed (250 ms live sender, ≤3 s `StopAsync`, 500 ms `DisposeAsync`) | `SingleInstanceGuard.TryAcquire` (`new Mutex(true, …, out createdNew)`); `App.xaml.cs:735-777` | confirmed |
| 10 | A named mutex released by its owner is acquired by `WaitOne(timeout)`; one abandoned by an exited process throws `AbandonedMutexException` with ownership granted | to verify: the Product Manager's manual test kills the old Companion during a restart (step 7); release path by `SingleInstanceGuardWaitAcquiresAfterHolderReleases` | unverified |
| 11 | The log file is opened `FileShare.Read`, so a second process cannot append while the first holds it | `LogFileLoggerProvider.cs:273` | confirmed |
| 12 | Every tray text is an English constant or format of `TrayMenu`; `TraySnapshot.StatusText` is built with the snapshot | `TrayMenu.cs:17-72`, `TraySnapshot.ForConnection`/`ForReplay` | confirmed |
| 13 | The same reason texts go to the log | `ReplaySender.Reason`, `LiveSender.Reason`, `PipeReplayRunner.cs:533` call `TrayMenu.FormatReplay*Reason` | confirmed |
| 14 | `TrayMenu.Create` has seven parameters, so a texts parameter needs a bundle (S107) | `TrayMenu.cs:282` | confirmed |
| 15 | The installation check exists | `GameInstallation.IsInstallation(string)` | confirmed |
| 16 | CI builds the Companion ZIP as an artifact on pull requests | `.github/workflows/ci.yml:157-161` | confirmed |
| 17 | Record 0045 is released, 0102 and 0110–0113 are not | `git tag --contains`; `git ls-remote --tags origin`: v0.2.0–v0.4.0 | confirmed |
| 18 | A balloon tip of `NotifyIcon` shows as a Windows 11 notification and raises `BalloonTipClicked` | to verify: Product Manager's manual test (step 7) | unverified |
| 19 | `LiveSender` ends every connection by `IngestClient.DisposeAsync` (abort), never the close handshake; `CloseAsync` waits at most 2 seconds for the server's close | `LiveSender.cs:746-758`; `IngestClient.cs:31`, `:230-270`, `:530-540` | confirmed |
| 20 | The server frees the Companion's place only at session end; a hello before is refused `companionConnected` and not counted toward the failing-host block | `IngestSession.cs:402-408` (`RecordUncountedHello`), `:860-869` (`EndAsync` → `ReleasePlace`); `ingest.md` *One Companion and silent connections* | confirmed |
| 21 | `JsonDocument.Parse` (default options) accepts invalid UTF-8 in a string and duplicate properties, refuses a byte order mark, comments and trailing commas; `GetString` throws `InvalidOperationException` on invalid UTF-8 and on `"\uD800"`; `JsonProperty.WriteTo` throws it on `"\uD800"` and writes U+FFFD for invalid UTF-8; `JsonProperty.Name` is unescaped (`"mode"` is `mode`); `TryGetInt32` refuses `1.0` and `1e0`; `Guid.TryParseExact(…, "D")` accepts uppercase; `Utf8JsonWriter.WriteString` writes a lone surrogate as `�` and escapes non-ASCII and control characters as `\uXXXX`; `WriteRawValue("05")` throws `JsonException` | probe run by the Lead on .NET 11.0.100 in a scratch project (step 3 revise) | confirmed |
| 22 | The readers match keys ordinal ignoring case on the unescaped name; `serverToken` is read only in forward mode | `CompanionSettings.FindProperties` `:381-387`; `ReadLocalServer` `:226-229` → `ReadForward` | confirmed |
| 23 | A local start whose port is in use is never retried | `LocalServerSession.ReportFailedStart` `:313-321`; `local-mode.md` *Limits and errors* | confirmed |
| 24 | The server refuses a hello text that is not well-formed UTF-16 | `ContractRules.CheckText` `:229-251` (strict encoding) | confirmed |

## Tests per criterion

In `tests/Censio.Companion.Core.Tests/` unless a file names another project; each `[TestCategory("ACn")]`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `SettingsWindowSessionOpenShowsFileValuesAndDefaults`, `SettingsWindowSessionSecondOpenBringsWindowToFront`, `SettingsWindowSessionCloseWritesNothing`, `SettingsWindowSessionOpenChecksFolderOffTheCaller`, `SettingsWindowSessionSaveChecksFolderOffTheCaller`, `CompanionSettingsDocumentLoadMissingFileGivesDefaults`, `TrayMenuCreateHasSettingsAboveOpenDiagnosticsInEveryMode`; manual: Product Manager, CI ZIP | `SettingsWindowSessionTests.cs`, `CompanionSettingsDocumentTests.cs`, `TrayMenuTests.cs` |
| AC2 | `CompanionSettingsDocumentSaveModeSwitchKeepsOtherKeys`, `…SaveKeepsUnknownKeysAndValues`, `…SaveOffRemovesMode`, `…SaveWritesEachKnownKeyOnceRemovingOtherCase` (rows: `MODE`, escaped `"mode"`), `…SavedFileReadsBackAsSaved` (through `CompanionSettings.ReadLocalServer`, `Read`, `ReadLanguage`), `…SwitchBackRestoresEarlierValues`, `…SaveWritesParsedNumber` (`05` → `5`), `…SaveWithoutLanguageLeavesKeyAbsent`; `SettingsWindowSessionSaveUntouchedLanguageWritesNone` | `CompanionSettingsDocumentTests.cs`, `SettingsWindowSessionTests.cs` |
| AC3 | `CompanionSettingsValidatorRefusesEachInvalidValue` (rows: E1–E6, a non-ASCII token, `5.0`, `"5"`, blank, 65536, 169, 10241, relative folder, missing folder, non-installation, blank/65-character/duplicate name, a name with U+0007, a name with a lone U+D800, 1,000th profile), `CompanionSettingsValidatorAcceptsBoundaries`, `CompanionSettingsDocumentLoadMarksInvalidAndDuplicatedKeys`, `SettingsWindowSessionSaveBlockedUntilFileProblemFieldEdited` (rows: a key of the selected mode, an invalid `serverToken` in local mode), `SettingsWindowSessionSaveWritesNothingWhileProblemShown`, `SettingsViewIsFieldEnabledKeepsFileProblemFieldEnabledInEveryMode` | `CompanionSettingsValidatorTests.cs`, `CompanionSettingsDocumentTests.cs`, `SettingsWindowSessionTests.cs`, `SettingsViewTests.cs` |
| AC4 | `SettingsWindowSessionSaveConnectionChangeRestarts`, `…SaveWithoutConnectionChangeDoesNotRestart`, `…SaveComparesWithStartupValues` (file changed before the window opened), `…WarnsAboutReplayRecordingAndMemoryBuffer`, `…RestartNotStartedKeepsRunningWithMessage`, `CompanionRestartStartsOwnExeWithRestartedSwitch`, `CompanionRestartLaunchFailureReturnsFalseAndLogs`, `CompanionCommandLineRestartedSwitchIsNormalRun`, `SingleInstanceGuardWaitAcquiresAfterHolderReleases`, `SingleInstanceGuardWaitTimesOutWhileHeld`, `SingleInstanceGuardTryAcquireWithoutWaitStillFailsAtOnce`, `LocalServerSessionPortInUseRetriedWithinRetryTime` (port freed on the third start), `LocalServerSessionPortInUseAfterRetryTimeShowsPortInUse`, `LocalServerSessionWithoutRetryTimeShowsPortInUseAtOnce`; manual: Product Manager, local and forward mode | `SettingsWindowSessionTests.cs`, `CompanionRestartTests.cs`, `CompanionCommandLineTests.cs`, `SingleInstanceGuardTests.cs`, `LocalServerSessionTests.cs` |
| AC5 | `CompanionTextsGermanDiffersFromEnglishInEveryText`, `TrayMenuCreateGermanShowsGermanEntries`, `TrayMenuFormatToolTipLongestGermanFitsLimit`, `TraySnapshotForConnectionUsesGivenTexts`, `CompanionSettingsReadLanguage` (rows `en`, `DE`, missing, `fr`, `5`, twice), `CompanionTextsLanguageOfCulture`, `SettingsWindowSessionSaveLanguageAppliesAtOnceWithoutRestart`, `SettingsWindowSessionAutostartFailureReported`; manual: Product Manager | `CompanionTextsTests.cs`, `TrayMenuTests.cs`, `TraySnapshotTests.cs`, `CompanionSettingsTests.cs`, `SettingsWindowSessionTests.cs` |
| AC6 | `LiveSenderProfileTests`: `…NewMessagesCarryActiveProfile`, `…SwitchReconnectsWithHelloOfNewProfile`, `…SwitchClosesOldConnectionBeforeNewHello`, `…BufferedMessageOfOtherProfileReconnectsWithItsName` (created offline, switched away before the connection), `…UnknownProfileSentOnCurrentConnection`, `…SwitchReconnectHasNoDelayAndLogsNoFailure`, `…SwitchRefusedCompanionConnectedRetriedWithoutFailure` (the fake connector refuses the first two post-switch hellos), `…SwitchRefusalPastRefusalTimeCountsAsFailure`, `…LocalServerSwitchReconnects`; `CompanionProfilesCreateSelectsAndKeepsLiveFirst`; `CensioServerStartLocalLiveProfileSwitchListsBothProfileNames` (real ingest endpoint, `ClientWebSocketConnector`, like `CensioServerStartLocalFillsProfileFromLocalReplay`: both hellos accepted, each profile's page shows its name) | `LiveSenderProfileTests.cs`, `CompanionProfilesTests.cs`, `tests/Censio.Server.Tests/CensioServerTests.cs` |
| AC7 | `CompanionProfilesTests`: `…FigureOfOtherProfileGivesNotice`, `…NewFigureOnProfileWithFigureGivesNotice`, `…FirstFigureLinkedSilently`, `…IgnoredNoticeLinksToActive`, `…SwitchDuringSessionMovesLink`, `…SessionEndKeepsLink`; `LiveSenderProfileTests.SessionStartRaisesNoticeWithoutHoldingTicks`; `CompanionProfilesIsObservedOnlyWhenSendingLive` (replay, Off, invalid setting, invalid command line) | `CompanionProfilesTests.cs`, `LiveSenderProfileTests.cs` |
| AC8 | `CompanionSettingsDocumentLoadUnparseableGivesDefaultsAndReason` (rows: over 1 MiB, not JSON, not an object, invalid UTF-8 in an unknown value, `"\uD800"` in an unknown key's value and in a known key, in a nested name), `…SaveReplacesUnparseableFile`, `…SaveChangedOutsideWritesNothing`, `…SaveFailureKeepsOldFile` (rows: `IOException`, `InvalidOperationException` from the writer), `…SaveLeavesNoTmpFile`; `CompanionProfilesInvalidFileUsesLiveAndLinksNothing` (rows per E16 form of *Approach* G3), `…LoadAcceptsUppercaseIds`, `…InvalidFileReplacedOnlyByProfileSave`, `…WorstCaseFileReadsBackValid`, `…RunAsyncWritesLatestStateOnCancel` | `CompanionSettingsDocumentTests.cs`, `CompanionProfilesTests.cs` |
| AC9 | `CompanionProfilesFileAboveBoundIsInvalid`, `CompanionProfilesFileHoldsNoFigureName`, `CompanionLogTests` rows for each new line (text, level, no value), `SettingsWindowSessionSaveLogsNoTokenOrName`, `LiveSenderProfileTests.FigureNameNeverLogged`, `CompanionSettingsValuesToStringHoldsNoToken`, `CompanionProfileToStringHoldsNoName`, `ProfileNoticeToStringHoldsNoName`, `SettingsViewToStringHoldsNoTokenOrName` | `CompanionProfilesTests.cs`, `CompanionLogTests.cs`, `SettingsWindowSessionTests.cs`, `LiveSenderProfileTests.cs`, `CompanionSettingsValuesTests.cs`, `CompanionProfileTests.cs`, `ProfileNoticeTests.cs`, `SettingsViewTests.cs` |
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
mark, comments, trailing commas, 1 MiB + 1 read) and finds each known key exactly as the readers do, through
`CompanionSettings.FindProperties` made `internal` (ordinal ignoring case on the unescaped name, so `"mode"` is
`mode`; fact 22), no copy. It then reads every string of the file with `GetString`, property names and values at any
depth: an `InvalidOperationException` (invalid UTF-8, a lone surrogate escape, fact 21) is E7 with the reason `holds
text that is not valid UTF-8`, so no kept property can make `WriteTo` throw or turn into U+FFFD. It gives the window's
`Values` (each field's text as found, the default where missing), `FileProblem` (E7 reason, the words
`CompanionSettings.Read` logs) and `FieldProblems` (E8: a wrong value or a duplicate of a known key, of any mode; a `mode` that is not a string or a text other than
empty, `local` or `forward` ignoring case is `ModeInvalid`, as `CompanionSettings.ReadLocalServer` calls it invalid, and
loads as `Off` with no radio button checked while the problem shows).
`language` is no E8 key: a wrong or repeated value loads as not set (`Values.Language` `null`, the Windows display
language, as `ReadLanguage` reads it). The document keeps the unknown root properties as written
(`JsonProperty.WriteTo`) and the SHA-256 of the bytes read (missing file: no hash).

`Save(values)` re-reads the file, returns `ChangedOutside` when the hash differs, otherwise writes `settings.json.tmp`
in the same folder with `Utf8JsonWriter` (UTF-8 without byte order mark, indented, keys in this order and spelling:
`mode` (left out for Off), `localPort`, `serverAddress` and `serverToken` (left out when empty; the token trimmed),
`bufferHours`, `bufferMegabytes`, `gameFolder` (left out when empty), `language` (`en`/`de`, only when
`Values.Language` is set: the file had a valid one or the player changed the field), then the unknown keys; every
other spelling of a known key is left out). Numbers are written with `WriteNumber` of the parsed `int` (`05` → `5`).
It flushes the file to disk and moves it over the old one (`File.Move(…, overwrite: true)`); an `IOException`,
`UnauthorizedAccessException`, `SecurityException`, `NotSupportedException`, `InvalidOperationException` or
`ArgumentException` deletes the `.tmp` file and returns `Failed` with the exception.

`CompanionSettingsDocument.NeedsRestart(running, saved)` compares the parsed mode, port, address, token, buffer limits
and game folder of the values the running Companion started with — `SettingsWindowContext.StartupValues`, which
`OnStartup` loads with `CompanionSettingsDocument.Load` beside `ReadLocalServer` (fact 2) — with the saved ones.
`CompanionSettingsValidator.Validate` checks every field's non-empty text whatever the mode (`AddressMissing` only in
Forward): the address by `CompanionCommandLine.TryParseServerAddress`, the token by `ReplayTokenFile.ReadToken`, whole
numbers by `CompanionSettings.TryParseWholeNumber`, the digits-only rule `TryReadWholeNumber` then calls, no copy; the
folder checks take `directoryExists` and `isInstallation` and run only for a non-empty folder; a new profile name by
`CompanionProfiles.CheckNewName`.

**Profiles (G3).** `CompanionProfiles` keeps `%LOCALAPPDATA%\Censio\profiles.json` (`CompanionFolders.Profiles`):
`{"version":1,"active":"<id>","profiles":[{"id":"<id>","name":"…"}],"figures":[{"figure":"<key>","profile":"<id>"}]}`.
A figure's key is the lowercase hex SHA-256 of its name's UTF-8 bytes (64 characters), so the file holds no figure
name and every figure entry has a fixed length. The writer is a default `Utf8JsonWriter` (not indented; non-ASCII and
control characters escaped as `\uXXXX`, at most 6 bytes per UTF-16 code unit), so the largest file it writes is
88 bytes of frame + 1,000 profiles × (7 + 36 + 10 + 64 × 6 + 2 + 1) − 1 + 1,000 figures × (11 + 64 + 13 + 36 + 2 + 1)
− 1 = 88 + 439,999 + 126,999 = 567,086 bytes, under `MaxFileLength` (1 MiB, raw bytes before decoding).

The reader parses with `JsonDocument`'s default options and fails closed (E16) on every form outside these rules:
- a byte order mark, a comment or a trailing comma (the writer writes none); a root that is not an object; a value of
  another kind than the rule names;
- property names compared ordinal on the unescaped name: exactly `version`, `active`, `profiles`, `figures` at the
  root, `id`, `name` in a profile, `figure`, `profile` in a figure; a property counted twice (JsonDocument keeps
  duplicates) or unknown (`Version`) is E16;
- `version` a number whose `TryGetInt32` gives exactly 1 (`1.0`, `1e0` refused);
- `active`, `id` and `profile` strings that `Guid.TryParseExact(…, "D")` reads (either case); ids unique, `Live`'s
  entry, `active` and every figure's profile compared as `Guid` values;
- 1 to 1,000 profiles; names read by `GetString` (an `InvalidOperationException` is E16), passing `CheckName` and
  unique ordinal ignoring case;
- at most 1,000 figures, keys 64 lowercase hex characters, unique ordinal;
- at most `MaxFileLength` raw bytes, read as `CompanionSettings` reads (1 MiB + 1).

E16 means `IsFileInvalid`, `Live` alone, the warning with a problem that quotes no value, and no write until `Create` or
`Select` from the window. A missing file is `Live` alone, valid. `CompanionProfiles.CheckName(name)` is the one name
rule of the reader and `CheckNewName`: not blank (`NameBlank`), at most 64 UTF-16 code units (`NameTooLong`),
well-formed UTF-16 and no character of category Cc (`NameInvalidCharacters`; fact 24); `CheckNewName` adds `NameExists`
and `ProfilesFull`.

`ObserveSessionStart(figure)` runs under the lock and never waits: a figure linked to another profile gives a
`LastUsedWithOther` notice with that profile's name; an unknown figure on a profile that has a figure gives `NewFigure`
with the active profile's name; an unknown figure on a profile without one gives none; then the figure is linked to the
active profile (an ignored notice is a link), the oldest link dropped beyond 1,000, and the figure is remembered as
current until `ObserveSessionEnd`. `Select` while a figure is current moves its link. Writes go through `RunAsync`, a
background writer that writes the latest state as `.tmp` and moves it (failure: `Could not save the settings` with the
exception type, the state kept in memory); when cancelled it writes the latest state once more if it changed since its
last write, then completes. `IsObserved(mode, setting)` is `LiveSender.IsSending`. The figure name and its key are
never logged; the notices name only profiles.

`LiveSender.UseProfiles(profiles)` (before `RunAsync`; without it the sender stays on `Live`, so existing tests and
callers keep their meaning): `Enqueue` stamps `profiles.Active.Id` and calls `ObserveSessionStart`/`ObserveSessionEnd`
for session items, raising `ProfileNotice`; the hello of each attempt names `_helloProfile` — the profile of the entry
that ended the previous connection, else the active one. In `SendLoopAsync` each entry's profile is read with
`ContractReader.Read` (own JSON, hash already checked); when its name is known (`profiles.NameOf`) and differs from the
hello's, the entry is released (`ReleaseInFlight`, resent first) and the attempt ends with a profile switch; an unknown
name is sent on. `ActiveChanged` completes a task `NextOrFailureAsync` also waits on, so an idle connection reconnects
at once. An attempt that ends with a profile switch closes its connection with `IngestClient.CloseAsync` (the close
handshake, at most 2 seconds, fact 19) before `DisposeAsync`, and `RetryAsync` then sets `Connecting`, logs no `Live
sending failed` line, waits no delay and leaves the delay step unchanged. The server frees its place only when the old
session ends (fact 20), so a `companionConnected` refusal within `SwitchRefusalTime` (5 seconds, beyond the about
3 seconds `ingest.md` gives for a closed connection's last store) after the switch counts as part of the switch: another
hello after `SwitchRetryDelay` (250 ms, on the injected `TimeProvider`), `Connecting`, no line, no delay step; a refusal
after that time is handled as today. Local mode takes the same path.

**Restart (G4).** `--restarted` is one more switch of `CompanionCommandLine` (once, no value, combinable with nothing
else, `IsRestart`; mode `Normal`). `OnStartup` calls `TryAcquire(SingleInstanceGuard.RestartWait)` (10 seconds,
`new Mutex(false, name)` then `WaitOne`, `AbandonedMutexException` counting as acquired) for a restart, `TryAcquire()`
otherwise; a restart whose wait ran out logs `Restart failed: the previous Companion did not end` and shuts down.
A restart's `LocalServerSession` gets `PortInUseRetry = SingleInstanceGuard.RestartWait`: a start that returns
`PortInUse` is started again every 500 ms on the session's `TimeProvider` while less than that time passed since
`RunAsync` began, showing `Server: starting`, and only then logs and shows `Server: port <port> in use`; without it
(every other start) nothing is retried, as today. `CompanionRestart.TryStart()` calls its launch delegate with
`Environment.ProcessPath` and `--restarted`; a null or non-full path, or a `Win32Exception`, `IOException`,
`InvalidOperationException` or `UnauthorizedAccessException` from the launch, logs `Restart failed: the Companion could
not be started` with the exception type and returns `false`. The shell's delegate is `Process.Start(new
ProcessStartInfo(path) { UseShellExecute = false, ArgumentList = { argument } })`.

`App.Dispose` (shell) cancels the profiles writer and waits up to 1 second for it before `_guard?.Dispose()` on every
exit. On the restart exit (`SettingsWindowContext.Exit` sets `_isRestarting` before the shutdown) it disposes the guard
only when the live sender, the hosted server's `StopAsync` and `DisposeAsync` and the profiles writer all completed
within their waits; otherwise it leaves the mutex to the process exit, which abandons it (fact 10), and the new
Companion's port retry covers a port Windows frees only with the process.

**Window (G5).** `SettingsWindowSession` (Core) opens one window (`ISettingsWindow`), builds its `SettingsView` off the
caller (folder check by `Task.Run`, posted back with `post`). On `SaveRequested` it ignores a second request while one
runs, runs `Validate` (folder checks and name check included) and, without a problem or an unedited file-problem
field, the file write by `Task.Run`, and posts the result back; there it creates or selects the profile, toggles the
autostart only when the box differs from `CompanionAutostart.Refresh()` and reports `IsAutostartFailed` when the state
read after `Toggle` differs, applies the language and, when `NeedsRestart(context.StartupValues, saved)`, calls
`CompanionRestart.TryStart()` and then `context.Exit` (`Restarting`), or reports `RestartFailed`. A refused or failed
save changes nothing else. `SettingsInput.Values.Language` is set only when `EditedFields` holds `Language` or the file
had one. Outcomes map to the spec's texts. Warnings (E12) come from `SettingsRunState` at open.
`SettingsView.IsFieldEnabled(field, mode)`: `LocalPort` with Local, `ServerAddress` and `ServerToken` with Forward, the
buffer fields with Local and Forward, the rest always — and a field named in `Problems` in every mode, so a file
problem of another mode can be edited away.

The WPF `SettingsWindow` in `Censio.Companion` shows, top to bottom: the title `Censio Companion settings`; a banner for
E7, E16 and the E12 warnings (one line each, in that order, only when they apply); `Mode` with the radio buttons `Off`,
`Local`, `Forward`; `Local port`; `Server address` and `Server token` (a masked box); `Buffer hours` and `Buffer
megabytes`; `Game folder` with `Browse…` (a folder dialog) and the hint `Empty: find automatically`; `Profile` (a
drop-down, `Live` first, then in creation order) with `New profile…` (shows a name box); `Start with Windows`;
`Language` (`English`, `Deutsch`); each field's message below it; the outcome line; `Save` and `Cancel`; fields enabled
by `IsFieldEnabled`. Labels come from `CompanionTexts`. A notification (`TrayIcon.ShowNotice`, title `Censio
Companion`) opens the window on click. The entry `Settings…` (`TrayMenuEntryKind.OpenSettings`, enabled in every mode)
sits directly above `Open diagnostics`, so in local mode `Open Censio` moves above it (after the first separator);
`local-mode.md` (*Menu*) and `companion.md` change their sentence on `Open Censio`'s place accordingly.

No type that holds the token, a profile name or a figure is a record: `CompanionSettingsValues`, `CompanionProfile`,
`ProfileNotice`, `SettingsView` and `SettingsInput` are sealed classes without `ToString` (`contract.md`, *What never
crosses*, as `ForwardServerSetting`).

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
- Figures in `profiles.json` by name with a 4 MiB bound (Security B1's second option) — lost: the file would hold the
  figure name (record 0041) and its size would depend on the pipe's strings; the hash is fixed at 64 characters.
- An invalid string in the settings file: `Save` failing on it instead of E7 (Security B2) — lost: the player could
  never save without editing the file by hand; E7 shows the defaults and the warning first.
- After a slow stop: retrying the bind only, or holding the guard only (Devil's Advocate 2) — lost: the guard alone
  leaves the race of an abandoned mutex against the socket's release at process exit, the retry alone lets the new
  Companion read a profiles file still being written.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `CompanionLanguage`, `CompanionTexts`, `TrayMenuState` | new |
| | `TrayMenu`, `TraySnapshot`, `TrayMenuEntryKind`, `DiagnosticsView`, `ReplaySender`, `PipeReplayRunner` | texts parameter; `OpenSettings` |
| | `CompanionSettings` | `ReadLanguage`, `LanguageKey`, internal `FindProperties`, `TryParseWholeNumber` |
| | `CompanionSettingsMode`, `CompanionSettingsValues`, `CompanionSettingsDocument`, `CompanionSettingsWrite`, `CompanionSettingsWriteKind`, `CompanionSettingsValidator`, `SettingsField`, `SettingsProblem`, `SettingsProblemKind` | new |
| | `CompanionProfile`, `CompanionProfiles`, `ProfileNotice`, `ProfileNoticeKind`, `CompanionFolders.Profiles` | new |
| | `LiveSender` | profiles, reconnect per profile with close and refusal window, `ProfileNotice` |
| | `CompanionCommandLine`, `SingleInstanceGuard`, `CompanionRestart`, `LocalServerSession` | restart, port retry |
| | `ISettingsWindow`, `SettingsWindowSession`, `SettingsWindowContext`, `SettingsRunState`, `SettingsView`, `SettingsInput`, `SettingsSaveOutcome`, `SettingsSaveKind` | new |
| | `CompanionLog` | seven lines (below) |
| `Censio.Companion` | `SettingsWindow.xaml(.cs)` new; `App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` | wiring, notice, texts, exit order |

## Signatures (for the Dev's skeleton)

All in `Censio.Companion.Core` unless marked shell; properties of the records and classes below are `{ get; init; }`.

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
- `public sealed class CompanionSettingsValues`: `public required CompanionSettingsMode Mode`, `public required string LocalPort`, `public required string ServerAddress`, `public required string ServerToken`, `public required string BufferHours`, `public required string BufferMegabytes`, `public required string GameFolder`, `public CompanionLanguage? Language`
- `public enum SettingsField { Mode, LocalPort, ServerAddress, ServerToken, BufferHours, BufferMegabytes, GameFolder, Language, ProfileName }`
- `public enum SettingsProblemKind { PortRange, AddressMissing, AddressInvalid, TokenInvalid, HoursRange, MegabytesRange, FolderNotFullPath, FolderMissing, FolderNotInstallation, NameBlank, NameTooLong, NameInvalidCharacters, NameExists, ProfilesFull, ModeInvalid, WrittenTwice }`
- `public readonly record struct SettingsProblem`: `public required SettingsField Field`, `public required SettingsProblemKind Kind`
- `public sealed class CompanionSettingsDocument`: `public static CompanionSettingsDocument Load(string path)`, `public CompanionSettingsValues Values { get; }`, `public string? FileProblem { get; }`, `public IReadOnlyList<SettingsProblem> FieldProblems { get; }`, `public static bool NeedsRestart(CompanionSettingsValues running, CompanionSettingsValues saved)`, `public CompanionSettingsWrite Save(CompanionSettingsValues values)`
- `public enum CompanionSettingsWriteKind { Saved, ChangedOutside, Failed }`; `public sealed record CompanionSettingsWrite`: `public required CompanionSettingsWriteKind Kind`, `public Exception? Exception`
- `public static IReadOnlyList<SettingsProblem> Validate(CompanionSettingsValues values, Func<string, bool> directoryExists, Func<string, bool> isInstallation)` on `public static class CompanionSettingsValidator`
- `internal static List<JsonElement> FindProperties(JsonElement root, string key)` (today private), `internal static bool TryParseWholeNumber(ReadOnlySpan<char> text, int minimum, int maximum, out int number)` on CompanionSettings

G3:
- `public sealed class CompanionProfile`: `public required Guid Id`, `public required string Name`, `public static CompanionProfile Live { get; }`
- `public enum ProfileNoticeKind { LastUsedWithOther, NewFigure }`; `public sealed class ProfileNotice`: `public required ProfileNoticeKind Kind`, `public required string ProfileName`
- `public sealed class CompanionProfiles`: `public const int MaxFileLength = 1048576;`, `public const int MaxProfiles = 1000;`, `public const int MaxFigures = 1000;`, `public const int MaxNameLength = 64;`, `public static CompanionProfiles Load(string path, ILogger logger)`, `public IReadOnlyList<CompanionProfile> Profiles { get; }`, `public CompanionProfile Active { get; }`, `public bool IsFileInvalid { get; }`, `public event EventHandler? ActiveChanged;`, `public static SettingsProblemKind? CheckName(string name)`, `public SettingsProblemKind? CheckNewName(string name)`, `public CompanionProfile Create(string name)`, `public void Select(Guid id)`, `public string? NameOf(Guid id)`, `public ProfileNotice? ObserveSessionStart(string figure)`, `public void ObserveSessionEnd()`, `public Task RunAsync(CancellationToken cancellationToken)`, `public static bool IsObserved(CompanionMode mode, LocalServerSetting setting)`
- `public static string Profiles(string localApplicationData)` on CompanionFolders
- `public void UseProfiles(CompanionProfiles profiles)`, `public event EventHandler<ProfileNotice>? ProfileNoticeRaised;`, `public static TimeSpan SwitchRefusalTime { get; }`, `public static TimeSpan SwitchRetryDelay { get; }` on LiveSender
- CompanionLog (each `[LoggerMessage]`): `public static partial void SettingsSaved(ILogger logger);`, `public static partial void SettingsNotSaved(ILogger logger, string exceptionType);`, `public static partial void ProfileSwitched(ILogger logger);`, `public static partial void ProfilesFileInvalid(ILogger logger, string problem);`, `public static partial void LanguageSettingInvalid(ILogger logger);`

G4:
- `public const string RestartedSwitch = "--restarted";`, `public bool IsRestart { get; init; }` on CompanionCommandLine
- `public static TimeSpan RestartWait { get; }`, `public bool TryAcquire(TimeSpan wait)` on SingleInstanceGuard
- `public TimeSpan PortInUseRetry { get; init; }` on LocalServerSession
- `public sealed class CompanionRestart`: `public CompanionRestart(Action<string, string> launch, string? exePath, ILogger logger)`, `public bool TryStart()`
- `public static partial void RestartNotStarted(ILogger logger, string exceptionType);`, `public static partial void RestartWaitTimedOut(ILogger logger);` on CompanionLog

G5:
- `public interface ISettingsWindow`: `event EventHandler? WindowClosed;`, `event EventHandler<SettingsInput>? SaveRequested;`, `void ShowView(SettingsView view);`, `void ShowOutcome(SettingsSaveOutcome outcome);`, `void BringToFront();`
- `public readonly record struct SettingsRunState`: `public bool IsReplay`, `public bool IsRecording`, `public bool IsBufferMemoryOnly`
- `public sealed class SettingsWindowContext`, all required: `public required string SettingsPath`, `public required CompanionSettingsValues StartupValues`, `public required CompanionProfiles Profiles`, `public required CompanionAutostart Autostart`, `public required CompanionRestart Restart`, `public required Func<CompanionTexts> Texts`, `public required Action<CompanionLanguage> ApplyLanguage`, `public required Func<SettingsRunState> RunState`, `public required Action Exit`, `public required Func<string, bool> DirectoryExists`, `public required Func<string, bool> IsInstallation`, `public required ILogger Logger`
- `public sealed class SettingsView`: `public required CompanionTexts Texts`, `public required CompanionSettingsValues Values`, `public string? FileProblem`, `public required IReadOnlyList<SettingsProblem> Problems`, `public required IReadOnlyList<CompanionProfile> ProfileList`, `public required Guid ActiveProfile`, `public bool IsProfilesFileInvalid`, `public bool IsAutostartEnabled`, `public required IReadOnlyList<string> Warnings`, `public bool IsFieldEnabled(SettingsField field, CompanionSettingsMode mode)`
- `public sealed class SettingsInput`: `public required CompanionSettingsValues Values`, `public required Guid SelectedProfile`, `public string? NewProfileName`, `public required bool IsAutostartEnabled`, `public required IReadOnlyCollection<SettingsField> EditedFields`
- `public enum SettingsSaveKind { Saved, Restarting, Refused, WriteFailed, ChangedOutside, RestartFailed }`; `public sealed record SettingsSaveOutcome`: `public required SettingsSaveKind Kind`, `public IReadOnlyList<SettingsProblem> Problems { get; init; } = [];`, `public bool IsAutostartFailed`
- `public sealed class SettingsWindowSession`: `public SettingsWindowSession(SettingsWindowContext context, Action<Action> post, Func<ISettingsWindow> createWindow)`, `public bool IsOpen { get; }`, `public void Open()`, `public void RefreshTexts()`
- Shell: `public void ShowNotice(string title, string text)`, `public event EventHandler? NoticeClicked;` on TrayIcon; `public required Func<CompanionTexts> Texts { get; init; }` on TrayIconSources

No nested types, no type name ending in Flags or Enum, at most seven parameters (the bundles TrayMenuState and
SettingsWindowContext), no keyword-named parameter (CA1716), one type per file.

## Test files

New: `CompanionTextsTests.cs`, `CompanionSettingsDocumentTests.cs`, `CompanionSettingsValidatorTests.cs`,
`CompanionSettingsValuesTests.cs`, `CompanionProfilesTests.cs`, `CompanionProfileTests.cs`, `ProfileNoticeTests.cs`,
`LiveSenderProfileTests.cs`, `CompanionRestartTests.cs`, `SettingsWindowSessionTests.cs`, `SettingsViewTests.cs`,
`FakeSettingsWindow.cs` (an `ISettingsWindow`). Extended: `TrayMenuTests.cs`, `TraySnapshotTests.cs`,
`CompanionSettingsTests.cs`, `CompanionCommandLineTests.cs`, `SingleInstanceGuardTests.cs`, `CompanionLogTests.cs`,
`LocalServerSessionTests.cs`; all under `tests/Censio.Companion.Core.Tests/`. Extended in `tests/Censio.Server.Tests/`:
`CensioServerTests.cs` (AC6's test against the real ingest endpoint).

Existing call sites of changed signatures: the Dev adapts them in step 4 (old signatures go away), mechanically — every
`TrayMenu.Create`/`Format*` call and text constant in `TrayMenuTests.cs` (78), `TraySnapshotTests.cs`,
`DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs`, `PipeReplayRunnerTests.cs`, `ReplaySenderTests.cs`
(insert `CompanionTexts.English`, constants to `CompanionTexts.English.<Name>`). `LiveSenderTests.cs`,
`LiveSenderCatalogTests.cs` and the existing `LocalServerSessionTests.cs` cases are unchanged: without `UseProfiles` the
sender keeps `Live`, and without `PortInUseRetry` the session retries nothing (no input they hold is handled
differently).

Files no unit test can cover: `SettingsWindow.xaml(.cs)`, `App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` — the
Windows shell, already excluded (`src/Censio.Companion/**`, record 0004); nothing new to exclude. The exit order of
`App.Dispose` (guard after the profiles writer and, on a restart, after the hosted server) is shell code: its effect
on the port is covered by the port-retry tests, the order itself by the Product Manager's local-mode restart (step 7).

## Areas

- New `docs/areas/companion-settings.md` (+1,000 words): scope as the spec's *Area documents*; index row in
  `docs/areas/README.md`.
- New `docs/areas/companion-profiles.md` (+800 words): profiles, the file, its accepted forms and bounds, figure keys,
  notices.
- New `docs/areas/companion-texts.md` (+1,200 words): the language choice and the English/German table.
- `docs/areas/companion.md` (+250 words): the menu entry, *Command line* (`--restarted`), *Settings file* (written by
  the window, `language`), *Folders* (`profiles.json`), *Limits and errors*.
- `docs/areas/live-sending.md` (+180 words): *Start and hello* and *Local mode* per profile, *Reconnect* (the profile
  switch, its close and refusal window), *Accepted risks* without *One profile*, *Related decisions* 0111 instead of
  0102.
- `docs/areas/local-mode.md` (+50 words): *Menu*, and *Limits and errors* (`nothing is retried` except after a restart,
  for up to 10 seconds).
- `docs/areas/game-installation.md` (+30 words): *Folders from outside* (the window probes a typed folder on open and on
  `Save`, off the tray thread; the search still writes nothing, the settings file is written only by the window).
- `docs/areas/companion-log.md` (+90 words), `docs/areas/contract.md` (+0: no change),
  `docs/areas/companion-diagnostics.md` (+15 words: stays English).

## Documentation updates

Owner Dev, written once the code is green. Sweep terms: `never written`, `never writes`, `writes nothing`, `Live`, `a
change counts after a restart`, `settings.json`, `nothing is retried`, `0102`, `0045`, the tray menu list.
- `README.md`: the menu list with `Settings…`; the settings section (window first, the file as alternative); the
  configuration keys with `language`; German part each text in German (AC10).
- `docs/USER_GUIDE.md`: a *Settings window* section (fields, restart, warnings), *Profiles* (create, select, the
  notice, the limits of record 0041), the language; *Local mode* and *Forward mode* without the fixed `Live` and
  "never writes"; the tooltip table with German texts in the German part.
- `docs/ARCHITECTURE.md`: line 66 (profile per connection, records 0111 instead of 0102), the `Censio.Companion.Core`
  row (texts, settings window, profiles), the settings flow (records 0110, 0112, 0113).
- The area documents of *Areas*; `docs/areas/README.md` index rows. `companion-settings.md` states record 0110's
  consequence on access rules and `settings.json.tmp` in its words.
- `SECURITY.md` (Security's text, applied by the Dev): *Scope*, first bullet, `(both read only; a UNC game folder` →
  `(the install records read only, the settings file also written by the settings window; a UNC game folder`; after
  it the bullet `- The Companion's settings window: writing %LOCALAPPDATA%\Censio\settings.json (with serverToken) and
  %LOCALAPPDATA%\Censio\profiles.json (the profile names and a hash of each figure name linked to them) through a .tmp
  file in the same folder, reading profiles.json back, and the restart that starts the Companion's own exe with
  --restarted`; *Deployment Security Considerations*, after `delete the token from it when you stop using forward
  mode.`: `The settings window writes the same file; %LOCALAPPDATA%\Censio\profiles.json beside it holds your profile
  names and a hash of the name of each figure you played them with, so do not share that file either.`
- `.squad/project.md (+120 words)`, text reported by the Dev: *Parsing*: `CompanionSettingsDocument`,
  `CompanionProfiles`; *File paths*: the same plus `CompanionRestart`; *Outbound calls*: `SettingsWindowSession` and
  `CompanionSettingsValidator` (the window's folder checks) among the UNC probes; *Windows autostart*:
  `SettingsWindowSession`; *Logging and showing*: `SettingsWindow`, `TrayIcon.ShowNotice`; integration surface:
  `language`, `CompanionSettingsDocumentTests`, `ISettingsWindow`, `CompanionTexts` for tray texts.
- `docs/UNIT_TESTS.md (Test doubles +2 words)`: `FakeSettingsWindow` on the line of `FakeDiagnosticsWindow` (Tester).

## Architecture check

Guarantees of `.squad/project.md`: the ingest token stays required and is never logged or shown (masked box, no log,
no generated string form); the buffer's order and bound hold — a profile reconnect resends the in-flight message first
and drops nothing; a switch never holds a tick. Windows-only code stays in `Censio.Companion` (record 0004): window,
notification and process start are behind `ISettingsWindow` and a delegate. The flow change (profile per connection,
the Companion writing its settings) is written into `docs/ARCHITECTURE.md` by the Dev, linking 0110–0113.

## Security considerations

- [x] Limits: `settings.json` 1 MiB on the raw bytes before decoding (as today); `profiles.json` 1 MiB raw, then 1,000
  profiles, 1,000 figures, names 1–64 UTF-16 code units after JSON unescaping; the writer's worst case is 567,086 bytes
  (G3), so it never writes a file its reader refuses; profile names reach the hello within the contract's 1,024 UTF-8
  bytes (64 × 3 at most) and well-formed (fact 24).
- [x] Exceptions reaching the user: a failed write shows `The settings could not be saved. Details are in the log.`; a
  failed restart E15's text; the log gets the exception type only. No exception text reaches the window. A string the
  readers cannot decode is E7 / E16, never an exception (record 0033).
- The `.tmp` files are created in the folder of their file, in the player's private folder; a link at the target is
  replaced by the move, not followed (`File.Move` renames the entry). The replaced `settings.json` takes the access
  rules its folder passes on; a crash between write and move leaves `settings.json.tmp` with the token until the next
  save (record 0110).
- The restart starts only `Environment.ProcessPath` with one fixed argument, no shell; `--restarted` from a user only
  makes a second start wait and retry a busy port for 10 seconds.
- Figure names stay in memory; `profiles.json` holds only their SHA-256, and notices, logs and the contract never carry
  them. The token, profile names and figures are in no generated string form.
- The window's folder checks (a UNC folder probed over SMB) run off the tray thread, on open and on `Save` (N3).

## Decision records

- `docs/decisions/0110-settings-window-writes-the-settings-file-keeping-unknown-keys.md` (Proposed, supersedes 0045)
- `docs/decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md` (Proposed)
- `docs/decisions/0112-connection-settings-applied-by-a-restart-that-waits-for-the-old-companion.md` (Proposed)
- `docs/decisions/0113-companion-texts-in-english-and-german-from-one-table.md` (Proposed)
- Deleted: `0102` (unreleased, replaced by 0111); its links in `live-sending.md` and `ARCHITECTURE.md` go in the Dev's
  documentation updates.

## Challenge

1. (major) A switch reconnect races the server's teardown — accepted: the switch closes with `CloseAsync` before the new
   hello, and a `companionConnected` refusal within 5 seconds of the switch is retried every 250 ms as part of the
   switch (no line, no delay step); tests with a fake connector refusing post-switch hellos and one against the real
   ingest endpoint (`CensioServerTests`); facts 19, 20; record 0111.
2. (minor) The guard is released after bounded waits — accepted, fact 9 corrected: on a restart the guard is disposed
   only after the hosted server stopped and was disposed (else left to the process exit), and a restart retries a port
   in use for 10 seconds (`LocalServerSession.PortInUseRetry`). The Tester's case is the port retry: `App.Dispose` is
   shell code no unit test reaches (*Test files*); record 0112.
3. (minor) `NeedsRestart` against the file at open — accepted: static `NeedsRestart(running, saved)` with
   `SettingsWindowContext.StartupValues` loaded in `OnStartup`; test `SettingsWindowSessionSaveComparesWithStartupValues`.
4. (minor) `language` always written — accepted: `Values.Language` is nullable, written only when the file had it or
   the field was changed; tests `…SaveWithoutLanguageLeavesKeyAbsent`, `SettingsWindowSessionSaveUntouchedLanguageWritesNone`.
5. (minor) A file problem on a disabled field — accepted: `SettingsView.IsFieldEnabled` keeps a field with a problem
   enabled in every mode, and `Validate` checks every field whatever the mode; AC3 row with an invalid `serverToken`
   in local mode.
6. (minor) Lone surrogates and control characters in names — accepted: `CompanionProfiles.CheckName`, shared by
   `CheckNewName` and the reader (`NameInvalidCharacters`, E6 text in the spec); validator and E16 rows.

## Security review

Fixes: B1, B2, B3, N1, N2, N3 (record 0110 and `companion-settings.md`), N4, N5. Justified exceptions: none; one
wording deviation in N4 (below).

- B1 (fixed, first option): figures are stored as the lowercase hex SHA-256 of the name's UTF-8 bytes; with names
  bounded to 64 UTF-16 code units and at most 6 bytes each, the writer's worst case is 567,086 bytes, computed in G3;
  `CompanionProfilesWorstCaseFileReadsBackValid` writes 1,000 names of 32 escaped surrogate pairs and 1,000 figures and
  reads the file back valid. `MaxFileLength` stays 1 MiB.
- B2 (fixed): each form is a rule of G2 or G3 and a test row. Invalid UTF-8 or `"\uD800"` in any string: E16 in the
  profiles reader, E7 in the settings document (every string read once at load, so no kept property reaches `WriteTo`
  undecodable; `Save` also catches `InvalidOperationException` and `ArgumentException`, deleting the `.tmp`). Known keys
  are found through the readers' own `FindProperties`, now `internal`. The profiles reader's default options, the
  duplicate and case-sensitive name checks, `TryGetInt32` for `version`, `Guid` value comparison and the shared
  well-formed name rule are listed in G3; fact 21 records the probe of each library behavior.
- B3 (fixed): `CompanionSettingsValues`, `CompanionProfile`, `ProfileNotice`, `SettingsView`, `SettingsInput` and
  `SettingsWindowContext` are sealed classes without `ToString`; tests `…ToStringHoldsNoToken`/`…HoldsNoName` (AC9).
- N1 (fixed): `Save`'s validation, folder checks included, and the file write run by `Task.Run`;
  `SettingsWindowSessionSaveChecksFolderOffTheCaller`; the `.squad/project.md` and `game-installation.md` texts are in
  the documentation updates.
- N2 (fixed): `RunAsync` writes the latest state when cancelled; `App.Dispose` waits up to 1 second for it before
  `_guard?.Dispose()`, and on a restart leaves the guard to the process exit when it did not complete (G4);
  `CompanionProfilesRunAsyncWritesLatestStateOnCancel`.
- N3 (fixed): record 0110's bullet replaced by Security's text; the Dev writes the same in `companion-settings.md`.
- N4 (fixed): `SECURITY.md` in the documentation updates, the Dev applying Security's three texts; deviation: "the
  figures linked to them" and "the figures you played them with" name the hash instead, because B1's fix keeps the
  names out of the file — Security confirms the wording.
- N5 (fixed): numbers written with `WriteNumber` of the parsed `int`; `…SaveWritesParsedNumber` (`05` → `5`).

## Out of scope / follow-ups

Renaming or deleting profiles in the Companion, a German diagnostics window and log, editing the hosted server's
settings (spec *Out of scope*).
