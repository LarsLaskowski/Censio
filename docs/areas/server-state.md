# Server state

## Scope

The server's current state per profile: what a profile's state holds, the latest-tick rule, the two profiles held in
memory and their order, the read on demand and its failure, the restore at start and its log lines, the notices to the
followers of the state, the Companion's connection and game state, the catalog's completeness, profiles as the Companion
chooses them, memory and privacy. The data pages ([`web-pages.md`](web-pages.md)) rely on it to show a profile without a
database read per update, and the operator relies on it to find the last state again after a restart. The database
itself is in [`server-data.md`](server-data.md), what the ingest answers in [`ingest.md`](ingest.md).

## Behavior

### What a profile's state holds

- The state of a profile is its stored row (identity and name, the name may be missing), its latest tick and the list of
  its islands. The latest tick is the one the store names ([`server-data.md`](server-data.md), *Latest tick*): the latest
  receive time and, among equal receive times, the one stored last. A tick that arrives after a newer one is stored, and
  the state stays the newer tick. A tick without entries (no data yet) or with only some of the islands becomes the state
  like any other; an island it lacks keeps its listing and name and has no values in that tick, never zeros.
- The island list holds every island the profile ever had, ordered by key (session GUID, island ID, area index), each
  with the name of the tick with the latest receive time that carried it, as the store lists them. An older tick can
  therefore name an island the latest tick lacks.
- A tick of one profile changes neither the state nor the island list of another profile.
- The server stores no figure name: a profile is what the Companion names, by the identity its hello and its ticks
  carry (record 0041).
- A reader gets an immutable snapshot: a state is replaced whole, never changed in place, so a state read before a tick
  stays as it was read and a reader never sees part of a tick.

### Profiles

- The list of profiles with their names is held whole, ordered by identity as the store lists them. A hello stores its
  profile identity and name before the server answers it: a new profile is created, a known one is renamed (record 0041).
  A tick of an identity no hello named creates the profile without a name; the next hello with that identity names it.
- A hello that only names or renames a profile does not move it in the order of the held profiles.

### Held profiles

- The server holds the state of at most two profiles (the profiles whose latest ticks are the newest by the order of
  *What a profile's state holds*: receive time, then storing order). The profile being played is among them as long as its ticks are not older than the held ones; since a tick's receive time is the Companion's clock, a profile whose Companion's clock runs behind, or any profile while two others carry ticks dated in the future, is not held and is read from the database (*Reading on demand*). Reading a held profile needs no database read.
- A tick of a profile that is held and not older than that profile's latest tick becomes its state, and the profile's
  island list takes the tick's islands with their names. A tick that is older stores and leaves the tick as it is; the
  island list is read again from the database, since the older tick can name an island the latest lacks.
- A tick of a profile that is not held takes the place of the oldest held state when fewer than two are held or the tick
  is not older than that oldest state; of equal receive times the profile restored or stored earlier leaves. Otherwise
  nothing is held for it. Its island list is read from the database and its name comes from the profile list; the whole latest tick is not read back.
- If reading the island list fails after the tick is stored, the tick stays stored and acknowledged and the held profiles
  stay as they are, the failure is logged (*Logging*). After a failed islands read a not-held profile enters at its next
  stored tick, with its stored latest state: the server then reads the profile's whole stored state, decides by that
  latest tick's receive time and stores it as the state; a failed read again keeps the mark until the next tick.
- Another profile's state is read from the database when it is asked for (*Reading on demand*).
- The profile whose latest tick is the newest among the held profiles (receive time, then storing order) is the newest
  profile (`NewestProfile`); none is when no profile is held. The data pages show it where no profile is picked.

### Reading on demand

- A read of a held profile returns the held snapshot. A read of another stored profile reads its row, its latest tick and
  its island list in one read transaction from the database, so the three agree. An identity the server does not know
  gives neither a state nor a failure.
- A read that fails gives the failure, never an empty or partial state, and logs the error line of *Logging*; the next
  request reads again. A read on demand is not limited in number (record 0069): a caller that anyone can reach without a
  login must serialize or bound its reads. The data pages are such callers: they ask whether a profile is held
  (`TryGetHeld`, one snapshot, so the answer and the held state agree) and read a profile that is not held only through
  one reader of the whole server, one read at a time, which their closed pages leave by cancelling
  ([`web-pages.md`](web-pages.md), record 0074). Anything that is shown from a state is derived from it and the state
  is dropped.

### Restore at start

- Before the server listens it reads the profile list, the state of the two profiles with the newest latest ticks (the
  same profiles *Held profiles* names, with ties ordered as they were stored), and the stored catalog. The state after
  a restart equals the one before the stop: the name, the latest tick bit for bit and the islands with their names.
- It logs `Profiles restored from the database: <n>.` (`Information`) with the number of
  stored profiles and nothing else of the data; an in-memory database restarts empty and logs `...: 0.`
- A failure reading the state stops the start with exit code 1 and the `Critical` line
  `The current state cannot be read from the database '<file>'; the server stops.` with the exception (the path is left
  out for an in-memory database). A database written before this feature is read as it is; the schema is unchanged.

### Catalog

- The server keeps one catalog, the one last uploaded, for every profile; an older upload replaces a newer one.
- Besides the catalog's identity and the references of its missing icons the state holds a lookup built from the
  document when it is stored or restored: for each asset GUID its kind, its English and German names, the region GUID
  of a region entry and the icon reference. The document itself is not kept, and the lookup is bounded by the catalog
  document's bounds ([`game-catalog-format.md`](game-catalog-format.md)). The data pages name regions and goods with it.
- The bytes of a stored icon are read from the database when a request asks for them, one read at a time (an `IconReader`
  over the store); they are not held.
- The catalog counts as complete only while it is usable and every icon reference of its document has a stored icon.
  `helloAccepted` names the catalog's identity only then, otherwise `null`, so a Companion uploads again after an
  interruption (record 0070). A stored catalog the server cannot read again at restore, or whose missing icons it cannot
  determine after storing it, is not usable and is not named.
- An upload whose identity (the SHA-256 of its document) equals the usable catalog's writes nothing; an acknowledgement,
  the resent one included, lists the references without a stored icon, sorted ordinally.

### Companion connection

- The state holds whether a Companion is connected and the pipe state it last reported (`status`), in memory only
  (record 0075). A Companion whose hello is accepted is connected with no pipe state; each accepted `status` message
  sets its pipe state; when the connection ends the state is not connected and the pipe state is forgotten. The server
  starts with no Companion connected.
- The ingest clears the state when a connection whose hello was accepted ends, before it frees the Companion's place, so
  the next Companion's connect cannot be overwritten ([`ingest.md`](ingest.md)).
- A change that leaves the state as it is (a `status` with the pipe state already held, a second disconnect) tells no one.
  The state is no database content and survives no restart.

### Followers and notices

- After each stored change the followers of the state are told which profile changed (a stored tick or hello) or that the
  catalog or its icons changed (a stored catalog or icon). A resend that writes nothing tells no one; a hello that only
  renames tells the profile changed. A change of the Companion's connection or pipe state is told as a third kind of
  notice, without a profile.
- Each follower is called on its own thread-pool work item, so a follower that blocks or throws delays neither the
  acknowledgement nor another follower. A follower that throws is logged. There is no order across changes. A follower
  that blocks for good keeps one thread-pool thread per change; the data pages hand a notice to their own work item
  and never block it ([`web-pages.md`](web-pages.md)).

### Logging

Category `Censio.Server.State`; no line holds a profile or island name, a figure name or a value of a tick, a catalog or
an icon.

| Level | Text | When |
| ----- | ---- | ---- |
| Error | `The current state of a profile cannot be read from the database.` with the exception | a read of a profile's islands or whole state fails |
| Warning | `A follower of the current state failed.` with the exception | a follower throws |

The restore lines are in *Restore at start*; the line of a message that could not be stored is in
[`ingest.md`](ingest.md).

### Memory

- The held state is two latest ticks, each held profile's full island list, the profile list and the references of the
  catalog's missing icons (at most 16,384). The island lists and the profile list are unbounded in count: a Companion
  with the token, or anyone with the token switched off, can create profiles and islands without a bound (*Accepted
  risks* in [`ingest.md`](ingest.md)).
- Measured with the worst tick of the contract (256 islands, 92,097 entries with four workforce pairs each, 368,388
  pairs; [`contract.md`](contract.md)) stored through the real state over a database file, .NET 11, Release, peak managed
  memory sampled every millisecond (`GC.GetTotalMemory`), three runs, a scratch program outside the repository:

  | Case | Retained before the call | Peak during the call |
  | ---- | ------------------------ | -------------------- |
  | Two held worst ticks and the incoming one, a tick of an unheld, newer profile that replaces the oldest | 125.4 MiB | 233.8 to 234.0 MiB |
  | The same, for a profile whose islands read failed earlier: the call also reads its whole stored state back (one more tick) | 125.4 MiB | 259.5 to 260.0 MiB |

  Retained, a worst tick built in code is about 41.8 MiB; read by the contract reader it retains 43.3 MiB ([`contract.md`](contract.md), *Memory of a read*). The peak includes what the write path allocates and the garbage collector has not collected yet, and a single run went higher (250.9 and 325.0 MiB in one of seven later runs); it is an order of magnitude, not a guarantee.
- Reads on demand are not limited in number; each holds the state it reads until its caller drops it. The data pages
  keep only what they show ([`web-pages.md`](web-pages.md)); an icon read holds its bytes until the response is written.

### Privacy

The state holds names of profiles and islands, which are private; the server writes none to its log
([`server-data.md`](server-data.md), *Start*) and sends none anywhere except to the data pages, which show them to
anyone who reaches the server ([`web-pages.md`](web-pages.md)).

## Limits and errors

- The number of held profiles is two; a tick the contract reader accepted is stored and held as it is, the state adds no
  limit of its own.
- A message whose storing fails changes no state; the ingest closes the connection ([`ingest.md`](ingest.md)).

## Related decisions

- [`decisions/0041`](../decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) — the profile is the one the Companion selects; no figure name is stored.
- [`decisions/0068`](../decisions/0068-ingest-acknowledges-a-message-only-after-it-is-stored.md) — a message is acknowledged only after it is stored.
- [`decisions/0069`](../decisions/0069-current-state-of-the-newest-profiles-held-in-memory-and-restored-at-start.md) — the two newest profiles are held in memory, restored at start and announced off the ingest's path.
- [`decisions/0070`](../decisions/0070-hello-names-the-stored-catalog-only-when-all-its-icons-are-stored.md) — the hello names the catalog only when all its icons are stored.
- [`decisions/0074`](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md) — the pages read profiles that are not held one at a time and keep only derived views.
- [`decisions/0075`](../decisions/0075-companion-connection-and-game-state-held-in-the-server-state.md) — the Companion's connection and game state are held in the server state and announced.

## Not here

- Storing, the schema and the database file: [`server-data.md`](server-data.md).
- What the ingest answers and when it closes: [`ingest.md`](ingest.md).
- The pages, the profile switch and the banner that show it: [`web-pages.md`](web-pages.md), [`web-ui.md`](web-ui.md).
- Retention and deleting a profile with its held state (#35).
