# Spec: Current state per profile and restore after restart

Source: Issue #30
Status: Draft

## Problem

The server acknowledges a Companion's ticks, catalog and icons but keeps none of them
([`ingest.md`](../../docs/areas/ingest.md)); the store of #29 is never written. A closed game or a restarted container
loses what the pages of #32 are to show. Profiles (records 0020, 0041) must not mix, and pages need a profile's latest
state without a database read per update.

## Scenarios

- S1: Given a connected Companion, when it sends a tick of profile A, then the server stores it, then acknowledges it
  (`refusal` `null`), and A's current state is that tick, its islands and their names; whoever follows the state
  (the pages of #32) is told that A changed.
- S2: Given profiles A and B with ticks, when a tick of B arrives, then B's current state changes and A's does not, and
  the islands of B are not listed for A.
- S3: Given a server with stored ticks of A and B and a stored catalog with all its icons, when the server (or its
  container) is stopped and started again with the same database file, then before it listens the current state of A
  and B is the one before the stop, the log holds `Profiles restored from the database: 2.`, and the next hello is
  answered with the stored catalog's identity.
- S4: Given a hello with profile identity P and name "Rome", when it is accepted, then P is stored with that name; a
  later hello with P and "Roma" renames it. A tick of an identity no hello named creates a profile without a name, which
  the next hello with that identity names.
- S5: Given a hello answered with `catalogIdentity` `null`, when the Companion uploads a catalog and then its icon
  images, then the catalog's acknowledgement lists in `missingIcons` the references with no stored icon, each icon is
  stored, followers are told that the catalog or its icons changed, and once every icon the catalog refers to is
  stored the next hello is answered with the catalog's identity. A later upload replaces the catalog for every profile.

## Errors and edge cases

- E1: A resend after a lost acknowledgement is acknowledged with `refusal` `null`, and the current state does not
  change and no one is told of a change. A tick is a resend when its `id` is stored; an icon image when its `reference`
  is stored; a catalog upload when its catalog identity (the SHA-256 of its document) equals the stored one. A resent
  catalog's acknowledgement lists in `missingIcons` the references still without a stored icon.
- E2: A tick of A that arrives after a newer one (a buffered tick resent after a reconnect) is stored, but A's current
  state stays the newer tick. Of two ticks with equal receive times the one stored later is the current one (the store's
  *Latest tick* rule).
- E3: Storing a message fails (disk full, file removed): it gets no acknowledgement, the current state does not change,
  the connection is closed with status 1011 and the server logs the error
  `A message could not be stored; the connection is closed.` with the exception; the message counts as refused in the
  disconnect line. The Companion reconnects and resends it. A hello whose profile cannot be stored gets no reply, is
  closed the same way and logs only that error line: neither `Companion connected.` nor a disconnect line, and the
  Companion's place is free again.
- E4: The stored state cannot be read at start: the server stops before it listens with exit code 1 and the `Critical`
  line `The current state cannot be read from the database '<file>'; the server stops.`
- E5: An in-memory database ([`server-data.md`](../../docs/areas/server-data.md)) restarts empty and logs
  `Profiles restored from the database: 0.`
- E6: The server stops while a tick is being stored: the tick is stored whole or not at all and is acknowledged only
  when stored, so after the restart it is either the current state or resent by the Companion.
- E7: A tick whose islands all have no entries (*no data yet*, sent after a load) is a tick like any other: it becomes
  the current state when it is the latest.
- E8: A tick that carries only some of the profile's islands (a tick split by a stall, [`pipe.md`](../../docs/areas/pipe.md))
  becomes the current state; the islands it lacks keep their listing and name but have no values in it, never zeros.
- E9: A session start, session end or `status` message is answered as today and changes no stored data and no
  current state.
- E10: A follower of the state that throws, or blocks, delays neither the acknowledgement nor the other followers.
- E11: The catalog is stored but some of its icons are not (the connection or the server ended between the upload and
  the icons): `helloAccepted` answers `catalogIdentity` `null`, so the Companion uploads the catalog again and its
  acknowledgement names the missing icons (E1).
- E12: An older catalog uploaded after a newer one replaces it: the server keeps the last upload, whatever its age.
- E13: Reading a profile's state that is not held in memory fails: the reader gets a failure, not an empty or a partial
  state; the server logs the error `The current state of a profile cannot be read from the database.` with the
  exception, and the next request reads again.
- E14: The Companion's connection ends while its message is being stored (up to about 3 s at the bounds, N3): its place
  is freed only when the store ends, so a hello in that time is refused `companionConnected`; the Companion reconnects
  as after any refusal. Accepted.

## Non-functional requirements

- N1: Reading a profile's current state needs no database read while the profile is held in memory (N2). A reader sees
  a profile's state wholly before or wholly after a change, never part of a tick; a state read from the database (at
  restore or on demand) reads the latest tick and the island list in one read transaction.
- N2: The memory the current state holds is bounded whatever the number of stored profiles: the latest ticks of at most
  a fixed number of profiles are held, those whose latest tick is the newest by the store's *Latest tick* order
  (receive time, then storing order); a hello that only names or renames a profile does not move it in that order.
  Restore loads the same profiles. Another profile's state is read from the database when asked for. The list of
  profiles with their names is held whole. The number, and the memory it means at the contract's tick bound, are fixed
  in the plan and stated in the area document.
- N3: Storing a tick at the contract's bounds takes seconds (2.6 to 2.9 s measured, `server-data.md`); the session
  stays within the keep-alive of [`ingest.md`](../../docs/areas/ingest.md), so a Companion is not aborted while the
  server stores its message.
- N4: Privacy: no new log line holds a profile or island name, a figure name, a value of a tick, catalog or icon
  content; the start line holds only the count, the failure lines only the path. The server stores no figure name
  (record 0041).
- N5: Compatibility: contract version 1 and the hello are unchanged; a database written by #29 is read as it is,
  without a schema change for the state.

## Acceptance criteria

- AC1: After a stop and a start with the same database file, every profile's current state equals the one before the
  stop: its name, its latest tick with every value bit for bit, and its islands with their names; the start logs
  `Profiles restored from the database: <n>.` with the number of stored profiles and nothing else of the data (S3, E5,
  E6, N4, N5).
- AC2: A tick of one profile changes neither the current state nor the island list of another, before and after a
  restart (S2).
- AC3: A tick, catalog upload or icon image the reader accepts is acknowledged with `refusal` `null` only after it is
  stored; a resend by the identity of E1 is acknowledged the same way and changes no state and tells no follower;
  session events and `status` change no stored data and no state (S1, E1, E9).
- AC4: A profile's current state is its latest tick by the store's rule: a later-arriving older tick leaves it
  unchanged, and of equal receive times the one stored later wins; a tick without entries or with only some islands
  becomes the state like any other (E2, E7, E8).
- AC5: An accepted hello stores its profile identity with its name (created, or renamed when known) before
  `helloAccepted`; a tick of an unknown identity creates the profile without a name (S4).
- AC6: A stored catalog upload replaces the earlier catalog for every profile, the last upload winning; `helloAccepted`
  names the stored catalog's identity only when every icon reference of it is stored, else `null`; a catalog's
  acknowledgement, a resent one included, lists in `missingIcons` exactly the references without a stored icon (S5,
  E1, E11, E12).
- AC7: A message whose storing fails gets no acknowledgement, leaves the state unchanged, closes the connection with
  1011 and logs the line of E3, which holds no content of the message; a hello whose profile cannot be stored gets no
  reply, closes the same way, logs neither `Companion connected.` nor a disconnect line and frees the place (E3, N4).
- AC8: After each stored change the followers are told which profile changed, or that the catalog or its icons changed;
  a reader never sees a partial state; a follower that throws or blocks delays neither the acknowledgement nor another
  follower (S1, S5, E10, N1).
- AC9: The state is restored before the server listens; a failure reading it stops the start with exit code 1 and the
  `Critical` line of E4 (S3, E4).
- AC10: With more stored profiles than the number of N2, the held profiles are those of N2's order, at restore and
  after each tick; the state of every profile, held or not, equals its stored latest tick and islands, read in one
  transaction; a failed read gives the failure and the log line of E13 (N1, N2, E13).

## Out of scope

- Pages, the profile switch on a page and the banner with the "as of" time and the connection status: #31 and #32;
  the Companion's connection state and `pipeState` are not part of the state here (#32).
- Storing session events, time series, retention and deleting a profile: #35.
- Sending ticks and the catalog from the Companion: #40, #42; the Companion's buffer and resend: #41.
- Selecting and creating profiles and the figure-name notice in the Companion: #43.
- N3's timing is not tested by a unit test; it rests on the measurement of `server-data.md`. E14 is accepted unchanged.

## Open questions

- Does the issue's "catalog per profile" mean one catalog for each profile?
  Answered: no. A catalog upload carries no profile ([`contract.md`](../../docs/areas/contract.md)) and the store keeps
  one catalog; it belongs to the game build. Ticks are per profile, the catalog per server.
- What does "shown unchanged" show before #32's pages exist?
  Answered: the current state the pages will read (AC1), and the start line.
- What decides a tick's profile?
  Answered: records 0041 and 0059: the profile identity the tick carries; no figure name is stored.
- Does a tick without entries replace the shown state?
  Answered: yes (E7): the store's *Latest tick* rule decides what is restored, so memory follows it too (AC1).
- Can external testers try it, as the issue's documentation section asks?
  Answered: partly: nothing sends (#40, #34) or shows (#32) data yet; the documents name the start line as the sign.

## Area documents

- A new area document for the server's current state (name in the plan): N1, N2, the latest-tick rule, restore and its
  lines, E13, followers, profiles as record 0041 has them, no figure name.
- [`ingest.md`](../../docs/areas/ingest.md): E1, E3, E11, E14, AC5, AC6; an acknowledgement means stored.
- [`server-data.md`](../../docs/areas/server-data.md): written by the ingest; the restore in the start order; the file
  grows with every tick (about 77 KB per real tick every two minutes, 22.1 MiB at the bounds) until #35.
- `README.md` and `docs/USER_GUIDE.md` (English, then German): the last state per profile is kept and restored after a
  restart, with the start line; the database file grows with every tick until #35.

## Challenge

Spec challenge by the Devil's Advocate (1 major, 8 minor); all accepted, the last as an edge case.

1. Major — a catalog stored with icons missing would be named in later hellos and its icons never sent. Accepted:
   `catalogIdentity` only when every icon reference is stored, else `null`, so the Companion uploads again (E11, AC6).
2. Minor — resend identity of catalog and icon. Accepted: E1 per type; E12 the last upload wins (AC3, AC6).
3. Minor — no notice for catalog or icons. Accepted: followers are told (S5, AC8).
4. Minor — log lines of a hello whose profile cannot be stored. Accepted: E3's line only, place freed (AC7).
5. Minor — a blocking follower. Accepted: E10, AC8.
6. Minor — two read transactions, failed read. Accepted: N1 one transaction; E13, AC10.
7. Minor — "most recently changed" undefined. Accepted: N2's order; rename-only hello does not count (AC10).
8. Minor — disk growth before #35. Accepted: *Area documents*, README and user guide.
9. Minor — reconnect refused while a closed connection's store runs. Accepted as E14, unchanged: at most about 3 s at
   the bounds, the Companion retries, and freeing the place early would overlap two connections against record 0062.
