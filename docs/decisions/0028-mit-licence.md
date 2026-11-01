# 0028: Censio is released under the MIT licence

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8
- **Supersedes:** —

## Context

Censio is an open-source fan tool. Its users run it at home, others may want to fork or reuse parts of it (the pipe
reader, the contract), and every dependency it takes must be compatible with its licence.

## Options considered

1. **A copyleft licence (GPL)** — derived work stays open; harder to reuse in other projects, and its obligations
   reach every redistributed binary.
2. **MIT** — short, permissive, widely understood; anyone may reuse the code, also in closed projects.
3. **Apache 2.0** — permissive with a patent grant; longer and with notice obligations a fan tool does not need.

## Decision

Option 2. The repository's `LICENSE` is the MIT licence, and README names it.

## Consequences

- Dependencies need a licence that permits use in an MIT project; GPL libraries are out (as #21 requires,
  [0024](0024-rda-reading-with-an-existing-nuget-library.md)).
- The licence covers Censio's own code only; game files and extracted game assets are not Censio's and are never
  distributed ([0023](0023-game-names-and-icons-extracted-locally.md)).
