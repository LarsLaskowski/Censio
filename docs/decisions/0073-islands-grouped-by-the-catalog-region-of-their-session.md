# 0073: Islands are grouped by the catalog region of their session, a session without one forms its own region

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32
- **Supersedes:** —

## Context

An island's key carries a session GUID; the catalog document's region entries are session assets, each with the GUID of its
region asset, which several sessions can share ([`areas/game-catalog.md`](../areas/game-catalog.md), *Kinds*; record
[0049](0049-region-type-of-a-region-asset-from-its-template-and-the-property-defaults.md)). The issue names two regions
(Latium, Albion); the fixture has three session GUIDs. No region may be written into the code.

## Options considered

1. **One region per session GUID** — needs no catalog and is stable, but shows one region several times when it has
   several sessions, which the player reads as one region.
2. **Group by the region GUID of the session's catalog entry, a session without one on its own** — matches the game's
   regions; the grouping changes when a catalog arrives.
3. **List every catalog region, with or without islands** — shows regions the profile never settled, and sessions of
   the catalog that are no playable map.

## Decision

Option 2: islands group by the region GUID of their session's catalog region entry, named by the entry of the
region's smallest session GUID; a session without an entry is a region of its own, and only regions with islands are
listed ([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

Without a catalog the pages show one region per session (`Region <session GUID>`). Addresses name a session, not a
region, so a link stays valid when a catalog regroups the sessions. Region names that come out equal are told apart by
their GUID.
