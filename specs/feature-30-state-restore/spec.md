# Spec: Current state per profile and restore after restart

Source: Issue #30
Status: Draft

## Problem

The server accepts a Companion's ticks, catalog and icons but keeps none of them: an acknowledgement only says that a
message was read and checked ([`ingest.md`](../../docs/areas/ingest.md), *Messages after the hello*), and the store of
#29 ([`server-data.md`](../../docs/areas/server-data.md)) is opened at start but never written. A player whose game is
closed, or whose server container restarts, would therefore lose everything the pages of #32 are to show. The player
keeps several playthroughs as profiles (records [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md),
[0041](../../docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)), whose data must not mix.
The pages need the latest state of a profile at once, without a database read per update.

## Scenarios

- S1: Given a connected Companion, when it sends a tick of profile A, then the server stores it, then acknowledges it
  (`refusal` `null`), and A's current state is that tick, its islands and their names; whoever follows A's state
  (the pages of #32) is told that A changed.
- S2: Given profiles A and B with ticks, when a tick of B arrives, then B's current state changes and A's does not, and
  the islands of B are not listed for A.
- S3: Given a server with stored ticks of A and B and a stored catalog, when the server (or its container) is stopped
  and started again with the same database file, then before it listens the current state of A and B is the one before
  the stop, the log holds `Profiles restored from the database: 2.`, and the next hello is answered with the stored
  catalog's identity.
- S4: Given a hello with profile identity P and name "Rome", when it is accepted, then P is stored with that name; a
  later hello with P and "Roma" renames it. A tick of an identity no hello named creates a profile without a name, which
  the next hello with that identity names.
- S5: Given a hello answered with `catalogIdentity` `null`, when the Companion uploads a catalog and then its icon
  images, then the catalog's acknowledgement lists in `missingIcons` the references with no stored icon, each icon is
  stored, and the next hello is answered with the catalog's identity. A later upload replaces the catalog for every
  profile.

## Errors and edge cases

- E1: A tick, catalog upload or icon image whose identity is already stored (a resend after a lost acknowledgement) is
  acknowledged with `refusal` `null`; nothing is stored again, the current state does not change and no one is told of
  a change.
- E2: A tick of A that arrives after a newer one (a buffered tick resent after a reconnect) is stored, but A's current
  state stays the newer tick. Of two ticks with equal receive times the one stored later is the current one (the store's
  *Latest tick* rule).
- E3: Storing a message fails (disk full, file removed): it gets no acknowledgement, the current state does not change,
  the connection is closed with status 1011 and the server logs the error
  `A message could not be stored; the connection is closed.` with the exception. The Companion reconnects and resends
  it. A hello whose profile cannot be stored gets no reply and is closed the same way. The message counts as refused in
  the disconnect line.
- E4: The stored state cannot be read at start: the server stops before it listens with exit code 1 and the `Critical`
  line `The current state cannot be read from the database '<file>'; the server stops.`
- E5: Without a database file (in memory, [`server-data.md`](../../docs/areas/server-data.md), *File and setting*) a
  restart starts with no profile and logs `Profiles restored from the database: 0.` after the existing warning.
- E6: The server stops while a tick is being stored: the tick is stored whole or not at all and is acknowledged only
  when stored, so after the restart it is either the current state or resent by the Companion.
- E7: A tick whose islands all have no entries (*no data yet*, sent after a load) is a tick like any other: it becomes
  the current state when it is the latest.
- E8: A tick that carries only some of the profile's islands (a tick split by a stall, [`pipe.md`](../../docs/areas/pipe.md))
  becomes the current state; the islands it lacks keep their listing and name but have no values in it, never zeros.
- E9: A session start, session end or `status` message is answered as today and changes no stored data and no
  current state.
- E10: A follower of the state that throws or is slow delays neither the acknowledgement nor the other followers.

## Non-functional requirements

- N1: Reading a profile's current state needs no database read while the profile is among those held in memory (N2);
  a reader sees a profile's state either wholly before or wholly after a change, never part of a tick.
- N2: The memory the current state holds is bounded whatever the number of stored profiles: the latest ticks of at most
  a fixed number of profiles, the most recently changed, are held; another profile's state is read from the database
  when asked for. The number, and the memory it means at the contract's tick bound, are fixed in the plan and stated in
  the area document.
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
  `Profiles restored from the database: <n>.` with the number of stored profiles and nothing else of the data (S3, E5, E6, N4, N5).
- AC2: A tick of one profile changes neither the current state nor the island list of another, before and after a
  restart (S2).
- AC3: A tick, catalog upload or icon image the reader accepts is acknowledged with `refusal` `null` only after it is
  stored; one whose identity is stored is acknowledged the same way and changes nothing; session events and `status`
  change no stored data and no state (S1, E1, E9).
- AC4: A profile's current state is its latest tick by the store's rule: a later-arriving older tick leaves it
  unchanged, and of equal receive times the one stored later wins; a tick without entries or with only some islands
  becomes the state like any other (E2, E7, E8).
- AC5: An accepted hello stores its profile identity with its name (created, or renamed when known) before
  `helloAccepted`; a tick of an unknown identity creates the profile without a name (S4).
- AC6: A stored catalog upload replaces the earlier catalog for every profile; `helloAccepted` names the stored
  catalog's identity, or `null` without one; a catalog's acknowledgement lists in `missingIcons` only the references
  without a stored icon (S5).
- AC7: A message whose storing fails gets no acknowledgement, leaves the state unchanged, closes the connection with
  1011 and logs the line of E3, which holds no content of the message; a hello whose profile cannot be stored gets no reply
  and closes the same way (E3, N4).
- AC8: After each stored change of a profile's state the followers are told which profile changed; a reader never sees a
  partial state, and a follower that throws affects neither the acknowledgement nor another follower (S1, E10, N1).
- AC9: The state is restored before the server listens; a failure reading it stops the start with exit code 1 and the
  `Critical` line of E4 (S3, E4).
- AC10: With more stored profiles than the number of N2, the state of every profile, held or not, equals its stored
  latest tick, and no more than that number of latest ticks is held in memory (N2).

## Out of scope

- Pages, the profile switch on a page and the banner with the "as of" time and the connection status: #31 and #32;
  the Companion's connection state and `pipeState` are not part of the state here (#32).
- Storing session events, time series, retention and deleting a profile: #35.
- Sending ticks and the catalog from the Companion: #40, #42; the Companion's buffer and resend: #41.
- Selecting and creating profiles and the figure-name notice in the Companion: #43.
- N3's timing is not tested by a unit test; it rests on the measurement of `server-data.md`.

## Open questions

- Does the issue's "catalog per profile" mean one catalog for each profile?
  Answered: no. A catalog upload carries no profile identity ([`contract.md`](../../docs/areas/contract.md)) and the
  store keeps one catalog ([`server-data.md`](../../docs/areas/server-data.md), *What is stored*); the catalog belongs to
  the game build, not a playthrough. Ticks are kept per profile, the catalog per server (S5, AC6).
- What does "shown unchanged" show before the pages of #32 exist?
  Answered: no page shows data before #32, which depends on this issue. Shown means the current state the pages read
  (AC1), and the start line lets an operator see that the state was restored.
- What decides a tick's profile?
  Answered: record 0041 and record 0059: the profile identity each tick carries, never the profile of the connection's
  hello or a figure name; the server stores no figure name.
- Does a tick without entries replace the shown state?
  Answered: yes (E7). The store's *Latest tick* rule (#29) decides what is restored, so the state in memory follows the
  same rule, or a restart would change what is shown (AC1).
- Can external testers try it, as the issue's documentation section asks?
  Answered: only partly: no Companion sends data yet (#40, #34) and no page shows it (#32). README and user guide say
  that the server now keeps the data it receives per profile and restores it at start, with the start line as the
  visible sign, and keep saying that nothing sends or shows data yet.

## Area documents

- A new area document for the server's current state (name decided in the plan): what the state of a profile holds,
  the latest-tick rule it follows, restore at start and its log lines, the memory bound of N2, how followers are told,
  profiles as the Companion chooses them (record 0041's consequence for #30) and that the server keeps no figure name.
- [`ingest.md`](../../docs/areas/ingest.md): an acknowledgement means stored; `catalogIdentity` and `missingIcons` from
  the store; the hello stores its profile; the storing failure, its close 1011 and log line; the disconnect counts.
- [`server-data.md`](../../docs/areas/server-data.md): the store is now written by the ingest; what the ingest answers to
  a failed write (link to ingest); the start order with the restore after the database is opened.
- `README.md` and `docs/USER_GUIDE.md` (English, then German): the last state per profile is kept and restored after a
  restart, with the start line.
