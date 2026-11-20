# 0095: Trade routes and item offers are decoded strictly with caps and anonymised with invented names

- **Status:** Accepted
- **Date:** 2026-11-20
- **Area:** pipe
- **Source:** Issue #221
- **Supersedes:** 0042 (amends what the anonymiser rewrites: trade-route and item-offer frames are written decoded, with
  invented route and folder names and changed integer amounts)

## Context

Protocol version 3 adds message types 4 (trade routes) and 5 (item offers). Until #221 the reader checked only their
leading count and skipped the rest, and the anonymiser wrote each as its type and a count of 0, so no fixture held
their content. Their layout is known from Ubisoft's reference reader (`UbisoftMainzAnno/anno117_pipe_example`, commit
`4cc8ad78d63b33855b4b35dd002380fc466e4b1d`): five nested counts (routes, stations, goods; traders, items), two strings
the player types (route name and folder) and flag bytes. The reference reads leniently: a missing field reads as 0, a
negative top-level count loops zero times, a negative nested count makes its `resize` throw and the program exit, a
repeated trader keeps only its last offer, and routes are grouped by folder without their ID. Two real version 3
recordings, kept private, confirm the layout ([`../protocol.md`](../protocol.md), *TradeRoutes and ItemOffers*). The
Product Manager chose to decode the types in `Censio.Pipe` only; nothing in the Companion or the server uses them yet
(issue #221, step 2).

## Options considered

1. **Decode strictly into ordered lists, with a cap per count** (chosen) — the same fail-closed rules as the island
   frames (record [0033](0033-pipe-reader-strict-from-the-reference-source.md)): a missing or negative count, a short
   field or a count beyond its cap or the bytes left rejects the frame. The caps are not needed for memory, which the
   bytes-left check already bounds, but make an implausible frame fail like an oversized map does. Con: a frame the
   reference half-reads is dropped whole.
2. **Keep skipping them** — no code and no risk, but no fixture with their content and nothing for a later feature to
   build on.
3. **Read as the reference does** — the same results for the frames the game sends, but zeros for a short frame;
   contradicts record 0033, and the reference itself cannot survive a negative nested count.
4. **Deliver the reference's shapes** (offers keyed by trader, routes keyed by folder) — loses order, repeated entries
   and the route ID, and a keyed collection over a count needs its own collision cap. Ordered lists keep everything
   and leave grouping to a consumer.
5. **Reject unnamed flag bits** — would drop every route frame of a game that adds a flag, though the bits carry nothing
   the reader delivers; they are ignored on reading and written as 0 by the anonymiser.
6. **Anonymiser replaces only the names** — keeps exact ship counts and goods amounts in public, against record
   [0042](0042-recordings-anonymised-by-a-contributor-tool-rewriting-frames.md)'s goal of no exact values; the integer
   rule of maintenance changes them instead, with lost ships kept at most ships and a negative goods amount kept.
7. **Anonymiser keeps emptying them** — no fixture could show a real trade route.

## Decision

The parser decodes types 4 and 5 into trade-routes and item-offers messages under the rules and caps of
[pipe](../areas/pipe.md) (*Message types*, *Limits and errors*); the messages change no state. The anonymiser writes
them from their decoded fields with invented route and folder names and changed integer amounts, checks each written
frame by parsing it back, and refuses any written frame over 1 MiB ([pipe-recording](../areas/pipe-recording.md),
*Anonymising*).

## Consequences

- A game update that changes the layout of types 4 and 5 without a new protocol version has these frames rejected as
  malformed or misread; the island data is not affected.
- The meanings of the problem counts, the owner and the traders rest on the reference's names; a feature that shows
  them (a follow-up issue of #221) checks them against the game first.
- Fixtures made by this anonymiser hold trade routes and item offers; the older version 3 fixture keeps its emptied
  frames, which decode as empty messages.
- A cap a future game exceeds rejects that frame until the cap is raised.
- Amounts are drawn afresh per frame, as for island frames. The copies of a repeated trade-route frame therefore let a
  reader infer a small original value (2 to 4 move by exactly 1, and 1 always becomes 2). This is accepted, like the
  factor an entry reveals under record 0042: the goal is no exact value in public, not the secrecy of an amount. A
  per-run mapping would need state keyed by positions that shift when a route is added.
