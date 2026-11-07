# Spec: Protocol documentation from our own recordings

Source: Issue #18
Status: Draft

## Problem

Our knowledge of the pipe rests on Ubisoft's example reader and a third-party capture (`docs/areas/pipe.md`,
*Residual risks*); developers cannot tell observed facts from guesses. One local recording of a real session
(2026-11-07, about 90 minutes, two loads) may be analysed but never published: only derived facts (counts,
intervals, sequences, field relations) may appear anywhere.

Tier `trivial`, not `docs`: documentation only, but `docs/areas/pipe.md` must change with it, which the `docs` tier
excludes (`.squad/routing.md`, *Tiers*).

## Scenarios

none

## Errors and edge cases

- E1: What the recording cannot show (single reader, duplicate map keys, non-ASCII names, rate units) stays
  *assumed*, with its source.
- E2: A statement the recording contradicts is corrected, with what it shows.
- E3: A tick rule that does not match the recording is not changed here: a follow-up issue records it.

## Non-functional requirements

- N1: Privacy (Product Manager): no frame, base64, payload, headline, savegame, island, area, region or player name
  and no recording path in any repository file, spec, log or GitHub comment; names become placeholders.
- N2: Byte layout and reader rules stay in `docs/areas/pipe.md`; the protocol document links them.

## Acceptance criteria

- AC1: Given the recording, when a developer reads `docs/protocol.md`, then it covers the issue's scope (framing by
  link, every message type with field meanings and units, timing, timestamp meaning, island identity, the edge
  cases, single-reader behaviour), each statement marked *confirmed* (recording of 2026-11-07, by date only) or
  *assumed* (with its source), and no file of the change holds anything N1 forbids (E1, N1, N2).
- AC2: Given the issue's edge cases (connect sequence, zero-entry tick, duplicate session end, rename), when the
  change is done, then each is stated as confirmed, corrected or still assumed (with why), and
  `docs/areas/pipe.md` does not contradict it (E1, E2).
- AC3: Given the tick rules of `docs/areas/pipe.md` (island key, the five close rules, no data yet, session
  events), when compared with the recording, then `docs/protocol.md` states for each "matches" or "not exercised",
  and every mismatch has a follow-up issue the document names (E3).

## Out of scope

- Code changes (E3); publishing or anonymising the recording (#20); further recordings.
- Released record 0033, which cites the third-party capture: unchanged.

## Open questions

- What does the recording settle? Answered: by the Lead (derived facts; the Dev checks each against the recording):
  - Connect: `Version 2`; a session end 13 s later with no session start before it; session start 93 s later; first
    island frame 4 s after it.
  - Session end: 2 ends, each followed by a start (48 s the second time), never two in a row. The third-party
    "second session end" is not seen; the one after the connect is the assembler's *repeated session end*.
  - Zero-entry tick: the first tick after each start, all islands empty (21 of 24, then 24); no other empty
    island. The first has timestamp 19,000 (later ticks about 1.75 × 10⁹); the second continues the game time.
  - Timing: island frames 1 s apart (some 3 s, at most 11 s); a 24-island tick spans about 23 s; ticks start
    120–121 s apart, except gaps of 25 s (after the zero tick), 169 s, 554 s (session change) and 1,099 s.
  - Timestamp: shared within a tick, never by consecutive ticks; a multiple of 100; +71,700 to +280,700 per tick,
    so game time in ms running with game speed, and a pause stops ticks (both assumed, consistent).
  - Identity: 24 keys over 3 session GUIDs; session ID byte one-to-one with the GUID; area index fixed per key; one
    island renamed mid-session, key unchanged; same keys after the second load; same island order every full tick.
  - Fields: delta = generation − consumption (all 21,091 entries); profit = income − maintenance (within 1);
    average productivity = 100 × summed productivity / building count; building-map amounts sum to building
    count; maintenance ≥ 0; perfect generation per building 0.5, 0.667, 1, 1.333, 2 … (fits per minute, unit
    assumed); no NaN, trailing bytes, duplicate map GUID or non-ASCII name byte; maps ≤ 3 workforce / 8 building
    pairs; one name with `_`.
  - Tick rules: 33 ticks — 31 *game time changed*, 1 *quiet period*, 1 *stream ended*; no *island repeated*, no
    tick limit.
- Single reader? Answered: not recordable; `docs/areas/pipe-connection.md` holds the manual observation, linked.

## Area documents

- `docs/areas/pipe.md`: *Session events* gets the observed reason for the repeated session end; *Residual risks*
  says what one recording confirmed, links `docs/protocol.md` and tells the third-party `docs/protocol.md` apart.
  Rules unchanged.
- `docs/protocol.md` (new, not an area): the game's side of the pipe as observed.
