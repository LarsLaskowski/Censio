# 0104: Live sending reconnects with growing delays, holds waiting messages in memory and finds a dead connection by its own pings

- **Status:** Proposed
- **Date:** 2026-11-22
- **Area:** live-sending
- **Source:** Issue #40; Issue #41 (persistent buffer)
- **Supersedes:** —

## Context

The live Companion must survive a server restart and a network loss without the player's hand
(#40), while the replay retries nothing ([0079](0079-recordings-sent-to-a-server-by-companion-replay-switches.md)).
The server refuses a second Companion while it still holds a vanished connection for up to about 56 seconds and blocks
a host after 10 failed hellos within 60 seconds ([`ingest.md`](../areas/ingest.md)). Ticks arrive about every two
minutes ([`pipe.md`](../areas/pipe.md)). The persistent buffer bounded by age and size is #41
([0031](0031-bounded-local-tick-buffer-gaps-accepted.md)). The server pings only after 15 seconds without data, and
.NET does not surface a received ping to the caller, so a client that waits for its next send learns of a dead
connection only then.

## Options considered

1. **Reconnect after 1, 2, 4, 8, 16 and then 30 seconds; a token or version refusal is final** (chosen) — a restart
   is caught within seconds, a long outage costs one attempt every 30 seconds, and a refusal that only a settings
   change or an update can cure never counts toward the host block. Against: a server whose token comes back needs a
   restart of the Companion.
2. **Retry every refusal as well** — cures a token that comes back; feeds the host block for nothing.
3. **Waiting messages kept in memory, bounded in count and bytes, oldest dropped first, the unacknowledged one resent
   with its identity** (chosen) — a short outage loses nothing while the Companion runs, memory stays bounded, the
   server treats a resend as known (record 0059). Against: `Exit` loses what waits until #41 persists it.
4. **Drop whatever is taken while no connection is accepted** — no queue across a reconnect; every restart of the
   server is a gap.
5. **The client's own WebSocket pings with a pong timeout** (chosen) — a dead idle connection is found within 60
   seconds (ping interval 15 s plus timeout 30 s, and the about 6 s the server's same mechanism measured beyond its
   sum), without a message of the contract. Against: rests on .NET's client keep-alive timeout, measured by the tests
   of #40.
6. **A `status` message as heartbeat** — the server never answers a `status`, so a send into a dead connection still
   succeeds into the socket's buffer.

## Decision

Options 1, 3 and 5. The values and texts are in [`live-sending.md`](../areas/live-sending.md).

## Consequences

- Up to the memory bound a server restart or network loss leaves no gap while the Companion runs; #41 replaces the
  bound by its persistent buffer.
- A refused token or contract version shows until the Companion is restarted.
- The replay keeps its rules: no reconnect, no own pings.
