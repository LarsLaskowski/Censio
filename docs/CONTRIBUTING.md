# Contributing

<!-- project:begin getting-started -->
## Getting started

### Machine setup

Git, the .NET SDK from `.squad/stack.md` (*Toolchain*) and Python 3 for the gates under `.squad/tools/`. The
formatter `reihitsu-format` and the SonarQube scanner are pinned in `.config/dotnet-tools.json`; run
`dotnet tool restore` once after cloning (and again when a pull request changes the manifest). The Companion builds on any platform but runs on Windows only; the server runs anywhere .NET and Docker do.

### Cloning the repository

```shell
git clone https://github.com/LarsLaskowski/Censio.git
```

### Building and running

```shell
dotnet restore Censio.slnx
dotnet build Censio.slnx -c Release --no-restore
```

Every build, on Linux too, writes the Windows executable
`src/Censio.Companion/bin/<configuration>/net10.0-windows/win-x64/Censio.Companion.exe` (framework-dependent: it
needs the .NET 10 Desktop Runtime and runs only on Windows).

Start the server with:

```shell
dotnet run --project src/Censio.Server
```

It runs in `Production` and listens on `http://localhost:5000`; set `ASPNETCORE_ENVIRONMENT=Development` for
development.

### Running tests

```shell
dotnet test Censio.slnx -c Release --no-build
```

For detailed rules on how unit tests are structured and named, see [`UNIT_TESTS.md`](UNIT_TESTS.md).
**Unit tests are mandatory for newly written code** — see the checklist there before opening a pull
request.

### Automated checks

- **CI** (`.github/workflows/ci.yml`, job `build`): runs on every push to `main` and every pull request against
  `main`. It restores the tools and the solution, checks the tool versions, checks the formatting, builds
  `Censio.slnx` (the Companion included, on Linux), runs the tests with coverage and uploads the results. It
  turns red on an unformatted file (the log names it: `Not formatted: <path>`; reproduce with
  `dotnet tool run reihitsu-format --check ./`, fix with *Format*), a build error, a failing test, a failing
  SonarQube step, or a *Check tool versions* error (`Reihitsu.Cli` in `.config/dotnet-tools.json` and
  `Reihitsu.Analyzer` in `Directory.Packages.props` must be on the same major.minor release line).
- **Image check** (`.github/workflows/ci.yml`, job `image`): builds the server image from
  `src/Censio.Server/Dockerfile` on the runner and checks it by running it (`.github/scripts/image-check.sh`:
  labels, user, health check, ports, `VERSION`, the missing-Blazor-script failure, a clean and a dirty working
  tree). It pushes nothing and uses no secret. The log names the failed case (`image-check: <case>: <expected> /
  <actual>`). Locally, `hadolint src/Censio.Server/Dockerfile` and the *Image build check* of `.squad/stack.md`.
- **Base image check** (`.github/workflows/base-check.yml`): runs weekly (Wednesday 05:30 UTC), on demand
  (`workflow_dispatch`) and on pull requests that change the Dockerfile, `.github/scripts/base-check.sh` or the
  workflow. It reads the public tag list of `mcr.microsoft.com` anonymously and compares every `FROM` line of
  `src/Censio.Server/Dockerfile` with it. It turns red when a newer .NET patch of a pinned base exists only on a
  newer Alpine minor (for example `10.0.14-alpine3.24-amd64` while the pin is `10.0.12-alpine3.23-amd64`),
  because Dependabot proposes no such update; the log names the minor to move to (move both `FROM` lines and the
  base labels there, as in the *Dependabot* bullet). It also turns red, saying `cannot check`, when a `FROM`
  line is not a full-version `mcr.microsoft.com/dotnet` Alpine tag, the pinned tag is not in the registry's
  list, or the registry cannot be read. It uses no secret and its token is read-only (`contents: read`). A failed scheduled run notifies the user who last changed the workflow's `cron` line, and GitHub disables the schedule of a public repository after 60 days without activity (it is re-enabled on the workflow's page under *Actions*). Locally:
  `bash .github/scripts/base-check.sh [dockerfile]` from the repository root (needs `curl` and `jq`).
- **Release check** (`.github/workflows/ci.yml`, job `release-check`): runs the cases of
  `.github/scripts/release-check-test.sh` on every pull request: the tag forms, a commit on and one off `main`, the
  full-release warning, missing Docker Hub secrets, a Companion publish output with an extra file, and an exe and an
  image built with another version than expected; each case asserts the exact annotation. It builds the Companion
  and the server image with version `1.2.3` and pushes the image only to a throwaway registry on the runner (no
  secret, no Docker Hub). It uploads the Companion ZIP as artifact `companion-dry-run` (14 days). Locally:
  `bash .github/scripts/release-check-test.sh` from the repository root, with the Docker daemon running (needs
  `docker` with buildx, `curl`, `git`, `dotnet`, `python3`, `zip`). Start `Censio.Companion.exe` from the artifact
  of a pull request's own run on Windows only after reading the change; never start one from a fork's run.
- **Release** (`.github/workflows/release.yml`): runs when a release is published; see *Versioning and releases*.
  It can run for real only after it is merged to `main`, because GitHub runs the workflow of the tagged commit.
- **Test report** (check *dotnet tests*): lists the results of the test step, also when a test failed. It is
  skipped on fork and Dependabot pull requests, whose token cannot create check runs.
- **SonarQube Cloud** (organisation `networlddev`, project `networlddev_censio`; check *SonarCloud Code
  Analysis*): analyses build and tests inside the CI job and shows the quality gate as its own check, which does
  not decide the result of *CI*. It is skipped when the repository secret `SONAR_TOKEN` is not available (fork
  and Dependabot pull requests); a rejected token or an unreachable server fails the CI job. Locally the *Sonar
  gate* of `.squad/stack.md` runs it.
- **CodeQL** (`.github/workflows/codeql.yml`): analyses C# and the workflows on pushes to `main`, on pull
  requests and weekly (Sunday 01:30 UTC); its checks *Analyze (csharp)* and *Analyze (actions)* turn red when
  the analysis itself fails, and the alerts it finds appear under *Security > Code scanning*. No local command
  reproduces it.
- **Dependabot** (`.github/dependabot.yml`): weekly version updates for NuGet (the .NET tools of the manifest
  included), GitHub Actions and Docker (`src/Censio.Server/`), each ecosystem in one grouped pull request;
  security updates arrive separately. A base-image pull request fails the job `image` until the labels
  `org.opencontainers.image.base.name` and `…base.digest` in the Dockerfile are set by hand to the tag and digest of
  the new runtime `FROM` line. Major base-image updates are ignored until the target framework moves. A new
  Alpine minor (a new tag suffix such as `alpine3.25`) is a manual bump of both `FROM` lines and the labels;
  the *Base image check* reports when that bump is due.
- **Fork scope** (`.github/workflows/fork-scope.yml`): runs on pull requests from forks, with the check as it
  is on `main`, and turns red when one changes a squad or agent instruction file (`.squad/` except `stack.md`
  and `project.md`, `.claude/`, `CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md`), text outside the
  `<!-- project:… -->` blocks of a marked file or a working record under `specs/`, or adds a control character
  to a Markdown file; it does not check `.github/workflows/`. `python3 .squad/tools/scope-check.py --no-specs`
  reproduces it locally, except that the local run allows changes inside the project blocks of `AGENTS.md` and
  its mirror.

Pull requests from forks and Dependabot's get no repository secrets, so SonarQube and the test report are
skipped there while restore, format check, build and tests run. A first-time contributor's run waits until a
maintainer approves it.

When a Dependabot pull request raises `Reihitsu.Analyzer` and `Reihitsu.Cli` together and the new formatter
formats the code differently, the format check turns it red. A maintainer first reviews the Dependabot diff,
because a push to its branch gives that run the repository secrets while the raised packages build; then checks
out the branch, runs *Format* and pushes the result. A raised `dotnet-sonarscanner` gets no SonarQube run on a
Dependabot pull request, so before merging a maintainer pushes a commit to its branch or runs the *Sonar gate*
locally. If *Check tool versions* is red, raise the package that is behind.
<!-- project:end getting-started -->

## Submitting a pull request

Nothing is ever committed or pushed directly to `main` — every change goes through a separate branch and
a pull request.

Pull requests are merged with **Squash and merge**: the PR title becomes the single commit subject on
`main` and the description its body, so the commits on the branch are working history and need not be
curated. Keep the branch up to date by merging the current `main` into it (no force-push needed); do not
use the plain *Create a merge commit* or *Rebase and merge* buttons (see the decision record on
squash-merging in [`decisions/`](decisions/README.md)).

For PR naming use the following convention: `[area] Description` (no period at the end).

- For the area, use one of the areas listed below, capitalized. Issue titles use the same `[area]` prefix.
- For the description, do not reference an issue number in there. A clear, short summary of what
  the change entails is enough; there is room to elaborate in the description.

<!-- project:begin areas -->
Issues and pull requests use the same areas (they are not the area documents of [`areas/`](areas/README.md)). A
pull request that closes an issue normally takes the issue's area; a change across several areas takes the one it
changes most.

- `Pipe` — the statistics pipe: connection, frame reader and parser, tick assembly, recorder and replay.
- `GameData` — the game installation, the game files and the catalog extracted from them.
- `Contracts` — the shared types and formats between the Companion and the server.
- `Companion` — the Windows Companion: tray app, its windows, local and forward mode.
- `Server` — the server: ingest, live state, persistence and configuration.
- `History` — the stored time series and their retention.
- `UI` — the web interface: layout, views and charts.
- `Alerts` — notifications the server sends out.
- `Tests` — test projects and test data without a product change.
- `Tooling` — developer tools beside the product, such as tools for recordings and test fixtures.
- `Repo` — the solution, the repository layout and the shared build configuration.
- `Docker` — the server image and its build.
- `CI` — the checks on pushes and pull requests: build, analysis, CodeQL, Dependabot, SonarQube Cloud.
- `Release` — publishing: the release workflow, release notes, the Windows ZIP and image tags.
- `Security` — security reviews and hardening that belong to no single area.
- `Docs` — `README.md`, the user guide and the documents under `docs/`.
- `Squad` — the squad's project knowledge (`.squad/stack.md`, `.squad/project.md`, the project blocks of the
  marked files) and squad-maintenance changes.
- `Manual` — a task done by hand outside the repository, such as recording reference sessions.

<!-- project:end areas -->

When a PR is related to an issue, use the `Closes #issuenumber` syntax so the issue links to the
PR automatically and closes when the PR is merged.

The PR's label decides its section in the generated release notes (`.github/release.yml`): `breaking-change`,
`enhancement` or `feature`, `security`, `bug`; any other PR is listed under *Other changes*, and `ignore-for-release` leaves it out.

Follow the PR template in [`.github/pull_request_template.md`](../.github/pull_request_template.md).

## Contributions from outside

Issues and pull requests from forks are welcome. Changes in this repository are spec-driven: what a feature does
is agreed as acceptance criteria before it is built, and the maintainers own those criteria.

- **Bug fixes and documentation** may come as a pull request directly. Describe how to reproduce the bug, or
  link an issue that does.
- **Features and changes of behavior start with an issue** (the feature request template). A maintainer answers
  with the proposed scenarios and acceptance criteria; open a pull request once you agree on them, and link the
  issue with `Closes #…`. A feature pull request without an agreed issue is put on hold until there is one.
- **Out of scope for an outside pull request:** the AI squad and agent instructions (`.squad/` except
  `stack.md` and `project.md`, `.claude/`, `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`), the
  text outside the `<!-- project:… -->` blocks of the template's marked files, and the workflows under
  `.github/workflows/`. The first two come from a shared template and are changed there, the workflows are the
  repository's own; the *Fork scope* check fails on the first two. Open an issue instead.
- **Checks on a fork:** pull requests from forks run without repository secrets, so the analysis that needs one
  (e.g. SonarQube Cloud) does not run there; a maintainer runs it before merging. Run the quality gates below
  locally so the pull request arrives clean. The CI run of a first-time contributor's fork waits for a maintainer's
  approval before it starts.
- Maintainers may push to your branch ("Allow edits by maintainers") or carry the change on in a branch of their
  own; you stay credited as co-author of the squashed commit.

## Quality gates

Code-style rules are documented in [`AGENTS.md`](../AGENTS.md) and in
[`.squad/stack.md`](../.squad/stack.md), and are binding for all contributions. Before opening a pull
request, run the commands from `stack.md`: *Format*, *Build*, the *Analyzer gate* (no analyzer diagnostic
of any severity in a changed file) and the *Coverage gate* (line coverage on new or changed production code
and overall, thresholds in [`UNIT_TESTS.md`](UNIT_TESTS.md#code-coverage)). A pull request is
expected to arrive clean (see the decision record on quality gates in [`decisions/`](decisions/README.md)).

<!-- project:begin releases -->
## Versioning and releases

A release is a `vMAJOR.MINOR.PATCH` tag created on `main` (no leading zeros, no pre-release suffix); it publishes
the Docker image `networlddev/censio` and the Windows ZIP of the Companion. Merging a PR by itself never publishes a
release.

To release, an administrator of the repository (only administrators can create `v*` tags, see below):

1. On GitHub, *Releases*, *Draft a new release*; create the new tag `vX.Y.Z` on the target `main`.
2. Tick *Set as a pre-release*, and publish.

The workflow `Release` ([`decisions/0012`](decisions/0012-release-pre-release-first-and-secret-free-builds.md)) then
runs. It refuses a tag of another form and a tag whose commit is not reachable from the head of `main`, and a tag lower than the highest `vMAJOR.MINOR.PATCH` tag (checked at
the start and again right before the promotion); builds and
checks the ZIP and the image without any secret; attaches the ZIP `Censio-<version>-win-x64.zip` to the release;
pushes the checked image as `networlddev/censio:<version>`; and only then moves `networlddev/censio:latest` to
that digest and turns the release into a full release marked *Latest*. While it runs, or when a job fails, the
release stays a pre-release and `latest` stays where it was. A release published as a full release only gets a
warning; it is marked *Latest* by GitHub at once and stays so if a job fails. The secrets `DOCKERHUB_USERNAME` and
`DOCKERHUB_TOKEN` (jobs `image-publish` and `promote`) and `WEBSITE_DISPATCH_TOKEN` (job `dispatch` of
`notify-website.yml`) are secrets of the GitHub environment `release` and must not exist as repository secrets: a
repository secret is readable by a workflow on any branch, an environment secret only by a job that names the
environment and runs on a ref its deployment rule admits. The rule admits only tags `v*`.

One-time setup by the administrator, in this order (GitHub, *Settings*):

1. *Environments*, *New environment* `release`, before the pull request that adds `environment: release` is merged,
   so no run creates it without a rule. If one already did, edit that one.
2. *Deployment branches and tags*: *Selected branches and tags*, one rule of type **Tag** with the pattern `v*`; no
   branch rule, no required reviewers. The type is Tag because a branch rule `v*` would admit any branch named
   `v...`, which everyone with write access can push. Clear *Allow administrators to bypass configured protection
   rules* if the page shows it.
3. *Environment secrets*: add `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` and `WEBSITE_DISPATCH_TOKEN`. A stored secret
   cannot be read back; a token not at hand is created anew (Docker Hub access token with read and write access;
   the fine-grained GitHub token described in `notify-website.yml`) and the old one revoked after step 5.
4. Merge the pull request.
5. *Secrets and variables*, *Actions*, *Repository secrets*: delete the three secrets (and any *Dependabot*
   secrets of those names).
6. Publish `v0.1.0` as above and check that the jobs `image-publish`, `promote` and `dispatch` show the
   environment `release`.

The workflow cannot read the environment's settings, so no step can check that the rule exists; a missing Docker
Hub secret fails the credentials check and the release stays a pre-release, and a missing `WEBSITE_DISPATCH_TOKEN`
fails the job `dispatch` of `notify-website.yml` at its step `Check the website token`, with a message naming the
secret. At `v0.1.0`, check that this step passes (it also shows that the secret is set). The protection holds only
after steps 1 to 3 and 5.

Who can do what:

- Only administrators create, move or delete `v*` tags (ruleset `tag-Protection`).
- Anyone with write access can publish a release on an existing `v*` tag or re-run a release run; that runs the
  reviewed workflow of that tag with the environment secrets. It discloses no secret, and it never moves Docker Hub
  `latest` for a tag but the highest (refused in `check` and in `promote`, [issue #78](https://github.com/LarsLaskowski/Censio/issues/78)).
  A re-run of only `image-publish` or `companion-publish` re-pushes that run's `<version>` or re-attaches its ZIP
  from the run's own checked artifacts within the 7 days, and stops at `promote` unless the tag is the highest; a republish or a re-run of all jobs of
  the highest tag rebuilds and re-promotes the same version.
- Residual: GitHub's *Latest* mark can be set by anyone with write access (by hand, by publishing an old tag's
  release as a full release, or by deleting the highest release), and the workflow does not protect it. The
  administrator restores it on the highest release with *Set as latest release*.
- The environment admits the workflow of whatever commit a `v*` tag points to and closes branch workflows, not
  `main`: while the ruleset `main-Protection` lets the write and maintain roles bypass its pull request rule, a
  writer can push a changed workflow to `main` without a pull request, and the administrator's next release runs it
  with the secrets ([issue #77](https://github.com/LarsLaskowski/Censio/issues/77)).

Not yet tried (a settings dry run, D1 to D4 of the plan of issue #74, and the first release `v0.1.0` have not run;
these statements stay unverified until they have):

- D1: that the bypass actors of `tag-Protection` (administrator only) and `main-Protection` (administrator,
  maintain and write roles) are as stated above.
- D2: that a job naming the environment on a branch ref is refused before its first step.
- D3: that a repository secret is still readable by a job without an environment, and that all three are gone after
  step 5.
- D4: that an unknown environment is created without a rule, that `*` of the tag rule and of the ruleset's
  `refs/tags/v*` match the same tags, and the labels of the settings pages named above (including *Allow
  administrators to bypass configured protection rules*).
- That a `release` event runs on `refs/tags/<tag>` and passes the rule, and that the jobs need no `deployments`
  permission (the first release), and that a release published or re-run by a writer on an existing `v*` tag passes it as well.

Limits and rules:

- The check that the tag is on `main` guards against a maintainer's mistake; it is not a security control. GitHub
  runs the workflow of the tagged commit, so a tag on a commit of `main` from before the workflow was merged starts
  no run, and a tag on a branch whose workflow differs is not refused.
- After a failed job, use *Re-run failed jobs* of the newest release's run, and only within the 7 days the run's
  artifacts are kept. Later, or when the failure needs a rebuild, use *Re-run all jobs*: it rebuilds, replaces the ZIP
  asset and overwrites the image tag `<version>`. Releasing a lower version is not supported while a higher
  `v*` tag exists: its run is refused. A failed release cannot be completed after a higher tag was created.
- The run uses the commit's `Release` workflow, so it can run for real only after it is merged; the first release is
  `v0.1.0`.

Every assembly of the solution gets its version from the build property `Version` (`-p:Version=<v>`, default
`0.0.0`), shown unchanged by the server's start page and the Companion's tray menu
([`decisions/0005`](decisions/0005-product-version-from-one-build-property.md)).

The release notes GitHub generates sort merged pull requests by label (`.github/release.yml`): `breaking-change`
under *Breaking changes*, `enhancement` or `feature` under *Features*, `security` under *Security fixes*, `bug`
under *Bug fixes*, any other under *Other changes*; `ignore-for-release` leaves a pull request out.
<!-- project:end releases -->

<!-- project:begin stability -->
## Stability policy

An essential consideration in every pull request is its impact on the system. Avoid introducing
unnecessary breaking changes, performance or functional regressions, or negative impacts on usability. In
particular, preserve the guarantees listed in [`.squad/project.md`](../.squad/project.md) (*Guarantees*)
and described in [`ARCHITECTURE.md`](ARCHITECTURE.md) unless a change explicitly intends to alter one.
<!-- project:end stability -->

## Reporting security issues

Do not report security vulnerabilities through public GitHub issues. See
[`SECURITY.md`](../SECURITY.md) for the private reporting process.

## License

<!-- project:begin license -->
By contributing to this project, you agree that your contributions will be licensed under the
same license that covers the project (see `LICENSE` or `LICENSE.md` in the repository root).
<!-- project:end license -->
