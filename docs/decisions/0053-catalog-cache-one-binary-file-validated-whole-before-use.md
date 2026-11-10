# 0053: The catalog cache is one own binary file with a checksum, validated whole before any of it is used

- **Status:** Proposed
- **Date:** 2026-11-10
- **Area:** game-catalog
- **Source:** Issue #25
- **Supersedes:** —

## Context

The cache holds the catalog and up to 256 MiB of PNG icons ([`game-catalog.md`](../areas/game-catalog.md), *Limits*)
in the player's local app data folder. Any program of the player's account can change it, two Companions of one
account in two Windows sessions can write it at once, and `Exit` may interrupt a write. The icons' PNG data reaches
the Windows image decoder in the diagnostics window. Readers of external input are strict and fail closed (record
[0033](0033-pipe-reader-strict-from-the-reference-source.md)). The catalog format for the server is #26, not merged.

## Options considered

1. **JSON with the icons in Base64** — readable; a third larger, and bounding a JSON document read forward with
   per-value limits needs as much code as a binary reader.
2. **A folder of PNG files with an index** — many files, no single replace, a partial set possible after a crash.
3. **The format of #26** — not written yet, and meant for the wire, not for a local cache.
4. **One own binary file, length-prefixed, with a SHA-256 checksum at the end, written to a unique temporary file and
   moved over the old one** — one replace, bounded reads, a torn or mixed file fails the checksum.

## Decision

Option 4. The file is checked as a whole before anything of it is published, and each icon must have exactly the PNG
layout the Companion's own writer produces; one failure refuses the whole file and leads to an extraction. Layout,
bounds and checks are in [`game-catalog.md`](../areas/game-catalog.md), *Cache*.

## Consequences

- The checksum detects damage and torn writes, not a deliberate change by a program of the same account, which could
  also change the Companion itself; the PNG checks keep anything but the writer's own layout from the image decoder.
- A change of the format raises its version number, and an older file is then extracted again.
- #26 may later read the same catalog but defines its own format.
