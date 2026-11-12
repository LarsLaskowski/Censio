# 0062: A second Companion is refused; a silent connection is found by WebSocket pings

- **Status:** Proposed
- **Date:** 2026-11-12
- **Area:** ingest
- **Source:** Issue #28
- **Supersedes:** —

## Context

A server serves exactly one active Companion ([0020](0020-one-companion-per-server-with-savegame-profiles.md)), and
#28 decides what happens to a second connection. The contract has the hello refusal `companionConnected`
([0058](0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md)). A Companion whose PC sleeps or loses the
network leaves a connection the server does not see end; refused reconnects must not last. The contract has no heartbeat,
and an idle Companion (no game running) sends no message for hours.

## Options considered

1. **A new connection replaces the old one** — a reconnect always wins; two Companions with the token take the
   connection from each other in turn, and the history mixes their data.
2. **A second connection is refused** (chosen) — matches the contract's refusal; needs a bound on how long a dead
   connection keeps the place.
3. **A heartbeat message in the contract** — visible to the reader; a contract change, and a new version.
4. **An own watchdog on received data** — testable on an injected clock; drops an idle Companion, which sends nothing.
5. **The WebSocket ping keep-alive of .NET: a ping after 15 s without received data, closed when no pong arrives within
   40 s** (chosen) — no contract change, idle Companions answer pings, a dead one is closed within 60 s; runs on the
   real clock.

## Decision

Options 2 and 5. A hello that passes the version and token checks while a Companion is connected is refused with
`companionConnected`; the endpoint accepts its WebSockets with the keep-alive interval 15 s and timeout 40 s. Values are in
[`ingest.md`](../areas/ingest.md).

## Consequences

- A ping unanswered when a long message begins is answered only after that message (the client cannot interleave a pong
  in a frame); such a message must end within the timeout, else the Companion reconnects and resends.
- The Companion (#40) keeps a receive pending, since .NET answers pings only while receiving.
- The keep-alive is not unit-testable on a fake clock; its timing is measured on the real component and recorded in the
  area document.
