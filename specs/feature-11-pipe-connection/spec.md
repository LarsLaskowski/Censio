# Spec: Pipe connection: discovery, reconnect and single-reader handling

Source: Issue #11
Status: Draft

## Problem

The pipe reader of #9 and the tick assembler of #10 work on any byte stream, but nothing opens the game's pipe yet:
the Companion starts, shows its tray icon and reads nothing. A player starts the Companion before or after the game,
quits and restarts the game, loads other saves and returns to the main menu, and may run another pipe tool
alongside. Today the player would have to do something for each of these. The Companion has to find the game on its
own, survive all of them without being restarted, never crash because the pipe is held by someone else, and never
keep the player waiting when they exit it. The tray app (#13) and the recorder (#12) need to know the connection
state, and the player needs to see it to tell whether data arrives.

Facts this spec rests on, checked against the reference reader (`UbisoftMainzAnno/anno117_pipe_example`,
`src/pipe.h`, `src/pipe.cpp`, read on 2026-11-01) and the third-party capture `Dealwatch/Tabularium117`,
`docs/protocol.md`:

- The pipe is `\\.\pipe\anno117`; the game is the pipe server and creates it only when started with the launch
  argument `/pipe` (reference `README.md`). Confirmed.
- The reference opens it with read access only and share mode `0`, and never writes. Confirmed. That a second
  client gets "pipe busy" is not confirmed: whether the game accepts more than one client is unknown (Tabularium
  `[open]`); the issue's "may" stands.
- A read fails with `ERROR_BROKEN_PIPE` when the game exits or closes the pipe, and the reference then waits for
  the pipe again. Confirmed (reference code). The reference waits with `WaitNamedPipe(NMPWAIT_WAIT_FOREVER)`, so it
  can block without end; this is why the issue asks for a timeout.
- Loading a save, and going to the main menu, keep the connection: the game sends `SessionEnd`, `SessionStart` and
  a tick on the same pipe; in the main menu it sends `SessionEnd` and then nothing (Tabularium `[live]`). On every
  connection the game sends `Version` first.

## Scenarios

The tray tooltip reads `Censio Companion: <state>`, with `<state>` one of `Waiting for game`, `Connected`,
`Receiving`, `Pipe busy`, `Protocol mismatch`, `Error`. Every change of state is also raised as an event for the
other parts of the Companion.

- S1: Given the game is not running, when the player starts the Companion, then the tooltip reads
  `Censio Companion: Waiting for game`; when the player then starts the game with `/pipe`, then within 10 seconds
  of the game creating the pipe the tooltip reads `Connected` and, once the game's `Version 2` arrives, `Receiving`.
- S2: Given the game is running with `/pipe`, when the player starts the Companion, then the tooltip goes from
  `Waiting for game` to `Connected` to `Receiving` within a few seconds, without any action by the player.
- S3: Given the tooltip reads `Receiving`, when the player quits the game, then the tooltip reads
  `Waiting for game`; when the player starts the game again, then it reads `Receiving` again (as S1), without a
  restart of the Companion.
- S4: Given the tooltip reads `Receiving`, when the player loads another save or goes to the main menu and back,
  then the tooltip keeps reading `Receiving` and the Companion keeps the connection.
- S5: Given any state, when the player clicks `Exit`, then the icon disappears and the process ends within
  2 seconds; the pipe is released, so a new Companion or another tool can connect.
- S6: Given the Companion is connected, while it stays connected it reads everything the game sends, without pause,
  so the game is never held up by an unread pipe.

## Errors and edge cases

- E1: The game runs without `/pipe`, or is not installed: the tooltip stays `Waiting for game` for as long as the
  Companion runs; the Companion keeps trying and never shows an error for it. The two causes look the same.
- E2: Another program (a second pipe tool, a Companion of another Windows user) holds the pipe and the game admits
  no second client: the tooltip reads `Pipe busy`; the Companion keeps trying and connects once the other program
  lets go. No crash, no dialog.
- E3: The game sends a protocol version other than `2` (a game update): the tooltip reads `Protocol mismatch`; the
  Companion stays connected and keeps reading, decodes nothing (`docs/areas/pipe.md`, *Version gate*), and reads
  `Receiving` once a `Version 2` arrives on that connection. After a reconnect it shows the state of the new
  connection.
- E4: A frame is skipped (empty frame, unknown type, malformed message): the state does not change.
- E5: The stream ends with an error (truncated frame, oversized frame, read failed — the reader ends the stream on
  each, `docs/areas/pipe.md`): the tooltip reads `Error`; the Companion closes the pipe and connects again after the
  retry delay. The event carries the error kind, no pipe content.
- E6: Opening the pipe fails for another reason (for example access denied when the game runs as administrator and
  the Companion does not): the tooltip reads `Error`; the Companion keeps trying with the retry delay. The event
  carries the Windows error code.
- E7: An attempt to open the pipe does not complete (the pipe exists but no instance becomes free): the attempt is
  abandoned after at most 2 seconds and counts as a failed attempt (`Pipe busy`).
- E8: The game is in the main menu, paused, or frozen, and sends nothing for minutes: the state stays as it was
  (`Receiving`); there is no read timeout, since the main menu legitimately sends nothing. `Exit` still ends the
  process within 2 seconds (S5).
- E9: The pipe opens and closes again at once, over and over (a crashing game, a hostile pipe): the Companion never
  tries more often than the retry delay allows, and raises one event per change of state, not one per attempt.
- E10: A part of the Companion that handles a state event throws: the connection goes on, and the other handlers
  still receive the event.
- E11: A program other than the game created `\\.\pipe\anno117` first: the Companion cannot tell it from the game
  and reads it as the game, within the limits of `docs/areas/pipe.md`; that program cannot act with the player's
  Windows rights through the connection (non-functional requirements).

## Non-functional requirements

- Retry delay: after a failed attempt, or after a connection ended, the next attempt waits 1 second; each further
  consecutive failure doubles the delay up to 10 seconds (1, 2, 4, 8, 10, 10, … seconds). A connection that reached
  `Receiving` resets the delay to 1 second. So the Companion connects within 10 seconds of the game creating the
  pipe, and a missing game costs one attempt every 10 seconds.
- One attempt to open the pipe takes at most 2 seconds. `Exit` ends the process within 2 seconds in every state,
  also during an attempt, a delay or a read.
- Security: the pipe is opened with read access only and nothing is ever written to it. The Companion opens it so
  that the pipe's server cannot act with the Companion's Windows identity (no impersonation of the client). Pipe
  content is read with the limits of `docs/areas/pipe.md` and never trusted. Events carry no pipe content.
- Memory: a connection holds no more than the reader and assembler limits of `docs/areas/pipe.md`; what is read is
  not kept after the state has been updated (no consumer exists yet).
- Platform: the connection logic is platform-neutral and tested on Linux with a fake pipe; only the opening of the
  Windows named pipe is Windows code. Windows 10 22H2 and Windows 11, x64, as `docs/areas/companion.md`.
- Configuration: none new. The pipe name, the delays and the timeout are fixed.
- Logs: none; the Companion logs nothing until #13. The events carry the state and its cause (error kind, Windows
  error code, received version) so #13 can log them.

## Acceptance criteria

- [ ] AC1: Before its first attempt the connection is in `Waiting for game` and the tooltip reads
  `Censio Companion: Waiting for game`; each state is shown in the tooltip with exactly the texts listed under
  *Scenarios* (S1, S2).
- [ ] AC2: While the pipe does not exist, the attempts follow the retry delays 1, 2, 4, 8, 10, 10 seconds and the
  state stays `Waiting for game` with no further event (S1, E1, E9).
- [ ] AC3: When an attempt opens the pipe, the state becomes `Connected`; when the first `Version 2` message
  arrives, it becomes `Receiving` and the retry delay is reset to 1 second (S1, S2).
- [ ] AC4: When the stream ends without an error (the game exited or closed the pipe), the state becomes
  `Waiting for game`, the next attempt follows 1 second later, and a pipe that appears again is connected (S3).
- [ ] AC5: Session end, session start and ticks on an open connection keep the state `Receiving` and cause no new
  attempt (S4, E8).
- [ ] AC6: An attempt that fails because the pipe is busy, or does not complete within 2 seconds, sets `Pipe busy`
  and is retried with the retry delay; the state changes once the pipe is free (E2, E7).
- [ ] AC7: A protocol-mismatch error sets `Protocol mismatch` with the received version, the connection stays open
  and keeps reading, and a following `Version 2` sets `Receiving` (E3).
- [ ] AC8: An empty-frame, unknown-type or malformed-message error does not change the state (E4).
- [ ] AC9: A truncated-frame, oversized-frame or read-failed error sets `Error` with that kind, closes the pipe and
  is followed by an attempt after the retry delay (E5).
- [ ] AC10: An attempt that fails for another reason sets `Error` with the Windows error code and is retried with
  the retry delay (E6).
- [ ] AC11: Stopping the connection cancels a pending attempt, delay or read at once — with a fake clock, the stop
  completes without the clock being advanced —, closes the pipe, and no event follows it; on Windows, `Exit` ends
  the process within 2 seconds in every state (S5, E8).
- [ ] AC12: An event is raised exactly once per change of state or cause, in order; a handler that throws stops
  neither the connection nor the other handlers (E9, E10).
- [ ] AC13: The pipe is opened by name `\\.\pipe\anno117`, with read access only, and with an impersonation level
  that does not let the pipe's server act as the user; the connection never writes to it (E11).
- [ ] AC14: While connected, the connection requests the next message as soon as the previous one is handled, with
  no delay of its own (S6).
- [ ] AC15: Manual test on Windows, as the issue asks: the Companion started before and after the game, the game quit
  and restarted, another save loaded, the main menu visited, and a second pipe client started if one is at hand;
  the tooltip shows the states of S1 to S5, E2 and E1, and the Companion never needs a restart (S1–S5, E1, E2).
- [ ] AC16: `README.md` and `docs/USER_GUIDE.md` (English first, then German) state that the game needs `/pipe`,
  list the six tooltip states with their meaning, and describe the reconnect behavior (S1–S5, E1–E3, E5).

## Out of scope

- Delivering messages or ticks to consumers, and decoupling a slow consumer from the reading: there is no consumer
  yet; the recorder (#12) consumes frames and the tray (#13) ticks, so the hand-off is built with them.
- Icon variants per state, the time of the last tick, the island count, and logging (#13).
- Recording (#12); forwarding to the server (#40).
- How to add `/pipe` in Ubisoft Connect and Steam, step by step (#16); this feature only says that it is needed.
- Telling the game from another program that created the pipe (E11 is handled by the limits, not by detection).
- Reading alongside another client, or taking the pipe from it.
- Settings for the pipe name, the delays or the timeout.

## Open questions

- Does this feature show the state to the player, or only raise events for #13? Answered: it shows the state in the
  tooltip. The issue's acceptance asks for a manual test on Windows and its documentation section for something
  external testers can try; both need a visible state, and the tooltip is the smallest one. #13's "Tooltip/menu:
  status" and icon then build on it. Lead decision within the scope of both issues.
- What counts as `Receiving`? Answered: the first `Version 2` message on the connection. The game sends `Version`
  first on every connection and may send nothing more for a long time in the main menu (Tabularium `[live]`), so
  waiting for an island frame would leave a working connection in `Connected`.
- Does loading a save break the pipe? Answered: no; it sends `SessionEnd`, `SessionStart` and a tick on the same
  connection (Tabularium `[live]`). If a future game recreates the pipe instead, S3's path reconnects it.
- Can "busy" be told from "no pipe"? Answered: yes; Windows reports `ERROR_FILE_NOT_FOUND` for a missing pipe and
  `ERROR_PIPE_BUSY` for one without a free instance (documented for `CreateFile` and `WaitNamedPipe`; the reference
  tests `ERROR_FILE_NOT_FOUND`). How the Companion obtains them is the plan's.
- Does the game accept a new client after the Companion exits or reconnects? Answered: the reference waits for the
  pipe again after a broken pipe and reconnects, so the game recreates or reopens it; confirmed only by the manual
  test (AC15), and named as a residual risk in `docs/areas/pipe.md`.

## Area documents

- `docs/areas/pipe.md`: a new section *Connection* — the pipe name, read-only opening without impersonation, the
  six states and what moves between them, the retry delays and their reset, the 2-second attempt limit, no read
  timeout, the stop; the residual risks (a second client's behavior, reconnect after exit, a foreign pipe server).
  Its *Scope*, *Not here* and index row in `docs/areas/README.md` lose "opening the pipe and reconnecting"; the
  sentence pointing a slow consumer to "the decoupling of #11" points to #12 and #13.
- `docs/areas/companion.md`: the tooltip `Censio Companion: <state>` with the six texts, linking `pipe.md` for their
  meaning; `Exit` ends the process within 2 seconds; *Not here* loses "reading the statistics pipe" as far as the
  connection state shown is concerned.
