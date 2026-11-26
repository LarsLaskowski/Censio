# 0110: The settings window writes the settings file, keeping unknown keys and dropping comments

- **Status:** Accepted
- **Date:** 2026-11-25
- **Area:** companion
- **Source:** Issue #43
- **Supersedes:** 0045 (the file is no longer only hand-edited: the Companion writes it from its settings window)

## Context

Record [0045](0045-companion-settings-in-a-json-file-under-local-app-data.md) made `%LOCALAPPDATA%\Censio\settings.json`
a hand-edited file the Companion never writes, and left #43 to write it. #43's window edits every key of the file, and
players who edited it by hand may hold comments, other-case spellings and keys of later versions. The file holds the
ingest token (`serverToken`, [live-sending](../areas/live-sending.md), *Privacy*).

## Options considered

1. **Rewrite the file from the window's values, keep unknown keys, drop comments** — one file, one reader; the
   window's values are written once in their documented spelling; comments are lost.
2. **Edit the file text in place, keeping comments** — needs a JSON-with-comments editor of its own; a duplicated or
   other-case key would have to be rewritten in place, which is the parser work 0045 kept out.
3. **A second file the window owns, overriding `settings.json`** — the hand-edited file stays untouched; two files
   decide one value, and the player cannot tell which wins.

## Decision

Option 1. The window writes the whole file new, replaces the old one by a rename, refuses to write when the file
changed since the window opened, and replaces an unparseable file only after a warning; the rules are in
[companion-settings](../areas/companion-settings.md).

## Consequences

- Comments and key order of a hand-edited file are lost at the first save; the user guide says so.
- The token is now written by the Companion, as plain text in the player's private folder like before; the file's
  access rules are not checked, and the replaced file takes the access rules its folder passes on, so rules set on
  `settings.json` alone are lost at a save. A crash between writing and replacing leaves `settings.json.tmp`, which
  holds the token, in the same folder until the next save overwrites it.
- Reading stays as 0045 states; a hand-edited file keeps working. A file holding a text that is not valid UTF-8 is
  shown as unreadable and replaced at a save, since its keys cannot be written back unchanged.
