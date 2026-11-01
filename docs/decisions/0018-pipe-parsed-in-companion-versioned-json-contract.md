# 0018: The Companion parses the pipe; the server receives only a versioned JSON contract

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #9 (pipe parser), Issue #27 (contract v1)
- **Supersedes:** —

## Context

The statistics pipe has a binary, undocumented format with its own protocol version that a game update can change.
Companion and server are released together but are not always updated together: a Docker server on a NAS may run
another version than the Companion on the gaming PC.

## Options considered

1. **Forward the raw pipe frames, parse on the server** — the Companion stays thin; the server depends on the game's
   format, and every game update needs a server update.
2. **Parse in the Companion and send the game's structures as they are** — no second model; the game's format still
   leaks into the server.
3. **Parse in the Companion and send a contract of Censio's own, JSON with an explicit contract version** — the
   server never sees pipe frames; a game update touches the Companion only; a mismatched pair is detected and
   refused instead of misread. Con: a second model to map to and keep in step.

## Decision

Option 3. Only the Companion reads and parses the pipe (`Censio.Pipe`); Companion and server exchange the JSON
messages of a versioned contract in `Censio.Contracts` (#27), and the server rejects a contract version it does not
support.

## Consequences

- The game's format is confined to `Censio.Pipe` and `Censio.GameData`.
- A contract change bumps its version and is documented with the contract (#27); a server and a Companion with
  incompatible versions refuse each other with a clear error instead of storing wrong data.
- Every WebSocket message is untrusted input for the server and validated there (`.squad/project.md`, *Security areas*).
