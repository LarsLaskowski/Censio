# 0043: RDA archives are read by an own bounded reader for format V2.2

- **Status:** Proposed
- **Date:** 2026-11-08
- **Area:** game-files
- **Source:** Issue #21
- **Supersedes:** 0024 — amends it: the spike found no library that fits, so its option 2 applies; and the spike's
  result stands here and the file locations in the [game-files](../areas/game-files.md) area document, not in 0024
  itself as 0024 says (0024 is released and stays unedited).

## Context

[0024](0024-rda-reading-with-an-existing-nuget-library.md) chose a NuGet library for the game's RDA archives if the
spike of #21 found one with a permissive licence ([0028](0028-mit-licence.md)), maintenance and Anno 117 support. The
Product Manager's listing of an installation found 40 archives under `maindata`, all with the header
`Resource File V2.2`, from about 2 MB to 28 GB, so offsets and sizes above 4 GiB occur. No Anno 117 archive may enter
the repository ([0023](0023-game-names-and-icons-extracted-locally.md), [0029](0029-anonymised-test-fixtures-only.md)).

Search: the nuget.org website (its search API is refused to the session) for `rda`, `anno`, `anno rda`,
`anno 1800`, `anno 2205`, `anno117`, `rda archive`, `rda reader`, `rdareader`, `rdaexplorer`, `anno mod`,
`resource file`, `ubisoft archive` on 2026-11-08: no package reads Anno archives (`rda` finds only unrelated
packages such as `rda.SocketHelpers`, `RDapter`). The community's readers (RDAExplorer, RdaConsole) are not on
NuGet; their repositories could not be opened from the session. The Product Manager confirms the search before PR
approval.

The community tool RDA Explorer 1.4.0.0, a reader for Anno 2205's V2.2, lists `config.rda` and the
`zz_patchfiles_200` and `_300` archives of Anno 117, so their layout is at least close to V2.2 as known; the same
icon paths occur in both patch archives. The Product Manager confirmed building the reader without a real sample in
the repository (issue #21's working record, option a).

## Options considered

1. **A NuGet library** — none exists (search above).
2. **Vendor a community reader's source** — less format work; its licence, maintenance and Anno 117 support could
   not be checked, it would still need Censio's bounds and typed results, and it would carry other games' variants.
3. **An own slim reader for V2.2** — only the needed features (listing, stored, compressed and packed entries),
   bounded and failing closed like the pipe reader ([0033](0033-pipe-reader-strict-from-the-reference-source.md));
   built from the community's description of the format without a real sample in the repository.

## Decision

Option 3: `Censio.GameData` reads V2.2 archives itself, read-only and shared with the running game, within fixed
bounds, returning typed results; other versions and encrypted entries are refused. Format, bounds and failure kinds
are in the [game-files](../areas/game-files.md) area document.

## Consequences

- No dependency; the format is Censio's to maintain when the game changes it.
- Residual risk: the layout is built from the community's description, not from a sample in the repository. The
  Product Manager's layout probe of 2026-11-08 confirmed it on all 40 archives for the header, the block chain, the
  block header, the stored directory and the 560-byte entry; it found only plain blocks and blocks flagged deleted
  (entries with offset and sizes 0), no compressed, encrypted or memory-resident block, so those three layouts rest
  on the community's description and synthetic tests alone. The run of the installation check on the final head
  before PR approval opens every block; a mismatch sends the reader back to its plan, and a game update that brings
  a compressed block is read by code no real archive has exercised.
- Patch archives carry many blocks flagged deleted (20,405 of 20,478 in `zz_patchfiles_200`) naming paths with no
  content. The reader skips them; whether they remove files of earlier archives is part of the load order #23
  settles.
- Which archive wins when several hold the same path (the `zz_patchfiles_*` archives repeat icon paths) is not
  known from the spike; #23 must settle it before the catalog is built, and #24 depends on it for icons.
- An entry larger than the per-entry bound cannot be read; #23 revisits the bound or reads entries as streams if a
  needed file exceeds it.
