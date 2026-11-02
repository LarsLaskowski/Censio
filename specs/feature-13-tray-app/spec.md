# Spec: Tray app with connection status and recording control

Source: Issue #13
Status: Draft

## Problem

The Companion is the first thing an external tester of release 0.1.0 runs, and today it tells them little: the tray
icon is the same generic Windows icon in every state (`src/Censio.Companion/TrayIcon.cs`, `SystemIcons.Application`),
the tooltip shows only the connection state, nothing shows whether game data actually arrives (the live connection
reads messages but does not assemble ticks: `PipeConnection.RunSessionAsync` uses only `PipeMessageReader`), recording
can be started only with `--record` before the start and stopped only by `Exit`, and the Companion writes no log
([`companion.md`](../../docs/areas/companion.md), *Limits and errors*). A tester who sees a problem has nothing to look
at and nothing to hand in. The issue asks for a tray app that shows status, last tick and island count, controls the
recording from the menu, opens the recordings and log folders, and logs to a rotating file.

Checked against the code: the connection states exist (`PipeConnectionState`), the release ZIP exists
([`companion.md`](../../docs/areas/companion.md), *Platform*), a recorder started mid-connection needs the
connection's last `Version` frame first ([`pipe.md`](../../docs/areas/pipe.md), *Recorder started in the middle of a
connection*). The Companion opens no window today and this feature adds none.

## Scenarios

Terms: the *status text* is what the tooltip shows today after `Censio Companion: ` (for example `Receiving
(recording)`, `Replay: Receiving`, `Invalid command line`). The *last tick* is the latest tick the tick assembler
delivered in this run, live or replayed.

- S1: Given the game runs with `/pipe` and a save is loaded, when the Companion has received a tick, then the icon
  shows the *receiving* look, and the tooltip reads `Censio Companion: Receiving` on its first line and
  `Last tick: <time>, <n> islands` on its second, where `<time>` is the tick's receive time in local time as
  `HH:mm:ss` and `<n>` the number of islands in that tick (`1 island` for one).
- S2: Given the Companion runs, when the user opens the tray menu, then it shows, top to bottom: `Status: <status
  text>` (greyed), `Last tick: <time>, <n> islands` or `Last tick: none` before the first tick (greyed), a separator,
  `Start recording` or `Stop recording`, `Open recordings folder`, `Open log folder`, a separator, `Version <version>`
  (greyed), a separator and `Exit`. The values are current each time the menu opens.
- S3: Given the connection state changes, then the icon changes with it: a *waiting* look for `Waiting for game`,
  `Connected` and `Pipe busy`, a *receiving* look for `Receiving`, a *problem* look for `Protocol mismatch` and `Error`.
  The three looks differ in colour and in shape.
- S4: Given the Companion receives without recording, when the user clicks `Start recording`, then the tooltip and the
  status line end with ` (recording)`, the entry reads `Stop recording`, and a file
  `recording-<yyyyMMdd-HHmmss>Z.jsonl` appears in `%LOCALAPPDATA%\Censio\Recordings` with the next frame of the game;
  the time in its name and its header is that frame's receive time.
- S5: Given a recording started from the menu in the middle of a connection, when the user clicks `Stop recording`
  and replays the file with `--replay`, then the replay shows `Replay: Receiving` (never `Protocol mismatch` caused
  by the start), starts without waiting for the time before the start, ends with `Replay finished`, and every tick
  that opened after the recording's first frame equals the tick of the live run.
- S6: Given a recording runs, when the user clicks `Stop recording`, then the suffix ` (recording)` disappears, the
  entry reads `Start recording`, the frames already queued are written to the file and the file is closed; a later
  `Start recording` writes a new file.
- S7: Given the Companion was started with `--record`, then it starts recording as today and the entry reads
  `Stop recording`; stopping and starting work as in S4 and S6.
- S8: Given recording is started while no connection is open (`Waiting for game`, `Pipe busy`), then the suffix
  ` (recording)` shows at once, and the file is created with the first frame of the next connection, as with
  `--record` today.
- S9: When the user clicks `Open recordings folder` or `Open log folder`, then Windows Explorer opens
  `%LOCALAPPDATA%\Censio\Recordings` or `%LOCALAPPDATA%\Censio\Logs`; a folder that does not exist yet is created
  first.
- S10: Given the Companion runs, then it writes a log to `%LOCALAPPDATA%\Censio\Logs\companion-<yyyyMMdd>.log`
  (local date): one line per event with the local time and its UTC offset, the level and the message; an
  exception follows on the next lines. It logs at least: the start (version and mode), each change of the connection
  state with its cause (error kind, Windows error code, received version), recording started, stopped and failed
  (with the file name), the replay's start, end and problem, a folder that could not be opened, an unexpected error of
  a background task, and `Exit`.
- S11: Given a replay (`--replay`), then the icon follows the replayed state (`Replay finished` the waiting look,
  `Replay error` the problem look), the last tick shows the recorded receive time of the latest replayed tick, and
  `Start recording` is greyed out; the folder entries work.

## Errors and edge cases

- E1: Invalid command line: the icon shows the problem look, the status reads `Invalid command line`, the last tick
  `none`, `Start recording` is greyed out; the folder entries, the log and `Exit` work.
- E2: The last tick is not from today's local date: `<time>` reads `yyyy-MM-dd HH:mm:ss` (always so in a replay of an
  older recording).
- E3: The game quits or the connection ends: the last tick stays as it was until a new tick arrives; the state changes
  as today.
- E4: A tick is shown only once it closes, which happens when the next tick starts or 30 seconds after its last island
  frame ([`pipe.md`](../../docs/areas/pipe.md), *Closing a tick*): `Receiving` can show before the first `Last tick`.
- E5: A recording started while the connection is in `Protocol mismatch` writes the connection's last `Version` frame,
  so its replay shows `Replay: Protocol mismatch` as the live run did. A recording started before any `Version` on the
  connection (`Connected`) writes nothing extra.
- E6: A recording started in the middle of a tick: the replay's first tick lacks the islands sent before the start,
  and a session start sent before it is missing (accepted, S5 compares only ticks opened after the first frame).
- E7: The recording fails (folder, write, slow disk): the suffix reads ` (recording failed)` and the entry `Start
  recording`; a new `Start recording` tries again with a new file and replaces the suffix by ` (recording)`.
- E8: `Start recording`, `Stop recording` clicked quickly in a row: each click toggles; a stopped recording still
  writing its queue finishes in the background while a new one starts; two files in one second get `-2`, `-3`, ...
  as today. The tray never waits for the disk.
- E9: `Exit` while recordings are still writing: all of them get the 1 second of today together, and the Companion
  ends within 2 seconds with exit code 0.
- E10: A folder entry cannot do its job (the folder cannot be created, Explorer does not start): nothing shows in the
  tray, a warning is logged, the Companion runs on.
- E11: The log folder or file cannot be created or written (no access, disk full): the Companion runs on without the
  lost lines, without a message; tray, connection and recording are unaffected.
- E12: The log grows: a file is continued past 10 MiB in a new file of the same day with a numbered suffix; a new day
  starts a new file; only the 10 newest log files are kept and older ones of this pattern are deleted. Other files in
  the folder are never touched.
- E13: A second start in the same session ends at once as today and writes nothing to the log.
- E14: A pipe in `Protocol mismatch` skipping frame after frame does not log a line per frame: only state changes are
  logged.

## Non-functional requirements

- Tooltip: at most two lines, within the length Windows' tray tooltip takes (127 characters in .NET's `NotifyIcon`,
  unverified, to be checked in the plan); the longest status text plus the last-tick line fits.
- Time: the icon and tooltip follow a state change as today (one pending update, the latest state); the menu, the
  folder entries and the recording toggle never block on the pipe, the disk or the log; `Exit` stays within 2 seconds.
- Privacy and security: the log holds no pipe content (no headline, island name or economic value) and no full path
  the Companion builds itself (recording files by name only, folders relative to `%LOCALAPPDATA%`); a replay path from
  the command line is logged by its file name with control and format characters replaced. Exception texts from
  Windows are logged as given and may name a path. The folders are fixed, never taken from the pipe or a file.
  Touches the security areas *File paths for recordings* and *Logging of external data* (`.squad/project.md`).
- Log level Information and above; no configuration (no settings exist yet). Times local with UTC offset.
- Memory: the last tick is kept as its time and island count only.
- Appearance: no window is opened; the context menu is light also with Windows set to dark mode.
- Compatibility: Windows 10 22H2 and Windows 11 x64; the release ZIP still holds exactly one file,
  `Censio.Companion.exe`, self-contained, with every new library and the icons embedded (record 0027). `--record`,
  `--replay`, `--speed` and the recording file format (version 1) are unchanged.

## Acceptance criteria

- [ ] AC1: While receiving, the tooltip's second line and the menu's last-tick entry show the receive time of the
  latest closed tick in local `HH:mm:ss` (with date when not today) and its island count; before the first tick the
  tooltip has one line and the menu shows `Last tick: none`; the value survives the end of a connection (S1, S2, E2,
  E3, E4).
- [ ] AC2: The menu holds exactly the entries of S2 in that order, greyed as S2, E1 and S11 say, with the current
  status text (S2, E1, S11).
- [ ] AC3: The icon shows the waiting, receiving or problem look for each connection, replay and command-line state
  as S3, S11 and E1 assign (S3, S11, E1).
- [ ] AC4: `Start recording` and `Stop recording` start and stop a recording at any connection state, the entry text
  and the ` (recording)` / ` (recording failed)` suffix follow, each start writes a new file, and `--record` starts
  with recording on (S4, S6, S7, S8, E7, E8).
- [ ] AC5: A recording started from the menu mid-connection and replayed with `--replay` shows `Replay: Receiving`
  (or `Replay: Protocol mismatch` when the live run was in mismatch), waits for no time before its first frame, ends
  with `Replay finished`, and its ticks opened after the first frame equal the live run's (S5, E5, E6).
- [ ] AC6: `Exit` writes the queues of every recording for at most 1 second together and ends within 2 seconds with
  exit code 0 (E9).
- [ ] AC7: The folder entries open the fixed folders, creating them when missing; a failure logs a warning and
  changes nothing else (S9, E10).
- [ ] AC8: The log file is written as S10 describes, rotates and is pruned as E12 says, logs state changes only (E14),
  holds no pipe content and no path beyond the rules of the privacy requirement, and its failure changes nothing else
  (S10, E11, E12, E14).
- [ ] AC9: A second instance writes no log line (E13).
- [ ] AC10: The release ZIP holds exactly `Censio.Companion.exe`, which starts on a Windows x64 machine without
  installed .NET and shows the tray icon and menu of S2 (verified by the maintainer's manual run on Windows).

## Out of scope

- Settings, a log level setting, autostart (#15), the connection to the server, a diagnostics or any other window and
  its theme (record 0017), a dark tray menu, German tray texts.
- Balloon notifications, starting a replay from the menu, choosing the recordings or log folder, deleting recordings.
- An icon for the `.exe` file in Explorer and file properties.
- Whether Windows sign-out or shutdown gives the recordings their 1 second (unverified today, unchanged).

## Open questions

- Does `--record` stay now that the menu toggles recording? Answered: yes; it starts with recording on (S7). The issue
  asks for a toggle and removes nothing, and the README documents `--record` for testers.
- Which "time of the last tick": game time or receive time? Answered: the receive time in local time; the game time is
  the game's own counter, not a clock time the user reads ([`pipe.md`](../../docs/areas/pipe.md), *Ticks and
  sessions*).
- Which windows need the light theme? Answered: none exist; this feature opens none, and the settings and diagnostics
  windows of record 0017 come with later issues. The menu is light (NFR).
- What does a recording started mid-connection need? Answered: the connection's last `Version` frame first (record
  0036, *Consequences*; [`pipe.md`](../../docs/areas/pipe.md)); written with the receive time of the first recorded
  frame so that the replay waits for nothing before it (S5).
- Where does the log go? Answered: the issue says local app data; `%LOCALAPPDATA%\Censio\Logs`, beside
  `Recordings`.
- May the recording toggle work during a replay? Answered: no; a replay does not open the pipe
  ([`companion.md`](../../docs/areas/companion.md), *Replay*), so there is nothing to record (S11).

## Area documents

- [`docs/areas/companion.md`](../../docs/areas/companion.md): icon looks per state, two-line tooltip, the new menu,
  the recording toggle with `--record` as the start state, the folder entries, the log (folder, file name, rotation,
  what is logged, privacy); *Limits and errors* loses "logs nothing" and "cannot be started or stopped".
- [`docs/areas/pipe.md`](../../docs/areas/pipe.md): the live connection assembles ticks; *Recorder started in the
  middle of a connection* and its residual risk become the rule of E5 and S5.
- `docs/ARCHITECTURE.md` (component row of `Censio.Companion.Core`, the flow paragraph), `README.md` and
  `docs/USER_GUIDE.md` in English and German (*Recording and replay*, *First start*, *Data and logs*, *Reporting
  problems*: where the log is and to check it before sharing).
