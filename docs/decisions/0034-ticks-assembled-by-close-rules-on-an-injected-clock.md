# 0034: Ticks are assembled by close rules and a quiet period on an injected clock, in a pure core with an async driver

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** pipe
- **Source:** Issue #10
- **Supersedes:** —

## Context

The game sends one island per frame, about one frame per second, and a full tick about every two minutes, with no
end-of-tick marker (issue #10; the timing is a third-party observation, to be confirmed with the recordings of #17).
The consumers of ticks — replay (#12), the tray (#13), the contract (#27) — need whole ticks, a stable island
identity and typed session events, and replay must yield the ticks of the live run. The reader of record
[0033](0033-pipe-reader-strict-from-the-reference-source.md) ends the stream after a cancelled read, so a wait for
the end of a tick must not cancel the read in progress. The pipe is untrusted input, so a tick must be bounded.

## Options considered

1. **Close a tick only on a timestamp change, a repeated island key, a session event or the end of the stream** —
   no clock; the last tick of a run, including the one the game sends on connect, waits about two minutes.
2. **Those rules plus a quiet period of 30 seconds after the last island frame, on a clock the caller injects**
   (chosen) — the last tick arrives within 30 seconds; replay drives the same clock from the recorded times. Con: a
   game stalling over 30 seconds mid-tick splits it into two ticks with one game time.
3. **Receive times carried by the message source instead of a clock** — replay supplies them directly; the quiet
   period still needs a clock, so time would come from two places.
4. **One async type that reads, times and groups** — fewer types; every grouping rule is tested only through
   asynchronous reads and timers.
5. **A pure, synchronous core fed with each result and its receive time, plus an async driver that reads the source
   and races the pending read against the quiet-period timer without cancelling it** (chosen) — the rules are
   deterministic and tested without timers; replay can feed the core with recorded times directly.
6. **The tick limit as a new reader error kind** — one error type for callers; changes the reader's documented error
   kinds for a condition the reader never produces. Rejected for an item of the assembler that carries the island key.

## Decision

Options 2 and 5: ticks close on a timestamp change, a repeated island key, a session event, the end of the stream or
a quiet period of 30 seconds measured on an injected clock, in a pure core with an async driver that never cancels
the read it waits on; a tick over its limits drops the frame with an assembler-level *tick limit* item. The rules,
values and limits are in [`areas/pipe.md`](../areas/pipe.md).

## Consequences

- A game stall of over 30 seconds inside a tick yields two partial ticks with one game time; a game that sends one
  island twice in a tick splits it as *island repeated*. Both are revisited with the recordings of #17.
- A replay that ends at its last frame closes that tick as *stream ended*, where the live run may have closed it by
  the quiet period; #12's same-ticks check allows for it.
- Telling a resent or paused tick from a new one is left to the consumers that store ticks (#19, #35).
- The tick limits count decoded entries and map pairs, so the reader trims a map whose duplicate GUIDs were summed
  to its distinct GUIDs (Security plan review); otherwise a map pre-sized to its wire count would keep up to 1,024
  slots while counting as one. Results are unchanged.
- Tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, test project only).
