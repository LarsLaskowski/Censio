# 0032: Telegram alerts and a mobile layout come later

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #52 (Telegram alerts), Issue #53 (mobile and tablet layout)
- **Supersedes:** —

## Context

Two wishes came up in planning: an alert on the phone when a good runs short (through Telegram), and pages that work
on a phone or tablet. Neither is needed to read, store and show the game data, and Telegram alerts would be the
server's first outbound call (`.squad/project.md`, *Security areas*: none planned) and need a bot token to store.

## Options considered

1. **Build them in the first versions** — earlier value for some users; delays the core and adds an outbound call and
   a secret before anything else works.
2. **Defer them to milestone M9 (*Later*), as placeholder issues refined when scheduled** — the core comes first;
   the UI is built for the desktop browser until then.

## Decision

Option 2. Telegram alerts (#52) and a mobile and tablet layout (#53) are not part of the releases up to 1.0; until
they are scheduled the server makes no outbound call and the UI targets desktop browsers.

## Consequences

- Pages may be unpleasant on a phone until #53.
- When #52 is scheduled, its outbound call and its bot token are new security areas, reviewed as such.
