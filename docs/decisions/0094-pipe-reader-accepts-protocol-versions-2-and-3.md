# 0094: The pipe reader accepts protocol versions 2 and 3

- **Status:** Accepted
- **Date:** 2026-11-18
- **Area:** pipe
- **Source:** Issue #125
- **Supersedes:** 0033 (amends its version gate: versions 2 and 3 are decoded, not only 2)

## Context

After the game update to build `26.39954.4.58957` (main menu `3.0.1.1743890`) the Companion shows `Protocol mismatch`;
its log reads `received version 3`. The same update confirms the open point of record
[0046](0046-game-build-is-the-program-files-version.md): the program file's version changed with the main-menu
version ([game-installation](../areas/game-installation.md), *Build*).

Ubisoft's reference reader (`UbisoftMainzAnno/anno117_pipe_example`, commit `4cc8ad78d63b33855b4b35dd002380fc466e4b1d`,
`src/pipe.h` and `src/pipe.cpp`, read on 2026-11-18) now names protocol version 3 and two new message types, 4 (trade
routes) and 5 (item offers). It reads versions, session events and island frames field for field as our reader decodes
them for version 2, and keeps its 1 MiB buffer. The Product Manager chose to build from the reference with synthetic
frames (issue #125, step 2); a raw version 3 recording the user then supplied (kept private) is read against the new
reader in the same pull request. How the two new types are read is a decision of its own, record
[0095](0095-trade-routes-and-item-offers-decoded-strictly-and-anonymised-with-invented-names.md).

## Options considered

1. **Accept 2 and 3** (chosen) — the Companion works again with the island data it already understands. Con: the
   island layout under version 3 rests on the reference and one real recording.
2. **Accept only 3** — matches the reference's single constant, but a game that has not updated (or a replay of an
   older recording) would show `Protocol mismatch`; nothing in version 3 changes the frames of version 2.

## Decision

The reader decodes frames under protocol version 2 or 3 alike, and a switch between them on one stream is no
mismatch. The rules are in [pipe](../areas/pipe.md), *Version gate*.

## Consequences

- One real recording backs the version 3 island layout (a field added at the end is ignored; one inserted earlier is
  misread or rejected as malformed).
- The parser has no version: the types of version 3 are accepted under version 2 too, and a version 2 game does not
  send them.
