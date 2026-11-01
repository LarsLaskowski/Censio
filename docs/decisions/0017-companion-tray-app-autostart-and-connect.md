# 0017: The Companion is a WPF tray application with optional autostart and an automatic connection

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
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

## Decision

Option 3. The Companion is a WPF tray application. Starting with Windows is a per-user setting, off by default (#15);
once configured, the Companion connects to its server without a user action and reconnects on its own (#40).

## Consequences

- No installer and no administrator rights are needed ([0027](0027-companion-zip-self-contained-single-file-unsigned.md)).
- A player who does not enable autostart starts the Companion by hand before or after the game.
- The behavior is written in [`areas/companion.md`](../areas/companion.md) as each issue builds it.
