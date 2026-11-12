# 0063: A refused contract message is acknowledged by its id when the reader yields one, else the connection closes

- **Status:** Proposed
- **Date:** 2026-11-12
- **Area:** ingest
- **Source:** Issue #28
- **Supersedes:** —

## Context

The contract lets an acknowledgement carry a refusal kind for the message `id` it answers
([`contract.md`](../areas/contract.md)), but the reader returned no message on a refusal, so the endpoint could not name
the `id`. A catalog may be refused for a schema the server does not read, and a Companion would resend it forever without
an answer. The server keeps nothing it receives until #30.

## Options considered

1. **Close the connection on every refusal** — no reader change; a refused catalog upload is resent at every reconnect.
2. **A second, lenient parser in the endpoint that finds the `id`** — every refusal answered; a second reader of untrusted
   input beside the strict one.
3. **The reader returns the `id` of a message it built before refusing it, and the parsed catalog of an accepted upload**
   (chosen) — one reader, no second read of a catalog; malformed input still has no `id`.
4. **Acknowledge only once stored (#30)** — honest about storage; no Companion could be tested before #30.

## Decision

Option 3. The reader exposes the usable `id` of a refused Companion-to-server message and the catalog document of an
accepted upload; the endpoint acknowledges *invalid* and catalog refusals by that `id`, and closes the connection for
every other refusal. Until #30 an accepted message is acknowledged and not kept. Rules are in
[`ingest.md`](../areas/ingest.md).

## Consequences

- `missingIcons` of a catalog upload comes from the catalog the reader already built, so there is no second read: the
  peak is the reader's own plus the `missingIcons` list and the written reply, measured in `ingest.md` (*Memory of a
  catalog upload*).
- A Companion that sends malformed messages loses its connection; its writer refuses what the reader refuses.
- #30 changes what an acknowledgement promises: stored, not only checked.
