# Spec: Move from .NET 10 to .NET 11

Source: the user's description ("Prüfe mit dem Squad eine Aktualisierung .NET 10 auf .NET 11")
Status: Draft

## Problem

The repository is built for one .NET major, .NET 10 (LTS): every target framework, `global.json` (SDK 10.0.404),
both stages of `src/Censio.Server/Dockerfile` and the Microsoft.Extensions / ASP.NET Core packages
(`.squad/stack.md`, *Toolchain*). .NET 11 was released on 2026-11-10 (runtime 11.0.0, SDK 11.0.100, release type
STS). Dependabot keeps the repository current only within its major, so the move is one change of its own. The
contributors want to know whether the move works, what it costs and what it risks, and to have it done if nothing
speaks against it, without the players, the operators of the server image or the contributors noticing a change in
behavior.

A trial build of the whole solution retargeted to .NET 11 (SDK 11.0.100, packages 11.0.0, made in a scratch copy on
2026-11-11) restores, builds and publishes, and the server publish output holds `_framework/blazor.web.js` in the
restore order of the Dockerfile (record 0007 still holds). It also showed the defects listed under *Errors and edge
cases* (E1–E6). None of them shows up on .NET 10.

## Scenarios

- S1: Given a contributor's machine or the CI runner with the .NET 11 SDK, when they run *Restore*, *Format check*,
  *Build*, *Test*, the *Analyzer gate* and the *Coverage gate* of `.squad/stack.md`, then every one passes. The
  solution is built for .NET 11 only.
- S2: Given an operator running the server image, when they pull the image of the first release after the move and
  start it with their existing `/config` volume, then it runs on the .NET 11 ASP.NET Core runtime. Port 8080, user
  `1654:1654`, `/config`, `/health` and the pages behave as before.
- S3: Given a player, when they download `Censio-<version>-win-x64.zip` of that release, then it holds the one
  self-contained `Censio.Companion.exe` (now .NET 11 inside). It needs no installed .NET and behaves as before:
  menu, log lines, recording, replay at every `--speed`.
- S4: Given a contributor who builds the Companion locally, when they read `README.md`, `docs/USER_GUIDE.md` or
  `docs/CONTRIBUTING.md`, then they find that building needs the .NET 11 SDK (11.0.100 or a later patch or feature
  band) and that running a local build needs the .NET 11 Desktop Runtime.
- S5: Given the move is merged, when Dependabot runs, then it proposes SDK, package and image updates within .NET 11
  and none of .NET 12.
- S6 (findings outcome): Given a defect from E1–E6 that cannot be removed in this repository without a change in
  behavior users can see, or a cause in .NET 11 itself with no workaround (E3 is the candidate), when the squad
  reaches that finding, then nothing is upgraded. The repository stays on .NET 10, and the change delivers the
  findings instead (AC9).

## Errors and edge cases

- E1: The warning about a game folder that is not an installation (`CompanionLog.cs:187`) is logged on .NET 11 with
  doubled backslashes (`maindata\\config.rda and bin\\win64\\anno117.exe`). The text documented in
  `docs/areas/game-installation.md` has single ones. Cause, measured: the .NET 10 logging source generator copied the
  template into the generated literal without escaping it, and the .NET 11 generator escapes it. The attribute's
  four-backslash spelling therefore only worked because of the old behavior. The test `GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord`
  ("No installation") fails.
- E2: The scaled replay pause changes, because the .NET 11 runtime converts `double` to `decimal` exactly
  (`(decimal)0.1` = `0.1000000000000000055511151231`) where .NET 10 gave `0.1`. `PipeReplaySpeed.Scale`
  (`PipeReplaySpeed.cs:97`) divides by that value. `PipeReplaySpeedScaleSaturatesAtMaximum(922337203685477581)`
  fails: it gets 509 ticks below `TimeSpan.MaxValue` instead of saturating. Within the documented range the difference
  stays below one tick, but the documented rule is "the recorded pause divided by the speed" as written
  (`docs/areas/pipe-recording.md`, `0.1` to `1000`).
- E3: `PipeRecordingSessionSeveralFailuresRaiseStateChangedOnce` times out after its 10-second guard. This happened in
  3 of 3 full-solution test runs on .NET 11 and in 0 of 3 on .NET 10, and the test passes alone or with its class on
  .NET 11 (3 of 3). Cause unknown: it can be a test that depends on scheduling or a real hang of
  `PipeRecordingSession` that .NET 11 exposes.
- E4: New diagnostics `CA2016` and `S8949` at `src/Censio.Anonymiser/AnonymiserCommand.cs:89`. .NET 11 offers a
  `WriteLineAsync` overload that takes the cancellation token. The command's output and exit codes do not change.
- E5: Build warning `NU1510` for `Microsoft.Extensions.Logging.Abstractions` in `Censio.Companion.Core`. .NET 11
  ships the assembly in `Microsoft.NETCore.App` (checked in the installed 11.0.0 shared framework).
- E6: `.github/scripts/release-check.sh:27` and `.github/scripts/release-check-test.sh:290` hard-code
  `obj/Release/net10.0-windows/…`. `dotnet_major.py` does not read scripts, so the release ZIP check fails after the
  retarget (measured; with the path changed, the ZIP is built and holds exactly `Censio.Companion.exe`).
- E7: A machine with only the .NET 10 SDK cannot build any more, and `dotnet` reports that no SDK compatible with
  `global.json` (11.0.100) is installed. The documentation of S4 names the SDK it needs.
- E8: Tools of `.config/dotnet-tools.json` on a runner with only the .NET 11 runtime: `reihitsu-format` 1.2.0 targets
  `net10.0` (manifest `rollForward: true`), `dotnet-sonarscanner` 12.1.0 targets `netcoreapp3.1` and already rolls
  forward in CI today. Both must run in CI. The sandbox cannot show it, because it has the .NET 10 runtime too.
- E9: An existing `/config` written by the .NET 10 image (data-protection key ring) keeps working after the image
  update. The server starts and does not fail on the old keys.

## Non-functional requirements

- N1: Platforms: the image bases go to `mcr.microsoft.com/dotnet/sdk:11.0.100-alpine3.24-amd64` and
  `mcr.microsoft.com/dotnet/aspnet:11.0.0-alpine3.24-amd64` (both tags exist), still pinned by their amd64 manifest
  digest (record 0010). Alpine stays 3.24, which .NET 11 supports. The Companion stays Windows x64.
- N2: The release ZIP grows by at most 10 % (measured: 69.7 MB on .NET 10, 72.2 MB on .NET 11).
- N3: No behavior users can see changes: log texts, command line, replay timing, the image's port, user, data
  directory and health check, the pages.
- N4: One .NET major in the whole repository, including the places `dotnet_major.py` does not read (scripts,
  documentation).
- N5: The test doubles whose behavior was checked against the .NET 10 runtime source (`ControlledPipeStream`,
  `ControlledWriteStream`, `FakeStartupEntryStore`, `.squad/project.md`, *Test doubles*) are checked again against
  `dotnet/runtime` `release/11.0` (`.squad/team.md`, *Test doubles that model a real component*).

## Acceptance criteria

- AC1: Every project targets `net11.0` (`Censio.Companion`: `net11.0-windows`), `global.json` names SDK 11.0.100
  (`rollForward` `latestFeature`, no prerelease), the `Microsoft.AspNetCore.*` and `Microsoft.Extensions.*` packages
  are on 11.0.x or are removed where .NET 11 provides them, and both Dockerfile stages use the .NET 11 images of N1
  pinned by amd64 manifest digest, with matching labels. `dotnet_major.py` passes, and no tracked file outside
  `specs/` and the released decision records names `net10.0` or a .NET 10 SDK, image or runtime as the current one
  (S1, S2, E5, E6, N1, N4).
- AC2: On SDK 11, *Restore*, *Format check*, *Build* (no warning), *Test*, the *Analyzer gate* and the *Coverage
  gate* pass. The diagnostics of E4 and E5 are fixed, not suppressed (S1, E4, E5).
- AC3: On .NET 11 the game-folder warning is logged exactly as `docs/areas/game-installation.md` states it, single
  backslashes. The existing "No installation" case passes unchanged, and no other `[LoggerMessage]` template's output
  differs from .NET 10 (E1, N3).
- AC4: On .NET 11 `PipeReplaySpeed.Scale` gives the results of .NET 10 for every existing case of
  `PipeReplaySpeedTests`, unchanged, saturation at `TimeSpan.MaxValue` included (E2, N3).
- AC5: The full test suite (*Test*) passes in five consecutive runs in the sandbox and in CI on .NET 11. The plan
  names the cause of the E3 timeout with file and line, and the change removes it where it lies, without a longer
  guard, a retry or a skipped test (E3).
- AC6: The CI jobs `check`, `image-build` and `release-check` pass. The release ZIP holds exactly the self-contained
  `Censio.Companion.exe`, within N2. The image passes `image-check.sh` (port, user, `/config`, health), and a
  `/config` with a key ring written by the current .NET 10 image starts the .NET 11 image without an error (S2, S3,
  E6, E8, E9, N2).
- AC7: `README.md` and `docs/USER_GUIDE.md` (English and German), `docs/CONTRIBUTING.md`, the project blocks of
  `AGENTS.md` and `.github/copilot-instructions.md`, `.squad/stack.md`, `.squad/project.md` (test doubles re-checked
  as N5), `.github/dependabot.yml` comments and the area documents below name .NET 11. A decision record supersedes
  the released record 0016. The SDK and runtime these documents name are those of S4 (S4, S5, E7, N5).
- AC8: (manual, before the pull request is approved) the Product Manager starts the branch's Companion ZIP on Windows
  11 x64. The tray menu shows `Version <version>`, the diagnostics window opens, and
  `--replay <recording> --speed 10` runs to `Replay finished` (S3).
- AC9: (findings outcome, instead of AC1–AC8) if S6 applies, no project, image or package changes. The change holds a
  decision record "Stay on .NET 10" that lists each finding with its cause and evidence and the condition for
  trying again. The orchestrator opens a follow-up issue with those findings (S6).

## Out of scope

- Using new .NET 11 or C# 15 features, and code changes the move does not force.
- The move to .NET 12, and any change of the Dependabot policy beyond the comments.
- The .NET 10.0.15 patch released on 2026-11-10 (Dependabot's ordinary update, made obsolete by AC1).
- An arm64 image, and the Windows versions the README names (Windows 10 22H2 appears in neither runtime's support
  list. The move does not change that.)
- The comment in `.claude/hooks/session-start.sh` (a squad file, and still true).

## Open questions

- Is .NET 11 released, and are its images there? Answered: yes. Runtime 11.0.0 and SDK 11.0.100 came out on
  2026-11-10, support phase "active". MCR has `sdk:11.0.100-alpine3.24-amd64` and `aspnet:11.0.0-alpine3.24-amd64`,
  and NuGet has the 11.0.0 packages.
- STS instead of LTS: move now or wait for .NET 12? Answered: (Lead) move now. The end of support is the same date
  (2028-11-14, release index of `builds.dotnet.microsoft.com`, read 2026-11-11), so nothing is lost. The work is small. Taking the breaking changes now (E1–E3) means the move to .NET 12
  meets fewer at once. The Product Manager can overrule this at the pull request, and AC9 covers a blocker.
- Record 0016 decides "C# on .NET 10". Answered: it was released in v0.1.0, and its consequences already foresee a
  move to a later .NET. A new record supersedes it (AC7). Records 0004, 0007 and 0010 name .NET 10 only as an example
  or as measured fact, and their decisions hold on .NET 11 (0007 measured in the trial publish).
- Do the analyzers and test tools work with SDK 11? Answered: yes in the trial. Reihitsu, Sonar and MSTest analyzers
  load, there is no `RH` diagnostic, MSTest 4.5.2 and coverlet 10.1.0 run and write coverage, and the only new
  diagnostics are those of E4 and E5.

## Area documents

- `docs/areas/server.md` (*Docker image*): the two image names and versions of N1.
- `docs/areas/companion.md` (*Platform*): a local build needs the .NET 11 Desktop Runtime, and the release ZIP still
  needs none.
- `docs/areas/game-installation.md` and `docs/areas/pipe-recording.md`: unchanged. AC3 and AC4 keep their text true.
