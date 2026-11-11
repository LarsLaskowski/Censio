# Plan: Move from .NET 10 to .NET 11

Source: [spec.md](spec.md)
Tier: standard — the change moves the toolchain and fixes four behavior regressions of .NET 11 in production code, and it creates, changes or removes no security control.
Security areas touched: network exposure (`src/Censio.Server/Dockerfile` base images); release publishing (`.github/scripts/release-check.sh`); logging (`CompanionLog`); parsing of external input (`PipeReplaySpeed` in `src/Censio.Pipe`); local pipe connection and data-protection key ring (the runtime under `NamedPipeConnector` and `DataProtectionSetup`); Docker and build configuration (`global.json`, `Directory.Packages.props`, every `.csproj`, `.github/dependabot.yml`, `.github/scripts/release-check-test.sh`); dependencies (Microsoft packages moved to 11.0.0, one removed)

## Problem / root cause

See [spec.md](spec.md). The four production defects and their causes are listed there with file and line: E1
`CompanionLog.cs:187`, E2 `PipeReplaySpeed.cs:97`, E4 `AnonymiserCommand.cs:89`, E6 `release-check.sh:27`. E3 is a
race in the test, at `PipeRecordingSessionTests.cs:172-197`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | .NET 11.0.0 / SDK 11.0.100 were released on 2026-11-10 as STS. .NET 10 and .NET 11 both end support on 2028-11-14. | `releases-index.json` at `builds.dotnet.microsoft.com`, read 2026-11-11. | confirmed |
| 2 | The tags `sdk:11.0.100-alpine3.24-amd64` and `aspnet:11.0.0-alpine3.24-amd64` exist, and they are single-arch manifests. Digests: `sha256:8234f4b51a16ff4416c013729616bd5df1211ced20e746067ddc66be7f526063` (sdk) and `sha256:c25d33205f2064b811c82770635314433c758eac35d364c1f2b6f3711da75bfd` (aspnet). | MCR `tags/list`, and a manifest HEAD with `Docker-Content-Digest` (v2 manifest), 2026-11-11. The Dev reads both again when editing. | confirmed |
| 3 | Retargeted to .NET 11, the solution builds. The only new diagnostics are `CA2016` and `S8949` at `AnonymiserCommand.cs:89` and `NU1510` in `Censio.Companion.Core`. There is no `RH` diagnostic. The failing tests are those of E1, E2 and E3. | A full SARIF build in a scratch copy and a SARIF build of `main`, compared. Three full test runs. | confirmed |
| 4 | Writing the template of `CompanionLog.cs:187` with one escaped backslash (`maindata\\config.rda` in the C# source) logs single backslashes on .NET 11. | Generated `LoggerMessage.g.cs` read on both majors. In the scratch copy, that spelling made the "No installation" case pass. | confirmed |
| 5 | `decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, …)` succeeds for every text the current `double` check accepts, and it gives the written value. | Twelve forms tried on .NET 11: `0.1`, `.5`, `5.`, `1000.0`, leading zeros, `0.09999999999999999999`, and others. Overflow happens only far above 1000. | confirmed |
| 6 | At `0.4`, `Math.Round(ticks / 0.4m)` gives 8 for 3 ticks and 75000008 for 30000003 ticks. For 922337203685477581 ticks at `0.1m` the result reaches `long.MaxValue`, so it saturates. | Scratch runs on 10.0.15 and 11.0.0, where `(decimal)` of a parsed `double` differs. | confirmed |
| 7 | E3 is a race. The second `Record` returns at `PipeRecordingSession.cs:218` once the first writer (`PipeRecorder.cs:174`, which opens its file at `:224`) has failed. `ControlledWriteStream.HoldWrites` (`ControlledWriteStream.cs:174`) holds a write before it fails. | Read the code. | confirmed |
| 8 | `TextWriter.WriteLineAsync(string, CancellationToken)` exists on .NET 11 and not on .NET 10. | Reflection on both runtimes. | confirmed |
| 9 | .NET 11's `Microsoft.NETCore.App` holds `Microsoft.Extensions.Logging.Abstractions`. Only `Censio.Companion.Core` references that package. Without the reference, the solution builds and the Companion.Core tests pass, logging generator included. | The shared framework folder, a grep, and a scratch build and test. | confirmed |
| 10 | In `release/11.0`, `NamedPipeClientStream.Windows.cs` and `PipeStream.ValueTaskSource.cs` are unchanged from `release/10.0`. `Win32Marshal` only adds the path to `DirectoryNotFoundException`, and `RegistryKey` changes only in refactorings. Record 0035 and the three test doubles still hold. | A diff of the raw files from `dotnet/runtime`, 2026-11-11. | confirmed |
| 11 | With the server restored the way the Dockerfile does it, the .NET 11 publish holds `wwwroot/_framework/blazor.web.js`. | The *Image build check* reproduction in the scratch copy. | confirmed |
| 12 | With the path fixed, `release-check.sh companion` builds a ZIP holding only `Censio.Companion.exe`, 72.2 MB (69.7 MB on .NET 10). | A scratch run. | confirmed |
| 13 | `reihitsu-format` (`net10.0` tool, `rollForward: true`) and `dotnet-sonarscanner` run in CI with the SDK of `global.json`. | to verify: CI job `check` on the draft PR, step 11, orchestrator. | unverified |
| 14 | A key ring written by the .NET 10 server is read by the .NET 11 server, and the other way round. | to verify: the Dev's cross-run (*Verification* under AC6), step 6. | unverified |
| 15 | Neither major's support list names Windows 10 22H2, and the lowest Windows 10 entry is 1607 for both. | `supported-os.json` 10.0 and 11.0, 2026-11-11. | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: `python3 .squad/tools/dotnet_major.py` passes, and `git grep -nE 'net10\.0\|10\.0\.404\|10\.0\.14\|\.NET 10\|\.NET-10' -- ':!specs' ':!docs/decisions/0*'` lists only the example of `docs/CONTRIBUTING.md` (*Base image check*), by the Dev in step 6. CI: `base-check.yml` and `check` | — |
| AC2 | `AnonymiserCommandWrongArgumentCountWithCancelledTokenPrintsUsage` (new). It also passes before the fix, as a guard: it fails if the Dev passes the run's token to the usage write. Gates: step 7 | `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs` |
| AC3 | `GameInstallationSearchSettingsProblemLogsWarningAndReadsNoRecord` (existing, unchanged, fails on .NET 11 before the fix). check: the grep of spec E1 (every `[LoggerMessage]` template in `src/` with an escape or `{{`) lists only `CompanionLog.cs:187`, by the Dev | `tests/Censio.Companion.Core.Tests/GameInstallationSearchTests.cs` |
| AC4 | `PipeReplaySpeedScaleNonBinaryFactorDividesByWrittenSpeed` (new; DataRows `"0.4", 3, 8` and `"0.4", 30000003, 75000008`; fails on .NET 11 before the fix). All existing cases of the class stay unchanged | `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs` |
| AC5 | `PipeRecordingSessionSeveralFailuresRaiseStateChangedOnce` (adapted). check: *Test* five times in a row, all green, by the Tester after the adaptation and again in step 7. CI: `check` | `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs` |
| AC6 | CI: `check`, `image-build` (`image-check.sh`) and `release-check` on the draft PR. check: *Image build check* and the key-ring cross-run (*Approach*), by the Dev | — |
| AC7 | check: the Reviewer reads each file of *Documentation updates* and record 0054 against the spec | — |
| AC8 | manual: the Product Manager, on Windows 11 x64, with the artifact `companion-dry-run` of CI job `release-check`, from an *Early draft PR*, before step 9 | — |
| AC9 | check: fallback only. If S6 applies, the Reviewer confirms that no project, image or package changed and that the record and the issue exist | — |

## Approach

1. **Toolchain (AC1).**
   - Every `.csproj`: `net10.0` becomes `net11.0`, and `net10.0-windows` becomes `net11.0-windows`.
   - `global.json` names `11.0.100`; `rollForward` and `allowPrerelease` stay as they are.
   - `Directory.Packages.props`: `Microsoft.AspNetCore.Mvc.Testing` goes to `11.0.0` and `Microsoft.Extensions.TimeProvider.Testing` to `11.0.0`. The `Microsoft.Extensions.Logging.Abstractions` entry is deleted, together with its `PackageReference` in `Censio.Companion.Core.csproj` (E5).
   - Dockerfile: both `FROM` lines and the two `org.opencontainers.image.base.*` labels take the tags and digests of fact 2.
   - The two script paths of E6 take `net11.0-windows`.
2. **E1 (AC3).** The template at `CompanionLog.cs:187` is written with `\\` (one backslash in the string) wherever it has `\\\\` today. The logged text stays as `game-installation.md` states it.
3. **E2 (AC4).**
   - `PipeReplaySpeed` keeps its public API, including `double? Factor`, so the existing tests stay unchanged.
   - It gets a private `decimal?` divisor. `TryParse` validates exactly as today with `double`, then sets the divisor from `decimal.TryParse` of the same text (fact 5). `RealTime` gets `1m`, and `Maximum` and `default` get `null`.
   - `Scale` divides by the divisor. It no longer converts with `(decimal)factor`.
   - The accepted texts and the documented range do not change.
4. **E3 (AC5, Tester).** The test calls `first.HoldWrites()` before the first `Record` and `first.ReleaseWrites()` after the second `Record`. It keeps both failures, the wait for both disposals and the assertions. The production code is unchanged: the behavior of a failed session (no further file) is the one documented.
5. **E4 (AC2).** Line 89 becomes `await _error.WriteLineAsync(Usage, CancellationToken.None).ConfigureAwait(false);`. The usage line is not cancellable work, and output and exit code stay unchanged with a cancelled token.
6. **Key-ring cross-run (AC6, Dev, step 6).** There is no Docker daemon in the sandbox, so the Dev uses two published servers.
   - Publish the server from `main` (a scratch `git worktree`, .NET 10) and from the branch (.NET 11).
   - With one empty data directory (`CENSIO__DATADIRECTORY`, a free port in `ASPNETCORE_HTTP_PORTS`), start in turn: .NET 10, then .NET 11, then .NET 10 again. Before the third start, set the key's activation and expiration dates back 91 days so the .NET 11 run creates a new key. Each run waits for `/health` 200 and stops.
   - Expected: no `Error` or `Critical` log line from data protection in any run, and the key files of the earlier runs still present.
   - The Dev reports the commands and the log lines.
7. No new user-visible output.

## Options considered

- **E2: parse the divisor from the text** (chosen) against **change `Factor` to `decimal?`** and against **round the converted `double` to 15 significant digits**. Changing `Factor` alters the public API and its eight existing tests, and every test would then build `decimal` expectations from `double` DataRows, which again depends on the conversion. Rounding to 15 digits copies the old runtime's conversion by hand: it is exact only for texts of 15 digits or fewer, and it hides the rule.
- **E6: update the literal** (chosen) against **derive the path from `dotnet msbuild -getProperty`**. Deriving adds a build call to the release job of a security area, to save one line per major. The gap that let the literal go stale belongs to `dotnet_major.py` and goes to a squad issue (*Out of scope*).
- **AC6 key ring: the cross-run of two published servers** (chosen) against **extending `image-check.sh` with a pull of the last released image**. The extension makes every CI run depend on Docker Hub and on a release existing. The data-protection library the image runs is the same one the published server runs.
- **The move itself:** option 1 of record 0054, staying until .NET 12, was rejected by the Product Manager.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| all | every `.csproj`, `global.json`, `Directory.Packages.props` | target framework, SDK, packages |
| Censio.Server | `Dockerfile` | base images, labels |
| Censio.Companion.Core | `CompanionLog`, `.csproj` | template escape, package reference removed |
| Censio.Pipe | `PipeReplaySpeed` | private divisor parsed from the text |
| Censio.Anonymiser | `AnonymiserCommand` | `CancellationToken.None` on the usage write |
| — | `.github/scripts/release-check.sh`, `release-check-test.sh`, `.github/dependabot.yml` | path, comments |

## Signatures (for the Dev's skeleton)

None: no public or internal member is added or changed. The divisor of `PipeReplaySpeed` is private (*Approach*), so step 4 adds no skeleton.

## Test files

- `tests/Censio.Pipe.Tests/PipeReplaySpeedTests.cs`
- `tests/Censio.Anonymiser.Tests/AnonymiserCommandTests.cs`
- `tests/Censio.Companion.Core.Tests/PipeRecordingSessionTests.cs`
- `tests/Censio.Companion.Core.Tests/GameInstallationSearchTests.cs`, unchanged

Existing test code calling a changed signature: none.

Files no unit test can cover: none (the Dockerfile and the scripts are checked by CI).

## Areas

- `server.md` (*Docker image*): the two image names of N1.
- `companion.md` (*Platform*): the .NET 11 Desktop Runtime for a local build.
- `pipe-recording.md` (*Replay*, *Speed*): "divided by the speed" becomes "divided by the speed exactly as written".

## Documentation updates

All by the Dev, once the code is green, unless another owner is named.

- `README.md` lines 129 and 294, and `docs/USER_GUIDE.md` lines 279 and 607: ".NET 10 SDK (version 10.0.404 …" becomes ".NET 11 SDK (version 11.0.100 …", and ".NET-10-SDK (Version 10.0.404" becomes ".NET-11-SDK (Version 11.0.100".
- `docs/CONTRIBUTING.md` lines 26–27: `net11.0-windows` and the ".NET 11 Desktop Runtime". The tag example at line 66 stays; it is a hypothetical example.
- `docs/areas/server.md` lines 65–66, `docs/areas/companion.md` line 163, `docs/areas/pipe-recording.md` line 89: as listed under *Areas*.
- The project block *configuration* of `AGENTS.md` (lines 111–112): `net11.0-windows` and `net11.0`, written identically in `.github/copilot-instructions.md`.
- `.github/dependabot.yml`, comments at lines 24–27 and 34–35: the next major becomes .NET 12 (`12.0.0` targets `net12.0`; "11 to 12").
- `.squad/stack.md` lines 10 and 32: `net11.0` and `net11.0-windows`. The Dev reports the exact text, and the orchestrator writes it.
- `.squad/project.md` lines 89, 90 and 99: `release/11.0` and ".NET 11 `RegistryKey` source" (fact 10). The Tester reports the text, and the orchestrator writes it.
- `tests/Censio.Pipe.Tests/ControlledWriteStream.cs` line 10: `release/11.0`, by the Tester.
- The sweep of `docs/ARCHITECTURE.md`, `docs/UNIT_TESTS.md` and `docs/protocol.md` found no hit.

## Architecture check

No guarantee in `.squad/project.md` changes:
- Plain HTTP, `/health` and the listener stay as they are.
- The image keeps port 8080, user 1654 and `/config` (`image-check.sh`).
- The key ring stays readable in both directions (AC6).
- `docs/ARCHITECTURE.md` names no .NET version, so it needs no update.

## Security considerations

- [x] No limits: no byte or length limit changes. The speed range 0.1–1000 is checked on the same `double` as today.
- [x] No exception reaches a user-visible text. The `decimal.TryParse` runs only on a text the `double` check already accepted, and it cannot fail there (fact 5).
- The image pins stay digest pins of single-arch manifests (record 0010).
- The release scripts change only one path literal.
- Removing the package reference takes the assembly from the shared framework, which Microsoft ships and the image and the self-contained ZIP already carry.

## Decision records

- `docs/decisions/0054-move-to-dotnet-11-sts-before-dotnet-12.md` (Proposed). It amends 0016, which stays unedited.

## Out of scope / follow-ups

- The squad issue for step 12: "`dotnet_major.py` misses target-framework paths in scripts". Body: `release-check.sh:27` and `release-check-test.sh:290` named `net10.0-windows`, the check passed, and the release check broke only once the build ran. Proposal: scan `.github/scripts/*.sh` (and `*.yml`) for `net<N>.0` and compare N with the repository's major.
- As in the spec.
