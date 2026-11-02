# Spec: Tray app with connection status and recording control

Source: Issue #13
Status: Approved by Lead

## Problem

The Companion is the first thing an external tester of release 0.1.0 runs, and it tells them little: the tray icon is
the generic Windows icon in every state (`src/Censio.Companion/TrayIcon.cs:78`), the tooltip shows only the connection
state, nothing shows whether game data arrives (the live connection reads messages but assembles no ticks:
`PipeConnection.RunSessionAsync`, `PipeConnection.cs:256`), recording runs only from `--record` until `Exit`, and the
Companion writes no log ([`companion.md`](../../docs/areas/companion.md), *Limits and errors*). A tester who sees a
problem has nothing to look at and nothing to hand in.

Checked against the code: the connection states (`PipeConnectionState`) and the release ZIP
([`companion.md`](../../docs/areas/companion.md), *Platform*) exist; a recorder started mid-connection needs the
connection's last `Version` frame first ([`pipe.md`](../../docs/areas/pipe.md); record 0036). The Companion opens no
window and this feature adds none.

## Scenarios

Terms: the *status text* is what the tooltip shows today after `Censio Companion: ` (for example `Receiving
(recording)`, `Replay: Receiving`, `Invalid command line`). The *last tick* is the latest tick delivered in this run,
live or replayed.

- S1: Given the game runs with `/pipe` and a save is loaded, when a tick has arrived, then the icon shows the
  *receiving* look and the tooltip reads `Censio Companion: Receiving` on its first line and
  `Last tick: <time>, <n> islands` on its second: `<time>` is the tick's receive time in local time as `HH:mm:ss`,
  `<n>` the number of islands in the tick (`1 island` for one).
- S2: When the user opens the tray menu, it shows, top to bottom: `Status: <status text>` (greyed), `Last tick:
  <time>, <n> islands` or `Last tick: none` (greyed), a separator, `Start recording` or `Stop recording`, `Open
  recordings folder`, `Open log folder`, a separator, `Version <version>` (greyed), a separator and `Exit`. The values
  are current each time the menu opens.
- S3: The icon follows the connection state: a *waiting* look for `Waiting for game`, `Connected` and `Pipe busy`, a
  *receiving* look for `Receiving`, a *problem* look for `Protocol mismatch` and `Error`. The looks differ in colour and
  in shape.
- S4: Given no recording runs, when the user clicks `Start recording`, then the status text ends with ` (recording)`,
  the entry reads `Stop recording`, and with the next frame of the game a file `recording-<yyyyMMdd-HHmmss>Z.jsonl`
  appears in `%LOCALAPPDATA%\Censio\Recordings`; the time in its name and header is that frame's receive time.
- S5: Given a recording started from the menu in the middle of a connection, when it is stopped and replayed with
  `--replay`, then the replay shows `Replay: Receiving`, waits for no time before its first frame, ends with `Replay
  finished`, and every tick that opened after the recording's first frame equals the live run's tick.
- S6: When the user clicks `Stop recording`, the suffix disappears, the entry reads `Start recording`, the queued
  frames are written and the file is closed; a later start writes a new file.
- S7: Started with `--record`, the Companion records from the start as today and the entry reads `Stop recording`.
- S8: Given no connection is open (every state but `Connected`, `Receiving` and `Protocol mismatch`), a start shows
  ` (recording)` at once and the file is created with the first frame of the next connection.
- S9: `Open recordings folder` and `Open log folder` open `%LOCALAPPDATA%\Censio\Recordings` and
  `%LOCALAPPDATA%\Censio\Logs` in Windows Explorer, creating a missing folder first.
- S10: The Companion logs to `%LOCALAPPDATA%\Censio\Logs\companion-<yyyyMMdd>.log` (local date): one line per event
  with local time and UTC offset, level and message, an exception on the lines after it. It logs the start (version,
  mode); each change of the connection state with its cause (error kind, Windows error code, received version);
  recording started and stopped (at the click), its file name when the file is created, and its failure; the replay's
  start (file name), end and problem; a folder that could not be opened; an unexpected error of a background task; and
  `Exit`.
- S11: In a replay the icon follows the replayed state (`Replay finished` waiting, `Replay error` problem), the last
  tick shows the recorded receive time of the latest replayed tick, and `Start recording` is greyed out.

## Errors and edge cases

- E1: Invalid command line: problem look, status `Invalid command line`, `Last tick: none`, `Start recording` greyed;
  folder entries, log and `Exit` work.
- E2: A last tick not from today's local date shows `yyyy-MM-dd HH:mm:ss`. A time less than one day from the edges of
  the representable range (possible in a recording) is shown in UTC as `yyyy-MM-dd HH:mm:ssZ`. Formatting never throws.
- E3: When a connection ends, the last tick stays until a new tick arrives.
- E4: A tick shows once it closes: when the next tick starts or 30 seconds after its last island frame
  ([`pipe.md`](../../docs/areas/pipe.md), *Closing a tick*), so `Receiving` can show before the first last tick.
- E5: A recording started in `Protocol mismatch` writes the connection's last `Version` frame and replays as `Replay:
  Protocol mismatch`; one started before any `Version` (`Connected`) writes nothing extra.
- E6: A recording started mid-tick: the replay's first tick lacks the islands sent before the start, and a session
  start sent before it is missing (accepted).
- E7: The recording fails (folder, write, slow disk): the suffix reads ` (recording failed)` and the entry `Start
  recording`; a new start tries again with a new file and shows ` (recording)`.
- E8: Quick toggles: each click toggles; a stopped recording still writing its queue finishes in the background while
  a new one runs; its later failure is only logged and changes neither suffix nor entry. Two files in one second get
  `-2`, `-3`, ... as today. The tray never waits for the disk.
- E9: `Exit` gives the queues of all recordings 1 second together and ends within 2 seconds with exit code 0.
- E10: A folder that cannot be created or opened: a warning is logged, nothing else changes.
- E11: The log folder or file cannot be created or written: the lines are lost, nothing else changes, no message.
- E12: A log file is continued past 10 MiB in `companion-<yyyyMMdd>-<n>.log` (`n` from 2); a new local day starts a new
  file. Only the 10 newest files are kept, newest by the date and then `n` in the name; only names of exactly these two
  forms are deleted, other files in the folder never.
- E13: A second start in the same session ends at once as today and writes no log line.
- E14: Frames skipped one after another log nothing per frame: only state changes are logged.

## Non-functional requirements

- Tooltip: two lines at most and at most 127 characters (the limit of .NET's tray icon); the longest status text and
  last-tick line fit.
- Time: icon and tooltip follow a change as today (one pending update, latest state); menu, folder entries and toggle
  never wait for the pipe, the disk or the log; `Exit` stays within 2 seconds.
- Privacy and security: the log holds no pipe content (headline, island names, values), recording files by name and
  folders by name only; a replay is logged by its file name. Control and format characters in a logged text are
  replaced by `?`, and a message is cut to a bounded length. Exception texts are logged as given and may hold the
  profile path, and so the Windows user name. The folders are fixed, never taken from the pipe or a file. Security
  areas: *File paths for recordings*, *Logging of external data*.
- Log level Information and above, no configuration (no settings exist yet).
- No window opens; the menu is light also with Windows in dark mode.
- Windows 10 22H2 and 11, x64; the release ZIP still holds exactly `Censio.Companion.exe`, self-contained, every new
  library embedded (record 0027). `--record`, `--replay`, `--speed` and the recording format (version 1) are unchanged.

## Acceptance criteria

- [ ] AC1: Tooltip line 2 and the menu's last-tick entry show the latest closed tick's receive time (local `HH:mm:ss`,
  with date when not today, UTC near the range edges) and island count; before the first tick the tooltip has one line
  and the menu `Last tick: none`; the value outlives a connection; the tooltip never exceeds 127 characters (S1, S2,
  E2, E3, E4).
- [ ] AC2: The menu holds the entries of S2 in that order, greyed as S2, E1 and S11 say, with the current status text
  (S2, E1, S11).
- [ ] AC3: The icon shows the look S3, S11 and E1 assign to each connection, replay and command-line state (S3, S11,
  E1).
- [ ] AC4: Start and stop work in every connection state, entry text and suffix follow, each start writes a new file
  that is created at its first frame, a failure can be retried, a stopped recording's failure changes nothing, and
  `--record` starts recording (S4, S6, S7, S8, E7, E8).
- [ ] AC5: A recording started mid-connection replays to `Replay: Receiving` (or `Replay: Protocol mismatch` when the
  live run was in mismatch), waits for nothing before its first frame, ends with `Replay finished`, and its ticks opened
  after the first frame equal the live run's (S5, E5, E6).
- [ ] AC6: `Exit` gives all recording queues 1 second together and ends within 2 seconds with exit code 0 (E9).
- [ ] AC7: The folder entries open the fixed folders, creating them when missing; a failure only logs a warning (S9,
  E10).
- [ ] AC8: The log is written, named, rotated and pruned as S10 and E12 say, logs state changes only, holds no pipe
  content, replaces control and format characters, and its failure changes nothing else (S10, E11, E12, E14).
- [ ] AC9: A second instance writes no log line (E13).
- [ ] AC10: The release ZIP holds exactly `Censio.Companion.exe`; on Windows x64 without installed .NET it starts and
  shows the menu of S2; with the game started with `/pipe` and a save loaded the tooltip shows `Receiving` and a `Last
  tick`; a recording toggled from the menu replays with `--replay` to `Replay: Receiving` and then `Replay finished`
  (the maintainer's manual run on Windows).

## Out of scope

- Settings, a log level setting, autostart (#15), the server connection, any window and its theme (record 0017), a dark
  menu, German tray texts, balloon notifications, replay from the menu, choosing the folders, deleting recordings, an
  icon for the `.exe` file, and whether Windows sign-out or shutdown gives the recordings their 1 second.

## Open questions

- Does `--record` stay? Answered: yes, it starts with recording on (S7); the issue removes nothing and the README
  documents it.
- Game time or receive time for the last tick? Answered: receive time in local time; game time is the game's counter,
  not a clock time ([`pipe.md`](../../docs/areas/pipe.md), *Ticks and sessions*).
- Which windows need the light theme? Answered: none exist or are added; the settings and diagnostics windows of record
  0017 come later. The menu is light.
- What does a mid-connection recording need? Answered: the connection's last `Version` frame first (record 0036),
  written with the first recorded frame's receive time so the replay waits for nothing before it (S5).
- Where does the log go? Answered: local app data, as the issue says: `%LOCALAPPDATA%\Censio\Logs`.
- May the toggle work in a replay? Answered: no, a replay opens no pipe (S11).
- What does "structured logging" mean here, and may a library be used? Answered: events are written through .NET's
  logging abstraction with message templates and named values (the Server's API, `Microsoft.Extensions.Logging`),
  rendered as the text lines of S10 so a tester can read them; the abstraction package is allowed, the rotating file
  writer is the Companion's own (decided in the plan).

## Area documents

- [`companion.md`](../../docs/areas/companion.md): icon looks, two-line tooltip, the new menu, the toggle with
  `--record` as start state, folder entries, the log; *Recording* loses "(recording failed) until Exit", *Limits and
  errors* loses "logs nothing" and "cannot be started or stopped".
- [`pipe.md`](../../docs/areas/pipe.md): the live connection assembles ticks; *What is recorded* states the exception
  of the prepended `Version` frame; *Writing* loses "for the rest of the run" and "a connection that starts afterwards
  records nothing" (a new start resets the failure); *Recorder started in the middle of a connection* and its residual
  risk become the rule of S5 and E5.
- `docs/ARCHITECTURE.md` (the `Censio.Companion.Core` row, the flow paragraph); `README.md` and `docs/USER_GUIDE.md` in
  English and German: *Recording and replay* (the menu; "stops for the rest of the run" at `README.md` lines 128 and
  288 and `USER_GUIDE.md` lines 134 and 331), *First start*, *Data and logs*, *Reporting problems* (where the log is,
  and that it may hold the Windows user name through a path: check it before sharing).

## Challenge

Spec challenge, 0 major and 10 minor objections, all accepted:

1. E7 contradicted documents not listed: accepted; *Area documents* names the passages, the plan resets a failed
   session on start.
2. A stopped recording's later failure: accepted; E8 says it is only logged.
3. S8 left out `Error`: accepted; S8 covers every state without an open connection.
4. AC1 and AC4 only verified against fakes: accepted; AC10's manual run covers `Receiving`, `Last tick` and a toggled
   recording's replay.
5. Extreme replay times: accepted; E2 gives the UTC fallback and requires that formatting never throws.
6. "Structured" undefined: accepted; answered under *Open questions*.
7. Continuation name and pruning order: accepted; E12 gives both and the exact deletion forms.
8. No file name at the click: accepted; S10 logs the click and the name at creation.
9. Privacy contradiction: accepted; the NFR says exception texts may hold the user name, and *Reporting problems* says
   so in both languages.
10. *What is recorded* not listed: accepted; listed with the exception.
