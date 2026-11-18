# 0094: The pipe reader accepts protocol versions 2 and 3 and skips trade routes and item offers undecoded

- **Status:** Proposed
- **Date:** 2026-11-18
- **Area:** pipe
- **Source:** Issue #125
- **Supersedes:** 0033 (amends its version gate: versions 2 and 3 are decoded, not only 2); 0042 (amends what the
  anonymiser refuses: a trade-route or item-offer frame is written emptied instead of refused)

## Context

After the game update to build `26.39954.4.58957` (main menu `3.0.1.1743890`) the Companion shows `Protocol mismatch`;
its log reads `received version 3`. The same update confirms the open point of record
[0046](0046-game-build-is-the-program-files-version.md): the program file's version changed with the main-menu
version ([game-installation](../areas/game-installation.md), *Build*).

Ubisoft's reference reader (`UbisoftMainzAnno/anno117_pipe_example`, commit `4cc8ad78d63b33855b4b35dd002380fc466e4b1d`,
`src/pipe.h` and `src/pipe.cpp`, read on 2026-11-18) now names protocol version 3 and two new message types: 4
(trade routes: a count, then per route an ID, name and folder strings, problem and ship counts and its stations with
their goods) and 5 (item offers: a count, then per trader an ID and a list of item GUIDs). It reads versions, session
events and island frames field for field as our reader decodes them for version 2, and keeps its 1 MiB buffer.
The Product Manager chose to build from the reference with synthetic frames (issue #125, step 2); a raw version 3
recording the user then supplied (kept private) confirms that frames of types 4 and 5 start with a count and stay far
within 1 MiB, and is read against the new reader in the same pull request. Trade routes carry text the player types (route and folder
names), and nothing in Censio uses either new type yet.

## Options considered

1. **Accept 2 and 3, skip types 4 and 5 undecoded** (chosen) — the Companion works again with the island data it
   already understands; the new types change no state and their content is never decoded, held or logged. Con: the
   island layout under version 3 rests on the reference and one real recording.
2. **Accept only 3** — matches the reference's single constant, but a game that has not updated (or a replay of an
   older recording) would show `Protocol mismatch`; nothing in version 3 changes the frames of version 2.
3. **Decode trade routes and item offers now** — the reference gives the layout, but no feature uses them, the strings
   need anonymising rules and the nested counts their own limits; it is a feature of its own (follow-up issue).
4. **Leave types 4 and 5 unknown** — no code for them, but every such frame would be an unknown-type error, and the
   anonymiser would refuse every version 3 recording, so no fixture could be made from one.
5. **Anonymiser decodes and rewrites types 4 and 5 or drops them** — rewriting needs the decoder of option 3; dropping
   breaks the rule that the line count and frame types are kept. Writing each with an empty list keeps both without
   decoding.

## Decision

The reader decodes frames under protocol version 2 or 3 alike; a frame of type 4 or 5 is checked for its leading
count only and delivered as an undecoded message that changes no tick, session or connection state, and the
anonymiser writes it as its type with a count of 0. The rules are in [pipe](../areas/pipe.md) and
[pipe-recording](../areas/pipe-recording.md), *Anonymising*.

## Consequences

- One real recording backs the version 3 island layout (a field added at the end is ignored; one inserted earlier is
  misread or rejected as malformed). The reference reads a missing count as 0 and a negative one as no entries; this
  reader rejects both, so a game that ever sends such a frame has it skipped.
- A malformed trade-route or item-offer frame is a skipped frame without effect, but the anonymiser refuses a
  recording that holds one.
- Fixtures made from version 3 recordings hold no trade routes or item offers; the feature that decodes them extends
  the anonymiser.
- The types are accepted under version 2 too: the parser has no version, and a version 2 game does not send them.
