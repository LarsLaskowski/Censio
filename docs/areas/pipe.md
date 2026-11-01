# Pipe

## Scope

The statistics pipe as the Companion reads it: the frame layout, the message types of protocol version 2, the
1 MiB limit, the error kinds, which errors skip a frame and which end the stream, and the version gate. The pipe is
untrusted input: the format is known only from Ubisoft's example reader (`UbisoftMainzAnno/anno117_pipe_example`,
read on 2026-11-01, "for documentation purposes only"), and a game update may change it without notice. The
implementation is in `Censio.Pipe`; it works on any readable byte stream and does not depend on Windows or on the
named pipe.

## Behavior

### Reading

- A reader serves one stream and one caller. The caller asks for one result at a time: a message, an error, or the
  end of the stream. The reader never throws for any byte sequence and logs nothing.
- Frames may arrive in any chunking (one byte per read, split between prefix and payload, or all at once); the
  results are the same.
- A read holds at most one frame plus its decoded message in memory, whatever the counts in the payload say.

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

## Limits and errors

- A frame's length (type byte and payload) is at most 1 MiB (1,048,576), inclusive: exactly 1 MiB is read.
- A workforce or building map holds at most 1,024 pairs; more is a malformed message. The real maps hold a few
  dozen pairs.
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
  stream marks where the next frame starts, so this reader ends the stream. The caller that opens the pipe (#11)
  must reconnect after an oversized-frame, truncated-frame or read-failed error.
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

## Related decisions

- [`decisions/0033-pipe-reader-strict-from-the-reference-source.md`](../decisions/0033-pipe-reader-strict-from-the-reference-source.md) — the reader is strict, fails closed and is built from the reference source with synthetic frames.
- [`decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md`](../decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md) — the Companion parses the pipe; the format is confined to `Censio.Pipe`.

## Not here

- Opening the pipe, waiting for the game, reconnecting and connection states (#11).
- Assembling ticks from the messages (#10).
- Recording the stream (#12).
