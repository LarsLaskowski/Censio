# Tasks: Spike — reading RDA archives (own V2.2 reader and installation survey)

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 0 | G1 | Discovery listing logged (`log.md` rows 2a, 2b) | `specs/feature-21-rda-archive-spike/log.md` | AC6 | — | Product Manager | [x] |
| 1 | G1 | Layout probe run (plan, *Layout probe*); output logged in a fenced block | `specs/feature-21-rda-archive-spike/log.md` | AC3, AC4 | 0 | Product Manager | [x] |
| 2 | G1 | Fact 7 and *Approach* corrected from the probe | `specs/feature-21-rda-archive-spike/plan.md` | AC3, AC4 | 1 | Lead | [x] |
| 3 | G1 | Skeleton: `RdaBlockFlags`, `RdaErrorKind`, `RdaError`, `RdaLimits`, `RdaEntry`, `RdaArchive`, `RdaOpenResult`, `RdaReadResult` (plan, *Signatures*) | `src/Censio.GameData/Rda*.cs` | AC3, AC4 | 2 | Dev | [ ] |
| 4 | G1 | Helpers `RdaArchiveBuilder`, `SparseStream`; failing tests of AC3, AC4 and E7 | `tests/Censio.GameData.Tests/RdaArchiveTests.cs`, `RdaLimitsTests.cs`, `RdaArchiveBuilder.cs`, `SparseStream.cs` | AC3, AC4, AC5 | 3 | Tester | [ ] |
| 5 | G1 | Implement the reader (plan, *Approach*, Reader) | `src/Censio.GameData/Rda*.cs` | AC3, AC4 | 4 | Dev | [ ] |
| 6 | G2 | Skeleton: `RdaInstallationSurvey`, `RdaSurveyReport` | `src/Censio.GameData/RdaInstallationSurvey.cs`, `RdaSurveyReport.cs` | AC5 | 5 | Dev | [ ] |
| 7 | G2 | Failing survey tests and the `CENSIO_ANNO_INSTALL` test | `tests/Censio.GameData.Tests/RdaInstallationSurveyTests.cs` | AC5 | 6 | Tester | [ ] |
| 8 | G2 | Implement the survey (plan, *Approach*, Survey) | `src/Censio.GameData/RdaInstallationSurvey.cs`, `RdaSurveyReport.cs` | AC5 | 7 | Dev | [ ] |
| 9 | G3 | Product Manager's survey run on the branch head after step 7; output logged in a fenced block | `specs/feature-21-rda-archive-spike/log.md` | AC2, AC5 | 8 | Product Manager | [ ] |
| 10 | G3 | Raise an `RdaLimits.Default` value the run of task 9 hit, if any | `src/Censio.GameData/RdaLimits.cs` | AC5 | 9 | Dev | [ ] |
| 11 | G3 | `docs/areas/game-files.md` with *Locations* from task 9, and its row in `docs/areas/README.md` | `docs/areas/game-files.md`, `docs/areas/README.md` | AC2 | 9 | Dev | [ ] |
| 12 | G3 | `docs/ARCHITECTURE.md` (GameData row, *Security model*) | `docs/ARCHITECTURE.md` | AC1 | 8 | Dev | [ ] |
| 13 | G3 | `.squad/project.md` *Security areas* | `.squad/project.md` | AC3 | 8 | Dev | [ ] |
| 14 | G3 | `.squad/project.md` *Test doubles* (`SparseStream`); `docs/UNIT_TESTS.md` (`CENSIO_ANNO_INSTALL`) | `.squad/project.md`, `docs/UNIT_TESTS.md` | AC5 | 7 | Tester | [ ] |
| 15 | G3 | Record 0043 updated from tasks 1 and 9 where a consequence changes | `docs/decisions/0043-rda-archives-read-by-an-own-bounded-reader.md` | AC1 | 9 | Lead | [ ] |

Tasks 0–2 (Product Manager and Lead, no production or test code) belong to G1 only because every row needs a group:
they finish before G1's step 4, and task 3's skeleton waits for task 2. G3 has no new API.
