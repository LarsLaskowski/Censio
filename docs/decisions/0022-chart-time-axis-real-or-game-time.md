# 0022: The chart time axis switches between real time and game time

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #35 (time series), Issue #37 (range picker and switch)
- **Supersedes:** —

## Context

Every tick has two times: when it was received and the game's own timestamp. The game time stops while the game is
paused or closed, so a chart over real time shows flat stretches and gaps, and a chart over game time shows the
economy's development without them but cannot be matched to the calendar.

## Options considered

1. **Real time only** — matches the calendar and the gaps of a real outage; pauses look like stagnation.
2. **Game time only** — shows play without pauses; "yesterday evening" cannot be found.
3. **Store both, let the user switch the axis** — both views; both times are stored per point.

## Decision

Option 3. Each history point keeps the real receive time and the game time (#35), and the user switches every chart
on a page between the two; the choice is remembered (#37).

## Consequences

- The contract carries both times per tick ([0018](0018-pipe-parsed-in-companion-versioned-json-contract.md), #27).
- The retention's hourly averages ([0021](0021-sqlite-history-with-retention.md)) must keep a game time per point as
  well; how #35 decides.
