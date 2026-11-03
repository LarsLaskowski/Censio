# Spec: Diagnostics window for the last tick

Source: Issue #14
Status: Draft

## Problem

A tester who runs the Companion today sees only the state, the receive time and the island count of the last
tick (tooltip and menu, [`companion.md`](../../docs/areas/companion.md), *Tray icon and menu*). Whether the game
really sent goods, and which values, cannot be checked without the server UI (M4) and the names and icons (M3);
the replayed ticks have no consumer at all (*Replay*: "only the tooltip, the last tick and the icon show the
progress"). A tester needs a plain view of the last tick's raw content, live and in a replay, to see that data
arrives and to compare a live run with its recording.

## Scenarios

- S1: Given the Companion runs live and a tick has closed, when the user chooses `Open diagnostics` in the tray
  menu, then a window titled `Censio Companion: Diagnostics` opens and shows that tick: a header and a table with
  one row per good of every island of the tick (layout under *Window content* below).
- S2: Given the window is open, when the next tick closes, then the window shows the new tick in place of the old
  one, without any action by the user; the header's `Status:` line follows the connection state as the menu's
  status entry does.
- S3: Given the window is open, when the user closes it (its close button or Alt+F4), then only the window closes:
  the tray icon stays, the connection, a running recording or replay go on, and the tooltip and menu keep
  updating. Choosing `Open diagnostics` again opens the window with the last tick at once, also when that tick
  closed while no window was open.
- S4: Given the Companion replays a recording (`--replay <file>`, any `--speed`), when the user opens the window,
  then it shows the latest replayed tick and follows the replay tick by tick; after `Replay finished` it keeps
  showing the last tick of the file. For the same frames the window shows the same content live and in the replay
  of their recording (the replay reproduces the ticks and their receive times, [`pipe.md`](../../docs/areas/pipe.md),
  *Replay*).
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
  reads back as the same 32-bit value (`0.1`, `-2.5`, `1E+38`), and `NaN`, `Infinity`, `-Infinity` — the same on a
  German Windows.

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
- E9: An island frame dropped for a tick limit, a skipped frame or a protocol mismatch: none of them is part of a
  tick, so the window shows nothing of them (the `Status:` line shows the mismatch).

## Non-functional requirements

- Privacy: the window shows island names and economic data as the game sent them, on screen only. Nothing of the
  window is written to disk, logged or sent anywhere; no new log line. A screenshot of the window holds the
  savegame's island names, and the user guide says so where it asks for problem reports.
- Memory: the Companion keeps the latest closed tick whole (not only its summary) whether or not the window is open
  (S3), across connections like the last-tick summary. At the tick limits this adds at most the bound of one open
  tick ([`pipe.md`](../../docs/areas/pipe.md), *Limits and errors*); real ticks are far smaller.
- Responsiveness: the window never makes the tray wait (S5, E6, E7); `Exit` keeps its 2-second bound in every
  state with the window open or closed.
- One window at most (E4). Its size and position are not remembered; there is no setting.
- Compatibility: Windows 10 22H2 and Windows 11 x64 as the rest of the Companion; the release ZIP still holds one
  file. Every other menu entry and the tooltip are unchanged; the menu gains `Open diagnostics` directly after the
  first separator, before `Start recording`/`Stop recording`.
- While the window is open it has a taskbar button; with the window closed the Companion again shows no window and
  no taskbar button.

## Acceptance criteria

- [ ] AC1: The tray menu holds `Open diagnostics`, enabled in every mode (live, `--record`, replay, invalid command
  line), directly after the first separator; every other entry keeps its text and order (S1, E1).
- [ ] AC2: For a tick, the header lines and the table rows are exactly as *Window content* states — columns and
  their order, island and good order, the `(no entries)` row, ` (no data yet)`, and the number formats with `NaN`,
  `Infinity`, `-Infinity`, negative values and the decimal point `.` under a German culture (S1).
- [ ] AC3: A recording replayed through the Companion's replay ends with the window content of its last tick
  holding exactly the GUIDs and values of that tick's frames, at speed `max` and at a finite speed, and the same
  frames fed as a live connection give the same content (S4, E6).
- [ ] AC4: Each closed tick replaces the window's content with the new tick and each change of the status replaces
  its `Status:` line; with ticks closing faster than the window updates, the content after the last tick is that
  tick (S2, E6).
- [ ] AC5: Closing the window leaves the connection or replay, a running recording, the tooltip and the menu
  running; opening it again shows the last tick at once, including a tick that closed while it was closed (S3).
- [ ] AC6: `Exit` with the window open closes it and ends the Companion within 2 seconds with exit code 0 (S5).
- [ ] AC7: Before the first tick, and with an invalid command line, the window shows `Status: <status text>`,
  `Last tick: none`, no `Game time:` line and an empty table; after the connection ends or a replay error it keeps
  the last tick (E1, E2, E3).
- [ ] AC8: `Open diagnostics` with the window open opens no second window and brings the open one to the front,
  restored when minimized (E4).
- [ ] AC9: In an island name each control character, format character, line or paragraph separator and unpaired
  surrogate is shown as one `?`; other characters, spaces included, are shown unchanged (E5).
- [ ] AC10: A tick at the limits of E7 is shown whole and can be scrolled, and while it is shown or replaced the
  tray menu opens and `Exit` keeps its bound; a new tick keeps the scroll position (E7, E8).
- [ ] AC11: README and user guide describe `Open diagnostics` and the window, English first, then German, so that
  an external tester can open it live and in a replay (documentation criterion of the issue).

## Out of scope

- E9: frames that are not part of a tick have no row by the pipe's own rules; the window adds nothing for them.
- Names and icons of goods, islands and regions (M3, #23–#25): every value is a raw GUID.
- The workforce and building maps of an entry (the per-island workforce view is #51).
- Earlier ticks, a history, session events (session start headline, session end) and the close reason of a tick
  (it differs on purpose between live and replay for the last tick, [`pipe.md`](../../docs/areas/pipe.md), *Replay*).
- Sorting, filtering, searching, copying or exporting the table; remembering size or position; a dark theme.
- Opening the window from the command line or at start.

## Open questions

- Which of an entry's values are the issue's "main values"?
  Answered: all scalar values of an entry ([`pipe.md`](../../docs/areas/pipe.md), *Message types*): the window is a
  raw check that data arrives, and a subset would hide exactly the field a tester wants to compare; the two maps are
  out of scope (#51).
- What does "shows the same values as a replayed recording" compare?
  Answered: the window of a live run and the window of the replay of its recording show the same content, because a
  replay reproduces the ticks and their receive times ([`pipe.md`](../../docs/areas/pipe.md), *Replay*, record 0036);
  and the replayed content equals the values in the recorded frames (AC3).
- Does the issue's claim hold that the tray app (#13) exists to host the entry?
  Answered: yes, merged in 50af0d4; the menu is built in `Censio.Companion.Core` (`TrayMenu.Create`) and shown by the
  shell's tray icon, and the live connection and the replay both raise an event with the whole tick
  (`TickReceived`) but keep only its summary today.
- Is a window allowed at all, given that the Companion "opens no window"?
  Answered: yes; record 0017 chose WPF for the Companion's "settings and diagnostics windows", and 0004 puts the
  window's logic in `Censio.Companion.Core` and only its UI in the shell. The sentence in `companion.md` and the user
  guide changes to "opens no window until `Open diagnostics` is chosen".

## Area documents

- [`docs/areas/companion.md`](../../docs/areas/companion.md): *Scope* adds the diagnostics window; *Tray icon and
  menu* lists `Open diagnostics` in the menu and drops "opens no window" for the window; a new section *Diagnostics
  window* holds *Window content*, the update, close, single-window and `Exit` rules, the name sanitizing and the
  privacy note; *Replay* no longer says the replayed ticks have no consumer; *Limits and errors* adds the window to the
  checks of the manual run on Windows.
- [`docs/areas/pipe.md`](../../docs/areas/pipe.md): *Order and end* — the live connection and the replay keep the
  latest closed tick whole, and the diagnostics window is its first consumer ("The tick itself has no consumer yet"
  changes).
- `docs/ARCHITECTURE.md` (not an area document, listed for the plan's sweep): the `Censio.Companion.Core` and
  `Censio.Companion` rows of the project table and the flow sentence "the tray shows the latest closed tick".
- `README.md` and `docs/USER_GUIDE.md` (both languages): the menu lists, the replay paragraph ("Nothing consumes the
  replayed data yet"), the "opens no window" sentence, and the screenshot note for problem reports.
