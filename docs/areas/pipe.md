# Pipe

## Scope

The statistics pipe as the Companion reads it: the frame layout, the message types of protocol version 2, the
1 MiB limit, the error kinds, which errors skip a frame and which end the stream, the version gate, and the
grouping of the messages into ticks and session events with the tick limits, and the connection: discovery,
states, reconnect and stop, and recording the frames to a file and replaying such a file. The pipe is
untrusted input: the format is known only from Ubisoft's example reader (`UbisoftMainzAnno/anno117_pipe_example`,
read on 2026-11-01, "for documentation purposes only"), and a game update may change it without notice. The
reader is in `Censio.Pipe`; it works on any readable byte stream and does not depend on Windows or on the
named pipe. The connection logic is in `Censio.Companion.Core`, the Windows pipe access in the Companion shell. Ticks are assembled on an injected clock, so the same rules serve a live stream and a replay. A recording file is untrusted input as well.

## Behavior

### Reading

- A reader serves one stream and one caller. The caller asks for one result at a time: a message, an error, or the
  end of the stream. The reader never throws for any byte sequence and logs nothing.
- Frames may arrive in any chunking (one byte per read, split between prefix and payload, or all at once); the
  results are the same.
- A read holds at most one frame plus its decoded message in memory, whatever the counts in the payload say.
- A map keeps memory for its distinct GUIDs only: a map whose pairs repeat a GUID is stored at the size of its
  decoded content, not of the pair count on the wire. Results do not change.

### Frame layout

- A frame is a signed 32-bit little-endian length followed by that many bytes, the type byte first. The length does
  not count its own 4 bytes. All numbers are little-endian, floats are IEEE 754 binary32 and delivered as read (NaN
  included).
- A string is one unsigned length byte (0 to 255) and that many bytes. It is decoded as UTF-8, each invalid
  sequence becomes U+FFFD, and it is not trimmed (`Zycada ` keeps its space; `R_mische K_ste` keeps its `_`).
- Bytes after the last field of a payload are ignored; the message is delivered.

### Message types

| Type | Message | Payload |
| ---- | ------- | ------- |
| 0 | Version | int32 protocol version |
| 1 | SessionStart | string (headline) |
| 2 | SessionEnd | empty |
| 3 | AreaProductionStatistics | session ID, island ID, area index (one unsigned byte each, 0 to 255); session GUID (int32); area name (string); timestamp (int64); entry count (int32); that many entries |

- An entry is: product GUID (int32); generation, consumption, delta, perfect generation, perfect consumption
  (float32); building count, maintenance (int32); income (float32); profit (int32); summed and average
  productivity (float32); workforce map; building map. An entry takes at least 56 bytes, its two map counts
  included. Entries are delivered in frame order.
- A map is an int32 pair count and that many (int32 GUID, int32 amount) pairs. It is a set of GUID to amount: its
  order carries no meaning, and a GUID that occurs twice is delivered once with the sum of its amounts.

### Version gate

- A reader starts with the version unknown and tracks it per stream. Only a `Version` frame is decoded while the
  version is unknown or not `2`.
- A frame of any other type before the first `Version`, an unknown type byte included, gives a protocol-mismatch
  error without a received version. An empty frame before any `Version` gives the empty-frame error, not a
  protocol-mismatch error, because it has no type byte to gate.
- After a `Version` other than `2`, the version frame gives a protocol-mismatch error with the received and the
  expected version, and every following frame of another type is skipped undecoded and gives that error again, until
  a `Version 2` arrives.

### Ticks and sessions

The game sends one island per frame and no end-of-tick marker. A tick is the set of island frames that share one
game time (the frame's timestamp); the assembler groups the frames into ticks and delivers them, with session
events, errors and version messages, as typed items.

#### Island key

- An island frame is identified by its session GUID, island ID and area index. The session ID byte and the area name
  are data of the frame, not part of the key: an island that is renamed keeps its key.
- Within a tick every key occurs once. Two frames with the same game time that differ in any part of the key belong to the same tick; a different key closes nothing.

#### Closing a tick

A tick is delivered when it closes, once, and is never reopened. It closes when:

1. an island frame carries another game time, higher or lower (*game time changed*);
2. an island frame repeats a key already in the tick (*island repeated*); the frame opens the next tick;
3. no island frame arrived for 30 seconds (*quiet period*); the period is measured on the injected clock from the
   receive time of the last island frame, and a frame received exactly at the deadline or later belongs to the next
   tick, which may carry the same game time;
4. a session start or a session end arrives (*session event*); the tick is delivered before the event;
5. the stream ends, also with a truncated-frame, oversized-frame or read-failed error (*stream ended*); the tick is
   delivered before the error and the end.

- A version message or a skipped-frame error does not close the tick and does not restart the quiet period: only an
  island frame does.
- The receive time of a tick is the time its last island frame was received; the receive time of a frame is the
  moment its read completed.
- A frame that is dropped for a tick limit does not restart the quiet period and its key does not count as present.

#### No data yet

- An island with no entries is marked as such. This can also mean an island without production, not only a game that
  has not produced data yet.
- A tick whose islands all have no entries is marked *no data yet* (the game sends such a tick after a load).

#### Session events

- A session start is delivered with its headline unchanged (not trimmed, `_` kept).
- A session end is delivered as a session end when an island frame or a session start came since the last session
  end; otherwise, at the start of the stream or after another session end, it is delivered as a *repeated session
  end*. The game sends a second session end; the repeated one carries no new information.
- Ticks carry no session gate: islands are delivered whatever session events came before.

#### Order and end

- Items come in stream order. A tick comes at its close, before the item that closed it.
- After the end of the stream, or after a cancellation, every further request returns the end of the stream.
- A cancelled request throws the platform's cancellation, discards the open tick and ends the stream; the source
  read is cancelled with it. A request after the assembler was disposed throws an object-disposed error, and the
  dispose cancels a pending read. No other exception leaves the assembler.
- The quiet period is waited in steps of at most one day and the clock is read again after each step, so a clock that
  was set back (by a replay's recorded times or by the system) makes the assembler wait on and throws nothing. The
  steps change no item: a deadline that the clock reaches closes the tick as before.
- The caller must keep requesting: the receive time of a frame is stamped when its read completes, but the next read
  starts only on a request. A slow consumer belongs behind the tick buffer (#41) or the hand-off built with the first consumers (#12, #13).

## Recording and replay

The Companion can keep the frames of a connection in a file and feed such a file back through the same message
reader and tick assembler as a live pipe, so the ticks of a run can be reproduced (record 0036, issue #12). The
Companion's switches, folder and tooltips are in [`companion.md`](companion.md).

### What is recorded

- Every frame the frame reader read whole is recorded, in order and exactly once, with the receive time the pipeline
  gave it (one clock reading per frame, shared by the recording and the ticks). That includes the frames the reader
  skips (empty, unknown type, malformed, protocol mismatch): they are recorded and replayed as skipped.
- A frame that is only partly received (the stream ended inside it, or its length is over 1 MiB or negative) is not
  recorded. The error that ended the stream is not recorded either.
- One connection is one file. The file is created when the connection delivers its first frame and closed when the
  connection ends; a connection that delivers no frame creates no file. A game session is not a file: the game sends
  `Version` once after the connect, and a replay of a part of a connection without it would be skipped.
- Recording never changes the live run: frames, states and receive times are the same with and without it.

### File format (version 1)

- Name: `recording-<yyyyMMdd-HHmmss>Z.jsonl`, the receive time of the first frame in UTC, cut to the second. If the
  name is taken, `-2`, `-3`, ... is appended before `.jsonl`; no file is overwritten (up to 1,000 names per second,
  then the recording fails).
- Text in UTF-8 without a byte order mark, one JSON object per line, every line (the last included) ended by one
  line feed.
- Line 1 is the header `{"format":"censio-pipe-recording","version":1,"started":"<time>"}`; `started` is the
  receive time of the first frame.
- Every further line is one frame: `{"time":"<time>","frame":"<base64>"}`. `frame` is the base64 of the frame's type
  byte and payload, without the 4-byte length prefix.
- A time is ISO 8601 in UTC with seven fractional digits and `Z` (`2026-11-01T17:09:00.1234567Z`), so a replay
  reproduces every receive time to the 100 ns.
- A file is about a third larger than the frames it holds (base64). There is no size cap and no rotation.
- A change of the format raises `version`; the anonymiser (#20) writes version 1.

### Writing

- Frames wait in a queue of at most 16 MiB (16,777,216) and a writer drains it to the file; the pipe is read on
  without waiting for the disk. A queued frame is counted as the length of its base64 text plus 128 bytes. The limit
  adds at most 16 MiB of queued lines to the frame the reader holds.
- Each line is flushed to the operating system after it is written. A killed or crashed Companion therefore loses
  at most the line it was writing; a power loss can also lose the lines the operating system had not yet written to
  the disk (there is no sync per line).
- The recording stops for the rest of the run, with the lines already written complete, when the folder or file cannot
  be created, a write or a flush fails, or a frame does not fit into the queue; frames already queued are still
  written, the frames after the failure are not recorded. A connection that starts afterwards records nothing.
  The live connection and its states are not affected. The Companion reports this in its tooltip.
- When the Companion exits, the queue is written for at most 1 second; frames still queued then count as a failed
  recording, and a line the disk is still writing at that moment may be cut off, as after a crash.

### Replay

- A replay turns the file back into the pipe's byte stream and reads it through the same message reader (with its
  version gate, limits and skip rules) and tick assembler as a live connection; nothing is a copy of those rules.
- The assembler's clock is set to the recorded time of each frame, so the replayed ticks carry the recorded receive
  times at every speed. With the same frames, the ticks, session events and version messages equal those of the
  live run, in the same order. The one accepted difference: the stream ends at the last frame, so the last tick
  closes as *stream ended*, where the live run may have closed it by the quiet period.
- Speed: before a frame the replay waits the recorded pause to the previous frame divided by the speed. The speed is
  a factor from `0.1` to `1000` (default `1`, real speed) or `max` (no waiting). A time that goes backwards or
  repeats gets no wait and is stamped as recorded, without clamping. A long pause is waited in steps of at most one
  day.
- The file is opened read-only and never changed. A replay holds one line in memory at a time, whatever the file
  size, and works on any platform.

### Line rules

- A line ends with a line feed or a carriage return plus line feed. A missing line feed after the last line and one
  empty line at the end are accepted; an empty line anywhere else is an invalid line.
- A UTF-8 byte order mark before the header is accepted. The header needs `format` (the string
  `censio-pipe-recording`) and `version` (an integer); `started` is not checked.
- JSON fields may come in any order. Fields other than the ones above are ignored, in the header and in a frame line.
  A field given twice, or of the wrong JSON type, is refused.
- Limits, checked before the content is decoded: a line is at most 1,402,200 bytes; `frame` is at most 1,398,104
  characters and decodes to at most 1,048,576 bytes (1 MiB); JSON nested deeper than 64 levels is refused. The decoded frame then passes the reader's own limits.
  A time must be convertible to UTC and at least 30 seconds before the latest representable time.

### Replay problems

A problem ends the replay after the frames before it were replayed; the reason is part of the status.

| Reason | Cause |
| ------ | ----- |
| file not found | The file cannot be opened: it does not exist, access is refused, the path is refused by the platform |
| not a recording | The first line is not the header (also an empty file, a header line over the line limit, or a header field of the wrong JSON type) |
| unsupported version | The header's `version` is not `1` |
| invalid line `<n>` | Line `n` (the header is line 1) is not valid JSON of the frame form, has invalid base64, a frame over 1 MiB, a line over the limit, a missing or unusable `time`, a field given twice or of the wrong type, or is an empty line that is not the last |
| read failed | The file cannot be read on after it was opened |

A recording that holds only the header replays as finished at once. No exception message reaches the status.

### Privacy

A recording holds the savegame headline, island names and the economic data as the game sent them. It stays on the
machine, is never sent anywhere and is never deleted automatically. Only anonymised fixtures (#20, record 0029) may be
shared publicly.

### Recorder started in the middle of a connection

Recording from the start of a connection is what exists. A recorder that starts in the middle of a connection (a
tray toggle, #13) has to write the connection's last `Version` frame first, or the replay shows `Protocol mismatch`:
the version gate skips every frame of a file without it.

## Connection

The Companion opens the game's pipe itself, keeps the connection and reconnects. The logic (states, delays) is
platform-neutral and runs on an injected clock; only opening the pipe and listing the existing pipes are Windows
code (record 0035).

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
  while sending) shows `Error` until the next attempt's result replaces it. An unexpected exception of a read ends
  the connection as read failed. The stream is closed when the connection ends.
- Every change of the state or of its cause is raised as an event on whatever thread the connection runs on; a
  handler that throws affects neither the connection nor the other handlers. An unchanged state raises no event.
  Events carry the state, the error kind, the Windows error code or the received version, and no pipe content.

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

### Stop

- Stopping ends the connection within about 50 ms, also while an attempt, a delay or a read is pending, closes the
  pipe and raises no further event. The tray's thread never waits for it: `Exit` removes the icon and ends the
  process within 2 seconds in every state.

## Limits and errors

- A frame's length (type byte and payload) is at most 1 MiB (1,048,576), inclusive: exactly 1 MiB is read.
- A workforce or building map holds at most 1,024 pairs; more is a malformed message. The real maps hold a few
  dozen pairs.
- A tick holds at most 256 islands and at most 524,288 entries and map pairs together, counted after decoding (the
  pairs of a map after the sum of repeated GUIDs). An island frame that would exceed either limit is dropped and
  delivered as a *tick limit* item with its key only; the open tick is unchanged. A frame that alone exceeds the item
  limit opens no tick. The limits bound the memory of an open tick: at both limits, with every island full of
  entries and no pairs (the worst shape), it held 124.1 MiB (measured with frames decoded by the parser; distinct
  pairs: 10.9 MiB; 1,024-pair maps of one repeated GUID: 7.3 MiB).
- Every error is a typed result with its kind and, where they apply, the type value, length, offset and versions.
  An error carries no payload text, so a caller that logs it logs no pipe content.

| Error kind | Cause | Effect |
| ---------- | ----- | ------ |
| Empty frame | Length `0` | Frame skipped |
| Unknown type | Type byte other than 0 to 3 | Frame skipped |
| Malformed message | Payload too short for its type, string length beyond the frame, negative count, count the rest of the frame cannot hold, map over 1,024 pairs, duplicate-amount sum outside the int32 range | Frame skipped; no message with a `0` in place of a missing value |
| Protocol mismatch | Version other than `2`, or a non-version frame while the version is unknown or not `2` | Frame skipped |
| Truncated frame | Stream ends inside the length prefix or a payload | Stream ends |
| Oversized frame | Length over 1 MiB, or negative | Stream ends |
| Read failed | I/O error of the stream | Stream ends |

- "Skipped": the frame's bytes are dropped and the next read returns the next frame. "Stream ends": every further
  read returns the end of the stream.
- A cancelled read ends with the platform's cancellation instead of a result; part of a frame may already be
  consumed, and every later read returns the end of the stream.
- A count larger than the rest of the frame can hold (such as `2147483647` in 100 bytes) is checked against the
  bytes left before any collection is created.

### Deliberate differences from the reference

The reference reads leniently; this reader is strict and fails closed (record 0033).

- **Oversized length.** The reference reports the frame and reads on, out of step with the stream. Nothing in the
  stream marks where the next frame starts, so this reader ends the stream. The connection
  reconnects after an oversized-frame, truncated-frame or read-failed error (*Retry*).
- **Overflowing duplicate sum.** The reference sums duplicate amounts in an int32 and wraps silently. Here the whole
  frame is rejected as malformed.
- **Short field.** The reference reads `0` for a field past the end of the frame. Here the frame is rejected as
  malformed.
- **Wrong version.** The reference logs an unexpected version and decodes on. Here decoding stops until a
  `Version 2` arrives.
- **Map cap.** The reference has no cap. Integer keys hash to themselves, so colliding keys would cost seconds of CPU
  per frame; the cap of 1,024 pairs per map bounds it. A future game with larger maps would see its frames rejected
  until the cap is raised.

### Residual risks

Until fixtures from real recordings (#20) test the reader:

- The format is known only from the reference source as read on 2026-11-01.
- Strings are decoded as UTF-8 although the reference names no encoding; Windows-1252 would garble non-ASCII bytes.
  That the game writes `_` for umlauts rests on a third-party capture (`Dealwatch/Tabularium117`,
  `docs/protocol.md`), not on our own.
- Whether the game ever sends duplicate map keys is unknown.
- A game update that changes the format without a new protocol version is misread as long as every field still
  fits; one that bumps the version is refused with a protocol-mismatch error.
- The timing (frames about one second apart, ticks about two minutes), the shared timestamp, the zero-entry tick after
  a load, the double session end and the number of islands per tick (estimated well under 256, with fewer than
  524,288 entries and pairs) are third-party observations until the recordings of #17 confirm them.
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
    connection only when it next sends something. The Companion keeps retrying meanwhile, with the delays of *Retry* (at most 10 seconds apart).
  - `Exit` in `Pipe busy` ended the process in under 2 seconds.
- A program that created `\\.\pipe\anno117` before the game is read as the game, within the limits above, and
  cannot act with the player's Windows rights through the connection; it can still show `Receiving` with false data.
- A stall of more than 30 seconds in the stream splits a tick into two ticks with one game time.
- A game that sends one island twice in a tick splits it as *island repeated*.
- A replay that ends at its last frame closes the last tick as *stream ended*.
- A caller that pauses for more than 30 seconds between requests stamps frames late and may split ticks.
- A recorder that starts in the middle of a connection needs the connection's last `Version` frame first (#13).
- A recording ends at the last line the operating system had written: a stalled disk at `Exit` or a power loss can
  cost the last lines.
- Whether Windows sign-out or shutdown gives the Companion the same 1 second to write the queue as `Exit` is not
  verified.

## Related decisions

- [`decisions/0033-pipe-reader-strict-from-the-reference-source.md`](../decisions/0033-pipe-reader-strict-from-the-reference-source.md) — the reader is strict, fails closed and is built from the reference source with synthetic frames.
- [`decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md`](../decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md) — ticks close by five rules and a quiet period on an injected clock; a pure core with an async driver.
- [`decisions/0035-pipe-connection-states-retry-and-identification-level.md`](../decisions/0035-pipe-connection-states-retry-and-identification-level.md) — the connection checks that the pipe exists, connects read-only at identification level, and resets its retry delay only after 30 seconds in `Receiving`.
- [`decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md`](../decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md) — the Companion parses the pipe; the format is confined to `Censio.Pipe`.
- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — recordings hold the raw frames as JSON lines, written off the read path; replay feeds them back on a recorded clock.

## Not here

- Telling a resent or paused tick from a new one (#19, #35).
- Anonymising recordings (#20).
