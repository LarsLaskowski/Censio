# Tasks: Spike — reading RDA archives (own V2.2 reader and installation survey)

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 0 | G0 | Discovery listing logged (`log.md` row 2a) | `specs/feature-21-rda-archive-spike/log.md` | AC6 | — | Product Manager | [x] |
| 1 | G1 | Skeleton: `RdaBlockFlags`, `RdaErrorKind`, `RdaError`, `RdaLimits`, `RdaEntry`, `RdaArchive`, `RdaOpenResult`, `RdaReadResult` (plan, *Signatures*) | `src/Censio.GameData/Rda*.cs` | AC3, AC4 | — | Dev | [ ] |
| 2 | G1 | Test helpers `RdaArchiveBuilder`, `SparseStream`; failing tests of AC3, AC4 and E7 | `tests/Censio.GameData.Tests/RdaArchiveTests.cs`, `RdaArchiveBuilder.cs`, `SparseStream.cs` | AC3, AC4, AC5 | 1 | Tester | [ ] |
| 3 | G1 | Implement the reader (plan, *Approach*, Reader) | `src/Censio.GameData/Rda*.cs` | AC3, AC4 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `RdaInstallationSurvey`, `RdaSurveyReport` | `src/Censio.GameData/RdaInstallationSurvey.cs`, `RdaSurveyReport.cs` | AC5 | 3 | Dev | [ ] |
| 5 | G2 | Failing survey tests and the `CENSIO_ANNO_INSTALL` test | `tests/Censio.GameData.Tests/RdaInstallationSurveyTests.cs` | AC5 | 4 | Tester | [ ] |
| 6 | G2 | Implement the survey (plan, *Approach*, Survey) | `src/Censio.GameData/RdaInstallationSurvey.cs`, `RdaSurveyReport.cs` | AC5 | 5 | Dev | [ ] |
| 7 | G3 | Product Manager's survey run on the branch head after step 7 (orchestrator hands over the command, logs the output) | `specs/feature-21-rda-archive-spike/log.md` | AC2, AC5 | 6 | Product Manager | [ ] |
| 8 | G3 | `docs/areas/game-files.md` with *Locations* from task 7, and its row in `docs/areas/README.md` | `docs/areas/game-files.md`, `docs/areas/README.md` | AC2 | 7 | Dev | [ ] |
| 9 | G3 | `docs/ARCHITECTURE.md` (GameData row, *Security model*) | `docs/ARCHITECTURE.md` | AC1 | 6 | Dev | [ ] |
| 10 | G3 | `.squad/project.md` *Security areas* | `.squad/project.md` | AC3 | 6 | Dev | [ ] |
| 11 | G3 | `.squad/project.md` *Test doubles* (`SparseStream`); `docs/UNIT_TESTS.md` (`CENSIO_ANNO_INSTALL`) | `.squad/project.md`, `docs/UNIT_TESTS.md` | AC5 | 5 | Tester | [ ] |
| 12 | G3 | Record 0043 updated from task 7 where a consequence changes | `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` | AC1 | 7 | Lead | [ ] |

G0 and G3 have no new API.
