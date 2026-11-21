# 0099: Local mode is switched on in the settings file and is off by default; its port defaults to 5000

- **Status:** Proposed
- **Date:** 2026-11-21
- **Area:** local-mode
- **Source:** Issue #39; Issue #43 (settings window)
- **Supersedes:** —

## Context

Until the settings window (#43) exists, a player needs some way to choose local mode. The autostart entry starts the
Companion without arguments ([companion](../areas/companion.md), *Start with Windows*). The settings file
`%LOCALAPPDATA%\Censio\settings.json` already exists. It is never written by the Companion and ignores unknown keys so
that later settings can share it (record [0045](0045-companion-settings-in-a-json-file-under-local-app-data.md)).
A listener is a security area ([`.squad/project.md`](../../.squad/project.md), *Network exposure*).

## Options considered

1. **Keys `mode` and `localPort` in the settings file, local mode off without them** (chosen) — kept across
   autostart and #43 can write them. Existing players see no change and no new listener. Con: a tester edits a file.
2. **A command-line switch `--local`** — easy to try, but autostart would lose it.
3. **Local mode on by default** — no edit at all, but every existing Companion would open a listener after an update
   without the player's choice, and two Windows users would fight over one port.
4. **A port without default** — the player would have to know a free port. Port 5000 is the server's own default
   outside the image ([server](../areas/server.md), *Listen address*), so the documents name one number.

## Decision

Local mode runs only when the settings file says `"mode": "local"`. It listens on `localPort`, by default 5000.
A wrong value of these keys stops only the local server, never the game search. The rules are in
[local-mode](../areas/local-mode.md).

## Consequences

- #43 writes the same keys and may add `forward` for #40; until then `forward` is an invalid value.
- A change counts after a restart, like `gameFolder`.
