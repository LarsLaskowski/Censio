# 0076: Icons are served by their content hash after a PNG header check

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32
- **Supersedes:** —

## Context

The pages show the game's icons, which the Companion uploads as PNG data that the contract carries, hashes and bounds
but does not validate ([`areas/contract.md`](../areas/contract.md)). A browser must never receive them as anything but an
image, and every resource comes from the server itself ([`areas/web-ui.md`](../areas/web-ui.md), *Resources*).

## Options considered

1. **Inline `data:` URIs in the page** — no new route, but every icon travels through the page connection at every
   update and is never cached.
2. **A route by content hash, with a header check** — one new route without login, cacheable forever since the hash is
   the content; the check is cheap.
3. **The same route with a full PNG decode** — catches damaged images, but runs a decoder on uploaded data in the
   server.

## Decision

Option 2: `/icons/{reference}` serves a stored icon whose bytes start with the PNG signature and an `IHDR` chunk, as
`image/png` with `nosniff`, a content security policy that allows nothing and an immutable cache lifetime, one read at a time; anything else is `404`
([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

A damaged PNG that passes the header check reaches the browser, whose decoder handles it. Icons are public to anyone
who reaches the server, like the pages.
