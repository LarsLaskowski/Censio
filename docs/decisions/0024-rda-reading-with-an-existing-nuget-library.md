# 0024: RDA archives are read with an existing NuGet library where one fits

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #21 (RDA spike)
- **Supersedes:** —

## Context

The game files that hold names and icons ([0023](0023-game-names-and-icons-extracted-locally.md)) are RDA archives.
The modding community has tools that read them; whether one exists as a maintained C# library on NuGet that supports
Anno 117 and has a licence compatible with Censio's ([0028](0028-mit-licence.md)) is not known yet.

## Options considered

1. **Use an existing NuGet library** — less own code for a format others already maintain; depends on its
   maintenance and licence.
2. **Write an own slim reader** — full control, only the features needed; more code to write and keep up with the
   format.

## Decision

Option 1 if the spike of #21 finds a library that fits: a permissive licence (MIT, BSD, Apache or Unlicense, no GPL),
maintained, with Anno 117 support; otherwise option 2. #21 records its result and the file locations in this record.

## Consequences

- A chosen library is a dependency of `Censio.GameData`, added through Central Package Management and updated by
  Dependabot.
- An own reader is parsing of external input and is bounded and validated like any other
  (`.squad/project.md`, *Security areas*).
