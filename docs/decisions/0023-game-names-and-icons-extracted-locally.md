# 0023: Names and icons are extracted from the local installation, cached, re-extracted after updates and never distributed; German and English

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issues #22–#26 (game data), Issue #42 (catalog upload)
- **Supersedes:** —

## Context

The pipe identifies goods, buildings and islands by numeric IDs. Names and icons live in the game's own files
(`config.rda`, `ui.rda`), which belong to the publisher. A game update can add or change them. The maintainer and the
users play in German or English.

## Options considered

1. **Ship a catalog of names and icons with Censio** — works without the game files; redistributes copyrighted game
   assets and goes stale with every game update.
2. **Show IDs only** — nothing to extract; unusable.
3. **Extract the catalog from the user's own installation, cache it, extract again when the game files change, and
   never distribute it** — always matches the installed version and stays on the user's machines; the first start
   takes the extraction time.

## Decision

Option 3. The Companion extracts names (German and English) and icons from the local game installation, caches them
and extracts again when the game files changed (#25); the catalog goes only to the user's own server (#42) and is
never part of a repository, release, image or test fixture.

## Consequences

- If extraction fails, the UI falls back to IDs (#25).
- Test fixtures never contain extracted game assets ([0029](0029-anonymised-test-fixtures-only.md)).
- Other game languages need only another text table, not a different design.
- How the game files are read is [0024](0024-rda-reading-with-an-existing-nuget-library.md).
