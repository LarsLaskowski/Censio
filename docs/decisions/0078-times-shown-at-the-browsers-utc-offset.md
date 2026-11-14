# 0078: Times are shown at the browser's UTC offset for the instant

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32, issue #200
- **Supersedes:** —

## Context

The pages show the receive time of the data. The Docker image has no culture data (record
[0072](0072-web-ui-texts-from-an-own-table-without-culture-data.md)) and no verified time-zone data, and the server's zone
is not the player's. The browser's offset is known only once the interactive circuit runs and the browser has answered;
the server's prerender and the first interactive render come before that answer.

## Options considered

1. **UTC only** — no script, but every player converts in their head.
2. **The server's time zone** — needs tzdata in the image and is the wrong zone for a remote server.
3. **The browser's offset for the instant shown, by a script, UTC until it answers** — right across daylight saving,
   but every load shows the UTC time first and then switches (issue #200).
4. **The browser's offset by a script, no time until its first answer** — right across daylight saving and no wrong
   time on screen; the line stays empty for one round trip and, without a running circuit, for good.
5. **The browser's offset kept in a cookie and read at render time** — the prerender could show the local time, but a
   first visit still has no cookie, the cookie is one more untrusted input, and an offset taken at another instant is
   wrong across a daylight-saving change.

## Decision

Option 4: a function of `js/censio.js` returns the browser's UTC offset for the instant, and the server formats the time
with it; until the browser's first answer the as-of line shows no time, and a failed or unusable answer shows UTC,
marked ` UTC` ([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

A load shows the as-of line empty until the browser answers, never a UTC time that is then replaced; a page whose
circuit never starts (scripts off) shows no time. The script adds a function that reads the browser's clock settings.
