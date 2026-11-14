# Pipe recording and replay

## Scope

How the Companion keeps the frames of a pipe connection in a file and feeds such a file back through the same message
reader and tick assembler as a live pipe, so the ticks of a run can be reproduced (record 0036, issue #12): what is
recorded, the file format, how it is written, how a replay paces and stamps the frames, which files it refuses, and
the privacy of a recording and how a contributor anonymises one (record 0042, issue #20). Testers and bug reporters
rely on it, and the anonymiser reads and writes its format. A recording file is untrusted input. The Companion's switches, menu entries, folder and tooltips are in
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
- A change of the format raises `version`; the anonymiser writes version 1.

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
- Speed: before a frame the replay waits the recorded pause to the previous frame divided by the speed exactly as
  written (`0.4` divides by 0.4, not by the nearest binary fraction). The speed is
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

A recording holds the name of the player figure, island names and the economic data as the game sent them. It stays on the
machine, is never deleted automatically and is sent only when the user replays it with `--server`, as contract messages (ticks, session events and states, without the figure name) to the server named there ([`replay-sending.md`](replay-sending.md)); nothing else sends it. Only anonymised fixtures (record 0029,
*Anonymising* below) may be shared publicly.

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

## Anonymising

A contributor tool turns a recording into an anonymised copy of the same format (record 0042, issue #20). It is a
console program of the repository, run with the .NET SDK, and is not part of any release. Raw recordings of testers
still go to a maintainer privately; the tool is for the fixtures that are committed (record 0029).

### Command

- `Censio.Anonymiser <recording> <output>`: exactly two arguments, or the tool prints
  `Usage: Censio.Anonymiser <recording> <output>` on standard error and exits with code 2.
- Success: one line `Anonymised <n> frames to <output>` on standard output (`<output>` as typed), exit code 0. Nothing
  else is printed, in particular no offset and no factor.
- Failure: one line `Error: <reason>` on standard error, exit code 1, nothing on standard output, no file under the
  output name and no temporary file left. No exception text is printed.
- Ctrl+C cancels the run: `Error: cancelled`, code 1.

| Reason | Cause |
| ------ | ----- |
| file not found | The recording cannot be opened (it does not exist, access is refused, the path is refused by the platform, another program such as a running Companion holds it for writing) |
| not a recording, unsupported version, invalid line `<n>` | As for a replay (*Limits and errors*); also a receive time that the shift would move before the earliest representable time |
| read failed | The recording cannot be read on, cannot seek (a pipe or device), or differs between the two passes |
| frame cannot be anonymised in line `<n>` | Line `n` holds a frame the tool does not decode (*What is refused*) |
| output file exists | The output exists as a file or a folder, or appears before the finished file is moved to it |
| output is the input | The full paths are equal (ignoring case on Windows) |
| write failed | The output or its temporary file cannot be created, written or moved (also a folder that does not exist) |
| cancelled | Ctrl+C |

- The recording is opened read-only, allowing other readers, and is never changed. A recording that is still being
  written cannot be opened: stop the recording first.
- The output is written to `<output>.<random name>.tmp` beside it and moved to the output name without overwriting
  when it is complete, so an existing file is never replaced and a killed run leaves at most a `.tmp` file.
- The recording is read twice, one line in memory at a time whatever its size: the first pass checks every line and
  collects the distinct names (memory bounded by those names), the second writes. It runs on Linux and Windows.

### What changes

- **Names.** Every non-empty island name (the area name of an island frame) becomes `Island <n>` and every non-empty
  figure name (the text of a session start) becomes `Player <n>`, numbered from 1 in the order of first appearance in
  the file, never derived from the original text. One original name always gets the same invented name, whichever
  island or session carries it, and two names get two, so a rename or a change of figure stays visible. A number is
  skipped when its invented name would equal an original name of the file, of either kind, ignoring case. An empty
  name stays empty and takes no number. Names compare as the decoded strings the reader delivers.
- **Times.** Every receive time and the header's `started` move earlier by one offset per run, drawn from 30 days up
  to but not including 365 days and never a whole number of days, so neither the date nor the time of day survives;
  every pause between two lines is kept to the 100 ns. The header's `started` is the first frame's shifted time, else
  the input header's `started` shifted, else 2000-01-01T00:00:00Z shifted; an input `started` less than 366 days after
  the earliest representable time is not used. Neither the offset nor any factor is written.
- **Amounts** of each entry of an island frame:
  - Generation, consumption, perfect generation, perfect consumption, income and summed productivity are multiplied
    by one factor per entry, drawn from 0.80 to 0.99 or from 1.01 to 1.20; if a finite value would become infinite,
    the entry's factor comes from the lower range.
  - Delta becomes generation minus consumption and average productivity 100 times summed productivity over the
    building count (0 for a building count of 0), each from the perturbed values; where generation, consumption or
    summed productivity is not finite, delta or average productivity is multiplied by the factor instead.
  - Maintenance and every workforce amount change on their own and keep their sign: 0 stays 0, a magnitude of 1
    becomes 2, 2 to 4 change by exactly 1, from 5 on by 1 % to 20 %, rounded and by at least 1; where an increase
    would leave the 32-bit range they decrease. The amounts of a workforce GUID that repeats are drawn so that their
    sum stays within the 32-bit range.
  - Profit becomes an integer within 1 of the new income minus maintenance, on the same side of it as the original
    profit was of the original difference; a non-zero profit never keeps its value, a zero profit keeps it only while
    0 is within 1 of the new difference, and its sign may change. At the edge of the 32-bit range it moves 1 inwards
    (then it is farther than 1 from a difference outside the range). With a non-finite new income, profit follows the
    integer rule of maintenance.
  - A finite, non-zero float that a rule above would write equal to its original (a multiplied amount that rounds back
    to itself, a subnormal one, a delta or average that comes out as before) is written as the adjacent float32
    instead: away from zero for a factor above 1, towards zero for a factor below 1, the other way where that step
    would give 0 or an infinity. No further random draw is made. Delta and average productivity follow the written
    (stepped) generation, consumption and summed productivity, so the relations hold to within 2^-22 of the largest operand plus 2^-148 (two steps of the smallest float, which
    matters only for subnormal values): `|delta - (generation - consumption)| <= 2^-22 * max(|generation|, |consumption|,
    |delta|) + 2^-148` and `|average - 100 * summed / count| <= 2^-22 * max(|average|, 100 * |summed| / count) + 2^-148`.
  - Zero stays zero and an infinity stays; a NaN in any float field is written as the standard NaN with its sign
    (`0x7FC00000` or `0xFFC00000`), so its payload bits cannot carry data.
- **Randomness.** Offset and factors come from the operating system's cryptographic random source, fresh for every
  run; there is no seed, and a result cannot be reproduced.

### What is kept

Line count and order, frame types, `Version` frames, session ends, session ID, island ID, area index, session GUID,
game time, entry count and order, product GUIDs, building counts, the building map and the workforce map's GUIDs, pair
count and order (a GUID that repeats keeps every pair in its place). A replay of the output gives the same ticks, keys,
game times, close reasons and session events as the original's, with every receive time earlier by the offset.

### What is refused

- A frame the tool does not decode gives `frame cannot be anonymised in line <n>`: an unknown type, a malformed
  message, and any frame but `Version` before a `Version 2` or after a `Version` of another number. Its bytes could
  hold a name, so it is neither copied nor dropped. An empty frame holds nothing and is copied. A `Version` frame of
  any number is written anew.
- Every line and every frame is written anew from its decoded fields by the format's own header and frame-line
  writers: unknown JSON fields, escapes, time offsets, a byte order mark, carriage returns, base64 padding bits and
  bytes after the last field of a frame do not reach the output. Each written island frame is parsed back and must end
  where the parser ends and keep the entry count, product GUIDs and the GUIDs of both maps; otherwise the frame is
  refused, so a mistake in the writing never copies bytes.

### Fixtures

Anonymised recordings under `tests/Censio.Pipe.Tests/Fixtures/` are read by the tests of the message reader and the
tick assembler; their `README.md` says how each was made and which facts it shows, never an original name, file name
or time. A maintainer may keep a local deny list (names that must not appear and the path of each fixture's original,
named by the environment variable `CENSIO_FIXTURE_DENY_LIST`); the test that uses it is inconclusive where it is
missing, compares each fixture with its original by position and reports counts, line numbers and field names only.
The deny list and the originals are never committed.

## Related decisions

- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — recordings hold the raw frames as JSON lines, written off the read path; replay feeds them back on a recorded clock.
- [`decisions/0029-anonymised-test-fixtures-only.md`](../decisions/0029-anonymised-test-fixtures-only.md) — only anonymised fixtures from recordings are shared.
- [`decisions/0042-recordings-anonymised-by-a-contributor-tool-rewriting-frames.md`](../decisions/0042-recordings-anonymised-by-a-contributor-tool-rewriting-frames.md) — a contributor console tool rewrites each frame at wire level, with fresh randomness per run.

## Not here

- The frame layout, the reader's limits and the tick rules a replay goes through: [`pipe.md`](pipe.md).
- The connection whose frames are recorded: [`pipe-connection.md`](pipe-connection.md).
- `--record`, `--replay`, `--speed`, the menu entries, the recordings folder and the tooltips: [`companion.md`](companion.md).
