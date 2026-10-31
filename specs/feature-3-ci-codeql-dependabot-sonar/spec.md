# Spec: CI, CodeQL, Dependabot and SonarQube Cloud

Source: Issue #3
Status: Draft

## Problem

Maintainers and outside contributors need every pull request checked automatically, as in the other .NET
repositories of the owner. Most of the files already exist on `main` (from the template and #2), but against
the issue they fall short:

- `.github/workflows/ci.yml` has no format check, so a formatting slip that skipped the local *Format* reaches
  `main` unnoticed (record 0001 relies on the local gate alone).
- Its SonarQube steps point at the SonarQube Cloud organisation `larslaskowski`, which does not exist
  (`api/organizations/search` returns no such organisation); the issue names `networlddev`, which exists and
  holds the owner's other projects. No project `LarsLaskowski_Censio` exists yet and the CI run on `main` of
  2026-10-31 skipped every SonarQube step (no `SONAR_TOKEN`), so no pull request gets a SonarQube check today.
- `SONAR_TOKEN` sits in the job-level `env`, so restore, build and test code (repository and NuGet package
  code) run with the token in their environment, and `actions/checkout` keeps the `checks: write` token in
  `.git/config` (issue comment; confirmed in `ci.yml`, where `persist-credentials` is not set).
- `docs/CONTRIBUTING.md` does not describe the checks.

Confirmed as already done, against the issue: `codeql.yml` (C# and Actions, on push, pull request and weekly),
`dependabot.yml` (NuGet, GitHub Actions and Docker, weekly, each grouped), `fork-scope.yml`, `release.yml`
(categories by label), the test report, the coverage collection, and the Companion built on Linux with
`EnableWindowsTargeting` and `RuntimeIdentifier` `win-x64` as part of `Censio.slnx` (record 0004).

## Scenarios

- S1: Given a maintainer's pull request against `main` with formatted code that builds and passes its tests,
  when it is opened or updated, then it shows the green checks *CI / build*, *dotnet tests* (the test report),
  *CodeQL / Analyze (csharp)*, *CodeQL / Analyze (actions)* and SonarQube Cloud's *SonarCloud Code Analysis*
  with the quality gate result, and the *build* log shows, in this order, the restore, the format check, the
  build (which writes `Censio.Companion.exe`), the tests with coverage and the SonarQube analysis.
- S2: Given a push to `main` (a merged pull request), when CI and CodeQL run, then both are green and SonarQube
  Cloud shows a new analysis of the `main` branch with coverage.
- S3: Given no push for a week, when the weekly schedule fires (Sunday 01:30 UTC), then CodeQL analyses `main`
  for C# and Actions and reports new alerts under *Security > Code scanning*.
- S4: Given outdated NuGet packages, GitHub Actions or Docker base images, when Dependabot's weekly run finds
  them, then it opens at most one pull request per ecosystem that bundles that ecosystem's updates, and CI
  checks it like S1 except for SonarQube and the test report (E3).
- S5: Given a pull request from a fork, when it is opened, then the *Fork scope* check runs, CI runs restore,
  format check, build and tests without repository secrets, and SonarQube and the test report are skipped
  (E3).
- S6: Given merged pull requests labelled `breaking-change`, `enhancement`/`feature`, `security` or `bug`, when
  the owner clicks *Generate release notes* on a release, then each pull request is listed under its category
  (*Breaking changes*, *Features*, *Security fixes*, *Bug fixes*), any other under *Other changes*, and one
  labelled `ignore-for-release` is left out.
- S7: Given a contributor preparing a pull request, when they read `docs/CONTRIBUTING.md`, then they find each
  check (CI, CodeQL, SonarQube Cloud, Dependabot, Fork scope), when it runs, what turns it red, which ones are
  skipped on forks and Dependabot pull requests, and the local command from `.squad/stack.md` that reproduces it.

## Errors and edge cases

- E1: A pull request with a file `reihitsu-format` would change: the *build* job fails at the format check, its
  log names the file (`Not formatted: <path>`) and ends `<n> of <m> file(s) need formatting.`, and the later
  steps do not run.
- E2: A pull request with a failing test: the *build* job fails at the test step, and the *dotnet tests* report
  names the failed test (where the report runs, E3).
- E3: No `SONAR_TOKEN` (a fork or Dependabot pull request, or the secret not set): the SonarQube steps are
  skipped, every other step runs, and CI is green when they pass; on fork and Dependabot pull requests the test
  report is skipped too, because their token cannot create check runs.
- E4: `SONAR_TOKEN` set but rejected, or SonarQube Cloud unreachable: CI fails at the SonarQube step that hit it
  (fails closed), never reports green with the analysis silently missing.
- E5: Code of the pull request (a test, an MSBuild target, a NuGet package's build script) reads its environment
  and `.git/config`: it finds neither `SONAR_TOKEN` nor a GitHub token.
- E6: A compile error in the Windows-only Companion: the *build* job fails on the Linux runner at the build step.
- E7: SonarQube's quality gate fails: the *SonarCloud Code Analysis* check is red; the CI *build* job's result
  does not depend on it.
- E8: No Dockerfile exists yet (it arrives with #4): Dependabot's Docker entry opens no pull request and no
  check of a pull request fails because of it; once the Dockerfile exists in the directory `.squad/stack.md`
  names (`src/Censio.Server/`), its base images are updated weekly.
- E9: A pull request label `release.yml` does not know: the pull request is listed under *Other changes*.

## Non-functional requirements

- Security: `SONAR_TOKEN` is in the environment only of the steps that pass it to SonarQube; no `actions/checkout`
  in `ci.yml` or `codeql.yml` persists credentials; workflow and job permissions stay at least as narrow as
  today (`contents: read`, `checks: write` for the test report, CodeQL's `security-events: write` on its job);
  every action stays pinned to a commit SHA, and every tool CI installs (formatter, scanner) is pinned to a
  version.
- The formatter in CI is on the release line of `Reihitsu.Analyzer` in `Directory.Packages.props` (`1.1.x`
  today, *Toolchain* in `.squad/stack.md`), so CI and the local *Format* agree.
- SonarQube Cloud: organisation `networlddev`, project key `LarsLaskowski_Censio` (the key SonarQube Cloud
  derives for the GitHub repository `LarsLaskowski/Censio`, as for the owner's `LarsLaskowski_DockerUpdateGuard`
  and `LarsLaskowski_PiMonitor` in the same organisation). The same values are used by the *Sonar gate* in
  `.squad/stack.md`.
- Prerequisites outside the repository, done by the owner (not files of this change): the SonarQube Cloud
  project bound to the repository with automatic analysis off, the repository secret `SONAR_TOKEN`, and the
  labels `breaking-change`, `security` and `ignore-for-release`, which the repository does not have yet
  (`enhancement`, `bug` exist).
- Time: the format check adds no more than about a minute to the *build* job.
- No new product configuration, logs or runtime behavior; the Docker image and release workflows are #4–#6.

## Acceptance criteria

- [ ] AC1: `ci.yml` runs on push to `main` and on pull requests against `main` with, in order, restore, format
  check (*Format check* of `.squad/stack.md`), build of `Censio.slnx` (Companion included), tests with coverage,
  SonarQube begin/end around build and tests, the test report and the artifact upload (S1, S2, E6).
- [ ] AC2: the format check fails the job on an unformatted C# file and names it in the log (E1).
- [ ] AC3: a failing test fails the job, and the test report names it where it runs (E2).
- [ ] AC4: without `SONAR_TOKEN` the SonarQube steps are skipped and the rest runs; on fork and Dependabot pull
  requests the test report is skipped (S4, S5, E3).
- [ ] AC5: the SonarQube steps use organisation `networlddev` and key `LarsLaskowski_Censio`, and with a token a
  rejected token or an unreachable server fails the job (E4, E7).
- [ ] AC6: `SONAR_TOKEN` appears only in the `env` (or `with`) of the steps that pass it to SonarQube or decide
  whether they run, and every `actions/checkout` in `ci.yml` and `codeql.yml` sets `persist-credentials: false`
  (E5).
- [ ] AC7: CodeQL analyses C# and Actions on push to `main`, on pull requests and weekly, and the C# analysis
  runs unconditionally: the *Detect C# sources* step, which the workflow's own comment says to remove once
  sources exist, is gone, so the check cannot turn green without an analysis (S3).
- [ ] AC8: `dependabot.yml` updates NuGet, GitHub Actions and Docker weekly, each ecosystem grouped into one pull
  request, and the Docker entry covers `src/Censio.Server/` (S4, E8).
- [ ] AC9: `release.yml` sorts pull requests by label as S6 describes, and `docs/CONTRIBUTING.md` names the same
  labels (S6, E9).
- [ ] AC10: `docs/CONTRIBUTING.md` describes the checks as S7 says, and the *Sonar gate* in `.squad/stack.md`
  names organisation `networlddev` (S7).
- [ ] AC11: on this feature's pull request the CI, CodeQL and SonarQube checks are green, and a throwaway
  commit with a broken format and one with a failing test each turn CI red (S1; verified only in CI, after the
  prerequisites above).

## Out of scope

- A Windows runner job for the Companion: it builds on Linux (record 0004), the issue calls the job optional.
- Making the checks required in branch protection (a repository setting).
- Creating the SonarQube Cloud project, the secret and the labels (owner prerequisites above).
- Changes to `fork-scope.yml`, which comes from the template and already does what the issue asks.
- CI steps for the analyzer and coverage gates (record 0001 keeps them local).

## Open questions

- Q1: Which SonarQube Cloud organisation, `networlddev` (issue) or `larslaskowski` (`ci.yml`, `.squad/stack.md`)?
  Answered: `networlddev`. SonarQube Cloud has no organisation `larslaskowski`; `networlddev` exists and holds
  the owner's projects, and the issue names it.
- Q2: Which project key? Answered: `LarsLaskowski_Censio`, the key SonarQube Cloud derives on import of
  `LarsLaskowski/Censio` and the one the owner's other personal repositories use in `networlddev`; the owner
  creates the project with it (prerequisite).
- Q3: Does a format check in CI contradict record 0001 (format runs locally, CI keeps build, tests and
  analysis)? Answered: the issue, written by the owner, asks for it; 0001 is unreleased (no tags on `origin`) and
  its own consequences name "adding a CI step back" as the remedy, so the plan changes 0001 in place rather than
  conflicting with it.
- Q4: Is the Windows build of the Companion needed? Answered: no; it builds on the Linux runner (record 0004,
  issue comment).
- Q5: Does the formatter's check exit non-zero? Answered: yes; `reihitsu-format --check` (1.1.0) exits 1 and
  prints `Not formatted: <path>` per file, checked in a scratch directory; the branch itself passes today
  (`0 of 19 file(s) need formatting.`).

## Area documents

None: development and release process are not areas (`docs/areas/README.md`). The checks are described in
`docs/CONTRIBUTING.md` (new section on automated checks, next to *Quality gates*, and the fork note) and the
SonarQube values in `.squad/stack.md` (*Sonar gate*).
