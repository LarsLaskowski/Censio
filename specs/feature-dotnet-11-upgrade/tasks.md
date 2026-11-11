# Tasks: Move from .NET 10 to .NET 11

Plan: [plan.md](plan.md)

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: no new API. Retarget every project, `global.json`, packages (E5 reference removed), Dockerfile images and labels, script paths | `src/*/*.csproj`, `tests/*/*.csproj`, `global.json`, `Directory.Packages.props`, `src/Censio.Server/Dockerfile`, `.github/scripts/release-check.sh`, `.github/scripts/release-check-test.sh` | AC1, AC6 | — | Dev | [ ] |
| 2 | G1 | Tests first: new speed case, new anonymiser cancelled-token case, adapted recording-session test | `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs`, `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs`, `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` | AC2, AC4, AC5 | 1 | Tester | [ ] |
| 3 | G1 | Fix E1, E2, E4 | `src/Censio.Companion.Core/CompanionLog.cs`, `src/Censio.Pipe/PipeReplaySpeed.cs`, `src/Censio.Anonymiser/AnonymiserCommand.cs` | AC2, AC3, AC4 | 2 | Dev | [ ] |
| 4 | G1 | Checks: AC1 grep, AC3 grep, *Image build check*, key-ring cross-run; five *Test* runs | — | AC1, AC3, AC5, AC6 | 3 | Dev / Tester | [ ] |
| 5 | G2 | Documentation updates of the plan; report `.squad/stack.md` text | `README.md`, `docs/USER_GUIDE.md`, `docs/CONTRIBUTING.md`, `docs/areas/server.md`, `docs/areas/companion.md`, `docs/areas/pipe-recording.md`, `AGENTS.md`, `.github/copilot-instructions.md`, `.github/dependabot.yml` | AC7 | 4 | Dev | [ ] |
| 6 | G2 | Test documentation: `ControlledWriteStream` reference; report `.squad/project.md` text | `tests/Censio.Pipe.Tests/ControlledWriteStream.cs` | AC7 | 4 | Tester | [ ] |
| 7 | G2 | Write the reported `.squad/stack.md` and `.squad/project.md` text | `.squad/stack.md`, `.squad/project.md` | AC7 | 5, 6 | Orchestrator | [ ] |
| 8 | PM | Early draft PR; manual check of the `companion-dry-run` ZIP on Windows 11 | — | AC8 | 7 | Product Manager | [ ] |
| 9 | PM | Fallback only if S6 applies: record "Stay on .NET 10" and describe the follow-up issue, instead of rows 1–8 | `docs/decisions/` | AC9 | — | Lead | [ ] |
