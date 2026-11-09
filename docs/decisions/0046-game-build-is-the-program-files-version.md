# 0046: The game build is identified by the program file's version, not the main-menu version

- **Status:** Accepted
- **Date:** 2026-11-09
- **Area:** game-installation
- **Source:** Issue #22; Issue #25 (cache keys)
- **Supersedes:** —

## Context

The issue asks for the game version, for the cache keys of #25. The Product Manager's sample
(`tests/Censio.Companion.Core.Tests/Fixtures/GameInstallation/game-installation.md`) shows the version `3.0.1733841` only in
the game's main menu; Ubisoft Connect shows none, and `bin\win64\anno117.exe` has the file version
`26.29905.4.51797`, a different number. No game file known to the repository holds the menu version
([game-files](../areas/game-files.md)).

## Options considered

1. **The main-menu version** — what the player knows; its location in the game files is unknown and needs a search of
   the archives' content first.
2. **The program file's version, shown as the build** — read with Windows' own version reader; a different number
   than the player knows, so it is labelled `build`.
3. **No version in this feature** — leaves #25 without a key; #25 plans file hashes too, but the issue asks for it here.

## Decision

Option 2: the four numbers of the program file's version are the build; without them the build is unknown and the
installation is still used.

## Consequences

- Not verified: that the file version changes with every game update. The Product Manager compares the menu line
  before and after the next game update (after merge); #25 does not rely on the build alone.
- If #25 needs the menu version, finding it in the archives is its own work.
- The text and its rules are in [game-installation](../areas/game-installation.md).
