# 0019: Companion and server talk over a WebSocket with a shared token, enabled by default

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #28 (ingest endpoint), Issue #40 (forward mode)
- **Supersedes:** —

## Context

The Companion pushes ticks for as long as the game runs and expects acknowledgements; the server runs in a home network or on
a NAS, often behind a reverse proxy, and must not store data from any client that happens to reach it. The server
speaks plain HTTP and leaves TLS to a reverse proxy ([0006](0006-server-plain-http-and-open-health.md)). In local mode
([0015](0015-two-deliverables-and-local-mode.md)) the server runs in the Companion's own process.

## Options considered

1. **HTTP POST per tick** — simplest; a request per tick, and nothing tells the Companion that the server went away
   until the next post fails.
2. **gRPC or SignalR** — richer protocols; an extra protocol layer and package on both sides, and gRPC needs HTTP/2
   through the proxy.
3. **A plain WebSocket with a shared token** — one long-lived connection both ways, passes through common reverse
   proxies, built into ASP.NET Core and .NET; a single shared secret is enough for one Companion per server
   ([0020](0020-one-companion-per-server-with-savegame-profiles.md)).
4. **Per-client credentials or certificates** — stronger identity; key management that one Companion per server does
   not need.

For the token: **always required**, against **enabled by default, switchable off by configuration** — chosen: an
operator in a closed network or in local mode can do without it, while a fresh server never accepts an
unauthenticated Companion unless the operator turns the token off on purpose.

## Decision

Option 3. The Companion sends the contract messages ([0018](0018-pipe-parsed-in-companion-versioned-json-contract.md))
over a WebSocket; the server accepts a connection only with the shared token, which is enabled by default and can be
switched off by configuration (#28); only an explicit setting switches it off, a missing or empty token never does. The token is compared in constant time and never logged.

## Consequences

- The token's lifecycle (generation on first start, where the operator reads it) and the rules of the endpoint are
  decided and written down by #28 in the area document it introduces.
- `.squad/project.md` (*Security areas*), `docs/ARCHITECTURE.md` (*Data flow*, *Security model*) and `SECURITY.md`
  describe the token as always required; #28, which builds the switch, updates them together with it.
- With the token switched off, anyone who reaches the WebSocket can write data; the documentation of #28 names that
  risk where it describes the switch.
- Without TLS at a proxy the token travels in plain text; the deployment note in `SECURITY.md` covers this.
