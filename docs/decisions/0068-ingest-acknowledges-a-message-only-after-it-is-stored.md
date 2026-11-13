# 0068: The ingest acknowledges a message only after it is stored; a failed write closes the connection unacknowledged

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** ingest
- **Source:** Issue #30
- **Supersedes:** —

## Context

Until #30 an acknowledgement said only that a message was read and checked ([`ingest.md`](../areas/ingest.md)). The
Companion discards an acknowledged message and resends one without an acknowledgement (record
[0059](0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md), #41), so an acknowledgement
sent before the write ends would lose the message when the write then fails or the server stops. The hello's refusal
reasons are frozen (record [0058](0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md)), and the
acknowledgement's refusal kinds name faults of the message, not of the server.

## Options considered

1. **Acknowledge on read, store afterwards** — fastest answer; a failed or interrupted write loses a message the
   Companion has already discarded.
2. **Acknowledge after the write; on a failed write acknowledge with a refusal kind** — needs a new kind (a contract
   change), and an acknowledged refusal tells the Companion to discard a message that was fine.
3. **Acknowledge after the write; on a failed write send nothing and close with 1011** (chosen) — the Companion keeps
   the message and resends it after its reconnect; no contract change. A hello whose profile cannot be stored is closed
   the same way, because no frozen refusal reason fits.

## Decision

Option 3: an accepted tick, catalog upload or icon image is acknowledged only once it is stored or found already
stored, and a write that fails sends no acknowledgement and closes the connection with status 1011. The rules are in
[`ingest.md`](../areas/ingest.md).

## Consequences

- A known identity is acknowledged like a new message: a resend after a lost acknowledgement costs one store lookup.
- A full disk makes the Companion reconnect and resend in a loop until space is freed; each attempt writes one error line.
- The session reads no further message while a write runs (at the bounds about 3 s), which stays far inside the
  keep-alive of record [0062](0062-one-companion-second-refused-silence-found-by-websocket-pings.md); a reconnect within
  that time is refused `companionConnected`.
