# Spec: SQLite persistence for the server

Source: Issue #29
Status: Approved by Lead

## Problem

The server reads every message of the Companion and keeps none of it ([`ingest.md`](../../docs/areas/ingest.md),
*Messages after the hello*): a restart loses everything, and #30 (state per profile, restore after restart), #35
(history and retention), #32 (pages) and #42 (catalog and icons) have nowhere to put their data. Record
[0021](../../docs/decisions/0021-sqlite-history-with-retention.md) chose one SQLite file in the data directory, so the
operator runs no database server and backs the data up with the directory. This feature creates that database, keeps
its schema current across versions, and gives the server one place to store and read profiles, islands, ticks, the
catalog and icons. Writing what the ingest receives, showing it and the retention are the later issues' work.

## Scenarios

- S1: Given the Docker image with an empty `/config`, when the server starts, then it creates `/config/censio.db` with
  the current schema, logs `The database '/config/censio.db' was created.` and then listens.
- S2: Given a database a previous start created, when the server starts again (also in a recreated container that keeps
  `/config`), then it opens that file, keeps everything stored in it and logs `The database '<file>' was opened.`
- S3: Given a database of an earlier schema (written by an earlier Censio version, or an empty file), when the server
  starts, then it brings the database to the current schema, keeps the data already stored and logs
  `The database '<file>' was updated to the current schema.`
- S4: Given the setting `Censio:Database:File` (`CENSIO__DATABASE__FILE`) with an absolute path, when the server
  starts, then the database is that file instead of `<data directory>/censio.db`, its folder is created when missing,
  and no data directory is needed for it (#39; a NAS operator keeps it on a local disk).
- S5: Given neither `Censio:DataDirectory` nor `Censio:Database:File` (a plain `dotnet run`), when the server starts,
  then it logs the warning
  `No database file without a data directory (Censio:DataDirectory) or Censio:Database:File; data is kept in memory and lost when the server stops.`,
  keeps its database in memory, writes no database file and otherwise behaves as with a file.
- S6: Given a profile identity and name (from a hello, record
  [0041](../../docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)), when the server
  stores it, then an unknown identity becomes a new profile and a known one keeps its identity and takes the new name;
  the server lists the stored profiles with identity and name. A profile first known from a tick (E9) is listed with
  no name until a hello names it.
- S7: Given an accepted tick, when the server stores it, then a later read returns every value it carried ([`contract.md`](../../docs/areas/contract.md), *Wire form*): identity, profile, receive time, game time, and
  per island its key, session ID and name with every entry's values and maps. The islands of a profile are listed by
  their key (session GUID, island ID, area index, [`pipe.md`](../../docs/areas/pipe.md), *Island key*), each with the name
  of the tick with the latest receive time that carried it.
- S8: Given stored ticks of two profiles, when the server asks for the latest tick of one profile, then it gets that
  profile's tick with the latest receive time, never one of the other profile; of ticks with equal receive times, the
  one stored last. A tick stored later with an earlier receive time (a clock set back) is not the latest.
- S9: Given an accepted catalog upload, when the server stores it, then it reads back the document's bytes and its
  catalog identity (the SHA-256 of those bytes, as the hello reply names it); a later catalog replaces it. Given icon
  images, it stores each PNG under its reference and, for a list of references, answers which ones it does not hold.

## Errors and edge cases

Every start failure is one `Critical` log line, exit code 1, before the server listens, after the key ring and the
ingest token checks ([`server.md`](../../docs/areas/server.md), [`ingest.md`](../../docs/areas/ingest.md), *Start
failures*); the database file is neither deleted nor replaced.

- E1: `Censio:Database:File` not an absolute path, the empty string included:
  `The database file (Censio:Database:File) must be an absolute path, not '<value>'; the server stops.`
- E2: The file or its folder cannot be created, opened or written (no permission, read-only mount):
  `The database '<file>' cannot be opened or written; the server stops. Make its folder writable for the user the server runs as.`
- E3: The file is not a SQLite database, is damaged where the start reads it, or is a SQLite database Censio did not write (it holds tables but
  no record of Censio's schema versions); the file is left unchanged:
  `The file '<file>' is not a readable Censio database; the server stops.`
- E4: The database holds a schema newer than this version knows (an older image started on a newer database):
  `The database '<file>' was written by a newer Censio version; the server stops. Start that version or a newer one.`
  The file is left unchanged.
- E5: Bringing the database to the current schema fails, in any of the steps from its schema to the current one:
  `The database '<file>' cannot be updated to the current schema; the server stops.` The database keeps the schema
  and data it had before the start, also when earlier steps of the same update had succeeded; a failed first update of
  a new or empty file may leave two empty bookkeeping tables.
- E6: A tick whose identity is already stored (a resend, record
  [0059](../../docs/decisions/0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md)) is
  reported as already stored and changes nothing; the same holds for an icon whose reference is stored.
- E7: A float the message carried as `null` reads back as `null`, never as zero; negative zero reads back negative; an
  island a tick did not carry has no values in that tick (a gap, never zeros, record
  [0031](../../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md)).
- E8: A write that fails while the server runs (disk full, file removed) stores nothing of that message and is reported
  to the caller as a failure; what the ingest then answers is #30's.
- E9: A tick of a profile identity the store does not hold yet (buffered under profile A, delivered after a hello for
  B, record 0059) is stored, and the profile is created without a name; that profile's next hello gives it its name.

## Non-functional requirements

- N1: New configuration: `Censio:Database:File` (`CENSIO__DATABASE__FILE`), unset by default; unset, the database is
  `<data directory>/censio.db` (`/config/censio.db` in the image, which needs no new `ENV` line).
- N2: Privacy: profile and island names are private ([`contract.md`](../../docs/areas/contract.md), *What never
  crosses*); no log line of this feature holds a name, a value of a tick, a catalog or an icon, only the database path.
  The server stores no figure name (record 0041). On Linux the server creates the database file, the files SQLite keeps
  beside it while it runs, and a folder it creates, readable by their owner only.
- N3: A tick is stored whole or not at all, also when the server stops during the write; a read during a write sees the
  state before or after it.
- N4: A tick at the contract's bounds (256 islands, 524,288 entries and map pairs, [`contract.md`](../../docs/areas/contract.md),
  *Bounds*) is stored and read back; the time to store such a tick and a tick of an anonymised fixture, and the file's
  growth per fixture tick, are measured and stated in the area document.
- N5: The data layer is tested against real SQLite (the issue's criterion), never a hand-written stand-in for the
  database; tests use no real recording beyond the anonymised fixtures (record 0029).
- N6: One server process per database file (record [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md)).
  SQLite's locking is unreliable on network file systems: the documentation advises a local disk.

## Acceptance criteria

- AC1: A start with a data directory and no database file creates `censio.db` there with the current schema and logs
  the line of S1 (S1).
- AC2: A start on a database at the current schema keeps all stored data and logs the line of S2; a start on a
  database at an earlier schema version holding data (from a test-only schema history: this feature ships the first
  schema), and on an empty file, brings it to the current schema with its data kept, records each version applied, and
  logs the line of S3 (S2, S3).
- AC3: With `Censio:Database:File` set to an absolute path the server uses that file, creates its missing folder and
  needs no data directory; a value that is not absolute stops the start as E1 (S4, E1, N1).
- AC4: Without a data directory and without the setting the server logs the warning of S5, starts, writes no database
  file, and stores and reads as with a file until it stops, N3 included but for a stop during a write (S5, N3).
- AC5: Each failure of E2 to E5 stops the start with its line, exit code 1 and before listening, E3 also for a foreign
  SQLite file, E4 for a recorded schema version unknown to the build, E5 for a two-step update whose second step fails;
  after E3 to E5 the file's schema and data are those of before the start, but for E5's bookkeeping tables (E2, E3,
  E4, E5).
- AC6: On Linux the database file, its side files and a folder the server creates are owner-only, and no log line of
  this feature holds a profile or island name or a stored value (N2).
- AC7: A stored profile is listed with identity and name; storing a known identity with another name keeps one profile
  with the new name; a profile created by a tick is listed without a name and takes the name of its next hello (S6, E9).
- AC8: A stored tick, read back from the database file through a new connection, has every value of the message
  equal, floats bit for bit with `null` and negative zero kept; the profile's islands are listed by key with the latest
  name; a tick of a profile identity not yet stored is stored and creates that profile without a name; a known tick
  identity changes nothing and is reported as already stored; AC7 to AC11 run against real SQLite (S7, E6, E7, E9, N5).
- AC9: The latest tick of a profile is its tick with the latest receive time, also when ticks of another profile were
  stored later; of two ticks with equal receive times it is the one stored last, and a tick stored later with an
  earlier receive time is not the latest (S8).
- AC10: A stored catalog reads back byte for byte with its identity and is replaced by a later one; a stored icon reads
  back by its reference, storing it again changes nothing, and the missing-references query returns exactly the
  references not stored (S9, E6).
- AC11: A write that fails stores nothing of its message and returns a failure, and a tick at the contract's bounds is
  stored and read back (E8, N3, N4).
- AC12: `README.md` (configuration table) and `docs/USER_GUIDE.md` (*Data directory and NAS hosts*, *First start*, *Data
  and logs*), English then German, name the database file and the setting, the start failures, the local-disk advice,
  a backup by copying the file while the server is stopped, and that nothing received is stored yet until #30; the
  measurements of N4 stand in the area document (N4, N6).

## Out of scope

- Writing ingested messages into the database, the in-memory live state and its restore (#30); pages (#32); retention,
  hourly averages and deleting a profile's history (#35); sending the catalog (#42).
- Session events: the issue lists no entity for them; #30 decides whether it needs one.
- Pruning icons no catalog refers to, and keeping older catalogs.
- A backup kept for the operator after a schema update: no schema is released yet; E5 covers a failed update.
- A database server other than SQLite (record 0021).
- Settings: the issue lists an entity for them, but no setting is stored in the database yet; the first feature that
  stores one adds it with its own migration (Q1).

## Open questions

- Q1: The issue lists an entity for **settings**; no issue, record or code says which settings the server keeps in its
  database (operator settings come from configuration, the theme is kept per browser, #31). Answered: left out of #29
  (Product Manager); the first feature that stores a setting in the database adds it with its own migration (*Out of
  scope*).
- Q2: Where is the database without a data directory? Answered: in memory with a warning (Lead decision) (S5), as the ingest
  token warns and degrades without one ([`ingest.md`](../../docs/areas/ingest.md), *Token*); the image always sets
  `/config`, and local mode (#39) sets a path.
- Q3: Is the catalog kept per profile (#30 says "catalog per profile")? Answered: no, per server: a catalog upload
  carries no profile and the hello reply names one catalog identity ([`contract.md`](../../docs/areas/contract.md)).
- Q4: Does the database path need its own setting? Answered: yes; the issue says "configurable", and a NAS share is no
  safe place for SQLite (N6).
- Q5: How do failures at start behave? Answered: they stop the start (Lead decision), as an unwritable key ring does
  (record 0011).

## Area documents

- New area `server-data` (`docs/areas/server-data.md`, with its index row): where the database lies and the setting,
  what happens at start (created, opened, updated, in memory), the start failures with their lines, what is stored (profiles,
  islands, ticks, catalog, icons) and the rules of E6 to E9, the latest-tick rule of S8, how N3 holds in memory, file
  modes and privacy, one server per file and the local disk, the measurements of N4.
- `server.md`: the data directory also holds `censio.db`; the image's `/config/censio.db`; link to `server-data`.
- `ingest.md` stays as is until #30.

## Challenge

- 1 (major, unknown profile refused): accepted; confirmed by `contract.md` (the hello's one `profileId`) and record
  0059 (option 4). E9 stores the tick and creates the profile without a name; S6, AC7 and AC8 follow.
- 2 (major, AC2 passes a create-only build): accepted. AC2 starts on an earlier schema version holding data and checks
  the recorded versions; AC5 builds E4 from an unknown recorded version and E5 from a failing second step. How the
  test-only history is made is the plan's.
- 3 (minor, negative zero): accepted; AC8 reads back through a new connection.
- 4 (minor, foreign SQLite file): accepted; E3 and AC5 cover it.
- 5 (minor, tie rule): accepted. S8 and AC9: on equal receive times the tick stored last; a clock set back does not
  make a tick the latest.
- 6 (minor, in-memory vs N3): accepted. AC4 keeps N3 except the stop during a write; the area document says how.
- 7 (minor, atomic update): accepted. E5 covers earlier successful steps, AC5 tests it; transaction or updated copy is
  the plan's, verified for the EF Core version added; *Out of scope* excludes only a backup kept for the operator.
