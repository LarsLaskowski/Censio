# 0045: The Companion's settings are a hand-edited JSON file under the local app data folder

- **Status:** Proposed
- **Date:** 2026-11-09
- **Area:** companion
- **Source:** Issue #22; Issue #43 (settings window)
- **Supersedes:** —

## Context

The game folder needs a manual override before the settings window (#43) exists; the issue asks for a config file.
The Companion already keeps fixed folders under `%LOCALAPPDATA%\Censio` and has a command line, but its autostart
entry starts it without arguments ([companion](../areas/companion.md), *Start with Windows*).

## Options considered

1. **A command-line switch** — no file; lost at every autostart, which writes the command without arguments.
2. **A JSON file `%LOCALAPPDATA%\Censio\settings.json`, read once at start, never written by the Companion** — next
   to the other Companion folders, extendable by #43; a player edits it by hand, so the reader tolerates what an
   editor produces (byte order mark, comments, trailing commas, the key's case).
3. **A registry value** — no file; harder to edit and to show in the user guide.

## Decision

Option 2. Unknown keys are ignored so #43 can add its settings to the same file; a file that cannot be read or
parsed is reported as invalid and is never repaired or rewritten.

## Consequences

- #43 writes this file; it must keep `gameFolder` and the reading rules or change them in the area document.
- The file is untrusted input with a size bound; the rules are in [companion](../areas/companion.md),
  *Settings file*.
