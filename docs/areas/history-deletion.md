# History deletion

## Scope

Deleting a profile's history and merging it into another profile's: what is removed, the tombstone, the removal steps,
secure delete and the truncating checkpoint, the residue of older versions, what a merge moves and combines, the log
lines, the failures and the measurements of both. Operators who run
the server rely on it. The retention, the fold and the reads of the history are in [`history.md`](history.md).

## Behavior

### Deleting a profile

- Deleting a profile's history removes the profile completely: its row, its islands, its trade routes, its ticks and its hourly points.
  The catalog, the icons and the other profiles stay. A profile that is not stored changes nothing.
- One write transaction removes the profile's row, its islands and its trade routes, creates a fresh random identity (a tombstone) and sets
  it as the profile of the profile's ticks and hourly points, one row per tick or hour. From then on no read by profile
  and no read of a tick by its identity returns them, and the server's state drops the profile
  ([`server-state.md`](server-state.md)). The tombstone is no profile and is carried by no profile's row; a row number
  would not serve as a mark, because SQLite reuses the number of a deleted row.
- Steps of their own then remove the tombstoned rows, each in one write transaction: one tick with its islands, entries
  and pairs, else one hourly point with its islands, entries, pairs and the identities of its folded ticks, else the
  tombstone once nothing is left. A tick or a trade-routes message of the deleted profile that arrives during the steps creates
  the profile again, unnamed until its next hello, with rows under its own identity that the steps leave alone.
- **Clean-up.** The server's connection runs with secure delete, so SQLite overwrites everything this version deletes
  (retention included), and after the steps the write-ahead log is checkpointed and truncated, under the write gate. A
  reader that holds an older snapshot blocks the truncation: the clean-up then logs the warning below and the next run
  tries again until it succeeds. A chart read's part never overlaps the truncation: the clean-up waits for the part in
  progress, takes the trend gate before the write gate and holds it through the checkpoint, so no part starts during it
  and no chart read can keep a deleted profile's names in the write-ahead log ([`history.md`](history.md), *The trend reads*). The in-memory database has no file and counts as cleared. The decision that nothing is
  left to sweep is made under the same write gate: while a tombstone exists (a deletion arrived after the last sweep step)
  the clean-up does nothing, logs nothing and keeps its request, and the run the deletion asked for sweeps the rows
  first. Once the clean-up line is
  logged, none of the names that only the deleted profile held (route and folder names included) occurs in the database file or in its `-wal` and `-shm` files; a test with the real schema checks that.
- **Residue of older versions.** A version before this one deleted nothing and left copies of changed names in the free
  space of the file; those stay until the operator runs `VACUUM` with the server stopped ([`USER_GUIDE.md`](../USER_GUIDE.md)).
  The file never shrinks by retention or deletion; the space is reused by new data.

### Merging a profile

- The message `profileMerge` of a Companion with the token ([`contract.md`](contract.md), [`ingest.md`](ingest.md)) moves
  the whole history of the source profile into the target identity and removes the source. The target keeps its name and
  identity; the pages offer no merge. Nothing is read into memory whole: every step is a statement of the database.
  One write transaction does all of it, under the write gate, so a failure changes nothing:
  1. A source that is not stored changes nothing and is acknowledged. A target that is not stored gets a profile row
     without a name, listed as `Unnamed profile` until a hello names it.
  2. The source's islands are upserted into the target's by key (session GUID, island ID, area index); the name and the
     stamp (receive time, then tick number) are taken from the source when its stamp is not older. The source's islands are
     then deleted.
  3. The source's ticks get the target as profile; their numbers, so the storing order, stay.
  4. The source's hourly points of an hour the target lacks get the target as profile. For an hour both hold, the tick
     count is added and the game-time range becomes the union. Below the kept point an island of both is one island, named
     by the later stamp; an entry of the same good under it adds every count and sum, its entry count included; a pair of the
     same kind and GUID adds its amount sum; what the kept point lacks moves over, and the folded tick identities of the
     source's point move to the kept point, so a resent folded tick stays recognised. The source's remaining rows of the
     point are deleted. The pairs of point numbers of the shared hours are kept in a temporary table of the transaction, two numbers per hour; no row is read into the server's memory.
  5. Trade routes: the source's set replaces the target's when the target has none or the source's receive time is later;
     otherwise (equal receive times included) the target's stays and the source's is deleted.
  6. The source's profile row is deleted.
- The ticks that moved fold by the target's latest tick at the next fold run: the one hour of the target's latest tick stays
  unfolded, and the source's latest hour no longer does ([`history.md`](history.md)). A trend read split by the merge may
  draw the hour partly from the merged history, counts nothing twice and draws it whole at the next read.
- The server's state reads the profiles and held states again and tells the followers ([`server-state.md`](server-state.md),
  *Merging profiles*). A merge asks for no sweep: nothing is deleted that must be cleared from the file. The removed profile
  row and trade-route set are overwritten by secure delete only; the clean-up guarantee of a deletion (*Deleting a profile*)
  is not given for a merge, which keeps the history.
- A merge that fails changes nothing; the connection is closed with 1011 and the Companion sends the merge again. Past the
  Companion's 30 seconds of waiting for the acknowledgement the server still ends the merge whole under its stopping token;
  the ingest of every Companion waits behind the write gate meanwhile, the unanswered messages are sent again with their
  identities (stored once), and the sent-again merge finds the source gone and is acknowledged.

### Log lines

The category and what no line holds are those of [`history.md`](history.md), *Log lines*.

| Level | Text | When |
| ----- | ---- | ---- |
| Information | `The history of a profile was deleted.` | the rows of a deletion are gone |
| Warning | `The history of a profile could not be deleted.` with the exception | a deletion failed before its rows were gone |
| Information | `The history of a profile was merged into another.` | the history of a profile was moved |
| Warning | `The history of a profile could not be merged into another.` with the exception | a merge failed and changed nothing |
| Information | `The deleted history was cleared from the database file.` | the clean-up finished |
| Warning | `The deleted history could not yet be cleared from the database file; the next retention run tries again.` (with the exception unless a read blocked it) | a removal step or the truncation failed, or a read blocks it |

## Limits and errors

- A deletion that fails before its rows are gone stores no change: the profile stays whole and the dialog of
  [`web-pages.md`](web-pages.md) shows its failure text. A deletion counts as done once its rows are gone, also when the
  clean-up has not finished.
- The deletion is reachable without a login, from a page opened by a trusted host only ([`web-pages.md`](web-pages.md),
  record 0085).
- A second entry point is the `/ingest` connection: a Companion with the token sends the message `profileDeletion` with the
  profile's identity ([`contract.md`](contract.md), [`ingest.md`](ingest.md), *Messages after the hello*), and the server
  deletes as above. The host rule of the pages does not apply to it: the token (or, with the token off, nothing but the
  refusal of browser upgrades) guards it, and a token holder can delete any profile by its identity (record 0116) and
  merge any stored profile into any identity (record 0119).

### Measurements

The sandbox, the fixture and the limit are those of [`history.md`](history.md), *Measurements*; these rows were measured
before the index of the tick entries by good (record 0087).

| What | Result |
| ---- | ------ |
| Removal step, tick at the bounds | 0.41 to 0.45 s |
| Removal step, hourly point at the bounds | 0.46 to 0.64 s |
| Removal step, hourly point of the fixture (682 entries, about 1,250 pairs) | median 3.2 ms, maximum 12.6 ms (200 steps) |
| A store queued 33 ms into a removal step of the tick at the bounds | acknowledged 8 to 10 ms after the step ended (0.41 to 0.43 s after the step began), in all three runs |
| The deletion's first transaction, 7 days of fixture ticks (5,040 ticks) | 12, 11 and 17 ms |
| The deletion's first transaction, a year of hourly points (8,684, built from the 167 real hours of the 7-day run) and 20,190 unfolded ticks | 1,133, 1,130 and 1,134 ms |
| By a throwaway test outside the repository through the real `ServerStore`: the deletion's first transaction for a profile with a full set of trade routes at the contract's bounds (53,453 rows) and no ticks | 94, 46 and 58 ms |

Merges, through the real `ServerStore` over a database file, three runs each (the merge's own measurement, after the index
of the tick entries by good):

| What | Result |
| ---- | ------ |
| (a) A source with a year of hourly points (8,684) and 20,190 unfolded ticks, into a target holding none of those hours | 1.66 to 1.90 s |
| (b) Two profiles each with 7 days of fixture ticks, folded, 168 shared hours | 1.42 to 1.50 s |
| (c) Two profiles holding the same year of hourly points built from the fixture hours, every hour shared | 54.2 to 54.8 s |
| (d) Both profiles with a full set of trade routes at the contract's bounds, the source's later | 55 to 68 ms |

A shared hour costs about 6.3 ms, so a merge takes at most 3 s up to about 450 shared hours. One Companion writes one profile
at a time, so two profiles share only the hours in which the player switched, far fewer than that. Beyond it the time grows
with the shared hours (a whole year shared: case (c), about 55 s); past about 4,700 shared hours the Companion's 30 seconds
end first (*Merging a profile*, last point).

The tick rows of the deletion's measurement over a year were written as rows of the tick table alone, since the first
transaction changes only the profile of those rows and of the hourly points; no figure here extends beyond those sizes.

## Related decisions

- [`decisions/0021-sqlite-history-with-retention.md`](../decisions/0021-sqlite-history-with-retention.md) — full resolution for 7 days, then hourly values kept without limit, deletion per profile.
- [`decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md`](../decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md) — a profile is gone at once for its readers, its rows are removed in steps, the file is cleared of them.
- [`decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md`](../decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md) — the deletion needs a page opened by a trusted host.
- [`decisions/0119-history-merged-through-the-ingest-connection-in-one-transaction.md`](../decisions/0119-history-merged-through-the-ingest-connection-in-one-transaction.md) — a history is merged through the ingest connection in one transaction.

## Not here

- The retention job that runs the steps and the clean-up, and the trend gate: [`history.md`](history.md).
- The tables and the tombstone column: [`server-data.md`](server-data.md).
- The state a deletion drops: [`server-state.md`](server-state.md).
- The button, the dialog and the host rule: [`web-pages.md`](web-pages.md); the setting `AllowedHosts`: [`server.md`](server.md).
- Shrinking the database file and removing the residue of older versions: the user guide.
