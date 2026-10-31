# Spec: CI, CodeQL, Dependabot and SonarQube Cloud

Source: Issue #3
Status: Approved by Lead

## Problem

Maintainers and outside contributors need every pull request checked automatically, as in the other .NET
repositories of the owner. Most of the files already exist on `main` (from the template and #2), but against
the issue they fall short:

- `.github/workflows/ci.yml` has no format check, so a formatting slip that skipped the local *Format* reaches
  `main` unnoticed (record 0001 relies on the local gate alone). The formatter is installed unpinned
  (`dotnet tool install -g Reihitsu.Cli`, *Toolchain* in `.squad/stack.md`), so nothing keeps a CI formatter, the
  local one and `Reihitsu.Analyzer` (pinned in `Directory.Packages.props`) on one release.
- Its SonarQube steps point at the SonarQube Cloud organisation `larslaskowski`, which does not exist
  (`api/organizations/search` returns no such organisation); the issue names `networlddev`, which exists and
  holds the owner's other projects. No project `LarsLaskowski_Censio` exists yet and the CI run on `main` of
  2026-10-31 skipped every SonarQube step (no `SONAR_TOKEN`), so no pull request gets a SonarQube check today.
- `SONAR_TOKEN` sits in the job-level `env`, so restore, build and test code (repository and NuGet package
  code) run with the token in their environment, and `actions/checkout` keeps the `checks: write` token in
  `.git/config` (issue comment; confirmed in `ci.yml`, where `persist-credentials` is not set).
- `docs/CONTRIBUTING.md` does not describe the checks.

Already done: `codeql.yml`, `dependabot.yml`, `fork-scope.yml` and `release.yml` as the issue asks (apart from
AC7 and AC8 below), the test report, and the Companion built on Linux as part of `Censio.slnx` (record 0004).

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
- S4: Given outdated NuGet packages (the repository's .NET tools included), GitHub Actions or Docker base images,
  when Dependabot's weekly version-update run finds them, then it opens one pull request per ecosystem that
  bundles that ecosystem's version updates, and CI checks it like S1 except for SonarQube and the test report
  (E3). A security advisory still arrives as its own Dependabot security-update pull request, as soon as it is
  published (security updates are enabled on the repository).
- S5: Given a pull request from a fork, when it is opened, then the *Fork scope* check runs, and CI — once a
  maintainer approves the run, as GitHub requires for a first-time contributor — runs restore, format check,
  build and tests without repository secrets, with SonarQube and the test report skipped (E3).
- S6: Given merged pull requests labelled `breaking-change`, `enhancement`/`feature`, `security` or `bug`, when
  the owner clicks *Generate release notes* on a release, then each pull request is listed under its category
  (*Breaking changes*, *Features*, *Security fixes*, *Bug fixes*), any other under *Other changes*, and one
  labelled `ignore-for-release` is left out.
- S7: Given a contributor preparing a pull request, when they read `docs/CONTRIBUTING.md`, then they find each
  check (CI, CodeQL, SonarQube Cloud, Dependabot, Fork scope), when it runs, what turns it red, which ones are
  skipped on forks and Dependabot pull requests, that a first-time contributor's run waits for a maintainer's
  approval, and the local command from `.squad/stack.md` that reproduces it.

## Errors and edge cases

- E1: A pull request with a file `reihitsu-format` would change: the *build* job fails at the format check, its
  log names the file (`Not formatted: <path>`) and ends `<n> of <m> file(s) need formatting.`. Build, tests,
  SonarQube begin and end, the test report and the artifact upload do not run, so the format check is the job's
  only error.
- E2: A pull request with a failing test: the *build* job fails at the test step; SonarQube end, the test report
  (where it runs, E3) and the artifact upload still run, and the report names the failed test.
- E3: No `SONAR_TOKEN` (a fork or Dependabot pull request, or the secret not set): the SonarQube steps are
  skipped, every other step runs, and CI is green when they pass; on fork and Dependabot pull requests the test
  report is skipped too, because their token cannot create check runs.
- E4: `SONAR_TOKEN` set but rejected, or SonarQube Cloud unreachable: CI fails at the SonarQube step that hit it
  (fails closed), never reports green with the analysis silently missing.
- E5: Code of the pull request (a test, an MSBuild target, a NuGet package's build script) reads its environment
  and `.git/config`: it finds neither `SONAR_TOKEN` nor a GitHub token. Residual exposure, accepted for Security's
  plan review to weigh: that code runs in the same job before SonarQube's end step (the .NET scanner needs begin,
  build and end in one job), so it could append to `$GITHUB_ENV` or `$GITHUB_PATH` or replace the scanner binary
  and reach the token in that later step. Only maintainers' branches and pushes to `main` get the secret; fork
  and Dependabot runs get none.
- E6: A compile error in the Windows-only Companion: the *build* job fails on the Linux runner at the build step.
- E7: SonarQube's quality gate fails: the *SonarCloud Code Analysis* check is red; the CI *build* job's result
  does not depend on it.
- E8: No Dockerfile exists yet (it arrives with #4): Dependabot's Docker entry opens no pull request and no
  check of a pull request fails because of it; once the Dockerfile exists in the directory `.squad/stack.md`
  names (`src/Censio.Server/`), its base images are updated weekly.
- E9: A pull request label `release.yml` does not know: the pull request is listed under *Other changes*.
- E10: A Dependabot pull request raises `Reihitsu.Analyzer` and the formatter together, and the new formatter
  formats the code differently: the format check turns that pull request red (E1). A maintainer checks out its
  branch, runs *Format*, and pushes the result to it; `docs/CONTRIBUTING.md` says so.

## Non-functional requirements

- Security: no workflow- or job-level `env` holds the value of `SONAR_TOKEN`; it is in the environment only of
  the steps that pass it to SonarQube or decide whether they run. No `actions/checkout` in `ci.yml` or
  `codeql.yml` persists credentials. Workflow and job permissions stay at least as narrow as today
  (`contents: read`, `checks: write` for the test report, CodeQL's `security-events: write` on its job); every
  action stays pinned to a commit SHA.
- One pinned version of each .NET tool CI runs (formatter, SonarQube scanner), in one place in the repository,
  read by CI and by the local *Format*, *Format check* and *Sonar gate*; Dependabot raises the formatter in the
  same pull request as `Reihitsu.Analyzer` (both are NuGet packages released in lockstep), and a raised version
  takes effect on the next run (no cache keeps the old one).
- SonarQube Cloud: organisation `networlddev`, project key `LarsLaskowski_Censio` (the key SonarQube Cloud
  derives for the GitHub repository `LarsLaskowski/Censio`, as for the owner's `LarsLaskowski_DockerUpdateGuard`
  and `LarsLaskowski_PiMonitor` in the same organisation). The same values are used by the *Sonar gate* in
  `.squad/stack.md`.
- Prerequisites outside the repository, done by the owner (not files of this change): the SonarQube Cloud
  project bound to the repository with automatic analysis off, the repository secret `SONAR_TOKEN`, and the
  labels `breaking-change`, `security` and `ignore-for-release`, which the repository does not have yet
  (`enhancement`, `bug` exist).
- `docs/CONTRIBUTING.md` is template-managed outside its `<!-- project:… -->` blocks (`scope-check.py`), so
  the new text goes into project blocks only.
- Time: the format check adds no more than about a minute to the *build* job.
- No new product configuration, logs or runtime behavior; the Docker image and release workflows are #4–#6.

## Acceptance criteria

- [ ] AC1: `ci.yml` runs on push to `main` and on pull requests against `main` with, in order, restore, format
  check (*Format check* of `.squad/stack.md`), build of `Censio.slnx` (Companion included), tests with coverage
  in the OpenCover format SonarQube reads, SonarQube begin/end around build and tests, the test report and the
  artifact upload (S1, S2, E6).
- [ ] AC2: the format check fails the job on an unformatted C# file and names it in the log, and none of the
  later steps runs or reports an error of its own (E1).
- [ ] AC3: a failing test fails the job; SonarQube end, the test report and the artifact upload still run, and
  the report names the test (E2).
- [ ] AC4: without `SONAR_TOKEN` the SonarQube steps are skipped and the rest runs; on fork and Dependabot pull
  requests the test report is skipped (S5, E3).
- [ ] AC5: the SonarQube steps use organisation `networlddev` and key `LarsLaskowski_Censio`, and with a token a
  rejected token or an unreachable server fails the job (E4, E7).
- [ ] AC6: no workflow- or job-level `env` of `ci.yml` holds the value of `SONAR_TOKEN`: it appears only in the
  `env` of the SonarQube steps and of the step that decides whether they run (a step with the secret in its
  `env` that writes only a boolean output, or a job-level `env` holding only the boolean
  `secrets.SONAR_TOKEN != ''`); every `actions/checkout` in `ci.yml` and `codeql.yml` sets
  `persist-credentials: false` (E5).
- [ ] AC7: CodeQL analyses C# and Actions on push to `main`, on pull requests and weekly, and the C# analysis
  runs unconditionally: the *Detect C# sources* step, which the workflow's own comment says to remove once
  sources exist, is gone, so the check cannot turn green without an analysis (S3).
- [ ] AC8: `dependabot.yml` runs version updates for NuGet, GitHub Actions and Docker weekly, each ecosystem
  grouped into one pull request, the NuGet entry covers the pinned .NET tools, and the Docker entry covers
  `src/Censio.Server/` (S4, E8).
- [ ] AC9: the formatter and the SonarQube scanner have one pinned version each in the repository, which CI and
  the commands *Format*, *Format check* and *Sonar gate* of `.squad/stack.md` use, and a raised version is the
  one the next CI run executes (E10).
- [ ] AC10: `release.yml` sorts pull requests by label as S6 describes, and a project block of
  `docs/CONTRIBUTING.md` names every label `release.yml` uses, `feature` included (S6, E9).
- [ ] AC11: `docs/CONTRIBUTING.md` describes the checks as S7 says and the formatter-bump case of E10, all inside
  `<!-- project:… -->` blocks (`scope-check.py` passes), and the *Sonar gate* in `.squad/stack.md` names
  organisation `networlddev` (S7, E10).
- [ ] AC12: on this feature's pull request the CI, CodeQL and SonarQube checks are green, and a throwaway
  commit with a broken format and one with a failing test each turn CI red (S1; verified only in CI, after the
  prerequisites above).

## Out of scope

- A Windows runner job for the Companion: it builds on Linux (record 0004), the issue calls the job optional.
- Making the checks required in branch protection (a repository setting).
- Creating the SonarQube Cloud project, the secret and the labels (owner prerequisites above).
- Changes to `fork-scope.yml`, which comes from the template and already does what the issue asks.
- CI steps for the analyzer and coverage gates (record 0001 keeps them local).
- The template text of `docs/CONTRIBUTING.md` outside its project blocks (label sentence without `feature`, fork
  note without the approval of first-time runs) and the SessionStart hook's unpinned global formatter install:
  squad files, reported as squad lessons.

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
  prints `Not formatted: <path>` (scratch test); the branch passes today.
- Q6: Does CI's test step write `coverage.opencover.xml` today? Answered: not by reading (the artifact could not
  be downloaded); the plan marks it unverified, owner Dev.

## Area documents

None: development and release process are not areas (`docs/areas/README.md`). The checks are described in
`docs/CONTRIBUTING.md` (inside project blocks) and the SonarQube values and tool commands in `.squad/stack.md`.

## Challenge

- M1 (formatter version drifts from the analyzer): accepted. New NFR and AC9: one pinned version per tool, used
  by CI and the local commands, raised by Dependabot together with `Reihitsu.Analyzer` (AC8 covers the tools);
  new E10 for an analyzer bump whose format check turns red. The mechanism (the DA's tool manifest
  `.config/dotnet-tools.json`, which the SessionStart hook already restores) is the plan's choice; that
  Dependabot's NuGet ecosystem reads a tool manifest is a claim the plan marks unverified.
- M2 (CONTRIBUTING.md is template-managed outside its blocks): accepted. NFR and AC11 require the text inside
  project blocks with `scope-check.py` passing; AC10 now asks a project block, not the template sentence, to name
  every label, `feature` included. A new named block inserted on a line boundary leaves the text outside the
  blocks unchanged (`PROJECT_BLOCK` in `scope-check.py`), but whether a template refresh keeps an unknown block
  could not be checked, so the plan prefers the existing `getting-started` and `releases` blocks. The template
  sentence and fork note are squad lessons (*Out of scope*).
- m1 (`!cancelled()` steps after a format failure): accepted; E1, E2, AC2 and AC3 name which steps run.
- m2 (static scanner cache key): accepted; NFR and AC9 require a raised version to take effect on the next run.
- m3 (token-check mechanism open): accepted; AC6 names the two allowed mechanisms.
- m4 (E5 overstates the scoping): accepted; E5 states the residual exposure for Security's plan review.
- m5 (security updates are separate pull requests): accepted; `dependabot_security_updates` is `enabled`
  (repository API). S4 and AC8 speak of version updates; security updates stay separate on purpose.
- m6 (first-time fork runs wait for approval): accepted; S5 and S7 say so.
- Unverified OpenCover output: accepted as Q6 and in AC1.
