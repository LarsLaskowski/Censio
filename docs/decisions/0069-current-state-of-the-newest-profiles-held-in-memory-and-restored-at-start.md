# 0069: The current state of the two profiles with the newest ticks is held in memory, restored at start and announced off the ingest's path

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** server-state
- **Source:** Issue #30
- **Supersedes:** —

## Context

The pages of #32 need a profile's latest tick and island names on every update, without a database read each time,
and the last state must survive a restart. The store of #29 already defines the latest tick of a profile
([`server-data.md`](../areas/server-data.md)). One tick at the contract's bounds held 124.1 MiB in the Companion
([`pipe.md`](../areas/pipe.md)), and anyone with the token (anyone at all with the token switched off) can create
profiles by sending ticks with new profile identities. Only one Companion sends at a time, and it sends for the one
profile the user selected (record [0041](0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)).

## Options considered

1. **Read the database on every request** — nothing to restore, no memory; a page update reads up to the whole tick
   (1 to 12 ms for a real tick, over a second at the bounds) for every follower.
2. **Hold every profile's state** — every read is free; memory grows with the number of profiles without a bound.
3. **Hold the latest tick of a fixed number of profiles, those with the newest ticks; read others on demand**
   (chosen) — the profile being played is always held, memory is bounded; a page on an old profile pays one read.
4. **Hold only the island values, merged across ticks per island** — survives split ticks; differs from the store's
   latest-tick rule, so a restart would change what is shown.

Followers: invoking them on the ingest's thread would let one slow page delay acknowledgements; each is called on its
own thread-pool work item instead.

## Decision

Option 3 with two held profiles, ordered by the store's latest-tick order and restored from the database before the
server listens; followers are told of a change on the thread pool, one work item per follower. The rules are in
[`server-state.md`](../areas/server-state.md).

## Consequences

- The held state is two latest ticks plus each held profile's full island list, the profile list and the catalog's
  missing-icon references. The island lists and the profile list are unbounded in count: a token holder (anyone, with
  the token off) can grow them, an accepted risk since a real game has a finite set of islands. The peak while a tick
  replaces a held one is measured and stated in the area document.
- A page on a third profile reads it from the database each time it asks; a failed read is reported, never shown as
  empty. Reads on demand are not limited in number; a caller reachable without login (#32) must serialize or bound them.
- A new held state takes the incoming tick and reads only the profile's island list, so no whole tick is read back
  before an acknowledgement.
- A follower that blocks for good keeps one thread-pool thread per change; the pages of #32 must not block.
- Revisit when pages show several profiles at once, or when the Companion can send for several profiles.
