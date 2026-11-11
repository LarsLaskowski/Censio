# 0051: DDS icons are decoded by own BC7 and PNG code, without an image library

- **Status:** Accepted
- **Date:** 2026-11-10
- **Area:** game-catalog-icons
- **Source:** Issue #24
- **Supersedes:** —

## Context

The game's icons are DDS files with a `DX10` header in BC7 (four samples, all eight BC7 modes used); the web UI and
the Companion's window need PNG. The issue suggested BCnEncoder.NET with ImageSharp. Dependencies need a licence that
permits use in an MIT project ([0028](0028-mit-licence.md)), and the Product Manager excluded ImageSharp (Six Labors
Split License). The decoder reads untrusted input in the platform-neutral `Censio.GameData`, tested on Linux.

## Options considered

1. **BCnEncoder.NET with ImageSharp** — complete; ImageSharp is excluded.
2. **BCnEncoder.NET 2.3.0 for BC7 (licence `MIT OR Unlicense`, read from its `LICENSE-MIT` and `UNLICENSE` at the tag;
   depends on CommunityToolkit.HighPerformance and Microsoft.Bcl.Numerics) with an own PNG writer** — a maintained
   decoder (last commit 2026-03-05); three new packages for one function, an own header check and PNG writer still
   needed, and its reserved BC7 mode decodes to opaque magenta, not to the transparent black the spec asks for.
3. **Own BC7 decoder, DDS header check and PNG writer** — no dependency, bounds and failure behavior in our hands, about
   500 lines; the decoder's tables must be right, which the tests pin against BCnEncoder.NET as a reference run once
   outside the repository.

## Decision

Option 3: `Censio.GameData` reads the DDS header, decodes BC7 blocks and writes PNG itself, from the BC7 format
description; BCnEncoder.NET serves only as the reference the tests' expected pixels were checked against. The formats
read and the bounds are in the [game-catalog-icons](../areas/game-catalog-icons.md) area document.

## Consequences

- No new package, no Dependabot update for icons; a format the game moves to later (another BC mode or format) needs
  code, and shows as *unreadable* icons until then.
- The tests pin each BC7 mode with expected pixels from the reference, and the four real samples are compared with the
  reference once outside the repository; the comparison is reported as counts only.
