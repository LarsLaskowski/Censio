# 0064: The ingest endpoint refuses browser upgrades and bounds connections waiting for their hello

- **Status:** Proposed
- **Date:** 2026-11-12
- **Area:** ingest
- **Source:** Issue #28
- **Supersedes:** —

## Context

Browsers may open WebSockets to any host and send an `Origin` header; a page a player visits could reach a server in the
home network, and with the token switched off write data to it. Before its hello a connection is unauthenticated: each may
hold a 32 KiB message for 10 seconds, and any host can open many ([`contract.md`](../areas/contract.md), *Security
considerations for the endpoint*).

## Options considered

1. **Allow-list of origins** — supports a browser client; Censio has none, and a list is configuration to get wrong.
2. **Refuse every upgrade with an `Origin` header** (chosen) — a Companion sends none; a browser always does.
3. **No bound on pending connections** — simplest; memory and sockets grow with an attacker's connections.
4. **A fixed bound of 16 pending connections, refused with `503` beyond** (chosen) — bounded; a host that keeps 16 open
   locks the Companion out, an accepted risk while there is no rate limit.
5. **A bound per remote address** — fairer; behind a reverse proxy every client has the proxy's address.

## Decision

Options 2 and 4. Values and statuses are in [`ingest.md`](../areas/ingest.md).

## Consequences

- A browser-based Companion would need this record revisited.
- Rate limiting per host stays open; the lock-out is named in `ingest.md` and `SECURITY.md`.
