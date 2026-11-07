# Statistics pipe protocol

The game's side of the Anno 117 statistics pipe as observed, with what is confirmed and what is still assumed. The
byte layout and the reader's rules are in [`areas/pipe.md`](areas/pipe.md); this document does not restate them.

Sources:

- The reference reader of Ubisoft (read on 2026-11-01) and a third-party capture (`Dealwatch/Tabularium117`, its own
  `docs/protocol.md`, not this file).
- One recording of Censio's own Companion of 2026-11-07: about 90 minutes, two loads of a game, one connection, 794
  frames (1 version, 2 session starts, 2 session ends, 789 island frames). It is the source of every section below
  unless a statement names the others.
- Three more recordings of #17 (2026-11-03, 2026-11-04 and 2026-11-07), about 1,360 frames in all four. Only the
  statements on the session start text and the connect sequence rest on all four.

None is published (#20); this document states derived facts only: no frame, name or path.

Each statement is marked *confirmed* (the 90-minute recording of 2026-11-07, or all four where stated) or *assumed* (with its source). What a single
recording cannot show stays *assumed*.

## Framing

The frame prefix, the string and number encodings, the type byte and the trailing-bytes rule are in
[`areas/pipe.md`](areas/pipe.md) (*Frame layout*, *Message types*). *Confirmed*: all 794 frames follow that layout;
no island frame has trailing bytes, and no float is NaN.

## Messages

### Version

- Payload: the protocol version. *Confirmed*: `2`, the first frame of the connection, sent once.

### SessionStart

- Payload: the name of the player figure. *Confirmed* (all four recordings): the text is 6 or 18 bytes long and carries
  no structure the reader uses; it is the same in every recording of one figure, also across two savegames of that
  figure, so it is no savegame key (record 0041). Two figures send different texts. *Assumed* (third-party capture): a non-ASCII character is written as `_`; the recording
  has no non-ASCII byte in any string, so it confirms neither this nor the opposite.

### SessionEnd

- Payload: none. *Confirmed*: sent twice, each time followed by a session start (see *Edge cases*).

### AreaProductionStatistics

One island per frame; the frames of one tick follow each other. *Confirmed*: 789 frames, 21,091 entries in all.

| Field | Meaning | Unit | Status |
| ----- | ------- | ---- | ------ |
| Session ID | Number of the session; maps one-to-one to the session GUID, not part of the island key (see *Island identity*) | none | confirmed |
| Island ID, area index | Parts of the island key (see *Island identity*) | none | confirmed |
| Session GUID | Identifies a session of the game; part of the island key | none | confirmed |
| Area name | Name of the island; it can change (see *Rename*) | text | confirmed |
| Timestamp | Game time of the tick (see *Timestamp*) | milliseconds of game time | assumed (no source states the unit; consistent with the recording, see *Timestamp*) |
| Entry count | Number of product entries of the island | count | confirmed: at most 69 |
| Product GUID | The product of an entry; 125 distinct in the recording, none twice in an island | none | confirmed |
| Generation, consumption | Produced and consumed amount | per minute | assumed (reference names; the unit is not visible) |
| Delta | Generation minus consumption | as generation | confirmed: holds for all 21,091 entries |
| Perfect generation, perfect consumption | Amount at full productivity | as generation | assumed (reference names; the unit is not visible); confirmed: perfect generation is at least generation in 20,980 of 21,091 entries, so it is not an upper bound |
| Building count | Buildings of the product | count | confirmed |
| Maintenance | Maintenance cost, never negative | per minute | assumed (unit; no source states it); confirmed (sign) |
| Income | Income of the product | per minute | assumed (reference name; no source states the unit) |
| Profit | Income minus maintenance | as income | confirmed: within 1 of the difference for all entries (an integer) |
| Summed productivity | Sum of the productivity of the buildings | none | assumed (reference name) |
| Average productivity | 100 times the summed productivity divided by the building count | percent | confirmed (the relation); assumed (percent; no source states it, values reach 435) |
| Workforce map | Workforce GUID to amount | count | confirmed: at most 3 pairs, amounts not negative, no GUID twice |
| Building map | Building GUID to amount | count | confirmed: at most 8 pairs, no GUID twice; the amounts sum to the building count in all entries |

- *Confirmed*: perfect generation per building (perfect generation divided by building count) ranges from 0 to about
  9, mostly from 0.5 to 2.4; the unit (per minute) is *assumed* (no source states it).
- *Confirmed*: with a building count of 0 (3,587 entries) generation, perfect generation, summed and average
  productivity are 0 and the building map is empty; so is the workforce map.
- *Confirmed*: no entry has a NaN, a duplicate map GUID or a non-ASCII name byte; the average productivity stays
  between 0 and 435.

## Timing

- *Confirmed*: island frames of a tick arrive 1 s apart (a few 3 s apart, at most 11 s), so a tick of 24 islands
  spans about 23 s.
- *Confirmed*: ticks start 120 to 121 s apart. Longer gaps: 169 s once, 1,099 s once (no cause is visible), and 554 s
  across the session change; the first tick after the first load starts 25 s after that load's zero-entry tick (the recording ends with the second load's zero-entry tick).
- *Confirmed*: a regular tick reaches the consumer by the quiet period, 30 s after its last island frame
  ([`areas/pipe.md`](areas/pipe.md), *Closing a tick*), so about 53 s after its first.
- *Assumed* (reference): the game sends no end-of-tick marker; the recording shows none.

## Timestamp

- *Confirmed*: shared by all islands of a tick, never by two consecutive ticks, always a multiple of 100.
- *Confirmed*: it rises by 71,700 to 280,700 per tick (and by about 1.75 × 10⁹ from the zero-entry tick of the first
  load, 19,000, to the next one); the second zero-entry tick continues the game time.
- *Assumed* (no source states the unit; consistent with the recording): milliseconds of game time, running with the game speed, so a paused game
  sends no ticks.

## Island identity

- *Confirmed*: 24 keys (session GUID, island ID, area index) over 3 session GUIDs.
- *Confirmed*: the session ID byte maps one-to-one to the session GUID; the area index belongs to a key and never
  changes.
- *Confirmed*: the second load shows the same keys; every full tick lists its islands in the same order.
- *Confirmed*: a key occurs once in a tick.

## Edge cases

### Connect sequence

*Confirmed* (all four recordings): the version comes first, then the session start either comes at once or follows a
session end, so both orders occur. In the 90-minute recording of 2026-11-07 a session end follows 13 s later with no session start before it (the reader's
*repeated session end*, [`areas/pipe.md`](areas/pipe.md), *Session events*); the session start follows 93 s after that, and
the first island frame 4 s later. So a Companion can receive an end before it knows a start; whether the game sends it because of the connect or because of a player action is not visible.

### Zero-entry tick

*Confirmed*: the first tick after each session start has an entry count of 0 for every island (21 islands after the
first start, a subset of the 24 keys seen later; 24 after the second), and no other island is empty. The tick is the
one marked *no data yet*. Its timestamp is 19,000 after the first start and continues the game time after the second.

### Duplicate session end

*Corrected*: the third-party capture reports a second session end. It was **not observed**: the two ends of the
recording are each followed by a session start and never come in a row. The end after the connect is the only case of
an end without a start before it. The rule of the reader is unchanged.

### Rename

*Confirmed*: one island was renamed once during a session; its key did not change, so the island stays the same island
([`areas/pipe.md`](areas/pipe.md), *Island key*).

## Single reader

*Assumed*: one reader per pipe instance, as the reference does; whether a second client sees the same frames cannot
be recorded. How the Companion opens the pipe is in [`areas/pipe-connection.md`](areas/pipe-connection.md).

## Tick rules against the recording

33 ticks: 31 closed by the quiet period, 1 by a changed game time (the first zero-entry tick), 1 by the end of the
stream.

| Rule ([`areas/pipe.md`](areas/pipe.md)) | Recording | Status |
| --------------------------------------- | --------- | ------ |
| Island key | 24 keys, each once per tick; a rename keeps the key | matches |
| Closed by game time changed | 1 tick (the zero-entry tick of the first load) | matches |
| Closed by island repeated | no key repeats in a tick | not exercised |
| Closed by quiet period | 31 ticks, 30 s after the last island frame | matches |
| Closed by session event | both session ends follow an already closed tick | not exercised |
| Closed by stream ended | 1 tick (the last, at the end of the recording) | matches |
| No data yet | both zero-entry ticks, and no other tick | matches |
| Session events | the end without a start after the connect is delivered as a repeated end; each later end follows a start | matches |
| Tick limits | at most 24 islands and 1,934 entries and pairs in a tick | not exercised |

No rule mismatches the recording, so no follow-up issue is needed.

## Sizes

- *Confirmed*: the largest frame is 4,946 bytes (limit 1 MiB); at most 69 entries per island; at most 24 islands per
  tick (limit 256) and 1,934 entries and map pairs per tick (limit 524,288).
