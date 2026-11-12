# 0057: CI checks the format, and the formatter and the scanner are pinned in the tool manifest

- **Status:** Accepted
- **Date:** 2026-11-12
- **Area:** —
- **Source:** Issue #3 (format check in CI, pinned tools); split from 0001
- **Supersedes:** 0001 (its decision for issue #3; the template's quality gates in it are in [`process.md`](process.md), 0040)

## Context

The squad's quality gates run locally before a push ([`process.md`](process.md)). A local gate alone does not stop
a push that skipped it (an outside contribution, a Dependabot pull request, a hand-made change), and issue #3 asks CI
to check the format too. A formatter on another release than the analyzer, or a CI formatter on another version than
the local one, reports differences the other side considers correct; the formatter was installed unpinned
(`dotnet tool install -g`), so nothing kept them together. Record 0001 decided this together with the template's
gates; the template's part now lives in `process.md`, so this record carries Censio's own part on its own.

## Options considered

1. **Unpinned global tool, and a CI install of a fixed `--version`** — no Dependabot update reaches a version in a
   workflow `run:` line, so CI drifts from the analyzer after its first release.
2. **Pinned in the repository's tool manifest (`.config/dotnet-tools.json`), used by CI and the local commands,
   plus a CI step that fails when formatter and analyzer are on different release lines** — one version everywhere;
   Dependabot raises both in one grouped pull request, and the guard catches a bump that raises only one.

## Decision

Option 2, and CI checks the format as well as building, testing and running the code analysis. The formatter and
the SonarQube scanner are pinned in `.config/dotnet-tools.json`; CI's *Check tool versions* step fails when the
formatter's major.minor differs from `Reihitsu.Analyzer`'s, and its *Format check* step fails on unformatted code.
The checks are described in `docs/CONTRIBUTING.md`.

## Consequences

- A push that skips the local *Format* fails CI.
- A Dependabot pull request that raises the analyzer and formatter can turn the format check red; a maintainer
  runs *Format* on its branch. The analyzer and the formatter may not be published the same day, and a hand-made
  bump may raise only one of them; the release-line check then turns CI red until both are on one line.
