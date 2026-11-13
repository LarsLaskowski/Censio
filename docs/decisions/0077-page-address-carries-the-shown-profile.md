# 0077: The page address carries the shown profile

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32
- **Supersedes:** —

## Context

The web UI's profile switch chooses which profile's data a page shows (record
[0041](0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)). The theme and language live in cookies that
carry no identity (record [0071](0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md)).

## Options considered

1. **A cookie** — survives a new tab, but carries an identity, and two tabs cannot show two profiles.
2. **The page's circuit only** — lost at every reload.
3. **The address** — kept by reload, bookmark and copied link, per tab; the region overview without one follows the
   newest profile.

## Decision

Option 3: a picked profile is part of the page address; `/regions` shows the profile with the newest tick and follows
it ([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

The address is untrusted input, parsed whole by own code; anything unknown shows `Not found.`
