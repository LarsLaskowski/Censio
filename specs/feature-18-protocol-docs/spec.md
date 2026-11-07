# Spec: Protocol documentation from our own recordings

Source: Issue #18
Status: Draft

## Problem

What we know about the game's statistics pipe comes from Ubisoft's example reader and a third-party capture
(`docs/areas/pipe.md`, *Residual risks*). Developers who build on the pipe (tick buffer, ingest, anonymiser) cannot tell which statements about field meanings, units, timing and edge cases
were seen in our own data and which are guesses. Since #17 the Companion can record a connection; one recording of
a real game session (2026-11-07, about 90 minutes, two loads of one savegame) is available to the maintainer. It
may be analysed but never published: only facts derived from it (counts, intervals, sequences, relations between
fields) may appear anywhere.

Tier: `trivial`, not `docs`. The change is documentation only, but the statements of `docs/areas/pipe.md` that
the recording confirms or corrects (*Session events*, *Residual risks*) must change with it, and the `docs` tier
excludes `docs/areas/` (`.squad/routing.md`, *Tiers*). No production or test code changes.

## Scenarios

none

## Errors and edge cases

- E1: A statement the recording cannot show (single-reader behaviour, duplicate map keys, non-ASCII names, the
  unit of rates) stays marked *assumed*, with its source, never *confirmed*.
- E2: A statement the recording contradicts is corrected and marked *confirmed*, with what the recording shows.
- E3: A tick-assembler rule that does not match the recording is not changed here: a follow-up issue records it.

## Non-functional requirements

- N1: Privacy (Product Manager): no frame, base64, raw payload, headline, savegame, island, area, region or player
  name, and no path of the recording appears in any repository file, spec, log or GitHub comment. Names are
  replaced by placeholders (`<island A>`); counts, intervals, sequences and field relations may be written.
- N2: One home per behavior: the byte layout and the reader's rules stay in `docs/areas/pipe.md`; the protocol
  document links them instead of restating them.
- N3: English only (record 0030: `README.md` and user guides are bilingual, this is neither).

## Acceptance criteria

- AC1: Given the recording, when a developer reads `docs/protocol.md`, then it gives, each statement marked
  *confirmed* (by the recording of 2026-11-07, named by date only) or *assumed* (with its source): the framing
  (linked to `docs/areas/pipe.md`), every message type with the meaning and unit of each field, the timing (frame
  spacing, tick interval, pauses), the meaning of the timestamp, the island identity, the connect sequence, the
  zero-entry tick, the duplicate session end, a rename, and the single-reader behaviour; and no repository file,
  spec or log of the change holds any item N1 forbids (E1, N1, N2, N3).
- AC2: Given the edge cases the issue lists (connect sequence, zero-entry tick, duplicate session end, rename),
  when the change is done, then each is stated in `docs/protocol.md` as confirmed, corrected (with what the
  recording shows) or still assumed (with why the recording cannot show it), and `docs/areas/pipe.md` says
  nothing that contradicts it (E1, E2).
- AC3: Given the tick rules of `docs/areas/pipe.md` (island key, the five close rules, no data yet, session events),
  when they are compared with the recording, then `docs/protocol.md` states for each that it matches the recording,
  or that the recording does not exercise it, and every mismatch has a follow-up issue whose number the document
  names (E3).

## Out of scope

- Changes to the reader, the tick assembler or any other code; a mismatch is a follow-up issue (E3).
- Publishing or anonymising the recording (#20) and fixtures built from it.
- More recordings than the one available; the statements only they could confirm stay *assumed*.
- The released decision record 0033, whose *Consequences* cite the third-party capture: it stays as written.

## Open questions

- Which statements does the recording settle? Answered: by the Lead, from the recording (derived facts only; the
  Dev checks each against the recording before writing it):
  - Connect sequence: `Version 2` at the connect; a session end 13 s later with no session start before it; the
    session start 93 s after that; the first island frame 4 s after the session start.
  - Session events: 2 session starts and 2 session ends, each end followed by a start (48 s later the second time);
    no two consecutive session ends. The third-party "second session end" is not seen; the one after the connect
    is what the assembler delivers as a *repeated session end*.
  - Zero-entry tick: the first tick after each session start has all islands with 0 entries (21 of the 24 islands
    after the first start, 24 after the second); no other tick has an island without entries. The first one's
    timestamp is 19,000, far below the following ticks (about 1.75 × 10⁹); the second's continues the game time.
  - Timing: island frames 1 s apart (median 1.000 s; some 3 s; at most 11 s, inside a zero-entry tick); a tick of 24
    islands spans about 23 s; tick starts 120 to 121 s apart, apart from gaps of 25 s (zero tick to first full
    tick), 169 s, 554 s (across the session change) and 1,099 s.
  - Timestamp: one value shared by all island frames of a tick, never by two consecutive ticks; always a multiple of
    100; it grows by 71,700 to 280,700 per 121 s tick, so it is game time in milliseconds that runs with the game
    speed (assumed meaning, consistent), and the 1,099 s gap with a normal increase points to a pause that stops
    the ticks (assumed).
  - Island identity: 24 keys over 3 session GUIDs; the session ID byte maps one-to-one to the session GUID; the
    area index is constant per key; one island's area name changed once mid-session while its key stayed
    (rename confirmed); the keys were the same after the second load; island order within a tick was the same in
    every full tick.
  - Fields: delta = generation − consumption in all 21,091 entries; profit = income − maintenance within 1 in all;
    average productivity = 100 × summed productivity / building count (percent); the building map's amounts sum to
    the building count; perfect generation ≥ generation in nearly all entries; maintenance never negative; perfect
    generation per building takes values such as 0.5, 0.667, 1, 1.333 and 2 (consistent with units per minute, but
    the unit stays assumed); no NaN, no bytes after the last field, no duplicate map GUID, at most 3 workforce and 8
    building pairs per map, no product twice in one island, no non-ASCII name byte, one name with `_`.
  - Tick rules: grouped by the close rules, the recording gives 33 ticks: 31 closed by *game time changed*, the last
    of the first session by the *quiet period*, the last by *stream ended*; no *island repeated*, no tick limit.
- Single-reader behaviour? Answered: a recording cannot show it; `docs/areas/pipe-connection.md` holds the manual
  observation (the game serves one client at a time), which the protocol document links.

## Area documents

- `docs/areas/pipe.md`: *Session events* — the reason given for the repeated session end becomes what the recording
  shows (the session end after the connect); *Residual risks* — the timing, shared timestamp, zero-entry tick and
  island-count bullet says which statements one recording confirmed and links `docs/protocol.md`; the third-party
  `docs/protocol.md` citation is told apart from ours. The rules themselves do not change.
- `docs/protocol.md` is new and not an area document: it describes the game's side of the pipe as observed, the
  area documents describe what the Companion does.
