# Spec: History time series storage and retention

Source: Issue #35
Status: Approved by Lead

## Problem

Every tick is kept at full resolution, about 55 MB a day without end; nothing reads one island and good over time
(#36 to #38), and no profile can be removed. Record
[0021](../../docs/decisions/0021-sqlite-history-with-retention.md) settled the rule (full resolution for 7 days, then
hourly averages kept without limit, deletion per profile) and left its details to this feature; gaps must stay gaps
(record [0031](../../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md)).

## Scenarios

- S1: Given a profile with stored ticks, when the history of one island and good over a time range is read, then it holds one point per tick of the profile in the range, oldest first, each with its real
  time (the tick's receive time) and game time and every value of that good on that island.
- S2: Given ticks whose whole UTC hour ended at least 7 days ago, when the retention job runs, then the ticks of
  each such hour of a profile are folded into one hourly point of their averages, kept without limit; younger ticks
  stay.
- S3: Given a read whose range spans both, then it returns the hourly points of the reduced hours and the ticks of the
  rest in one time order. An hour can hold an hourly point and ticks at once (a fold in progress, a late tick of E4):
  the read then returns both, but never a tick together with the hourly point that already holds it.
- S4: Given a profile in the profile switch, when the operator chooses `Delete history` on its region overview
  (`/profiles/{profile}/regions`) and confirms the dialog, then every stored datum of that profile is gone, the profile
  leaves the switch and the pages without a reload, and the other profiles, the catalog and the icons are untouched.

## Errors and edge cases

- E1: An island, good or value missing from a tick is a point without that value in S1, never a zero. Time without any
  tick of the profile has no point; nothing is filled in.
- E2: In an hour, a value missing from some ticks is averaged over the ticks that carried it; missing from all, the
  hourly point has no value there. An hour without any tick of the profile has no hourly point.
- E3: The hour of a profile's latest tick is never reduced, so the pages that show it stay as today.
- E4: A tick whose hour was already reduced arrives later (a replay of an old recording keeps its recorded receive
  times, [`replay-sending.md`](../../docs/areas/replay-sending.md)): until the next run a read returns it beside the
  hourly point (S3); the next run folds it in as if it had been there at the first reduction.
- E5: A tick whose identity was stored, also one already reduced, is reported as already stored and changes nothing
  ([`server-data.md`](../../docs/areas/server-data.md), *Storing and reading*), so a resent tick is never counted twice.
- E6: A fold step that fails (disk full, file removed) changes nothing, logs
  `The history could not be reduced; the next run tries again.` (Warning, with the exception) and ends the run; the
  server keeps running. A run cut off by a stop leaves every tick either whole or folded.
- E7: A Companion still sending under a deleted profile creates it again, unnamed until its next hello.
- E8: A page that shows a deleted profile shows `Not found.` / `Nicht gefunden.`; `/regions` follows the profile with
  the newest tick or shows `No data yet. …`. Deleting a profile already
  gone (two browsers) changes nothing.
- E9: A deletion that fails before its rows are gone stores no change, the profile stays whole and the dialog shows
  `The history could not be deleted.` / `Der Verlauf konnte nicht gelöscht werden.`
- E10: The clean-up after a deletion (N5) cannot finish, for example because a read holds the write-ahead log: the
  deletion still counts as done for the user (the profile is gone), the server logs
  `The deleted history could not yet be cleared from the database file; the next retention run tries again.` (Warning)
  and the next retention run tries again until it finishes.
- E11: A tick that carries two entries of one good on one island (the contract allows it): the read and the reduction
  use the first of them in stored position; the stored tick keeps all of them.
- E12: The dialog deletes the profile it named when it opened, never another, also when the page has meanwhile moved
  on; `/regions`, which follows the newest profile, offers no `Delete history`.

## Non-functional requirements

- N1: No new setting: 7 days and one hour are fixed (record 0021). The job runs on the server's clock (injected) after
  the server listens, never delaying the start, and again every hour, also for the in-memory database.
- N2: The job folds in steps of its own write transaction, each of at most one tick, and no step holds the write gate
  for more than 10 seconds at the contract's bounds, so an ingest write waiting behind it is still acknowledged within
  the Companion's 30 seconds ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Failures*); a tick at the
  bounds that one step cannot fold within that is split into smaller steps. The same holds for each step that removes
  a deleted profile's rows (AC6). The plan measures a step at the fixture's size and at the bounds.
- N3: A run that folded at least one tick logs `History reduced to hourly averages: <h> hours from <t> ticks.`
  (Information); a deletion logs `The history of a profile was deleted.` once its rows are gone and
  `The deleted history was cleared from the database file.` once its clean-up finished (Information); a failed deletion
  logs `The history of a profile could not be deleted.` (Warning, with the exception). No log line
  names a profile or island or holds a value (`.squad/project.md`, *Security areas*, server database).
- N4: A reduced hour stores one row per island and good and one per map GUID of that hour, whatever the number of its
  ticks, each with a sum and a count per value, plus one identity row per folded tick (E5).
  The plan measures both against the ticks they replaced.
- N5: Once the clean-up after a deletion finished (N3), no profile or island name that only the deleted profile held
  can be read from the database file or the files SQLite keeps beside it. Older copies of names that a version before
  this one left in the file's free space are the exception; the user guide says how to remove them.
- N6: An existing database gets the new schema at start (record 0065); its ticks are kept and folded step by step while
  the server serves.
- N7: A deletion can be started only from a page this server rendered: a page of another site (DNS rebinding, record
  [0006](../../docs/decisions/0006-server-plain-http-and-open-health.md), *Consequences*) cannot start it. A page opened
  by IP address or as `localhost` may delete; one opened by a host name only when the setting `AllowedHosts` lists it.
- N8: The plan measures the read of S1 for one island and good over 7 days of ticks and over a year of hourly points.
  The area document states every measurement of N2, N4 and N8.

## Acceptance criteria

- AC1: A run folds the ticks of every profile-hour whose end lies 7 days or more before the server's clock into that
  hour's hourly point, except the hour of the profile's latest tick; the point has one row per island and good and one
  per map GUID of the hour; ticks of younger hours stay unchanged (S2, E3, N1, N4).
- AC2: An hourly point carries the hour's start (UTC), the smallest and largest game time of its ticks, their number,
  and per island and good, for each of the eight floats and three integers, the sum and the number of the ticks that
  carried it, and per workforce and building GUID the sum of its amounts and the number of ticks that carried the entry,
  a GUID absent from a carried entry counting as 0. A read gives each mean as a fractional number (sum divided by
  number, integers included, not rounded); a value no tick carried has no mean, never 0; the island's name is that of
  its latest tick in the hour (S2, E2).
- AC3: A tick arriving for an already reduced hour is folded into its hourly point at the next run; the counts, the
  integer sums and the game times then equal those of folding all of the hour's ticks at once, and each float sum
  differs from that at most by the rounding of the order of additions (at most n × 2^-52 times the sum of the absolute
  values of its n terms). A tick whose
  identity was stored, also folded, is reported as already stored and changes nothing (E4, E5).
- AC4: A read of one island and good over a time range returns, oldest first, one point per hourly point and per tick
  of the profile in the range, each with real time and game time, a missing value as no value, and no point for time
  without ticks; a folded tick is never returned beside the hourly point that holds it, and a tick not yet folded is
  (S1, S3, E1, E4, N8).
- AC5: Each fold step is its own write transaction; a failing or interrupted run leaves every tick whole or folded, logs
  the warning of E6 and the next run continues; a run that folded logs the line of N3 (E6, N2, N3, N6).
- AC6: Deleting a profile removes its profile row and islands at once, and from then on no read returns its ticks or
  hourly points; steps of their own then remove those and its tick identities, so that once the clean-up line of N3 is
  logged no row of it is left; other profiles, the catalog and the icons read back unchanged; deleting an unknown
  profile changes nothing (S4, E8, N2).
- AC7: After a deletion its held state is dropped and followers are told: the profile switch no longer lists it, a page
  showing it shows `Not found.`, and `/regions` follows the next profile; a later tick of that profile creates it
  without a name (S4, E7, E8).
- AC8: Once the clean-up line of N3 is logged, none of the names held only by the deleted profile (the test uses names
  no other profile holds) occurs in the bytes of the database file or its `-wal` and `-shm` files; a clean-up that a
  held read blocks logs the warning of E10, and the next run finishes it (N5, E10).
- AC9: `/profiles/{profile}/regions` offers `Delete history` / `Verlauf löschen`, `/regions` does not; it opens a dialog
  `Delete history?` / `Verlauf löschen?` with `All data of the profile <name> is removed from the server: its regions,
  islands, goods and history. This cannot be undone.` / `Alle Daten des Profils <name> werden vom Server entfernt: seine
  Regionen, Inseln, Waren und sein Verlauf. Das lässt sich nicht rückgängig machen.`, `<name>` the profile's shown name
  as in the profile switch (`Unnamed profile` / `Unbenanntes Profil` without one), and the buttons `Delete` / `Löschen`
  and `Cancel` / `Abbrechen`; only `Delete` deletes, and it deletes the identity the dialog opened for; a failure shows
  the text of E9; after a deletion the page goes to `/regions`. On a page opened by a host N7 does not trust, the button
  is disabled and followed by `Deleting needs this page opened by IP address or as localhost, or by a host name the
  setting AllowedHosts lists.` / `Löschen setzt voraus, dass diese Seite über die IP-Adresse oder localhost geöffnet
  wurde, oder über einen Hostnamen, den die Einstellung AllowedHosts nennt.`, and no event of that page deletes (S4,
  E9, E12, N7).
- AC10: A deletion, its clean-up and a failed deletion log the lines of N3 and no name (N3).
- AC11: A tick stored while fold or removal steps run waits at most for the step in progress: the write gate passes
  to it before the next step (N2).
- AC12: A tick with two entries of one good on one island is read and folded by its first entry (E11).

## Out of scope

- Charts and how they draw time without points (#36 to #38).
- Shrinking the database file: space freed by retention and deletion is reused by new data; the file does not shrink.
- Deleting part of a profile's history, an undo, an export.

## Open questions

- Q1: How does the operator start a deletion? Answered: (Product Manager) option A, a `Delete history` button with a
  confirmation dialog on the profile's region overview, without login like every page (record 0006), with record 0006
  revisited so that a page of another site cannot start it (N7, AC9).
- Q2: Does deleting the history keep the profile and its name? Answered: (Lead) no, "removes it completely" (issue).
- Q3: Does an absent workforce or building GUID mean 0? Answered: (Lead) yes for a carried entry: the building amounts
  sum to the building count ([`protocol.md`](../../docs/protocol.md)).
- Q4: Which hour holds a tick? Answered: (Lead) the UTC hour of its receive time; the server's clock decides only when
  an hour is old enough.
- Q5: Is the latest tick reduced after 7 days? Answered: (Lead) no (E3).

## Area documents

A new area `history` (series, retention, deletion and its clean-up, measurements); `server-data.md`, `server-state.md`,
`web-pages.md`, `web-ui.md`, `server.md` and `replay-sending.md` point to it; the plan lists each edit.

## Challenge

Spec challenge by the Devil's Advocate (4 major, 5 minor), all accepted:

1. (major) N4 against E2, E4, AC3: an hourly point stores a sum and a count per value (AC2, N4); N4 is a row rule
   plus a measured size; AC3 defines equality at stored precision.
2. (major) No ceiling on the write gate: fold steps of at most one tick, at most 10 seconds at the bounds, split
   further if needed (N2, AC5); AC11 asks for the ingest's acknowledgement within 30 seconds during such a fold.
3. (major) Two entries of one good in one tick: the read and the fold use the first in stored position (E11, AC12).
4. (major) Clean-up after a deletion: the deletion is done once its rows are gone; the clean-up follows with its own
   line, a warning when blocked and a retry (N3, E10, AC8); E9 covers only a failure before the rows are gone.
5. (minor) Shared names: only names no remaining profile holds (N5, AC8).
6. (minor) Point and late tick in one hour: a read returns both until the next run (S3, E4, AC4).
7. (minor) Wrong profile: only `/profiles/{profile}/regions` offers the button; the dialog deletes the identity it
   opened for; `<name>` is the switch's shown name (E12, AC9).
8. (minor) Fractional means: read unrounded (AC2).
9. (minor) Read time: N8.
