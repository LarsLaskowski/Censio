# 0020: One Companion per server; savegames are kept apart as profiles

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #19 (profile detection), Issue #28 (ingest), Issue #30 (state per profile)
- **Supersedes:** —

## Context

Censio is a tool for one player. A player plays several savegames, whose islands and goods must not be mixed in one
history. The pipe carries no savegame ID, so which savegame a tick belongs to has to be decided some other way (#19).

## Options considered

1. **Several Companions per server (several players or PCs)** — a shared server for a household; needs per-client
   identity, data separated per player and a UI to choose between them.
2. **One Companion per server, one history** — simplest; savegames mixed.
3. **One active Companion per server, its data kept apart in savegame profiles** — matches one player; each savegame
   has its own state and history, which can be deleted on its own.

## Decision

Option 3. A server serves exactly one active Companion; everything the server stores belongs to a savegame profile.
What happens to a second connection is decided in the spec of #28, how a tick finds its profile in #19.

## Consequences

- One shared token is enough ([0019](0019-websocket-transport-with-shared-token.md)).
- Retention and deletion work per profile ([0021](0021-sqlite-history-with-retention.md)).
- A household with two players runs two servers, or two local-mode Companions.
