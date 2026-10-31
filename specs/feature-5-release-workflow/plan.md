# Plan: Release workflow: pre-release, Windows ZIP, Docker image, promote

Source: Issue #5 | [spec.md](spec.md)
Status: Revised (1)
Tier: security — the change adds a workflow that holds the Docker Hub credentials and a `contents: write` token and decides what is published (trust of the build and release).
Security areas touched: CI workflows (new `.github/workflows/release.yml` with secrets and write permissions, new job in `.github/workflows/ci.yml`), the published Docker image (`networlddev/censio` on Docker Hub), the Companion release build (publish options), new CI scripts under `.github/scripts/`, one new pinned action (`actions/download-artifact`)

## Problem / root cause

Feature: see [spec.md](spec.md) (AC1–AC10).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A Linux publish of `Censio.Companion` with `-r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -p:Version=1.2.3` writes exactly one file, `Censio.Companion.exe` (173,037,946 bytes), 0 warnings | Scratch copy of `git ls-files`, SDK 10.0.403, `ls -la out3` | confirmed |
| 2 | Without the last three properties `.pdb` and `.xml` files land beside the exe | Same scratch, `out`, `out2` | confirmed |
| 3 | The WPF native libraries (`wpfgfx_cor3.dll`, `PresentationNative_cor3.dll`, `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `PenImc_cor3.dll`) are inside the bundle | Their names found in the exe bytes | confirmed |
| 4 | The Linux-built exe is `PE32+ executable (GUI) x86-64` (subsystem 2: no console window) | `file`, PE optional header read | confirmed |
| 5 | The bundle holds `Censio.Companion.dll` byte for byte (no single-file compression by default) | `dll in exe` over the bytes of `obj/Release/net10.0-windows/win-x64/Censio.Companion.dll`: True | confirmed |
| 6 | The tray shows `ProductVersion.Read(typeof(App).Assembly)`, the `AssemblyInformationalVersionAttribute` of `Censio.Companion` | `src/Censio.Companion/App.xaml.cs:38`, `src/Censio.Contracts/ProductVersion.cs:28` | confirmed |
| 7 | The compiler writes the informational version into the assembly's own Win32 `VERSIONINFO` as `ProductVersion` | Build with `-p:Version=1.2.3 -p:InformationalVersion=9.9.9-x`: `ProductVersion 9.9.9-x` in the dll (UTF-16) | confirmed |
| 8 | The Linux-built exe starts on Windows 10 22H2 / 11 x64 without .NET and shows the tray menu (the apphost is not customized with the project's resources on Linux) | to verify: Product Manager runs the ZIP artifact of the CI job `release-check` on Windows without .NET, after step 6, before step 8 | unverified |
| 9 | A release-triggered run uses the workflow of the tagged commit | GitHub docs, *Events that trigger workflows*, `release` (`GITHUB_SHA`: last commit in the tagged release) | confirmed |
| 10 | Events raised with `GITHUB_TOKEN` start no workflow; a promotion raises no `published` event | GitHub docs, *Triggering a workflow from a workflow*; `release` activity types | confirmed |
| 11 | `gh release upload --clobber` replaces an asset; `gh release edit --prerelease=false --latest` exists | `gh help release upload`, `gh help release edit` (gh 2.89.0) | confirmed |
| 12 | `gh` is installed on `ubuntu-latest` | to verify: the CI job `release-check` prints `gh --version` on its first run (Dev, step 11) | unverified |
| 13 | `actions/download-artifact` reads an artifact of the same run without `actions: read` | to verify: Dev reads the action's README at the pinned commit, step 6; else grant `actions: read` to the downloading jobs | unverified |
| 14 | With the containerd image store (Docker 29.9.2) `docker build` makes an index with an `unknown/unknown` attestation entry; `--provenance=false` makes one `linux/amd64` manifest. Save, load and push keep the image ID; the pushed digest equals it. `docker buildx imagetools create -t …:latest …@<digest>` wraps a single manifest into a new index (new digest); with `--prefer-index=false` the digest stays | Local `registry:2` on `127.0.0.1:5555`, busybox test image, buildx 0.38.1: digests `58f91a17…` kept, `f9fcf63b…` for the default retag | confirmed |
| 14a | The same on `ubuntu-latest` (its Docker may use the classic store, where the image ID is the config digest) | to verify: the `release-check-test.sh` cases against `registry:2` in the CI job `release-check` (Dev, step 11) | unverified |
| 15 | "Re-run failed jobs" keeps the outputs of the jobs that succeeded (the pushed digest) | to verify: on the first failed release; the promote job refuses an empty digest, so the fallback is "Re-run all jobs" | unverified |
| 16 | Tag names may hold shell metacharacters (`$(…)`, `;`, a backtick, `"`, `%`) | `git check-ref-format refs/tags/'v1.2.3$(id)'` and others: ok; `:` and space refused | confirmed |
| 18 | `networlddev/censio` exists on Docker Hub, public, no tag | `hub.docker.com/v2/repositories/networlddev/censio/` | confirmed |
| 19 | The secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` exist | Product Manager's answer (spec, *Open questions*) | confirmed |
| 20 | Related defect: `notify-website.yml` interpolates `tag_name` into the JSON `client-payload` unescaped; a tag with `"` breaks it | `.github/workflows/notify-website.yml`, last line; fact 16 | confirmed |
| 21 | No Docker daemon runs at session start; `sudo -n dockerd` starts one (containerd store) and the SDK is 10.0.403 | `docker info` after starting `dockerd` in this session | confirmed |
| 22 | The server exits at once when `CENSIO__DATADIRECTORY` is not absolute (E6's test image) | `src/Censio.Server/DataProtectionSetup.cs:153`; `docs/areas/server.md` | confirmed |

## Tests per criterion

The test script is `.github/scripts/release-check-test.sh` (CI job `release-check`); every case asserts the exit
code and the exact annotation line. *Layout* in `.squad/stack.md` covers C# tests only; the script follows the
precedent of `image-check.sh`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC2 | `companion 1.2.3`: exactly `Censio.Companion.exe` in the ZIP and the publish directory; extra `x.pdb` in a copy of the publish directory → E10 text naming `Censio.Companion.exe, x.pdb`; a dll that is not in the bundle → the integrity error | `.github/scripts/release-check-test.sh` |
| AC3 | `image 1.2.3` passes and makes one `linux/amd64` manifest; a derived image with `CENSIO__DATADIRECTORY=relative` (exits at once, fact 22) → E6 text as the last line, exit 1, no container left; against `registry:2` on `127.0.0.1`: `push` of the saved archive → one `linux/amd64` manifest whose digest is printed; a wrong expected image ID → its error; an archive built with provenance → the single-image error; `promote-image` → `:latest` has the digest of `:<version>` | `.github/scripts/release-check-test.sh` |
| AC4 | `tag`: `v0.1.0`, `v10.20.30` print `0.1.0`, `10.20.30`; `v1.2`, `v1.2.3-rc.1`, `1.2.3`, `v01.2.3`, `V1.2.3`, `v1.2.3.4`, empty, `v1.2.3$(touch pwned)` → E1 text, no file `pwned`; `v1.2.3%0A` → the `%` written as `%25`; `v1.2.3` + LF + `x` → E1. `form version|digest <name> <value>`: `1.2.3` and `sha256:` + 64 hex pass; `v1.2.3`, `1.2.3;id`, an uppercase digest, 63 hex, a digest + LF, empty → the form error | `.github/scripts/release-check-test.sh` |
| AC5 | `on-main` in a scratch Git repository: the head of `main` and an older `main` commit pass; a commit on a side branch → E2 text with its SHA | `.github/scripts/release-check-test.sh` |
| AC6 | `prerelease`: `true` prints nothing; `false` → E3 warning, exit 0 | `.github/scripts/release-check-test.sh` |
| AC7 | `dockerhub-secrets`: `true true` passes; `false true`, `true false`, `false false` → E4 text | `.github/scripts/release-check-test.sh` |
| AC8 | `check-companion` with the 1.2.3 build and expected `1.2.4` → `Censio.Companion.exe shows version '1.2.3', expected '1.2.4'.`; `check-image` with the 1.2.3 image and expected `1.2.4` → `The server image shows version '1.2.3', expected '1.2.4'.` | `.github/scripts/release-check-test.sh` |
| AC1 | none — release run only (*Verification without tests*) | — |
| AC9 | none — review (*Verification without tests*) | — |
| AC10 | none — documentation (*Verification without tests*) | — |

Documented shell procedures: the README's commands stay single, independent commands without variables, run one at
a time (the existing note); a failed `docker pull` makes the following `docker run` fail with Docker's own error,
nothing half-installed remains. No case runs in CI only: the Dev starts the daemon first (`sudo -n dockerd > /tmp/dockerd.log 2>&1 &`, then wait
for `docker info`, fact 21) and reports any case that could not run locally.

## Verification without tests

The change holds no production or test code (C#): workflows, CI scripts and documentation only (*Changes without
production or test code*). Steps 4, 5 and the *Coverage gate* are not applicable; step 6 is the Dev's edits, and the
Dev runs `bash .github/scripts/release-check-test.sh` locally before handing back, besides the *Analyzer gate*
(`shellcheck`, `actionlint`).

| AC | Where and by whom |
| -- | ----------------- |
| AC1 | `v0.1.0` after the merge (Product Manager): green run, asset, both tags with one digest (`docker buildx imagetools inspect`), *Latest*; S4 at the next release. Review (Reviewer, Security): only `promote` moves `latest` and *Latest*; the run creates no other tag |
| AC2 | CI cases above; dry run (Product Manager) of the ZIP artifact `companion-dry-run` from this branch's own run of the CI job `release-check` (never one of a fork's run) on Windows 10 22H2 or 11 x64 without .NET, before step 8 (fact 8); again on `v0.1.0` |
| AC3 | CI cases above (fact 14a); review: `image-publish` pushes the archive `image-build` checked, compared by image ID; `promote` retags in the registry and compares digests; `v0.1.0`: the README's `docker run` with `networlddev/censio:0.1.0` |
| AC4–AC8 | CI cases above; review: every build job `needs: check`, the checks run before any upload or push; AC7 also review of the secret scoping and the `v0.1.0` logs |
| AC9 | Review: `promote` `needs` both publish jobs; `--clobber`; a re-push overwrites `<version>`; `promote` refuses a digest not of the form (empty included); `retention-days: 7` |
| AC10 | Review of README, user guide and CONTRIBUTING against AC10's list |

## Approach

**`.github/scripts/release-check.sh <command> …`** (bash, `set -euo pipefail`, `LC_ALL=C`). Every error is printed as
`::error::<text>` and exits 1; the E3 warning is `::warning::<text>`, exit 0. `%`, CR and LF of a value in an
annotation are encoded (`%25`, `%0D`, `%0A`). Patterns are matched with `[[ $value =~ $re ]]` on the whole value
(never `grep`, which matches line by line).

**Values between jobs and from the event** (Security B1): every `github.event.*` and `needs.*.outputs.*` value reaches
a `run:` only through `env` and is passed as an argument; no `${{ }}` inside `run:`. Each publish and promote job
first checks the form of every output it uses with `form`: a version against the tag pattern without `v`, an image
ID or digest against `^sha256:[0-9a-f]{64}$`.

| Command | Does | Errors (exact text) |
| ------- | ---- | ------------------- |
| `tag <tag>` | prints the version: the tag without `v`, matched by `^v(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)$` | `Release tag '<tag>' is not of the form vMAJOR.MINOR.PATCH.` |
| `form version\|digest <name> <value>` | checks a passed value | `The value <name> passed from an earlier job is not of the expected form.` (the value is not printed) |
| `on-main <tag> <main-ref>` | `git rev-parse "refs/tags/<tag>^{commit}"`, then `git merge-base --is-ancestor <sha> <main-ref>` | `Release tag '<tag>' points to commit <sha>, which is not on main.` |
| `prerelease <tag> <true\|false>` | warns on `false` | warning: `Release <tag> was published as a full release; publish releases as pre-releases so they are not shown as the latest release before their artifacts exist.` |
| `dockerhub-secrets <user-set> <token-set>` | both must be `true` | `Docker Hub credentials are missing: set the repository secrets DOCKERHUB_USERNAME and DOCKERHUB_TOKEN.` |
| `companion <version> <out-dir>` | the publish of fact 1 with `-p:Version=<version>` into `<out-dir>/publish`, then `check-companion`, then `zip -j -X <out-dir>/Censio-<version>-win-x64.zip`, then the ZIP's list must be `Censio.Companion.exe` | as `check-companion` |
| `check-companion <publish-dir> <dll> <version>` | file list; `<dll>` (the build's `Censio.Companion.dll`) must occur byte for byte in the exe; the dll's `ProductVersion` (fact 7, read by an inline `python3 -I` heredoc) must equal `<version>` | `The Companion publish output holds <files>, expected Censio.Companion.exe only.` (`<files>`: sorted, joined by `, `); `Censio.Companion.exe does not bundle the checked Censio.Companion.dll.`; `Censio.Companion.exe shows version '<actual>', expected '<version>'.` |
| `image <version> <image>` | `docker build --provenance=false -f src/Censio.Server/Dockerfile --build-arg VERSION=<version> -t <image> .`, then `check-image`; prints the image ID | as `check-image` |
| `check-image <image> <version>` | runs it on `127.0.0.1` with a random host port, waits up to 60 s for `/health` = `Healthy` (an exited container ends the wait), reads `Version <x>` from `/`; a `trap` removes the container on every path; output of `docker` and `curl` goes to the log above the error, never as the last line or the exit code | `The server image did not answer Healthy on /health within 60 s.`; `The server image shows version '<actual>', expected '<version>'.` |
| `push <archive> <image-id> <repo> <version>` | `docker load -i <archive>`, the loaded ID must be `<image-id>`, tag and push `<repo>:<version>`, then the registry's manifest of `<repo>:<version>` (`docker buildx imagetools inspect`) must be one image manifest for `linux/amd64`; prints its digest | `The loaded image is <id>, expected the checked image <image-id>.`; `<repo>:<version> is not a single linux/amd64 image.` |
| `promote-image <repo> <digest>` | `docker buildx imagetools create --prefer-index=false -t <repo>:latest <repo>@<digest>` (registry-side, no pull, fact 14), then the registry digest of `<repo>:latest` must be `<digest>` | `<repo>:latest has digest <actual>, expected <digest>.` |

**`.github/workflows/release.yml`** (`name: Release`), `on: release: types: [published]`, `permissions: {}`, every
action pinned by commit, every checkout `persist-credentials: false` and at the tagged commit. No cache of any kind:
no `actions/cache`, no `cache:` of `setup-dotnet`, no Buildx `type=gha` cache (record 0008). Artifacts are uploaded
with `retention-days: 7`. Jobs:

1. `check` (`contents: read`): checkout with `fetch-depth: 0`; `tag`, `on-main <tag> origin/main`, `prerelease`;
   output `version`.
2. `companion-build` (`contents: read`, needs `check`): `form version`, setup .NET, `companion`, upload the ZIP as
   artifact `companion-zip`.
3. `image-build` (`contents: read`, needs `check`): `form version`, `image <version> censio-release:<version>`, output
   the image ID, `docker save -o censio-image.tar`, upload as artifact `server-image`.
4. `companion-publish` (`contents: write`, needs `check`, `companion-build`): checkout (for the script), `form
   version`; then download `companion-zip` by name into the new directory `${{ runner.temp }}/companion-zip`; run
   nothing from it; upload `${{ runner.temp }}/companion-zip/Censio-<version>-win-x64.zip` to the release with
   `gh release upload … --clobber`, `GH_TOKEN` and `GH_REPO` in that step's `env` only.
5. `image-publish` (`contents: read` for the checkout, needs `check`, `image-build`): checkout, `form version`, `form
   digest` on the image ID, `dockerhub-secrets` with the booleans `secrets.X != ''` (record 0008); then download
   `server-image` into `${{ runner.temp }}/server-image`; `docker login --password-stdin` with both secrets in that
   step's `env`; `push ${{ runner.temp }}/server-image/censio-image.tar <id> networlddev/censio <version>` (the script
   from the checkout, which the download cannot write); output the digest; `docker logout` with `if: always()`.
6. `promote` (`contents: write`, needs `check` and both publish jobs): checkout, `form version`, `form digest` (an
   empty digest fails here, fact 15); login as in 5; `promote-image networlddev/censio <digest>`; logout; then mark
   the release with `gh release edit … --prerelease=false --latest`, `GH_TOKEN` and `GH_REPO` in that step's `env`.

**`.github/workflows/ci.yml`**, new job `release-check` (`contents: read`, no secret, no cache): checkout, setup .NET,
`gh --version` (fact 12), `bash .github/scripts/release-check-test.sh` (it starts `registry:2`, pinned by digest, on
`127.0.0.1` and removes it), then upload the ZIP the test built with version `1.2.3` as artifact `companion-dry-run`
(retention 14 days).

User-visible output: the annotation texts above are fixed; the documentation's layout is free for the Dev within
AC10, approved in step 9.

## Options considered

Job layout, build platform and the metadata action: see
[record 0012](../../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md). Further:

- Version check of the exe: parsing the assembly metadata (a C# helper or a Python ECMA-335 reader) against the
  dll's `VERSIONINFO` `ProductVersion` plus the byte check that the bundle holds that dll (chosen: facts 5 and 7,
  no new code project, no new tool).
- Single-file compression (`EnableCompressionInSingleFile`): a smaller ZIP, but the bundled dll is no longer
  findable byte for byte and every start decompresses; rejected.
- E4's check in `check` (fails before anything is attached) against `image-publish` (as the spec words E4);
  `image-publish` chosen to match the spec.
- Moving `latest`: pull by digest, tag and push (keeps the digest, fact 14, but pulls into a job with the secrets);
  `imagetools create` with its default (wraps the manifest into a new index: new digest, refuted for us); `imagetools
  create --prefer-index=false` (registry-side, digest kept; chosen); re-loading the archive in `promote` (one more
  artifact download in a job with `contents: write`).
- Attestations: `--provenance=false` (one `linux/amd64` manifest, as the area document promises) against keeping
  BuildKit's default provenance index (an `unknown/unknown` entry next to `linux/amd64`); provenance is #44's topic.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/workflows/release.yml` | new |
| CI | `.github/workflows/ci.yml` | new job `release-check` |
| CI | `.github/scripts/release-check.sh`, `.github/scripts/release-check-test.sh` | new |
| Docs | see *Documentation updates* | — |

No `.csproj`, `Directory.*.props` or Dockerfile changes: the publish options are passed on the command line, so the
*Image build check* is not triggered.

## Signatures (for the Dev's skeleton)

none (no C# API; the script's commands are in *Approach*).

## Test files

- `.github/scripts/release-check-test.sh` (new).

Existing test code that calls a changed signature: none.

Files no unit test can cover: `release.yml` (runs only on a published release); covered by review, the CI cases of
the script it calls, and `v0.1.0`. No coverage exclusion follows (no C#).

## Areas

- `server` (`docs/areas/server.md`), *Docker image*: published on Docker Hub as `networlddev/censio:<version>` for
  every release and `latest` for the last promoted release, linux/amd64; *Not here* drops #5.
- `companion` (`docs/areas/companion.md`), *Platform*: the release ZIP `Censio-<version>-win-x64.zip` holds only a
  self-contained single-file `Censio.Companion.exe` (native libraries embedded), Windows 10 22H2 and 11 x64, no
  installed .NET, no administrator rights; unsigned, SmartScreen asks once; no version in the file properties; a
  local build still needs the Desktop Runtime. *Not here* drops the ZIP. `docs/areas/README.md`: the companion row's
  *Scope* gains "release ZIP", *Not here* loses it; the server row's *Not here* keeps "release workflows".

## Documentation updates

All by the Dev, after the scripts pass:

- `README.md` and `docs/USER_GUIDE.md` (English, then German, both files alike) — AC10: *Docker* opens with
  `docker pull networlddev/censio:<version>` (tags on Docker Hub, `latest`); every `docker run` uses
  `networlddev/censio:<version>`; the NAS part pulls on the NAS (drops `docker save`/`docker load` and "build on the
  NAS"); building from a checkout moves to a subsection *Build from source*; a new section *Companion* /
  *Companion (Deutsch)*: download from the release page, needs no .NET, extract anywhere, start
  `Censio.Companion.exe`, SmartScreen "More info → Run anyway" ("Weitere Informationen → Trotzdem ausführen"),
  Windows 10 22H2 / 11 x64, `Version <version>` in the tray menu. Statements no run has shown yet (the pull
  commands until `v0.1.0`, the ZIP until the dry run) are marked "not yet tried" as the README does today.
- `docs/CONTRIBUTING.md`: *Versioning and releases* (project block) — the maintainer's steps (draft a release, new
  tag `vX.Y.Z` on `main`, tick *Set as a pre-release*, publish), what the run does, the two secrets, E2's limits,
  re-run failed jobs of the newest release only, and only within the 7 days the run's artifacts are kept (later
  *Re-run all jobs*, which rebuilds and overwrites `<version>`, E8), `latest`/*Latest* move with the promotion, link
  record 0012; *getting-started* block — new bullets *Release check* (`ci.yml`, job `release-check`, local command
  with the daemon running; an exe from a fork's run is never started) and *Release* (`release.yml`).
- `docs/ARCHITECTURE.md`, *Deployment*: "A release is cut from a `v*` tag" becomes pre-release → build and check
  without secrets → publish → promote, linking record 0012.
- `.squad/project.md`, *Security areas*: new entry "Release publishing: `.github/workflows/release.yml` — the Docker
  Hub credentials and the `contents: write` token only in jobs that run no build code (record 0012)".
- The area documents as in *Areas*.

## Architecture check

No guarantee in `.squad/project.md` exists yet; none is weakened. Record 0005 (one `Version` property) is followed:
the workflow passes `-p:Version` and `VERSION`, and checks the artifacts as its consequence asks. Record 0008's rule
(secrets in the steps that use them) is kept and tightened (no secret in a build job). Record 0004's Linux build of
the Companion is kept. The image is feature 4's, unchanged.

## Security considerations

- [ ] No limits on parsed input: the tag is matched whole by a fixed pattern before use; nothing is decoded.
- [ ] Exceptions reaching a user-visible message: none; every message is one of the fixed texts above, with the tag
  annotation-encoded.
- Script injection: event values and every `needs.*.outputs.*` reach scripts only through `env`/arguments (fact 16),
  and the publish and promote jobs check each output's form before use (`form`).
- Artifact poisoning: build code holds the run's artifact token and may upload any file tree. The publish jobs check
  out the script first, download one artifact by name into a new directory under `runner.temp` outside the checkout,
  execute nothing from it and read one file by its fixed name; every script call runs from the checkout.
- No cache in `release.yml` or the `release-check` job (no `actions/cache`, no `setup-dotnet` cache, no Buildx
  `type=gha` cache).
- Secrets: Docker Hub secrets only in the `env` of the login steps of `image-publish` and `promote` and as booleans
  in the check step; `GH_TOKEN` only in the upload and edit steps; no build code runs in those jobs; `docker logout`
  always.
- Dry-run artifact: every pull request run uploads `companion-dry-run`, forks' runs included; only the one from this
  branch's own run is started, and CONTRIBUTING says an exe from a fork's run is never started.
- Supply chain: one new action (`actions/download-artifact`), pinned by commit; Dependabot's `github-actions`
  ecosystem covers it.
- Who can release: users with write access; fork pull requests cannot raise `release`.

## Decision records

- `docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md` (Proposed), indexed.

## Out of scope / follow-ups

- Follow-up issue (orchestrator): title "[CI] Pass the release tag to notify-website.yml without JSON
  interpolation"; body: "`notify-website.yml` writes `${{ github.event.release.tag_name }}` into the JSON
  `client-payload` unescaped; a tag name may contain a double quote (`git check-ref-format` allows it), which breaks the
  payload. Build the payload with `toJSON()` or from `env`. Found while planning #5."
- Follow-up issue (orchestrator, Security N4): title "[CI] Release secrets in a GitHub environment limited to v*
  tags"; body: "The Docker Hub secrets of `release.yml` are repository secrets, readable by any workflow job that
  references them. Move them into an environment `release` with a deployment rule for `v*` tags and use it in
  `image-publish` and `promote` only, so a workflow on a branch cannot reach them. Found in the plan review of #5."
- Everything the spec lists under *Out of scope*.

## Challenge

- M1 (fact 14; provenance index; pull and push): accepted with one correction, measured (fact 14): `image` builds with
  `--provenance=false`, `push` asserts one `linux/amd64` manifest, and `promote-image` retags registry-side with
  `--prefer-index=false` (the proposed default wraps the manifest and changes the digest), keeping the digest
  comparison; both are tested against `registry:2` in CI (fact 14a). Alternatives under *Options considered*.
- m1 (no daemon): accepted. Fact 21 corrected; the Dev starts `dockerd` before the test script and reports any case
  that could not run.
- m2 (no checkout for `gh`): accepted. `GH_REPO` next to `GH_TOKEN` in both steps (the jobs now check out the script
  anyway).
- m3 (retention): accepted. Both artifacts `retention-days: 7`; CONTRIBUTING states the window and *Re-run all jobs*
  after it; spec E7 names the window.
- m4 (E6 on an exited container): accepted. `check-image` ends the wait when the container exited, prints E6's text
  as the last line with exit 1, keeps `docker`/`curl` errors above it, removes the container by `trap`; the test
  asserts all three.

## Security review

- B1 (job outputs into credentialed jobs): accepted — *Approach* (values between jobs, `form`), with one test case per
  form, an injected `;id` and an LF included.
- B2 (artifact poisoning): accepted — *Approach* jobs 4–6 and *Security considerations*.
- N1: accepted, `[[ =~ ]]` under `LC_ALL=C`, with a tag + LF test case.
- N2: accepted, no cache of any kind (*Approach*, *Security considerations*).
- N3: accepted, only this branch's dry-run artifact is started; CONTRIBUTING says so.
- N4: follow-up issue described under *Out of scope / follow-ups*.
- N5: no change.

### Security notes (round 2, non-blocking, verbatim)

- N6 (`actions/download-artifact` version): B2's guarantee that the download "cannot write" the checked-out script depends on the action extracting safely. Versions 4.0.0–4.1.6 had a path-traversal (zip-slip) flaw (CVE-2024-42471) that let an artifact write outside its target directory. The Dev should pin a commit of the current major (at least v4.1.7), in line with the existing `upload-artifact` v7.0.2 pin. The diff review will check this pin.
- N7 (`runner.temp` in `run:` steps): inside `run:` steps the path should be written `$RUNNER_TEMP`, not `${{ runner.temp }}`. The plan's "no `${{ }}` inside `run:`" rule already requires this; the diff review will check it.
- N8 (open facts are not part of the security case): facts 13, 14a and 15 are still unverified. All three fail safe: an access error, a failed digest or ID comparison, or the empty-digest refusal. No security property depends on them being true, so no measurement is needed before approval.
