# Spec: Companion settings window

Source: Issue #43
Status: Draft

## Problem

Every Companion option lives today in the hand-edited file `%LOCALAPPDATA%\Censio\settings.json`
([`companion.md`](../../docs/areas/companion.md), *Settings file*), which the Companion reads once per start and never
writes (record 0045). A tester changes mode, address, token, buffer limits, port or game folder by editing JSON and
learns about a mistake only from the line `Server: settings invalid` and the log. Only the autostart has a menu entry. Every
playthrough goes to the one fixed profile `Live` (record 0102), so two playthroughs mix in one history; record 0041
moves the profile choice and the figure-name notice into this window. #39 (local mode, PR #238) and #40 (forward mode,
PR #240) are merged, and the keys `mode`, `localPort`, `serverAddress`, `serverToken`, `bufferHours`,
`bufferMegabytes` and `gameFolder` exist with the rules of [`local-mode.md`](../../docs/areas/local-mode.md) and
[`live-sending.md`](../../docs/areas/live-sending.md) (*Switching on*, *Buffer*). There is no `language` setting: every
Companion text is English today.

## Scenarios

- S1: Given the Companion runs, when the player chooses `Settings…` in the tray menu (directly above `Open
  diagnostics`, enabled in every mode), then the window `Censio Companion settings` opens showing the values the
  settings file holds now, the default where a key is missing (mode `Off`, port 5000, 24 hours, 100 megabytes, an empty
  game folder meaning `Find automatically`), and the autostart state the menu entry shows. A second click brings the
  open window to the front.
- S2: Given the window shows mode `Local`, when the player picks `Forward`, enters a valid server address and token
  and clicks `Save`, then the file holds `"mode": "forward"` with the address and the token and still holds `localPort`,
  the buffer limits and `gameFolder`; switching back to `Local` later keeps `serverAddress` and `serverToken`. No
  reinstall is needed and no other setting is lost.
- S3: Given the player saved a change of mode, port, server address, token, buffer limits or game folder, then
  the window says `Saved. The Companion restarts to apply the changes.`, the Companion exits as `Exit` does and starts
  again from the same exe, and the menu then shows the new `Server:` and `Game:` lines. A save that changed none of these
  restarts nothing.
- S4: Given the window, when the player ticks or clears `Start with Windows` and clicks `Save`, then the autostart is
  switched as the menu entry switches it ([`companion.md`](../../docs/areas/companion.md), *Start with Windows*), at
  once and without a restart.
- S5: Given the profile list shows `Live` selected (the existing profile, so its history continues), when the
  player clicks `New profile…`, types a name and clicks `Save`, then the new profile is selected, the messages taken
  from then on carry it, and the server's pages list it under that name once the Companion has sent it; the messages
  already waiting in the buffer keep the profile they were taken for.
- S6: Given two profiles, when the player selects the other one and clicks `Save`, then the messages taken from
  then on go to it, at once and without a restart, in forward and local mode alike.
- S7: Given the active profile is `B` and the figure loaded in the game was last used with profile `A`, when its
  session starts, then a Windows notification of the tray icon reads `This figure was last played with profile A.
  Click to open the settings and switch.`; ticks keep going to `B` meanwhile, a click opens the window, and a notice
  left alone links the figure to `B` (record 0041). A figure seen for the first time while the active profile has no
  figure is linked to it silently.
- S8: Given the active profile already has a figure and a figure never seen before starts a session, then the
  notification reads `This figure is new to profile B. Click to open the settings to switch or create a profile.`
- S9: Given the window, when the player picks `Deutsch` under `Language` and clicks `Save`, then the settings window,
  every tray menu entry, the tooltip and the notifications of S7 and S8 show their German texts at once, without a
  restart; `English` switches back. Without a saved choice the Companion shows German when the Windows display language
  is German and English otherwise. The diagnostics window, the log lines and the web pages keep their own language.
- S10: Given the game folder field, when the player clicks `Browse…` and picks a folder, then the field shows its full
  path; clearing the field means `Find automatically`.

## Errors and edge cases

Each message stands below its field in the window, `Save` writes nothing while one is shown, and the values typed stay.

- E1: A port that is not a whole number from 1 to 65535: `Enter a whole number from 1 to 65535.`
- E2: Forward mode without an address: `Enter the server address.`; an address that breaks the rule of `serverAddress`:
  `Enter a ws:// or wss:// address with a host, without user name, password or fragment.`
- E3: A token that breaks the rule of `serverToken` after trimming: `The token must be 32 to 1024 visible characters
  without spaces.` An empty token is valid (no token).
- E4: Buffer hours outside 1 to 168, or megabytes outside 1 to 10240, or not whole numbers: `Enter a whole number from 1
  to 168.` / `Enter a whole number from 1 to 10240.`
- E5: A game folder that is not a full path: `Enter a full path, such as C:\Games\Anno 117.`; one that does not exist:
  `This folder does not exist.`; one that is not an Anno 117 installation by the search's own check
  ([`game-installation.md`](../../docs/areas/game-installation.md)): `This folder is not an Anno 117 installation.`
- E6: A new profile name that is empty or blank: `Enter a name.`; longer than 64 characters: `Use at most 64
  characters.`; equal to an existing profile's name ignoring case: `A profile of this name exists.`
- E7: A settings file that is invalid as a whole (too large, not JSON, not an object, unreadable, a duplicated key) when
  the window opens: the window shows the defaults and above them `The settings file could not be read (<reason>).
  Saving replaces it with the values shown.`, the reason the one the log names today; `Save` then replaces the file.
- E8: Single keys with a wrong value (a `localPort` of `"abc"`): the window shows the default for that field with its
  message from E1–E6, so `Save` asks for a valid value first.
- E9: The file cannot be written (access denied, disk full, a folder of that name): `The settings could not be saved.
  Details are in the log.`; the old file stays whole, never half written, and nothing restarts.
- E10: The file was changed by another program after the window opened: `Save` says `The settings file was changed
  outside this window. Close the window and open it again.` and writes nothing.
- E11: Keys the window does not know stay in the file with their values when it saves; the file's comments and the
  order of its keys are not kept.
- E12: A save during a replay or with `--record`: the restart of S3 starts the Companion without arguments (as the
  autostart does), so the replay or recording ends; the window says so before it saves: `Saving ends the running
  replay.` / `Saving ends the running recording.`
- E13: `Cancel` or closing the window writes nothing and changes nothing.
- E14: The autostart cannot be written: the message of the menu applies (`Could not change the autostart setting`
  logged) and the window shows `Start with Windows could not be changed.`; the other values are saved.

## Non-functional requirements

- N1: The settings file stays readable by the rules of [`companion.md`](../../docs/areas/companion.md) (*Settings
  file*); a file the window wrote is a file the Companion reads with the same result, and a hand-edited file keeps
  working. It is written as a whole new file and then replaced, so a crash leaves the old or the new file.
- N2: Privacy: the token is shown masked and appears in no log line; the figure name is kept only in the Companion's
  private folder, never logged, sent or shown outside the notice's own context (record 0041), and the notice does not
  quote it. The profile list and the figure links lie in a fixed file under `%LOCALAPPDATA%\Censio`, read with a size
  bound and failing closed like the settings file.
- N3: The tray, the pipe, the menu and `Exit` never wait for the window, its file write or its folder check; the window
  opens within a second on the target host.
- N4: Compatibility: an existing `settings.json` keeps its meaning; a player who never opens the window sees no
  change except the new menu entry, and the existing history of `Live` continues under its identity.
- N5: Logging: saving logs `Settings saved` (Information), a failed write `Could not save the settings` with the
  exception's type (Warning), a profile switch `Profile switched` (Information), without values, names or paths
  ([`companion-log.md`](../../docs/areas/companion-log.md)).
- N6: Documentation: `README.md` and `docs/USER_GUIDE.md` (English first, then German) describe the window, the
  profile choice with the notice, the restart and the language choice, so an external tester can try them; the
  hand-edited file stays documented as an alternative. The menu list of `README.md` and the tooltip table of the user
  guide give each text in English and, in the German part, its German form.

## Acceptance criteria

- AC1: `Settings…` opens one window showing the file's values, the defaults for missing keys and the autostart
  state; `Cancel` changes nothing (S1, S10, E13, N3).
- AC2: Saving a mode switch writes the new mode and keeps every other key of the file, known or unknown; switching
  back restores the earlier values without re-entering them, and no reinstall is needed (S2, E11, N1, N4).
- AC3: Each invalid value is refused with its message from E1–E6 and E8, and `Save` writes nothing while one is shown
  (E1, E2, E3, E4, E5, E6, E8).
- AC4: Saving a change of a connection or game setting restarts the Companion, which then runs with the saved values;
  a save without such a change restarts nothing, and a replay or recording is warned about first (S3, E12).
- AC5: The autostart and the language apply at once on `Save`, the language to the window, the tray menu and the
  tooltip; a failed autostart write is reported (S4, S9, E14).
- AC6: A new or selected profile receives every message taken after `Save`, at once and in both modes; waiting
  messages keep their profile; the pages show the new profile's name (S5, S6).
- AC7: A session start of a figure last used with another profile, or of a new figure on a profile that has one, shows
  the non-blocking notice; ticks are never held, an ignored notice links the figure to the active profile, a first
  figure is linked silently (S7, S8).
- AC8: An invalid file is replaced only on `Save` after the warning; a write failure or a file changed outside the
  window leaves the file as it was and is reported (E7, E9, E10).
- AC9: The token and the figure name appear in no log line; the profile and figure file is read with a bound and fails
  closed; the logged lines of N5 are written (N2, N5).
- AC10: README and the user guide describe the window, profiles, restart and language in English, then German, the
  menu list and the tooltip table with each text's German form (N6).

## Out of scope

- Editing the hosted server's settings or its token, deleting or renaming a profile (history is deleted on the pages,
  [`history-deletion.md`](../../docs/areas/history-deletion.md)), reading the server's profile list, the command-line
  switches, the diagnostics window's columns and the log lines' language.

## Open questions

- Q1 (apply): How do mode, port, server address, token, buffer limits and game folder take effect?
  Answered: by the Product Manager, `Save` restarts the Companion automatically, a running replay or recording warned
  about first (S3, E12); profile, autostart and language apply at once.
- Q2 (language): What does `Language` switch?
  Answered: by the Product Manager, the Companion's own texts (settings window, tray menu, tooltip) between English
  and German, default the Windows display language (S9); the web pages keep their own switch
  ([`web-ui.md`](../../docs/areas/web-ui.md)).
- Q3 (profiles): Where is the profile list kept and how is the notice shown?
  Answered: by the Product Manager, the Companion keeps the list locally, `Live` its first entry, create and select
  only; the notice is a Windows notification of the tray icon whose click opens the window (S5–S8).
- Q4: Who writes the file? Answered: the window writes `settings.json`; record 0045 (released in v0.4.0) is superseded
  by a new record, and record 0102 by the profile choice, as 0102's own consequences ask.
- Q5: Is an invalid file overwritten? Answered: only on `Save` after the warning of E7, since the Companion ignores
  such a file anyway (the Lead's choice, open to the challenge).

## Area documents

- New `docs/areas/companion-settings.md`: the window, its fields and defaults, the validation texts, writing the file
  (unknown keys kept, comments dropped, replace-on-write, changed-outside check), what applies at once and what by
  restart.
- New `docs/areas/companion-profiles.md`: the profile list and its file, creating and selecting, the figure links and
  the notice, privacy of the figure name.
- `docs/areas/companion.md`: the `Settings…` entry in the menu list; *Settings file* no longer says "never written";
  the restart; the language choice, its default and which texts follow it, with the German form of each menu and
  tooltip text.
- `docs/areas/live-sending.md` and `local-mode.md`: the selected profile instead of `Live`, the sentences "read once
  per start, never written" and "a change counts after a restart"; the accepted risk *One profile* removed.
- `docs/areas/companion-log.md`: the new lines of N5.
