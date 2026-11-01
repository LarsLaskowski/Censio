# 0006: The server serves plain HTTP; the web UI and the health check need no login

- **Status:** Accepted
- **Date:** 2026-10-30
- **Area:** server
- **Source:** Issue #2, Issue #4, Issue #8
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

For the web UI as a whole (issue #8): **a login of the server's own** (a password or user accounts) — protects a
server that is exposed by mistake; a credential store and a login flow for a tool one player runs at home, and a
reverse proxy can add authentication where it is needed — against **no web login, access decided by the deployment**
— chosen.

## Decision

Option 2. The server adds no HTTPS redirection and no HSTS, listens on the ASP.NET Core default address
(loopback only) unless configured, and answers `/health` with its status alone. The Docker image configures it
deliberately: it sets `ASPNETCORE_HTTP_PORTS=8080`, so inside the container the server listens on all interfaces,
and the operator's `-p` mapping decides who can reach it (issue #4). The web UI has no login at all: who may reach it
is decided by the network and an optional reverse proxy (`SECURITY.md`). The rules are in
[`areas/server.md`](../areas/server.md).

## Consequences

- An operator who exposes the server beyond a trusted network must put a TLS proxy in front (`SECURITY.md`).
- In the image the loopback default does not protect the server: a `-p 8080:8080` mapping publishes it on every
  interface of the host. Kept, because a container listening on loopback alone is unreachable, and Docker's
  `-p 127.0.0.1:8080:8080` restricts it where needed (documented in the README).
- Anyone who reaches the server can read every page and use every action the UI offers; a later login would supersede this record and must exempt
  `/health`.
- The start page shows the version to anyone who can reach it, accepted in a trusted network.
- Host filtering is off (`AllowedHosts` `*`) and the Blazor hub checks no `Origin`, so a DNS-rebinding page can
  read the start page; it shows only the version, accepted. The first change that puts game data or a state-changing
  action on a page revisits this choice.
