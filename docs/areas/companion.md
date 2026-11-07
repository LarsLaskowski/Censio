# Companion

## Scope

The Companion process on Windows: its tray icon, tooltip and menu, how it exits, how many instances run, the
version it shows, its command line (recording the pipe and replaying a recording), starting and stopping a recording
from the menu, the optional start with Windows, the diagnostics window with the last tick, the folders it opens, its log file, and the release ZIP it ships in. The user who starts the Companion relies on it.

## Behavior

### Tray icon and menu

- Started, the Companion shows one icon in the notification area. It opens no window until `Open diagnostics` is chosen, and shows a taskbar button only while the diagnostics window is open.
- The *status text* is the state of the pipe connection ([`pipe-connection.md`](pipe-connection.md), *States*): `Waiting for game`, `Connected`,
  `Receiving`, `Pipe busy`, `Protocol mismatch` or `Error`; before the first attempt it reads `Waiting for game`. In a
  replay it is the replay's text (*Replay*), with an invalid command line `Invalid command line`.
- The tooltip reads `Censio Companion: <status text>`. Once a tick has closed it has a second line,
  `Last tick: <time>, <n> islands` (`1 island` for one), where `<time>` is the receive time of the latest closed tick
  of this run, live or replayed: in local time as `HH:mm:ss` when its local date is today's, otherwise as
  `yyyy-MM-dd HH:mm:ss`; a time less than one day from the ends of the representable range (possible in a recording)
  is shown in UTC as `yyyy-MM-dd HH:mm:ssZ`. Before the first tick there is no second line. A tick closes by the rules
  of [`pipe.md`](pipe.md) (*Closing a tick*), so `Receiving` can show before the first last tick. The last tick stays when the connection ends, until a new tick arrives. The tooltip
  is cut to 127 characters, the limit of the tray icon; the longest text the Companion builds is 109.
- The tooltip follows the state, the recording and the last tick without any action by the player, and the date of
  the last tick is re-evaluated at the next local midnight. At most one tooltip update waits for the tray at a time
  and it shows the latest state. The tooltip shows no cause.
- The status text ends with ` (recording)` while the Companion records and with ` (recording failed)` after the
  recording failed (*Recording*); it shows neither while no recording runs.
- The icon follows the state with three looks that differ in colour and shape: a grey ring (*waiting*) for `Waiting
  for game`, `Connected` and `Pipe busy`; a green filled circle (*receiving*) for `Receiving`; a red filled triangle
  (*problem*) for `Protocol mismatch`, `Error` and an invalid command line. In a replay the icon follows the replayed
  state, shows *waiting* after `Replay finished` and *problem* after `Replay error`.
- The context menu holds, top to bottom: `Status: <status text>` (greyed out), `Last tick: <time>, <n> islands` or
  `Last tick: none` (greyed out), a separator, `Open diagnostics`, `Start recording` or `Stop recording`, `Open recordings folder`, `Open
  log folder`, a separator, `Start with Windows` (checked while the autostart is on, enabled in every mode), a separator, `Version <version>` (greyed out, nothing happens on click), a separator and `Exit`. The
  values are current each time the menu opens. `Open diagnostics` is enabled in every mode (live, `--record`, replay and an invalid command line); `Start recording` is greyed out in a replay and with an invalid command
  line. The menu is light, also with Windows in dark mode.
- `<version>` is the informational version the Companion was built with: `0.0.0` without a version, otherwise
  the build property `Version` exactly as passed.
- `Exit` removes the icon at once and ends the process with exit code 0 within 2 seconds in every connection state;
  the tray never waits for a pending pipe call, the disk or the log.
- When Windows Explorer restarts, the icon appears again once the notification area is back. Sign-out and
  shutdown end the Companion without a dialog.

### Command line

The Companion knows three switches; the case of a switch does not matter. A start without a switch behaves as
described in the other sections.

| Switch | Meaning |
| ------ | ------- |
| `--record` | Start with the recording on: record every connection to the pipe to a file until it is stopped from the menu or `Exit` (*Recording*) |
| `--replay <file>` | Do not open the pipe; feed the recording `<file>` through the pipe's reader and tick assembler (*Replay*) |
| `--speed <value>` | With `--replay` only: `max` (no pauses) or a factor from `0.1` to `1000`, written with a decimal point (`.`) also on a German Windows; default `1` (real speed) |

- Any other command line is invalid: an unknown argument, a switch given twice, `--speed` without `--replay`, a speed
  that is not `max` or a factor in that range, `--replay` without a file or with an empty one or with a switch in
  the place of the file, or `--record` together with `--replay`. The tooltip reads `Censio Companion: Invalid command
  line`, the Companion neither records, replays nor opens the pipe, the icon shows the *problem* look, and the menu shows
  `Last tick: none` with `Start recording` greyed out; the folder entries, the log and `Exit` work as usual.
- The menu has the entries of *Tray icon and menu* with every switch.
- A start with a switch obeys *Single instance*: if a Companion already runs in the user session, the new one ends
  at once, so close the running one first.

### Recording

- A recording starts and stops from the menu. A start without `--record` begins with it off, and `--record` starts with
  it on, so the entry reads `Stop recording` from the start. The entry works in every connection state of the live
  Companion: `Start recording` makes the status text end with ` (recording)` and the entry read `Stop recording`;
  `Stop recording` removes the suffix and makes the entry read `Start recording`. Each click toggles. In a replay and
  with an invalid command line nothing records.
- While recording, the Companion reads the pipe as usual (the states are unchanged) and writes the frames of each
  connection to its own file in `%LOCALAPPDATA%\Censio\Recordings`. The folder is fixed; no part of it comes from
  the pipe or the command line. What is recorded, when a file is created and named, the file format, the write queue
  and its failures, a start in the middle of a connection and the privacy of a recording are in
  [`pipe-recording.md`](pipe-recording.md).
- A start while no connection is open (every state but `Connected`, `Receiving` and `Protocol mismatch`) shows
  ` (recording)` at once; the file follows with the first frame of the next connection. No folder or file is created
  while no recording runs or while no frame arrives.
- `Stop recording` writes the frames still waiting in the background and closes the file; the tray never waits for the
  disk. A new start can run while the stopped recording still writes.
- `Exit` ends the Companion as without a recording, within 2 seconds with exit code 0; the frames still waiting get
  the time [`pipe-recording.md`](pipe-recording.md) (*Writing*) states.
- If the recording fails, the status text ends with ` (recording failed)` and the entry reads `Start recording`. The
  connection is not affected. A new `Start recording` tries again with a new file and shows ` (recording)`. A stopped
  recording that fails while it writes its remaining frames is only logged: it changes neither the status text nor
  the entry.
- Recordings are never deleted by the Companion; the user deletes them.

### Folders

- `Open recordings folder` opens `%LOCALAPPDATA%\Censio\Recordings` and `Open log folder` opens
  `%LOCALAPPDATA%\Censio\Logs` in Windows Explorer, creating a missing folder first. Both folders are fixed. Explorer
  is started from the Windows folder by its full path with the folder as its one argument, and only for a fully
  qualified folder path.
- The menu does not wait for the disk or for Explorer. If a folder cannot be created or opened, or is not a full path
  (local app data unresolved), a warning is logged and nothing else changes.

### Diagnostics window

`Open diagnostics` opens one window, titled `Censio Companion: Diagnostics`, with the latest closed tick (record
[0038](../decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md)). It shows every value as the game
sent it: GUIDs are raw numbers until names and icons exist.

- **Header**, top to bottom: `Status: <status text>`; `Last tick: <time>, <n> islands` or `Last tick: none`, exactly the
  text of the menu entry of the same name (*Tray icon and menu*); and, once a tick exists, `Game time: <game time>`, with
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
- **Names** pass the rule of the log (*Log*): each control character, format character, line separator, paragraph
  separator (the categories Cc, Cf, Zl, Zp) and unpaired surrogate is shown as one `?`, nothing else is changed (combining
  marks, right-to-left letters and spaces stay as sent), and a name is never cut, also at the longest pipe string of 255
  bytes. Each cell is its own text, so a name cannot reorder another cell.
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
- **Privacy:** the window shows island names and economic data on screen only. Nothing of it is written to disk, logged
  or sent anywhere, and the window adds no log line. A screenshot holds the savegame's island names.

### Start with Windows

- Off by default: the Companion writes nothing about autostart until the entry is clicked (not at start, not when the menu opens). The entry is the string value `Censio Companion` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, in the user's own hive; no other key or user is touched and no administrator rights are needed.
- The menu entry `Start with Windows` is checked when the value is exactly the command the Companion writes: the full path of the running `Censio.Companion.exe` in double quotes, without arguments, compared ignoring case and unexpanded. The state is read from the registry each time the menu opens. Anything else counts as off: no value, another program's path, the same path unquoted, with arguments (for example `--record`), with spaces around it, or a value that is not a string. A click then writes the Companion's own quoted command, replacing what was there.
- A click on an unchecked entry writes that command; a click on a checked entry deletes the value `Censio Companion`. The Companion starts with Windows the same way as when started by hand, without a command line argument, so it neither records nor replays.
- The path of the exe is used only when it is a full path of at most 200 characters without `"` or `%`. For any other path (a folder with `%` in its name, a very long one) the click changes nothing and the change warning is logged; the entry stays unchecked.
- Moving the exe, or starting a new version from another folder, leaves the old command behind; the entry then shows unchecked. To remove it, start any copy of the exe and click the entry (it replaces the stale command with this exe's), then click it again (it deletes the value), or delete the value under the key above in the Registry Editor; switching it off in Windows' startup apps list does not delete it.
- Turning it on and off is logged (*Log*). The Companion does not read or change the switch of Windows' startup apps list (`Settings > Apps > Startup`): a value switched off there is still checked in the menu, and the player switches it on again in that list.

### Log

- The Companion writes a log to `%LOCALAPPDATA%\Censio\Logs\companion-<yyyyMMdd>.log` (the local date). The folder
  is fixed. Information and above is logged; there is no setting.
- A file that would pass 10 MiB (10,485,760 bytes) with the next line is continued in
  `companion-<yyyyMMdd>-<n>.log` with `n` from 2 up to 9999 (beyond that lines are dropped); a new local day starts a
  new file, and a Companion started again on the same day continues the day's highest-numbered file. When a new file
  is created, only the 10 newest log files are kept, newest by the date and then `n` in the name. Only top-level files
  whose whole name is exactly `companion-<yyyyMMdd>.log` or `companion-<yyyyMMdd>-<n>.log` (`n` from 2, no leading
  zero, ASCII digits only, case-insensitive) are ever deleted, other files in the folder never; a symbolic link is
  deleted as a link, not its target.
- A line is `<yyyy-MM-ddTHH:mm:ss.fffzzz> <level> <message>`: local time with UTC offset, the level `INF`, `WRN`, `ERR`
  or `CRT`, and the message; an exception follows on the lines after it, each indented by two spaces.
- It logs: the start with the version and the mode (`Normal`, `Record`, `Replay`, `Invalid`); each change of the
  connection state with the error kind, the Windows error code and the received version (only changes, never a line
  per skipped frame); recording started and stopped at the click, its file name (the name only) when the file is
  created, a recording failure and the failure of a stopped recording; a replay's start (the file name only), its
  end and its problem reason; a folder that could not be opened; the autostart turned on (`Autostart enabled`) or off (`Autostart disabled`) and, as warnings, `Could not read the autostart setting` (once until a read succeeds again, so a blocked registry does not log at every menu opening) and `Could not change the autostart setting`, with the exception where there is one and never the exe path; an unexpected error of a background task; and `Exit`.
- The log holds no pipe content (headline, island names, values): recording files and folders are logged by name only.
- Text that came from outside (a replay file name, exception texts) is sanitized: each control character, format
  character, line or paragraph separator and unpaired surrogate becomes one `?`, so a text cannot add a line or hide
  characters. A message is cut at 2,048 characters and an exception text at 16,384, each with `…` appended when cut.
- **Exception texts are logged as given and may contain the profile path, and so the Windows user name.** A user
  checks a log for it before sharing it.
- The writer works in the background: a line waits in a queue of at most 1,048,576 characters (counted per line) and a
  line that does not fit is dropped. If the log folder or file cannot be created or written, or the folder is not a
  full path, the lines are lost and nothing else changes, with no message. At `Exit` the queue gets 0.5 second.
- A second Companion in the same session ends at once and writes no log line.

### Replay

- With `--replay <file>` the Companion reads the file instead of the pipe, paced by `--speed`
  ([`pipe-recording.md`](pipe-recording.md), *Replay*).
- The status text reads `Replay: Connected` from the start until a replayed frame changes the state,
  then `Replay: Receiving` or `Replay: Protocol mismatch` by the same rules as the
  live connection ([`pipe-connection.md`](pipe-connection.md), *States*), and after the last frame `Replay finished`
  until `Exit`. A replay never shows `Error`. The tooltip is `Censio Companion: ` and the status text.
- A problem with the file ends the replay with the status text `Replay error: <reason>` after the frames before it
  were replayed, with a reason of [`pipe-recording.md`](pipe-recording.md) (*Limits and errors*).
- The tooltip, the last tick and the icon show the progress, and the diagnostics window shows the replayed ticks and follows the replay tick by tick; after `Replay finished` it keeps the last tick of the file. The last tick is the recorded receive time of the latest replayed tick.

### Single instance

- Only one Companion runs per Windows user session. A second start in the same session ends at once with exit
  code 0, without a message and without a second icon; the first keeps running.
- A start in another user session (another signed-in user) is not blocked and has its own icon.

### Platform

- The Companion runs on Windows x64. Every build writes `Censio.Companion.exe`, on Linux too; it runs only on
  Windows, and a local build needs the .NET 10 Desktop Runtime (the release ZIP below does not).
- Every release attaches the ZIP `Censio-<version>-win-x64.zip`. It holds exactly one file at its root, the
  self-contained single-file `Censio.Companion.exe` with the native libraries embedded, and needs no installed .NET,
  installer or administrator rights, on Windows 10 22H2 and Windows 11, x64. The exe is not signed, so SmartScreen
  asks once ("More info", "Run anyway"). Its tray menu shows `Version <version>`; the exe's file properties show no
  product version.

## Limits and errors

- The Companion shows no error message or dialog; a connection problem shows only as a status text, a recording
  problem as `(recording failed)`, a replay problem as `Replay error`, and details go to the log (*Log*).
- A folder that cannot be opened, or a log that cannot be written, changes nothing else and shows no message.
- A read failure (access denied, a policy, a value that is not a string): the entry is shown unchecked and stays clickable, and `Could not read the autostart setting` is logged once until a read succeeds again. If the value cannot be written or deleted, a click changes nothing, `Could not change the autostart setting` is logged and the player gets no message; the next opening shows the state read then. A policy that stops Windows from starting the user's startup programs while the value can still be written is not visible: the menu shows the state of the value only. A switch-off in Windows' startup apps list is not read: the entry stays checked while Windows does not start the Companion, and the player switches it on in that list; a click deletes the value, and a later click writes it again with Windows' switch-off still in place. An exe started from inside the ZIP runs from a temporary folder, so the entry registers that path and nothing starts at sign-in once Windows cleans the folder.
- Manual runs on Windows confirmed the behavior above (issues #13, #14 and #15, and the Product Manager's run on
  Windows 11 26H2 with the real game). Not verified yet: that the menu stays light with Windows in dark mode, that the
  three icon looks show, that Explorer opens a folder whose path holds a comma (Explorer splits its command line at
  commas), whether SmartScreen asks at the first autostart, what name the startup apps list shows, and that a folder
  holding `%` stays unchecked.

## Related decisions

- [`decisions/0004-companion-shell-and-linux-build.md`](../decisions/0004-companion-shell-and-linux-build.md) — logic in a platform-neutral project, the Windows shell builds on Linux.
- [`decisions/0005-product-version-from-one-build-property.md`](../decisions/0005-product-version-from-one-build-property.md) — the version comes from the build property `Version`.
- [`decisions/0012-release-pre-release-first-and-secret-free-builds.md`](../decisions/0012-release-pre-release-first-and-secret-free-builds.md) — the release ZIP is built on Linux without secrets and checked before it is attached.
- [`decisions/0017-companion-tray-app-autostart-and-connect.md`](../decisions/0017-companion-tray-app-autostart-and-connect.md) — the autostart is a per-user Run value written on the player's click.
- [`decisions/0027-companion-zip-self-contained-single-file-unsigned.md`](../decisions/0027-companion-zip-self-contained-single-file-unsigned.md) — self-contained single-file exe in a ZIP, not signed.
- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — recordings hold the raw frames, written off the read path; a replay feeds them back on a recorded clock.
- [`decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md`](../decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md) — the Companion logs through .NET's logging abstraction into its own rotating text file.
- [`decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md`](../decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md) — the diagnostics window is a flat table whose rows are made on demand from the latest tick, kept whole.

## Not here

- Reading the statistics pipe and assembling ticks: [`pipe.md`](pipe.md).
- Opening the pipe, the connection states and the retry rules: [`pipe-connection.md`](pipe-connection.md).
- The recording file, its writing and failures, and the replay rules: [`pipe-recording.md`](pipe-recording.md).
- Reading the game files, the connection to the server, settings (later issues).
