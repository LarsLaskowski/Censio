# 0029: Test fixtures from recordings and game files are anonymised

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #12 (recorder), Issue #17 (reference sessions), Issue #20 (anonymiser)
- **Supersedes:** —

## Context

Parsers are best tested against real samples of the format they read, and the pipe recordings of #12 and #17 are
those samples. A recording can contain what identifies a player or their machine — savegame and island names, user
and host names, paths — and data from the game's files belongs to the publisher. The repository is public.

## Options considered

1. **Commit real recordings** — the most faithful tests; publishes private data and game content.
2. **Hand-written fixtures only** — nothing private; tests then check our own understanding of the format, not the
   format.
3. **Real recordings, anonymised before they are committed** — real structure and variants, with every private value
   replaced and no game asset included.

## Decision

Option 3. A fixture under `tests/` that comes from a recording or a game file is anonymised with the anonymiser of
#20 before it is committed: every private value is replaced whole, economic values are perturbed and timestamps
shifted while structure and GUIDs are kept, and no extracted game asset is included.

## Consequences

- The anonymiser (#20) has to exist before the first real recording is committed.
- A reviewer checks a new fixture for values that were not replaced.
- Names and icons in tests are invented, never extracted ([0023](0023-game-names-and-icons-extracted-locally.md)).
