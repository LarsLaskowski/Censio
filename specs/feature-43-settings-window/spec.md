# Spec: Companion settings window

Source: Issue #43
Status: Draft

## Problem

Every Companion option lives in the hand-edited `%LOCALAPPDATA%\Censio\settings.json`
([`companion.md`](../../docs/areas/companion.md), *Settings file*), read once per start, never written (record 0045). A tester changes mode, address, token, buffer limits, port or game folder by editing JSON and
learns about a mistake only from the line `Server: settings invalid` and the log. Every playthrough goes to the fixed
profile `Live` (record 0102); record 0041 moves the profile choice and the figure notice into this window. #39 and #40
are merged; their keys follow [`local-mode.md`](../../docs/areas/local-mode.md) and
[`live-sending.md`](../../docs/areas/live-sending.md). Every Companion text is English today.

## Scenarios

- S1: Given the Companion runs, when the player chooses `Settings…` (directly above `Open diagnostics`, enabled in
  every mode), then the window `Censio Companion settings` shows the file's values, the defaults for missing keys (mode
  `Off`, port 5000, 24 hours, 100 megabytes, an empty game folder meaning `Find automatically`) and the autostart
  state. A second click brings the open window to the front.
- S2: Given mode `Local`, when the player picks `Forward`, enters an address and token and clicks `Save`, then the file
  holds `"mode": "forward"`, the address and the token and keeps `localPort`, the buffer limits and `gameFolder`;
  switching back keeps `serverAddress` and `serverToken`. `Off` is saved by removing `mode`; each known key is written
  once in the spelling of [`companion.md`](../../docs/areas/companion.md) (*Settings file*), other-case spellings removed.
- S3: Given a saved change of mode, port, address, token, buffer limits or game folder, then the window says `Saved.
  The Companion restarts to apply the changes.`; the Companion starts the same exe (a copy run from the ZIP included)
  and exits as `Exit` does, ending its single instance only after its hosted server stopped. The new Companion waits up
  to 10 seconds for that; one started by hand meanwhile still ends at once. A save without such a change restarts
  nothing.
- S4: `Start with Windows` ticked or cleared and saved switches the autostart as the menu entry does
  ([`companion.md`](../../docs/areas/companion.md), *Start with Windows*), at once.
- S5: Given `Live` selected (its history continues), when the player clicks `New profile…`, types a name and saves,
  then the new profile is selected, messages taken from then on carry it and the pages list it under that name;
  waiting messages keep their profile and reach the server under its name.
- S6: Selecting another profile and saving sends the messages taken from then on to it, at once, in both modes. Only a
  hello names a profile ([`contract.md`](../../docs/areas/contract.md)) and a second hello is refused
  ([`ingest.md`](../../docs/areas/ingest.md)), so a switch reconnects with a hello naming the active profile, and a next
  message of another profile than the connection's hello (taken before a switch, also offline) is sent after a
  reconnect naming its profile: no failure, no retry delay, `Server: connecting` until accepted.
- S7: Only in a run that sends live data (forward or local mode, also with `--record`): given active profile `B` and a
  figure last used with `A`, at its session start a Windows notification of the tray icon reads `This figure was last
  played with profile A. Click to open the settings and switch.`; ticks keep going to `B`, a click opens the window, an
  ignored notice links the figure to `B` (record 0041). A first figure on a profile without one is linked silently.
- S8: Given the active profile has a figure, a new figure's session start shows `This figure is new to profile B.
  Click to open the settings to switch or create a profile.`
- S9: Picking `Deutsch` under `Language` and saving switches, at once, the settings window, the notifications and
  every tray text to German: menu entries, status texts, the `Last tick`, `Game:`, `Catalog:`, `Server:` and `Buffer:`
  lines, the recording suffixes, the replay texts with the reasons of
  [`replay-sending.md`](../../docs/areas/replay-sending.md) and [`pipe-recording.md`](../../docs/areas/pipe-recording.md),
  and the tooltip, whose longest German text fits the tray's 127 characters; `English` switches back. Without a saved
  choice German shows when the Windows display language is German. The diagnostics window, log and web pages keep
  their own language.
- S10: `Browse…` puts the picked folder's full path in the game folder field; an empty field means `Find
  automatically`.

## Errors and edge cases

A message stands below its field, the typed values stay, and `Save` writes nothing while one is shown.

- E1: A port not a whole number from 1 to 65535: `Enter a whole number from 1 to 65535.`
- E2: Forward mode without an address: `Enter the server address.`; one breaking the `serverAddress` rule: `Enter a
  ws:// or wss:// address with a host, without user name, password or fragment.`
- E3: A token breaking the `serverToken` rule after trimming, a non-ASCII one included: `The token must be 32 to 1024
  visible ASCII characters (! to ~).` An empty token means none.
- E4: Hours or megabytes not whole numbers from 1 to 168 or 1 to 10240: `Enter a whole number from 1 to 168.` / `Enter a
  whole number from 1 to 10240.`
- E5: A game folder not a full path: `Enter a full path, such as C:\Games\Anno 117.`; missing: `This folder does not
  exist.`; failing the search's check ([`game-installation.md`](../../docs/areas/game-installation.md)): `This folder is
  not an Anno 117 installation.`
- E6: A new profile name blank: `Enter a name.`; over 64 characters: `Use at most 64 characters.`; equal to another
  ignoring case: `A profile of this name exists.`
- E7: A file that cannot be parsed (over 1 MiB, not JSON, not an object, unreadable): the window shows the defaults
  under `The settings file could not be read (<reason>). Saving replaces it with the values shown.`, the reason the
  log's; `Save` replaces it.
- E8: A key with a wrong value or written twice (`"localPort": "abc"`, `"gameFolder": 5`, two `mode`): its field is
  empty with its E1–E6 message or `This setting is written more than once in the file.` until the player edits it;
  `Save` stays blocked until then. The other fields show the file's values.
- E9: The file cannot be written: `The settings could not be saved. Details are in the log.`; the old file stays whole
  and nothing restarts.
- E10: The file changed after the window opened: `The settings file was changed outside this window. Close the window
  and open it again.`; nothing is written.
- E11: Unknown keys stay with their values; comments and key order are not kept.
- E12: The restart starts without arguments, so before saving the window warns `Saving ends the running replay.` /
  `Saving ends the running recording.`, and while the buffer is memory only
  ([`live-sending.md`](../../docs/areas/live-sending.md), *Buffer*) `Messages that could not be saved to disk are lost.`
- E13: `Cancel` or closing the window changes nothing.
- E14: The autostart cannot be written: logged as by the menu, the window shows `Start with Windows could not be
  changed.`; the other values are saved.
- E15: The exe cannot be started (moved, deleted): the old Companion keeps running with the old values and shows `The
  Companion could not restart. Exit and start it again to apply the changes.`; the file holds the saved values. A new
  Companion whose 10 seconds run out ends without an icon and logs `Restart failed: the previous Companion did not end`.

## Non-functional requirements

- N1: A file the window wrote, read back by the Companion's reader, gives the saved values; a hand-edited file keeps
  working ([`companion.md`](../../docs/areas/companion.md), *Settings file*). The file is written new and then
  replaced, so a crash leaves the old or the new one.
- N2: Privacy: the token is masked and in no log line; the figure name stays in the Companion's private folder, is
  never logged or sent, and no notice quotes it (record 0041). The profiles and figure links lie in a fixed file under
  `%LOCALAPPDATA%\Censio`, read with a size bound and failing closed.
- N3: The tray, pipe, menu and `Exit` never wait for the window, its write or its folder check.
- N4: An existing `settings.json` keeps its meaning; without the window nothing changes but the menu entry, and
  `Live`'s history continues.
- N5: Logged without values, names or paths ([`companion-log.md`](../../docs/areas/companion-log.md)): `Settings saved`
  (Information), `Could not save the settings` with the exception's type (Warning), `Profile switched` (Information),
  E15's line (Warning).
- N6: `README.md` and `docs/USER_GUIDE.md` (English, then German) describe the window, profiles with the notice, the
  restart and the language so external testers can try them, the hand-edited file as an alternative; the README menu
  list and the user guide's tooltip table give each text's German form in the German part.

## Acceptance criteria

- AC1: `Settings…` opens one window with the file's values, the defaults and the autostart state; `Cancel` changes
  nothing (S1, S10, E13, N3).
- AC2: Saving a mode switch writes the new mode (`Off` as no `mode`), keeps every other key, each known one once; the
  file read back gives the saved values; switching back needs no re-entry and no reinstall (S2, E11, N1, N4).
- AC3: Each invalid value gets its E1–E6 message, a non-ASCII token included; an invalid or duplicated file key blocks
  `Save` until its field is edited; nothing is written while a message shows (E1, E2, E3, E4, E5, E6, E8).
- AC4: A changed connection or game setting restarts the Companion with the saved values; the new one comes up while
  the old one shuts down (within 10 seconds, local mode's port included), a hand-started one still ends at once, an
  exe that cannot start leaves the old one running with E15's message; other saves restart nothing; replay, recording
  and memory-only buffer are warned about (S3, E12, E15).
- AC5: Autostart and language apply at once; the language reaches the window, every tray text of S9 and the
  notifications, the longest German tooltip within 127 characters; a failed autostart write is reported (S4, S9, E14).
- AC6: A new or selected profile gets every message taken after `Save`, in both modes; every hello names the profile of
  the messages its connection sends, so the pages name each profile, also one fed offline and switched away from before
  the reconnect; waiting messages keep their profile (S5, S6).
- AC7: A figure last used with another profile, or new on a profile with one, shows the notice; ticks are never held,
  an ignored notice links to the active profile, a first figure links silently; a replay, mode `Off`, an invalid server
  setting and an invalid command line show no notice and write no link (S7, S8).
- AC8: An unparseable file is replaced only on `Save` after the warning; a write failure or an outside change leaves
  the file as it was and is reported (E7, E9, E10).
- AC9: Token and figure name are in no log line; the profile file is read with a bound and fails closed; N5's lines
  are written (N2, N5).
- AC10: README and the user guide describe window, profiles, restart and language in English, then German, with the
  German form of each menu and tooltip text (N6).

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
- Q5: Is an invalid file overwritten? Answered: only a file that cannot be parsed at all (E7), on `Save` after the
  warning: for such a file `CompanionSettings.Read` and `ReadLocalServer` yield no game folder and mode `Off`, so it
  holds nothing the Companion uses. Every other wrong value is a single field (E8) and the rest of the file is kept.

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

## Challenge

1. (major) Profile name only in a hello — accepted: S6 reconnects with a hello naming the profile of the next message,
   also one buffered offline; AC6 asserts the name of a profile fed offline and switched away from.
2. (major) Restart against the single-instance guard and the port — accepted (`TryAcquire` does not wait): S3 gives the
   order and the 10-second wait, E15 the failures, AC4 tests both and the hand-started second Companion.
3. (major) Q5/E7 rested on a wrong claim — accepted (`ReadLocalServer` still reads the server keys): E7 only for
   unparseable files, bad or duplicated keys under E8, Q5 re-answered.
4. (major) Mode scope of notice and links — accepted: S7 limits them to live runs; AC7 tests replay, `Off`, invalid
   setting and command line.
5. (minor) `Off` and other-case keys — accepted: S2, N1 and AC2 (read-back test).
6. (minor) E8's default — accepted: empty field, message until edited, `Save` blocked (AC3).
7. (minor) E3 wording — accepted: visible ASCII (! to ~), non-ASCII case in AC3.
8. (minor) German tooltip and dynamic texts — accepted: S9 lists them, 127 characters, AC5.
9. (minor) Memory-only buffer at restart — accepted: E12 warns, AC4.
