# 0082: The ingest endpoint limits connections per host and blocks a host whose hellos keep failing

- **Status:** Proposed
- **Date:** 2026-11-15
- **Area:** ingest
- **Source:** Issue #177
- **Supersedes:** —

## Context

Record [0064](0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md) bounds the connections waiting for
their hello at 16 in total and left rate limiting open: one host could keep the Companion out by refilling the 16 places,
and a client reconnecting with a wrong token or an old contract version wrote a warning line per attempt into a log that
Docker's default driver does not rotate. Logs name no remote address ([`ingest.md`](../areas/ingest.md), *Log lines*).
The server reads no forwarded header of its own, but `ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true` makes ASP.NET Core take
the remote address from any client's `X-Forwarded-For`. Whether a failed hello was the host's fault is known only after
the hello, not at the upgrade.

## Options considered

1. **ASP.NET Core's rate limiter, partitioned by address** — built in; it counts requests, not failed hellos, a
   concurrency lease lasts the whole WebSocket session rather than the hello, and the number of partitions has no bound.
2. **A bound on waiting connections per host only** — ends the lock-out by one host; a wrong-token loop still writes a
   line per attempt.
3. **A cap on refusal lines across all hosts** — bounds the log; one host still locks the Companion out.
4. **Waiting connections per host and a block of a host after repeated failed hellos, in a bounded table of hosts**
   (chosen) — bounds both per host; the table's overflow shares one entry, so a flood of addresses is limited together.
5. **A host as an IPv6 `/64`** — catches a device that picks many addresses; merges every device of an IPv6 network, so
   one device's wrong token blocks the Companion beside it.
6. **Trusting forwarded headers from proxies the operator lists** — fair behind a proxy; configuration to get wrong,
   the ground on which 0064 rejected an origin list.
7. **Naming the blocked address in the log** — lets the operator block it; breaks the rule that no line names an
   address.

## Decision

Option 4: limits per TCP peer address (a full IPv6 address is a host of its own), counted on failed hellos within a
sliding window, with the host of the last accepted hello kept outside the bounded table. `X-Forwarded-For` never
changes the remote address. Values, the cases that count and the log lines are in [`ingest.md`](../areas/ingest.md).

## Consequences

- Behind a reverse proxy, or a Docker setup that does not keep the client's address, all clients are one host: one
  client's failures block the Companion behind the same address.
- A device that uses many IPv6 addresses escapes the limits per host; only the total of 16 holds against it.
- A flood of more hosts than the table holds refuses a Companion that arrives on a new address while it lasts.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED` keeps its effect on the scheme only, for the whole server.
- Many hosts can still grow the log; Docker log rotation stays the operator's remedy.
