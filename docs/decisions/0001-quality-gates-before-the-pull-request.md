# 0001: Quality gates before the pull request

- **Status:** Accepted
- **Date:** 2026-10-30
- **Area:** —
- **Source:** Squad adopted from Squad-Spec-Repository-Template; Issue #3 (format check in CI, pinned tools)
- **Supersedes:** —

## Context

Formatting slips, analyzer findings and missing tests that only surface in CI or in the SonarQube Cloud
analysis of the pull request cost an extra fix-and-push round each, and every round costs reviewer
attention. The squad has a Code Officer whose sole job is to leave nothing of that kind behind, and the
`create-pr` skill runs the same checks outside the squad.

A local gate alone does not stop a push that skipped it (an outside contribution, a Dependabot pull request, a
hand-made change), and issue #3 asks CI to check the format too. A formatter on another release than the
analyzer, or a CI formatter on another version than the local one, reports differences the other side
considers correct; the formatter was installed unpinned (`dotnet tool install -g`), so nothing kept them together.

## Options considered

1. **Keep everything in CI** — a safety net for every contributor, but findings keep arriving late.
2. **Check locally and in CI** — earliest feedback plus a safety net; some checks run twice.
3. **Check locally first, CI as the system of record** — format, analyzer gate and coverage gate run
   before the push (`.squad/stack.md`); CI keeps the build, the tests and the code analysis.

For the formatter's version:

- **Unpinned global tool, and a CI install of a fixed `--version`** — no Dependabot update reaches a version in a
  workflow `run:` line, so CI drifts from the analyzer after its first release.
- **Pinned in the repository's tool manifest (`.config/dotnet-tools.json`), used by CI and the local commands,
  plus a CI step that fails when formatter and analyzer are on different release lines** — one version everywhere;
  Dependabot raises both in one grouped pull request, and the guard catches a bump that raises only one.

## Decision

Option 3, with the format check also in CI (option 2 for the format alone). Before a push: *Format*, the
*Analyzer gate* (no diagnostic of any severity in a changed file) and the *Coverage gate* (at least 80 % line
coverage on new/changed production code and overall) from `.squad/stack.md` pass. CI checks the format, builds,
tests and runs the code analysis (e.g. SonarQube Cloud), which stays the system of record for the quality gate.
The formatter and the SonarQube scanner are pinned in `.config/dotnet-tools.json`, and CI fails when the
formatter's major.minor differs from `Reihitsu.Analyzer`'s. Squad and agent tooling (`.squad/**`,
`.claude/**`) is excluded from the coverage measure in CI; it is developer tooling, not production code. The
checks are described in `docs/CONTRIBUTING.md`.

## Consequences

- Findings appear while coding; the Code Officer clears them before the review.
- A push that skips the local *Format* fails CI; one that skips the analyzer or coverage gate is only caught by
  what CI still checks. The PR template checklist and the `create-pr` skill require the local gates; adding a CI
  step back is the remedy if unchecked code starts reaching `main`.
- A Dependabot pull request that raises the analyzer and formatter can turn the format check red; a maintainer
  runs *Format* on its branch. The analyzer and the formatter may not be published the same day, and a hand-made
  bump may raise only one of them; the release-line check then turns CI red until both are on one line.
- The local analyzers may differ from the CI quality profile, and some checks (duplication, security
  hotspots, taint analysis) only run in CI; such findings arrive in squad step 11.
- The 80 % rule applies to new or changed lines, so it does not force retroactive tests on old code.
