# 0048: Catalog names are shown in the diagnostics window; GUIDs the catalog cannot name are logged once per run

- **Status:** Proposed
- **Date:** 2026-11-09
- **Area:** companion
- **Source:** Issue #23
- **Supersedes:** —

## Context

Issue #23 asks that external testers can try the catalog before the server shows any game data (#26, #42), and that
unknown GUIDs are reported, not fatal. The Companion's only data view is the diagnostics window, which shows the
region and good of each row and no building or workforce column
([0038](0038-diagnostics-window-flat-table-of-rows-made-on-demand.md)). No game name may enter a log
([0023](0023-game-names-and-icons-extracted-locally.md)).

## Options considered

1. **Names in the diagnostics window, a `Catalog:` menu line, unresolved GUIDs in the log** — testers see names where
   they already see GUIDs; buildings and workforce are only checked through the log.
2. **A command-line switch that writes the catalog to a file the tester names** — every kind visible; a file of
   extracted game text on the tester's disk, and a format before #26.
3. **No visible change, only an opt-in test** — nothing for external testers.

## Decision

Option 1, chosen by the Product Manager: the `Region` and `Good` cells show both names with the GUID, the tray menu
shows the catalog's state, and every GUID of every closed tick in all four roles is checked, whether or not the
window is open, each unresolved one logged once per run by GUID, role and reason, never by name. The texts are in the
[companion](../areas/companion.md) area document.

## Consequences

- The log line per unresolved GUID is bounded per run, so a stream of invented GUIDs cannot flood the log.
- Ticks that close before the catalog is ready are not checked, except the latest one at the moment it is ready; a GUID seen only in earlier ticks is not reported.
- A catalog name is cut for display, so one hostile text cannot stall the window's layout.
- Building and workforce names stay invisible to testers until the web UI.
