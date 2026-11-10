# Spec: Catalog cache and re-extraction after game updates

Source: Issue #25
Status: Draft

## Problem

The Companion extracts the catalog (names) and the icons from the player's installation at every start
([`game-catalog.md`](../../docs/areas/game-catalog.md)), although the game files change only with a game update. Every
start pays the extraction time; with the game folder on a network share every start reads the archives over SMB. A
game update while the Companion runs (it may run all day through `Start with Windows`) is not noticed until the next
start. The tray shows `Catalog: reading` for the whole extraction and says nothing when the icons fail. Record
[0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md) decided to cache the extraction and
extract again after updates; this feature delivers it.

## Scenarios

Texts in the tray are the greyed-out `Catalog:` menu line; log lines are in the Companion's log.

- S1: Given no cache, when the Companion starts and finds the game, then the line reads `Catalog: checking`, then
  `Catalog: extracting names`, then `Catalog: extracting icons, <n> entries` (names already shown in the diagnostics
  window), then `Catalog: <n> entries`; the cache is written, and the log holds `Catalog cache not used: missing` and,
  after the icon line, `Catalog extracted in <t> s (names <a> s, icons <b> s)`.
- S2: Given a cache of the same game and Companion build, when the Companion starts, then the line goes from
  `Catalog: checking` to `Catalog: <n> entries`, names and icons show as after an extraction, no game archive's content
  is read, and the log holds `Catalog loaded from cache in <t> s: <n> entries, <g> goods and <r> regions with an icon`.
- S3: Given a cache, when the game was updated while the Companion was not running, then the start logs
  `Catalog cache not used: game changed`, runs S1's extraction and replaces the cache.
- S4: Given the Companion runs with a catalog, when the game is updated and the player starts it with `/pipe`, then
  once the pipe connection reaches `Connected` the check runs; it finds the change, the names and icons disappear
  (GUIDs show), the `Game:` line shows the build read now, S1's extraction runs and the cache is replaced.
- S5: Given the Companion runs with a catalog, when the player starts the unchanged game, then the check at
  `Connected` changes nothing visible and writes no log line.
- S6: Given a cache written by another Companion build (another version, or another build of the same version), when
  the Companion starts, then it logs `Catalog cache not used: Companion changed` and extracts as in S1.
- S7: Given a cache of one installation, when the settings file names another installation, then the start logs
  `Catalog cache not used: game changed`, extracts, and the cache then holds only the new installation.

## Errors and edge cases

- E1: The catalog extraction fails: `Catalog: failed`, the diagnostics window shows GUIDs, nothing is written to the
  cache, a cache of the old game state is not used, and the next start or game start tries again.
- E2: The names are built but the icons fail: `Catalog: <n> entries, icons failed`, names without icons, the existing
  `Catalog icons failed: …` warning, and nothing is cached, so the next check extracts again.
- E3: The cache cannot be written (folder cannot be created, disk full, access denied, local app data not a full path):
  names and icons are used, the line reads `Catalog: <n> entries, not cached`, and the Warning `Catalog cache not
  written` is logged with the exception where there is one.
- E4: The cache is unreadable, truncated, larger than its bound, of another format, or its content contradicts itself
  or its own checks (a modified file included): no part of it is used, the Warning `Catalog cache not used: invalid` is
  logged, and S1's extraction replaces it.
- E5: The check cannot read what it compares (the build, the listing of `maindata`, an archive's properties): the
  cache is not used (`Catalog cache not used: game changed`) and the extraction runs and reports as today.
- E6: No game is found or the settings file is invalid: `Catalog: no game`; the cache is neither used nor deleted.
- E7: `Exit` during an extraction or while the cache is written: the Companion still ends within 2 seconds, and the
  next start uses either the previous complete cache or none (E4), never a half-written one.
- E8: The game starts (or the pipe reconnects) while a check or an extraction runs: no second one starts; each later
  `Connected` runs a check again.
- E9: The game files change during an extraction: the cache never claims the new state for content read before the
  change, so the next check extracts again.
- E10: An archive rewritten with the same name, size and modification time is not detected; the user guide tells the
  player to delete the cache folder and restart.
- E11: In a replay or with an invalid command line there is no pipe connection: only the check at start runs.
- E12: The game is started without `/pipe`: no connection, so only the check at start runs.

## Non-functional requirements

- N1: The cache lies in the fixed folder `%LOCALAPPDATA%\Censio\Cache`, one cache per Windows account for the
  installation last checked; nothing is written into the installation or elsewhere. No new setting.
- N2: Privacy (record 0023): names and icons are now saved on the player's disk, in the cache only; they are never
  sent, logged, or part of a repository, fixture or release. The documents that promise "held in memory only, never
  saved" change.
- N3: The cache is untrusted input when read (a security area of `.squad/project.md`): bounded before it is read,
  validated completely before any of it is used, and refused as a whole otherwise (fails closed, record 0033).
- N4: A check of an unchanged game reads the build, the listing of `maindata` and each archive's name, size and
  modification time, and no archive content — over SMB too.
- N5: Neither the menu, the pipe connection, ticks, the diagnostics window nor `Exit` waits for a check, a cache load,
  an extraction or a cache write.
- N6: The durations are logged with one decimal place and a decimal point; the user guide and `game-catalog.md`
  state the extraction and cache-load durations measured on the Product Manager's installation.
- N7: A cache load holds no more memory than an extraction of the same game holds today.
- N8: The first start after updating the Companion extracts once (S6); there is no migration.

## Acceptance criteria

- AC1: An unchanged game — same installation folder, build, set of archives below `maindata` with the same sizes and
  modification times, same Companion build — at start publishes names and icons from the cache without reading any
  archive's content, and logs the cache-load line (S2, N4, N7).
- AC2: A different build, an archive below `maindata` added, removed, or with another size or modification time,
  another installation folder, an unreadable property (E5) or another Companion build makes the check extract again,
  log the reason (`missing`, `game changed`, `Companion changed`) and replace the cache; a change during the
  extraction leads to another extraction at the next check (S1, S3, S6, S7, E5, E9, N8).
- AC3: In a live mode each transition of the pipe connection to `Connected` runs a check unless one or an extraction
  runs; the check runs in the background, so the connection and its ticks never wait for it; a found change removes
  names and icons, updates the build of the `Game:` line and extracts; no change shows and logs nothing (S4, S5, E8,
  E11, N5).
- AC4: The `Catalog:` line shows `checking`, `extracting names`, `extracting icons, <n> entries`, `<n> entries`,
  `<n> entries, icons failed`, `<n> entries, not cached`, `no game` and `failed` exactly as in S1, S2, E1, E2, E3 and
  E6.
- AC5: Only a complete result (catalog and icons built) is cached; a failed catalog or icon build caches nothing and
  uses no cache of the old state, and a cache that cannot be written leaves the extracted names and icons in use with
  the Warning (E1, E2, E3, E6).
- AC6: A cache that is missing, unreadable, truncated, above its bound, of another format, modified, or left by an
  interrupted write is never used in part: it is logged (`missing` or the Warning `invalid`) and extracted again (E4,
  E7, N3).
- AC7: Each extraction logs `Catalog extracted in <t> s (names <a> s, icons <b> s)` and each cache load `Catalog loaded
  from cache in <t> s: …`, timed on an injected clock (S1, S2, N6).
- AC8: `README.md` and `docs/USER_GUIDE.md` (English, then German) describe the cache, its folder, when it is
  extracted again, the new menu and log texts, deleting the folder (E10), the privacy change, and the durations the
  Product Manager measured (N1, N2, N6, E10, E12).

## Out of scope

- A menu entry to rebuild or delete the cache, a percentage, and a cache per installation (N1).
- Detecting a game start without the pipe (E12), and a new game search at game start (only the build is read again).
- The catalog format sent to the server (#26) and the upload (#42).

## Open questions

- Hashes or file properties as the key? Answered: properties (N4). Hashing every archive reads the whole of
  `maindata` at every check, over SMB too, and costs more than the extraction it avoids; the build alone is not enough,
  since `game-installation.md` (*Build*) states it unverified that the file version changes with every update. E10 is
  the accepted residual case.
- Use the old cache when the extraction after a change fails? Answered: no; the issue asks for GUIDs, and record 0033
  fails closed (E1).
- What is "when the game starts"? Answered: the pipe connection reaching `Connected` — the only sign of a running
  game the Companion has.
- Who measures the durations (N6)? Answered: only the Product Manager has the game; a manual run on a local build of
  the branch head before the PR approval.

## Area documents

- `game-catalog.md`: a *Cache* section (folder, what decides "unchanged", when the check runs, complete results only,
  validation and bounds, failures, interrupted writes); *Memory only* and *Privacy* revised; *Time* with the measured
  durations; *Network* — an unchanged game reads no archive over SMB.
- `companion.md`: the `Catalog:` texts (*Tray icon and menu*), the new log lines (*Log*), the cache folder (*Folders*),
  the 2-second `Exit` with an extraction running.
- `game-installation.md`: the build is read again at each check at game start, and the `Game:` line shows it.
