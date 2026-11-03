# Spec: Diagnostics window for the last tick

Source: Issue #14
Status: Approved by Lead

## Problem

A tester sees today only the state, receive time and island count of the last tick
([`companion.md`](../../docs/areas/companion.md), *Tray icon and menu*); which goods and values the game sent cannot
be checked before the server UI (M4) and the names (M3), and replayed ticks have no consumer. A tester needs a raw
view of the last tick, live and in a replay, to see that data arrives and to compare a run with its recording.

## Scenarios

- S1: Given the Companion runs live and a tick has closed, when the user chooses `Open diagnostics` in the tray
  menu, then a window titled `Censio Companion: Diagnostics` opens and shows that tick: a header and a table with
  one row per good of every island of the tick (layout under *Window content* below).
- S2: Given the window is open, when the next tick closes, then the window shows the new tick in place of the old
  one, without any action by the user; the header's `Status:` line follows the connection state as the menu's
  status entry does, and its `Last tick:` line switches from `HH:mm:ss` to the dated form at the next local
  midnight, as the tooltip does.
- S3: Given the window is open, when the user closes it (its close button or Alt+F4), then only the window closes:
  the tray icon stays, the connection, a running recording or replay go on, and the tooltip and menu keep
  updating. Choosing `Open diagnostics` again opens the window with the last tick at once, also when that tick
  closed while no window was open.
- S4: Given the Companion replays a recording (`--replay <file>`, any `--speed`), when the user opens the window,
  then it shows the latest replayed tick and follows the replay tick by tick; after `Replay finished` it keeps
  showing the last tick of the file. For the same frames the `Last tick:` and `Game time:` lines and the table are
  the same live and in the replay ([`pipe.md`](../../docs/areas/pipe.md), *Replay*); the `Status:` line differs on
  purpose (`Replay: Receiving`).
- S5: Given the window is open, when the user chooses `Exit` in the tray menu, then the window closes with the icon
  and the Companion ends within 2 seconds with exit code 0, as without the window.

### Window content

- Header, top to bottom: `Status: <status text>` and `Last tick: <time>, <n> islands` (or `Last tick: none`),
  each exactly the text of the menu entry of the same name, then `Game time: <game time>` with ` (no data yet)`
  appended when every island of the tick has no entries. Before the first tick the header holds `Status:` and
  `Last tick: none` only.
- Table columns, left to right: `Region` (session GUID), `Island` (island ID), `Area` (area index), `Session ID`,
  `Name` (area name), `Good` (product GUID), `Generation`, `Consumption`, `Delta`, `Perfect generation`, `Perfect
  consumption`, `Buildings`, `Maintenance`, `Income`, `Profit`, `Productivity (sum)`, `Productivity (avg)`.
- Rows: islands in the order the tick holds them (the frame order), the goods of an island in frame order; the
  region and island columns repeat on every row of the island. An island without entries has one row with its
  region and island columns, `(no entries)` in `Good` and the value columns empty.
- Numbers are raw, culture-independent: integers (GUIDs, IDs, counts, maintenance, profit, game time) in decimal
  with a leading `-` when negative; floats with the decimal point `.`, no digit grouping, as the shortest text that
  reads back as the same 32-bit value (`0.1`, `-2.5`, `1E+38`), negative zero as `-0` (not normalised to `0`), and
  `NaN`, `Infinity`, `-Infinity` — the same on a German Windows.

## Errors and edge cases

- E1: The Companion was started with an invalid command line: `Open diagnostics` works; the window shows `Status:
  Invalid command line`, `Last tick: none` and an empty table.
- E2: No tick has closed yet (also `Receiving` before the first close, [`companion.md`](../../docs/areas/companion.md)):
  the window shows the header without `Game time:` and an empty table; it fills when the first tick closes.
- E3: The connection ends or the replay stops with `Replay error: <reason>`: the window keeps the last tick, as the
  menu's `Last tick` does, and only its `Status:` line changes.
- E4: `Open diagnostics` while the window is already open (also minimized or behind another window): no second
  window; the open one is restored and brought to the front.
- E5: An island name holds a control character, a format character (a bidi override such as `U+202E`, a zero-width
  character), a line or paragraph separator or an unpaired surrogate: each is shown as one `?`, as in the log
  ([`companion.md`](../../docs/areas/companion.md), *Log*), so a name cannot break a row or reorder the text around it.
  A name that is empty or only spaces is shown as sent (spaces kept, as the pipe delivers them).
- E6: Ticks close faster than the window can show them (a replay with `--speed max`): the window may skip
  intermediate ticks but always ends with the latest one; the tray menu and `Exit` stay responsive.
- E7: A tick at the pipe's limits (256 islands, 524,288 entries and map pairs together, [`pipe.md`](../../docs/areas/pipe.md),
  *Limits and errors*): the window shows all its rows and can be scrolled; showing or replacing it does not block the
  tray menu, the tooltip or `Exit`.
- E8: A tick arrives while the user has scrolled down: the scroll position stays where it was, as far as the new
  table reaches.

## Non-functional requirements

- Privacy: the window shows island names and economic data as the game sent them, on screen only. Nothing of the
  window is written to disk, logged or sent anywhere; no new log line. A screenshot of the window holds the
  savegame's island names, and the user guide says so where it asks for problem reports.
- Memory: the latest closed tick is kept whole, window open or not (S3), across connections; at the limits that
  adds one open tick's bound ([`pipe.md`](../../docs/areas/pipe.md), *Limits and errors*). With the window closed
  nothing else is held. Open, it makes text only for the rows on screen and releases the
  replaced tick: at most three ticks are referenced (open, kept, shown), a closed window none.
- Read path: handing a closed tick to the window only stores it as the latest and signals; its text is made on the
  window's thread, never on the thread that reads the pipe or the recording. Receive times, states, ticks and
  recorded frames are the same with the window open, closed or stalled ([`pipe.md`](../../docs/areas/pipe.md),
  *Order and end*).
- Responsiveness: the window never makes the tray wait (S2, S5, E6, E7); `Exit` keeps its 2-second bound in every
  state with the window open or closed.
- Verification: the shell has no test project (record 0004), so every rule a unit test can check is decided in the
  platform-neutral Companion logic; the rest is the manual run (*Acceptance criteria*).
- One window at most (E4). Its size and position are not remembered; there is no setting.
- Compatibility: as the rest of the Companion; the release ZIP still holds one file; the tooltip is unchanged.
- While the window is open it has a taskbar button; closed, the Companion again shows none.

## Acceptance criteria

Each criterion names its check: *unit* (a unit test of the platform-neutral Companion logic) or *manual* (the
maintainer's run on Windows, listed under "Not verified yet" in `companion.md` until it is done).

- [ ] AC1: The tray menu holds `Open diagnostics`, enabled in every mode (live, `--record`, replay, invalid command
  line), directly after the first separator; every other entry keeps its text and order (S1, E1). *Unit.*
- [ ] AC2: For a tick, the header lines and the table rows are exactly as *Window content* states — columns and
  their order, island and good order, the `(no entries)` row, ` (no data yet)`, and the number formats with `NaN`,
  `Infinity`, `-Infinity`, `-0`, negative values and the decimal point `.` under a German culture (S1). *Unit.*
- [ ] AC3: A recording replayed through the Companion's replay ends with the window's `Last tick:` and `Game time:`
  lines and table for its last tick holding exactly the receive time, game time, GUIDs and values of that tick's
  frames, at speed `max` and at a finite speed, and the same frames fed as a live connection give the same lines and
  table; the `Status:` line is not compared (S4, E6). *Unit.*
- [ ] AC4: Each closed tick replaces the window's content with the new tick, each change of the status replaces its
  `Status:` line, and the `Last tick:` line is made again at the next local midnight; with ticks closing faster than
  the window updates, at most one update waits and the content after the last tick is that tick (S2, E6). *Unit.*
- [ ] AC5: With the window closed the latest tick is still kept, the connection or replay and a running recording
  go on, and opening the window shows the kept tick at once, including a tick that closed while no window was open
  (S3). *Unit*; that the close button and Alt+F4 close only the window and leave the tray icon: *manual*.
- [ ] AC6: `Exit` with the window open closes it and ends the Companion within 2 seconds with exit code 0 (S5).
  *Manual.*
- [ ] AC7: Before the first tick, and with an invalid command line, the window shows `Status: <status text>`,
  `Last tick: none`, no `Game time:` line and an empty table; after the connection ends or a replay error it keeps
  the last tick (E1, E2, E3). *Unit.*
- [ ] AC8: `Open diagnostics` while a window is open asks to bring that window to the front instead of creating a
  second one, and after the window closed it creates a new one (E4). *Unit*; that the window really comes to the
  front, restored when minimized: *manual*.
- [ ] AC9: In an island name each control character, format character, line or paragraph separator and unpaired
  surrogate is shown as one `?`, by the same rule as the log, and a name of the longest pipe string (255 bytes) is
  never cut; other characters, spaces included, are shown unchanged (E5). *Unit.*
- [ ] AC10: For a tick at the limits of E7 the window's content gives the row count and the text of any one row
  without making the text of the other rows, and no row text exists while the window is closed (E7). *Unit*; that
  such a tick scrolls, that the tray menu opens and `Exit` keeps its bound while it is shown or replaced, and that a
  new tick keeps the scroll position (E8): *manual*.
- [ ] AC11: README and user guide describe `Open diagnostics` and the window, English first, then German, so that
  an external tester can open it live and in a replay (documentation criterion of the issue). *Review.*
- [ ] AC12: Handing a closed tick to the window returns without waiting for the window: with a window consumer that
  is stalled, the live connection and the replay keep reading, and their receive times, states, ticks and recorded
  frames equal those of a run without a window (E6, E7). *Unit.*

## Out of scope

- Frames that are not part of a tick (skipped, dropped for a tick limit, protocol mismatch): no row.
- Names and icons of goods, islands and regions (M3, #23–#25): every value is a raw GUID.
- The workforce and building maps of an entry (the per-island workforce view is #51).
- Earlier ticks, a history, session events (session start headline, session end) and the close reason of a tick
  (it differs on purpose between live and replay for the last tick, [`pipe.md`](../../docs/areas/pipe.md), *Replay*).
- Sorting, filtering, searching, copying or exporting the table; remembering size or position; a dark theme.
- Opening the window from the command line or at start.

## Open questions

- Which values are the issue's "main values"?
  Answered: all scalar values of an entry ([`pipe.md`](../../docs/areas/pipe.md), *Message types*); a subset would
  hide the field a tester compares. The maps are out of scope (#51).
- What does "the same values as a replayed recording" compare?
  Answered: live and replayed window show the same content, since a replay reproduces the ticks and their receive
  times (record 0036), and the replayed content equals the recorded frames (AC3).
- Is a window allowed, given that the Companion "opens no window"?
  Answered: yes; record 0017 chose WPF for the "settings and diagnostics windows", record 0004 puts its logic in the
  platform-neutral project. The #13 tray app is merged (50af0d4); connection and replay raise each whole tick but
  keep only its summary today.

## Area documents

- [`docs/areas/companion.md`](../../docs/areas/companion.md): *Scope* adds the diagnostics window; *Tray icon and
  menu* lists `Open diagnostics` in the menu and drops "opens no window" for the window; a new section *Diagnostics
  window* holds *Window content*, the update, close, single-window and `Exit` rules, the name sanitizing and the
  privacy note; *Replay* no longer says the replayed ticks have no consumer; *Limits and errors* adds the *manual*
  parts of AC5, AC6, AC8 and AC10 to "Not verified yet".
- [`docs/areas/pipe.md`](../../docs/areas/pipe.md): *Order and end* — the live connection and the replay keep the
  latest closed tick whole, and the diagnostics window is its first consumer ("The tick itself has no consumer yet"
  changes).
- `docs/ARCHITECTURE.md` (not an area document, listed for the plan's sweep): the `Censio.Companion.Core` and
  `Censio.Companion` rows of the project table and the flow sentence "the tray shows the latest closed tick".
- `README.md` and `docs/USER_GUIDE.md` (both languages): the menu lists, the replay paragraph ("Nothing consumes the
  replayed data yet"), the "opens no window" sentence; the screenshot note goes into `docs/USER_GUIDE.md` *Reporting
  problems* and *Probleme melden*, next to the warning against attaching a raw recording.

## Challenge

Devil's Advocate, spec challenge:

1. Major, window memory unbounded — accepted: *Memory* bounds the window (rows on screen only, nothing held while
   closed, at most three ticks); AC10 checks it by unit test, a WPF measurement cannot run on Linux.
2. Major, the window may slow the read path — accepted (confirmed: `PipeConnection.PublishTick` runs the handlers on
   the reading thread): NFR *Read path* and AC12.
3. Major, criteria without a test route — accepted: each criterion names *unit*, *manual* or *review*; AC5, AC8 and
   AC10 are split, AC6 is manual, the manual parts go to "Not verified yet".
4. Minor, `Status:` differs live and replayed — accepted: S4 and AC3 leave it out.
5. Minor, local midnight — accepted: S2 and AC4.
6. Minor, `-0` — accepted: *Window content* and AC2.
7. Minor, reuse of the log sanitizer and the coalescing updater — accepted as behavior (AC9: the log's rule, no cut
   of a 255-byte name; AC4: at most one waiting update); the types are the plan's choice.
8. Minor, place of the privacy note — accepted: *Area documents*.
