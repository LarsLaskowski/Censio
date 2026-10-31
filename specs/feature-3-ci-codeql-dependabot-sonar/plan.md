# Plan: CI, CodeQL, Dependabot and SonarQube Cloud

Source: Issue #3 | [spec.md](spec.md)
Status: Revised (1)
Tier: security — the change moves where a CI secret (`SONAR_TOKEN`) and the job's GitHub token are exposed, a change to the trust of the build (`.squad/routing.md`, *Tiers*).
Security areas touched: CI configuration (`.github/workflows/ci.yml` secrets, checkout credentials and permissions; `.github/workflows/codeql.yml` checkout credentials and its C# analysis condition), dependency update configuration (`.github/dependabot.yml`), build tooling pinned as dependencies (new `.config/dotnet-tools.json`)

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `ci.yml` has no format check and holds `SONAR_TOKEN` in the job-level `env` | `.github/workflows/ci.yml:16-21`, steps 22-118 | confirmed |
| 2 | No `actions/checkout` in `ci.yml` or `codeql.yml` sets `persist-credentials` | `ci.yml:29-32`, `codeql.yml:28`; `fork-scope.yml:29-31` already sets `false` | confirmed |
| 3 | SonarQube Cloud has no organisation `larslaskowski`; `networlddev` exists, has no project `LarsLaskowski_Censio` yet and holds `LarsLaskowski_DockerUpdateGuard`, `LarsLaskowski_PiMonitor` | `api/organizations/search?organizations=larslaskowski` → 0; `api/projects/search?organization=networlddev` | confirmed |
| 4 | `SONAR_TOKEN` is not set: the push run of CI on `main` (2026-10-31) skipped *Install scanner*, *Begin* and *End* | `gh api repos/LarsLaskowski/Censio/actions/runs/<id>/jobs` | confirmed |
| 5 | `reihitsu-format --check` exits 1 and prints `Not formatted: <path>`; the branch passes today | scratch copy with broken indentation; `reihitsu-format --check ./` → `0 of 19 file(s) need formatting.` | confirmed |
| 6 | `Reihitsu.Cli` and `Reihitsu.Analyzer` have identical version lists up to 1.1.0; `dotnet-sonarscanner` 12.0.0 exists (11.3.0 the fallback) | NuGet flat container `index.json` of each package (Lead, DA) | confirmed |
| 7 | No tool manifest exists; the SessionStart hook runs `dotnet tool restore` when `.config/dotnet-tools.json` exists and installs the global formatter only when none is on `PATH` | `git ls-files`; `.claude/hooks/session-start.sh:27-33` | confirmed |
| 8 | Dependabot's `nuget` ecosystem reads `.config/dotnet-tools.json` and raises its tools in the grouped NuGet pull request | dependabot-core `DiscoveryWorker.cs:105` (`DotNetToolsJsonDiscovery`), `MSBuildHelper.cs:989-993` (DA) | confirmed |
| 9 | CI's test command writes `coverage.opencover.xml` per test project (VSTest with `coverlet.collector` 10.1.0) | to verify: Dev in step 6 runs CI's *Test* command locally with `--results-directory` in a scratch path and lists the `coverage.opencover.xml` files (expect 6); the main run's artifact could not be downloaded | unverified |
| 10 | `dotnet-sonarscanner begin` does not write `sonar.token` under `.sonarqube/`; `end` needs it again as `/d:sonar.token` | SonarScanner for .NET `AnalysisConfigProcessorBase.cs:42`, `SonarProperties.cs:95-102`, `InitializationProcessor.cs:40-42`, `PostProcessor.cs:146` (Security) | confirmed |
| 11 | `secrets` is available in `jobs.<id>.env`, and a missing secret compares equal to `''` | to verify: `actionlint` in the *Analyzer gate* (step 7) checks context availability; AC12 run (with and without secret: this PR, a Dependabot PR) | unverified |
| 12 | Scanner 12.0.0 runs with JDK 17 (`actions/setup-java`, `ci.yml:23-27`) | to verify: AC12 CI run; on failure the Dev pins 11.3.0 | unverified |
| 13 | A rejected token or unreachable server makes `begin`/`end` exit non-zero; `end` does not wait for the quality gate unless `sonar.qualitygate.wait` is set | to verify: AC12 run on this PR (CI only: no session can present a rejected token through the agent proxy); design is fail closed — no `continue-on-error` | unverified |
| 14 | Dependabot security updates are enabled on the repository | `gh api repos/LarsLaskowski/Censio --jq .security_and_analysis` → `dependabot_security_updates: enabled` | confirmed |
| 14a | A Dependabot group without `applies-to` applies to version updates only | to verify: Dev in step 6 from Dependabot's configuration reference, with fact 8; if not, add `applies-to: version-updates` to each group | unverified |
| 15 | No Dockerfile exists; `.squad/stack.md` names `src/Censio.Server/Dockerfile` | `git ls-files`; `stack.md` *Commands*, *Image build check* | confirmed |
| 16 | `docs/CONTRIBUTING.md` is a marked file; text outside `<!-- project:… -->` blocks may not change in a product PR | `.squad/tools/scope-check.py:63-65,101-102,195-199` | confirmed |
| 17 | The repository has the labels `enhancement`, `bug`, not `breaking-change`, `security`, `ignore-for-release` | `gh api repos/LarsLaskowski/Censio/labels` | confirmed |
| 18 | Record 0001 is unreleased | `git ls-remote --tags origin` → no tags | confirmed |

## Acceptance criteria

The spec's AC1–AC12; none of the plan's own.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1–AC12 | none: no production or test code; see *Verification without tests* | — |

## Verification without tests

The change touches no production or test code (workflows, Dependabot configuration, a tool manifest,
documentation, records): steps 4 (*Skeleton*), 5 (*Tests first*) and the *Coverage gate* are not applicable.
Every edited workflow passes `actionlint` and the shell rules (*Analyzer gate*, run by the orchestrator in step 7);
the Dev runs `python3 .squad/tools/analyzer-check.py --lint-only` before handing back.

| AC | Verification | Owner, step |
| -- | ------------ | ----------- |
| AC1 | Read-only check of the step order in `ci.yml`; Dev runs locally, in that order, `dotnet tool restore`, *Restore*, *Format check*, *Build*, CI's *Test* command (fact 9); CI run on this PR | Dev 6; Reviewer 8; orchestrator 11 |
| AC2 | Dev runs `dotnet tool run reihitsu-format --check ./` on a scratch copy with one broken file (exit 1, `Not formatted:`); `if:` conditions read: nothing after the format check runs unless `steps.test.outcome` is `success`/`failure` or `steps.sonar-begin.outcome == 'success'`; throwaway commit in AC12 | Dev 6; Reviewer 8; orchestrator 11 |
| AC3 | `if:` conditions read (End, report, upload run after a failed test step); throwaway failing-test commit in AC12 | Reviewer 8; orchestrator 11 |
| AC4 | `if:` conditions read; a Dependabot pull request's CI after merge shows the SonarQube steps and the report skipped | Reviewer 8; orchestrator 11 (first Dependabot PR) |
| AC5 | `grep -n '/o:\|/k:' ci.yml .squad/stack.md` shows `networlddev` / `LarsLaskowski_Censio` only; no `continue-on-error`; fact 13 | Dev 6; Security 8; orchestrator 11 |
| AC6 | `grep -n 'SONAR_TOKEN' ci.yml` shows it only in `SONAR_ENABLED` (boolean expression), the `env` of *Begin* and *End* and their `/d:sonar.token="$SONAR_TOKEN"`; every checkout has `persist-credentials: false`; `grep -n 'actions/cache' ci.yml` finds nothing | Dev 6; Security 8 |
| AC7 | `codeql.yml` read: no *Detect C# sources* step, no `if:`; CodeQL checks on this PR | Reviewer 8; orchestrator 11 |
| AC8 | `dependabot.yml` read (fact 8 confirmed); first weekly Dependabot run after merge | Reviewer 8; orchestrator after merge |
| AC9 | `.config/dotnet-tools.json` lists `reihitsu.cli` 1.1.0 and `dotnet-sonarscanner` 12.0.0; `ci.yml` and `stack.md` *Format*, *Format check*, *Sonar gate* run them through the manifest; no `actions/cache` step remains; Dev runs the release-line check locally against the real files (pass) and against a scratch copy with `reihitsu.cli` set to 1.0.0 (fail, the error text below) | Dev 6; Reviewer 8 |
| AC10 | `release.yml` read (unchanged); the `releases` block of `CONTRIBUTING.md` names `breaking-change`, `enhancement`, `feature`, `security`, `bug`, `ignore-for-release` | Reviewer 8 |
| AC11 | `scope-check.py` (step 7) passes; Reviewer reads the new `getting-started` text against S7 and E10 | orchestrator 7; Reviewer 8 |
| AC12 | On the draft PR (after the owner's prerequisites): CI, CodeQL and *SonarCloud Code Analysis* green; one throwaway commit breaking the format and one with a failing test, each pushed and reverted, turn CI red | orchestrator 11 |

## Approach

1. **Tool manifest.** The Dev creates `.config/dotnet-tools.json` with `dotnet new tool-manifest`, then
   `dotnet tool install reihitsu.cli --version 1.1.0` and `dotnet tool install dotnet-sonarscanner --version 12.0.0`.
2. **`ci.yml`, one job `build`, steps in this order:** *Checkout* (`fetch-depth: 0`, `persist-credentials: false`);
   *Set up JDK 17* (as today); *Setup .NET*; *Restore
   tools* (`dotnet tool restore`); *Restore*; *Check tool versions* (new, below); *Format check*
   (`dotnet tool run reihitsu-format --check ./`); *Begin SonarQube analysis* (`id: sonar-begin`,
   `if: env.SONAR_ENABLED == 'true'`, `env: SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}`, `dotnet tool run
   dotnet-sonarscanner begin /k:"LarsLaskowski_Censio" /o:"networlddev"`, the other arguments as today, `/d:sonar.token="$SONAR_TOKEN"` included); *Build*;
   *Test* (`id: test`, command as today); *End SonarQube analysis* (`if: ${{ !cancelled() &&
   steps.sonar-begin.outcome == 'success' }}`, the same step `env`, `end /d:sonar.token="$SONAR_TOKEN"` as today); *Publish test report* (`if:` the current
   actor/fork condition `&& (steps.test.outcome == 'success' || steps.test.outcome == 'failure')`); *Upload test &
   coverage artifacts* (`if: ${{ !cancelled() && (steps.test.outcome == 'success' || steps.test.outcome ==
   'failure') }}`). Removed: the job-level `SONAR_TOKEN`, *Cache SonarQube Cloud packages*, *Cache SonarQube Cloud scanner* and
   *Install SonarQube Cloud scanner*: no `actions/cache` remains, so nothing the build can write is saved for a
   later run (Security B2, record 0008), and the manifest pins the scanner. Job-level `env` holds only
   `SONAR_ENABLED: ${{ secrets.SONAR_TOKEN != '' }}`, with the comment that it is a boolean and never the value.
   Workflow `permissions` unchanged. **Check tool versions** is a `run:` step that reads the `reihitsu.cli` version
   from `.config/dotnet-tools.json` (`jq`) and the `Reihitsu.Analyzer` `Version` from `Directory.Packages.props`,
   compares major.minor, and on a difference prints exactly
   `::error::Reihitsu.Cli <cli> (.config/dotnet-tools.json) and Reihitsu.Analyzer <analyzer> (Directory.Packages.props) are on different release lines; raise Reihitsu.Cli in the tool manifest`
   and exits 1; a missing value is the same failure with `<missing>` (fails closed). Its reason: Dependabot raises
   both in one grouped pull request (fact 8), but the analyzer and the formatter may not be published the same
   day, and a hand-made bump may raise only one.
3. **`codeql.yml`:** `persist-credentials: false` on the checkout; the *Detect C# sources* step, its comment and the
   two `if:` lines removed. Nothing else changes.
4. **`dependabot.yml`:** the `docker` entry's `directory` becomes `"/src/Censio.Server"`; nothing else (the `nuget`
   entry at `/` covers the manifest (fact 8); groups stay version-update groups, security updates separate).
5. **`release.yml`, `fork-scope.yml`:** unchanged.
6. **Outside the repository** (owner, before AC12; the orchestrator asks the Product Manager when the PR is
   drafted): SonarQube Cloud project `LarsLaskowski_Censio` in `networlddev`, bound to the repository, automatic
   analysis off; secret `SONAR_TOKEN`; labels `breaking-change`, `security`, `ignore-for-release`.

New user-visible output: the error line of *Check tool versions* (above, exact) and the `CONTRIBUTING.md` text
(layout under *Documentation updates*).

## Options considered

- **Formatter pin:** (a) `dotnet tool install -g Reihitsu.Cli --version` in a `run:` line — Dependabot never
  raises it, CI drifts from the analyzer (spec M1); (b) tool manifest without a guard — a bump that raises only one package (published on different days, or by
  hand) goes unnoticed; (c) **tool manifest plus a release-line check in CI** — chosen. Recorded in 0001.
- **Caches:** a version in the scanner cache key; a `~/.sonar/cache` restore before *Begin* and save right after it;
  versus **no `actions/cache` at all** — chosen: no entry saved after build code ran (or by an earlier workflow
  under the same key) can be restored and executed, and the downloads cost seconds. Recorded in 0008.
- **Token check:** a step with the secret writing a boolean output versus **a job-level boolean
  `secrets.SONAR_TOKEN != ''`** — chosen: no extra step holds the secret. Separate SonarQube job: impossible
  (begin/build/end in one job). Recorded in 0008.
- **CONTRIBUTING placement:** a new named project block (passes `scope-check.py`, but whether a template refresh
  keeps an unknown block is unknown) versus **the existing `getting-started` and `releases` blocks** — chosen.
- **Windows runner for the Companion:** rejected, record 0004 and spec *Out of scope*.
- **Docker entry:** `directories: ["/", "/src/Censio.Server"]` versus **`/src/Censio.Server` alone** — chosen: the
  root will never hold a Dockerfile (`stack.md`).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `.config/dotnet-tools.json` | new: `reihitsu.cli` 1.1.0, `dotnet-sonarscanner` 12.0.0 |
| — | `.github/workflows/ci.yml` | rewritten as Approach 2 |
| — | `.github/workflows/codeql.yml` | Approach 3 |
| — | `.github/dependabot.yml` | Approach 4 |
| — | `.squad/stack.md`, `docs/CONTRIBUTING.md` | *Documentation updates* |
| — | `docs/decisions/0001-…`, `0008-…`, `README.md` | Lead, done in step 2 |

## Signatures (for the Dev's skeleton)

none

## Test files

none (no production or test code). Existing test call sites affected: none. Files no unit test can cover:
`ci.yml`, `codeql.yml`, `dependabot.yml`, `.config/dotnet-tools.json` — configuration that only GitHub runs; they
are not in the coverage measure (`COVERAGE_PATHSPECS` is `src/*.cs`, `src/*.razor`), so no exclusion and no record
follows.

## Areas

none: development and release process are not areas (`docs/areas/README.md`).

## Documentation updates

Sweep: `grep` for `larslaskowski`, `/o:`, `sonarscanner`, `reihitsu-format`, `Reihitsu.Cli`, `dotnet-tools` over
`*.md`, `*.yml`, `*.py`, `*.sh` outside `specs/`; `docs/ARCHITECTURE.md` has no CI enumeration. Hits outside the
files below are squad files (`AGENTS.md:80-82` and its mirror, `.claude/hooks/session-start.sh:27-28`): squad
lessons, not edited. All owned by the Dev, written once the workflows are final:

- `.squad/stack.md` *Toolchain*: the formatter and `dotnet-sonarscanner` come from `.config/dotnet-tools.json`
  (`dotnet tool restore`), formatter on `Reihitsu.Analyzer`'s release line, which CI enforces.
- `.squad/stack.md` *Commands*: *Format* `dotnet tool run reihitsu-format --force ./`; *Format check*
  `dotnet tool run reihitsu-format --check ./`; *Sonar gate* `/o:"networlddev"` (rest unchanged).
- `.squad/stack.md` *Sonar gate* section (lines 84-87): the scanner comes from the tool manifest (drop the
  `dotnet tool install` alternative); *Known pitfalls* `DOTNET_ROOT` item: keep only if `dotnet tool run
  reihitsu-format` still needs it (Dev checks with `DOTNET_ROOT` unset).
- `docs/CONTRIBUTING.md`, `getting-started` block: *Machine setup* — formatter from the tool manifest
  (`dotnet tool restore`); a new `### Automated checks` subsection after *Running tests*, as a bullet list in this
  order — CI (`build` job: steps, triggers, what turns it red, the *Format check* command that reproduces it), test
  report, SonarQube Cloud (organisation `networlddev`, skipped without the secret, quality gate as its own check),
  CodeQL (C#, Actions; pushes, pull requests, weekly), Dependabot (weekly grouped version updates per ecosystem,
  security updates separate), Fork scope — then one paragraph on fork and Dependabot pull requests (no secrets:
  SonarQube and the report skipped; a first-time contributor's run waits for a maintainer's approval) and one on
  E10 (a red format check on a Dependabot analyzer bump: review the Dependabot diff first, since a push to its
  branch gives that run the secrets while the raised packages build; then check out the branch, run *Format*,
  push; a raised `dotnet-sonarscanner` gets no SonarQube run on a Dependabot pull request, so before merging a
  maintainer pushes a commit to its branch or runs the *Sonar gate* locally; a red *Check tool versions*: raise
  the package that is behind).
- `docs/CONTRIBUTING.md`, `releases` block: one sentence on release notes — labels `breaking-change`, `enhancement`
  or `feature`, `security`, `bug` decide the section, others land under *Other changes*, `ignore-for-release` is
  left out (`.github/release.yml`).

## Architecture check

No guarantee in `.squad/project.md` (none recorded) or `docs/ARCHITECTURE.md` is touched; product code and its
build are unchanged. The Companion keeps building on Linux (record 0004).

## Security considerations

- [ ] Byte or length limits on parsed input: no limits (no parser).
- [ ] Exceptions reaching a user-visible message: none; the only new message is the *Check tool versions* line.
- [ ] `SONAR_TOKEN` in `env` of *Begin* and *End* only; job `env` holds a boolean; no `actions/checkout` persists
  credentials; no `actions/cache`; permissions unchanged; actions stay SHA-pinned; tools pinned by exact version.
- [ ] Step scoping guards against accidental disclosure only; malicious code in the job (a compromised dependency
  on `main`) can still reach both tokens (spec E5, record 0008), accepted. The token is not on disk (fact 10).

## Decision records

- `docs/decisions/0001-quality-gates-before-the-pull-request.md` (Accepted, unreleased, edited in place: format
  check in CI, tools pinned in the manifest with the release-line check)
- `docs/decisions/0008-ci-secrets-scoped-to-the-steps-that-use-them.md` (Proposed)

## Security review

- B1 (E5 and 0008 understate the residual exposure): accepted. Spec E5 and record 0008 now name runner memory
  (passwordless `sudo`, secrets delivered at job start), `/proc/<pid>` of the SonarQube steps, and tampering with
  later steps and actions; the `checks: write` `GITHUB_TOKEN` is reachable the same way; pushes to `main` and a
  compromised NuGet dependency are the exposed case. Step scoping is justified as protection against accidental
  disclosure; option 2 is kept on that basis.
- B2 (poisonable `~/.sonar/cache`): accepted, option (b): every `actions/cache` step leaves `ci.yml` (Approach 2,
  AC6 verification); record 0008 states that nothing the build can write is cached for a later run. Option (a)
  lost because an entry saved under the same key by the current workflow could still be restored.
- N1: fact 10 confirmed with Security's sources; the Dev's `.sonarqube/` grep is dropped; Approach 2 keeps
  `/d:sonar.token` on both *Begin* and *End*.
- N2, N3, N5, N6: no change (fact 8 confirmed, see *Challenge*).
- N4: accepted; the CONTRIBUTING E10 paragraph says to review the Dependabot diff before pushing to its branch,
  spec E10 says so, and 0008 says `SONAR_TOKEN` is never added as a Dependabot secret.

## Challenge

- 1 (facts 6 and 8 confirmed; justify the guard otherwise): accepted; facts 6 and 8 confirmed with the DA's
  sources, the fact-8 research left task 1, *Check tool versions* and record 0001 now justify the guard by
  packages published on different days or a hand-made bump.
- 2 (`checks: write` token reachable like `SONAR_TOKEN`): accepted, in 0008 *Consequences* (with B1).
- 3 (a raised scanner is never run with a token before merge): accepted; the CONTRIBUTING E10 paragraph and spec
  E10 say a maintainer pushes a commit to the Dependabot branch or runs the *Sonar gate* locally first.

## Out of scope / follow-ups

- Spec *Out of scope*. Squad lessons for step 12: `AGENTS.md`/`copilot-instructions.md` *Commands* still say the
  formatter is installed globally; the SessionStart hook installs an unpinned global formatter; the template text
  of `CONTRIBUTING.md` omits `feature` and the approval of first-time fork runs.

## Security notes

Non-blocking findings of Security's round 2 (plan review), appended verbatim:

- N-a: The AC6/AC9 check `grep -n 'actions/cache' ci.yml` would not catch a cache turned on through the `cache:` input of `actions/setup-java` or `actions/setup-dotnet`. That would cache build-writable paths (`~/.m2`, `~/.nuget/packages`) through the same store, which goes against the 0008 rule "nothing the build can write is cached". Neither action sets `cache:` today (ci.yml:23-36), and the plan keeps both "as today". Suggest the Dev also confirms no `cache:` input is added, and Security will check this in the step-8 diff.
- N-b: In 0008 *Options considered*, the scanner-cache sentence ("a restore before `begin` and a save directly after it, before the build, against **no cache** — chosen") is easy to misread about which option was chosen. The bold marks "no cache", which matches *Decision*. A wording clarification is optional.
