# Tasks: Release workflow: pre-release, Windows ZIP, Docker image, promote

Plan: [plan.md](plan.md) — Status: Draft

Steps 4 and 5 are not applicable (no production or test code, plan *Verification without tests*); every group is
step 6, the Dev's edits.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G0 | Decision record 0012 and its index row | `docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md`, `docs/decisions/README.md` | AC1, AC3, AC7, AC9 | — | Lead | [x] |
| 2 | G1 | no new API; the check script with the commands and texts of plan *Approach* (`form`, `push`, `promote-image` included) | `.github/scripts/release-check.sh` | AC2, AC3, AC4, AC5, AC6, AC7, AC8 | — | Dev | [x] |
| 3 | G1 | The test script with the cases of plan *Tests per criterion* (`registry:2` pinned by digest on `127.0.0.1`); run locally after starting `dockerd` | `.github/scripts/release-check-test.sh` | AC2, AC3, AC4, AC5, AC6, AC7, AC8 | 2 | Dev | [x] |
| 4 | G1 | CI job `release-check`: `gh --version`, the test script, artifact `companion-dry-run` | `.github/workflows/ci.yml` | AC2, AC4, AC5, AC6, AC7, AC8 | 3 | Dev | [x] |
| 5 | G2 | no new API; the release workflow, six jobs as plan *Approach*: outputs via `env` and `form`, downloads into `runner.temp`, no cache, retention 7 days | `.github/workflows/release.yml` | AC1, AC3, AC6, AC7, AC9 | 2 | Dev | [x] |
| 6 | G3 | README: Docker by pull, *Build from source*, *Companion*, English and German | `README.md` | AC10 | 5 | Dev | [x] |
| 7 | G3 | User guide: as README | `docs/USER_GUIDE.md` | AC10 | 5 | Dev | [x] |
| 8 | G3 | CONTRIBUTING: *Versioning and releases*, *Release check* and *Release* bullets | `docs/CONTRIBUTING.md` | AC5, AC10 | 5 | Dev | [x] |
| 9 | G3 | Area documents and their index rows | `docs/areas/server.md`, `docs/areas/companion.md`, `docs/areas/README.md` | AC1, AC2, AC10 | 5 | Dev | [x] |
| 10 | G3 | ARCHITECTURE *Deployment*; project *Security areas* entry | `docs/ARCHITECTURE.md`, `.squad/project.md` | AC10 | 5 | Dev | [x] |
