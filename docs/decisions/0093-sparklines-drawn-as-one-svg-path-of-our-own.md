# 0093: Sparklines are drawn as one SVG path of our own

- **Status:** Accepted
- **Date:** 2026-11-18
- **Area:** web-trends
- **Source:** Issue #38
- **Supersedes:** —

## Context

The charts use MudBlazor's time-series chart ([0086](0086-charts-drawn-with-mudblazors-own-time-series-chart.md)), which
draws a hover point per point, axes, a legend and a tooltip; a chart of 100 points renders a batch of about 120 KB
([`web-trends.md`](../areas/web-trends.md), *Limits and errors*). A table of 500 rows with up to 240 points each
re-renders on every tick ([0091](0091-row-trends-show-delta-and-buildings-over-a-fixed-four-hours.md)).

## Options considered

1. **MudBlazor's chart per row, its extras hidden** — one library for all lines; its hover points and markup stay in the
   render batch, roughly 60 MB a tick for 500 rows.
2. **A sparkline component of another library** — a new dependency for one SVG path.
3. **One SVG path per row, built on the server from coordinates only** (chosen) — at most 4,000 characters a row, no
   script, no hover points; no name ever reaches its markup.

## Decision

A sparkline is one `svg` element holding one `path` whose data the server writes from the points' times and values,
hidden from the accessibility tree; the arrow beside it carries the trend's name. The layout and limits are in
[`web-trends.md`](../areas/web-trends.md), *Sparklines and trend arrows*.

## Consequences

The sparkline has no tooltip, no axes and no animation of its own; a reader wanting values opens the chart. Its colour is
the palette's secondary colour, checked for 3:1 with the charts' line colours.
