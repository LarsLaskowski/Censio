# Server database

## Scope

The server's database: its file, the setting that names it and the in-memory case, what happens at start and the start
failures, what is stored and the rules of storing and reading it, file modes, privacy, one server per file and the
measurements. Operators who run the server, the data pages ([`web-pages.md`](web-pages.md)), the history that folds and deletes stored ticks ([`history.md`](history.md)) and live sending, which uploads the catalog ([`live-sending.md`](live-sending.md), *Catalog*), rely on it.
The ingest writes into the store ([`ingest.md`](ingest.md)): it stores a tick, a catalog or an icon before it
acknowledges it, and the server reads its state back from the store at start ([`server-state.md`](server-state.md)).

## Behavior

### File and setting

- The database is the file the setting `Censio:Database:File` (environment variable `CENSIO__DATABASE__FILE`) names, which
  must be an absolute path. Unset, it is `censio.db` in the data directory ([`server.md`](server.md)), in the image
  `/config/censio.db`. The setting is unset by default.
- Without the setting and without a data directory the database is kept in memory, written to no file and lost when the
  server stops; the server logs the warning
  `No database file without a data directory (Censio:DataDirectory) or Censio:Database:File; data is kept in memory and lost when the server stops.`
  and otherwise behaves as with a file.
- The setting takes a folder that does not exist yet: the server creates it. A value that is not an absolute path (the
  empty string included) stops the start (*Limits and errors*).
- One server process uses one database file. SQLite's locking is unreliable on network file systems, so the file belongs
  on a local disk.

### Start

The server opens the database after the key ring and the ingest token, before it listens. In this order, where every step
before the update only reads an existing file:

1. Without a file name, an in-memory database is opened and brought to the current schema; the kind is *in memory*.
2. A missing folder is created and a missing file is created empty, then SQLite opens it in read-write mode and never
   creates it itself. On Linux the folder that holds the file has mode `0700` when the server creates it (missing folders above it take the process's default mode) and the file `0600`; the files SQLite keeps
   beside it (`-wal`, `-shm`) take the file's mode. An existing file or folder keeps its mode. On Windows the file takes
   its folder's rules.
3. The file is classified. A file without any table of its own, or holding only EF Core's two bookkeeping tables
   (`__EFMigrationsLock`, `__EFMigrationsHistory`) with no applied schema version, counts as empty. A file with tables but
   without the history, or whose first recorded version is not the first version of the schema, is not a Censio database.
   A recorded version this build does not list means the file was written by a newer version. A file that SQLite reports
   as not a database or as corrupt is not a Censio database; any other failure reading it counts as not writable.
4. A write probe creates a table inside a transaction and rolls it back; if that fails, the file or its folder cannot be
   written (a read-only file, a read-only folder), also when every read would work.
5. Pending schema versions are updated. The server generates the commands of each; a version with a step that cannot run
   inside a transaction is refused before anything runs. Otherwise the script of all pending versions runs in one
   transaction of the server's own, which also records each version applied. A failure at any step rolls the whole
   update back, so the file keeps the schema and data it had, a new or empty file included: it holds no table of the
   schema afterwards. The server never takes EF Core's migration lock and neither reads nor clears its lock table, so a
   lock row an earlier tool left behind cannot block the start.
6. The journal mode is write-ahead logging (file only); synchronization stays at SQLite's default `FULL`, so a tick
   reported stored survives a power loss. Secure delete is switched on for the server's kept connection, a file and an
   in-memory database alike, so SQLite overwrites everything this version deletes ([`history-deletion.md`](history-deletion.md)); a failure
   to switch it on counts as not writable, as in step 4.
7. The result kind is *created* for a file this start created, *updated* when versions were applied to an existing file
   (an empty file included), *opened* otherwise.
8. After the database is open and before the server listens, the current state is restored from it
   ([`server-state.md`](server-state.md), *Restore at start*); a failure there stops the start with exit code 1.

A failed start releases what it opened. The database file is never deleted or replaced.

The server logs one line for the result, in English, with the path as the only variable:

| Kind | Level | Text |
| ---- | ----- | ---- |
| created | Information | `The database '<file>' was created.` |
| opened | Information | `The database '<file>' was opened.` |
| updated | Information | `The database '<file>' was updated to the current schema.` |
| in memory | Warning | the text above |

The server logs no profile or island name, no value of a tick, no catalog or icon content and no SQL parameter value. The
log level of `Microsoft.EntityFrameworkCore` is `Warning`.

### What is stored

The schema has these tables; the server's store is the only code that reads or writes them.

- **Profiles:** identity and name (the name may be missing).
- **Ticks:** identity (unique), profile, receive time as UTC ticks in an INTEGER column, game time, and a row number that
  records the storing order.
- **Islands:** per profile and island key (session GUID, island ID, area index), the latest name with the receive time and
  tick number it came from.
- **Tick islands, entries and map pairs:** the rows of a tick in stored positions; an entry's eight floats and three
  integers, and its workforce and building amounts as pairs. The entries are indexed by tick island and position, and by
  tick island, product GUID and position: the second index serves the trend reads of the charts, which find a good's
  entry on each island of a tick without scanning the island's entries ([`history.md`](history.md), *The trend reads*;
  record 0087). Its bytes and cost are measured in [`history.md`](history.md) (*Measurements*).
- **Trade route sets, trade routes and trade route stations:** a profile's latest trade-routes message ([`contract.md`](contract.md)),
  at most one set per profile. The set row holds the profile (its key), the identity and the receive time (UTC ticks) of the
  message; a route row the profile, its position, the route's ID, name, folder and four counts; a station row its route,
  its position and the island key (session GUID, island ID, area index). Positions keep the order of the message, so a set
  reads back as it was sent; route IDs and names may repeat. Route and folder names are private like island names.
- **Catalog:** one row, the catalog identity (the SHA-256 of the document's bytes, as the hello reply names it) and the
  document's bytes.
- **Icons:** the PNG bytes by reference key.
- **Hourly points, hourly islands, hourly entries and hourly map pairs:** the history folded from ticks, per profile and
  UTC hour, island and good ([`history.md`](history.md)).
- **Folded ticks:** the identity of every tick folded into an hourly point, with the number of that point.
- **Deleted profiles:** one random identity (a tombstone) per deletion; the ticks and hourly points of the deleted profile
  carry it as their profile until the steps of [`history-deletion.md`](history-deletion.md) have removed them.

A float is stored as its 32-bit pattern in an INTEGER column (`NULL` for no value, record 0067), so a value reads back
bit for bit, negative zero included, and a missing value never reads back as zero.

### Storing and reading

- Every write runs in one immediate transaction, serialized by one gate per open database, so a message is stored whole or
  not at all, also when the server stops during the write. A read from a file runs on its own connection in a deferred
  read transaction, sees the state before or after a write, and neither waits for it nor delays the next one. An
  in-memory database has no write-ahead log, so every read and every write takes the gate in turn and the same holds.
- **Trend gate.** Besides the write gate a database has a trend gate, one waiter at a time: every transaction of a trend
  read takes it first ([`history.md`](history.md), *The trend reads*), and so does the clean-up of a deleted profile, before the
  write gate and through the truncation of the write-ahead log. No store of a message and no read of the pages takes it.
- **Profile.** Storing a profile identity and name creates the profile or, for a known identity, keeps it and takes the
  new name. A tick of an identity the store does not hold yet is stored and creates that profile without a name; its next
  hello gives it the name. Listing returns every profile with identity and name.
- **Trade routes.** Storing a message first looks at the profile's stored set: one with the message's identity, or with a later
  receive time, is reported as already stored and nothing is written (an equal receive time with another identity
  replaces). Otherwise, in the one transaction, the profile is created without a name when unknown (as a tick does, so a
  message that arrives during a deletion's steps creates the profile again), its old set is deleted and the new one is
  written in order. Reading a profile's trade routes gives the set in position order, or nothing when none is stored.
  Nothing keeps an earlier set: the table holds the latest only.
- **Tick.** A tick whose identity is stored, as a tick or as a folded tick ([`history.md`](history.md)), is reported as
  already stored and changes nothing. Otherwise the tick, its
  islands, entries and pairs are written, and each island's row in the island table is updated when the tick's
  (receive time, storing order) is not older than the one stored. A tick reads back with every value of the message equal,
  the islands and entries in their stored positions and the maps ascending by GUID. An island a tick did not carry has no
  values in that tick: a gap, never zeros.
- **Profile state.** The store reads a profile's row, its latest tick, its island list and its latest trade routes in one
  deferred read transaction, so the four agree, and gives nothing for an unknown profile. It lists the profiles with ticks, the one
  whose latest tick is the newest first, by the order of *Latest tick*, at most as many as asked for. The server's state
  is built from these two reads ([`server-state.md`](server-state.md)).
- **Latest tick.** The latest tick of a profile is the one with the latest receive time and, among equal receive times, the
  one stored last; a tick stored later with an earlier receive time is not the latest, and another profile's ticks never
  count. The islands of a profile are listed by their key, each with the name of the tick with the latest receive time
  that carried it.
- **Deleting a profile.** One write transaction removes the profile's row, islands and trade routes (set, routes and stations) and gives its ticks and hourly
  points a fresh tombstone as their profile, so no read by profile returns them and a read of a tick by its identity
  gives none; an unknown profile changes nothing, and the catalog, the icons and the other profiles are untouched. Steps
  of their own then remove the rows ([`history-deletion.md`](history-deletion.md)).
- **Merging a profile.** One write transaction moves the source's islands, ticks, hourly points and trade routes to the target,
  combines what both hold and removes the source's row; no tombstone is made ([`history-deletion.md`](history-deletion.md), *Merging a profile*).
- **Catalog.** Storing a catalog replaces an earlier one; it reads back byte for byte with its identity.
- **Icons.** Storing an icon whose reference is stored changes nothing and is reported as already stored. The query for
  missing icons takes a list of references and returns those with no stored icon, each once, ordinally sorted. It passes
  the list as one JSON parameter to SQLite's `json_each`, never as SQL text and without chunking, so the number of
  references is not limited by SQLite's parameter limit (the contract allows 16,384 per message).
- A tick is bounded by the contract's limits (*Bounds* in [`contract.md`](contract.md): 256 islands, 524,288 entries and
  map pairs together, a message of at most 24 MiB); the store takes only what the contract reader accepted and adds no
  limit of its own.

## Limits and errors

Every start failure is one `Critical` log line and exit code 1 before the server listens; the file is left unchanged.

| Case | Text |
| ---- | ---- |
| Setting not an absolute path | `The database file (Censio:Database:File) must be an absolute path, not '<value>'; the server stops.` |
| File or folder cannot be created, opened or written | `The database '<file>' cannot be opened or written; the server stops. Make its folder writable for the user the server runs as.` |
| Not a SQLite database, damaged where the start reads it, or a SQLite database Censio did not write | `The file '<file>' is not a readable Censio database; the server stops.` |
| Written by a newer version | `The database '<file>' was written by a newer Censio version; the server stops. Start that version or a newer one.` |
| Updating the schema fails | `The database '<file>' cannot be updated to the current schema; the server stops.` |

The exception behind the second, third and last line is attached to the entry for the operator. The server makes no backup
before an update.

- A write that fails while the server runs (disk full, file removed) stores nothing of that message and is reported to the
  caller as a failure with the exception; the ingest then does not acknowledge the message and closes the connection
  with 1011 ([`ingest.md`](ingest.md)). Damage beyond the header surfaces this way, not at start.
- A read that fails surfaces as the provider's exception; the store does not wrap it.
- A restored backup must be readable by the server's user only (on Linux `chmod 600 censio.db`): a restored copy keeps
  the mode the copy had, and the files SQLite keeps beside it take the file's mode.

### Measurements

Measured on a 4-vCPU Linux sandbox, ext4, SQLite with `synchronous=FULL` and write-ahead logging, one store call per
tick, Release build, by a scratch program outside the repository that replays the fixture `long-session.jsonl`
(`tests/Censio.Pipe.Tests/Fixtures`) through `PipeTickAssembler` and `ContractMessageConverter.ToTick` and stores each
tick with `ServerStore.StoreTickAsync`. The figures are an order of magnitude, not a guarantee.

| Tick | Store time | Read-back time | File growth |
| ---- | ---------- | -------------- | ----------- |
| Fixture, 33 ticks of 21,091 entries in total, at most 682 entries per tick | median 10 to 25 ms, minimum about 1 ms, maximum 22 to 64 ms (three runs; the first is the coldest) | latest tick 1 to 12 ms | 77,079 bytes per tick (2,543,616 bytes for 33 ticks above the empty file of 81,920 bytes) |
| At the bounds: 256 islands of 512 entries, 131,072 entries and 393,216 pairs (524,288 together) | 2.6 to 2.9 s | 1.0 to 1.3 s | 22.1 MiB |

A stored tick takes about 77 KB for a real tick, which at one tick every two minutes is about 55 MB a day, and 22.1 MiB at
the bounds; the retention job folds ticks 7 days old or older into hourly points ([`history.md`](history.md)). The space that folding and deletion free is reused by new data; the file never shrinks.

## Related decisions

- [`decisions/0021-sqlite-history-with-retention.md`](../decisions/0021-sqlite-history-with-retention.md) — one SQLite file in the data directory; the retention rule is in [`history.md`](history.md).
- [`decisions/0065-database-schema-kept-by-migrations-applied-at-start.md`](../decisions/0065-database-schema-kept-by-migrations-applied-at-start.md) — migrations applied at start in one transaction; a foreign, newer or failed database stops the start.
- [`decisions/0066-database-file-setting-and-in-memory-without-data-directory.md`](../decisions/0066-database-file-setting-and-in-memory-without-data-directory.md) — the file setting, and an in-memory database with a warning without a data directory.
- [`decisions/0067-ticks-stored-as-rows-with-float-bit-patterns.md`](../decisions/0067-ticks-stored-as-rows-with-float-bit-patterns.md) — ticks as rows written by prepared commands, floats as 32-bit patterns.
- [`decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md`](../decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md) — secure delete on the server's connection, a deleted profile's rows behind a tombstone.
- [`decisions/0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md`](../decisions/0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md) — the latest trade routes per profile as rows, replaced whole.

## Not here

- The data directory and the Docker image: [`server.md`](server.md).
- What the ingest accepts and answers and when it stores: [`ingest.md`](ingest.md); the state kept from the store: [`server-state.md`](server-state.md).
- The contract's messages and bounds: [`contract.md`](contract.md); the catalog document: [`game-catalog-format.md`](game-catalog-format.md).
- Retention, hourly averages, the series read and the trend reads: [`history.md`](history.md); deleting a profile's history: [`history-deletion.md`](history-deletion.md); the pages that show the data: [`web-pages.md`](web-pages.md); the charts: [`web-charts.md`](web-charts.md).
