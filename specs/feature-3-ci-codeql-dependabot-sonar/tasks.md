# Tasks: CI, CodeQL, Dependabot and SonarQube Cloud

Plan: [plan.md](plan.md) — Status: Draft

No production or test code: steps 4 (*Skeleton*) and 5 (*Tests first*) are not applicable (*Verification without
tests* in the plan). Every group: no new API.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | no new API — create the tool manifest with `reihitsu.cli` 1.1.0 and `dotnet-sonarscanner` 12.0.0 (plan *Approach* 1); verify fact 14a | `.config/dotnet-tools.json` | AC8, AC9 | — | Dev | [x] |
| 2 | G1 | Rewrite `ci.yml` as plan *Approach* 2 (step order, `SONAR_ENABLED`, step-scoped token, `persist-credentials: false`, *Check tool versions*, `if:` conditions, every `actions/cache` step and the scanner install removed, `/o:"networlddev"`); run the local checks of *Verification without tests* for AC1, AC2, AC5, AC6, AC9 and fact 9 | `.github/workflows/ci.yml` | AC1, AC2, AC3, AC4, AC5, AC6, AC9 | 1 | Dev | [x] |
| 3 | G1 | `codeql.yml` as plan *Approach* 3 | `.github/workflows/codeql.yml` | AC6, AC7 | — | Dev | [x] |
| 4 | G1 | `dependabot.yml` Docker entry to `/src/Censio.Server` (plan *Approach* 4) | `.github/dependabot.yml` | AC8 | — | Dev | [x] |
| 5 | G1 | `python3 .squad/tools/analyzer-check.py --lint-only` on the changed workflows | — | AC1, AC6, AC7 | 2, 3, 4 | Dev | [x] |
| 6 | G2 | `.squad/stack.md`: *Toolchain*, *Format*, *Format check*, *Sonar gate* command and section, `DOTNET_ROOT` pitfall (plan *Documentation updates*) | `.squad/stack.md` | AC9, AC11 | 2 | Dev | [x] |
| 7 | G2 | `docs/CONTRIBUTING.md` `getting-started` block: tool manifest, *Automated checks* subsection (plan *Documentation updates*) | `docs/CONTRIBUTING.md` | AC11 | 2, 3, 4 | Dev | [x] |
| 8 | G2 | `docs/CONTRIBUTING.md` `releases` block: release-note labels, `feature` included | `docs/CONTRIBUTING.md` | AC10 | — | Dev | [x] |
| 9 | G3 | Draft PR: owner prerequisites asked of the Product Manager, CI/CodeQL/SonarQube green, throwaway broken-format and failing-test commits turn CI red and are reverted | — | AC12 | 1–8 | Orchestrator | [ ] |
