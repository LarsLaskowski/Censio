# 0031: The Companion buffers unsent ticks within a bound; beyond it gaps are accepted

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #41 (tick buffer)
- **Supersedes:** —

## Context

In forward mode ([0015](0015-two-deliverables-and-local-mode.md)) the server can be unreachable while the game runs:
a NAS restart, a network loss, a server update. Ticks produced then are lost unless the Companion keeps them, and
the gaming PC's disk and the player's patience are not unlimited.

## Options considered

1. **No buffer** — simplest; every outage is a gap.
2. **Unbounded buffer** — no gap ever; a long outage fills the disk.
3. **A persistent buffer bounded by age and size, delivered in order after the reconnect; beyond the bound the oldest
   ticks are dropped and the gap is shown as a gap** — short outages leave no trace, long ones a visible gap and a
   bounded disk use.

## Decision

Option 3. The Companion keeps unsent ticks in a persistent local buffer bounded by age and size, delivers them in
order after reconnecting and drops the oldest beyond the bound; the dropped ticks are counted and reported, and the
server shows a gap, never zeros (#41). The bounds and their defaults are set and documented by #41.

## Consequences

- An outage longer than the bound loses data; the history shows where.
- The server must accept late ticks in order ([0021](0021-sqlite-history-with-retention.md) keeps them at full
  resolution only while they are younger than 7 days).
- The buffer lies in a path from configuration, never from received data (`.squad/project.md`, *Security areas*).
