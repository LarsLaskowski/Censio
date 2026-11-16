# 0088: Charts show the day up to a profile's latest tick and break a line after 15 minutes without a point

- **Status:** Proposed
- **Date:** 2026-11-16
- **Area:** web-charts
- **Source:** Issue #36
- **Supersedes:** —

## Context

The charts of #36 come before the range picker of #37, so they need a fixed range, and the issue asks that gaps stay
visible ([0031](0031-bounded-local-tick-buffer-gaps-accepted.md)). The series read has no point for time without a
tick, so a drawn line would join across any outage. The game sends a tick about every 2 minutes; the fixture shows
an 18-minute pause and a 9-minute savegame change. A replayed recording keeps its old receive times, and a profile
not played for a week keeps only its latest hour as ticks.

## Options considered

1. **The last 24 hours before the server's clock** — matches the calendar; a replay or an idle profile shows nothing.
2. **The 24 hours up to the profile's latest tick, from the start of their first hour** (chosen) — every profile
   shows its last day, as the pages show its latest tick; an hourly point at the start is shown whole.
3. **All history** — no choice to make; unbounded, which #37's "all" will have to bound anyway.

For a gap: **join every point** (outages invisible), **break after a fixed time** (chosen, 15 minutes: seven missed
ticks; the pause is a gap, the savegame change is not), or **break at every pause of the game time** (game time is
#37's axis).

## Decision

The charts show the points from the start of the UTC hour that holds the time 24 hours before the profile's latest
tick up to that tick, and a line breaks where a value is missing or where more than 15 minutes pass between the end of
one point and the start of the next. The rules are in [`web-charts.md`](../areas/web-charts.md).

## Consequences

- A replay shows at once; #37 replaces the fixed range with its picker.
- After the retention folds an hour, a pause inside it no longer shows; a missing hour stays a gap.
- A game that sends ticks less often than every 15 minutes would show only points; no such cadence is known.
