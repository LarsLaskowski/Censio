# Spec: Move from .NET 10 to .NET 11

Source: the user's description ("Prüfe mit dem Squad eine Aktualisierung .NET 10 auf .NET 11")
Status: Approved by Lead

## Problem

The repository is built for one .NET major, .NET 10 (LTS): every target framework, `global.json` (SDK 10.0.404),
both stages of `src/Censio.Server/Dockerfile` and the Microsoft.Extensions / ASP.NET Core packages
(`.squad/stack.md`, *Toolchain*). .NET 11 was released on 2026-11-10 (runtime 11.0.0, SDK 11.0.100, release type
STS). Dependabot stays within the major, so the move is one change of its own. The
contributors asked the squad to check whether the move works, and what it costs and risks, with no change in
behavior that users can see.

A trial build of the whole solution retargeted to .NET 11 (SDK 11.0.100, packages 11.0.0, made in a scratch copy on
2026-11-11) restores, builds and publishes. The server publish output holds `_framework/blazor.web.js` in the
restore order of the Dockerfile (record 0007 holds). It also showed E1, E2 and E4–E6, and E3 more often than on
.NET 10. The Product Manager decided to move now (*Open questions*).

## Scenarios

- S1: Given a contributor's machine or the CI runner with the .NET 11 SDK, when they run *Restore*,
  *Format check*, *Build*, *Test*, the *Analyzer gate* and the *Coverage gate* of `.squad/stack.md`, then every one
  passes. The solution is built for .NET 11 only.
- S2: Given an operator running the server image, when they pull the image of the first release after the
  move and start it with their existing `/config` volume, then it runs on the .NET 11 ASP.NET Core runtime. Port
  8080, user `1654:1654`, `/config`, `/health` and the pages behave as before.
- S3: Given a player, when they download `Censio-<version>-win-x64.zip` of that release, then it holds the
  one self-contained `Censio.Companion.exe` (now .NET 11 inside). It needs no installed .NET and behaves as before:
  menu, log lines, recording, replay at every `--speed`.
- S4: Given a contributor who builds the Companion locally, when they read `README.md`,
  `docs/USER_GUIDE.md` or `docs/CONTRIBUTING.md`, then they find that building needs the .NET 11 SDK (11.0.100 or a
  later patch or feature band) and that running a local build needs the .NET 11 Desktop Runtime.
- S5: Given the move is merged, when Dependabot runs, then it proposes SDK, package and image updates
  within .NET 11 and none of .NET 12.
- S6: (fallback) Given a defect of E1–E10 that cannot be removed here without a change users can see, when the squad
  finds it, then the repository stays on .NET 10 and the findings are recorded (AC9).

## Errors and edge cases

- E1: The warning about a game folder that is not an installation (`CompanionLog.cs:187`) is logged on .NET 11 with
  doubled backslashes (`maindata\\config.rda and bin\\win64\\anno117.exe`). The text in
  `docs/areas/game-installation.md` has single ones. Cause, measured from the generated sources: the .NET 10 logging
  generator copied the template into the generated literal without escaping it, and the .NET 11 generator escapes
  it. The four-backslash spelling only worked because of the old behavior. The "No installation" case of
  `GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord` fails. It is the only `[LoggerMessage]` template
  in `src/` holding `\\`, `\"`, `\t`, `\n` or `{{` (grep, 2026-11-11).
- E2: The scaled replay pause changes inside the documented speed range. The .NET 11 runtime converts `double` to
  `decimal` exactly (`(decimal)0.4` = `0.40000000000000002220446049250`), where .NET 10 rounded to `0.4`.
  `PipeReplaySpeed.Scale` (`PipeReplaySpeed.cs:97`, `Math.Round(ticks / (decimal)factor)`, rounding to even) divides by
  that value. Measured at `--speed 0.4`: a 3-tick pause gives 8 ticks on 10.0.15 and 7 on 11.0.0, and 30,000,003
  ticks give 75,000,008 and 75,000,007. At `0.1`, `PipeReplaySpeedScaleSaturatesAtMaximum(922337203685477581)` gets
  509 ticks below `TimeSpan.MaxValue` instead of saturating. The documented rule is the recorded pause divided by the
  speed as written (`docs/areas/pipe-recording.md`).
- E3: `PipeRecordingSessionSeveralFailuresRaiseStateChangedOnce` times out after its 10-second guard. This happened in
  3 of 3 full-solution runs on .NET 11 and in 0 of 3 on .NET 10, and the test passes alone on .NET 11. It is a race
  in the test, possible on .NET 10 too:
  - `PipeRecorder` starts its writer with `Task.Run` (`src/Censio.Pipe/PipeRecorder.cs:174`) and opens the file only
    there (`:224`).
  - If the first writer fails before the second connection records, `OnRecorderFailed` sets `Failed`
    (`PipeRecordingSession.cs:331-332`).
  - The second `Record` then returns early (`:218`) and creates no recorder. Its stream is never disposed, so the
    wait for it runs into the guard.
- E4: New diagnostics `CA2016` and `S8949` at `src/Censio.Anonymiser/AnonymiserCommand.cs:89`. .NET 11 offers a
  `WriteLineAsync` overload that takes the cancellation token. That write lies before the `try` that maps
  cancellation to `Error: cancelled` (`:95-102`). With the token passed and already cancelled, the usage line may not
  print and an unhandled exception may replace exit code 2.
- E5: Build warning `NU1510` for `Microsoft.Extensions.Logging.Abstractions` in `Censio.Companion.Core`. .NET 11
  ships the assembly in `Microsoft.NETCore.App`.
- E6: `.github/scripts/release-check.sh:27` and `.github/scripts/release-check-test.sh:290` hard-code
  `obj/Release/net10.0-windows/…`. `dotnet_major.py` does not read scripts, so the release ZIP check fails after the
  retarget (measured; with the path changed, the ZIP holds exactly `Censio.Companion.exe`).
- E7: A machine with only the .NET 10 SDK cannot build any more, and `dotnet` reports that no SDK compatible with
  `global.json` (11.0.100) is installed.
- E8: The tools of `.config/dotnet-tools.json` on a runner with only the .NET 11 runtime:
  - `reihitsu-format` 1.2.0 targets `net10.0`, with the manifest's `rollForward: true`.
  - `dotnet-sonarscanner` 12.1.0 targets `netcoreapp3.1` and already rolls forward in CI today.
  - Both must run in CI (the sandbox also has the .NET 10 runtime).
- E9: Upgrade: a `/config` whose key ring the .NET 10 image wrote is used by the .NET 11 image (record 0011). The
  server starts and does not fail on the old keys.
- E10: Rollback: an operator who returns to the last .NET 10 image with a `/config` that the .NET 11 image used (and
  in which it may have created a key) gets a server that starts and reads that key ring.

## Non-functional requirements

- N1: Platforms: the image bases go to `mcr.microsoft.com/dotnet/sdk:11.0.100-alpine3.24-amd64` and
  `mcr.microsoft.com/dotnet/aspnet:11.0.0-alpine3.24-amd64` (both tags exist), still pinned by their amd64 manifest
  digest (record 0010). Alpine 3.24 is supported by both majors. The Companion stays Windows x64.
- N2: The release ZIP grows by at most 10 % (measured: 69.7 MB on .NET 10, 72.2 MB on .NET 11).
- N3: No behavior users can see changes: log texts, command line, replay timing, the image's port, user, data
  directory and health check, the pages.
- N4: One .NET major in the whole repository, including the places `dotnet_major.py` does not read (scripts,
  documentation).
- N5: The test doubles whose behavior was checked against the .NET 10 runtime source (`ControlledPipeStream`,
  `ControlledWriteStream`, `FakeStartupEntryStore`, `.squad/project.md`, *Test doubles*) are checked again against
  `dotnet/runtime` `release/11.0` (`.squad/team.md`, *Test doubles that model a real component*).
- N6: Windows: `supported-os.json` (`builds.dotnet.microsoft.com`, 10.0 and 11.0, read 2026-11-11) lists Windows 10
  only as LTSC 1607, 1809 and 21H2 for both majors. Neither lists Windows 10 22H2 (`README.md:40`,
  `docs/USER_GUIDE.md:289`), and the minimum does not rise. The new decision record states that Windows 10 22H2 is
  untested on .NET 11.

## Acceptance criteria

- AC1: Every project targets `net11.0` (`Censio.Companion`: `net11.0-windows`), `global.json` names SDK
  11.0.100 (`rollForward` `latestFeature`, no prerelease), the `Microsoft.AspNetCore.*` and `Microsoft.Extensions.*`
  packages are on 11.0.x or are removed where .NET 11 provides them, and both Dockerfile stages use the images of N1
  pinned by amd64 manifest digest, with matching labels. `dotnet_major.py` passes, and no tracked file outside
  `specs/` and the released decision records names `net10.0` or a .NET 10 SDK, image or runtime as the current one
  (S1, S2, E5, E6, N1, N4).
- AC2: On SDK 11, *Restore*, *Format check*, *Build* (no warning), *Test*, the *Analyzer gate* and the
  *Coverage gate* pass. The diagnostics of E4 and E5 are fixed, not suppressed. A run with the wrong number of
  arguments and an already-cancelled token still prints the usage line and returns the usage exit code, and a test
  pins it (S1, E4, E5).
- AC3: On .NET 11 the game-folder warning is logged exactly as `docs/areas/game-installation.md` states
  it, with single backslashes. The existing "No installation" case passes unchanged. The grep of E1 over every
  `[LoggerMessage]` template in `src/` finds no other template with an escape or `{{` (E1, N3).
- AC4: `PipeReplaySpeed.Scale` divides by the speed as written, not by its binary approximation, and does
  not depend on how the runtime converts `double` to `decimal`. On .NET 11 every existing case of
  `PipeReplaySpeedTests` passes unchanged, saturation included, and a new case pins `--speed 0.4` with a 3-tick pause
  to 8 ticks (the .NET 10 result) (E2, N3).
- AC5: The test of E3 holds the first write until both recorders exist and keeps the assertion that
  the state change is raised once. The plan confirms the race of E3 or refutes it with evidence. The full test suite
  (*Test*) passes in five consecutive runs in the sandbox and in CI, without a longer guard, a
  retry or a skipped test (E3).
- AC6: The CI jobs `check`, `image-build` and `release-check` pass. The release ZIP holds exactly the
  self-contained `Censio.Companion.exe`, within N2. The image passes `image-check.sh` (port, user, `/config`, health).
  With one data directory, the .NET 11 server starts on a key ring the current .NET 10 server wrote, and the .NET 10
  server starts again on that ring after the .NET 11 server ran, both without an error (S2, S3, E6, E8, E9, E10, N2).
- AC7: `README.md` and `docs/USER_GUIDE.md` (English and German), `docs/CONTRIBUTING.md`, the project
  blocks of `AGENTS.md` and `.github/copilot-instructions.md`, `.squad/stack.md`, `.squad/project.md` (test doubles
  re-checked as N5), the `.github/dependabot.yml` comments and the area documents below name .NET 11 and the SDK of
  S4. A decision record supersedes the released record 0016 and states N6 (S4, S5, E7, N5, N6).
- AC8: (manual, before the pull request is approved) the Product Manager starts the branch's Companion ZIP
  on Windows 11 x64. The tray menu shows `Version <version>`, the diagnostics window opens, and
  `--replay <recording> --speed 10` runs to `Replay finished` (S3).
- AC9: (fallback, only if S6 applies, instead of AC1–AC4 and AC6–AC8) No project, image or package changes, and
  AC5 is delivered on .NET 10. A decision record "Stay on .NET 10" lists each finding with cause and evidence, and the
  orchestrator opens a follow-up issue carrying them (S6).

## Out of scope

- Using new .NET 11 or C# 15 features, and code changes the move does not force.
- The move to .NET 12 itself, and any change of the Dependabot policy beyond the comments.
- The .NET 10.0.15 patch released on 2026-11-10 (Dependabot's ordinary update).
- An arm64 image, and changing the Windows versions the README names (N6 only records their status).

## Open questions

- Is .NET 11 released, and are its images there? Answered: yes. Runtime 11.0.0 and SDK 11.0.100 came out on
  2026-11-10, support phase "active". MCR has `sdk:11.0.100-alpine3.24-amd64` and `aspnet:11.0.0-alpine3.24-amd64`,
  and NuGet has the 11.0.0 packages.
- Move to .NET 11 now, or stay on .NET 10 until .NET 12? Answered: the Product Manager, 2026-11-11: "Jetzt auf .NET 11
  wechseln" (move now). This was weighed against the facts that both majors end on 2028-11-14 and that moving now
  means two migrations before .NET 12 instead of one. The decision record of AC7 states the decision and that reason.
- Record 0016 decides "C# on .NET 10". Answered: it is released (v0.1.0), so a new record supersedes it (AC7).
  Records 0004, 0007 and 0010 still hold on .NET 11.
- Do the analyzers and test tools work with SDK 11? Answered: yes in the trial. There is no `RH` diagnostic, MSTest
  and coverlet run, and the only new diagnostics are those of E4 and E5.

## Area documents

- `docs/areas/server.md` (*Docker image*): the two image names and versions of N1.
- `docs/areas/companion.md` (*Platform*): a local build needs the .NET 11 Desktop Runtime, and the release
  ZIP still needs none.
- `docs/areas/game-installation.md` and `docs/areas/pipe-recording.md`: unchanged. AC3 and AC4 keep their text true.

## Challenge

- 1 (major, AC4 passes a wrong build): accepted. The 0.4 results were re-measured, and E2 and AC4 changed.
- 2 (major, product decision): accepted. It was escalated, and the Product Manager chose to move now. The stay path
  remains only as a fallback (S6, AC9).
- 3 (minor, E3 cause): accepted. The race is in E3, the test fix in AC5 (also under the fallback), and E3 is no blocker.
- 4 (minor, cancelled token before the `try`): accepted. See E4 and AC2.
- 5 (minor, other templates): accepted. The grep was run, see E1 and AC3.
- 6 (minor, rollback): accepted. See E10 and AC6.
- 7 (minor, Windows support): accepted. See N6. The minimum does not rise, so there is no new scenario.
