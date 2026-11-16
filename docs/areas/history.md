# History

## Scope

The time series the server keeps per profile, island and good: the read of a series and its points and gaps, the
retention rule (ticks at full resolution for 7 days, then hourly sums and counts without limit), the fold and its cutoff,
the latest hour, late and resent ticks, repeated goods, the retention job with its schedule and log lines, deleting a
profile's history with its removal steps, secure delete and the clean-up of the database file, the legacy residue and the
measurements. Operators who run the server rely on the retention and the deletion; the charts of later features (#36 to
#38) call the series read.

## Behavior

### What is kept

- A tick is kept as it was stored ([`server-data.md`](server-data.md)) for 7 days. A tick 7 days old or older is
  folded into the hourly point of its profile and UTC hour and removed; the hourly points are kept without limit.
- A tick belongs to the UTC hour of its receive time, the Companion's read stamp ([`contract.md`](contract.md)); the
  server's clock decides only when an hour is old enough. A replay keeps the recorded receive times
  ([`replay-sending.md`](replay-sending.md)), so a tick of an old recording is old at once.
- An hourly point holds the hour's start, the number of folded ticks and their smallest and largest game time. Per island
  it holds the island key and the name of its latest tick in the hour (receive time, then storing order). Per good of the
  island it holds the number of entries and, for each of the eight floats (generation, consumption, delta, perfect
  generation, perfect consumption, income, summed productivity, average productivity), the sum and the number of ticks
  that carried it, and the sums of the three integers (building count, maintenance, profit). Per workforce and building
  GUID of the good it holds the sum of its amounts. A GUID that a carried entry lacks counts as 0, so the building
  amounts of an hour sum to its building count ([`protocol.md`](../protocol.md)).
- A float is added as a double; a negative zero becomes a sum of zero, so an hourly mean does not keep the bit pattern
  that a tick keeps (record 0083). An hour without any tick of the profile has no hourly point, and a value that no tick
  of the hour carried has a count of 0.
- The identity of every folded tick is kept, in its own table with the number of its hourly point, so a resent tick is
  never counted twice. The table grows by one row per folded tick without end: 107.9 bytes per tick measured (*Measurements*).

### The series read

- A read names a profile, an island (session GUID, island ID, area index), a good (product GUID) and a time range from a
  start (included) to an end (excluded). It returns, oldest first, one point per hourly point whose hour start lies in
  the range and one per stored tick of the profile whose receive time lies in the range, each read in one read
  transaction. A folded tick is never returned beside the hourly point that holds it; a tick that is not folded yet is.
  Time without a tick of the profile has no point; nothing is filled in. The range is not limited, so a caller that
  anyone can reach must bound it.
- A tick's point has the receive time as its time, one tick, both game times equal to the tick's, the island's name in that
  tick and every value of the good's entry as a number (a float widened to a double, an integer as a double) or no value.
  When the tick carries the good twice on the island, the read uses the first entry in stored position; the stored tick
  keeps both.
- An hourly point has the hour start as its time, the number of ticks, the smallest and largest game time, the island's
  name of the hour and, per float, the sum divided by the number of ticks that carried it, not rounded, and no value when
  that number is 0, never 0. The means of the three integers and of every amount are the sum divided by the number of
  entries of the good in that hour. An hourly point whose hour had the island without the good has the name and no
  values; one whose hour lacked the island has neither.
- A tick or hourly point that lacks the island or the good is a point without values, with no name when the island is
  missing: a gap in the values, not in the time. A value a tick did not carry is no value, never 0.
- An hour can hold an hourly point and ticks at once (a fold in progress, a late tick). The read returns both, the hourly
  point first on equal times, then by storing order.

### Retention

- A fold step folds one tick in one write transaction. The cutoff is the start of the hour 7 days before the server's
  clock; the tick folded is the profile's oldest by receive time (ties by storing order) whose receive time is before
  the cutoff and before the start of the hour of the profile's latest tick. So an hour is folded once it ended 7 days or
  more ago, and the hour of a profile's latest tick is never folded, so the pages that show the latest tick stay as they
  are, also for a profile not played for a week.
- A step reads the tick, adds it to its hourly point (the point is created by the first tick), takes each island's name
  when the tick is not older than the one held, adds the first entry of each good in stored position and its map pairs,
  and no other entry of the same good, records the identity and removes the tick's rows. It either completes or leaves the
  tick whole. The memory of a step is one tick, as for the ingest.
- A tick that arrives for an hour that was folded is folded into that point at the next run: its counts, integer sums and
  game times then equal those of folding all ticks of the hour at once, and each float sum differs at most by the rounding
  of the order of the additions (at most n × 2^-52 times the sum of the absolute values of its n terms). Until then a read
  returns it beside the hourly point.
- A resent tick, whose identity is stored as a tick or as a folded tick, is reported as already stored and changes
  nothing. The ticks of a deleted profile that are not yet swept count as stored, so a resent one is reported as already
  stored and then swept; after its sweep it is stored again and creates the profile without a name.

### The retention job

- The job starts after the server listens, so the start never waits for it, and runs on the server's clock: a run at
  once, then one an hour after each run ended, and one more whenever a deletion asks for it. The job also runs for the in-memory
  database. Runs never overlap. The first run folds the backlog of a database that an older version filled, step by step while
  the server serves.
- A run first sweeps what a deletion left (*Deleting a profile*), then clears the database file when a clean-up is
  requested, then folds profile by profile, steps until a profile has nothing to fold, over the profiles known when the
  folding starts; a profile created meanwhile waits for the next run. Before each fold step the run checks whether a
  deletion asked meanwhile and, if so, sweeps first, so a deletion waits at most for the fold step in progress, not for
  a backlog of folds. A clean-up is attempted at most once right after the first sweep and once more after the folding
  when a sweep during it asked anew; a failed attempt is not repeated in that run and is kept for the next. A fold that
  keeps failing never holds the clean-up back.
- A failed step ends the run, changes nothing and is logged; the next run tries again. A stop of the server cancels the
  loop and waits for it to end; a step cut off by it rolls back and logs nothing.
- **Gate.** Every fold and removal step runs inside the one write gate that serializes every write ([`server-data.md`](server-data.md)),
  and the gate hands itself to the first caller waiting, so a message stored while a step runs waits for that step and is
  stored before the next one (`SemaphoreSlim` queues its asynchronous waiters first in, first out). The ingest therefore
  waits at most for one step, which the measurements put at about 4 seconds at the contract's bounds, within the Companion's
  30 seconds ([`replay-sending.md`](replay-sending.md), *Failures*).

### Deleting a profile

- Deleting a profile's history removes the profile completely: its row, its islands, its ticks and its hourly points.
  The catalog, the icons and the other profiles stay. A profile that is not stored changes nothing.
- One write transaction removes the profile's row and its islands, creates a fresh random identity (a tombstone) and sets
  it as the profile of the profile's ticks and hourly points, one row per tick or hour. From then on no read by profile
  and no read of a tick by its identity returns them, and the server's state drops the profile
  ([`server-state.md`](server-state.md)). The tombstone is no profile and is carried by no profile's row; a row number
  would not serve as a mark, because SQLite reuses the number of a deleted row.
- Steps of their own then remove the tombstoned rows, each in one write transaction: one tick with its islands, entries
  and pairs, else one hourly point with its islands, entries, pairs and the identities of its folded ticks, else the
  tombstone once nothing is left. A tick of the deleted profile that arrives during the steps creates the profile again,
  unnamed until its next hello, with rows under its own identity that the steps leave alone.
- **Clean-up.** The server's connection runs with secure delete, so SQLite overwrites everything this version deletes
  (retention included), and after the steps the write-ahead log is checkpointed and truncated, under the write gate. A
  reader that holds an older snapshot blocks the truncation: the clean-up then logs the warning below and the next run
  tries again until it succeeds. The in-memory database has no file and counts as cleared. Once the clean-up line is
  logged, none of the names that only the deleted profile held occurs in the database file or in its `-wal` and `-shm`
  files; a test with the real schema checks that.
- **Residue of older versions.** A version before this one deleted nothing and left copies of changed names in the free
  space of the file; those stay until the operator runs `VACUUM` with the server stopped ([`USER_GUIDE.md`](../USER_GUIDE.md)).
  The file never shrinks by retention or deletion; the space is reused by new data.

### Log lines

Category `Censio.Server.History`. No line names a profile or an island or holds a value.

| Level | Text | When |
| ----- | ---- | ---- |
| Information | `History reduced to hourly averages: <h> hours from <t> ticks.` | a run folded at least one tick; `<h>` counts the distinct profile and hour pairs, `<t>` the ticks |
| Warning | `The history could not be reduced; the next run tries again.` with the exception | a fold step failed |
| Information | `The history of a profile was deleted.` | the rows of a deletion are gone |
| Warning | `The history of a profile could not be deleted.` with the exception | a deletion failed before its rows were gone |
| Information | `The deleted history was cleared from the database file.` | the clean-up finished |
| Warning | `The deleted history could not yet be cleared from the database file; the next retention run tries again.` (with the exception unless a read blocked it) | a removal step or the truncation failed, or a read blocks it |

After a failed fold step the warning comes first, then the line of the ticks already folded in that run.

## Limits and errors

- A deletion that fails before its rows are gone stores no change: the profile stays whole and the dialog of
  [`web-pages.md`](web-pages.md) shows its failure text. A deletion counts as done once its rows are gone, also when the
  clean-up has not finished.
- A write that fails (disk full, file removed) changes nothing of its step ([`server-data.md`](server-data.md)).
- The deletion is reachable without a login, from a page opened by a trusted host only ([`web-pages.md`](web-pages.md),
  record 0085).

### Measurements

Measured on a 4-vCPU Linux sandbox, ext4, SQLite with `synchronous=FULL`, write-ahead logging and secure delete as the
server opens it, Release build, by a scratch program outside the repository that stores the ticks of the fixture
`long-session.jsonl` (33 ticks, 21,091 entries in all, at most 682 entries per tick; built as in
[`server-data.md`](server-data.md), *Measurements*) and a tick at the contract's bounds (256 islands of 512 entries,
131,072 entries and 393,216 pairs) through the stores of the server. The figures are an order of magnitude, not a guarantee;
three runs unless a row says otherwise. The limit is 10 seconds a step at the bounds.

| What | Result |
| ---- | ------ |
| Store of one fixture tick | median 8.5 to 17.0 ms, maximum 14.5 to 67.2 ms |
| Fold step, one fixture tick | median 18.6 to 22.3 ms, minimum 1.2 to 1.7 ms, maximum 23.6 to 84.1 ms; 5,010 steps in one run: median 16.1 ms, 90th percentile 20.7 ms, maximum 371.0 ms |
| Store of the tick at the bounds | 2.1 to 2.5 s |
| Fold step, tick at the bounds | 3.85 to 3.92 s |
| Removal step, tick at the bounds | 0.41 to 0.45 s |
| Removal step, hourly point at the bounds | 0.46 to 0.64 s |
| Removal step, hourly point of the fixture (682 entries, about 1,250 pairs) | median 3.2 ms, maximum 12.6 ms (200 steps) |
| A store queued 0.3 s into a fold step at the bounds | acknowledged 3.56 to 3.63 s later, about 10 ms after the step ended and before the next fold step queued behind it, in all three runs |
| A store queued 33 ms into a removal step of the tick at the bounds | acknowledged 8 to 10 ms after the step ended (0.41 to 0.43 s after the step began), in all three runs |
| The deletion's first transaction, 7 days of fixture ticks (5,040 ticks) | 12, 11 and 17 ms |
| The deletion's first transaction, a year of hourly points (8,684, built from the 167 real hours of the 7-day run) and 20,190 unfolded ticks | 1,133, 1,130 and 1,134 ms |
| The series read over 7 days of ticks (5,040 points) | 146, 82 and 82 ms |
| The series read over a year of hourly points (8,714 points) | 163, 152 and 126 ms |

The tick rows of the deletion's measurement over a year were written as rows of the tick table alone, since the first
transaction changes only the profile of those rows and of the hourly points; no figure here extends beyond those sizes.

Size, measured on the file after a truncating checkpoint, by the pages of each table and its indexes:

| What | Ticks | Hourly rows |
| ---- | ----- | ----------- |
| One fixture hour of 33 ticks, folded | 2,543,616 bytes (77,079 per tick) | 122,880 bytes |
| 7 days of fixture ticks, one every two minutes (5,040 ticks), folded but the latest hour: 5,010 ticks in 167 hours | 425,246,720 bytes (84,880 per tick, 2.55 MB per hour of 30 ticks) | 22,863,872 bytes (136,909 per hour, about 5 % of the ticks) |

A file with a year of hourly rows (8,684 hours of 682 entries on average, made by copying the 167 real hours 52 times) was
1,256 MiB. The table of
folded identities grew by 540,672 bytes for 5,010 ticks, 107.9 bytes per folded tick (the identity as text with its unique
index): about 78 KB a day for a profile played with one tick every two minutes, about 28 MB a year, which stays when the
ticks are gone.

## Related decisions

- [`decisions/0021-sqlite-history-with-retention.md`](../decisions/0021-sqlite-history-with-retention.md) — full resolution for 7 days, then hourly values kept without limit, deletion per profile.
- [`decisions/0031-bounded-local-tick-buffer-gaps-accepted.md`](../decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) — a gap stays a gap.
- [`decisions/0083-history-folded-tick-by-tick-into-hourly-sums-and-counts.md`](../decisions/0083-history-folded-tick-by-tick-into-hourly-sums-and-counts.md) — the fold, one tick per transaction, sums and counts, the latest hour and the kept identities.
- [`decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md`](../decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md) — a profile is gone at once for its readers, its rows are removed in steps, the file is cleared of them.
- [`decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md`](../decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md) — the deletion needs a page opened by a trusted host.

## Not here

- The tables, the file, the start and the rules of storing: [`server-data.md`](server-data.md).
- The state a deletion drops and rebuilds: [`server-state.md`](server-state.md).
- The button, the dialog and the host rule: [`web-pages.md`](web-pages.md); the setting `AllowedHosts`: [`server.md`](server.md).
- Charts and how they draw time without points (#36 to #38).
- Shrinking the database file and removing the residue of older versions: the user guide.
