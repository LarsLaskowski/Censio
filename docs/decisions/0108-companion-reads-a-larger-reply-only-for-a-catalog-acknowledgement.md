# 0108: The Companion reads a larger reply only for the acknowledgement of a catalog upload

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area:** live-sending
- **Source:** Issue #42
- **Supersedes:** —

## Context

The Companion reads every server reply up to 65,536 bytes ([`replay-sending.md`](../areas/replay-sending.md),
*Limits and errors*). The acknowledgement of a catalog upload may list 16,384 missing icon references, about 1.1 MB
([`ingest.md`](../areas/ingest.md), *Memory of a catalog upload*); the number a real catalog lists was never measured.

## Options considered

1. **Keep 65,536 bytes** — a fresh server's acknowledgement of a catalog with more than about 970 icons would end every
   connection as an invalid reply.
2. **Raise the bound for every reply** — a broken or hostile server could make every connection, the replay's too, hold
   the larger buffer.
3. **A larger bound only while a catalog acknowledgement is awaited** (chosen).

## Decision

Option 3: while the acknowledgement of a catalog upload is awaited, a reply may be up to 1,179,648 bytes, read into a
buffer allocated for that message only; every other reply keeps 65,536 bytes. The rule is in
[`live-sending.md`](../areas/live-sending.md), *Limits and errors*.

## Consequences

- At most one such buffer exists at a time, only on the live connection.
- A contract change that raises the `missingIcons` bound must raise this bound too.
