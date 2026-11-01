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
goods at zero". Nothing changes for a player before #11 opens the pipe.

## Scenarios

Island frames are written `(session GUID, island ID, area index, name, timestamp, entry count)`. "Delivered" means
the assembler hands the item to its caller, in stream order, a tick at the point where it is closed. All time comes
from one clock the caller injects: a frame's receive time is that clock's time when the read returned it, and the
quiet period runs on the same clock (live: the system clock in UTC; replay #12: a clock that follows the recorded
times).

- S1: Normal tick: Given `Version 2` was read, when `(G1, 1, 0, Ostia, T, 40)`, `(G1, 2, 0, Neapolis, T, 35)` and
  `(G1, 3, 0, Capua, T, 12)` arrive about a second apart and then a frame with timestamp `T2 ≠ T`, then one tick is
  delivered before anything of `T2`: game time `T`, receive time that of `Capua`, the three islands in arrival
  order with key, session ID, name and entries as the reader delivered them, close reason *game time changed*.
- S2: Quiet period: Given the tick of S1 is open after `Capua` and the next read is still pending, when the clock
  reaches 30 seconds after `Capua`'s receive time, then the tick is delivered with close reason *quiet period*; the
  pending read is not cancelled, and its frame is delivered on a later request.
- S3: Pause: Given a tick with game time `T` was delivered and the game is paused, when the next tick's frames arrive
  with timestamp `T` again, then they form a new tick with game time `T`. As a safety net for a source whose ticks
  follow each other within 30 seconds, an island key already in the open tick closes it with close reason *island
  repeated*, and the repeated frame opens the next tick.
- S4: Load: Given a session is open, when `SessionEnd`, `SessionStart` with the headline `Mare Nostrum` and frames
  with entry count `0` for every island arrive, then *session end*, *session start* with `Mare Nostrum`, and a tick
  marked *no data yet* whose islands are each marked *no entries* are delivered; no good carries a value of `0`.
- S5: Region switch: Given an open tick with `(G1, 1, 0, Ostia, T, 40)`, when `(G2, 1, 0, Londinium, T, 30)` arrives,
  then it joins the same tick as a second island with its own key, name and entries; nothing is closed.
- S6: Duplicate SessionEnd: Given a session is open, when `SessionEnd` arrives twice, then the first is delivered as
  *session end* and the second as *repeated session end*.
- S7: Connect: Given a new stream, when the game sends `Version 2`, `SessionEnd` and the frames of its last tick,
  then the version, a *repeated session end* (nothing came before it to end), and after the quiet period the tick
  are delivered. When that session later ends with `SessionEnd`, it is delivered as *session end*.
- S8: Names as received: Given frames named `R_mische K_ste` and `Zycada ` and a headline with `_`, when they are
  delivered, then names and headline are the reader's strings unchanged: no `_` turned into an umlaut, nothing
  trimmed.
- S9: Session event in a tick: Given an open tick, when `SessionStart` or `SessionEnd` arrives, then the tick is
  delivered first with close reason *session event*, the event after it.

## Errors and edge cases

- E1: Skipped frame: a reader error that skips a frame (malformed, unknown type, empty, protocol mismatch) is
  delivered unchanged in stream order; the open tick stays open and lacks that island — never present with zeros.
- E2: Stream ends: on the end of the stream or a stream-ending error (truncated, oversized, read failed) while a
  tick is open, the tick is delivered with close reason *stream ended*, then the error if any, then the end; every
  later request returns the end.
- E3: Game time goes back: a lower timestamp than the open tick's closes it as *game time changed*.
- E4: Late frame: a frame with the timestamp of a tick already closed by the quiet period opens a new tick with the
  same game time; ticks are never reopened or merged. A game that stalls over 30 seconds mid-tick thus yields two
  partial ticks with one game time.
- E5: Mixed: in a tick where some islands have entries and others none, the empty ones are marked *no entries* (which
  can also mean an island without production) and the tick is not *no data yet*.
- E6: Tick limit: an island frame that would make the open tick exceed 256 islands, or 524,288 entries and map pairs
  counted together, is dropped and delivered as a *tick limit* item of the assembler — not a reader error — carrying
  the island key only. The key is not added to the open tick, which stays open with its earlier islands; every
  further frame over the limit gives its own *tick limit* item.
- E7: Two starts: `SessionStart` while a session is open is a second *session start* with its own headline; no end is
  invented. An empty headline is delivered as an empty string.
- E8: Rename: an island whose name differs from the previous tick's keeps its key; each tick carries its frame's name.
- E9: Cancellation: a cancelled request ends in the platform's cancellation, as the reader does; the open tick is
  discarded and every later request returns the end.
- E10: Non-island items: `Version` messages and reader errors leave the open tick open and do not restart its quiet
  period, which runs from the receive time of its last island frame only.

## Non-functional requirements

- Memory: at most one open tick, bounded by E6; besides it only the open tick's island keys and the session state are
  kept. The plan measures and the area document records the memory bound E6 gives.
- Determinism: the ticks, close reasons and events depend only on the messages, their order and the injected clock,
  never on how fast they are processed, so a replay (#12) whose clock follows the recorded times yields the live ticks
  — except that a replay ending at its last frame closes that tick as *stream ended* where live may have closed it by
  quiet period.
- Time: on a live stream a tick is delivered 30 seconds after its last island frame even when nothing follows; the
  quiet period and the limits of E6 are fixed values documented in the area document; no new configuration.
- Security: the pipe is untrusted input (`.squad/project.md`, *Parsing of external input*): limits are checked before
  a frame is added, no exception escapes for any message sequence, nothing is logged, and no error or *tick limit*
  item carries a name, headline or entry.
- Concurrency: one assembler serves one stream and one caller, like the reader.
- Compatibility: the reader's behavior and error kinds in `areas/pipe.md` are unchanged; `Censio.Pipe` stays
  platform-neutral, tested on Linux with synthetic sequences until the fixtures of #20 exist (record
  [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)).

## Acceptance criteria

- [ ] AC1: consecutive island frames with one timestamp form one tick with that game time, the receive time of its
  last frame and its islands in arrival order; a frame with a different timestamp, higher or lower, delivers the
  open tick with close reason *game time changed* and opens the next (S1, E3).
- [ ] AC2: with a read that never completes, advancing the injected clock to 30 seconds after the open tick's last
  island frame delivers the tick with close reason *quiet period*; at 29.999 seconds nothing is delivered, and a
  later request still gets the frame of the original read; a frame with the closed tick's timestamp then opens a new
  tick (S2, S7, E4).
- [ ] AC3: an island key already in the open tick delivers it with close reason *island repeated* and opens the next
  tick with the repeated frame, also when the timestamp is unchanged (S3).
- [ ] AC4: the island key is session GUID, island ID and area index together: two frames that differ in the session
  GUID only, or in the area index only, are two islands of one tick and close nothing (S5).
- [ ] AC5: an island whose frame has no entries is marked *no entries* and has no goods; a tick is marked *no data
  yet* exactly when all its islands are; no value `0` is produced for a missing good or island (S4, E5, E1).
- [ ] AC6: `SessionStart` is delivered as *session start* with its headline; `SessionEnd` is delivered as *repeated
  session end* when neither an island frame nor a `SessionStart` came since the previous `SessionEnd` or since the
  stream began, otherwise as *session end* (S4, S6, S7, E7).
- [ ] AC7: a `SessionStart` or `SessionEnd` delivers the open tick first with close reason *session event* (S9).
- [ ] AC8: island names and headlines are delivered exactly as the reader decoded them, `_` and trailing spaces
  included, and a renamed island keeps its key (S8, E7, E8).
- [ ] AC9: reader errors and `Version` messages are delivered unchanged in stream order and leave the open tick open;
  the end of the stream or a stream-ending error delivers the open tick with close reason *stream ended* before the
  error and the end, and every later request returns the end (E1, E2).
- [ ] AC10: the frame that would make a tick exceed 256 islands or 524,288 entries and map pairs is dropped and
  delivered as a *tick limit* item with its island key only; the tick keeps its earlier islands, the key does not
  count for *island repeated*, and each further frame over the limit gives its own item (E6).
- [ ] AC11: a cancelled request ends in cancellation, discards the open tick, and every later request returns the end
  (E9).
- [ ] AC12: the same messages with the same injected receive times give the same ticks, close reasons and events
  whether the caller requests them at once or with pauses in processing time between requests (determinism).
- [ ] AC13: with an open tick, a reader error or `Version` every second for 31 seconds after its last island frame
  still delivers the tick with close reason *quiet period* at 30 seconds (E10).

## Out of scope

- Opening the pipe, reconnecting and connection states (#11); recording and replay (#12).
- Telling a resent or paused tick from a new one: the storing consumers (#19, #35) decide.
- Which savegame a tick belongs to (#19, record [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md)).
- Repairing names or mapping GUIDs to names (record [0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md)).
- Converting the game's timestamp into a unit or a date: it is carried as the reader's 64-bit value.
- The JSON contract (#27) and the tick buffer (#41).

## Open questions

- Which rule closes a tick, without an end-of-tick marker? Answered: the rules of AC1–AC3, AC7 and AC9, each the
  tick's close reason; the quiet period keeps the last tick of a run (also the one sent on connect) from waiting two
  minutes. 30 seconds lies far above the frame spacing (about one second) and far below the tick spacing (about two
  minutes), both observations of others, revisited with the recordings of #17.
- Does the session ID byte belong to the island key? Answered: no; the issue fixes the key, and the session ID is
  carried with each island (`AreaProductionStatisticsMessage` has both fields).
- Do two regions share one timestamp within a tick? Answered: unknown until #17; with a shared timestamp they form
  one tick (S5), otherwise one tick per region; in neither case do islands of the same ID merge.
- How is the `SessionEnd` sent on connect classified? Answered: *repeated session end*, since nothing came before it
  on this stream; the later real end follows island frames and is a *session end* (AC6, S7).
- Do README and user guide change, as the issue asks? Answered: no; nothing a tester can try changes before #11 opens
  the pipe, the README's *Status* line "No game data is read" stays true, and record
  [0013](../../docs/decisions/0013-readme-quick-start-user-guide-reference.md) makes the README the only home of
  *Status*. The PR description states that the user documentation follows with #11 and #13.
- Do the issue's claims hold? Answered: untrimmed names with `_`, the headline and the three key fields are confirmed
  in `Censio.Pipe`; the timing, the shared timestamp, the zero-entry tick, the double end and the connect sequence are
  third-party observations, and the behavior holds either way.

## Area documents

- `docs/areas/pipe.md`: a new section *Ticks and sessions* with the behavior of this spec — key, close rules and
  the clock, receive and game time, *no entries* (which can also mean no production) and *no data yet*, session
  events, delivery order, the limits with the measured memory bound, cancellation and end of stream. *Scope* gains
  ticks, the *Not here* entry for #10 goes. Residual risks gain the unconfirmed timing and island count, the stall
  split of E4, a game sending one island twice in a tick (split as *island repeated*), and a replay's last tick
  closed as *stream ended*.
- `docs/areas/README.md`: the pipe row's *Scope* names ticks and session events, and its *Not here* drops "ticks".

## Challenge

- Major 1 (time source): accepted — one injected clock gives receive times and runs the quiet period, which never
  cancels the pending read (S2, AC2); AC12 pauses in processing time with fixed receive times.
- Major 2 (repeated end): accepted — rule and connect case rewritten (S6, S7, AC6).
- Minor 3 (frame lengths): accepted — the limit counts entries and map pairs (E6); the plan measures the memory bound.
- Minor 4 (limit type): accepted — an assembler item with the key only (E6, AC10).
- Minor 5 (quiet reset): accepted — only island frames restart it (E10, AC13).
- Minor 6 (*no data yet*): accepted — *no entries* per island, *no data yet* per tick only (S4, E5, AC5).
- Minor 7 (repeated rationale): accepted — a safety net (S3), its split a residual risk.
- Minor 8 (README grounds): accepted — README *Status* and record 0013; the risks go to `areas/pipe.md`.
