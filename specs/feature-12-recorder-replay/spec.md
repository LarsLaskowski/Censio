# Spec: Recorder and replay of the statistics pipe

Source: Issue #12
Status: Approved by Product Manager

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

- S1: Given no Companion runs, when the tester starts `Censio.Companion.exe --record`, then the Companion connects
  to the pipe as usual and the tooltip reads `Censio Companion: <state> (recording)`. When a connection delivers its
  first frame, a new file `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC time of that frame) appears in
  `%LOCALAPPDATA%\Censio\Recordings`. Its first line is the header
  `{"format":"censio-pipe-recording","version":1,"started":"<UTC time>"}`, and every frame of the connection, its
  first one included, is one more line `{"time":"<UTC receive time>","frame":"<base64 of type byte and payload>"}`.
- S2: Given a recording runs, when the tester chooses `Exit`, or Windows signs out or shuts down, then the file ends
  with the last frame received whole before the stop, every line in it is complete, and the Companion ends as
  without `--record` (within 2 seconds, exit code 0).
- S3: Given the Companion runs with `--record`, when the game quits and is started again, then the file of the ended
  connection is closed and the next connection writes a new file; no file is created while no connection delivers
  a frame.
- S4: Given the Companion is started without `--record`, then nothing is recorded, no folder is created and the
  tooltip and menu are unchanged.
- S5: Given no Companion runs, when the tester starts `Censio.Companion.exe --replay <file>`, then the Companion
  does not open the pipe; it feeds the file's frames at their recorded pace through the same reading, version gate
  and tick assembly as a live connection, the tooltip reads `Censio Companion: Replay: <state>` with the states of
  the live connection (`Receiving`, `Protocol mismatch`, `Error`), and after the last frame it reads
  `Censio Companion: Replay finished` until `Exit`.
- S6: Given a recording, when it is replayed with `--speed 10`, the pauses between frames are a tenth of the recorded
  ones; with `--speed max` there are none; without `--speed` the speed is `1` (real speed).
- S7: Given a recording of a live connection, when a test replays it at maximum speed, then it receives the same
  ticks (game time, islands with their data, receive time, close reason), session events and version messages as
  the live run, in the same order, whatever the speed, and without waiting in real time.
- S8: Given a test that drives the pipeline with synthetic frames on a fake clock, when it records the run and
  replays the recording, then it can compare both runs item by item (the check of the issue's first criterion).

## Errors and edge cases

- E1: The connection ends while recording, at a frame boundary or with a truncated, oversized or failed frame, or
  the Companion exits while a frame is only partly received: the file holds every frame received whole; an
  incomplete or oversized frame is not recorded. The replay's last tick closes as *stream ended*; where the live
  run closed it by the quiet period, this is the one accepted difference (record 0034).
- E2: The Companion is killed or the machine loses power: the lines written before stay complete; at most the last
  line is cut off, and its replay reports it as E6 describes.
- E3: The recordings folder cannot be created or a write fails (disk full, folder deleted): recording stops for the
  rest of the run, the lines written so far stay complete, the tooltip reads `Censio Companion: <state> (recording
  failed)` until `Exit`; the connection and its states are not affected.
- E4: Two connections whose first frames fall in the same second: the second file gets the suffix `-2` (`-3`, …); no
  file is overwritten.
- E5: The frames of a pipe are untrusted, so are the frames in a recording: a replayed frame passes the same limits
  and version gate as a live one, and a frame that is skipped live (empty, unknown type, malformed) is recorded and
  replayed as skipped.
- E6: Replay file problems, each ending the replay with the tooltip
  `Censio Companion: Replay error: <reason>` after the frames before it were replayed: `file not found` (also no
  access), `not a recording` (first line is not the header; also an empty file), `unsupported version` (header
  version other than 1), `invalid line <n>` (not valid JSON of the frame form, invalid base64, a frame over 1 MiB, a
  line over the size such a frame needs, or a missing time). A header-only recording replays as finished at once.
- E7: Recorded times that go backwards or repeat: the replay does not wait for them and the assembler's clock does
  not go back; a large gap is waited for at the chosen speed (`Exit` ends the wait).
- E8: Invalid command line — an unknown argument, `--speed` without `--replay`, a speed that is not `max` or a number
  from `0.1` to `1000` written with `.` (also on a German Windows), `--replay` without a file, or `--record` together
  with `--replay`: the Companion shows `Censio Companion: Invalid command line`, neither records, replays nor opens
  the pipe, and `Exit` works as usual.
- E9: A Companion already runs: a start with `--record` or `--replay` ends at once like any second start (single
  instance, [`companion.md`](../../docs/areas/companion.md)); the tester closes the running one first.

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
- The tray menu stays as it is (`Version <version>`, separator, `Exit`). No configuration file and no log; the
  Companion's only outputs stay the tooltip and the files.

## Acceptance criteria

- [ ] AC1: A synthetic live run (frames through a stream into the tick assembler on a fake clock), recorded and
  replayed at maximum speed and at a factor, yields the same ticks, session events and version messages in the same
  order, including receive times and close reasons, except the last tick's close reason as E1 allows (S6, S7, S8, E1).
- [ ] AC2: Every frame a connection received whole while recording is in its file exactly once, in order, each as
  one complete line, also when the stop (`Exit`, sign-out, shutdown) or the end of the connection falls between the
  parts of a frame that arrives in chunks; the partial frame is not in the file (S2, E1, E2).
- [ ] AC3: A recording file starts with the header line, then one line per frame in the form of S1; its name follows
  S1 and E4 (S1, E4).
- [ ] AC4: With `--record`, each connection writes its own file, created at its first frame and closed when the
  connection ends; without `--record` nothing is written (S1, S3, S4).
- [ ] AC5: The tooltip shows `(recording)` with `--record` and `(recording failed)` after a write failure; a write
  failure stops the recording without changing the connection's states or frames (S1, E3).
- [ ] AC6: A replayed frame goes through the same version gate, limits and skip rules as a live one (E5).
- [ ] AC7: Each replay file problem of E6 ends the replay with its reason after the frames before it; memory stays
  at one line (E2, E6).
- [ ] AC8: Replay waits the recorded pauses divided by the speed, never for times that go backwards, and its ticks
  carry the recorded times at every speed (S6, E7).
- [ ] AC9: The Companion parses `--record`, `--replay <file>` and `--speed <value>` as S1, S5, S6 and E8 describe,
  shows the replay tooltips and does not open the pipe while replaying; a second start ends as E9 says (S5, E8, E9).
- [ ] AC10: README and user guide describe `--record`, the folder, the file's privacy, and replay with its switches,
  English first, then German (S1, S5).

## Out of scope

- Starting or stopping a recording while the Companion runs; a tray entry for recording or replay.
- Anonymising recordings (#20) and committing real recordings as fixtures (#17).
- Sending replayed ticks anywhere: the Companion assembles them, but has no consumer yet (#13, #27); during a replay
  only the tooltip shows its progress.
- Choosing a recording folder, a size cap, rotation or automatic deletion.
- Recording a replay, and replaying while the pipe is also read (E8 refuses both switches together).
- Recording the stream-ending error itself: E1's difference stays.

## Open questions

- Q1: How does a tester start and stop recording in the Companion? Answered: (Product Manager, option b) only by
  the command-line switch `--record`, which records every connection from the Companion's start until `Exit`; no
  tray menu entry.
- Q2: What is "one file per session"? Answered: one file per connection while `--record` is on. A game session
  (`SessionStart` to `SessionEnd`) cannot be a file: the game sends `Version` only once after the connect, and the
  version gate skips every frame of a file without it (`PipeMessageReader`, *Version gate* in
  [`pipe.md`](../../docs/areas/pipe.md)). Since recording starts with the Companion, every file covers its connection
  from the first frame.
- Q3: Which times do replayed ticks carry? Answered: the recorded ones; record 0034 says replay drives the clock
  from the recorded times, so the ticks are the live run's at every speed.
- Q4: Where is "the local app data folder"? Answered: `%LOCALAPPDATA%\Censio\Recordings` — the per-user local
  folder, as the recording path must not come from external input (`.squad/project.md`, *Security areas*).
- Q5: Is a frame that the live reader skipped recorded? Answered: yes, recording is raw: every frame the frame
  reader delivered whole, skipped or not; frames it could not read whole are not frames (E1).

## Area documents

- `docs/areas/pipe.md`: a *Recording and replay* section — the file format (header, line forms, encoding, UTC
  times), what is recorded (whole frames), one file per connection, the replay's clock from recorded times, speeds,
  the replay errors and the E1 difference; *Not here* loses "Recording the stream", the index row in
  `docs/areas/README.md` loses "Recording".
- `docs/areas/companion.md`: the command-line switches `--record`, `--replay` and `--speed`, the recordings folder,
  the tooltip suffixes `(recording)` and `(recording failed)`, the replay tooltips and `Invalid command line`, and
  that a start with a switch obeys the single-instance rule; the menu is unchanged.
