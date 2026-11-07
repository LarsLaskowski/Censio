# Pipe connection

## Scope

How the Companion finds the game's statistics pipe, opens it, keeps the connection, reports its state and
reconnects, and what it keeps of the latest tick across connections. The tray tooltip, the icon and the diagnostics
window rely on it. The rules are platform-neutral and run on an injected clock; only opening the pipe and listing the
existing pipes are Windows code (record 0035). What is read on the connection, and how it is grouped into ticks, is in
[`pipe.md`](pipe.md).

## Behavior

### Opening

- The pipe is `\\.\pipe\anno117` on the local machine. The game creates it only when it is started with the launch
  argument `/pipe`; without it, or with the game not running, the pipe does not exist, and the two cases look the
  same.
- The Companion opens it read-only and asynchronously and never writes. It connects at impersonation level
  identification, so the pipe's server (the game, or any program that created the name first) cannot act with the
  player's Windows identity through the connection.
- Before each attempt the Companion lists the existing pipes of the machine. If `anno117` is not among them (the
  whole name is compared, case-insensitive; `x\anno117` does not count), the attempt ends at once as *not found*
  without opening anything. If the listing itself fails, the pipe counts as existing.
- An attempt waits at most 2 seconds for a free pipe instance. A timeout while the pipe is still listed is *busy*
  (another client holds it); a timeout when it is no longer listed is *not found*. Any other failure of the
  attempt is *failed*, carrying the Windows error code where it has one (access denied is code 5).
- There is no read timeout: a connection that sends nothing stays open and is read without pause.

### States

The state is shown in the tray tooltip ([`companion.md`](companion.md)).

| State | Meaning |
| ----- | ------- |
| Waiting for game | No connection: the pipe does not exist, or the connection ended at a frame boundary. The state before the first attempt |
| Connected | The pipe is open; the game's `Version 2` has not arrived yet |
| Receiving | A message of protocol version 2 has arrived on this connection |
| Pipe busy | The pipe exists but no instance became free within 2 seconds |
| Protocol mismatch | A version other than 2, or a frame before the first `Version`; the connection stays open and keeps being read |
| Error | An attempt failed, or the stream ended with a truncated frame, an oversized frame or a read failure (the kind is part of the status) |

- A message result moves to `Receiving`; a skipped frame (empty, unknown type, malformed) changes nothing. Session
  start, session end and island messages keep `Receiving` and cause no new attempt.
- A protocol mismatch gives `Protocol mismatch` with the received version (none for a frame before any `Version`);
  the mismatch errors of frames skipped meanwhile raise no new event. A `Version 2` on that connection gives
  `Receiving`.
- A stream that ends at a frame boundary gives `Waiting for game`; one that ends inside a frame (a game that quits
  while sending) shows `Error` until the next attempt's result replaces it. An unexpected failure of a read ends
  the connection as read failed. The stream is closed when the connection ends.
- Every change of the state or of its cause is raised as an event on whatever thread the connection runs on; a
  handler that throws affects neither the connection nor the other handlers. An unchanged state raises no event.
  Events carry the state, the error kind, the Windows error code or the received version, and no pipe content. The
  same values are written to the Companion's log ([`companion.md`](companion.md), *Log*), once per change.

### Retry

- The first attempt runs at once after the start. After each attempt that did not lead to a connection and after
  each connection that ended, the next attempt follows after a delay of 1, 2, 4 and 8 seconds and then 10 seconds
  each. One counter spans all failure kinds.
- The counter resets when the connection ended after it had been in `Receiving` without interruption for at least
  30 seconds up to its end; a protocol mismatch in between restarts that measurement. A connection that reaches
  `Receiving` only briefly does not reset the counter, so a pipe that sends `Version 2` and breaks is not retried
  faster than the delays above.
- A pipe that appears again is connected by the next attempt; one that appears between the check and the open is
  found by the next attempt.
- The connection reconnects after an oversized-frame, truncated-frame or read-failed error, which end the stream
  ([`pipe.md`](pipe.md), *Limits and errors*).

### Latest tick

- The live connection requests items until the end of each stream, so it assembles ticks as a replay does
  ([`pipe.md`](pipe.md), *Ticks and sessions*). It keeps a summary of the latest closed tick (its receive time and
  island count) and the tick itself, whole, across connections, and raises an event for each closed tick; a handler
  that throws affects neither the connection nor the other handlers. The diagnostics window is the consumer of that
  tick ([`companion.md`](companion.md), *Diagnostics window*).

### Stop

- Stopping ends the connection within about 50 ms, also while an attempt, a delay or a read is pending, closes the
  pipe and raises no further event. The tray's thread never waits for it: `Exit` removes the icon and ends the
  process within 2 seconds in every state.

## Limits and errors

- A program that created `\\.\pipe\anno117` before the game is read as the game, within the reader's limits, and
  cannot act with the player's Windows rights through the connection; it can still show `Receiving` with false data.
- Observed in the manual test on Windows with the real game (not guaranteed for other game versions):
  - Listing `\\.\pipe\` takes no pipe instance: with the game off the Companion shows `Waiting for game` at once,
    with the game on `Pipe busy` or `Receiving`. A foreign pipe named `x\anno117` with the game off left the state
    at `Waiting for game`, and after the game was started with that pipe still existing the Companion switched to
    `Receiving`.
  - The game serves one client at a time. While the reference reader held the pipe, the Companion showed `Pipe busy`
    and switched to `Receiving` when that reader ended, so the game accepts a new client after the previous one ended.
  - The game sends `Version` at once after the connect, without a loaded save, so `Connected` is not visible in
    practice. A return to the main menu and loading another save while connected kept `Receiving`.
  - A Companion restarted while the game runs shows `Receiving` at once with a loaded save. With the game in the
    main menu it stays at `Pipe busy` until a game is loaded; the game apparently releases the old
    connection only when it next sends something. The Companion keeps retrying meanwhile, with the delays of *Retry*
    (at most 10 seconds apart).
  - `Exit` in `Pipe busy` ended the process in under 2 seconds.

## Related decisions

- [`decisions/0035-pipe-connection-states-retry-and-identification-level.md`](../decisions/0035-pipe-connection-states-retry-and-identification-level.md) — the connection checks that the pipe exists, connects read-only at identification level, and resets its retry delay only after 30 seconds in `Receiving`.
- [`decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md`](../decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md) — ticks are assembled on an injected clock, in the live connection as in a replay.

## Not here

- The frame layout, the error kinds and the tick rules: [`pipe.md`](pipe.md).
- Recording the frames of a connection and replaying them: [`pipe-recording.md`](pipe-recording.md).
- The tooltip texts, the icon looks and the tray menu: [`companion.md`](companion.md).
