# 0075: The Companion's connection and game state are held in memory in the server state and announced to its followers

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** server-state
- **Source:** Issue #32
- **Supersedes:** —

## Context

The banner of the pages says whether the Companion is connected and whether it can read the game (issue #32). The
ingest knows the connection and receives `status` messages, but kept nothing of them
([`areas/ingest.md`](../areas/ingest.md)); the pages already follow the server state's change notices
([`areas/server-state.md`](../areas/server-state.md)).

## Options considered

1. **A separate service for the Companion's state** — keeps the state of stored data apart, but gives the pages a
   second subscription with its own threading rules.
2. **Held in `ServerState` with a third notice kind** — one event for everything a page follows.
3. **Stored in the database** — survives a restart, where the connection is gone anyway; writes per `status` message.

## Decision

Option 2: `ServerState` holds whether a Companion is connected and its last pipe state, in memory only, and tells its
followers when either changes; the ingest clears it before it frees the Companion's place.

## Consequences

After a restart no Companion counts as connected until one says hello. Until the Companion sends `status` (#40) the
banner knows only the connection.
