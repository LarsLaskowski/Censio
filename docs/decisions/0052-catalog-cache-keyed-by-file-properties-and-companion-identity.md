# 0052: The catalog cache is keyed by the archives' file properties, the build and the Companion's module identity, and checked at start and at each new pipe connection

- **Status:** Accepted
- **Date:** 2026-11-10
- **Area:** game-catalog
- **Source:** Issue #25
- **Supersedes:** —

## Context

Record [0023](0023-game-names-and-icons-extracted-locally.md) decided that names and icons are cached and extracted
again when the game files change. The issue asks for a key of "game version / file hashes" and a check at start and
when the game starts. The game's archives below `maindata` are large and may lie on a network share
([`game-installation.md`](../areas/game-installation.md), *Folders from outside*); the build is the program file's
version, and whether it changes with every update is unverified (record
[0046](0046-game-build-is-the-program-files-version.md)). The product version is `0.0.0` on every build from `main`,
which the external testers run (record [0013](0013-readme-quick-start-user-guide-reference.md)). The Companion's only
sign of a running game is the statistics pipe.

## Options considered

1. **Content hashes of the archives** — detects every change; reads all of `maindata` at each check, over SMB too,
   which costs more than the extraction it avoids.
2. **The build alone** — cheap; misses an update that keeps the program file's version.
3. **The build, plus each archive's relative path, size and modification time** — reads only directory metadata;
   misses a file rewritten with the same size and time.
4. **Companion side of the key: the product version** — `0.0.0` for every build from source, so a changed extraction
   rule would reuse an old cache.
5. **Companion side: a hash of the exe** — reads the whole single-file exe at every check, and is not defined for a
   build run from `bin`.
6. **Companion side: the module version ids of `Censio.GameData` and `Censio.Companion.Core`** — deterministic builds give one id per content, so a code change changes it even when the product version stays the same, and a change of the product version changes it too.
7. **When to check: watching `maindata` for file changes** — file notifications over SMB are unreliable and fire
   throughout an update; **the game's process** — needs process enumeration the Companion does not do today; **each
   new pipe connection** — already observed, and an unchanged check costs only the listing.

## Decision

Options 3, 6 and the pipe connection of 7. A check compares the current key with the key of the last result held in
memory, successful or failed, and at start with the key in the cache; only an extraction whose catalog and icons were
both built is cached. The rules are in [`game-catalog.md`](../areas/game-catalog.md), *Cache*.

## Consequences

- A file rewritten with the same size and modification time goes undetected; the user guide says to delete the cache
  folder.
- A failure is not retried until the next start or a changed key, so a permanent failure does not re-read the
  archives at every reconnect.
- A game started without `/pipe`, or a replay, is checked only at the Companion's start.
- That the module version id is stable for one source and changes with the code is to be measured (plan of #25, fact
  9); if a build setting turned determinism off, every start would extract again. A change of the product version
  changes the id too, which costs one extraction.
- An unreadable program file counts as one without a version (`unknown`); a missing one, a missing `maindata` or a
  folder that cannot be listed makes the check fail, and the current catalog is kept.
