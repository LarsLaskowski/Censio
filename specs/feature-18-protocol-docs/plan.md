# Plan: Protocol documentation from our own recordings

Source: Issue #18 | [spec.md](spec.md)
Tier: trivial — documentation only, but it edits `docs/areas/pipe.md`, which the `docs` tier excludes.
Security areas touched: none

## Problem / root cause

[spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The spec's recording facts hold | Lead's scratch analysis of the local recording, outside the repository | confirmed |
| 2 | The quiet deadline is checked before every other close rule, 30 s after the last island frame's receive time | `src/Censio.Pipe/PipeTickBuilder.cs:62` (`QuietDeadline`), `:86` (`CloseIfQuiet` first in `Add`), `PipeTickLimits.cs:27` | confirmed |
| 3 | `pipe.md:104` says "The game sends a second session end"; the recording shows no two ends in a row | `docs/areas/pipe.md:104`; spec, *Session end* | refuted |
| 4 | `pipe.md` *Residual risks* calls timing, shared timestamp, zero-entry tick and island count "third-party observations until the recordings of #17 confirm them" | `docs/areas/pipe.md:180-182` | confirmed |
| 5 | Record 0033 is released (unedited) | `git tag --contains b4b4db6` → `v0.1.0` | confirmed |
| 6 | No tick rule of pipe.md contradicts the recording; *session event*, *island repeated* and tick limits are not exercised | spec, *Tick rules* | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | none: *Verification without tests* | — |
| AC2 | none: *Verification without tests* | — |
| AC3 | none: *Verification without tests* | — |

## Verification without tests

Steps 4, 5 and the *Coverage gate* are not applicable (no code); step 7 runs *Format check* and `scope-check.py`.

- AC1: check: Dev — every AC1 scope item has a section, every statement a mark; privacy: a scratch script outside the repository reads the headlines and area names from the
  local recording and greps `git diff main...HEAD` plus uncommitted changes and `specs/` for each, printing only the
  hit count (must be 0) — never the names; the Reviewer checks that no base64, frame bytes or path appears.
- AC2: check: Reviewer — the four edge cases are each *confirmed* or *corrected* in `docs/protocol.md`, and
  `docs/areas/pipe.md` no longer says the game sends a second session end.
- AC3: check: Dev re-derives the close reasons from the local recording with a scratch model of pipe.md's rules
  (quiet deadline first, fact 2) and compares with the spec; Reviewer checks one row per rule in
  `docs/protocol.md`. A mismatch: the Dev reports it, the orchestrator files the follow-up issue, its number goes
  into the document.

The orchestrator gives the Dev the local recording's location, read-only; nothing from it, nor its path, enters
the repository, `log.md` or GitHub.

## Approach

`docs/protocol.md`, English, layout:

1. `# Statistics pipe protocol`, intro: the game's side as observed, the sources (reference reader 2026-11-01;
   recording of 2026-11-07), the marks *confirmed* / *assumed*, derived facts only.
2. `## Framing` — one paragraph linking `areas/pipe.md` (*Frame layout*, *Message types*); no restated layout.
3. `## Messages` — one `###` per type; for island frames a table `| Field | Meaning | Unit | Status |` with every
   field, the relations and the building-count-0 values.
4. `## Timing`, `## Timestamp`, `## Island identity` — bullets from the spec.
5. `## Edge cases` — `### Connect sequence`, `### Zero-entry tick`, `### Duplicate session end` (corrected),
   `### Rename`; each opens with its status.
6. `## Single reader` — assumed, linked to `areas/pipe-connection.md`.
7. `## Tick rules against the recording` — a table `| Rule (pipe.md) | Recording | Status |`, one row each: island
   key, the five close rules, no data yet, session events, tick limits; counts from the spec.
8. `## Sizes` — the maxima against the limits.


## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `docs/protocol.md`, `docs/areas/pipe.md`, `docs/ARCHITECTURE.md` | *Documentation updates* |

## Signatures (for the Dev's skeleton)

none

## Test files

none. Existing test call sites: none. Files no unit test can cover: none (no code).

## Areas

`pipe`: wording only, rules unchanged. No new area: `docs/protocol.md` describes the game, not Censio.

## Documentation updates

All by the Dev:

- `docs/protocol.md`: new, as *Approach*.
- `docs/areas/pipe.md`: *Scope* links `../protocol.md` for the observed protocol. *Session events* (line 104):
  replace "The game sends a second session end; …" with the observed case — the game sends a session end right
  after the connect, before any session start — keeping the rule. *Residual risks*: the bullet of lines 180–182
  says timing, shared timestamp, zero-entry tick and island count were confirmed by one recording
  (`../protocol.md`) and the double session end was not seen; the duplicate-map-key bullet says none was seen in
  one recording; the `_`/umlaut bullet names the third-party file as `Dealwatch/Tabularium117`'s, distinct from
  ours. Lines 171 and 173 stay ("#20 fixtures").
- `docs/ARCHITECTURE.md`, *Data flow* (lines 40–42): link `protocol.md` after the strict-parsing sentence.
- Sweep: no enumeration is extended; `README.md`, the area index and the PR template need nothing (no changelog).

## Architecture check

No guarantee touched; documentation only.

## Security considerations

- [x] no limits.
- [x] none.

## Decision records

none: the issue fixes the file and its content; no lasting code decision.

## Out of scope / follow-ups

As spec; a follow-up only for an AC3 mismatch.
