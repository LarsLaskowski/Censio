# Spec: Frame reader and message parser for pipe protocol version 2

Source: Issue #9
Status: Draft

## Problem

The Companion components that come next — the tick assembler (#10), the pipe connection (#11) and the recorder
(#12) — need the bytes of the statistics pipe as typed messages. `Censio.Pipe` holds only `PipeAssembly` today
(`src/Censio.Pipe/PipeAssembly.cs`), so nothing reads the pipe yet. The pipe is untrusted input
(`.squad/project.md`, *Security areas*, *Parsing of external input*), a game update may change its format without
notice, and Ubisoft's example reader (`UbisoftMainzAnno/anno117_pipe_example`, `src/pipe.h` and `src/pipe.cpp`,
read at its default branch on 2026-11-01; its README calls it "for documentation purposes only", not a supported
reference) reads leniently: a short field reads as `0`, a frame over the limit desynchronises the stream, and an
unexpected version is logged and then ignored. A Companion built that way would show wrong numbers instead of an
error.

"The reader" below is the frame reader and message parser together, as seen by its caller: the caller hands it a
readable byte stream and asks it for the next result, one at a time. A result is a message, an error, or the end of
the stream.

Wire format, from the reference source (the issue's description is confirmed; the reference adds the field types):
a frame is a signed 32-bit little-endian length, then that many bytes; the length counts the type byte and the
payload, not the prefix itself. The type byte is `0` `Version`, `1` `SessionStart`, `2` `SessionEnd`, `3`
`AreaProductionStatistics`. Payloads: `Version` — int32 version; `SessionStart` — string (one length byte 0–255,
then that many bytes); `SessionEnd` — empty; `AreaProductionStatistics` — session ID, island ID and area index (one
byte each), session GUID (int32), area name (string), timestamp (int64), entry count (int32), then per entry: product
GUID (int32), generation, consumption, delta, perfect generation, perfect consumption (float32 each), building count,
maintenance (int32 each), income (float32), profit (int32), summed productivity, average productivity (float32
each), then the workforce map and the building map, each an int32 pair count followed by that many (int32 GUID,
int32 amount) pairs. All numbers are little-endian; floats are IEEE 754 binary32. The fixed part of an entry is 48
bytes, so an entry takes at least 56 bytes.

## Scenarios

- S1: Given a stream holding a `Version` frame with version `2`, when the caller reads, then it gets a version
  message with version `2`, and statistics frames that follow are parsed.
- S2: Given a `SessionStart` frame with the headline `Captain` after `Version 2`, when the caller reads, then it gets
  a session-start message with the headline `Captain`.
- S3: Given a `SessionEnd` frame after `Version 2`, when the caller reads, then it gets a session-end message; two
  `SessionEnd` frames in a row give two session-end messages.
- S4: Given an `AreaProductionStatistics` frame with two entries after `Version 2`, when the caller reads, then it
  gets one statistics message with the session ID, island ID, area index, session GUID, area name, timestamp and
  both entries in frame order, each with all twelve values and both maps; a frame with entry count `0` gives a
  message with no entries.
- S5: Given a stream that delivers a frame in pieces (one byte per read, or the length prefix split from the payload)
  or several frames in one read, when the caller reads, then it gets the same results as from one frame per read.
- S6: Given an entry whose workforce or building map holds the same GUID twice (amounts `3` and `4`), when the
  caller reads, then the message holds that GUID once with the amount `7`, as the reference does.
- S7: Given a stream that ends right after a complete frame, when the caller reads past it, then it gets the end of
  the stream.

## Errors and edge cases

Every error is a typed result the caller can tell apart by kind; the reader never throws for any byte sequence.
"Skipped" means the reader drops the frame's bytes and the next read returns the next frame.

- E1: The stream ends inside the 4-byte length prefix (1–3 bytes left): a truncated-frame error, then the end of the
  stream.
- E2: The stream ends inside a payload (fewer bytes than the length announced): a truncated-frame error, then the
  end of the stream.
- E3: The length is over 1 MiB (1,048,576 bytes): an oversized-frame error carrying the length; the reader reads
  nothing more from the stream, and every further read returns the end of the stream. A length of exactly 1 MiB is
  accepted.
- E4: The length is negative (the sign bit is set): the same oversized-frame error as E3.
- E5: The length is `0` (no type byte): an empty-frame error; the frame is skipped.
- E6: The type byte is not `0`–`3`: an unknown-type error carrying the type value; the frame is skipped.
- E7: A payload too short for its type — a `Version` with fewer than 4 bytes, a statistics frame that ends inside a
  field or an entry: a malformed-message error carrying the message type; the frame is skipped, and no message with
  a value filled in as `0` is delivered.
- E8: A string whose length byte announces more bytes than the frame has left: the malformed-message error of E7.
- E9: A negative entry count or map pair count: the malformed-message error of E7.
- E10: An entry count or pair count larger than the rest of the frame can hold (for example `2147483647` in a
  100-byte frame): the malformed-message error of E7, returned without the reader taking memory for that count.
- E11: A `Version` frame with a version other than `2` (for example `3`): a protocol-mismatch error carrying the
  received and the expected version. Every following frame of another type is skipped undecoded, and each read
  returns that error again, until a `Version 2` frame arrives.
- E12: The duplicate amounts of S6 sum to a value outside the int32 range: the malformed-message error of E7.
- E13: A read after the end of the stream, or after E1–E3: the end of the stream again, every time.
- E14: A `SessionStart`, `SessionEnd` or statistics frame before any `Version` frame: a protocol-mismatch error
  without a received version (version not yet known); the frame is skipped.
- E15: A payload with bytes left after the last field of its type: the message is delivered; the extra bytes are
  ignored.
- E16: A string that is not valid UTF-8 (byte `0xFF`), that ends in a space (`Zycada `), or that holds `_` where the
  player typed an umlaut (`R_mische K_ste`): the string is delivered as UTF-8 with each invalid sequence replaced by
  U+FFFD, not trimmed, `_` kept.
- E17: The stream fails with an I/O error while reading: a read-failed error, then the end of the stream.
- E18: The caller cancels a pending read: the read ends with the platform's cancellation, not with an error result;
  the stream is not read further by that call.

## Non-functional requirements

- Limits: a frame's length (type byte and payload, the prefix not counted) is at most 1 MiB; the reader holds at
  most one frame in memory, so the memory a read takes is bounded by the 1 MiB limit plus the decoded message,
  whatever the counts in the payload say (E10).
- Security: the pipe is untrusted input (`.squad/project.md`, *Parsing of external input*). An error result carries
  only its kind, the message type, the length, the version or the byte offset, never payload text, so a caller that
  logs it logs no pipe content.
- Platform: the reader works on any readable byte stream and has no dependency on Windows or on the named pipe, so
  it is tested on Linux; opening `\\.\pipe\anno117` and reconnecting are #11's.
- Compatibility: protocol version 2 only, as of the reference read on 2026-11-01. Floats and the timestamp are
  delivered as read, NaN and infinities included; judging values belongs to the consumers.
- Concurrency: one caller reads one stream at a time; concurrent reads on one reader are not supported.
- Configuration and logs: no new configuration; the reader logs nothing.
- Coverage: at least 80 % line coverage on the new code (issue #9; the *Coverage gate* of `.squad/stack.md`).

## Acceptance criteria

- [ ] AC1: Each of the four message types, read from a synthetic frame after `Version 2`, gives its typed message
  with every field equal to the value written, two `SessionEnd` frames give two messages, and a statistics frame
  with entry count `0` gives a message with no entries (S1, S2, S3, S4).
- [ ] AC2: A sequence of frames delivered one byte per read, split between prefix and payload, or all in one read
  gives the same results as one frame per read (S5).
- [ ] AC3: A GUID that appears twice in a workforce or building map appears once with the sum of its amounts; a sum
  outside the int32 range gives a malformed-message error (S6, E12).
- [ ] AC4: A stream that ends at a frame boundary gives the end of the stream after its last message, and every
  further read gives the end again, also after a truncated-frame or oversized-frame error (S7, E13).
- [ ] AC5: A stream that ends inside the length prefix or inside a payload gives a truncated-frame error, then the
  end of the stream (E1, E2).
- [ ] AC6: A length of 1,048,577 or a negative length gives an oversized-frame error with that length and nothing
  more is read from the stream; a frame of exactly 1,048,576 bytes is read (E3, E4).
- [ ] AC7: A frame of length `0` gives an empty-frame error, a frame with type byte `4` or `255` gives an
  unknown-type error with that value, and in both cases the next frame is read normally (E5, E6).
- [ ] AC8: A payload that ends inside a field, a string length beyond the frame, a negative count, or a count
  larger than the rest of the frame can hold gives a malformed-message error with the message type, no message
  carries a `0` in place of a missing value, and the next frame is read normally (E7, E8, E9, E10).
- [ ] AC9: A count of `2147483647` in a frame of under 1 KiB gives the malformed-message error without the read
  taking memory for that count (E10).
- [ ] AC10: After `Version 3`, the version frame gives a protocol-mismatch error with received `3` and expected `2`,
  each following non-version frame gives that error again without being decoded, and a later `Version 2` frame
  makes statistics frames parse again; a frame before any `Version` frame gives a protocol-mismatch error without
  a received version (E11, E14).
- [ ] AC11: A payload with extra bytes after its last field gives its message, and a string is delivered as UTF-8
  with invalid sequences replaced by U+FFFD, untrimmed and with `_` kept (E15, E16).
- [ ] AC12: An I/O error of the stream gives a read-failed error, then the end of the stream; a cancelled read ends
  with cancellation and returns no result (E17, E18).
- [ ] AC13: For a valid sequence of all four message types cut at every byte offset, and with every single byte of
  it replaced by `0x00` and by `0xFF` in turn, reading to the end never throws and ends with the end of the stream
  (E1–E10).
- [ ] AC14: No error result carries text from the payload: the error for a malformed `SessionStart` with the
  headline `secret` contains no `secret` in any of its values or its string form (E8).

## Out of scope

- Opening the named pipe, waiting for the game, reconnecting and the connection states (#11).
- Grouping frames into ticks, island identity, the meaning of zero-entry ticks and duplicate `SessionEnd` (#10).
- Recording and replay (#12); the anonymiser and fixtures from real recordings (#20).
- The text the tray app shows for a protocol mismatch (#13) and the JSON contract to the server (#27).
- Judging the values themselves (NaN, negative amounts, unknown GUIDs): the consumers'.
- README and user guide: the issue defers user documentation to the tray app; the reader changes nothing a player
  sees.

## Open questions

- Q1: Does the length count the type byte? Answered: yes — the reference reads `length` bytes and takes the type
  from the first of them (`pipe.cpp`, the message loop).
- Q2: Is 1 MiB inclusive? Answered: yes — the reference rejects only `size > 1024 * 1024`.
- Q3: What does "stops statistics parsing" mean for the other types and before the first `Version`? Answered: (Lead)
  no frame other than `Version` is decoded while the version is not `2` or not yet known (E11, E14), which
  fails closed; the reference sends `Version` first on every connection (`pipe.cpp`, "Read preamble").
- Q4: After an oversized length, skip the frame or stop? Answered: (Lead) stop (E3). The stream has no
  frame marker to resynchronise on, and skipping means reading up to 2 GiB that the reader cannot check; #11
  reconnects.
- Q5: Is there a real sample of the format to build and test from (`.squad/routing.md`, *Escalation to the Product
  Manager*)? Answered: (Product Manager, step 2) no — the reader is built from the reference source and tested with
  synthetic frames only, no real recording. None is in the repository or on the issue, and Censio cannot record
  one before #12, which depends on this feature. The residual risk (the format is known only from the reference
  source) goes into a decision record; tests on anonymised real recordings come with #20.

## Area documents

- `docs/areas/pipe.md`, a new area (the plan decides it with its index row in `docs/areas/README.md`): the frame
  layout, the four message types and their fields, the 1 MiB limit, the error kinds and when the reader stops or
  skips, the version gate (E11, E14), duplicate map keys, string decoding, extra bytes; what is not here (#10, #11,
  #12).
- `docs/areas/companion.md`, *Not here*: points to the pipe area for reading the statistics pipe.
- `docs/ARCHITECTURE.md`: the `Censio.Pipe` row and *Data flow* say that the pipe layer frames and parses the
  stream into typed messages, and link the pipe area.
