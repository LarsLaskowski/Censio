# 0038: The diagnostics window is a flat table whose rows are made on demand from the latest tick, kept whole

- **Status:** Proposed
- **Date:** 2026-11-03
- **Area:** companion
- **Source:** Issue #14
- **Supersedes:** —

## Context

Issue #14 asks for a window that lists the regions, islands and goods of the last tick with raw GUIDs, opened from
the tray menu and updated on each tick, before names and icons exist (M3). A tick can hold up to 256 islands and
524,288 entries and map pairs ([`areas/pipe.md`](../areas/pipe.md), *Limits and errors*), real ones a few hundred
rows. The tick arrives on the thread that reads the pipe or the recording, and that thread must keep requesting or
frames are stamped late (*Order and end*). The window's UI is WPF in the shell, which has no test project
([0004](0004-companion-shell-and-linux-build.md)); WPF was chosen for exactly such windows
([0017](0017-companion-tray-app-autostart-and-connect.md)). The window must also show a tick that closed while it
was closed.

## Options considered

1. **A flat table over a read-only row list in `Censio.Companion.Core` that makes a row's text only when the row is
   asked for; the WPF grid virtualizes rows; the connection and the replay keep the latest tick whole; ticks reach
   the window through the tray's coalescing updater** — memory that does not grow with the row count beyond the
   tick itself, all text rules unit-tested on Linux, the reading thread only signals. The grid is handed a collection view of its own, so WPF caches no view that keeps old ticks. Relies on the WPF grid asking
   only for the rows on screen, which only a Windows run shows.
2. **A tree (region, island, good)** — follows the issue's wording; a tree has no column headers for the eleven
   values, virtualizes expanded nodes poorly and loses its place when the content is replaced.
3. **All rows built up front as view models** — the simplest WPF binding; at the limits about nine million strings,
   several times the tick's own memory, built on every tick.
4. **The window subscribes to ticks only while it is open** — nothing kept beyond the tray's summary; a window
   opened after a tick shows nothing until the next one, about two minutes later.

## Decision

Option 1: the window shows one flat table whose rows are made on demand from the latest tick, which the live
connection and the replay keep whole; its content and its open, update and close rules live in
`Censio.Companion.Core`, and the shell only renders them. The behavior is written in
[`areas/companion.md`](../areas/companion.md), *Diagnostics window*.

## Consequences

- The Companion keeps one more tick in memory, at most the bound of an open tick.
- The row list must stay index-based (no sorting or grouping in the grid), or the grid would read every row.
- The grid gets the row list wrapped in its own `ListCollectionView`, never the list itself: WPF caches the view of a
  plain list in its view table and would keep replaced ticks, and the last one after the window closed, alive. A closed
  window drops its rows.
- That the grid really reads only the visible rows, keeps the scroll position and leaves the tray responsive at the
  limits is verified by the maintainer's run on Windows, not by a unit test.
- Names and icons (M3) can later replace the GUID cells without changing the structure.
