# 0078: Times are shown at the browser's UTC offset for the instant

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32
- **Supersedes:** —

## Context

The pages show the receive time of the data. The Docker image has no culture data (record
[0072](0072-web-ui-texts-from-an-own-table-without-culture-data.md)) and no verified time-zone data, and the server's zone
is not the player's.

## Options considered

1. **UTC only** — no script, but every player converts in their head.
2. **The server's time zone** — needs tzdata in the image and is the wrong zone for a remote server.
3. **The browser's offset for the instant shown, by a script** — right across daylight saving; UTC is shown until the
   script answers.

## Decision

Option 3: a function of `js/censio.js` returns the browser's UTC offset for the instant, and the server formats the time
with it; without an answer the time is UTC, marked ` UTC` ([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

The first paint shows UTC for a moment. The script adds a function that reads the browser's clock settings.
