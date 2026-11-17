# 0086: Charts are drawn with MudBlazor's own time-series chart, not Blazor-ApexCharts

- **Status:** Accepted
- **Date:** 2026-11-16
- **Area:** web-charts
- **Source:** Issue #36
- **Supersedes:** 0025 — amends its chart library: the charts use MudBlazor's time-series chart instead of
  Blazor-ApexCharts; MudBlazor as the component library and the light default theme stay.

## Context

Record [0025](0025-mudblazor-and-blazor-apexcharts-light-theme.md) chose Blazor-ApexCharts for the charts. When #36
needed it, the wrapper (MIT) shipped ApexCharts 8.8.1, whose own licence is free only for organisations under
$2 million revenue and asks for a paid OEM licence to embed it in a product used by others; ApexCharts was MIT up to
4.7.0, which only the wrapper 6.0.x ships, built for .NET 8 and 9. Censio is MIT ([0028](0028-mit-licence.md)), is
redistributed as a Docker image, and moves to every current .NET major ([0054](0054-censio-moves-to-every-current-dotnet-major.md)).
ApexCharts also renders tooltips and legends as HTML in the browser, where island names, which the game's players
choose, would need escaping of our own. MudBlazor 9.13.0, already referenced, has a time-series chart that the server
renders as SVG through Blazor, which breaks a line at a point without a value; it writes its legend and a tooltip
template as escaped text, but its axis labels as raw markup.

## Options considered

1. **Blazor-ApexCharts 8 with ApexCharts 8.8.1** — the most polished, zoom and animation built in; a licence the
   project cannot pass on without a paid OEM licence.
2. **Blazor-ApexCharts 6.0.2 pinned with the MIT ApexCharts 4.7.0** — MIT; built for .NET 8 and 9 only, frozen without
   fixes, and every later update brings the licence back.
3. **Own SVG charts** — no dependency; axes, scaling, tooltips and legends to write and test ourselves.
4. **MudBlazor's own time-series chart** (chosen by the Product Manager) — MIT, already referenced, no script, legend
   and tooltip template rendered as text by Blazor, axis labels as raw markup, theme colours from the palette; no
   animation of its own, one value axis.

## Decision

The charts are MudBlazor's time-series chart (`MudChart` with `ChartType.Timeseries`), fed by the server; no chart
package or chart script is added. The rules of the charts are in [`web-charts.md`](../areas/web-charts.md).

## Consequences

- No new dependency and no new script; the page's resources stay those of [`web-ui.md`](../areas/web-ui.md).
- MudBlazor draws no animation: a style rule of our own reveals each line when a chart is drawn, as the Product
  Manager chose; no script is added for it.
- One value axis per chart: the overview's buildings and goods counts share it.
- The axis labels are raw markup, so they only ever take number and time text of our own formatting, never a name.
- A MudBlazor update can change the chart's markup; the page tests pin what the charts must show.
