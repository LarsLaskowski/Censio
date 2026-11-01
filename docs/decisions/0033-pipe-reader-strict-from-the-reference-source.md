# 0033: The pipe reader is strict, fails closed and is built from the reference source with synthetic frames

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** pipe
- **Source:** Issue #9
- **Supersedes:** —

## Context

The statistics pipe is the Companion's untrusted input (`.squad/project.md`, *Parsing of external input*). Its only
description is Ubisoft's example reader (`UbisoftMainzAnno/anno117_pipe_example`, `src/pipe.h` and `src/pipe.cpp`,
read on 2026-11-01), which calls itself "for documentation purposes only" and reads leniently: a field past the end
of a frame reads as `0`, a frame over 1 MiB is reported and the stream read on out of step, an unexpected protocol
version is logged and ignored, and duplicate map amounts are summed in an `int32` that can overflow. No real
recording exists yet: Censio's recorder (#12) and the anonymiser (#20) come after this reader, and record
[0029](0029-anonymised-test-fixtures-only.md) admits only anonymised fixtures.

## Options considered

1. **Read as the reference does** — the same numbers as the example; a short or hostile frame gives zeros, and after
   an oversized frame every later frame is misread.
2. **Strict and fail-closed** (chosen) — a short field, a count the frame cannot hold or an overflowing sum rejects
   the frame; an oversized or negative length ends the stream, since nothing marks where the next frame starts; no
   frame other than `Version` is decoded while the version is unknown or not `2`. Con: a frame the reference would
   half-read is dropped whole.
3. **Skip an oversized frame instead of stopping** — keeps the connection; means reading up to 2 GiB the reader
   cannot check, from a stream that is already out of step if the length was corrupt.
4. **Wait for a real recording before building the reader** — the strongest tests; blocks #10–#12, which a recording
   needs first. The Product Manager chose to build from the reference with synthetic frames (issue #9, step 2).

## Decision

The reader in `Censio.Pipe` implements option 2 against the wire format of the reference source, tested with
synthetic frames only; what it accepts, skips and stops on is in [`areas/pipe.md`](../areas/pipe.md).

## Consequences

- A caller (#11) reconnects after an oversized-frame, truncated-frame or read-failed error: the reader ends the
  stream.
- Residual risks until the #20 fixtures test the reader against real recordings: the format is known only from the
  reference source as read on 2026-11-01; strings are decoded as UTF-8 although the reference names no encoding
  (Windows-1252 would garble non-ASCII bytes); that the game writes `_` for umlauts rests on a third-party capture
  (`Dealwatch/Tabularium117`, `docs/protocol.md`), not on our own; whether the game ever sends duplicate map keys is
  unknown.
- A map holds at most 1,024 pairs (Security plan review): integer keys hash to themselves, so colliding keys
  would cost seconds of CPU per frame; real maps hold a few dozen pairs. A future game with larger maps would see its
  frames rejected until the cap is raised.
- A game update that changes the format without a new protocol version is misread as long as every field still fits;
  one that bumps the version is refused with a protocol-mismatch error.
