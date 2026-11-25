# 0114: Start with Windows is switched only in the settings window

- **Status:** Proposed
- **Date:** 2026-11-25
- **Area:** companion
- **Source:** Issue #43
- **Supersedes:** 0017 (amends it: the autostart is switched by the settings window's box, not by a tray menu entry
  the menu reads each time it opens; how it is stored stays as 0017 decides)

## Context

Record [0017](0017-companion-tray-app-autostart-and-connect.md) put `Start with Windows` in the tray menu as a checked
entry that reads the Run value each time the menu opens. #43's settings window shows the same switch as a box
([companion-settings](../areas/companion-settings.md)). In the Product Manager's manual test the option stood in both
places.

## Options considered

1. **Keep both** — one click in the menu still works; two switches for one value, and the menu reads the registry at
   every opening.
2. **Only the settings window** — one place for every option; turning it on takes the window and `Save`; the menu
   no longer reads the registry.
3. **Only the menu** — the window loses a field it was built with and still would not hold every option.

## Decision

Option 2, the Product Manager's decision: the tray menu has no `Start with Windows` entry and no check mark; the
window's box is read from the Run value at each opening (`CompanionAutostart.Refresh`) and switches it on `Save`
([companion](../areas/companion.md), *Start with Windows*).

## Consequences

- A value changed outside the Companion (the Registry Editor, another copy of the exe) shows at the next opening of
  the window; Windows' startup apps switch stays unread, as 0017 decides.
- A failed change shows the window's message instead of passing silently as the menu's click did.
- The tray menu no longer needs a check mark: `TrayMenuEntry` loses `IsChecked`.
