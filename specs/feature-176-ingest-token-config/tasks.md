# Tasks: Ingest token from a file named by configuration

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: `public const string TokenFileKey`, `public const int MinTokenLength` (plan, *Signatures*) | `src/Censio.Server/IngestTokenSetup.cs` | AC1–AC7 | — | Dev | [ ] |
| 2 | G1 | Tests first: the new tests of *Tests per criterion*, the adapted rows and texts, the `CreateConfiguration` helper with the token file setting | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs`, `tests/Censio.Server.Tests/ProgramTests.cs` | AC1–AC7 | 1 | Tester | [ ] |
| 3 | G1 | Implement the decision order, the configured read without exception (catching `ArgumentException` too), the minimum and the log lines (plan, *Approach*); check fact 9 once with the published server | `src/Censio.Server/IngestTokenSetup.cs` | AC1–AC7 | 2 | Dev | [ ] |
| 4 | G2 | Documentation: area documents, README (English and German), user guide (English and German), `SECURITY.md`, `docs/ARCHITECTURE.md`; report the exact `.squad/project.md` text (plan, *Areas* and *Documentation updates*: the E4 sentences of `ingest.md`, the `--mount` example, the `chown 1654`/`chmod 0400` step, user guide lines 61/349/423/711, `server.md` line 84) | `docs/areas/ingest.md`, `docs/areas/server.md`, `README.md`, `docs/USER_GUIDE.md`, `SECURITY.md`, `docs/ARCHITECTURE.md` | AC8 | 3 | Dev | [ ] |
| 5 | G2 | Write in the reported `.squad/project.md` text | `.squad/project.md` | — | 4 | Orchestrator | [ ] |
| 6 | PM | Dry run of the user guide's new steps on Linux Docker Engine (host file `1654`/`0400`, `--mount ...,readonly`, then with the file removed, recording what happens), on an image built from the branch head (fallback: the orchestrator builds and hands over the image); confirms or corrects facts 12–14 before the documentation review | — | AC8 | 4 | Product Manager | [x] |

G2 has no new API.
