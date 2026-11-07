# Pipe

## Scope

The statistics pipe as the Companion reads it: the frame layout, the message types of protocol version 2, the
1 MiB limit, the error kinds, which errors skip a frame and which end the stream, the version gate, and the
grouping of the messages into ticks and session events with the tick limits. The pipe is untrusted input: the format
is known from Ubisoft's example reader (`UbisoftMainzAnno/anno117_pipe_example`, read on 2026-11-01, "for
documentation purposes only") and one recording of ours; [`../protocol.md`](../protocol.md) states what the recording
confirms. A game update may change the format without notice. The reader works on any readable byte
stream and does not depend on Windows or on the named pipe (implemented in `Censio.Pipe`). Ticks are assembled on an
injected clock, so the same rules serve a live connection ([`pipe-connection.md`](pipe-connection.md)) and a replay
([`pipe-recording.md`](pipe-recording.md)).

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
| 1 | SessionStart | string (player figure's name) |
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

- A session start is delivered with the figure's name unchanged (not trimmed, `_` kept). It names the player figure, not the savegame: two savegames of one figure send the same text (record 0041).
- A session end is delivered as a session end when an island frame or a session start came since the last session
  end; otherwise, at the start of the stream or after another session end, it is delivered as a *repeated session
  end*. The game may send a session end right after the connect, before any session start (two of the four recordings in [`../protocol.md`](../protocol.md) began with one, the other two with a session start); that one is a
  repeated session end and carries no new information.
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
  starts only on a request. A slow consumer belongs behind the tick buffer (#41) or a hand-off that never blocks the
  reading ([`companion.md`](companion.md), *Diagnostics window*).

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
  stream marks where the next frame starts, so this reader ends the stream, and the connection reconnects
  ([`pipe-connection.md`](pipe-connection.md), *Retry*).
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

Until more fixtures from real recordings test the reader (one anonymised fixture exists, [`pipe-recording.md`](pipe-recording.md), *Fixtures*):

- The format is known from the reference source as read on 2026-11-01 and one recording of 2026-11-07
  ([`../protocol.md`](../protocol.md)); a single recording cannot show a single reader or the units of the rates.
- Strings are decoded as UTF-8 although the reference names no encoding; Windows-1252 would garble non-ASCII bytes.
  That the game writes `_` for umlauts rests on a third-party capture (`Dealwatch/Tabularium117`'s
  `docs/protocol.md`, a file of that repository, not ours), not on our own; our recording has no non-ASCII byte.
- Whether the game ever sends duplicate map keys is unknown; none was seen in one recording.
- A game update that changes the format without a new protocol version is misread as long as every field still
  fits; one that bumps the version is refused with a protocol-mismatch error.
- One recording confirms the timing (frames about one second apart, ticks about two minutes), the shared timestamp,
  the zero-entry tick after a load and the number of islands per tick (at most 24, with at most 1,934 entries and
  pairs); the double session end was not seen ([`../protocol.md`](../protocol.md)). Other games and updates may differ.
- The SessionStart text is the player figure's name and no savegame key: two savegames of one figure send byte-identical text, so it cannot tell them apart (record 0041).
- A stall of more than 30 seconds in the stream splits a tick into two ticks with one game time.
- A game that sends one island twice in a tick splits it as *island repeated*.
- A caller that pauses for more than 30 seconds between requests stamps frames late and may split ticks.

## Related decisions

- [`decisions/0033-pipe-reader-strict-from-the-reference-source.md`](../decisions/0033-pipe-reader-strict-from-the-reference-source.md) — the reader is strict, fails closed and is built from the reference source with synthetic frames.
- [`decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md`](../decisions/0034-ticks-assembled-by-close-rules-on-an-injected-clock.md) — ticks close by five rules and a quiet period on an injected clock; a pure core with an async driver.
- [`decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md`](../decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) — the profile is selected in the Companion; the figure's name is only a hint.
- [`decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md`](../decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md) — the Companion parses the pipe; the format is confined to `Censio.Pipe`.

## Not here

- Finding, opening and keeping the connection, its states and the retry: [`pipe-connection.md`](pipe-connection.md).
- Recording the frames to a file and replaying it: [`pipe-recording.md`](pipe-recording.md).
- Telling a resent or paused tick from a new one (#35).
- Assigning ticks to a profile: record 0041.
- Anonymising recordings: [`pipe-recording.md`](pipe-recording.md), *Anonymising*.
