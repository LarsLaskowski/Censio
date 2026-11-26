# 0116: A profile's history is also deleted through the ingest connection, after a hello with the token

- **Status:** Proposed
- **Date:** 2026-11-26
- **Area:** history-deletion
- **Source:** Issue #251
- **Supersedes:** 0085 — amends its rule that a profile's history can be deleted only from a page opened by a trusted
  host: the Companion's deletion through `/ingest` is a second way, guarded by the ingest token instead.

## Context

Record [0115](0115-profile-deleted-in-the-companion-history-on-the-server-by-the-players-choice.md) lets the player
delete a profile's history from the Companion. Record [0085](0085-deletion-only-from-pages-opened-by-a-trusted-host.md)
guards the pages' deletion against DNS rebinding with a trusted-host rule; `/ingest` refuses browser upgrades (record
[0064](0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md)) and stores data only after a hello with the
token (record [0019](0019-websocket-transport-with-shared-token.md)). Contract version 2 is released and read by exact
version (record [0058](0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md)).

## Options considered

1. **A new contract message `profileDeletion` (`id`, `profileId`), acknowledged like every message once the deletion's
   rows are gone, contract version 3** (chosen) — one rule for every Companion message, the token guards it, a failed
   deletion ends the connection as a failed store does.
2. **A property of the hello naming profiles to delete** — the hello is frozen in every version.
3. **An HTTP endpoint for deletions** — a new route reachable without login, a second authentication path.
4. **The Companion opens the pages' deletion** — needs a browser and a trusted host; offline deletions could not wait.

## Decision

Option 1. The server deletes the named profile through the same deletion as the pages
([history-deletion](../areas/history-deletion.md)) on an accepted ingest connection, and answers it with an
acknowledgement; the message is in [contract](../areas/contract.md), its handling in [ingest](../areas/ingest.md).

## Consequences

- With the token required only a token holder can delete, any profile by identity; with the token switched off anyone
  who reaches `/ingest` outside a browser can, as anyone reaching the pages by IP address already can.
- A Companion and a server of different releases refuse each other's hello (version 3 against 2).
- The trusted-host rule stays the pages' alone; a deletion through `/ingest` logs the retention job's lines and
  nothing that names the profile.
