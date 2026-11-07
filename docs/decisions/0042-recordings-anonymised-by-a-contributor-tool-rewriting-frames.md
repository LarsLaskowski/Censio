# 0042: Recordings are anonymised by a contributor console tool that rewrites each frame at wire level, with fresh randomness per run

- **Status:** Proposed
- **Date:** 2026-11-07
- **Area:** pipe-recording
- **Source:** Issue #20
- **Supersedes:** —

## Context

Record [0029](0029-anonymised-test-fixtures-only.md) allows only anonymised fixtures from recordings and needs an
anonymiser before the first one is committed; the #17 reference recordings exist only outside the repository. A
recording holds the player figure's name, island names, exact economic data and the wall-clock times of play
([`areas/pipe-recording.md`](../areas/pipe-recording.md), *Privacy*). The fixtures have to keep what the reader and the
tick assembler react to — the frame layout, keys, game times, the order and pairs of the maps — and the relations
[`../protocol.md`](../protocol.md) confirms (delta, profit, average productivity), so that later analysis work can use
them (spec `specs/feature-20-anonymiser/spec.md` on the work branch, its challenge 1 and 2). The recording is untrusted
input, and a byte that is copied without being understood could hold a name.

## Options considered

1. **A contributor console program in the repository, run with the .NET SDK** (chosen by the Product Manager) — no
   change to the release; testers keep handing raw recordings to a maintainer privately, and a reviewer sees every
   fixture before it is public (record 0029).
2. **The same program also in the Companion ZIP** — testers could publish anonymised recordings themselves; a second
   program in the release (record [0027](0027-companion-zip-self-contained-single-file-unsigned.md)), and the tool
   becomes a privacy promise to end users that no reviewer checks per file.
3. **A Companion switch `--anonymise`** — no extra program; the tray application has no console to report to.
4. **Re-encoding each frame from the parsed message** — reuses the parser's types; the parser merges a map GUID that
   repeats, so pairs and their places change (spec challenge 2).
5. **Rewriting each frame by walking its wire layout after the parser accepted it** (chosen) — every pair and field
   keeps its place, the version gate and the parser decide what is accepted, and bytes the parser ignores (after the
   last field) are dropped rather than copied. Con: the layout is walked a second time, next to the parser.
6. **Independent noise per value** — simplest; breaks delta, profit and average productivity, and small integers
   cannot change within a relative bound (spec challenge 1).
7. **One factor per entry for the float amounts, derived values recomputed, a fixed step for small integers**
   (chosen) — the relations hold to float32 rounding and every non-zero amount changes; knowing one original amount
   of an entry reveals that entry's factor (accepted: the goal is no exact values in public, not secrecy of an
   amount).
8. **A seed for reproducible runs** — a fixture could be regenerated, but a published seed with the algorithm turns
   the output back into the original; every run draws new values from the operating system's cryptographic random
   source and writes none of them.

## Decision

Options 1, 5 and 7, without a seed (option 8 rejected): a console project `Censio.Anonymiser` over logic in
`Censio.Pipe` reads a recording through the same line rules as a replay, refuses any frame the parser or the version
gate would not decode, and writes each accepted frame anew with invented names, perturbed amounts and receive times
shifted by one random offset. What is kept, changed and refused is in [`areas/pipe-recording.md`](../areas/pipe-recording.md),
*Anonymising*.

## Consequences

- The wire layout of an island frame is known in two places, the parser and the anonymiser; a new field or message type
  changes both (`.squad/project.md`, *Integration surface*).
- Nothing of an input line or frame is copied: every line and frame is written anew (a NaN as the standard NaN with
  its sign), and a written frame that does not parse back to the same shape is refused, so a mistake in the second
  walk refuses a file instead of leaking bytes.
- A profit whose derived change rounds to 0 moves by 1 towards the new income minus maintenance, so no non-zero
  amount stays exact.
- A fixture cannot be reproduced bit for bit; its README says how it was made, not with which values.
- A local deny-list test compares each fixture with its original by place, because a set of values would match by
  chance; it runs only where the originals are, so CI shows it inconclusive.
- A recording with a frame the reader skips, or of a protocol version other than 2, cannot be anonymised.
- Shipping the tool to testers later is a new decision (option 2 or 3) that has to add a reviewer-free privacy promise.
