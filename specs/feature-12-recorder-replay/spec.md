# Spec: Recorder and replay of the statistics pipe

Source: Issue #12
Status: Approved by Product Manager

## Problem

What comes after the pipe reader (#13, #27, the history) has to be built and tested against the stream the game
really sends, but nothing keeps that stream: a run cannot be reproduced, a tester cannot hand in what their game
sent, and record 0034's open questions cannot be checked against real data. Raw recordings hold private names and stay on the tester's machine; only anonymised fixtures
reach the repository (record [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md), #20).

From the code: the Companion reads the pipe through the message reader but assembles no ticks yet
(`PipeConnection.RunSessionAsync` only derives the tooltip state); the tick assembler takes any message source and
an injected clock (record 0034). The tooltip is the Companion's only output
([`companion.md`](../../docs/areas/companion.md)).

## Scenarios

- S1: Given no Companion runs, when the tester starts `Censio.Companion.exe --record`, then the Companion connects
  to the pipe as usual and the tooltip reads `Censio Companion: <state> (recording)`. When a connection delivers its
  first frame, a new file `recording-<yyyyMMdd-HHmmss>Z.jsonl` appears in `%LOCALAPPDATA%\Censio\Recordings`. Its
  first line is the header `{"format":"censio-pipe-recording","version":1,"started":"<UTC time>"}`, and every frame
  of the connection, its first one included, is one more line
  `{"time":"<UTC receive time>","frame":"<base64 of type byte and payload>"}`. `started` is the receive time of the
  first frame; the file name is the same instant cut to the second. A time is ISO 8601 in UTC with seven fractional
  digits and `Z` (`2026-11-01T17:09:00.1234567Z`); each line, the last included, ends with one line feed. The
  receive time of a frame is the one the pipeline gives it: one clock reading per frame, shared by the recording and
  the ticks.
- S2: Given a recording runs, when the tester chooses `Exit`, or Windows signs out or shuts down, then the file ends
  with the last frame received whole before the stop, every line in it is complete, and the Companion ends as
  without `--record` (within 2 seconds, exit code 0). Frames still waiting to be written are written first, for at
  most 1 second; E10 says what happens to those left after it.
- S3: Given the Companion runs with `--record`, when the game quits and is started again, then the file of the ended
  connection is closed and the next connection writes a new file; no file is created while no connection delivers
  a frame.
- S4: Given the Companion is started without `--record`, then nothing is recorded, no folder is created and the
  tooltip and menu are unchanged.
- S5: Given no Companion runs, when the tester starts `Censio.Companion.exe --replay <file>`, then the Companion
  does not open the pipe; it feeds the file's frames at their recorded pace through the same message reader (with
  its version gate) and tick assembler a test uses, and derives the tooltip from them with the same state rules as
  the live connection, not a copy of them. The tooltip reads `Censio Companion: Replay: Connected` from the start
  until the first replayed frame that changes the live state (skipped frames do not; a first non-empty frame that
  is not a `Version` shows `Protocol mismatch`, as live), then
  `Censio Companion: Replay: Receiving` or `Censio Companion: Replay: Protocol mismatch` as the live states would,
  and after the last frame `Censio Companion: Replay finished` until `Exit`. A replay never shows `Error`: a frame
  that would end a live stream cannot be in a valid file, and a file problem shows as E6 describes.
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
- E2: The Companion is killed or crashes: the lines written before stay complete; at most the last line is cut
  off, and its replay reports it as E6 describes. A power loss can also lose the last lines the operating system
  had not yet written to the disk.
- E3: The recordings folder cannot be created or a write fails (disk full, folder deleted): recording stops for the
  rest of the run, the lines written so far stay complete, the tooltip reads `Censio Companion: <state> (recording
  failed)` until `Exit`; the connection and its states are not affected. A recordings folder Windows cannot name
(not a full path) counts as a folder that cannot be created.
- E4: Two connections whose first frames fall in the same second: the second file gets the suffix `-2` (`-3`, …); no
  file is overwritten.
- E5: The frames of a pipe are untrusted, so are the frames in a recording: a replayed frame passes the same limits
  and version gate as a live one, and a frame that is skipped live (empty, unknown type, malformed) is recorded and
  replayed as skipped.
- E6: Replay file problems, each ending the replay with the tooltip
  `Censio Companion: Replay error: <reason>` after the frames before it were replayed: `file not found` (also no
  access), `not a recording` (first line is not the header; also an empty file), `unsupported version` (header
  version other than 1), `invalid line <n>` (not valid JSON of the frame form, invalid base64, a frame over 1 MiB, a
  line over the size such a frame needs, a missing time, a time too late to add 30 seconds to or not convertible to UTC, nesting deeper than 64, a field
  given twice), `read failed` (the file cannot be read
  on after it was opened). A header-only recording replays as finished at once.
- E7: Recorded times that go backwards or repeat: the replay does not wait before such a frame, and the frame is
  stamped with its recorded time unchanged, so the ticks are the ones the recorded times give; a large gap is waited
  for at the chosen speed (`Exit` ends the wait).
- E8: Invalid command line — an unknown argument, `--speed` without `--replay`, a speed that is not `max` or a number
  from `0.1` to `1000` written with `.` (also on a German Windows), `--replay` without a file, or `--record` together
  with `--replay`: the Companion shows `Censio Companion: Invalid command line`, neither records, replays nor opens
  the pipe, and `Exit` works as usual.
- E9: A Companion already runs: a start with `--record` or `--replay` ends at once like any second start (single
  instance, [`companion.md`](../../docs/areas/companion.md)); the tester closes the running one first.
- E10: The disk is slower than the pipe: frames wait to be written in a queue of at most 16 MiB, each frame counted as its line's base64 length plus 128
  bytes; the
  pipe is read on without waiting for the disk. When a frame does not fit, or frames are still queued 1 second after
  `Exit`, sign-out or shutdown began, the recording stops as E3 describes: the file ends with the last complete line
  written, the frames after it are not recorded, and the live run is unchanged. A line a stalled disk is still
  writing when the second ends may be cut off, as in E2.
- E11: Line format when replaying: a line feed or a carriage return plus line feed ends a line; a missing line feed
  after the last line and one empty line at the end are accepted; an empty line elsewhere is `invalid line <n>`. JSON
  fields may come in any order; fields other than those of S1 are ignored, in the header and in a frame line. A
  UTF-8 byte order mark before the header is accepted (a file saved by a Windows editor). The header needs `format`
  and `version`; `started` is not checked.

## Non-functional requirements

- Recording never changes the live run: the frames, states and receive times of a connection are the same with and
  without it, also with a stalled disk; a slow disk can cost the recording (E10), never the live run.
- Memory: recording adds at most 16 MiB of queued lines (E10's count) to the frame the reader holds.
- The folder is fixed (`%LOCALAPPDATA%\Censio\Recordings`); no part of a file name or path comes from pipe data.
- Privacy: a recording holds the savegame headline, island names and economic data as the game sent them. It stays
  local, is never sent anywhere, and is never deleted automatically; README and user guide say so and say that only
  anonymised fixtures (#20) may be shared publicly.
- Size: one line per frame, about a third larger than the frame (base64); no cap and no rotation. A replay holds one
  line in memory at a time, whatever the file size.
- Times are written to the 100 ns, so a replay reproduces every receive time exactly.
- A replay file is read only and never changed; replay works on Linux too (tests), the tray only on Windows.
- The tray menu stays as it is (`Version <version>`, separator, `Exit`). No configuration file and no log; the
  Companion's only outputs stay the tooltip and the files.

## Acceptance criteria

- [ ] AC1: A synthetic live run (frames through a stream into the tick assembler on a fake clock), recorded and
  replayed at maximum speed and at a factor, yields the same ticks, session events and version messages in the same
  order, including receive times and close reasons, except the last tick's close reason as E1 allows; the test
  moves the fake clock while a frame arrives in chunks, so a second clock reading for the same frame would show as a
  different receive time (S1, S6, S7, S8, E1).
- [ ] AC2: Every frame a connection received whole while recording is in its file exactly once, in order, each as
  one complete line, also when the stop (`Exit`, sign-out, shutdown) or the end of the connection falls between the
  parts of a frame that arrives in chunks; the partial frame is not in the file. This holds unless the recording
  failed as E3 or E10 describe (S2, E1, E2).
- [ ] AC3: A recording file starts with the header line, then one line per frame in the form of S1; its name follows
  S1 and E4 (S1, E4).
- [ ] AC4: With `--record`, each connection writes its own file, created at its first frame and closed when the
  connection ends; without `--record` nothing is written (S1, S3, S4).
- [ ] AC5: The tooltip shows `(recording)` with `--record` and `(recording failed)` after a write failure, a full
  queue or a flush that ran out of time; each stops the recording at a complete line without delaying a read or
  changing the connection's states, frames or receive times; the queue never holds more than 16 MiB (S1, S2, E3,
  E10).
- [ ] AC6: A replayed frame goes through the same version gate, limits and skip rules as a live one (E5).
- [ ] AC7: Each replay file problem of E6 ends the replay with its reason after the frames before it, the line
  forms of E11 are accepted or refused as E11 says, and memory stays at one line (E2, E6, E11).
- [ ] AC8: Replay waits the recorded pauses divided by the speed, never before a time that goes backwards or
  repeats, and stamps every frame with its recorded time unchanged at every speed (S6, E7).
- [ ] AC9: The Companion parses `--record`, `--replay <file>` and `--speed <value>` as S1, S5, S6 and E8 describe,
  shows the replay tooltips of S5 from the live connection's state rules and does not open the pipe while
  replaying; a second start ends as E9 says (S5, E8, E9).
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
- Starting a recording in the middle of a connection (a tray toggle, #13). A recorder that does so must write the
  connection's last `Version` frame first, or the replay shows `Protocol mismatch`; `pipe.md` states that rule.
- The anonymiser's output (#20): it writes recording format version 1 as defined here.

## Open questions

- Q1: How does a tester start and stop recording in the Companion? Answered: (Product Manager, option b) only by
  the command-line switch `--record`, which records every connection from the Companion's start until `Exit`; no
  tray menu entry.
- Q2: What is "one file per session"? Answered: one file per connection. A game session cannot be a file: the game
  sends `Version` once after the connect, and the version gate skips every frame of a file without it (*Version
  gate* in [`pipe.md`](../../docs/areas/pipe.md)).
- Q3: Which times do replayed ticks carry? Answered: the recorded ones (record 0034).
- Q4: Where is "the local app data folder"? Answered: `%LOCALAPPDATA%\Censio\Recordings`, fixed, as a recording
  path must not come from external input (`.squad/project.md`, *Security areas*).
- Q5: Is a frame the reader skips recorded? Answered: yes, every frame read whole is recorded (E1, E5).

## Area documents

- `docs/areas/pipe.md`: a *Recording and replay* section — the file format (header, line forms, encoding, UTC
  times), what is recorded (whole frames), one file per connection, the replay's clock from recorded times, speeds,
  the line rules of E11, the 16 MiB queue and its failure, the replay errors, the E1 difference and the rule for a
  recorder started mid-connection (out of scope); *Not here* loses "Recording the stream", the index row in
  `docs/areas/README.md` loses "Recording".
- `docs/areas/companion.md`: the command-line switches `--record`, `--replay` and `--speed`, the recordings folder,
  the tooltip suffixes `(recording)` and `(recording failed)`, the replay tooltips and `Invalid command line`, and
  that a start with a switch obeys the single-instance rule; the menu is unchanged.

## Challenge

Devil's Advocate, spec challenge (Q1 option b unchanged):

- M1 (slow disk, unbounded memory): accepted. A queue of at most 16 MiB off the read path; a full queue, a write
  failure or a flush past 1 second after `Exit` stops the recording at a complete line as `(recording failed)`; the
  live run is never delayed (S2, E10, AC2, AC5).
- m1 (one clock reading per frame): accepted. S1 defines the recorded time as the pipeline's receive time, read
  once per frame; AC1's test moves the clock during a chunked frame.
- m2 (backward times): accepted. E7 and AC8: no wait, the recorded time is stamped unchanged; no clamping, so no
  second difference.
- m3 (`Replay: Error`): accepted. Removed; S5 names `Replay: Connected` as the first tooltip, also before
  `Version 2`, and says why `Error` cannot occur.
- m4 (#13 starts mid-connection): accepted in the second form. Building it is out of scope; the rule that such a
  recorder writes the connection's last `Version` frame first goes into `pipe.md` for #13.
- m5 (format details): accepted. S1 fixes `started` (first frame's receive time, the file name's instant cut to
  the second), the time form with seven digits and `Z`, and the final line feed; E11 fixes CRLF, final newline,
  empty lines and extra fields; *Out of scope* binds #20 to format version 1.
- m6 (wording): accepted. S5 names the same message reader and tick assembler a test uses, and the live
  connection's state rules, not a copy.
