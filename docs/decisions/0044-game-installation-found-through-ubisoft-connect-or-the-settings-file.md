# 0044: The game installation is found through Ubisoft Connect's records or the settings file; Steam is not searched

- **Status:** Proposed
- **Date:** 2026-11-09
- **Area:** game-installation
- **Source:** Issue #22
- **Supersedes:** —

## Context

The catalog (#23 to #26) reads the game's archives below the installation folder, which nothing finds yet. The issue
asks for Ubisoft Connect and Steam, registry and default folders, a manual override, and a clear message for a wrong
path. Anno 117 is sold through Ubisoft Connect and Steam; the Product Manager plays through Ubisoft Connect and
supplied a sample of that installation only. The Companion shows no dialog for any problem
([companion](../areas/companion.md), *Limits and errors*).

## Options considered

1. **Ubisoft Connect and Steam, each from its records and default folder** — covers both stores; Steam's library
   list and app manifest would be read without a real sample, so its reader could pass its tests and still be wrong.
2. **Ubisoft Connect's records and default games folder, with a folder in the settings file overriding the search;
   Steam not searched** — built from a real sample; a Steam player names the folder by hand.
3. **Only a folder named by the player** — no store formats at all; every player has to look up and type the folder.
4. **A dialog for a missing or wrong folder** — hard to miss; the only dialog of a tray program that otherwise reports
   problems in the menu, the tooltip and the log.

## Decision

Option 2, with the result shown as a greyed-out menu line and the detail in the log (not option 4). The Product
Manager decided that Steam is not searched and gets no follow-up issue: it is done at most later, once the software
is finished and a Steam user has a problem. A folder in the settings file that is wrong is reported and never
replaced by a searched one.

## Consequences

- A Steam player, and a player whose Ubisoft Connect record is missing outside the default games folder, names the
  folder in the settings file ([0045](0045-companion-settings-in-a-json-file-under-local-app-data.md)).
- The records are read as the Product Manager's export of the key shows them; a record never leads to a network or
  device path.
- Revisiting Steam needs a real sample of Steam's library list and app manifest.
- The rules are in [game-installation](../areas/game-installation.md).
