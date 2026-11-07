# Plan: Savegame profile detection from SessionStart

Source: Issue #19 | [spec.md](spec.md)
Tier: trivial — documentation that needs a decision record, no production or test code and no behavior change.
Security areas touched: none

## Problem / root cause

See [spec.md](spec.md): the decision how a tick finds its profile, and the documents corrected to the confirmed
SessionStart content.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The pipe carries no savegame ID | `docs/areas/pipe.md:38-44` message table; `src/Censio.Pipe/PipeMessageParser.cs:57` | confirmed |
| 2 | SessionStart is the figure name; two saves of one figure identical, another figure differs; a load is end then start | PM's recordings, anonymised summary `/tmp/claude-0/-home-user-Censio/5c1ea317-5dd6-5cde-a63b-e67027602d4d/scratchpad/sessionstart-findings.md` | confirmed |
| 3 | Observed name lengths 6 and 18 bytes; start right after the version (R1, R2) or after an end (R3, R4) | same file | confirmed |
| 4 | R3 is the recording `docs/protocol.md` already describes (2026-11-07, about 90 min, two loads, 18-byte starts) | `docs/protocol.md:10-11,31`; findings table | confirmed |
| 5 | Record 0020 and 0031 are released, so not edited | `git tag --contains` of their adding commits: `v0.1.0` | confirmed |
| 6 | No code outside `Censio.Pipe` uses the SessionStart text | `grep -rn Headline src` | confirmed |
| 7 | "headline" for the text appears in `docs/USER_GUIDE.md:170` (DE: `:419` "Überschrift des Spielstands"), `docs/areas/pipe-recording.md:111`, `docs/areas/companion.md:167`, `docs/areas/pipe.md:41,102`, `docs/protocol.md:12,31`; not in `README.md` | grep | confirmed |
| 8 | `docs/areas/pipe.md:200` names #19 for telling resent ticks apart, which #19 does not decide | read | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: Reviewer reads record 0041 against S1–S7, N1 | — |
| AC2 | check: Reviewer reads record 0041 *Options considered*, *Consequences* | — |
| AC3 | check: Reviewer reads `docs/areas/pipe.md`, `docs/protocol.md` | — |
| AC4 | check: `grep -rn -i "headline\|Überschrift des Spielstands" docs README.md` finds no line that calls the SessionStart text the savegame headline | — |
| AC5 | check: orchestrator, step 10 | — |
| AC6 | check: orchestrator | — |

## Verification without tests

Steps 4, 5 and the *Coverage gate* are not applicable: documentation only.

- AC1: check: Reviewer, step 8 — record 0041 decides option 5, local name memory, no name in the contract; links 0020, 0031; neither edited (`decision-check.py`).
- AC2: check: Reviewer, step 8 — five options with reasons; consequences name E2, S7, #27, #29, #30, #31, #32, #43.
- AC3: check: Reviewer, step 8 — the edits of *Documentation updates* items 1 and 2.
- AC4: check: Reviewer, step 8 — the grep of *Tests per criterion*; `README.md` not in the diff.
- AC5: check: orchestrator, step 10 — a comment linking `docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md` on #27, #29, #30, #31, #32, #43, confirmed with `gh api repos/larslaskowski/censio/issues/<n>/comments`.
- AC6: check: orchestrator, step 7 — `scope-check.py` (no file outside `docs/`, `specs/`), and a grep of the diff for the real figure names before each commit, logged as passed only.

## Approach

The Lead wrote record 0041 and its index row. The Dev corrects the documents below from the facts above, stating
lengths, counts and comparisons only (N1). Layout free for the Dev within the existing sections, approved in step 9.

## Options considered

Tier `trivial`: the product options are in record 0041.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | documentation only | see *Documentation updates* |

## Signatures (for the Dev's skeleton)

none

## Test files

none. Existing test call sites: none. Files no unit test can cover: none.

## Areas

`pipe`, `pipe-recording`, `companion`: wording and confirmed facts only, no behavior change (items below).

## Documentation updates

All by the Dev.

1. `docs/areas/pipe.md`: table row type 1 payload "string (player figure's name)"; *Session events*: the start is
   delivered with the figure's name unchanged; "observed once" → seen in the recordings that began with an end;
   *Residual risks*: the SessionStart text is the figure's name, no savegame key; *Not here*: line 200 keeps #35 and
   adds "Assigning ticks to a profile: record 0041"; *Related decisions*: add 0041.
2. `docs/protocol.md`: *Sources* adds the three other recordings of #17 (2026-11-03, 2026-11-04, 2026-11-07; about
   1,360 frames in all four) next to the existing one, which stays the source of the other sections; the
   status line says which statements rest on all four. *SessionStart*: the figure's name, confirmed (facts 2, 3), no
   savegame key, record 0041. *Connect sequence*: both orders (fact 3). Line 12 keeps "no … name".
3. `docs/areas/pipe-recording.md:111`, `docs/areas/companion.md:167`: "headline" → "the player figure's name".
4. `docs/USER_GUIDE.md:170` (EN) and `:419` (DE): the recording holds the name of the player figure (which can be
   your real name) — DE "den Namen der Spielfigur (der Ihr echter Name sein kann)".

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` changes; record 0041 fills record 0020's open point
and keeps record 0031's buffer as it is. `docs/ARCHITECTURE.md` names no profile flow yet: unchanged.

## Security considerations

- [x] No limits.
- [x] None.

## Decision records

- `docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md` (Proposed)

## Out of scope / follow-ups

As in [spec.md](spec.md); no follow-up issue: #27, #29, #30, #31, #32 and #43 build it (AC5).
