# Pipe recording and replay

## Scope

How the Companion keeps the frames of a pipe connection in a file and feeds such a file back through the same message
reader and tick assembler as a live pipe, so the ticks of a run can be reproduced (record 0036, issue #12): what is
recorded, the file format, how it is written, how a replay paces and stamps the frames, which files it refuses, and
the privacy of a recording. Testers and bug reporters rely on it, and the anonymiser (#20) writes its format. A
recording file is untrusted input. The Companion's switches, menu entries, folder and tooltips are in
[`companion.md`](companion.md).

## Behavior

### What is recorded

- Every frame the frame reader read whole is recorded, in order and exactly once, with the receive time the pipeline
  gave it (one clock reading per frame, shared by the recording and the ticks). That includes the frames the reader
  skips (empty, unknown type, malformed, protocol mismatch): they are recorded and replayed as skipped.
- A frame that is only partly received (the stream ended inside it, or its length is over 1 MiB or negative) is not
  recorded. The error that ended the stream is not recorded either.
- One connection is one file. The file is created when the connection delivers its first frame and closed when the
  connection ends; a connection that delivers no frame creates no file. A game session is not a file: the game sends
  `Version` once after the connect, and a replay of a part of a connection without it would be skipped.
- The one exception to "recorded exactly as read" is the `Version` line a recording started in the middle of a
  connection begins with (*Recorder started in the middle of a connection*).
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
- The recording stops, with the lines already written complete, when the folder or file cannot
  be created, a write or a flush fails, or a frame does not fit into the queue; frames already queued are still
  written, the frames after the failure are not recorded, and a connection that starts while it is failed records
  nothing. A new start of the recording resets the failure and writes a new file. A recording that was
  stopped by the user and fails while it writes its remaining frames is only logged.
  The live connection and its states are not affected.
- Stopped recordings that are still writing are not capped in total: each holds a queue of up to 16 MiB while its disk
  stalls, and only a user's new start adds one (accepted).
- When the Companion exits, the queues of all recordings are written together for at most 1 second; frames still
  queued then count as a failed recording, and a line the disk is still writing at that moment may be cut off, as
  after a crash.

### Recorder started in the middle of a connection

A recorder that starts in the middle of a connection (the tray menu, #13) first writes the connection's last `Version`
frame, the frame whose type byte is `0` (also when its version is not 2), or the replay would show `Protocol
mismatch`: the version gate skips every frame of a file without it.

- The `Version` line is stamped with the receive time of the first frame the recorder records, so the file name and
  header carry that time and a replay waits for nothing before it. This is the one line that does not carry its own
  receive time; version messages close no tick and start no quiet period, so the replayed ticks are unchanged.
- A start before any `Version` arrived writes nothing extra. A start in `Protocol mismatch` writes the last `Version`
  frame, and the replay shows `Protocol mismatch`. If the first recorded frame is itself a `Version`, nothing is added.
- The recording lacks the islands of a tick sent before the start and a session start sent before it, so its first
  replayed tick is partial; the ticks opened after its first frame equal the live run's.

### Replay

- A replay turns the file back into the pipe's byte stream and reads it through the same message reader (with its
  version gate, limits and skip rules) and tick assembler as a live connection ([`pipe.md`](pipe.md)); nothing is a
  copy of those rules.
- The assembler's clock is set to the recorded time of each frame, so the replayed ticks carry the recorded receive
  times at every speed. With the same frames, the ticks, session events and version messages equal those of the
  live run, in the same order. The one accepted difference: the stream ends at the last frame, so the last tick
  closes as *stream ended*, where the live run may have closed it by the quiet period.
- The replay keeps the latest closed tick whole, as the live connection does
  ([`pipe-connection.md`](pipe-connection.md), *Latest tick*).
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
  characters and decodes to at most 1,048,576 bytes (1 MiB); JSON nested deeper than 64 levels is refused. The
  decoded frame then passes the reader's own limits. A time must be convertible to UTC and at least 30 seconds before
  the latest representable time.

### Privacy

A recording holds the savegame headline, island names and the economic data as the game sent them. It stays on the
machine, is never sent anywhere and is never deleted automatically. Only anonymised fixtures (#20, record 0029) may be
shared publicly.

## Limits and errors

A problem ends the replay after the frames before it were replayed; the reason is part of the status.

| Reason | Cause |
| ------ | ----- |
| file not found | The file cannot be opened: it does not exist, access is refused, the path is refused by the platform |
| not a recording | The first line is not the header (also an empty file, a header line over the line limit, or a header field of the wrong JSON type) |
| unsupported version | The header's `version` is not `1` |
| invalid line `<n>` | Line `n` (the header is line 1) is not valid JSON of the frame form, has invalid base64, a frame over 1 MiB, a line over the limit, a missing or unusable `time`, a field given twice or of the wrong type, or is an empty line that is not the last |
| read failed | The file cannot be read on after it was opened |

- A recording that holds only the header replays as finished at once. No exception message reaches the status.
- A replay that ends at its last frame closes the last tick as *stream ended*.
- A recording ends at the last line the operating system had written: a stalled disk at `Exit` or a power loss can
  cost the last lines.
- Whether Windows sign-out or shutdown gives the Companion the same 1 second to write the queue as `Exit` is not
  verified.

## Related decisions

- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — recordings hold the raw frames as JSON lines, written off the read path; replay feeds them back on a recorded clock.
- [`decisions/0029-anonymised-test-fixtures-only.md`](../decisions/0029-anonymised-test-fixtures-only.md) — only anonymised fixtures from recordings are shared.

## Not here

- The frame layout, the reader's limits and the tick rules a replay goes through: [`pipe.md`](pipe.md).
- The connection whose frames are recorded: [`pipe-connection.md`](pipe-connection.md).
- `--record`, `--replay`, `--speed`, the menu entries, the recordings folder and the tooltips: [`companion.md`](companion.md).
- Anonymising recordings (#20).
