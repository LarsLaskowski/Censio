# Spec: Frame reader and message parser for pipe protocol version 2

Source: Issue #9
Status: Approved by Lead

## Problem

The tick assembler (#10), the pipe connection (#11) and the recorder (#12) need the statistics pipe as typed
messages; `Censio.Pipe` holds only `PipeAssembly` today. The pipe is untrusted input (`.squad/project.md`,
*Parsing of external input*) and a game update may change it without notice. Ubisoft's example reader
(`UbisoftMainzAnno/anno117_pipe_example`, `src/pipe.h`, `src/pipe.cpp`, read on 2026-11-01; "for documentation
purposes only") is lenient: a short field reads as `0`, an oversized frame desynchronises the stream, an unexpected
version is ignored. A Companion built that way would show wrong numbers instead of an error.

"The reader" is the frame reader and message parser as their caller sees them: the caller hands it one readable
byte stream (one connection) and asks for one result at a time — a message, an error, or the end of the stream.

Wire format (`pipe.h`, `pipe.cpp`; the issue's description is confirmed): a signed 32-bit little-endian length, then
that many bytes, the type byte first (the length does not count its own 4 bytes). Types `0` `Version` (int32),
`1` `SessionStart` (string), `2` `SessionEnd` (empty), `3` `AreaProductionStatistics`: session ID, island ID, area
index (one unsigned byte each, 0–255), session GUID (int32), area name (string), timestamp (int64), entry count
(int32), then per entry product GUID (int32), generation, consumption, delta, perfect generation, perfect consumption
(float32), building count, maintenance (int32), income (float32), profit (int32), summed and average productivity
(float32), then a workforce map and a building map, each an int32 pair count and that many (int32 GUID, int32
amount) pairs. A string is one unsigned length byte (0–255) and that many bytes. All numbers little-endian, floats
IEEE 754 binary32; an entry takes at least 56 bytes. A map is a set of GUID→amount pairs: its order carries no
meaning (the reference keeps it in an `unordered_map`).

## Scenarios

- S1: Given a new reader on a stream starting with `Version 2`, when the caller reads, then it gets a version
  message `2`, and the frames that follow are decoded.
- S2: Given a `SessionStart` frame with a 200-byte headline after `Version 2`, when the caller reads, then it gets a
  session-start message with that headline.
- S3: Given two `SessionEnd` frames after `Version 2`, when the caller reads twice, then it gets two session-end
  messages.
- S4: Given an `AreaProductionStatistics` frame with session ID `200`, island ID `255`, area index `128` and two
  entries after `Version 2`, when the caller reads, then it gets one statistics message with every header value and
  both entries in frame order, each with its twelve values and both maps; entry count `0` gives no entries.
- S5: Given a stream that delivers frames one byte per read, with the prefix split from the payload, or all in one
  read, when the caller reads, then it gets the same results as from one frame per read.
- S6: Given a map holding the same GUID twice (amounts `3` and `4`), when the caller reads, then the message holds
  that GUID once with `7`, as the reference does.
- S7: Given a stream that ends right after a complete frame, when the caller reads past it, then it gets the end of
  the stream.

## Errors and edge cases

Every error is a typed result the caller tells apart by kind; the reader never throws for any byte sequence.
"Skipped": the frame's bytes are dropped and the next read returns the next frame.

- E1: The stream ends inside the 4-byte length prefix: a truncated-frame error, then the end of the stream.
- E2: The stream ends inside a payload: a truncated-frame error, then the end of the stream.
- E3: The length is over 1 MiB (1,048,576): an oversized-frame error with the length; nothing more is read from
  the stream, and every further read returns the end of the stream. Exactly 1 MiB is accepted.
- E4: A negative length: the oversized-frame error of E3.
- E5: Length `0` (no type byte): an empty-frame error; skipped.
- E6: A type byte other than `0`–`3`: an unknown-type error with the type value; skipped.
- E7: A payload too short for its type (a `Version` of under 4 bytes, a statistics frame ending inside a field):
  a malformed-message error with the type; skipped; no message with a `0` in place of a missing value.
- E8: A string length beyond the end of the frame: the error of E7.
- E9: A negative entry count or pair count: the error of E7.
- E10: A count larger than the rest of the frame can hold (`2147483647` in 100 bytes): the error of E7, without the
  reader taking memory for that count.
- E11: A `Version` other than `2` (`3`): a protocol-mismatch error with received and expected version; every
  following frame of another type is skipped undecoded and returns that error again, until a `Version 2` arrives.
- E12: Duplicate amounts (S6) whose sum leaves the int32 range: the error of E7 for the whole frame (the reference
  wraps silently).
- E13: A read after the end of the stream or after E1–E3, E17, E18: the end of the stream, every time.
- E14: A frame other than `Version` before any `Version` frame of this reader: a protocol-mismatch error without a
  received version; skipped.
- E15: Bytes after the last field of a payload: the message is delivered, the extra bytes ignored.
- E16: A string that is not valid UTF-8 (`0xFF`), ends in a space (`Zycada `) or holds `_` for an umlaut
  (`R_mische K_ste`): delivered as UTF-8 with each invalid sequence replaced by U+FFFD, untrimmed, `_` kept.
- E17: An I/O error of the stream: a read-failed error, then the end of the stream.
- E18: The caller cancels a read: that read ends with the platform's cancellation, not with a result, and every
  later read returns the end of the stream (part of a frame may already be consumed).

## Non-functional requirements

- Limits: a frame's length (type byte and payload) is at most 1 MiB; a read holds at most one frame plus its
  decoded message in memory, whatever the counts in the payload say (E10).
- Security: an error carries only its kind, type value, length, offset and versions, never payload text, so a
  caller that logs it logs no pipe content.
- Platform: any readable byte stream, no dependency on Windows or the named pipe; tested on Linux. Opening
  `\\.\pipe\anno117` and reconnecting are #11's.
- Compatibility: protocol version 2 as in the reference read on 2026-11-01, built from that source with synthetic
  frames (Q5). Floats and the timestamp are delivered as read, NaN included.
- Concurrency: one reader serves one stream and one caller; concurrent reads are not supported.
- Configuration and logs: none new; the reader logs nothing.
- Coverage: at least 80 % line coverage on the new code (*Coverage gate*).

## Acceptance criteria

- [ ] AC1: Each message type read from a synthetic frame after `Version 2` gives its typed message with every field
  equal to the value written — one-byte IDs of `128` and above and a headline of 200 bytes read as unsigned, maps
  compared as GUID→amount sets — two `SessionEnd` frames give two messages, and entry count `0` gives no entries
  (S1, S2, S3, S4).
- [ ] AC2: Frames delivered one byte per read, split between prefix and payload, or all in one read give the same
  results as one frame per read (S5).
- [ ] AC3: A GUID twice in a workforce or building map appears once with the sum of its amounts; a sum outside the
  int32 range gives a malformed-message error (S6, E12).
- [ ] AC4: A stream that ends at a frame boundary gives the end of the stream after its last message, and every
  further read gives the end again, also after a truncated-frame or oversized-frame error (S7, E13).
- [ ] AC5: A stream that ends inside the length prefix or inside a payload gives a truncated-frame error, then the
  end of the stream (E1, E2).
- [ ] AC6: A length of 1,048,577 or a negative length gives an oversized-frame error with that length and nothing
  more is read from the stream; a frame of exactly 1,048,576 bytes is read (E3, E4).
- [ ] AC7: A frame of length `0` gives an empty-frame error, a type byte `4` or `255` an unknown-type error with
  that value, and in both cases the next frame is read normally (E5, E6).
- [ ] AC8: A payload ending inside a field, a string length beyond the frame, a negative count or a count larger
  than the rest of the frame gives a malformed-message error with the type and no message, and the next frame is
  read normally (E7, E8, E9, E10).
- [ ] AC9: A count of `2147483647` in a frame of under 1 KiB gives the malformed-message error without the parse
  taking memory for that count (E10).
- [ ] AC10: After `Version 3` the version frame gives a protocol-mismatch error with received `3` and expected `2`,
  each following non-version frame that error again without being decoded, and a later `Version 2` makes frames
  decode again; on a new reader a frame before any `Version` gives a protocol-mismatch error without a received
  version (E11, E14).
- [ ] AC11: A payload with extra bytes after its last field gives its message, and a string is delivered as UTF-8
  with invalid sequences replaced by U+FFFD, untrimmed and with `_` kept (E15, E16).
- [ ] AC12: An I/O error of the stream gives a read-failed error, then the end of the stream; a cancelled read ends
  with cancellation and returns no result, and the next read gives the end of the stream (E17, E18).
- [ ] AC13: A valid sequence of all four message types cut at every byte offset, and with each single byte replaced
  by `0x00` and by `0xFF` in turn, read to the end never throws and ends with the end of the stream (E1–E10).
- [ ] AC14: No error carries payload text: the error for a malformed `SessionStart` with the headline `secret`
  contains `secret` in none of its values and not in its string form (E8).

## Out of scope

- Opening the pipe, waiting for the game, reconnecting, connection states (#11).
- Ticks, island identity, zero-entry ticks, duplicate `SessionEnd` semantics (#10).
- Recording and replay (#12); the anonymiser and fixtures from real recordings (#20).
- The tray text for a protocol mismatch (#13); the JSON contract to the server (#27).
- Judging values (NaN, negative amounts, unknown GUIDs): the consumers'.
- README and user guide: the issue defers them to the tray app; nothing a player sees changes.

## Open questions

- Q1: Does the length count the type byte? Answered: yes — the reference reads `length` bytes and takes the type
  from the first (`pipe.cpp`, message loop).
- Q2: Is 1 MiB inclusive? Answered: yes — the reference rejects only `size > 1024 * 1024`.
- Q3: What does "stops statistics parsing" mean for other types and before the first `Version`? Answered: (Lead) no
  frame other than `Version` is decoded while the version is not `2` or not yet known (E11, E14), fail closed; the
  reference sends `Version` first on every connection (`pipe.cpp`, "Read preamble"), and a reader serves one.
- Q4: After an oversized length, skip or stop? Answered: (Lead) stop (E3): the stream has no frame marker to
  resynchronise on; #11 reconnects.
- Q5: Is there a real sample to build and test from? Answered: (Product Manager, step 2) no — built from the
  reference source, tested with synthetic frames only; the residual risk is in the decision record, tests on
  anonymised real recordings come with #20.

## Area documents

- `docs/areas/pipe.md` (new area): frame layout, message types and fields, the 1 MiB limit, the error kinds and
  when the reader skips or stops, the version gate, duplicate map keys, string decoding, extra bytes — and where
  it deliberately differs from the reference (E3, E11, E12); not here: #10, #11, #12.
- `docs/areas/companion.md`, *Not here*: points to the pipe area.
- `docs/ARCHITECTURE.md`: the `Censio.Pipe` row and *Data flow* link the pipe area.

## Challenge

Spec challenge (Devil's Advocate): 0 major, 7 minor, all accepted.

1. Signedness of the one-byte fields and the string length. Accepted: the wire format says unsigned 0–255; S2, S4
   and AC1 use IDs of `128` and above and a 200-byte headline.
2. Map order. Accepted: maps are GUID→amount sets (*Problem*); AC1 compares them as sets.
3. State after a cancelled read. Accepted: E18 and AC12 — every later read returns the end of the stream.
4. The version gate is per stream. Accepted: a reader serves one stream and starts with the version unknown
   (*Problem*, S1, E14, AC10, Q3).
5. E12 differs from the reference. Accepted: E12 says so; `docs/areas/pipe.md` records it as deliberate (plan,
   *Documentation updates*).
6. The string encoding is an assumption. Accepted: UTF-8 and the `_` for umlauts are named residual risks in the
   decision record, to be checked against the #20 fixtures.
7. Stopping after an oversized length differs from the reference. Accepted: `docs/areas/pipe.md` says so, and that
   the caller (#11) reconnects after an oversized-frame error.
