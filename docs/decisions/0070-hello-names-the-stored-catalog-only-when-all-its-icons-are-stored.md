# 0070: The hello reply names the stored catalog only when all its icons are stored

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** ingest
- **Source:** Issue #30
- **Supersedes:** —

## Context

The Companion of #42 skips the catalog upload when the hello reply names the catalog it has. Only the acknowledgement
of a catalog upload tells it which icons the server lacks ([`contract.md`](../areas/contract.md)). The server stores the
catalog before the icons arrive, so a connection or a server that ends in between would leave a catalog the reply
names but whose icons never come.

## Options considered

1. **Name the stored catalog always** — simple; icons missing after an interruption stay missing until the game
   build changes.
2. **A new hello reply field with the missing icons** — tells the Companion directly; needs a new contract version, which
   every Companion and server must then speak.
3. **Name the stored catalog only when every icon it refers to is stored, else `null`** (chosen) — the Companion
   uploads the catalog again and learns the missing icons from its acknowledgement; no contract change.

## Decision

Option 3. The server keeps the missing icon references of the stored catalog and names the catalog's identity in
`helloAccepted` only while that set is empty. The rules are in [`ingest.md`](../areas/ingest.md).

## Consequences

- An interrupted icon transfer costs one more catalog upload (up to 24 MiB) at the next connection.
- A stored catalog that this build's catalog reader refuses at start counts as incomplete, so it is replaced by the next upload.
- #42 builds on this: a named catalog is complete.
