# Tasks: Docker image for the server

Plan: [plan.md](plan.md) — Status: Draft

Steps 4 and 5 are not applicable (no production or test code, plan *Verification without tests*); every group
says "no new API".

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Image: Dockerfile and ignore file as in *Approach* (no new API), the `VERSION` check as the whole-string `case` before the `grep`; the newest full-version tags and their digests at the time | `src/Censio.Server/Dockerfile`, `.dockerignore` | AC1, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC11 | — | Dev | [x] |
| 10 | G1 | `ignore` of major updates in the `docker` entry | `.github/dependabot.yml` | AC9 | — | Dev | [x] |
| 2 | G1 | *Image build check* in `.squad/stack.md` follows both copies of the Dockerfile; run it, and run it once on a copy without `RequiresAspNetWebAssets` (must fail with the message) | `.squad/stack.md` | AC10 | 1 | Dev | [x] |
| 3 | G2 | CI job `image` and its script with every case of *Verification without tests* (no new API), AC7 with the line-break value `$'1.0.0\n;Foo=bar'`; `bash -n`, `shellcheck`, `actionlint`, `hadolint` via the *Analyzer gate* | `.github/workflows/ci.yml`, `.github/scripts/image-check.sh` | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC9, AC10, AC11, AC13 | 1 | Dev | [x] |
| 4 | G3 | Area document and index row | `docs/areas/server.md`, `docs/areas/README.md` | AC12 | 3 | Dev | [ ] |
| 5 | G3 | `SECURITY.md` loopback sentence for the image | `SECURITY.md` | AC12 | 3 | Dev | [ ] |
| 6 | G3 | `docs/ARCHITECTURE.md` *Deployment* and *Security model* | `docs/ARCHITECTURE.md` | AC12 | 3 | Dev | [ ] |
| 7 | G3 | README Docker section, English then German | `README.md` | AC12 | 3 | Dev | [ ] |
| 8 | G3 | User guide Docker section, English then German | `docs/USER_GUIDE.md` | AC12 | 3 | Dev | [ ] |
| 9 | G3 | CI and Dependabot bullets | `docs/CONTRIBUTING.md` | AC12 | 3 | Dev | [ ] |
