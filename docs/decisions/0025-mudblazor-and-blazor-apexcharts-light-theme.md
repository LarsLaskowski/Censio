# 0025: The web UI uses MudBlazor and Blazor-ApexCharts; the light theme is the default

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #31 (layout and theming), Issue #36 (trend charts)
- **Supersedes:** —

## Context

The UI should look modern and polished from the start and shows tables, live values and trend charts. It is Blazor
Server only ([0016](0016-csharp-dotnet-10-blazor-server-only.md)); building a component set and a chart library of
our own is out of proportion.

## Options considered

1. **Plain Bootstrap of the project template and hand-made SVG charts** — no dependency; much own UI code and an
   unpolished look.
2. **A commercial component suite** — complete; a licence cost and terms an open-source project cannot pass on.
3. **MudBlazor for layout and components, Blazor-ApexCharts for charts** — Material design in pure Blazor with a
   theme provider (light and dark), and a Blazor wrapper around ApexCharts for zoomable time series.

## Decision

Option 3. The UI is built with MudBlazor and its charts with Blazor-ApexCharts; the light theme is the default, the
dark theme can be chosen and the choice is remembered per browser (#31).

## Consequences

- Two UI dependencies, added through Central Package Management when #31 and #36 need them; each one's licence is
  checked when it is added.
- ApexCharts runs JavaScript in the browser through its wrapper; the page's script sources grow accordingly.
