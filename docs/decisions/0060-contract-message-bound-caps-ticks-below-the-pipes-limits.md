# 0060: One byte bound per contract message caps a tick below the pipe's limits; images and catalog travel as Base64

- **Status:** Proposed
- **Date:** 2026-11-12
- **Area:** contract
- **Source:** Issue #27
- **Supersedes:** —

## Context

The pipe's tick limits ([`pipe.md`](../areas/pipe.md), *Limits and errors*) guard the Companion against a hostile pipe:
a tick at its entry limit is about 200 MB of JSON, while the largest recorded tick has 1,934 entries and pairs
([`../protocol.md`](../protocol.md), *Sizes*), well under 1 MB. The server reads every message as untrusted input, may run
on a NAS, and with the token switched off reads anyone's messages ([0019](0019-websocket-transport-with-shared-token.md)).
Icons are PNG files of up to about 16.8 MB each, which the catalog document refers to by hash
([0055](0055-catalog-document-refers-to-icons-by-content-hash.md)).

## Options considered

1. **A bound that admits a tick at the pipe's limits (about 200 MB)** — no valid tick is ever refused; a server reads a
   200 MB message and its model into memory.
2. **Ticks split into parts that each fit a smaller bound** — every tick fits; the server reassembles parts, each part
   is acknowledged and buffered on its own, for ticks no game sends.
3. **One bound per message, 24 MiB, a tick above it refused by the writer** (chosen) — over 30 times the largest
   recorded tick, bounded server memory; a tick only a broken or hostile game sends is not delivered.
4. **Images and the catalog as binary WebSocket frames beside the JSON** — a third less traffic; a second, non-JSON
   message format with its own framing.
5. **Images and the catalog as Base64 strings in the JSON** (chosen) — one format and one reader; the catalog bytes keep
   their hash, which is the catalog's identity.

## Decision

Options 3 and 5. Every contract message has one byte bound, set by the largest icon image in Base64; a tick above it is
refused by the writer as *too large*. The bounds and the measured memory are in [`contract.md`](../areas/contract.md).

## Consequences

- A tick over the bound is lost and reported by the Companion (#40, #41); revisit when a recording comes near it.
- The largest read is a catalog upload, about 159 MiB managed at its peak (`contract.md`); a tick at the bound about
  71 MiB.
- For the endpoint (#28): it bounds a WebSocket message at the same size before the reader sees it, the first,
  unauthenticated message at 32 KiB (a hello), and the reads it runs at once, since with the token off N connections
  can hold N × about 159 MiB; the reader does not check direction, so the endpoint refuses a server-to-Companion type.
