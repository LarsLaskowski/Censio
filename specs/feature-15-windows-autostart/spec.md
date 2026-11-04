# Spec: Optional Windows autostart for the Companion

Source: Issue #15
Status: Draft

## Problem

A player has to start the Companion by hand before or after the game today: the tray menu has no autostart entry
and the Companion writes nothing that Windows starts at sign-in (checked: `TrayMenuEntryKind` and
`src/Censio.Companion/` have no such entry or code). A player who forgets it loses the data of that session. Record
[0017](../../docs/decisions/0017-companion-tray-app-autostart-and-connect.md) already decided that starting with
Windows is a per-user setting, off by default, without administrator rights; the Companion ships as an unsigned exe
in a ZIP without an installer ([0027](../../docs/decisions/0027-companion-zip-self-contained-single-file-unsigned.md)),
so the exe lives wherever the player extracted it.

## Scenarios

- S1: Given a Companion that was never set to start with Windows, when the player opens the tray menu, then it shows
  the entry `Start with Windows` without a check mark, between `Open log folder` and `Version <version>`, set off by
  a separator above and below it.
- S2: Given S1, when the player clicks `Start with Windows`, then the next time the menu opens the entry carries a
  check mark, and Windows' startup apps list (Settings > Apps > Startup, Task Manager > Startup apps) shows the
  Companion as enabled. The running Companion goes on unchanged.
- S3: Given S2, when the player signs out and in again or restarts the PC, then the Companion starts with the
  player's sign-in exactly as a start by hand without switches: one icon, no window, `Waiting for game` until the
  game runs, recording off, and the menu shows `Start with Windows` checked.
- S4: Given autostart is on, when the player clicks the checked `Start with Windows`, then the check mark is gone
  the next time the menu opens, the running Companion goes on, and after the next sign-in or restart no Companion
  starts.
- S5: Given a Companion started with `--record`, `--replay <file>` or an invalid command line, when the player
  toggles `Start with Windows`, then it works as in S2 and S4, and the start at sign-in is a start without switches.
- S6: Given another Windows user on the same PC, when one user turns autostart on or off, then the other user's
  setting and sign-in are not affected.

## Errors and edge cases

- E1: The setting cannot be written or removed (for example a policy blocks it). The check mark shows the state
  that holds afterwards, read again when the menu opens; no message or dialog appears, and a warning is logged.
- E2: The player moved or renamed the exe, or extracted a new version to another folder, after turning autostart
  on. The entry starts the old path: if the exe is gone, nothing starts at sign-in and Windows shows nothing; if an
  old copy remains, that copy starts. A Companion started from the new path shows `Start with Windows` unchecked,
  since autostart does not start this exe; a click turns it on for the new path and replaces the old entry.
- E3: Two copies of the exe in different folders. There is one autostart setting per Windows user: turning it on
  in one copy replaces the other's, whose menu then shows the entry unchecked. Only the copy that holds it is
  started at sign-in.
- E4: The player disabled the Companion in Windows' startup apps list after turning it on in the menu. The menu
  shows `Start with Windows` unchecked; a click turns it on again, and the startup apps list shows it enabled.
- E5: The exe's path holds spaces, a comma, non-ASCII letters or is long (up to the Windows path limit the
  Companion already runs from). Autostart starts the exe at that path.
- E6: The player started the exe from inside the ZIP without extracting it, so Windows ran it from a temporary
  folder. Turning autostart on registers that path, which no longer exists after the next cleanup: nothing starts
  at sign-in (as E2). README and user guide say to extract the ZIP to a folder that stays.
- E7: Repeated clicks. Each click toggles; the check mark always shows the state of the last click that took
  effect (E1). A second Companion cannot run in the session (*Single instance*), so no two clicks race.
- E8: The Companion is started at sign-in while one already runs in the session (started by hand before the
  sign-in finished). The new one ends at once as *Single instance* says.
- E9: The exe is unsigned. Whether SmartScreen asks again at the autostart after the player chose "Run anyway"
  once is to be checked on Windows (AC6); README and user guide describe what the player sees.

## Non-functional requirements

- Per-user only: turning autostart on or off needs no administrator rights, writes only settings of the signed-in
  Windows user, and changes nothing for other users. The Companion writes no file of its own for it; the state is
  the one Windows holds, so Windows' startup apps list and the menu agree.
- Off by default: a Companion that was never toggled, on a new or an updated version, does not start with Windows.
- What starts at sign-in is only the Companion exe itself, by its full path, without switches.
- The menu does not wait noticeably: toggling and opening the menu stay as quick as the other entries.
- Log: `Autostart enabled` and `Autostart disabled` (information) at the click, and a warning when the change
  fails (E1), with the exception text as the log already allows (record 0037). The exe path is not logged by these
  lines.
- Compatibility: Windows 10 22H2 and Windows 11, x64, as the release ZIP ([`companion.md`](../../docs/areas/companion.md),
  *Platform*). No new configuration besides the menu entry.
- Removing the Companion: turning autostart off before deleting the exe leaves nothing behind; a stale entry for a
  deleted exe starts nothing (E2), and README and user guide say how to remove it.

## Acceptance criteria

- [ ] AC1: The tray menu holds `Start with Windows` between `Open log folder` and `Version <version>`, with a
  separator above and below, enabled in every mode (live, `--record`, replay, invalid command line), checked
  exactly when Windows starts this exe at sign-in and unchecked otherwise, read when the menu opens (S1, S5, E2,
  E3, E4).
- [ ] AC2: Clicking the unchecked entry turns autostart on for this exe by its full path without switches,
  replacing an entry for another path and re-enabling one disabled in Windows' startup apps list; clicking the
  checked entry turns it off; the running Companion goes on in both cases (S2, S4, S5, E2, E3, E4, E5, E7).
- [ ] AC3: Autostart is off until the player turns it on; nothing the Companion does on start, `Exit` or in any
  mode turns it on or off (S1, S5).
- [ ] AC4: A change that fails leaves the check mark at the state that holds, shows no message, and logs a warning;
  a successful click logs `Autostart enabled` or `Autostart disabled`, without the exe path (E1).
- [ ] AC5: Turning autostart on or off writes only the signed-in user's settings and needs no administrator rights
  (S6).
- [ ] AC6: Verified by hand on Windows with the release ZIP: on, then restart, the Companion starts as in S3 (and
  whether SmartScreen asks, E9); off, then restart, no Companion starts; the startup apps list follows the menu and
  the menu follows the startup apps list (S2, S3, S4, E4); a second Windows user is unaffected (S6).
- [ ] AC7: README and the user guide, English first and then German, describe the entry, that it is off by default
  and per-user, to extract the ZIP to a folder that stays, what moving the exe or a new version in another folder
  does, and how to turn it off before removing the Companion (E2, E6, E9).
- [ ] AC8: [`companion.md`](../../docs/areas/companion.md) describes the entry, its place in the menu, its rules and
  its log lines, and no longer lists autostart under *Not here* (S1 to S6, E1 to E8).

## Out of scope

- A settings window or any other autostart setting (a delay, starting with the game, starting minimized or not).
- Starting with `--record` or any other switch at sign-in (S5 always starts without switches).
- Moving an autostart entry to a new path on its own when the exe moves (E2: the player clicks again).
- An installer or uninstaller; administrator-wide (all users) autostart.
- E8 needs no criterion beyond the existing *Single instance* behavior.

## Open questions

- Is autostart already decided, and how? Answered: record 0017 (Accepted) — per-user, off by default, no
  administrator rights, issue #15 builds it.
- Is the entry a toggle with a check mark or two entries like `Start recording` / `Stop recording`? Answered: the
  issue asks for "a toggle"; an on/off setting reads as one entry with a check mark (`Start with Windows`, the
  issue's wording), unlike the recording, which is an action.
- Is this an install or deploy feature that needs the Product Manager's answer on the target host? Answered: no;
  the target is the player's own Windows 10 22H2 or 11 x64 as the release ZIP already states (*Platform*), and
  nothing is installed.
- Which path does autostart start when the player moves the exe or keeps two copies? Answered: the exe whose
  menu turned it on last; the menu shows the truth for the running exe (E2, E3), which follows from 0017's "per-user
  setting" without an installer that owns one location (0027).

## Area documents

- [`docs/areas/companion.md`](../../docs/areas/companion.md): a section *Start with Windows* with S1 to S6 and E1 to
  E8 as rules; the menu enumeration in *Tray icon and menu* gains the entry and its separators; *Log* gains the two
  lines and the warning; *Limits and errors* names what is verified by hand (AC6); *Not here* drops "autostart";
  *Related decisions* links 0017.
