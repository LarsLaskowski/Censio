# Tasks: Tick assembler and island identity

Plan: [plan.md](plan.md) — Status: Draft

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Skeleton: tick types, limits and `PipeTickBuilder` (plan, *Signatures*) | `src/Censio.Pipe/PipeIslandKey.cs`, `PipeTickIsland.cs`, `PipeTick.cs`, `PipeTickCloseReason.cs`, `PipeTickResult.cs`, `PipeTickResultKind.cs`, `PipeTickLimits.cs`, `PipeTickBuilder.cs` | AC1, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC13 | — | Dev | [x] |
| 2 | G1 | Failing builder tests and the parser's map-capacity test | `tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs`, `PipeMessageParserTests.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC12, AC13 | 1 | Tester | [ ] |
| 3 | G1 | Implement `PipeTickBuilder` and the tick types; `TrimExcess` in `PipeMessageParser.TryReadMap`; measure the memory bound (plan fact 9) | files of task 1, `src/Censio.Pipe/PipeMessageParser.cs` | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC12, AC13 | 2 | Dev | [ ] |
| 4 | G2 | Skeleton: `IPipeMessageSource` (`ReadAsync`), `PipeMessageReader` implements it, `PipeTickAssembler` (`ReadAsync`, `Dispose`); package `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 | `src/Censio.Pipe/IPipeMessageSource.cs`, `PipeMessageReader.cs`, `PipeTickAssembler.cs`, `Directory.Packages.props`, `tests/Censio.Pipe.Tests/Censio.Pipe.Tests.csproj` | AC2, AC9, AC11, AC12 | 3 | Dev | [ ] |
| 5 | G2 | Test double and failing assembler tests | `tests/Censio.Pipe.Tests/ScriptedPipeMessageSource.cs`, `PipeTickAssemblerTests.cs` | AC2, AC9, AC11, AC12 | 4 | Tester | [ ] |
| 6 | G2 | Implement `PipeTickAssembler` | `src/Censio.Pipe/PipeTickAssembler.cs` | AC2, AC9, AC11, AC12 | 5 | Dev | [ ] |
| 7 | G2 | Area document `pipe.md` (plan, *Documentation updates*) | `docs/areas/pipe.md` | AC1, AC2, AC5, AC6, AC10 | 6 | Dev | [ ] |
| 8 | G2 | Area index row and `ARCHITECTURE.md` | `docs/areas/README.md`, `docs/ARCHITECTURE.md` | AC1 | 6 | Dev | [ ] |
| 9 | G2 | Test doubles in `.squad/project.md` | `.squad/project.md` | AC2, AC12 | 5 | Tester | [ ] |
