# 0006: The server serves plain HTTP; start page and health check need no login

- **Status:** Proposed
- **Date:** 2026-10-30
- **Area:** server
- **Source:** Issue #2
- **Supersedes:** —

## Context

The server opens its first listener with a start page and `GET /health`, which the Docker image (#4) and the
release checks (#5) call. `SECURITY.md` places the server in a trusted network or behind a reverse proxy that
terminates TLS. The ASP.NET Core project template adds HTTPS redirection and HSTS, which behind such a proxy
redirect health probes and pin browsers to HTTPS on hosts that have none.

## Options considered

1. **Keep the template's HTTPS redirection and HSTS** — safe on a directly exposed host; breaks plain-HTTP
   health probes in a container and conflicts with TLS at the proxy.
2. **Plain HTTP only, TLS at the reverse proxy; `/` and `/health` without login, `/health` without details** —
   matches `SECURITY.md`; the server itself never encrypts.

## Decision

Option 2. The server adds no HTTPS redirection and no HSTS, listens on the ASP.NET Core default address
(loopback only) unless configured, and answers `/health` with its status alone. The rules are in
[`areas/server.md`](../areas/server.md).

## Consequences

- An operator who exposes the server beyond a trusted network must put a TLS proxy in front (`SECURITY.md`).
- Web login stays out (none planned); a later login must exempt `/health`.
- The start page shows the version to anyone who can reach it, accepted in a trusted network.
