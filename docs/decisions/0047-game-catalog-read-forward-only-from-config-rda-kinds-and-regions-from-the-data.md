# 0047: The game catalog is read forward-only from `config.rda`; kinds and regions come from the data, and the reader fails closed

- **Status:** Proposed
- **Date:** 2026-11-09
- **Area:** game-catalog
- **Source:** Issue #23
- **Supersedes:** —

## Context

The pipe names regions, goods, workforce and buildings by GUID only; the names are in the player's installation and
never leave it ([0023](0023-game-names-and-icons-extracted-locally.md)). The spike of #21 located the asset, data set
and text files in `config.rda` but not the nodes ([0043](0043-rda-archives-read-by-an-own-bounded-reader.md)). For
#23 the Product Manager supplied the real `assets.xml` (46,440,056 bytes), `datasets.xml` and the English and German
texts of one installation, kept outside the repository, and saw no copy of them in the patch archives. The issue
forbids hard-coded regions. The files are untrusted input, and the Companion is a tray application that builds the
catalog at every start until the cache of #25.

## Options considered

1. **Load each file as a document tree** — simplest code; holds several hundred MB for `assets.xml` in a tray
   application at every start.
2. **Generated serialisation types for the game's schema** — typed access; ties the code to hundreds of templates a
   game update may change, and still loads whole documents.
3. **Forward-only reading of `config.rda`'s four files, one file at a time, walking each asset with the reader
   itself** — memory holds one archive entry, the catalog candidates, the texts they use and the nodes of one asset;
   only the few nodes the catalog needs are interpreted. Building each asset as a subtree (`XNode.ReadFrom`) lost: its
   cost grows with the square of the depth and is paid before any bound can be checked, so one asset within the
   entry bound could pin a core for hours (Security's measurement).
4. **Skip a malformed asset and go on** — a partial catalog survives an odd value; a contradiction in untrusted
   input goes unnoticed, unlike the pipe and archive readers ([0033](0033-pipe-reader-strict-from-the-reference-source.md)).
5. **Resolve inherited assets (`BaseAssetGUID`) through their base** — covers about 45 building-like assets; the
   inheritance rules are unknown and none of them is in the fixture.
6. **Read a region asset's missing `RegionID` as a written-in default** — links the base game's sessions to their
   region type; breaks the issue's "never hard-coded".

## Decision

Option 3 with fail-closed reading: the catalog is built from `config.rda` alone; a defining asset's kind, names and
regions are taken only from nodes the data states, an inherited asset is left out, a missing `RegionID` stays "not
stated" (#130), each asset is bounded in depth and elements, and the first malformed file or value, or an asset over
a bound, fails the whole catalog. The nodes, rules and failure kinds are
in the [game-catalog](../areas/game-catalog.md) area document.

## Consequences

- Residual risks, each visible to the player and none hidden: the layout rests on one installation's files (game
  version 3.0.1733841); a game update that changes a node name or adds an odd value turns the catalog into
  `Catalog: failed` until the reader is changed; a patch archive that one day carries its own copy is reported in the
  log but not read; inherited assets are left out and counted; the base game's sessions carry no region type until
  #130.
- Text identifiers shared by several assets are normal (a session shares one with its region asset); only a GUID
  defined twice, or one text identifier with two different texts in one language, makes a GUID ambiguous.
- That the XML parser refuses every document type form and resolves no entity is a property of .NET's `XmlReader`
  with document type processing prohibited and no resolver, measured by Security's probe and pinned by a unit test.
  Exception messages quote content, so no exception text reaches a result or the log.
- The boundary catches of the archive opening and of the copy listing stay although today's archive reader reports
  every failure it knows as a result, so no unit test reaches them: they keep an exception of a later reader change
  from ending the Companion's catalog task untyped.
- With a UNC installation folder from the settings file, `config.rda` is read and the `maindata` archives listed over
  the network at every start.
- Building at every start costs the listing of every archive under `maindata` (for the copy report) and one pass over
  about 60 MB of XML; the duration is measured by #25.
