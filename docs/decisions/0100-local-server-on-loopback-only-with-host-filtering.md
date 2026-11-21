# 0100: The local server listens on the loopback addresses only and answers only loopback host names

- **Status:** Proposed
- **Date:** 2026-11-21
- **Area:** local-mode
- **Source:** Issue #39
- **Supersedes:** —

## Context

The server's pages need no login and anyone who reaches the port reads every profile
(record [0006](0006-server-plain-http-and-open-health.md)). The standalone server leaves host filtering off by
default. A page of another site can point its own name at `127.0.0.1` (DNS rebinding) and read a loopback server's
pages through the player's browser. The Product Manager decided that the local server is for the local PC only.
On a gaming PC, other signed-in Windows users and programs share the loopback interface.

## Options considered

1. **Explicit endpoints on `127.0.0.1` and `[::1]` (IPv4 alone without IPv6), host filtering to `localhost`,
   `127.0.0.1` and `[::1]`** (chosen) — no firewall prompt, nothing reachable from the network, and rebinding is
   answered with `400`.
2. **`localhost` as Kestrel resolves it** — if `127.0.0.1` cannot be bound for a reason other than a taken port,
   Kestrel continues on `[::1]` alone. The browser's `localhost` could then reach another program holding
   `127.0.0.1`.
3. **All interfaces** — another device of the network could reach the pages and delete history. Windows asks for a
   firewall permission. The Product Manager put the LAN out of scope.
4. **Loopback without host filtering** — matches the standalone server, but leaves rebinding open to reading.

## Decision

The local server binds `127.0.0.1`, and `[::1]` when the system supports IPv6. A port taken on either address fails
the start as *port in use*. Requests are accepted only for the host names `localhost`, `127.0.0.1` and `[::1]`, and a request
without a host name is refused; the rules are in [local-mode](../areas/local-mode.md).

## Consequences

- Programs and other Windows users of the same PC can read every profile and delete history. Sending data still
  needs the token in the player's private folder. This is an accepted risk, stated in local-mode, `README.md`, the
  user guide and `SECURITY.md`.
- Reaching the local server from another device needs a new decision.
