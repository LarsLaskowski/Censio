# 0071: The web UI's theme and language are kept in two browser cookies written by a script and read when the page is rendered

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** web-ui
- **Source:** Issue #31
- **Supersedes:** —

## Context

The web UI has a light and a dark theme and German and English texts, and the choice is remembered per browser
(record [0025](0025-mudblazor-and-blazor-apexcharts-light-theme.md), issue #31). The spec asks for the chosen theme and
language from the first paint, with no light flash and no English flash. Blazor Server renders the first HTML on the
server before the interactive connection exists, and once it exists the server can no longer set a response header:
only the browser can write what it keeps. The server stores nothing about a browser (record
[0006](0006-server-plain-http-and-open-health.md): no login, no user).

## Options considered

1. **Browser local storage, read by the interactive component** — no cookie; the server's first HTML cannot know the
   choice, so a dark or German page first paints light and English and then switches.
2. **Cookies set by a server endpoint the switch navigates to (`/preferences?theme=dark&return=/`)** — the pattern
   ASP.NET Core documents for its culture cookie, no own script; a new route reachable without login, a full reload on
   every switch, and a return address the endpoint must confine against open redirects.
3. **Two cookies written by a small own script through JavaScript interop, read by the server when it renders the
   page** — no new route and no reload; the first HTML is already in the choice; an own browser script that no unit
   test covers.

## Decision

Option 3. The switch writes the cookie `censio-theme` (`light` or `dark`) or `censio-language` (`en` or `de`) through
the script `wwwroot/js/censio.js`, and the server reads both when it renders a page, taking only those exact values.
The cookie rules (lifetime, path, unknown values) are in [`areas/web-ui.md`](../areas/web-ui.md).

## Consequences

- The script is excluded from coverage in `sonar.coverage.exclusions` (`.squad/stack.md`, *Coverage exclusions*);
  its two functions are checked by hand and their calls by the component tests.
- The cookies are readable by scripts and not `Secure`, since the server speaks plain HTTP; they hold only a theme and
  a language, no identity.
- A browser that blocks cookies gets the defaults on every load; the switch still changes the open page.
- Revisit if the web UI gets a login with a per-user setting store.
