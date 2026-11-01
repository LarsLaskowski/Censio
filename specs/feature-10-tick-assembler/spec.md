# Spec: Tick assembler and island identity

Source: Issue #10
Status: Draft

## Problem

The pipe reader of #9 ([`areas/pipe.md`](../../docs/areas/pipe.md)) delivers one message at a time, one island
per `AreaProductionStatistics` frame; the game sends about one such frame per second and a full tick roughly every
two minutes, with no end-of-tick marker. The later consumers — replay (#12), the tray (#13), the diagnostics window
(#14) and the contract's tick with receive time and game time (#27, record
[0022](../../docs/decisions/0022-chart-time-axis-real-or-game-time.md)) — need whole ticks, a stable island
identity and typed session events. Today each would have to guess where a tick ends, an island keyed by its island
ID alone collides with the same ID in another region, and the zero-entry frames after a load would read as "all
goods at zero". The consumer is the Companion's own code; nothing changes for a player before #11 opens the pipe.

## Scenarios

Island frames below are written `(session GUID, island ID, area index, name, timestamp, entry count)`. "Delivered"
means the assembler hands the item to its caller; every item is delivered in the order of the stream, a tick at the
point where it is closed. A frame's receive time is the time the reader returned it (live: the Companion's clock in
UTC; replay #12: the recorded time).

- S1: Normal tick: Given `Version 2` was read, when the frames `(G1, 1, 0, Ostia, T, 40)`,
  `(G1, 2, 0, Neapolis, T, 35)` and `(G1, 3, 0, Capua, T, 12)` arrive about a second apart and then a frame with
  timestamp `T2 ≠ T` arrives, then one tick is delivered before anything of `T2`: game time `T`, receive time that of
  the `Capua` frame, the three islands in arrival order, each with its key, session ID, name and entries exactly as
  the reader delivered them, close reason *game time changed*; the `T2` frame opens the next tick.
- S2: Quiet period: Given the tick of S1 is open after its `Capua` frame, when no further message arrives for
  30 seconds of receive time, then the tick is delivered with close reason *quiet period*, without waiting for the
  next tick two minutes later.
- S3: Pause: Given a tick with game time `T` was delivered and the game is paused, so its game time stands still,
  when the next tick's frames arrive with timestamp `T` again, then they form a new tick with game time `T`. When an
  island key already in the open tick arrives again before the quiet period ended (a recording replayed at full
  speed), the open tick is delivered with close reason *island repeated* and the repeated frame opens the next tick.
- S4: Load: Given a session is open, when `SessionEnd`, `SessionStart` with the headline `Mare Nostrum` and then
  frames with entry count `0` for every island arrive, then a *session end* event, a *session start* event with the
  headline `Mare Nostrum` and a tick in which every island is marked *no data yet* and the tick itself is marked
  *no data yet* are delivered; no island and no good carries a value of `0` for that tick.
- S5: Region switch: Given an open tick with `(G1, 1, 0, Ostia, T, 40)`, when `(G2, 1, 0, Londinium, T, 30)`
  arrives — the same island ID and area index in another region — then it joins the same tick as a second island;
  the tick is not closed as *island repeated*, and the two islands keep separate keys, names and entries.
- S6: Duplicate SessionEnd: Given a session is open, when `SessionEnd` arrives twice, then the first is delivered as
  *session end* and the second as *repeated session end*.
- S7: Connect: Given a new stream, when the game sends `Version 2`, `SessionEnd` and the frames of its last tick,
  then the version, a *repeated session end* (no session was open on this stream) and, after the quiet period, the
  tick are delivered; a tick is delivered whether or not a `SessionStart` was seen.
- S8: Names as received: Given frames named `R_mische K_ste` and `Zycada ` and a `SessionStart` headline with `_`,
  when they are delivered, then names and headline are byte-for-byte the reader's strings: no `_` turned into an
  umlaut, nothing trimmed.
- S9: Session event in a tick: Given an open tick, when `SessionStart` or `SessionEnd` arrives, then the open tick
  is delivered first, with close reason *session event*, and the event after it.

## Errors and edge cases

- E1: Skipped frame: a reader error that skips a frame (malformed message, unknown type, empty frame, protocol
  mismatch) is delivered unchanged in stream order, the open tick stays open, and the skipped island is absent from
  that tick — never present with zeros.
- E2: Stream ends: when the reader returns the end of the stream or a stream-ending error (truncated frame,
  oversized frame, read failed) while a tick is open, the tick is delivered with close reason *stream ended*, then
  the error, if any, then the end of the stream. Every later request returns the end of the stream.
- E3: Game time goes back: a frame whose timestamp is lower than the open tick's (an older save loaded without a
  session event) closes the tick with close reason *game time changed*, like any other change.
- E4: Late frame: a frame that arrives after a tick was closed by the quiet period, with that tick's timestamp,
  opens a new tick with the same game time; ticks are never reopened or merged.
- E5: Mixed zero entries: in a tick where some islands have entries and others have none, only the islands without
  entries are marked *no data yet*; the tick is marked *no data yet* only when all its islands are.
- E6: Tick limit: an island frame that would make the open tick exceed 256 islands or 16 MiB of island frames
  (counted as the length of each island frame) is dropped and delivered as a *tick limit* error that carries the
  island key and no name or entry; the open tick stays open and keeps its earlier islands.
- E7: Two starts: `SessionStart` while a session is open is delivered as a second *session start* with its own
  headline; no *session end* is invented. An empty headline is delivered as an empty string.
- E8: Rename: an island whose name differs from the previous tick's (renamed by the player) keeps its key; each
  tick carries the name from its own frame.
- E9: Cancellation: a cancelled request ends with the platform's cancellation instead of an item, as the reader
  does; the open tick is discarded and every later request returns the end of the stream.
- E10: Version mid-stream: a `Version 2` frame is delivered as the version and leaves an open tick open; a version
  other than `2` arrives from the reader as a protocol-mismatch error (E1), and the frames it skips leave the open
  tick to the quiet period.

## Non-functional requirements

- Memory: at most one open tick, bounded by E6; nothing else from earlier ticks is kept but the island keys of the
  open tick and whether a session is open.
- Determinism: which frames form a tick and why it closes depend only on the messages, their order and their
  receive times, never on when or how fast they are processed, so a replay (#12) of the same messages with the same
  receive times yields the same ticks.
- Time: on a live stream a tick is delivered at most about 30 seconds after its last frame even when nothing follows;
  the quiet period of 30 seconds and the limits of E6 are fixed values, documented in the area document, with no new
  configuration.
- Security: the pipe is untrusted input (`.squad/project.md`, *Parsing of external input*): limits are checked before
  a frame is added, no exception escapes for any message sequence, nothing is logged, and no error carries a name,
  headline or entry.
- Concurrency: one assembler serves one stream and one caller, like the reader; concurrent requests are not supported.
- Compatibility: the reader's behavior in `areas/pipe.md` is unchanged; `Censio.Pipe` stays platform-neutral and is
  tested on Linux with synthetic sequences only until the fixtures of #20 exist (record
  [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)).

## Acceptance criteria

- [ ] AC1: consecutive island frames with one timestamp form one tick with that game time, the receive time of its
  last frame and its islands in arrival order; a frame with a different timestamp, higher or lower, delivers the
  open tick with close reason *game time changed* and opens the next (S1, E3).
- [ ] AC2: an open tick is delivered with close reason *quiet period* once 30 seconds of receive time pass after its
  last frame without a further message, and not before; a frame with that tick's timestamp after this opens a new
  tick (S2, S7, E4).
- [ ] AC3: an island key already in the open tick delivers the open tick with close reason *island repeated* and opens
  the next tick with the repeated frame, also when the timestamp is unchanged (S3).
- [ ] AC4: the island key is session GUID, island ID and area index together: two frames that differ in the session
  GUID only, or in the area index only, are two islands of one tick and close nothing (S5).
- [ ] AC5: an island whose frame has no entries is marked *no data yet* and has no goods; a tick is marked *no data
  yet* exactly when all its islands are; no value `0` is produced for a missing good or island (S4, E5, E1).
- [ ] AC6: `SessionStart` is delivered as *session start* with its headline; `SessionEnd` as *session end* when a
  session was opened on this stream by a *session start* since the last end, otherwise as *repeated session end*
  (S4, S6, S7, E7).
- [ ] AC7: a `SessionStart` or `SessionEnd` delivers the open tick first with close reason *session event* (S9).
- [ ] AC8: island names and headlines are delivered byte-for-byte as the reader decoded them, `_` and trailing spaces
  included, and a renamed island keeps its key (S8, E7, E8).
- [ ] AC9: reader errors and `Version` messages are delivered unchanged in stream order and leave the open tick open;
  the end of the stream or a stream-ending error delivers the open tick with close reason *stream ended* before the
  error and the end, and every later request returns the end (E1, E2, E10).
- [ ] AC10: the frame that would make a tick exceed 256 islands or 16 MiB of island frames is dropped and delivered as
  a *tick limit* error with its island key and no name or entry; the tick keeps its earlier islands (E6).
- [ ] AC11: a cancelled request ends in cancellation, discards the open tick, and every later request returns the end
  of the stream (E9).
- [ ] AC12: the same messages with the same receive times, fed twice — once with pauses between them, once without —
  give the same ticks, close reasons and events (determinism requirement).

## Out of scope

- Opening the pipe, reconnecting and connection states (#11); recording and replay (#12).
- Telling apart a tick sent again after a reconnect from a new one, or a paused tick from a running one: the ticks
  are delivered as they come; the consumers that store them (#19, #35) decide.
- Which savegame a tick belongs to (#19, record [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md)).
- Repairing names (`_` back to umlauts) or mapping GUIDs to names (#23 ff., record
  [0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md)).
- Converting the game's timestamp into a unit or a date: it is carried as the reader's 64-bit value.
- The JSON contract (#27) and the tick buffer (#41).

## Open questions

- Which rule closes a tick, given that there is no end-of-tick marker? Answered: four rules, all needed, each named as
  the tick's close reason (AC1–AC3, AC7, AC9) — a timestamp change (the issue's observation that all frames of a tick
  share one), a repeated island key (a paused game repeats the timestamp; a replay at full speed has no quiet gap), a
  session event, and the end of the stream; plus the quiet period of 30 seconds, without which the last tick of a
  run, including the one the game sends on connect, would wait two minutes for the next one. 30 seconds lies far above
  the frame spacing of about one second and far below the tick spacing of about two minutes; both spacings are
  observations of others, and the quiet period is revisited with the recordings of #17.
- Does the session ID byte belong to the island key? Answered: no; the issue fixes the key as session GUID, island ID
  and area index, and the session ID is carried with each island unchanged (`AreaProductionStatisticsMessage` has
  both fields).
- Do the frames of two regions share one timestamp within a tick? Answered: unknown until #17; both cases are handled
  — with a shared timestamp the regions form one tick (S5), with separate timestamps the timestamp rule splits them
  into one tick per region, and in neither case do two islands of the same ID merge.
- What is a `SessionEnd` that arrives with no session open, as on connect? Answered: a *repeated session end*
  (AC6) — it ends nothing this stream saw; ticks are delivered regardless of the session state (S7).
- Does the README or user guide change, as the issue's *Documentation* section asks? Answered: no; nothing a player
  or tester can try changes before the Companion opens the pipe (#11), the README's *Status* ("No game data is read")
  stays true, and #9 (PR #87) set the precedent of documenting the pipe library in `areas/pipe.md` only.
- Do the issue's claims hold? Answered: island names arrive untrimmed with `_` kept, and a session start carries a
  headline string — confirmed (`PipeMessageParser`, `SessionStartMessage`, `areas/pipe.md`); the frame carries
  session GUID, island ID and area index — confirmed (`AreaProductionStatisticsMessage`). The timing, the shared
  timestamp, the zero-entry tick after a load, the double `SessionEnd` and the connect sequence are third-party
  observations the code cannot confirm; the behavior above holds whether or not they are true.

## Area documents

- `docs/areas/pipe.md`: a new section *Ticks and sessions* — the island key; the close rules with their close
  reasons and the quiet period of 30 seconds; the receive time and game time of a tick; *no data yet* for islands
  and ticks; the session events and when an end counts as repeated; the order of delivery; the limits of 256 islands
  and 16 MiB per tick and the *tick limit* error; cancellation and end of stream. Its *Scope* gains ticks, and the
  *Not here* entry "Assembling ticks from the messages (#10)" is removed; the residual risks gain the unconfirmed
  timing and the unverified real island count.
- `docs/areas/README.md`: the pipe row's *Scope* names ticks and session events, and its *Not here* drops "ticks".
