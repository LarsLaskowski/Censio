# History

## Scope

The time series the server keeps per profile, island and good: the read of a series and its points and gaps, the four
trend reads of the charts and the tables' trend column with the trend gate, the retention rule (ticks at full resolution for 7 days, then hourly sums and counts without limit), the fold and its cutoff,
the latest hour, late and resent ticks, repeated goods, the retention job with its schedule and log lines, deleting a
profile's history with its removal steps, secure delete and the clean-up of the database file, the legacy residue and the
measurements. Operators who run the server rely on the retention and the deletion; the charts and the trend column
([`web-charts.md`](web-charts.md)) call the trend reads, and the charts of later features (#49 to #51) call these or the
series read.

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

### The trend reads

- Four reads serve [`web-charts.md`](web-charts.md): two for the charts, two for the sparklines of the tables (*The
  batch reads of the trend column*). Each returns one point per tick or hourly point of a
  range, never more than the maximum its caller names (the server asks for 2,880 for a chart and 240 for a table). The good
  read names a profile, a set of islands, a good and the range; the figures read names a profile, one island and the range.
  Each also returns the number
  of all points of the range, so a caller can say that it shows the newest only. A query names a resolution:
  `Points` (the default, the points just described) or `Hours` (*Hourly resolution*). A point also carries its game
  time, its first game time and whether no island of it holds an entry (*Game times*).
- **Times first.** One read transaction takes the count of all points of the range and the times of the newest points up to
  the maximum: the ticks by receive time and storing order, the hourly points by hour, merged oldest first, an hourly point
  before a tick of equal time. A range holds hourly points and ticks as the series read says: a folded tick is never
  beside the hourly point that holds it.
- **A good point.** For a tick: production (generation), consumption and delta, each the sum over the islands of the set
  that carry the value in their first entry of the good in stored position, and no value where none does. For an hourly
  point: the same sum of the islands' hourly means (the sum divided by the number of ticks that carried it, no value when
  that number is 0). A float is read as its stored bit pattern, widened to a double in C# and summed there in island key
  order; no float goes through SQL `SUM`, `AVG` or `ROUND` (integers may).
- **A figures point.** For a tick: the island's key figures as *Key figures* of [`web-pages.md`](web-pages.md) count them
  (buildings the sum of the entries' building counts, produced goods the entries with a building count above 0, goods in
  deficit the entries whose delta rounded to two decimals, midpoint away from zero, is below 0), and no value when the tick
  lacks the island or the island has no entries. For an hourly point: buildings `Math.Round(Σ BuildingCountSum / EntryCount,
  MidpointRounding.AwayFromZero)` over the island's goods, produced goods the goods with `BuildingCountSum > 0`, goods in
  deficit the goods with at least one delta whose mean `DeltaSum / DeltaCount`, rounded as above, is below 0.
- The island set is one JSON parameter read through SQLite's `json_each`, never one parameter per island and never
  formatted into the statement; the rows a tick adds stay bounded by the tick's islands (256). The reads use the index of
  the tick entries by tick island, good and position ([`server-data.md`](server-data.md)); the series read keeps its
  statement.
- **Game times.** A tick gives its game time (the first game time is the same). An hourly point gives its smallest and
  largest game time, those of all ticks folded into it. `HasNoEntries` is set for a tick or hourly point none of whose
  islands holds an entry (the tick the game sends after a load, [`pipe.md`](pipe.md)); it is read with an `EXISTS` in the part's own
  transaction, so the times transaction reads no more than before.
- **Parts.** After the times, the values are read oldest first in parts of at most 240 consecutive points (`TrendPartSize`),
  one read transaction each. A part keeps only its points, and no tick is held beyond the one folded into a point.
- **Hourly resolution (`Hours`).** The read gives one point per UTC hour of the range that holds a tick or an hourly
  point, folded or not; the cap is 2,880 hours, the count is that of all such hours.
  - *Times.* One read transaction reads, from the indexes only, the newest hours that hold an hourly point, the newest hours
    that hold a tick with their tick counts (a `GROUP BY` of the receive time floored to the hour over every tick of the
    range) and the count of all distinct hours (a `UNION` of both); the newest 2,880 distinct hours are kept.
  - *Parts of scanned rows.* The hours, oldest first, are cut into parts of at most `TrendPartSize` rows (240), an hourly
    point and a tick counting one row each, so a part scans at most 240 ticks or hourly points; an hour with more ticks
    continues in the next part, and a part may hold several hours. A part selects the ticks of its slice by a keyset on the
    receive time and storing order.
  - *Values.* The hour's values are accumulated in C# from the hourly point of the hour, if any, and then the ticks, per
    island (good read) or per good (figures read), and the hour's point is formed as the hourly point after the fold would
    read: each value the mean over the ticks that carried it, summed over the islands in key order; for the figures the
    first entry of each good of the island, as the fold keeps. A late tick of a folded hour counts on top of the folded point.
  - *Game times of an hour.* The largest game time and the smallest over the ticks that hold an entry and over the folded
    point when it holds one; an hour with no entry anywhere is flagged `HasNoEntries` and takes the smallest and largest over
    all its rows. The smallest of a folded hour covers all its folded ticks, a no-entry tick included, so it can be that
    tick's (*Game-time axis* of [`web-charts.md`](web-charts.md)).
  - The read scans every tick of its range, so its time grows with the tick rate and the range, not with the cap
    ([`web-charts.md`](web-charts.md), *Limits and errors*).
- **The batch reads of the trend column.** Two more reads give a table's sparklines in one read for all its rows, each of
  one profile, a range and the newest `MaxPoints` points (the server asks for 240, over the fixed four hours of
  [`web-charts.md`](web-charts.md), *Sparklines and trend arrows*) at `Points` resolution, with the times of *Times first*
  and then the values in parts, each part one read transaction under the trend gate.
  - *Goods of one island.* The read names one island and a set of product GUIDs (at most the 500 shown rows). A point holds,
    for each good of the set, the `Delta` of the good's first entry of the island in stored position in that tick (no value
    where that delta has none, and the second entry of a repeated good is never used), or for an hourly point the mean
    `DeltaSum / DeltaCount` of the island's hourly entry; a good with no value is absent from the point. A part is one
    statement over at most 240 ticks, the goods read through `json_each` of one JSON parameter, ordered by tick and
    position.
  - *Buildings of a set of islands.* The read names a set of islands (the island list's region, unbounded in count). A point
    holds, for each island that has entries, its buildings: the sum of the entries' `BuildingCount` for a tick, and for an
    hourly point `Math.Round(Σ BuildingCountSum / EntryCount, MidpointRounding.AwayFromZero)`; an island with no entries in
    the point is absent. The island set is one JSON parameter as the figures read has it.
  - *Parts of island points.* A part reads at most 240 island points (a tick or hourly point of one island), so that it scans
    no more entries than a part of the figures read: the islands are cut in chunks of 240, the last chunk taking the rest,
    and a chunk of `c` islands reads its points in slices of `240 / c` points (integer division, at least one). 24 islands by
    240 points are 24 parts, 240 islands by 240 points 240 parts and 241 islands by 240 points 241 parts; no point or no
    island gives no part. A part of the goods read is a part of 240 points like the others.
  - Like the other trend reads they return at most the maximum of points asked and the count of all points of the range.
- **The trend gate.** Every transaction of a trend read, the times' and each part's, runs under the database's trend gate,
  taken before the in-memory database's write gate, so the trend reads of the whole server run one at a time and the
  clean-up of a deleted profile (*Deleting a profile*) waits for the part in progress and lets none start until it has
  truncated the write-ahead log. On the in-memory database every transaction also takes the write gate, so the ingest waits
  at most for one part (*Measurements*).
- **A fold or a deletion between two parts.** A fold can remove a tick whose time a read already holds. The tick's point then
  has no value (a gap in the drawing), and the hourly point that now holds the tick was read in an earlier part, or did not
  exist when the times were read, so it does not count it: the drawing misses the tick and never shows it twice. This
  concerns only a profile whose range reaches back 7 days (idle, or a replayed old recording), and the next read draws
  the hour as one mean. The points of a profile deleted between two parts have no value in the same way.

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
  tries again until it succeeds. A chart read's part never overlaps the truncation: the clean-up waits for the part in
  progress, takes the trend gate before the write gate and holds it through the checkpoint, so no part starts during it
  and no chart read can keep a deleted profile's names in the write-ahead log (*The trend reads*). The in-memory database has no file and counts as cleared. The decision that nothing is
  left to sweep is made under the same write gate: while a tombstone exists (a deletion arrived after the last sweep step)
  the clean-up does nothing, logs nothing and keeps its request, and the run the deletion asked for sweeps the rows
  first. Once the clean-up line is
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
| The batch reads of the trend column, 60 ticks at the bounds (256 islands, 512 entries) scaled to 240 points: one part of 240 island points | 90 to 150 ms |
| The whole opening read of the island list, 256 islands by 240 points (256 parts), warm and with the page cache dropped | about 12.8 to 13.4 s warm and 17 to 19.7 s cold |
| A refresh of the island list after one new tick (a times transaction and two parts: 256 islands by 1 point) | 100 to 120 ms |
| The opening read of a goods table (the goods of one island by 240 points) | 0.36 to 0.7 s |

The tick rows of the deletion's measurement over a year were written as rows of the tick table alone, since the first
transaction changes only the profile of those rows and of the hourly points; no figure here extends beyond those sizes.

The rows above were measured before the index of the tick entries by good (record 0087). A second scratch program measured
the steps again with and without it (the index dropped from a copy of the schema, the same ticks at the bounds, three
runs of four stores, three fold steps and the removal steps each), the trend reads and the migration, on the same machine:

| What | With the index | Without it |
| ---- | -------------- | ---------- |
| Store of a tick at the bounds | median 2.21 s (1.85 to 2.59, 12 stores) | median 2.07 s (1.98 to 2.30) |
| Fold step, tick at the bounds | median 3.92 s (3.75 to 4.24, 9 steps) | median 3.85 s (3.63 to 4.20) |
| Removal step, tick at the bounds | median 0.49 s (0.48 to 0.53, 3 steps) | median 0.42 s (0.41 to 0.44) |
| Removal step, hourly point at the bounds | 0.45 to 0.50 s | 0.46 to 0.50 s |

The index costs about 7 % of a store, 2 % of a fold step and 17 % of a tick's removal step at the bounds; the fold step stays
at about 4 seconds, the figure the gate's guarantee names. Its size is 9,928,704 bytes for the 524,288 entries of four ticks at
the bounds (18.9 bytes an entry; the file 100,585,472 bytes against 90,656,768 without it, 10.9 % more) and 61,493,248 bytes
for the about 3.22 million entries of 7 days of fixture ticks (19.1 bytes an entry), measured as the pages that dropping the
index freed.

| What | Result |
| ---- | ------ |
| The migration that builds the index, 7 days of fixture ticks (5,040 ticks, about 3.22 million entries) | 1.59, 1.67 and 1.58 s; the open of a database that has it 2 to 62 ms |
| The same, 60 ticks at the bounds (7.86 million entries) | 4.60, 3.73 and 3.69 s |
| Region read, 2,880 points of ticks at the bounds (256 islands), 60 ticks read in one part and scaled | 4.4, 3.8 and 3.5 s |
| Island read, same | 0.2 s (2.2 s for the first read of a process, which includes the JIT) |
| Figures read, same | 2.0, 1.3 and 1.3 s |
| The same three reads with the page cache dropped before a fresh process (`drop_caches`), scaled | region 73.5, 68.3 and 70.0 s; island 5.4, 3.4 and 2.9 s; figures 3.4, 3.6 and 4.5 s |
| Reads of 2,880 of 5,040 fixture ticks (24 islands in the region), no scaling | island 36 to 116 ms, region 167 to 199 ms, figures 99 to 116 ms; with the page cache dropped: 318 and 387 ms, 1.01 and 1.03 s, 207 and 168 ms |
| One part on the in-memory database: a read of 20 ticks at the bounds, scaled to a part of 240 | region 0.28 to 0.46 s, figures 0.14 to 0.29 s, island 0.04 s (0.6 s for the first read); the slowest store of a fixture tick queued meanwhile waited 4 to 38 ms, until the 20-tick read ended |

The reads are linear in their points (a part is at most 240 points), so the tick-at-the-bounds rows read 60 ticks and scale
by 2,880 / 60; 2,880 ticks at the bounds would be a file of about 63 GB, which the sandbox cannot hold. Where the index
pages are in the page cache (the way the rows above measure, with the file just written) each read is within the limit of
10 seconds; the region read at the bounds is the slowest. With the page cache dropped the region read at the bounds takes
1.42 to 1.53 s for the 15,360 index lookups of 60 ticks (256 islands each), which scales to more than 10 seconds; the same
read over the fixture's 24 islands takes 1 second. A part holds the in-memory database's write gate for its transaction only,
0.6 seconds at most in these rows, against about 3.9 seconds of a fold step.

The whole opening read of the island list at the bounds exceeds the 10 seconds a step of this section's limit: 256 islands by
240 points take about 12.8 to 13.4 s warm and 17 to 19.7 s with the page cache dropped, while each part stays far within the
limit (90 to 150 ms) and the figures of the page never wait for it, because the page shows them while the trends are read
([`web-charts.md`](web-charts.md), *Limits and errors*). The refresh after a
tick reads only the points after the held newest one and takes 100 to 120 ms.

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

- [`decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md`](../decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md) — a table's trends are one read for all its rows, in parts of at most 240 island points.
- [`decisions/0021-sqlite-history-with-retention.md`](../decisions/0021-sqlite-history-with-retention.md) — full resolution for 7 days, then hourly values kept without limit, deletion per profile.
- [`decisions/0031-bounded-local-tick-buffer-gaps-accepted.md`](../decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) — a gap stays a gap.
- [`decisions/0083-history-folded-tick-by-tick-into-hourly-sums-and-counts.md`](../decisions/0083-history-folded-tick-by-tick-into-hourly-sums-and-counts.md) — the fold, one tick per transaction, sums and counts, the latest hour and the kept identities.
- [`decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md`](../decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md) — a profile is gone at once for its readers, its rows are removed in steps, the file is cleared of them.
- [`decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md`](../decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md) — the deletion needs a page opened by a trusted host.
- [`decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md`](../decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md) — the chart reads aggregate per point, read the newest 2,880 points in parts under a trend gate and use an index of tick entries by good.
- [`decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md`](../decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md) — the wide ranges read one mean per hour from ticks and hourly points when asked.

## Not here

- The tables, the file, the start and the rules of storing: [`server-data.md`](server-data.md).
- The state a deletion drops and rebuilds: [`server-state.md`](server-state.md).
- The button, the dialog and the host rule: [`web-pages.md`](web-pages.md); the setting `AllowedHosts`: [`server.md`](server.md).
- The charts, their range, gaps and states, and the sparklines and trend arrows built from the batch reads: [`web-charts.md`](web-charts.md).
- Shrinking the database file and removing the residue of older versions: the user guide.
