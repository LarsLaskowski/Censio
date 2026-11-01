# Spec: Recorder and replay of the statistics pipe

Source: Issue #12
Status: Draft

## Problem

Everything after the pipe reader — the tray (#13), the contract (#27), the history — has to be built and tested
against the stream the game really sends, but today the stream exists only while a game runs with `/pipe`: nothing
keeps it. A developer cannot reproduce a run, an external tester cannot hand in what their game sent, and the
assembler's open questions (record 0034: quiet period, double session end, island counts) cannot be checked
against real data. Raw recordings hold private names and stay on the tester's machine; only anonymised fixtures
reach the repository (record [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md), #20).

Facts from the code this spec builds on: the Companion reads the pipe through the frame reader and the message
reader with the version gate, but does not assemble ticks yet (`PipeConnection.RunSessionAsync` only derives the
tooltip state); the tick assembler takes any message source and an injected clock (record 0034), so a replay can
drive it with recorded times. The Companion has no settings, no log and no window; the tray tooltip is its only
output ([`companion.md`](../../docs/areas/companion.md)).

## Scenarios

The Companion's controls below follow the recommendation of the open question Q1 (escalated); they change with the
Product Manager's answer.

- S1: Given the Companion shows `Censio Companion: Receiving`, when the tester chooses `Start recording` in the
  tray menu, then the tooltip reads `Censio Companion: Receiving (recording)`, the menu entry reads
  `Stop recording`, and a new file `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC start time) appears in
  `%LOCALAPPDATA%\Censio\Recordings`. Its first line is the header
  `{"format":"censio-pipe-recording","version":1,"started":"<UTC time>"}`, its second line the last `Version` frame of
  the connection, and every frame received from then on is one more line
  `{"time":"<UTC receive time>","frame":"<base64 of type byte and payload>"}`.
- S2: Given a recording runs, when the tester chooses `Stop recording`, then the file ends with the last frame
  received before the stop, every line in it is complete, the tooltip drops `(recording)` and the menu entry reads
  `Start recording` again. `Exit` while recording stops the recording the same way.
- S3: Given recording is on, when the game quits and is started again, then the file of the ended connection is
  closed and the next connection writes a new file that starts with that connection's first frame; recording stays
  on until it is stopped.
- S4: Given recording is on and no game runs, when the tester waits, then no file is created until a connection
  delivers its first frame.
- S5: Given no Companion runs, when the tester starts `Censio.Companion.exe --replay <file>`, then the Companion
  does not open the pipe; it feeds the file's frames at their recorded pace through the same reading, version gate
  and tick assembly as a live connection, the tooltip reads `Censio Companion: Replay: <state>` with the states of
  the live connection (`Receiving`, `Protocol mismatch`, `Error`), and after the last frame it reads
  `Censio Companion: Replay finished` until `Exit`.
- S6: Given a recording, when it is replayed with `--speed 10`, the pauses between frames are a tenth of the recorded
  ones; with `--speed max` there are none; without `--speed` the speed is `1` (real speed).
- S7: Given a recording of a live run, when a test replays it at maximum speed, then it receives the same ticks
  (game time, islands with their data, receive time, close reason), session events and version messages as the live
  run, in the same order, whatever the speed, and without waiting in real time.
- S8: Given a test that drives the pipeline with synthetic frames on a fake clock, when it records the run and
  replays the recording, then it can compare both runs item by item (the check of the issue's first criterion).

## Errors and edge cases

- E1: Recording started in the middle of a tick: the first replayed tick lacks the islands received before the
  start, and a session end that the live run delivered as *session end* may replay as *repeated session end*. Every
  later tick is the same.
- E2: Recording started before the connection's first `Version` arrived: no `Version` line is copied; the file
  starts with the frames as they come. Recording started after a `Version` other than 2: that version is copied, so
  the replay shows `Protocol mismatch` as the live run did. The copied `Version` line carries the start time of the
  recording, not its original receive time.
- E3: The connection ends while recording, at a frame boundary or with a truncated, oversized or failed frame: the
  file holds every complete frame received; an incomplete or oversized frame is not recorded. The replay's last tick
  closes as *stream ended*; where the live run closed it by the quiet period, this is the one accepted difference
  (record 0034).
- E4: The recordings folder cannot be created or a write fails (disk full, folder deleted): the recording stops, the
  lines written so far stay complete, the tooltip reads `<state> (recording failed)` until the next start, the menu
  entry reads `Start recording`; the connection and its states are not affected.
- E5: `Start recording` twice, or `Stop recording` without a recording: the menu offers only the entry that applies,
  so neither can happen from the tray; a start or stop that arrives while another one is in progress is applied once.
- E6: Two recordings started within the same second: the second file gets the suffix `-2` (`-3`, …); no file is
  overwritten.
- E7: The frames of a pipe are untrusted, so are the frames in a recording: a replayed frame passes the same limits
  and version gate as a live one, and a frame that is skipped live (empty, unknown type, malformed) is recorded and
  replayed as skipped.
- E8: Replay file problems, each ending the replay with the tooltip
  `Censio Companion: Replay error: <reason>` after the frames before it were replayed: `file not found` (also no
  access), `not a recording` (first line is not the header), `unsupported version` (header version other than 1),
  `invalid line <n>` (not valid JSON of the frame form, invalid base64, a frame over 1 MiB, a line over the size such
  a frame needs, or a missing time). A last line cut off by a crash or power loss is `invalid line <n>` too.
- E9: Recorded times that go backwards or repeat: the replay does not wait for them and the assembler's clock does
  not go back; a large gap is waited for at the chosen speed (`Exit` ends the wait).
- E10: Invalid command line: `--speed` without `--replay`, a speed that is not `max` or a number from `0.1` to
  `1000` written with `.` (also on a German Windows), or `--replay` without a file: the Companion shows
  `Censio Companion: Replay error: invalid command line` and neither replays nor opens the pipe.
- E11: A Companion already runs: a replay start ends at once like any second start (single instance,
  [`companion.md`](../../docs/areas/companion.md)); the tester closes the running one first.
- E12: An empty recording (header only) replays as finished at once; an empty file is `not a recording`.
- E13: During a replay the tray offers no `Start recording` (a replay is not recorded).

## Non-functional requirements

- Recording never changes the live run: the frames, states and ticks of a connection are the same with and without
  it, and a frame is not dropped from the recording because the disk is slower than the pipe.
- The folder is fixed (`%LOCALAPPDATA%\Censio\Recordings`); no part of a file name or path comes from pipe data.
- Privacy: a recording holds the savegame headline, island names and economic data as the game sent them. It stays
  local, is never sent anywhere, and is never deleted automatically; README and user guide say so and say that only
  anonymised fixtures (#20) may be shared publicly.
- Size: one line per frame, about a third larger than the frame (base64); no cap and no rotation. A replay holds one
  line in memory at a time, whatever the file size.
- Times are written in UTC with full precision, so a replay reproduces every receive time exactly.
- A replay file is read only and never changed; replay works on Linux too (tests), the tray only on Windows.
- No new configuration file and no log; the Companion's only outputs stay the tooltip and the files.

## Acceptance criteria

- [ ] AC1: A synthetic live run (frames through a stream into the tick assembler on a fake clock), recorded and
  replayed at maximum speed and at a factor, yields the same ticks, session events and version messages in the same
  order, including receive times and close reasons, except the last tick's close reason as E3 allows; a recording
  started in the middle of that run differs from it only in its first tick and first session end as E1 describes
  (S7, S8, S6, E1, E3).
- [ ] AC2: Frames received between a start and a stop of a recording are in the file exactly once, in order, each
  as one complete line, also when start and stop fall between the parts of a frame that arrives in chunks, and frames
  outside that span are not (S1, S2).
- [ ] AC3: A recording file starts with the header line, then the copied `Version` line under the rules of E2, then
  one line per frame in the form of S1; its name follows S1 and E6 (S1, E2, E6).
- [ ] AC4: With recording on, each connection writes its own file, created at its first frame; a closed connection
  closes its file (S3, S4, E3).
- [ ] AC5: The tray menu offers `Start recording` or `Stop recording` as S1, S2 and E13 describe, and the tooltip shows
  `(recording)` and `(recording failed)` as S1 and E4 describe; `Exit` while recording leaves a complete file (S1, S2,
  E4, E5, E13).
- [ ] AC6: A write failure stops the recording without changing the connection's states or its frames (E4).
- [ ] AC7: A replayed frame goes through the same version gate, limits and skip rules as a live one (E7).
- [ ] AC8: Each replay file problem of E8 and E12 ends the replay with its reason after the frames before it;
  memory stays at one line (E8, E12).
- [ ] AC9: Replay waits the recorded pauses divided by the speed, never for times that go backwards, and its ticks
  carry the recorded times at every speed (S6, E9).
- [ ] AC10: The Companion parses `--replay <file>` and `--speed <value>` as S5, S6 and E10 describe, shows the
  replay tooltips and does not open the pipe while replaying (S5, E10, E11).
- [ ] AC11: README and user guide describe recording, the folder, the file's privacy, and replay with its switches,
  English first, then German (S1, S5).

## Out of scope

- Anonymising recordings (#20) and committing real recordings as fixtures (#17).
- Sending replayed ticks anywhere: the Companion assembles them, but has no consumer yet (#13, #27); during a replay
  only the tooltip shows its progress.
- Choosing a recording folder, a size cap, rotation or automatic deletion.
- Recording during a replay (E13), and replaying while the pipe is also read.
- A replay control in the tray (pause, seek, choosing a file); replay starts only from the command line.
- Recording the stream-ending error itself: E3's difference stays.

## Open questions

- Q1: How does a tester start and stop recording in the Companion? Escalated to the Product Manager. Options:
  (a) a tray menu entry `Start recording` / `Stop recording` at runtime, with `(recording)` in the tooltip
  (recommended: testers use the tray, not a console, and can record only the part they need — the scenarios above
  assume it); (b) a command-line switch `--record` that records every connection from start until `Exit`, no new
  menu entry; (c) both.
- Q2: What is "one file per session"? Answered: one file per connection while recording is on, and a new file
  per start. A game session (`SessionStart` to `SessionEnd`) cannot be a file: the game sends `Version` only once
  after the connect, and the version gate skips every frame of a file without it (`PipeMessageReader`, *Version
  gate* in [`pipe.md`](../../docs/areas/pipe.md)).
- Q3: Which times do replayed ticks carry? Answered: the recorded ones; record 0034 says replay drives the clock
  from the recorded times, so the ticks are the live run's at every speed.
- Q4: Where is "the local app data folder"? Answered: `%LOCALAPPDATA%\Censio\Recordings` — the per-user local
  folder, as the recording path must not come from external input (`.squad/project.md`, *Security areas*).
- Q5: Is a frame that the live reader skipped recorded? Answered: yes, recording is raw: every frame the frame
  reader delivered whole, skipped or not; frames it could not read whole are not frames (E3).

## Area documents

- `docs/areas/pipe.md`: a *Recording and replay* section — the file format (header, line forms, encoding, UTC
  times), what is recorded (whole frames, the copied `Version`), one file per connection, the replay's clock from
  recorded times, speeds, the replay errors and the E1/E3 differences; *Not here* loses "Recording the stream", the
  index row in `docs/areas/README.md` loses "Recording".
- `docs/areas/companion.md`: the tray menu entry, the tooltip suffixes, the recordings folder, the command-line
  switches and the replay tooltips, and that a replay start obeys the single-instance rule.
