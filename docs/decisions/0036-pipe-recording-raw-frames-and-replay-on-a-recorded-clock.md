# 0036: Recordings hold the raw frames as JSON lines, written off the read path; replay feeds them back as a byte stream on a recorded clock

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** pipe
- **Source:** Issue #12; Issue #13 (recording toggled from the tray menu)
- **Supersedes:** —

## Context

Everything after the pipe reader has to be built and tested against what the game really sends, and an external
tester has to be able to hand that in (issue #12, `specs/feature-12-recorder-replay/spec.md` on the work branch).
Issue #12 started recording only with the Companion's switch `--record`, until `Exit`; issue #13 adds
`Start recording` and `Stop recording` to the tray menu, so a recording can begin in the middle of a connection and
end before `Exit`, and a failed one can be started again (`specs/feature-13-tray-app/spec.md` on the work branch). A replay must give the live run's ticks (record [0034](0034-ticks-assembled-by-close-rules-on-an-injected-clock.md)),
which assigns every frame its receive time once, when its read completes. The pipe is untrusted, sends frames of up
to 1 MiB, and is read by one caller that has to keep requesting; a slow disk must not slow it down, and the
Companion must still end within 2 seconds.

## Options considered

1. **The whole frame on the read result, recorded where the receive time is read** (chosen) — the frame reader
   already holds each frame in its own array; the recorder gets the same bytes and the same clock reading as the
   tick assembler. Con: a read result keeps its frame alive as long as the caller keeps the result.
2. **A tee stream that copies the pipe's bytes** — no change to the reader; the recorder would need its own framing
   to find frame boundaries and would read the clock a second time.
3. **A frame hook in the frame reader with its own clock** — no change to the result type; two clock readings per
   frame, so a recorded time can differ from the tick's (spec challenge m1).
4. **Writing each line on the read path** — no queue and no memory beyond one line; a stalled disk delays the next
   read, so receive times and the quiet period change (spec challenge M1).
5. **A bounded queue of 16 MiB off the read path, each entry charged its line length plus a fixed overhead;
   overflow stops the recording** (chosen) — the live run never waits for the disk; a disk slower than the pipe
   costs the recording, not the run; the per-entry charge keeps a flood of empty frames bounded too (Security plan
   review).
6. **Replay as a byte stream into the existing message reader and tick assembler, on a clock set to the recorded
   times** (chosen) — the version gate, limits, skip rules, close rules and quiet period are the live code, not a
   copy (spec challenge m6). The clock's timers never fire: the next frame's recorded time closes a quiet tick
   before the frame is added, so the items are the same as with a firing timer. Con: an own small clock type.
7. **Replay feeding the pure tick core directly with the recorded times** — no clock type (record 0034 allows it);
   bypasses the async driver the live run uses, and the version gate would still need the message reader.
8. **The test library's fake clock as the replay clock** — no own type; ships a test package in the Companion.
9. **JSON lines without a header** — the issue's minimum; a file could not be told from other JSON lines, and the
   format could not change without guessing. A header line with a format name and version 1 was chosen.
10. **Buffered writes, flushed at the end** — fewer writes; a crash would lose up to the whole buffer. Each line
    is flushed to the operating system (about one write per second), which survives a killed Companion.
11. **A disk sync per line (`WriteThrough` or `Flush(true)`)** — survives a power loss too; a disk write per frame
    and less of the 1-second budget at `Exit` for the queue (plan challenge M1). Rejected: a power loss may cost
    the last lines.
12. **The assembler waits the quiet period in steps of at most one day and re-checks the clock** (chosen) — a
    clock set back after an island, by a recorded time (E7) or the system, puts the quiet deadline beyond what
    `Task.Delay` accepts (about 49.7 days) and made it throw out of the assembler (Security plan review F1,
    measured). The cap changes no item: a replay's timers never fire, a live clock reaches the same deadline.
13. **Refusing recorded times far before the previous one as an invalid line** — keeps the assembler as it was;
    changes E7's promise that backward times replay unchanged, and leaves a system clock set back unguarded.
14. **Stopping the recording from `Application.SessionEnding` as well as from `OnExit`** — a sign-out or shutdown
    would get the 1-second flush before Windows asks the next application; but `SessionEnding` comes with the
    query, which another application can still refuse, so a refused sign-out would leave the Companion running
    without its recording. Rejected: sign-out and shutdown are left to the `Exit` stop in `OnExit`, which WPF is expected
    to raise when the session ends (not verified; checked by the maintainer's manual test on Windows); whether
    Windows waits for its second is not promised.
15. **A recording started mid-connection first writes the connection's last `Version` frame, stamped with the
    receive time of the first frame it records** (chosen, #13) — the version gate skips every frame of a file without
    it; the stamp keeps the replay from waiting before the first frame and names the file by the start. Con: that one
    line does not carry its own receive time (version messages close no tick and start no quiet period, so the
    replayed ticks are unchanged).
16. **The same `Version` frame with its own receive time** — every line true to the pipe; the replay at speed 1 would
    wait out the whole time from the connect to the start, and the file name would carry the connect time.
17. **Starting the file only at the next `Version` frame** — no synthetic line; the game sends `Version` once per
    connection, so a start while receiving would record nothing.
18. **A failure stops recording for the rest of the run** (#12) — simple; with a menu the user can only retry by
    restarting the Companion. Rejected for #13: a new `Start recording` resets the failure, and a stopped recording that
    fails while it writes its last frames is only logged, so it cannot mark a healthy new recording as failed.

## Decision

Options 1, 5, 6, 12, 15 and the header of 9: each whole frame travels with its read result and is recorded with the
receive time the pipeline read once, through a queue bounded to 16 MiB of line cost that a writer drains to a
file flushed per line to the operating system; a replay turns a recording back into the pipe's byte stream for the same message reader and
tick assembler, on a clock that reads the recorded times. A recording is started and stopped from the tray menu
as well; one started mid-connection begins with the connection's last `Version` frame (option 15), and a new start
resets a failure (option 18). The format, the limits and the replay errors are in
[`areas/pipe.md`](../areas/pipe.md); the switches and tooltips in [`areas/companion.md`](../areas/companion.md).

## Consequences

- The last tick of a replay closes as *stream ended* where the live run may have closed it by the quiet period
  (record 0034); the stream-ending error itself is not recorded.
- A recording started mid-connection lacks the islands of the open tick sent before the start and the session start
  before it; its first replayed tick is therefore partial (#13).
- The anonymiser (#20) writes format version 1; a change to the format raises the header version.
- A recording holds private names and stays local; only anonymised fixtures are committed (record
  [0029](0029-anonymised-test-fixtures-only.md)).
- At `Exit` the queue is written for at most 1 second; a stalled disk can leave the last line cut, as a crash does.
  A power loss can lose the lines the operating system had not yet written.
- Recorded times are untrusted: a time whose quiet deadline would overflow is an invalid line, a gap longer than a
  single delay allows is waited in steps by the replay, and a quiet deadline far ahead of a clock set back is waited
  in steps by the assembler (option 12, a change to `PipeTickAssembler` within this feature).
