# 0081: Pages resolve their resources against a fixed document base of `/`

- **Status:** Proposed
- **Date:** 2026-11-14
- **Area:** web-ui
- **Source:** Issue #199
- **Supersedes:** —

## Context

`App.razor` writes the style sheets and scripts as relative paths (`@Assets[...]`) and had no `<base>` element, so a
full load of a page below the first path segment (`/profiles/<profile>/islands/...`) resolved them below the page's
own path and the server answered 404: the page showed without styles. `blazor.web.js` also reads `document.baseURI`
for the circuit's base address and to decide which links it follows without a full load, so the base matters beyond
the assets. Nothing in `Program.cs` sets a path base (no `UsePathBase`, no forwarded headers), and every in-app link,
the icon route and the preference cookie are root-relative (`/regions`, `/icons/...`, `Path=/`).

## Options considered

1. **A fixed `<base href="/" />` in the document head** — one element, the form of the Blazor template; every
   relative resource and the Blazor base resolve against the root. Behind a path base it would be wrong, but so would
   every root-relative link of the pages.
2. **A base that follows the request's path base** (`PathBase` + `/`) — what the issue suggests; it alone does not
   make the pages work behind a path base (the root-relative links, icons and cookie path stay), and it puts a
   request-derived value into the element that decides where every script is loaded from: a path base taken from
   a forwarded prefix such as `//host` would load scripts from another host.
3. **Root-relative resource paths without a base** (`/` before each `@Assets[...]`) — fixes the assets only:
   `document.baseURI` stays the page's address, so Blazor's base and link handling stay wrong on a deep page, and
   every future resource has to remember the slash.

## Decision

Option 1: the document head carries `<base href="/" />` before any resource, and the resources stay relative to it
([`areas/web-ui.md`](../areas/web-ui.md), *Resources*).

## Consequences

Every page loads the same resource addresses at any URL depth. Running the server under a path base is not supported;
a change that adds one revisits this record together with the root-relative links, the icon route and the cookie path.
