# Spec: Optional Windows autostart for the Companion

Source: Issue #15
Status: Approved by Product Manager

## Problem

A player has to start the Companion by hand before or after the game today: the tray menu has no autostart entry
and the Companion writes nothing that Windows starts at sign-in (checked: `TrayMenuEntryKind` and
`src/Censio.Companion/` have no such entry or code). A player who forgets it loses the data of that session. Record
[0017](../../docs/decisions/0017-companion-tray-app-autostart-and-connect.md) already decided that starting with
Windows is a per-user setting, off by default, without administrator rights; the Companion ships as an unsigned exe
in a ZIP without an installer ([0027](../../docs/decisions/0027-companion-zip-self-contained-single-file-unsigned.md)),
so the exe lives wherever the player extracted it.

Terms used below:

- The *startup entry* is the one value named `Censio Companion` in the signed-in user's own Run list of programs
  Windows starts at sign-in (record 0017); Settings > Apps > Startup and Task Manager > Startup apps list it, maybe
  under the name Windows takes from the exe.
- Its *command* is the exe's full path in double quotes and nothing else: `"<full path>"`.
- It *matches this exe* when its command equals `"<full path of the running exe>"` exactly, compared
  case-insensitively.
- The switch-off of Settings and Task Manager (the player turns an entry off there) is Windows' own record, which
  the Companion neither reads nor changes (Product Manager's decision, *Challenge*).

## Scenarios

- S1: Given a Companion that was never set to start with Windows, when the player opens the tray menu, then it shows
  the entry `Start with Windows` without a check mark, between `Open log folder` and `Version <version>`, set off by
  a separator above and below it.
- S2: Given S1, when the player clicks `Start with Windows`, then the next time the menu opens the entry carries a
  check mark, and Windows' startup apps list shows the Companion. The running Companion goes on
  unchanged.
- S3: Given S2, when the player signs out and in again or restarts the PC, then the Companion starts with the
  player's sign-in exactly as a start by hand without switches: one icon, no window, `Waiting for game` until the
  game runs, recording off, and the menu shows `Start with Windows` checked.
- S4: Given autostart is on, when the player clicks the checked `Start with Windows`, then the check mark is gone
  the next time the menu opens, the Companion is no longer in the startup apps list, the running Companion goes
  on, and after the next sign-in or restart no Companion starts.
- S5: Given a Companion started with `--record`, `--replay <file>` or an invalid command line, when the player
  toggles `Start with Windows`, then it works as in S2 and S4, and the start at sign-in is a start without switches.
- S6: Given another Windows user on the same PC, when one user turns autostart on or off, then the other user's
  setting and sign-in are not affected.

## Errors and edge cases

- E1: The startup entry cannot be read, written or removed (for example a policy blocks it). Reading fails: the menu
  still opens with all other entries, `Start with Windows` shows unchecked and stays clickable, and a warning is
  logged once, not again until a read has succeeded. Writing or removing fails: the check mark shows the state read when the menu opens next; no message or
  dialog appears, and a warning is logged.
- E2: The player moved or renamed the exe, or extracted a new version to another folder, after turning autostart
  on. The startup entry still starts the old path: if the exe is gone, nothing starts at sign-in and Windows shows
  nothing; if an old copy remains, that copy starts. A Companion started from the new path shows `Start with
  Windows` unchecked, since the entry does not match this exe; a click replaces the entry with this exe's command.
- E3: Two copies of the exe in different folders. There is one startup entry per Windows user: turning it on in one
  copy replaces the other's, whose menu then shows the entry unchecked. Only the copy that holds it starts at
  sign-in.
- E4: The player turned the Companion off in Windows' startup apps list. Windows does not start it at sign-in, but
  the menu still shows `Start with Windows` checked, since the switch-off is not read; a click deletes the startup
  entry, and a later click writes it again while Windows keeps its switch-off. The player turns it on again in the
  startup apps list. README, user guide and the area document say so.
- E5: The exe's path holds spaces, a comma, `&` or non-ASCII letters. The command is that path in double quotes, and
  autostart starts the exe at that path. A path holding `%` or longer than 200 characters cannot be turned on: a
  click leaves `Start with Windows` unchecked, writes nothing and logs a warning (Windows might expand `%` or cut a
  long command, so the Companion does not risk starting another file).
- E6: The player started the exe from inside the ZIP without extracting it, so Windows ran it from a temporary
  folder. The entry works as everywhere else and registers that path, which is gone after Windows cleans the folder:
  nothing starts at sign-in (as E2). README and user guide say to extract the ZIP to a folder that stays.
- E7: Repeated clicks. Each click toggles from the state read when the menu opened; the check mark always shows the
  state read at the next opening (E1). A second Companion cannot run in the session (*Single instance*), so no two
  clicks race.
- E8: The Companion is started at sign-in while one already runs in the session. The new one ends at once as
  *Single instance* says.
- E9: The exe is unsigned. Whether SmartScreen asks again at the autostart after the player chose "Run anyway"
  once is checked on Windows (AC6); README and user guide describe what the player sees.
- E10: An entry named `Censio Companion` exists whose command holds arguments or another program's path. The menu
  shows `Start with Windows` unchecked; a click replaces it with this exe's command.
- E11: A Windows policy stops Windows from starting the user's startup programs while the entry can still be
  written. The menu shows the state of the entry and cannot tell that Windows will not start it.

## Non-functional requirements

- Per-user only: turning autostart on or off needs no administrator rights and no UAC prompt, writes only settings
  of the signed-in Windows user, and changes nothing for other users. The Companion writes no file of its own for
  it; the state is the startup entry Windows holds (the check mark can disagree with Windows after E4 or E11).
- Off by default: a Companion that was never toggled, on a new or an updated version, does not start with Windows.
- What starts at sign-in is only the Companion exe itself, its full path in double quotes, without arguments. A
  command line Windows runs at every sign-in is a persistence point: the plan lists it under *Security areas
  touched*.
- The menu does not wait noticeably: toggling and opening the menu stay as quick as the other entries.
- Log: `Autostart enabled` and `Autostart disabled` (information) at the click, and a warning when a read, write or
  removal fails or a path is refused (E1, E5), with the exception text as the log already allows (record 0037). These lines do not hold the
  exe path.
- Compatibility: Windows 10 22H2 and Windows 11, x64, as the release ZIP ([`companion.md`](../../docs/areas/companion.md),
  *Platform*). No new configuration besides the menu entry.
- Removing the Companion: turning autostart off deletes the startup entry; a switch-off Windows recorded for it
  (E4) is Windows' own and stays; a stale entry for a deleted exe starts nothing (E2), and README and user guide say
  how to remove it.

## Acceptance criteria

- [ ] AC1: The tray menu holds `Start with Windows` between `Open log folder` and `Version <version>`, with a
  separator above and below, enabled in every mode (live, `--record`, replay, invalid command line). It is checked
  when the menu opens exactly when the startup entry exists and matches this exe; otherwise it is unchecked; a
  switch-off in Windows' startup apps list is not read (S1, S5, E2, E3, E4, E10, E11).
- [ ] AC2: Clicking the unchecked entry writes the startup entry with this exe's command (`"<full path>"`, no
  arguments), replacing any other command under that name — unless the path holds `%` or is longer than 200
  characters, when it writes nothing and logs a warning; clicking the checked entry deletes the startup entry; the
  running Companion goes on in both cases (S2, S4, S5, E2, E3, E5, E7, E10).
- [ ] AC3: Autostart is off until the player turns it on; nothing the Companion does on start, `Exit` or in any
  mode writes or removes the startup entry (S1, S5).
- [ ] AC4: A read that fails shows the entry unchecked and clickable with every other menu entry present, and logs
  a warning once until a read succeeds again; a write or removal that fails shows no message and logs a warning, and the next opening shows the
  state read then; a successful click logs `Autostart enabled` or `Autostart disabled`, without the exe path (E1).
- [ ] AC5: Turning autostart on or off writes only the signed-in user's settings and needs no administrator rights
  (S6).
- [ ] AC6: Verified by the Product Manager by hand on Windows 11 before the merge, with an exe built with the release
  flags (README, *Build the Companion from source*), under a standard (non-administrator) account: no UAC prompt
  appears; on, then restart, the Companion starts as in S3 from the exe's real path (and whether SmartScreen asks,
  E9); off, then restart, no Companion starts and the startup apps list no longer shows it; the name the startup
  apps list shows; a folder with spaces, `&` and `,` starts (E5); a folder holding `%` leaves the entry unchecked
  (E5); a second Windows user is unaffected (S2, S3, S4, S6, E5).
- [ ] AC7: README and the user guide, English first and then German, describe the entry, that it is off by default
  and per-user, to extract the ZIP to a folder of the player's own profile that stays (not a shared or root-level
  folder other users can write), that a folder path holding `%` or longer than 200 characters cannot be used, that
  a switch-off in Windows' startup apps list is not shown in the menu and is undone only there (E4), what moving the
  exe or a new version in another folder does, to turn it off before removing the Companion, and how to remove a stale entry: start any copy, click `Start
  with Windows` (it replaces the stale entry), then click it again to turn it off (E2, E4, E6, E9).
- [ ] AC8: [`companion.md`](../../docs/areas/companion.md) describes the entry, its place in the menu, its rules,
  its log lines, E4 and E11 under *Limits and errors*, and no longer lists autostart under *Not here* (S1 to S6, E1
  to E11).

## Out of scope

- A settings window or any other autostart setting (a delay, starting with the game, starting minimized or not).
- Starting with `--record` or any other switch at sign-in (S5 always starts without switches).
- Moving the startup entry to a new path on its own when the exe moves (E2: the player clicks again).
- Detecting a start from a temporary folder (E6: documented only).
- Reading or clearing the switch-off of Windows' startup apps list (E4: documented only).
- A scheduled task, an installer or uninstaller, and administrator-wide (all users) autostart.
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
- Where is the startup entry stored? Answered: as a per-user Run value, decided in the plan and recorded in 0017;
  it must appear in the startup apps list (S2), which rules out a scheduled task.

## Area documents

- [`docs/areas/companion.md`](../../docs/areas/companion.md): a section *Start with Windows* with S1 to S6 and E1 to
  E10 as rules; the menu enumeration in *Tray icon and menu* gains the entry and its separators; *Log* gains the two
  lines and the warnings; *Limits and errors* names E4, E11 and what the Product Manager verified by hand
  (AC6); *Not here* drops "autostart"; *Related decisions* links 0017 and the plan's record.

## Challenge

- M1 (AC1 and E4 untestable, "exactly" over-promises): accepted. *Problem* defines the startup entry, its command,
  *matches this exe* and *turned off in Windows*; AC1 checks the three conditions observably and counts an
  unrecognised off record as off (E4); AC2 says re-enabling clears the off switch; a policy that stops startup
  programs is E11, documented, not promised; the undocumented off record is named in the NFR, AC6 and AC8.
- m1 (read failure while the menu opens): accepted; E1 and AC4 cover it — unchecked, clickable, other entries
  present, warning logged.
- m2 (quoting, security area): accepted; the command is `"<full path>"` without arguments (terms, E5, AC2); the NFR
  requires the plan to list the sign-in command line under *Security areas touched*. How the path is obtained
  (not `Assembly.Location`) is the plan's to name.
- m3 (path comparison, foreign entry): accepted; *matches this exe* compares case-insensitively without the quotes;
  E10 covers an entry with arguments or another program.
- m4 (turning off leaves the off record): accepted; AC2 and the NFR remove the off switch with the entry; AC6
  checks the startup apps list after turning off.
- m5 (removing a stale entry): accepted; AC7 names the steps.
- m6 (exe run from inside the ZIP): decided for documentation only. A rule on temporary folders would grey out a
  legitimate location by guess and adds a case to test, while the failure is harmless and visible (the next manual
  start shows unchecked, E2); listed under *Out of scope*.
- m7 (no-admin check): accepted; AC6 runs under a standard account and checks no UAC prompt and no write outside
  the user's settings and profile.
- m8 (Run value or Startup-folder shortcut): accepted as a planning decision; the plan records it in a decision
  record referring to 0017 (*Open questions*); a scheduled task is out of scope.
- Product Manager's decision after the plan review (escalation of the off-switch measurement): the Companion uses the
  Run value only — written, compared and deleted — and does not read or clear the startup apps list's switch-off.
  E4 now describes the resulting limitation, AC1, AC2, AC6 and the NFR drop the off switch, and AC6 is the Product
  Manager's own test on Windows 11 before the merge. This supersedes the answers to M1 (the off-switch part) and m4
  above.
