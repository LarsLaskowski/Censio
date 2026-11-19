# Spec: Decode trade routes and item offers of pipe protocol 3

Source: Issue #221
Status: Draft

## Problem

Since protocol version 3 (game build `26.39954.4.58957`) the game sends two message types the Companion cannot read:
type 4, the player's trade routes, and type 5, the item offers of traders. The reader checks only their leading count
and delivers them as undecoded messages that change nothing ([record 0094](../../docs/decisions/0094-pipe-reader-accepts-protocol-versions-2-and-3-and-skips-trade-routes-and-item-offers.md),
[pipe](../../docs/areas/pipe.md), *Message types*); the anonymiser writes each as its type byte and a count of 0
([pipe-recording](../../docs/areas/pipe-recording.md), *Anonymising*). So nothing in Censio can show a route with
problems or lost ships, and no fixture holds real trade-route content to test a reader against. Route and folder
names are text the player types, so they must never reach a committed fixture or a log.

## Scenarios

- S1: Given a connection of protocol version 3, when a trade-routes frame arrives, then the reader delivers a
  trade-routes message: per route, in frame order, its ID, name, folder, major and minor problem counts, ship and
  lost-ship counts, and its stations; per station its ID, the island it calls at (session ID, island ID, area index,
  session GUID), its owner, the flags *throw overboard*, *wait until full* and *wait until empty*, and its goods; per
  good its GUID, amount and whether it is loaded or unloaded.
- S2: Given the same connection, when an item-offers frame arrives, then the reader delivers an item-offers message:
  per trader, in frame order, its ID and the GUIDs of the items it offers, in frame order.
- S3: Given an open tick, when a trade-routes or item-offers message arrives, then the tick, the quiet period, the
  session events and the connection state are as they are today: the messages produce no tick item, and the
  Companion sends, shows and logs nothing of them.
- S4: Given a raw version 3 recording with trade routes and item offers, when a contributor runs
  `Censio.Anonymiser <recording> <output>`, then every route and item-offer frame is written with its decoded
  structure: route names become `Route <n>`, folder names `Folder <n>`, the integer amounts change, and IDs, GUIDs,
  island keys, flags, list lengths and order are kept; a replay of the output gives the same ticks and session events as
  before the feature.
- S5: Given the real version 3 recording, when it has been anonymised, then a committed fixture holds its trade routes
  and item offers decoded, and the reader's tests read it.

## Errors and edge cases

- E1: A count of 0 at any level (routes, stations, goods, traders, items) is valid: the list is delivered empty. The
  emptied frames of the existing fixture `protocol-3-session.jsonl` decode as a message with no routes or traders.
- E2: A count that is missing (fewer than 4 bytes left) or negative, at any level, makes the frame a malformed message:
  it is skipped, and no message carries a `0` or an empty list in its place. The reference reads a missing count as 0,
  loops zero times on a negative top-level count, and on a negative nested count fails (its `resize` takes the count
  as a huge size and the program exits); this reader rejects all of them (record 0033).
- E3: A count above its cap, or above what the rest of the frame can hold, is a malformed message, found before
  anything of that size is allocated.
- E4: A field past the end of the frame (a station's three key bytes, a flags byte, a good's loading byte, a string's
  bytes) is a malformed message; the reference returns early and keeps defaults instead.
- E5: Flag bits the reference does not name (bits 3 to 7 of a station's flags, 1 to 7 of a good's) are ignored; the
  named bits are delivered.
- E6: A route ID, trader ID, station ID or item GUID that occurs twice is delivered twice, in its place; nothing is
  merged or dropped.
- E7: Route and folder names follow the string rules of [pipe](../../docs/areas/pipe.md), *Frame layout* (UTF-8 with
  U+FFFD, not trimmed, empty allowed). An empty folder is delivered as an empty text.
- E8: Bytes after the last field of a trade-routes or item-offers frame are ignored; the message is delivered.
- E9: A station's island key is delivered as read, whether or not an island frame with that key was seen.
- E10: Types 4 and 5 are decoded under protocol version 2 as well, as today: the parser has no version.
- E11: The anonymiser refuses a malformed trade-routes or item-offers frame with `frame cannot be anonymised in line
  <n>`, as it does today for a missing or negative leading count.
- E12: An invented name is never equal to an original name of the file of any kind (figure, island, route, folder),
  ignoring case; one original name keeps one invented name per kind, and an empty name stays empty.

## Non-functional requirements

- N1: Memory: a read still holds at most one frame (1 MiB) plus its decoded message. Each count has a cap with a wide
  margin over the version 3 recording (57 routes, 7 traders per frame); the caps' values are set in the plan.
- N2: Time: decoding a frame of up to 1 MiB of any shape takes time proportional to its size; no count can make the
  reader do work that the frame's bytes do not pay for.
- N3: Privacy: route and folder names and every other decoded field of types 4 and 5 are never logged and never part of
  an error; an error keeps its kind, type value and offset only. No original route or folder name and no original
  amount of these frames reaches the anonymiser's output.
- N4: Compatibility: the recording format and its version are unchanged (recordings hold raw frames, record 0036);
  existing recordings and both fixtures replay as before; frames of types 0 to 3 decode as before.

## Acceptance criteria

- AC1: A trade-routes frame built from the reference layout is delivered with every field of S1 in frame order, a
  repeated ID kept in its place, names under the string rules and trailing bytes ignored (S1, E6, E7, E8, E9, E10).
- AC2: An item-offers frame built from the reference layout is delivered with every trader and item of S2 in frame
  order, repeats kept (S2, E6, E8).
- AC3: A count of 0 at each of the five levels gives an empty list, and the existing fixture's emptied frames decode
  as messages without routes or traders (E1).
- AC4: A missing or negative count at each of the five levels, a short field of E4 and a string past the frame each
  give a malformed-message error with the type value and offset and no message (E2, E4).
- AC5: A count one above its cap, and a count the rest of the frame cannot hold, at each level, give a
  malformed-message error before any collection of that size is created; a count at its cap is delivered (E3, N1, N2).
- AC6: Unnamed flag bits set to 1 change nothing but the named flags they sit beside, which are delivered as set (E5).
- AC7: Trade-routes and item-offers messages produce no tick item, do not close a tick, do not restart the quiet period
  and do not change whether a following session end is a repeated one (S3).
- AC8: The anonymiser writes route names as `Route <n>` and folder names as `Folder <n>` under the numbering rules of
  island and figure names, never equal to any original name of the file ignoring case (S4, E12, N3).
- AC9: The anonymiser keeps IDs, GUIDs, island keys, flags, list lengths and order of types 4 and 5, changes problem
  counts, ship counts, lost-ship counts and goods amounts by the integer rule of maintenance, and each written frame
  parses back to that structure, else it is refused (S4, N3).
- AC10: The anonymiser refuses a malformed trade-routes or item-offers frame with `frame cannot be anonymised in line
  <n>` (E11).
- AC11: A fixture anonymised from the real version 3 recording holds its trade-route and item-offer frames decoded,
  the reader delivers every one of them without an error, and a replay of it gives the ticks and session events of
  the original (S5, N4).

## Out of scope

- A history of trade routes or item offers over time.
- Using the decoded data in the Companion or the server: no contract message, server state, page or diagnostics
  column shows it (the Product Manager's answer to Q1); showing it is a follow-up feature issue.
- Interpreting the owner value or the problem counts beyond delivering them as numbers.
- Changing what a raw recording holds: it keeps the raw frames, as for figure and island names (record 0036).

## Open questions

- Q1: How do the Companion or the server use the decoded data (the issue's "decide how the Companion or server uses
  the data")? Answered: the Product Manager chose the pipe layer only — types 4 and 5 are decoded with caps, the anonymiser
  is extended and a real fixture is made; the Companion changes no state with them and nothing downstream uses them.
  Showing the data is a follow-up feature issue.
- Q2: Which real sample is the reader built from? The version 3 recording of #125 is kept private and is not in the
  session; the plan needs it supplied outside the repository (`.squad/routing.md`, *Escalation to the Product
  Manager*) for AC11 and to confirm the nested counts. Answered: the Product Manager supplies the
  real version 3 recording outside the repository before the plan is written; the plan does not start without it.
- Q3: Does the game ever send a missing or negative count? Answered: none was seen at the top level — the version 3
  recording's 5 trade-route frames (count 57) and 4 item-offer frames (count 7) all start with a positive count
  ([`../../docs/protocol.md`](../../docs/protocol.md), *TradeRoutes and ItemOffers*); its nested counts are unknown
  until the plan decodes it. The reference itself cannot survive a negative nested count (E2), so the reader keeps
  rejecting both (record 0033).
- Q4: Must a recording anonymise route names "before any recording keeps them"? Answered: no — a Companion recording
  holds the raw frames and stays private, as it does for figure and island names (records 0029, 0036); names are
  replaced by the anonymiser before a recording becomes a fixture (record 0042).
- Q5: What do *Errors* and *Warnings* mean? Answered: the reference prints "major problems" for Errors above 0 and
  "minor problems" for Warnings above 0; they are delivered as the major and minor problem counts, uninterpreted.

## Area documents

- `docs/areas/pipe.md`: the layout of types 4 and 5 in *Message types*; their caps and malformed cases in *Limits and
  errors*; the missing and negative counts, short fields and nested negative counts in *Deliberate differences from
  the reference*; the residual risks updated.
- `docs/areas/pipe-recording.md`: route and folder names, integer amounts and the kept fields of types 4 and 5 in
  *Anonymising*; the new fixture in *Fixtures*.
- `docs/protocol.md`: what the real recording confirms of types 4 and 5 (counts, sizes, no missing or negative
  count), derived facts only.
- `docs/areas/pipe.md`, *Ticks and sessions*: the rule that a type 4 or 5 message changes no tick, quiet period or
  session state, now worded for the decoded messages instead of "an undecoded message".
- `tests/Censio.Pipe.Tests/Fixtures/README.md` (no area document, but where a fixture is described): how the new
  fixture was made and which facts of types 4 and 5 it shows.
- No Companion, contract, server or web area document changes (`companion.md`, `companion-diagnostics.md`,
  `contract.md`, `server-state.md`, `web-pages.md`): nothing downstream uses the data (Q1).
