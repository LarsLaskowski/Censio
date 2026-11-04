# 0017: The Companion is a WPF tray application with optional autostart and an automatic connection

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** companion
- **Source:** Issue #8; Issue #13 (tray app), Issue #15 (autostart), Issue #40 (connection)
- **Supersedes:** —

## Context

The Companion has to run whenever the game runs and should not get in the player's way: no window to keep open, no
step to remember before starting the game. It needs a little UI of its own — connection status, recording, settings,
a diagnostics window. How its projects are split so that it builds and is tested on Linux is
[0004](0004-companion-shell-and-linux-build.md).

## Options considered

1. **A Windows service** — always running; needs an installer and administrator rights, and has no UI of its own.
2. **A console program** — no UI framework; a window the player has to keep open.
3. **A WPF application that lives in the notification area, starts with Windows only when the user enables it, and
   connects to its server on its own** — no window by default, per-user, no administrator rights; WPF is part of the
   .NET Desktop runtime for the settings and diagnostics windows.
4. **WinUI 3 or MAUI** — newer UI stacks; more packaging and runtime requirements for a tray tool.

How the per-user autostart is stored (#15):

1. **A value `Censio Companion` under the user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key** —
   one string, no COM, written, read and deleted with the plain registry API; listed in Windows' startup apps list.
2. **A shortcut in the user's Startup folder** — also listed there; creating a `.lnk` needs the shell's COM
   `IShellLink`, and the file can be copied or edited by hand, so the menu would have to parse a shortcut to tell whether it is the Companion's.
3. **A scheduled task at log-on** — not shown in the startup apps list, so the player could not see or switch it
   off where Windows lists startup programs; the Task Scheduler API is a larger surface.

Whether the menu also follows the switch-off of the startup apps list (`Explorer\StartupApproved\Run`):

1. **Read it, and clear it when the player turns autostart on** — the check mark agrees with Windows; the value's
   binary format is not documented by Microsoft and would have to be measured on each Windows version.
2. **Leave it to Windows** — the Companion touches only its Run value; after a switch-off in Windows the check mark
   still shows on, and the player switches it back on in Windows.

## Decision

Option 3. The Companion is a WPF tray application. Starting with Windows is a per-user setting, off by default (#15);
once configured, the Companion connects to its server without a user action and reconnects on its own (#40).
Autostart is stored as the per-user Run value (option 1 of the second list): its command is the exe's full path in
double quotes without arguments, the menu reads the value each time it opens, and turning it off deletes the value.
The switch-off of the startup apps list is left to Windows (option 2 of the third list, the Product Manager's
decision): the plain Run-value API, tested by hand on Windows 11 before the merge.

## Consequences

- No installer and no administrator rights are needed ([0027](0027-companion-zip-self-contained-single-file-unsigned.md)).
- A player who does not enable autostart starts the Companion by hand before or after the game.
- Autostart follows the exe the player last turned it on from: a moved exe or a second copy needs one click again,
  and a stale value for a deleted exe starts nothing.
- After the player switches the Companion off in Windows' startup apps list, the menu still shows it checked; the
  player switches it on there again. No undocumented format is read or written.
- A Windows policy that stops Run processing is invisible to the menu.
- A path holding `%` or longer than 200 characters is refused rather than measured: whether Explorer expands `%` or
  cuts a long command is never relied on. Only the exact quoted command counts as the Companion's.
- The behavior is written in [`areas/companion.md`](../areas/companion.md) as each issue builds it.
