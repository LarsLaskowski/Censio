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
NuGet; their repositories could not be opened from the session. The Product Manager repeated the search before PR
approval with the same result: no NuGet package; the only readers are RDAExplorer and RdaConsole on GitHub.
RdaConsole worked for Anno 1800, the direct predecessor, and RDAExplorer reads the current files (Product Manager's
statement, the repositories not opened by the session).

The community tool RDA Explorer 1.4.0.0, a reader for Anno 2205's V2.2, lists `config.rda` and the
`zz_patchfiles_200` and `_300` archives of Anno 117 (the only ones tried), so their layout is at least close to V2.2
as known. The Product Manager confirmed building the reader without a real sample in
the repository (issue #21's working record, option a).

## Options considered

1. **A NuGet library** — none exists (search above).
2. **Vendor a community reader's source** — less format work; its licence, maintenance and Anno 117 support could
   not be checked, it would still need Censio's bounds and typed results, and it would carry other games' variants.
3. **An own slim reader for V2.2** — only the needed features (listing, stored, compressed and packed entries),
   bounded and failing closed like the pipe reader ([0033](0033-pipe-reader-strict-from-the-reference-source.md));
   built from the community's description of the format without a real sample in the repository; effort: about
   1,700 lines of reader and survey code and 2,300 lines of tests on synthetic archives, written within this spike,
   plus the upkeep when a game update changes the format.

## Decision

Option 3: `Censio.GameData` reads V2.2 archives itself, read-only and shared with the running game, within fixed
bounds, returning typed results; other versions are refused, and encrypted blocks are skipped and
counted, their entries not listed. Format, bounds and failure kinds
are in the [game-files](../areas/game-files.md) area document.

## Consequences

- No dependency; the format is Censio's to maintain when the game changes it.
- Residual risk: the layout is built from the community's description, not from a sample in the repository. The
  Product Manager's layout probe and the installation checks of 2026-11-08 (the last on the final head, game version
  3.0.1733841, store not stated) confirmed it on all 40 archives: header,
  block chain, block header, stored directory and 560-byte entry; every block's directory passes the reader's checks
  and no archive comes near the default bounds (at most 52,878 entries). Every listed entry is plain: no block that is
  not deleted is compressed, encrypted or memory-resident, so those three layouts rest on the community's description
  and synthetic tests alone, and a game update that brings such a block is read by code no real archive has
  exercised. Reading while the game runs is not confirmed: the run on the final head was requested with the game
  running, but the Product Manager's report does not state the game's state, and the unit test of the shared open
  discriminates on Windows only. Should the game block the reader or be disturbed by it, #22 or #23 changes the
  open mode.
- Patch archives carry many blocks with flag 8 (20,405 of 20,478 in `zz_patchfiles_200`) whose entries have no
  content. The reader skips them as deleted; whether flag 8 means that, and whether such entries remove files of
  earlier archives, is part of the load order #23 settles.
- Which archive wins when several hold the same path (`ui.rda` and the `zz_patchfiles_*` archives repeat icon
  folders) is not known from the spike; #23 must settle it before the catalog is built, and #24 depends on it for
  icons. An unverified hint from the Product Manager, without evidence: the number in `zz_patchfiles_<n>` is
  the game version (higher is newer), and probably all of them are loaded, since a newer one does not hold the
  contents of the earlier ones.
- The spike locates files, not data: the asset, data set and text files and the icon folders are known, but which
  file and which nodes hold goods, buildings, workforce and regions is left to #23, since the check reads no content.
- An entry larger than the per-entry bound cannot be read; #23 revisits the bound or reads entries as streams if a
  needed file exceeds it.
