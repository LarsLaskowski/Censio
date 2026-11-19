# 0090: The game-time axis places the range's points at their game time, without time labels

- **Status:** Accepted
- **Date:** 2026-11-17
- **Area:** web-charts
- **Source:** Issue #37
- **Supersedes:** —

## Context

The game time stops while the game is paused and the game sends no ticks then ([`protocol.md`](../protocol.md),
*Timestamp*, confirmed at 1x speed), so on a real-time axis every pause is a gap. Issue #37 asks for a switch to game time. The game
time is a number of milliseconds (confirmed at 1x speed) with no confirmed origin; the tick after the first load of a game start
carries 19,000 against about 1.75 × 10⁹ for the next one, and an hourly point keeps the smallest and largest game time of
its folded ticks, that tick's included. MudBlazor's time-series chart writes its axis labels only through a `DateTime`
format ([0086](0086-charts-drawn-with-mudblazors-own-time-series-chart.md)), which wraps at 24 hours and, with the day,
at a month. The Product Manager decided that a range covers the same receive-time period on both axes.

## Options considered

1. **Labels `d HH:mm` of the game time mapped onto a `DateTime`** — the chart can write them; they repeat after 31 days
   of game time, which `All` of a long savegame reaches.
2. **An own label row under MudBlazor's chart** — any label text; its alignment with the plot no unit test can pin.
3. **Another chart component** — free labels; reverses 0086.
4. **No time labels on the game-time axis; the tooltip names the real time and the game time** (chosen) — nothing
   misleading; the axis gives order and distance, not a reading.

For the placement: an hourly mean **at its smallest game time** (can be the load tick's 19,000) or **at its largest**
(chosen); the tick in which no island holds an entry **kept as a point** (stretches the axis) or **left out** (chosen).

## Decision

Option 4. On `Game time` each point of the chosen range sits at its game time (an hourly mean at its largest), a tick
without any entry is left out, a line breaks after 15 minutes of game time without a point, the axis has no time labels,
and the tooltip adds `Game time <h>:<mm>`. The rules are in [`web-charts.md`](../areas/web-charts.md).

## Consequences

- A pause no longer shows as a gap on `Game time`; an outage while the game runs still does.
- Reading a time off the game-time axis needs the tooltip. MudBlazor 9.13.0 draws no vertical grid lines on either axis,
  so the game-time axis has no guide on x at all; only the value axis's horizontal grid lines remain. An own guide row was
  not added, for the reason of option 2.
- Should the game time's unit prove other than milliseconds, the tooltip's game time and the 15-minute rule are off by
  that factor.
