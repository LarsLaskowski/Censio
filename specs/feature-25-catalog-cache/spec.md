# Spec: Catalog cache and re-extraction after game updates

Source: Issue #25
Status: Approved by Lead

## Problem

The Companion extracts names and icons at every start ([`game-catalog.md`](../../docs/areas/game-catalog.md)), over
SMB too when the game lies on a share, although the files change only with a game update; an update while the
Companion runs goes unnoticed until a restart, and the tray says nothing while it extracts or when the icons fail.
Record [0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md) decided to cache and re-extract.

## Scenarios

Texts in the tray are the greyed-out `Catalog:` menu line; log lines are in the Companion's log. The *game state* is
the installation folder, the build (`unknown` equals `unknown`), and the name, size and modification time of every
archive below `maindata`; the *Companion identity* is the module version ids of the assemblies that extract and write
the cache, which change with their code, not with the version number. A *check* compares both with the state of the
last check's result — successful or failed — held in memory, and at start with the cache.

- S1: Given no cache, when the Companion starts and finds the game, then the line reads `Catalog: reading` while the
  game search runs, then `Catalog: checking`, `Catalog: extracting names`, `Catalog: extracting icons, <n> entries`
  (names already shown in the diagnostics window) and `Catalog: <n> entries`; the cache is written, and the log holds
  `Catalog cache not used: missing`, the existing catalog and icon lines and, after them, `Catalog extracted in <t> s
  (names <a> s, icons <b> s)`.
- S2: Given a cache of the same game state and Companion identity, when the Companion starts, then the line goes from
  `Catalog: checking` to `Catalog: <n> entries`, names and icons show as after an extraction, no archive content is
  read, and the log holds only `Catalog loaded from cache in <t> s: <n> entries, <g> goods and <r> regions with an
  icon` (the other catalog and icon lines are written at an extraction only).
- S3: Given a cache, when the game was updated or the settings file names another installation, then the start logs
  `Catalog cache not used: game changed`, extracts as in S1 and replaces the cache (one cache, N1).
- S4: Given the Companion runs, when the game is updated and the player starts it with `/pipe`, then at the new pipe
  connection the check finds the change, the `Game:` line shows the build read now, and S1's extraction runs while the
  previous names stay shown; the new names replace them when ready, the previous icons go until the new ones are
  built, and the cache is replaced.
- S5: Given the Companion runs, when the player starts the unchanged game, then the check changes, extracts and logs
  nothing — also after a failed last result (E1 to E3).
- S6: Given a cache of another Companion identity (an updated Companion), when it starts, then it logs `Catalog cache
  not used: Companion changed` and extracts as in S1; there is no migration.

## Errors and edge cases

- E1: The catalog extraction fails: `Catalog: failed`, the diagnostics window shows GUIDs (previous names and icons
  removed), nothing is cached and no cache of an old state is used; it is tried again only at the next start or when a
  check finds the game state changed.
- E2: The names are built but the icons fail: `Catalog: <n> entries, icons failed`, the existing `Catalog icons failed:
  …` warning, nothing cached; retried as E1.
- E3: The cache cannot be written (no folder, disk full, access denied, local app data not a full path): names and
  icons are used, the line reads `Catalog: <n> entries, not cached`, the Warning `Catalog cache not written`
  is logged with the exception where there is one; retried as E1.
- E4: The cache is unreadable, truncated, above 320 MiB, of another format, or fails a validation of N3: no part of it
  is used, the Warning `Catalog cache not used: invalid` is logged, and S1's extraction replaces it.
- E5: At start the check cannot read what it compares (the build file, the listing of `maindata`, an archive's
  properties): the cache is not used (`Catalog cache not used: game changed`) and the extraction runs and reports as
  today. At a later check the current catalog, icons and line stay, nothing is extracted, and the Warning `Catalog
  check failed; the current catalog is kept` is logged.
- E6: No game found or settings file invalid: `Catalog: no game`; the cache is neither used nor deleted.
- E7: `Exit` during an extraction or a cache write still ends the Companion within 2 seconds. Two Companions of one
  account in two Windows sessions may write at once. The next load finds the previous complete cache, one writer's
  complete cache, or none (E4) — never a half-written or mixed one.
- E8: A new pipe connection during the game search, a check or an extraction (as when the game already runs) starts
  no second check.
- E9: Files changed during an extraction: the result keeps the state taken before it, so the next check extracts.
- E10: An archive rewritten with the same name, size and modification time goes undetected; the user guide says to
  delete the cache folder and restart.
- E11: A replay, an invalid command line or a game started without `/pipe` has no pipe connection: only the check at
  start runs.
- E13: Each published catalog has its own `Catalog cannot name …` lines and 1,000-line limit.

## Non-functional requirements

- N1: The cache lies in the fixed folder `%LOCALAPPDATA%\Censio\Cache`, one cache per Windows account for the
  installation last extracted; nothing is written into the installation or elsewhere. No new setting.
- N2: Privacy (record 0023): names and icons are saved in the cache only, never sent, logged or released; the cache is
  private like a recording and stays when the Companion is removed.
- N3: The cache is untrusted input when read (a security area of `.squad/project.md`). A file above 320 MiB (the
  256 MiB icon bound of `game-catalog.md`, *Limits*, plus 64 MiB for the catalog) is refused before it is read. Before
  anything is used, the whole file is checked: its format, its Companion identity and game state, a checksum over its
  content, the bounds of the catalog, and per icon the PNG signature, a header of 1 to 2,048 pixels per side, 8 bits
  per channel with alpha, and exactly the chunks the Companion's own PNG writer emits. One failure refuses the whole
  file (record 0033).
- N4: A check reads the build and the listing of `maindata` with each archive's size and modification time, never
  archive content.
- N5: Nothing the player uses (menu, connection, ticks, diagnostics window, `Exit`) waits for the catalog work.
- N6: The durations are logged with one decimal place and a decimal point; the user guide and `game-catalog.md`
  state the extraction and cache-load durations measured on the Product Manager's installation.
- N7: A cache load reads the file in one forward pass and holds no whole copy of it beside the catalog and icons it
  publishes.

## Acceptance criteria

- AC1: At start, a cache of the same game state and Companion identity publishes names and icons without reading any
  archive's content, in one forward pass over the file, and logs only the cache-load line (S2, N4, N7).
- AC2: Each difference of the game state (build, an archive added, removed or with another size or modification time,
  another folder), an unreadable property at start, or another Companion identity with the same version number makes
  the check extract, log its reason and replace the cache; a change during the extraction is extracted at the next
  check (S1, S3, S6, E5, E9).
- AC3: In a live mode each new pipe connection runs a background check, merged into one that runs; it behaves as S4,
  S5 and E5 (a later check) say, after a successful or a failed last result (S4, S5, E1, E5, E8, E11, N5).
- AC4: The `Catalog:` line shows each text of S1, S2, E1, E2, E3 and E6 exactly as written there.
- AC5: Only a complete result (catalog and icons built) is cached; E1, E2 and E3 behave as written (E1, E2, E3, E6).
- AC6: A cache as in E4, or left by an interrupted or concurrent write, is never used in part, is logged and is
  extracted again (E4, E7, N3).
- AC7: Each extraction that builds a catalog and each cache load logs its duration line, timed on an injected clock; a re-extraction resets
  the `Catalog cannot name` reporting (S1, S2, E13, N6).
- AC8: `README.md` (the overview bullets on the Companion and on names and icons, the quick-start menu line) and
  `docs/USER_GUIDE.md` (*Game catalog* with its icon paragraph, the storage paragraph "built in memory at every start
  and not saved", the log contents, *Reporting problems*), English and German, describe the cache and its folder, when
  it is extracted again, the new texts, that it is private, stays after removal and can be deleted (E10), and the
  measured durations (N1, N2, N6, E10, E11).

## Out of scope

- A menu entry to rebuild or delete the cache, a percentage, and a cache per installation (N1).
- Detecting a game start without the pipe (E11), and a new game search at a check (only the build is read again).
- The catalog format sent to the server (#26) and the upload (#42).

## Open questions

- Hashes or file properties as the key? Answered: properties (N4) — a departure from the issue's "file hashes", shown
  to the Product Manager at spec approval. Hashing reads all of `maindata` at every check and costs more than the
  extraction; the build alone is not enough (`game-installation.md`, *Build*: unverified that it changes with every
  update). E10 is the residual case.
- Old cache after a failed extraction? Answered: no — the issue asks for GUIDs; record 0033 fails closed (E1).
- "When the game starts"? Answered: a new pipe connection, the Companion's only sign of a running game.
- Who measures the durations (N6)? Answered: the Product Manager, on a local build of the branch head before the PR
  approval.

## Area documents

- `game-catalog.md`: a *Cache* section (the terms above, checks, results, bound, validation, failures, writes);
  *Memory only*, *Privacy*, *Time* (measured durations) and *Network* revised.
- `companion.md`: the `Catalog:` texts, the new log lines and the per-catalog `cannot name` reporting, the cache folder,
  `Exit` during an extraction.
- `game-installation.md`: the build is read again at each later check and shown in the `Game:` line.

## Challenge

- M1 — accepted: checks compare with the last result's state, failed or not; failures retry only at start or on a
  change (S5, E1 to E3, AC3).
- 1 — accepted: a later check that cannot read keeps the current state with a Warning (E5, AC3).
- 2 — accepted: previous names stay until new ones are published; `cannot name` reporting restarts per catalog (S4,
  E13, AC7).
- 3 — accepted: module version ids (AC2).
- 4 — accepted: `unknown` equals `unknown`.
- 5 — accepted: `Catalog: reading` stays (S1); a connection then merges (E8).
- 6 — accepted: 320 MiB, per-icon checks (N3); N7 checkable (AC1).
- 7 — accepted: AC8 names the passages.
- 8 — accepted: other catalog lines at an extraction only (S2); no mixed cache (E7).
