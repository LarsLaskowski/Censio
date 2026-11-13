# 0074: The pages keep only derived views and read profiles that are not held one at a time

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** web-pages
- **Source:** Issue #32
- **Supersedes:** —

## Context

The pages need no login (record [0006](0006-server-plain-http-and-open-health.md)) and can show any profile. A
profile the server does not hold is read from the database on demand, unbounded in number; a worst tick retains about
43 MiB ([`areas/server-state.md`](../areas/server-state.md), *Reading on demand*, *Memory*), and every open page of
Blazor Server keeps its component state for as long as it is open.

## Options considered

1. **Each page reads and keeps its profile state** — simple; memory and database reads grow with the number of open
   pages.
2. **A shared cache of the last state read** — one copy for many pages, but it outlives its pages, must be invalidated
   on ticks and catalogs, and two pages of two unheld profiles thrash it.
3. **One read at a time across the server, and pages that keep only a view derived from the state** — bounds the reads
   and the memory per page to what a page shows; a page of an unheld profile waits behind other reads.

## Decision

Option 3: reads of profiles that are not held pass one gate of one place, and a page keeps a view of figures, names
and at most 500 rows, holding no reference to a profile state once the view is built; a page that closes cancels its waiting or running read
([`areas/web-pages.md`](../areas/web-pages.md)).

## Consequences

A language switch re-renders from the view, but a catalog change re-reads. Many pages of unheld profiles queue behind
one another (they show `Loading…`); held profiles never wait.
