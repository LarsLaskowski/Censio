# Plan: Rename a profile in the settings window

Source: Issue #255
Tier: standard — a new user-visible behavior (renaming a profile, and a reconnect of live sending after it) that creates, changes or removes no security control.
Security areas touched: Parsing of external input (`CompanionProfiles`: the name rules a rename shares with the reader); File paths the Companion writes (`CompanionProfiles` writes `profiles.json` after a rename); Logging and showing external data (`SettingsWindow` and the new `RenameProfileDialog` show profile names, the new log line `Profile renamed`); Outbound calls (`LiveSender` reconnects and sends the new name in the hello)

## Problem / root cause

The Companion can create, select and delete profiles but not rename one (`docs/areas/companion-profiles.md:22`, "they
are not renamed"; no rename member in `CompanionProfiles.cs`). The issue asks for the rules — what the user sees
meanwhile and offline — and for merging to be considered. The rules are stated in the area documents (the lasting
"spec" of a bug-fix run) and record 0117; merging goes to a follow-up issue (*Out of scope*).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The server renames a known profile when a hello gives it another name, and pages update without a reload. | `ServerStore.cs:59` (`ON CONFLICT ("Id") DO UPDATE SET "Name"`), `IngestSession.cs:414`; `web-pages.md:184` | confirmed |
| 2 | Only a hello names a profile; a connection lasts until it fails or a switch ends it. | `LiveSender.cs:997-1005` (hello per `AttemptAsync`), `IsSwitchDue` `:1094-1112` | confirmed |
| 3 | A switch is keyed by identity only, so today a rename of the connected profile would not reconnect. | `LiveSender.cs:1103` (`profiles.Active.Id == _connectionProfile`), `:165` (`Guid _connectionProfile`) | confirmed |
| 4 | Buffered messages carry only the profile identity; the hello for them takes the name from the list when it is sent. | `LiveSender.cs:1236-1247` (`profiles.NameOf(id)` in `IsOfOtherProfile`), `ProfileOf` `:524` | confirmed |
| 5 | `CompanionProfile` is immutable (`init`); `Select` compares the active profile by reference. | `CompanionProfile.cs`; `CompanionProfiles.cs:409` | confirmed |
| 6 | The tray menu reads `Active.Name` each time it opens. | `TrayIcon.cs:98-101`, `:185` | confirmed |
| 7 | `ActiveChanged` has one subscriber, the live sender. | `LiveSender.cs:353`; grep of `src` | confirmed |
| 8 | A replay never sends under `Live`: it makes its own profile `Replay <time>`. | `replay-sending.md:86` | confirmed |
| 9 | A file that cannot be used, or a missing one, gives `Live` alone under the name `Live`. | `CompanionProfiles.cs:624-627` | confirmed |
| 10 | The window's delete flow takes effect at once through `DeleteRequested`, ignored while a save runs. | `SettingsWindowSession.cs:344-360`, `SettingsWindow.xaml.cs:480-501` | confirmed |
| 11 | Records 0111 and 0115 are unreleased (no tag contains them), so they are edited in place. | `git tag --contains bada5d0` / `7125c16`: empty; tags up to `v0.4.0` | confirmed |

## Acceptance criteria

- AC1: `CompanionProfiles.Rename(id, name)` gives a listed profile other than `Live` (active or not) the new name; its
  identity, its place in the list, its figure links and whether it is active stay; the file written afterwards holds the
  new name and is read back by `Load` as written; `Rename` returns `true`.
- AC2: The rename name rules are those of a new name except the count: `CheckRename` gives `NameBlank`, `NameTooLong`,
  `NameInvalidCharacters` as `CheckName` does, `NameExists` for the name of **another** profile ignoring case, and
  `null` for the profile's own name in another letter case and with 1,000 profiles listed (never `ProfilesFull`).
  `Rename` throws `ArgumentException` for `Live`, an identity not listed and a name `CheckRename` refuses, and then
  changes nothing (list, file version, events).
- AC3: A rename to the identical name (ordinal) returns `false` and changes nothing: no file write, no event, no log
  line, the pending notice kept.
- AC4: A rename that changes the name clears a pending notice (raising `PendingNoticeChanged` when there was one),
  raises `Renamed` once, and logs `Profile renamed` at Information with neither the old nor the new name nor the
  identity.
- AC5: `SettingsView.CanRename` is `false` for `Live` and an identity not in `ProfileList`, `true` for every other
  listed profile, the active one included; `SettingsView.CheckRename` gives what `CompanionProfiles.CheckRename` gives
  over `ProfileList`.
- AC6: `SettingsWindowSession`, on `RenameRequested` of the open window after its first view, renames the profile and
  calls `ShowProfiles` once with the new name in `ProfileList`, without a save and without `ShowView` (other edits
  kept); it ignores the request from another sender, before the first view, while a save runs, for `Live`, and for a
  name `CheckRename` refuses; after `WindowClosed` it no longer handles it.
- AC7: Live sending: a rename of the profile the current connection's hello named ends that connection with the close
  handshake and opens a new one whose hello names the same identity with the new name, handled as a switch (`Server:
  connecting`, no `Live sending failed` line, no delay step; a `companionConnected` refusal within 5 seconds retried).
  When that profile is not the active one (a connection for buffered messages), the new hello names that profile, not
  the active one. A rename of any other profile keeps the connection.
- AC8: Offline: after a rename made while no connection is accepted (retrying, or before the sender runs), the next
  accepted hello names the new name; buffered messages of a renamed profile other than the active one, taken before the
  rename, are sent on a connection whose hello names the new name.
- AC9: The texts, exactly: `Rename profile…` / `Profil umbenennen…`; `Live cannot be renamed.` / `Live kann nicht
  umbenannt werden.`; `Rename profile` / `Profil umbenennen`; `New name of the profile {0}` / `Neuer Name des Profils
  {0}`; `Rename` / `Umbenennen`.
- AC10: `README.md` and `docs/USER_GUIDE.md` (English, then German) describe renaming, the offline case and that
  `Live` cannot be renamed, and no longer say that a profile cannot be renamed.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionProfilesRenameKeepsIdentityOrderLinksAndActive` (data rows: active, not active), `CompanionProfilesRenameWrittenAndReadBack` | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |
| AC2 | `CompanionProfilesCheckRenameGivesProblemOfName` (data rows: blank, 65 characters, a lone high surrogate (U+D800), another profile's name in other case, own name in other case, 1,000 profiles), `CompanionProfilesRenameRefusedThrowsAndChangesNothing` (data rows: `Live`, unknown identity, refused name) | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |
| AC3 | `CompanionProfilesRenameSameNameChangesNothing` | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |
| AC4 | `CompanionProfilesRenameClearsNoticeRaisesRenamedAndLogsWithoutName` | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |
| AC5 | `SettingsViewCanRenameAllButLiveAndUnknown`, `SettingsViewCheckRenameGivesProblemOfProfiles` | `tests/Censio.Companion.Core.Tests/SettingsViewTests.cs` |
| AC6 | `SettingsWindowSessionRenameRenamesAndShowsProfiles`, `SettingsWindowSessionRenameIgnored` (data rows: other sender, before first view, during save, `Live`, refused name, after close) | `tests/Censio.Companion.Core.Tests/SettingsWindowSessionTests.cs` |
| AC7 | `LiveSenderProfileRenameOfConnectionProfileReconnectsWithNewName`, `LiveSenderProfileRenameOfBufferedConnectionProfileNamesThatProfile`, `LiveSenderProfileRenameOfOtherProfileKeepsConnection` | `tests/Censio.Companion.Core.Tests/LiveSenderProfileTests.cs` |
| AC8 | `LiveSenderProfileRenameWhileOfflineNextHelloNamesNewName`, `LiveSenderProfileRenameBufferedOtherProfileSentUnderNewName` | `tests/Censio.Companion.Core.Tests/LiveSenderProfileTests.cs` |
| AC9 | `CompanionTextsRenameTextsInBothLanguages` | `tests/Censio.Companion.Core.Tests/CompanionTextsTests.cs` |
| AC10 | check: the Reviewer reads both sections in step 8 | — |

## Approach

1. **`CompanionProfiles`.** `IsRenamable` and `CheckRename` are static over a list, like `IsDeletable`. One private
   static helper `IsNameTaken(name, except, profiles)` (ignoring case, skipping the identity `except`) serves both
   `CheckNewName` (rewritten onto it, no change of behavior) and `CheckRename`. `Rename` takes the lock, throws as AC2
   says, returns `false` on an ordinal-equal name, else replaces the entry at its index by a new `CompanionProfile`
   with the same `Id` and assigns `_active` to it when it was active (never through `Activate`, which moves figure
   links), clears the notice and calls `MarkChanged`; outside the lock it logs `CompanionLog.ProfileRenamed`, raises
   `PendingNoticeChanged` when the notice changed, then `Renamed`.
2. **`LiveSender`.** `UseProfiles` subscribes `Renamed` to the same wake-up as `ActiveChanged` (`OnActiveChanged`; the
   counter's doc says "changes of the active profile or of a name"). A new run-only field `_connectionName` is set in
   `ChooseHelloProfile` with `_connectionProfile`. `IsSwitchDue` keeps its identity check and adds: when
   `profiles.NameOf(_connectionProfile)` is not null and differs ordinally from `_connectionName`, it sets
   `_helloProfile` to that profile with the new name when `_connectionProfile` is not the active one (else `null`),
   sets `_isSwitching` and returns `true`. A connection whose profile left the list (deleted) is not switched by this
   check. The switch path (close handshake, `TryContinueSwitchAsync`) is reused unchanged.
3. **Window session.** `SettingsRename` carries the request; `ISettingsWindow.RenameRequested` is subscribed in `Open`
   and removed in `OnWindowClosed`; a private `OnRenameRequested` mirrors `OnDeleteRequested` (same guards, then
   `IsRenamable` and `CheckRename` over the current list), calls `Rename`, then `ShowProfilesAgain`.
4. **Shell (`Censio.Companion`).** Layout: the profile row reads, left to right, the drop-down, `New profile…`,
   `Rename profile…`, `Delete profile…` (XAML docks `Delete`, then `Rename`, then `New` to the right). `Rename profile…`
   is disabled for `Live` with the tooltip `Live cannot be renamed.` (`ToolTipService.ShowOnDisabled`), updated with
   the delete button on every selection change. `RenameProfileDialog` (width 440, `CenterOwner`, no taskbar entry, title
   `Rename profile`): the label `New name of the profile <name>` (wrapping), a text box holding the current name with
   all of it selected and focused, a message line below it (Firebrick, collapsed until a problem), then `Rename`
   (default) and `Cancel` right-aligned. `Rename` checks `view.CheckRename` and, on a problem, shows
   `texts.FormatProblem(kind)` and stays open; otherwise it closes with `true` and the window raises `RenameRequested`.
5. **What the user sees.** The rename shows in the drop-down at once, in the tray menu's `Server:` line at its next
   opening, and on the pages once a hello names it: within seconds when connected under that profile (one reconnect),
   otherwise at the next connection that names the profile. Stated in `companion-profiles.md` (*Renaming*).

Early draft PR: no

## Options considered

See record 0117: a name box applied by `Save` (lost: `Save` also activates the selected profile); the server following
only the next natural hello (lost: the old name stays for a whole connection); a contract rename message (lost: a
contract version for what a reconnect does); `Live` renamable (lost: texts name it and a lost file restores `Live`);
a rename onto another profile's name merging the two (lost: a server-side merge of histories, follow-up issue).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `CompanionProfiles` | `IsRenamable`, `CheckRename`, `Rename`, event `Renamed`, helper `IsNameTaken` |
| `Censio.Companion.Core` | `LiveSender` | `Renamed` subscription, `_connectionName`, `IsSwitchDue` |
| `Censio.Companion.Core` | `SettingsRename` (new), `ISettingsWindow`, `SettingsView`, `SettingsWindowSession` | the rename request |
| `Censio.Companion.Core` | `CompanionTexts`, `CompanionLog` | five texts per language, `ProfileRenamed` |
| `Censio.Companion` | `SettingsWindow.xaml(.cs)`, `RenameProfileDialog.xaml(.cs)` (new) | button and dialog |

## Signatures (for the Dev's skeleton)

- `public static bool IsRenamable(Guid id, IReadOnlyList<CompanionProfile> profiles)` (`CompanionProfiles`)
- `public static SettingsProblemKind? CheckRename(Guid id, string name, IReadOnlyList<CompanionProfile> profiles)` (`CompanionProfiles`)
- `public bool Rename(Guid id, string name)` (`CompanionProfiles`; `ArgumentException` as AC2)
- `public event EventHandler? Renamed;` (`CompanionProfiles`)
- `public sealed class SettingsRename` with `public required Guid Profile { get; init; }` and `public required string Name { get; init; }`
- `event EventHandler<SettingsRename>? RenameRequested;` (`ISettingsWindow`)
- `public bool CanRename(Guid selected)` and `public SettingsProblemKind? CheckRename(Guid profile, string name)` (`SettingsView`)
- `public required string RenameProfileText { get; init; }`, `public required string RenameProfileDisabledText { get; init; }`, `public required string RenameProfileTitle { get; init; }`, `public required string RenameProfileTextFormat { get; init; }`, `public required string RenameProfileConfirmText { get; init; }` (`CompanionTexts`, both instances)
- `[LoggerMessage(Level = LogLevel.Information, Message = "Profile renamed")] public static partial void ProfileRenamed(ILogger logger);` (`CompanionLog`)

`RenameProfileDialog(CompanionTexts texts, SettingsView view, CompanionProfile profile)` with `NewName` is internal to
the shell. All names checked against *Writing code*: no nested type, at most three parameters, no keyword names.

## Test files

The five files of *Tests per criterion*. Existing test code: `FakeSettingsWindow` implements `ISettingsWindow`, so the
Dev adds the `RenameRequested` event declaration in step 4 (mechanically, *Build* passes); the Tester adds
`RequestRename` and `RaiseRenameRequested` in step 5. `CompanionTextsTests._texts` (`CreateTexts`) gets the five new
texts (Tester), so `CompanionTextsGermanDiffersFromEnglishInEveryText` covers them. No other call site changes.

Files no unit test can cover: `SettingsWindow.xaml(.cs)` and `RenameProfileDialog.xaml(.cs)`, the Windows shell, already
outside every coverage measure (record 0004; `stack.md`, *Coverage exclusions*): no new exclusion.

## Areas

- `docs/areas/companion-profiles.md (+190 words)`: *The list and its file* — "they are not renamed" becomes "renamed
  (*Renaming*)"; a new subsection *Renaming* after *Creating and selecting* with the rules of AC1–AC4, AC7, AC8 and
  *What the user sees*; *Notice* — a rename also clears a pending notice; *Related decisions* — record 0117.
- `docs/areas/companion-settings.md (+70 words)`: *Fields* — the field list and a bullet for `Rename profile…`, its
  disabled tooltip and the dialog, taking effect at once without `Save`; *Validation* — the rename name takes the
  new-name rules except `No more profiles can be created.`, its message shown in the dialog.
- `docs/areas/live-sending.md (+60 words)`: *Profiles on the connection* — a rename of the connection's profile
  reconnects as a switch (AC7); renaming another profile keeps it.
- `docs/areas/companion-texts.md (+60 words)`: the window table gets `Rename profile…`; a table *Profile renaming*
  with the other four texts, noting `Cancel` is `CancelText`.
- `docs/areas/companion-log.md (+8 words)`: `Profile renamed` (Information) in the list of what is logged.

## Documentation updates

All by the Dev, once the code is green:

- `docs/USER_GUIDE.md`: *Settings window* field table row `Profile` (also `Rename profile…`); *Profiles* — a bullet
  **Rename** (the button, dialog, rules, `Live` excluded, at once, what the pages show connected and offline, buffered
  messages under the new name), and the sentence "A profile cannot be renamed." removed from **Delete**; the same in
  the German *Einstellungsfenster* and *Profile* (**Umbenennen**); the "Not yet tried on Windows" line of *Settings
  window* names the rename dialog too (both languages).
- `README.md`: *Profiles* paragraph and the settings-window sentence (line 20) mention renaming, English and German.
- `docs/ARCHITECTURE.md`: none — no flow or guarantee changes (the hello already names the profile).
- `.squad/project.md`: none — `SettingsWindow` and `CompanionProfiles` already stand in their areas, and the new dialog is
  covered by "`SettingsWindow` with … profile names"; the Reviewer confirms.

## Architecture check

No guarantee of `.squad/project.md` is touched: the token, the buffer and its order, retention and deletion stay as
they are. A rename reconnect reuses the switch path, so no tick is dropped (messages wait in the buffer).

## Security considerations

- [x] Limits: the name is at most 64 UTF-16 code units, judged on the decoded string as for a new name; the file's
  1 MiB bound is unchanged (the largest file stays the one 1,000 escaped 64-character names give).
- [x] Exceptions: `Rename`'s `ArgumentException` never reaches the user — the dialog and the session check first; a
  write failure is the existing `Could not save the settings: <exception type>` log line.
- The new name is the player's text: shown in the window and dialog (WPF text, no markup), written to `profiles.json`
  and sent in the hello, never logged (AC4).

## Decision records

- `docs/decisions/0117-profile-renamed-in-a-dialog-at-once-the-connection-reopened-with-the-new-name.md` (Proposed),
  indexed; records 0111 and 0115 (unreleased) now point to it instead of saying renaming is not done.

## Out of scope / follow-ups

- Merging two profiles — proposed issue: title `[Companion] Merge two profiles`, body: "Split from #255 (record 0117,
  option 7). A player who fed one playthrough into two profiles wants one history. Merging moves one profile's ticks,
  hourly sums, trade routes and held state into another on the server and needs a contract message and a server
  operation; the Companion's names stay distinct ignoring case. Decide whether it is wanted and specify it."
- Renaming a profile only the server has (deleted in the Companion with its history kept): not possible, as deleting it.
