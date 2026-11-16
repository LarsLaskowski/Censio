# Spec: History time series storage and retention

Source: Issue #35
Status: Draft

## Problem

The server stores every tick at full resolution ([`server-data.md`](../../docs/areas/server-data.md), *What is stored*):
about 77 KB per real tick, 55 MB a day, without end. Nothing reads a value of one island and good over time, which the
trend features #36 to #38 need, and a profile cannot be removed: every replay run adds a profile `Replay <time>` that
stays ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Growth*). Record
[0021](../../docs/decisions/0021-sqlite-history-with-retention.md) settled the rule (full resolution for 7 days, then
hourly averages kept without limit, deletion per profile) and left its details to this feature; gaps must stay gaps
(record [0031](../../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md)).

## Scenarios

- S1: Given a profile with stored ticks, when the history of one island and good over a time range is read (by the
  charts of #36 to #38), then it holds one point per tick of the profile in the range, oldest first, each with its real
  time (the tick's receive time) and game time and every value of that good on that island.
- S2: Given ticks whose whole UTC hour lies more than 7 days in the past, when the retention job runs, then the ticks of
  each such hour of a profile are replaced by one hourly point of their averages, kept without limit; younger ticks
  stay as they are.
- S3: Given a read whose range spans both, then it returns the hourly points of the reduced hours and the ticks of the
  rest in one time order, never a tick together with the hourly point that already holds it.
- S4: Given a profile in the profile switch, when the operator chooses `Delete history` on its region
  overview and confirms the dialog, then every stored datum of that profile is gone, the profile leaves the switch and
  the pages without a reload, and the other profiles, the catalog and the icons are untouched.

## Errors and edge cases

- E1: An island, good or value missing from a tick (no island in that tick, no entry for the good, a `null` value) is a
  point without that value in S1, never a zero. Time without any tick of the profile (the game closed, ticks the
  Companion dropped) has no point; nothing is filled in.
- E2: In an hour, a value missing from some ticks is averaged over the ticks that carried it; missing from all, the
  hourly point has no value there. An hour without any tick of the profile has no hourly point.
- E3: The hour of a profile's latest tick is never reduced, so its latest tick and the pages that show it stay as today
  ([`server-state.md`](../../docs/areas/server-state.md)), also for a profile not played for weeks.
- E4: A tick whose hour was already reduced arrives later (a replay of an old recording keeps its recorded receive
  times, [`replay-sending.md`](../../docs/areas/replay-sending.md)): at the next run it is folded into that hourly point
  as if it had been there at the first reduction.
- E5: A tick whose identity was stored, also one already reduced, is reported as already stored and changes nothing
  ([`server-data.md`](../../docs/areas/server-data.md), *Storing and reading*), so a resent tick is never counted twice.
- E6: A retention run that fails (disk full, file removed) reduces nothing of the hour it was on, logs
  `The history could not be reduced; the next run tries again.` (Warning, with the exception) and the server keeps
  running; a run cut off by a stop leaves every hour either whole or reduced.
- E7: A Companion still sending under a deleted profile: its next tick creates the profile again without a name, as for
  any unknown profile ([`server-data.md`](../../docs/areas/server-data.md)); its next hello names it.
- E8: A page that shows a deleted profile shows `Not found.` / `Nicht gefunden.`; `/regions` follows the profile with
  the newest tick or shows `No data yet. …` ([`web-pages.md`](../../docs/areas/web-pages.md)). Deleting a profile that
  is already gone (two browsers) changes nothing and shows the same.
- E9: A deletion that fails stores no change, the profile stays whole and the dialog shows
  `The history could not be deleted.` / `Der Verlauf konnte nicht gelöscht werden.`

## Non-functional requirements

- N1: No new setting: 7 days and one hour are fixed (record 0021). The job runs on the server's clock (injected) after
  the server listens, never delaying the start, and again every hour, also for the in-memory database.
- N2: The job reduces one profile-hour per write transaction, so an ingest write waits at most for one such hour; the
  plan measures that time at the fixture's size and at the bounds and the area document states it.
- N3: A run that reduced at least one hour logs `History reduced to hourly averages: <h> hours from <t> ticks.`
  (Information); a deletion logs `The history of a profile was deleted.` (Information). No log line names a profile or
  island or holds a value (`.squad/project.md`, *Security areas*, server database).
- N4: The stored size of a reduced hour is at most that of one tick holding every island and good of that hour.
- N5: After a deletion is reported done, no profile or island name of the deleted profile can be read from the database
  file or the files SQLite keeps beside it.
- N6: An existing database is updated by a new schema version at start (record
  [0065](../../docs/decisions/0065-database-schema-kept-by-migrations-applied-at-start.md)); its stored ticks are kept
  and reduced by the job hour by hour, while the server serves.
- N7: A deletion can be started only from a page this server rendered: a page of another site (DNS rebinding, record
  [0006](../../docs/decisions/0006-server-plain-http-and-open-health.md), *Consequences*) cannot start it.

## Acceptance criteria

- AC1: A run reduces the ticks of every profile-hour whose end lies more than 7 days before the server's clock to one
  hourly point of at most one row per island, good and map GUID of that hour, except the hour of the profile's latest
  tick; ticks of younger hours stay unchanged (S2, E3, N1, N4).
- AC2: An hourly point carries the hour's start (UTC), the smallest and largest game time of its ticks, their number,
  and per island and good the mean of each of the eight floats and three integers over the ticks that carried it, and
  per workforce and building GUID the mean amount over the ticks that carried the entry, a GUID absent from a carried
  entry counting as 0; a value no tick carried has no mean, never 0; the island's name is that of its latest tick in
  the hour (S2, E2).
- AC3: A tick arriving for an already reduced hour is folded into its hourly point at the next run, with the result
  equal to reducing all of the hour's ticks at once; a tick whose identity was stored, also reduced, is reported as
  already stored and changes nothing (E4, E5).
- AC4: A read of one island and good over a time range returns, oldest first, one point per hourly point and per tick
  of the profile in the range, each with real time and game time, a missing value as no value, and no point for time
  without ticks; no tick is returned beside the hourly point that holds it (S1, S3, E1).
- AC5: Each profile-hour is reduced in its own write transaction; a failing or interrupted run leaves every hour whole
  or reduced, logs the warning of E6 and the next run continues; a run that reduced logs the line of N3 (E6, N2, N3,
  N6).
- AC6: Deleting a profile removes its profile row, its ticks, hourly points, islands and every tick identity; other
  profiles, the catalog and the icons read back unchanged; deleting an unknown profile changes nothing (S4, E8).
- AC7: After a deletion its held state is dropped and followers are told: the profile switch no longer lists it, a page
  showing it shows `Not found.`, and `/regions` follows the next profile; a later tick of that profile creates it
  without a name (S4, E7, E8).
- AC8: After a deletion is reported done, none of the deleted profile's names occurs in the bytes of the database file
  or its `-wal` and `-shm` files (N5).
- AC9: The region overview of a profile offers `Delete history` / `Verlauf löschen`; it opens a dialog `Delete history?`
  / `Verlauf löschen?` with `All data of the profile <name> is removed from the server: its regions, islands, goods and
  history. This cannot be undone.` / `Alle Daten des Profils <name> werden vom Server entfernt: seine Regionen, Inseln,
  Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.` and the buttons `Delete` / `Löschen` and `Cancel` /
  `Abbrechen`; only `Delete` deletes, a failure shows the text of E9, and a page of another site cannot start it (S4,
  E9, N7).
- AC10: A deletion logs the line of N3 and no name (N3).

## Out of scope

- Charts, the range picker, the real-time / game-time switch, sparklines and how a chart draws time without points
  (#36 to #38).
- Shrinking the database file: space freed by retention and deletion is reused by new data; the file does not shrink.
- Deleting part of a profile's history, an undo, an export, or deleting the catalog and icons.
- A web login (record 0006).

## Open questions

- Q1: How does the operator start a deletion? Answered: (Product Manager) option A, a `Delete history` button with a
  confirmation dialog on the profile's region overview, without login like every page (record 0006), with record 0006
  revisited so that a page of another site cannot start it (N7, AC9).
- Q2: Does deleting the history keep the profile and its name? Answered: (Lead) no, "removes it completely" (issue);
  an empty profile would stay in the switch with nothing to show, and replay profiles are what users want gone.
- Q3: Does an absent workforce or building GUID mean 0? Answered: (Lead) yes for a carried entry: a map is the set of
  GUIDs with an amount and the building amounts sum to the building count ([`protocol.md`](../../docs/protocol.md),
  *AreaProductionStatistics*); a missing entry stays a gap (AC2).
- Q4: Which hour holds a tick? Answered: (Lead) the UTC hour of its receive time, the Companion's read stamp
  ([`contract.md`](../../docs/areas/contract.md)); the server's clock decides only when an hour is old enough.
- Q5: Is the latest tick reduced after 7 days? Answered: (Lead) no, its hour is kept (E3), else the pages of a profile
  not played for a week would show nothing.

## Area documents

- New area `history`: the series read and its points, gaps, the retention rule and job (schedule, transaction per
  hour, late ticks, log lines, measured times), deleting a profile and what it removes, privacy of the deletion.
- `server-data.md`: the new tables, tick identities kept after reduction, the *Not here* line on retention.
- `server-state.md`: a deleted profile leaves the held state and its followers are told.
- `web-pages.md` and `web-ui.md`: the delete action, the dialog, its texts in English and German.
- `replay-sending.md`: *Growth* points to deletion and retention instead of #35.
- `server.md` (record 0006 revisited for N7), and `README.md` and `docs/USER_GUIDE.md`, English and German.
