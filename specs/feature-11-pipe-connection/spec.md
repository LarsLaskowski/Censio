# Spec: Pipe connection: discovery, reconnect and single-reader handling

Source: Issue #11
Status: Draft

## Problem

Nothing opens the game's pipe yet: the Companion shows its tray icon and reads nothing. A player starts it before or
after the game, quits and restarts the game, loads saves, visits the main menu, and may run another pipe tool. The
Companion has to find the game on its own, survive all of this without a restart, never crash when someone else
holds the pipe, and never hang on `Exit`; the player, the tray (#13) and the recorder (#12) need the connection state.

Checked against the reference reader (`UbisoftMainzAnno/anno117_pipe_example`, read on 2026-11-01) and the capture
notes of `Dealwatch/Tabularium117` (`docs/protocol.md`):

- The pipe is `\\.\pipe\anno117`; the game is its server and creates it only with the launch argument `/pipe`.
  Confirmed.
- The reference opens it read-only with share mode `0` and never writes. Confirmed. That a second client gets "pipe
  busy" is unconfirmed: whether the game admits more than one client is unknown.
- A read fails with `ERROR_BROKEN_PIPE` when the game exits; the reference then waits for the pipe again, with
  `WaitNamedPipe(NMPWAIT_WAIT_FOREVER)`, which can block without end. Confirmed.
- Loading a save and the main menu keep the connection (`SessionEnd`, `SessionStart`, a tick; in the main menu
  `SessionEnd`, then nothing); the game sends `Version` first on every connection (capture, `[live]`).

## Scenarios

The tray tooltip reads `Censio Companion: <state>`, with `<state>` one of `Waiting for game`, `Connected`,
`Receiving`, `Pipe busy`, `Protocol mismatch`, `Error`. Every change of state is also raised as an event for the
other parts of the Companion. The states are decided from what the pipe reader of #9 returns (messages and error
kinds, `docs/areas/pipe.md`); the tick assembler is attached with its first consumer (*Out of scope*).

- S1: Given the game is not running, when the player starts the Companion, then the first attempt runs at once and
  the tooltip reads `Censio Companion: Waiting for game`; when the player then starts the game with `/pipe`, then
  within 10 seconds of the game creating the pipe the tooltip reads `Connected` and, once the game's `Version 2`
  arrives, `Receiving`.
- S2: Given the game is running with `/pipe`, when the player starts the Companion, then the tooltip goes from
  `Waiting for game` to `Connected` to `Receiving` within seconds, without any action by the player.
- S3: Given the tooltip reads `Receiving`, when the player quits the game, then the tooltip reads
  `Waiting for game` (after a moment of `Error` if the game closed the pipe inside a frame, E5); when the player
  starts the game again, then it reads `Receiving` again (as S1), without a restart of the Companion.
- S4: Given the tooltip reads `Receiving`, when the player loads another save or goes to the main menu and back,
  then the tooltip keeps reading `Receiving` and the Companion keeps the connection.
- S5: Given any state, when the player clicks `Exit`, then the icon disappears at once and the process ends within
  2 seconds; the pipe is released, so a new Companion or another tool can connect.
- S6: Given the Companion is connected, it reads everything the game sends without pause, so the game is never
  held up by an unread pipe.

## Errors and edge cases

- E1: The game runs without `/pipe`, or not at all: the tooltip stays `Waiting for game`, never `Error` or
  `Pipe busy`; a missing pipe is recognized at once. The two causes look the same.
- E2: Another program (a second pipe tool, a Companion of another Windows user) holds the pipe and the game admits
  no second client: `Pipe busy`, retried until the other program lets go. No crash, no dialog.
- E3: A protocol-mismatch error (a version other than `2`, or any frame before the first `Version`, E11):
  `Protocol mismatch`, the event carrying the received version or none. The Companion stays connected and keeps
  reading without decoding (`docs/areas/pipe.md`, *Version gate*); a `Version 2` on that connection gives
  `Receiving`; the mismatch errors of the frames skipped meanwhile raise no new event.
- E4: A skipped frame (empty, unknown type, malformed): the state does not change.
- E5: The stream ends with an error (truncated frame, oversized frame, read failed): `Error` with the kind; the pipe
  is closed and the next attempt's result replaces `Error`. A game that quits inside a frame ends as a truncated
  frame, so the player sees `Error` for about a second before `Waiting for game`; accepted, and the user guide says so.
- E6: Opening fails for another reason (for example access denied when the game runs as administrator): `Error`
  with the Windows error code, retried; each attempt's result replaces it.
- E7: The pipe exists but no instance becomes free: the attempt waits at most 2 seconds, then gives `Pipe busy`.
- E8: The game sends nothing for minutes (main menu, pause, frozen): the state stays; there is no read timeout.
  `Exit` still works (S5).
- E9: The pipe opens and closes at once, over and over (a crashing game, a hostile pipe sending `Version 2` and then
  an oversized frame): such a connection does not reset the retry delay, so attempts slow to one every 10 seconds;
  one event per change of state, not per attempt.
- E10: A handler of a state event throws: the connection and the other handlers go on.
- E11: Another program created `\\.\pipe\anno117` first: it is read as the game, within the limits of
  `docs/areas/pipe.md`, and cannot act with the player's Windows rights through the connection.

## Non-functional requirements

- Attempts: the first runs at once at start. Each result replaces the state: not found gives `Waiting for game` at
  once, no free instance within 2 seconds `Pipe busy`, any other failure `Error` with the Windows code, success
  `Connected`.
- Retry delay: one counter of consecutive failures spans all kinds, a connection that ended without counting as
  successful included. The delays are 1, 2, 4, 8, then 10 seconds. A connection that was in `Receiving` for at
  least 30 seconds, however it ended, resets the counter. Time comes from an injected clock. So the Companion
  connects within 10 seconds of the pipe appearing.
- Time: one attempt takes at most 2 seconds. `Exit` removes the icon at once and ends the process within 2 seconds
  in every state; the tray's UI thread never waits on a pending pipe call. Events may come on any thread; the tray
  moves the tooltip update to its UI thread.
- Security: read access only, nothing written; the pipe's server cannot impersonate the Companion's Windows
  identity; content read within the limits of `docs/areas/pipe.md`; events carry no pipe content.
- Memory: one frame and its message (the reader's limit); nothing is kept once the state is updated.
- Platform: the logic is platform-neutral and tested on Linux with a fake pipe; only opening the named pipe and
  classifying its Windows result are Windows code.
- Configuration: none new; the pipe name, delays and timeout are fixed.
- Logs: none until #13; the events carry the state and its cause (error kind, Windows error code, received version).

## Acceptance criteria

- [ ] AC1: Before its first attempt the connection is in `Waiting for game` and the tooltip reads
  `Censio Companion: Waiting for game`; the first attempt runs without delay; each state is shown in the tooltip
  with exactly the texts listed under *Scenarios* (S1, S2).
- [ ] AC2: While the pipe does not exist, each attempt ends at once, the attempts follow the retry delays 1, 2, 4,
  8, 10, 10 seconds and the state stays `Waiting for game` with no further event (S1, E1, E9).
- [ ] AC3: When an attempt opens the pipe, the state becomes `Connected`; when the first `Version 2` message
  arrives, it becomes `Receiving` (S1, S2).
- [ ] AC4: When the stream ends without an error at a frame boundary (the game exited or closed the pipe), the
  state becomes `Waiting for game` and the next attempt follows after the retry delay; when it ends inside a frame,
  the state becomes `Error` with the truncated-frame kind and then the next attempt's result; a pipe that appears
  again is connected (S3, E5).
- [ ] AC5: Session-start, session-end and island messages on an open connection keep the state `Receiving` and
  cause no new attempt (S4, E8).
- [ ] AC6: An attempt on an existing pipe without a free instance waits at most 2 seconds, sets `Pipe busy` and is
  retried with the retry delay; the state changes once the pipe is free (E2, E7).
- [ ] AC7: A protocol-mismatch error sets `Protocol mismatch` with the received version, or with none when no
  version came; the connection stays open and keeps reading; further mismatch errors raise no new event; a
  following `Version 2` sets `Receiving` (E3).
- [ ] AC8: An empty-frame, unknown-type or malformed-message error does not change the state (E4).
- [ ] AC9: A truncated-frame, oversized-frame or read-failed error sets `Error` with that kind, closes the pipe and
  is followed by an attempt after the retry delay, whose result replaces `Error` (E5).
- [ ] AC10: An attempt that fails for another reason sets `Error` with the Windows error code and is retried with
  the retry delay; the next attempt's result replaces it (E6).
- [ ] AC11: Stopping the connection cancels a pending attempt, delay or read at once — with a fake clock, the stop
  completes without the clock being advanced —, closes the pipe, and no event follows it; on Windows, `Exit`
  removes the icon at once and ends the process within 2 seconds in every state (S5, E8).
- [ ] AC12: An event is raised exactly once per change of state or cause, in order; a handler that throws stops
  neither the connection nor the other handlers (E3, E9, E10).
- [ ] AC13: The pipe is opened by name `\\.\pipe\anno117`, with read access only, and with an impersonation level
  that does not let the pipe's server act as the user; the connection never writes to it (E11).
- [ ] AC14: While connected, the connection requests the next message as soon as the previous one is handled, with
  no delay of its own (S6).
- [ ] AC15: A missing pipe is never reported as `Pipe busy` and never takes the 2-second wait; on Windows the
  attempt's result is classified from the Windows error of the opening: not found, no free instance or expired
  wait, anything else (E1, E2, E6, E7).
- [ ] AC16: One retry counter spans all failure kinds; it is reset only after a connection that was in `Receiving`
  for at least 30 seconds on the injected clock, so a connection that reaches `Receiving` and fails within 30
  seconds lets the delay keep growing (E9).
- [ ] AC17: Manual test on Windows, as the issue asks: the Companion started before and after the game, the game quit
  and restarted, another save loaded, the main menu visited, and a second pipe client started if one is at hand;
  the tooltip shows the states of S1 to S5, E2 and E1, and the Companion never needs a restart (S1–S5, E1, E2).
- [ ] AC18: `README.md` and `docs/USER_GUIDE.md` (English first, then German) state that the game needs `/pipe`,
  list the six tooltip states with their meaning, note the brief `Error` a game exit may show, and describe the
  reconnect behavior (S1–S5, E1–E3, E5).

## Out of scope

- Delivering messages or ticks to consumers, and decoupling a slow consumer from the reading: there is no consumer
  yet; the recorder (#12) consumes frames and the tray (#13) ticks, so the hand-off, and the tick assembler behind
  the connection, are built with them.
- Icon variants per state, the time of the last tick, the island count, and logging (#13).
- Recording (#12); forwarding to the server (#40).
- How to add `/pipe` in Ubisoft Connect and Steam, step by step (#16); this feature only says that it is needed.
- Telling the game from another program that created the pipe (E11 is handled by the limits, not by detection).
- Reading alongside another client, or taking the pipe from it.
- Settings for the pipe name, the delays or the timeout.

## Open questions

- Does this feature show the state, or only raise events for #13? Answered: the tooltip shows it. The issue's manual
  test and its documentation for external testers need a visible state; the tooltip is the smallest, and #13's
  status and icon build on it. Lead decision within both issues' scope.
- What counts as `Receiving`? Answered: the first `Version 2` message; the main menu may send nothing for long, so
  waiting for an island frame would leave a working connection in `Connected`.
- Does loading a save break the pipe? Answered: no (capture, `[live]`); if a future game recreates it, S3 applies.
- Can busy be told from missing? Answered: yes; Windows reports `ERROR_FILE_NOT_FOUND` and `ERROR_PIPE_BUSY`
  (`CreateFile`, `WaitNamedPipe`). How the Companion obtains them is the plan's.
- Does the game admit a new client after the Companion exits? Answered: the reference reconnects after a broken pipe;
  confirmed only by AC17, and a residual risk in `docs/areas/pipe.md`.

## Area documents

- `docs/areas/pipe.md`: a new section *Connection* (pipe name, read-only opening without impersonation, the six
  states and their transitions, attempt classification, retry delays and their reset, the 2-second wait, no read
  timeout, the brief `Error` of a quit inside a frame, the stop, the residual risks); *Scope*, *Not here* and the
  index row in `docs/areas/README.md` lose "opening the pipe and reconnecting"; the slow-consumer pointer to #11 moves
  to #12 and #13.
- `docs/areas/companion.md`: the tooltip `Censio Companion: <state>` with the six texts, linking `pipe.md`; `Exit`
  ends the process within 2 seconds.

## Challenge

- M1 (missing game shown as `Pipe busy`): accepted. Each attempt is classified by its Windows result; a missing pipe
  gives `Waiting for game` at once, and the 2-second wait applies only to an existing pipe (*Attempts*, E1, E7, new
  AC15). The `ConnectAsync` behavior is the plan's to verify.
- m1 (reader or assembler): accepted. The connection reads with the reader of #9; the assembler comes with its first
  consumer. AC5 and the memory requirement are worded on the reader's messages and limit.
- m2 (result replaces the state): accepted; *Attempts*, E5, E6, AC9, AC10.
- m3 (quit mid-frame gives `Error`): accepted as behavior, since a truncated stream cannot be told from a broken
  one; S3, E5, AC4 (both endings), AC18 (user guide).
- m4 (mismatch without version, repeats): accepted; E3, AC7, AC12.
- m5 (hostile pipe resets the backoff): accepted. Reset only after 30 seconds in `Receiving`, the assembler's quiet
  period (*Retry delay*, E9, new AC16).
- m6 (threads, Exit on the UI thread): accepted; *Threads*, the `Exit` requirement, S5, AC11.
- m7 (one counter, first attempt at once): accepted; *Attempts*, *Retry delay*, AC1, AC16.
