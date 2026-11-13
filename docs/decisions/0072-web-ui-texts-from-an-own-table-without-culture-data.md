# 0072: The web UI's texts come from an own table per language, without .NET culture data

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** web-ui
- **Source:** Issue #31
- **Supersedes:** —

## Context

The web UI shows its texts in German or English and switches without a reload (issue #31). The server's Docker image
runs with invariant globalization and has no ICU culture data ([`areas/server.md`](../areas/server.md), *Docker
image*); there, creating the culture `de` fails by default. A Blazor Server page keeps the culture of the request
that opened it, so a culture change takes effect only after a reload.

## Options considered

1. **Resource files with `IStringLocalizer` and ASP.NET Core's request localization** — the framework's way, with
   tooling for translators; depends on culture objects the image lacks, needs a reload to switch, and the tests pass
   on a machine with ICU while the image fails.
2. **Turn culture data on in the image (ICU packages in the runtime stage)** — the framework's way works; a larger
   image and a change of the pinned base's contents for a few texts.
3. **An own table of texts per language, chosen by the component from the current choice** — no culture data,
   switches at once, each text checked by a unit test; translations are edited in code.

## Decision

Option 3. Every text of the web UI comes from one table with an English and a German entry; no web UI type creates a
culture object or sets the request culture. The texts are listed in [`areas/web-ui.md`](../areas/web-ui.md).

## Consequences

- The server's tests run with invariant globalization like the image, so a later use of culture data fails there too.
- Numbers and dates on later pages (#32) are formatted without culture data as well; #32 decides how.
- MudBlazor's own few texts stay in its default language.
- Revisit when the web UI needs more languages or a translator's workflow.
