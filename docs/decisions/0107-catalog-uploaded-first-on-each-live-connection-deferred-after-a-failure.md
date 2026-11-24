# 0107: The catalog is uploaded first on each live connection, deferred after a failed upload and given up after three

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area:** live-sending
- **Source:** Issue #42
- **Supersedes:** —

## Context

The server cannot read game files (record [0023](0023-game-names-and-icons-extracted-locally.md)) and names a stored,
complete catalog in its hello reply (record [0070](0070-hello-names-the-stored-catalog-only-when-all-its-icons-are-stored.md)).
Live sending has one message in flight and a bounded buffer whose oldest messages are dropped at its limits (records
[0031](0031-bounded-local-tick-buffer-gaps-accepted.md), [0105](0105-unsent-live-messages-kept-one-file-each-in-a-fixed-buffer-folder.md)).
A catalog that the server never takes in time (a slow link, a failing store) would end every connection if it always
went first, so no tick would be sent.

## Options considered

1. **The catalog as buffer files** — up to 24 MiB inside the buffer's limits would drop ticks; the catalog is rebuilt
   from memory anyway.
2. **A connection of its own** — the server accepts one Companion at a time (record 0062).
3. **Always after the buffered messages** — after a long outage the names come last.
4. **First, deferred behind the waiting messages after a connection that failed during it** — ticks still flow, but a
   catalog the server never takes reconnects every 30 seconds for as long as the Companion runs.
5. **First, with a cap of attempts** — ticks starve for the capped rounds.
6. **First, deferred after a failure, and given up after three failed connections in a run** (chosen).

## Decision

Option 6: after `helloAccepted` the ready catalog whose identity the server does not name goes before the waiting
messages, then its missing icons. A connection that fails during the upload defers it behind the messages waiting at
the next hello, and the third such failure in a row ends its upload for the run. The rules are in
[`live-sending.md`](../areas/live-sending.md), *Catalog*.

## Consequences

- A catalog that keeps failing costs at most three reconnects per catalog and run; the ticks waiting meanwhile are sent.
- A catalog given up or refused reaches the server only after a newer catalog or a restart.
- The catalog and icons are never in the buffer folder, and a replay sends none (#42 out of scope).
