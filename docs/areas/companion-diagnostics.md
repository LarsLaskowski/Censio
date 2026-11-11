# Companion diagnostics window

## Scope

The diagnostics window of the Companion: its header, table, numbers, catalog names, icons and area names, how it
follows the ticks, its single window, its read path, memory and privacy. The user who opens it from the tray menu relies on it.

## Behavior


`Open diagnostics` opens one window, titled `Censio Companion: Diagnostics`, with the latest closed tick (record
[0038](../decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md)). It shows every value as the game
sent it: GUIDs are raw numbers where the catalog has no name for them; icons are shown where the game's files give one.

- **Header**, top to bottom: `Status: <status text>`; `Last tick: <time>, <n> islands` or `Last tick: none`, exactly the
  text of the menu entry of the same name ([`companion.md`](companion.md), *Tray icon and menu*); and, once a tick exists, `Game time: <game time>`, with
  ` (no data yet)` appended when every island of the tick has no entries. Before the first tick, and with an invalid
  command line, the header holds `Status:` and `Last tick: none` only, and the table is empty.
- **Table**, 17 columns left to right: `Region` (session GUID), `Island` (island ID), `Area` (area index), `Session ID`,
  `Name` (area name), `Good` (product GUID), `Generation`, `Consumption`, `Delta`, `Perfect generation`, `Perfect
  consumption`, `Buildings`, `Maintenance`, `Income`, `Profit`, `Productivity (sum)`, `Productivity (avg)`. Rows follow
  the tick: islands in the order the tick holds them, the goods of an island in frame order, the region and island
  columns repeated on every row of the island. An island without entries has one row with `(no entries)` in `Good` and
  the value columns empty. The table cannot be sorted, edited or copied.
- **Numbers** are culture-independent, also on a German Windows: integers in decimal with a leading `-` when negative,
  floats with the decimal point `.`, no digit grouping, as the shortest text that reads back as the same 32-bit value
  (`0.1`, `-2.5`, `1E+38`), negative zero as `-0`, and `NaN`, `Infinity`, `-Infinity`.
- **Catalog names:** once the catalog is ready, the `Region` and `Good` cells of a GUID the catalog names read `<English
  name> / <German name> (<GUID>)`; with one name only (an incomplete entry) `<name> (<GUID>)`. A GUID the catalog cannot
  name (unknown, nameless, ambiguous), GUID `0` and every cell while no catalog is ready show the bare number. The names
  pass the rule of the log ([`companion-log.md`](companion-log.md)) and a name over 4,096 UTF-16 characters is cut there, ending in `…`; a name of
  exactly 4,096 is kept whole. Buildings and workforce have no column.
- **Icons:** once the icons are built ([`game-catalog-icons.md`](game-catalog-icons.md)), the `Region` and `Good` cells show
  the icon of the cell's GUID before the text: an image of 20 by 20 device-independent pixels, then a gap of 4, then the
  text, vertically centred. In the `Region` column the icon sits on a dark round backdrop of that size (the game's region
  icons are light glyphs); the `Good` icon has none. A GUID without an icon (none, unknown, a problem of the icon, GUID `0`) and every cell while
  no icons are built show the text alone, without the image's place, backdrop or gap.
  The names are published first: the window shows them as soon as the catalog is ready and adds the icons when they are
  built, and a new build of the catalog removes the icons until the new ones are ready. Each distinct icon is decoded once, at twice the size of the
  cell (sharp up to 200 % display scaling), and shared by the rows that show it. The icons are held in memory and written only to the catalog cache ([`game-catalog-cache.md`](game-catalog-cache.md)): the window writes nothing of them to disk, nothing of them is logged or sent, and a screenshot holds the game's icons.
- **Area names** (the `Name` column) pass the rule of the log ([`companion-log.md`](companion-log.md)): each control character, format character, line
  separator, paragraph separator (the categories Cc, Cf, Zl, Zp) and unpaired surrogate is shown as one `?`, nothing
  else is changed (combining marks, right-to-left letters and spaces stay as sent), and an area name is never cut, also
  at the longest pipe string of 255 bytes. Each cell is its own text, so a name cannot reorder another cell.
- **Update:** the window follows each closed tick, each change of the status text, and the next local midnight (when
  `Last tick:` changes from `HH:mm:ss` to the dated form). At most one update waits at a time; with ticks closing faster
  than the window shows them (a replay with `--speed max`) intermediate ticks may be skipped and the window always ends
  with the latest tick. The scroll position (vertical and horizontal) is kept as far as the new table reaches.
- **Kept tick:** opening the window shows the latest closed tick at once, also one that closed while no window was open
  (the connection and the replay keep it, [`pipe-connection.md`](pipe-connection.md), *Latest tick*).
- **Close and single window:** the close button and Alt+F4 close only the window; the icon, the connection, a running
  recording or replay, the tooltip and the menu go on. `Open diagnostics` with a window open brings that window to the
  front (restored when minimized) and opens no second one; after a close it opens a new one. `Exit` closes the window with
  the icon and keeps its 2-second bound with the window open.
- **Read path:** handing a closed tick to the window only stores it and signals; its text is made on the window's thread,
  never on the thread that reads the pipe or the recording. Receive times, states, ticks and recorded frames are the same
  with the window open, closed or stalled.
- **Memory:** the window makes text only for the rows on screen. At most three ticks are referenced: the open one, the
  kept one and the one shown; a replaced or a closed window references none.
- **Privacy:** the window shows island names, economic data and the game's icons on screen only. Nothing of it is written to disk, logged
  or sent anywhere, and the window adds no log line. A screenshot holds the savegame's island names.

## Limits and errors

- The window shows no error message; a GUID it cannot name shows as the bare number, and the window adds no log line.

## Related decisions

- [`decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md`](../decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md) — the diagnostics window is a flat table whose rows are made on demand from the latest tick, kept whole.
- [`decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md`](../decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md) — names in the diagnostics window, unresolved GUIDs logged once.

## Not here

- The menu entry `Open diagnostics`, the tooltip and the other Companion behavior: [`companion.md`](companion.md).
- The log file and its lines: [`companion-log.md`](companion-log.md).
- Building the catalog and its names: [`game-catalog.md`](game-catalog.md); the icons: [`game-catalog-icons.md`](game-catalog-icons.md); the cache: [`game-catalog-cache.md`](game-catalog-cache.md).
- The latest tick the window shows: [`pipe-connection.md`](pipe-connection.md) (*Latest tick*).
