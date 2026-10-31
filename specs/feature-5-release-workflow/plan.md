# Plan: Release workflow: pre-release, Windows ZIP, Docker image, promote

Source: Issue #5 | [spec.md](spec.md)
Status: Draft
Tier: security — the change adds a workflow that holds the Docker Hub credentials and a `contents: write` token and decides what is published (trust of the build and release).
Security areas touched: CI workflows (new `.github/workflows/release.yml` with secrets and write permissions, new job in `.github/workflows/ci.yml`), the published Docker image (`networlddev/censio` on Docker Hub), the Companion release build (publish options), new CI scripts under `.github/scripts/`, one new pinned action (`actions/download-artifact`)

## Problem / root cause

Feature: see [spec.md](spec.md) (AC1–AC10).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A Linux publish of `Censio.Companion` with `-r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -p:Version=1.2.3` writes exactly one file, `Censio.Companion.exe` (173,037,946 bytes), 0 warnings | Scratch copy of `git ls-files`, SDK 10.0.403, `ls -la out3` | confirmed |
| 2 | Without `DebugType=embedded` and the two documentation properties, the `.pdb` files of the referenced projects and the `.xml` documentation files land beside the exe | Same scratch, `out` and `out2` listings (4 `.pdb`, 5 `.xml`) | confirmed |
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
| 14 | Pushing `:latest` for the pulled `@<digest>` keeps the digest | to verify: the promote job compares the registry digest of `:latest` with the pushed one and fails closed; seen on `v0.1.0` | unverified |
| 15 | "Re-run failed jobs" keeps the outputs of the jobs that succeeded (the pushed digest) | to verify: on the first failed release; the promote job refuses an empty digest, so the fallback is "Re-run all jobs" | unverified |
| 16 | Tag names may hold shell metacharacters (`$(…)`, `;`, a backtick, `"`, `%`) | `git check-ref-format refs/tags/'v1.2.3$(id)'` and others: ok; `:` and space refused | confirmed |
| 17 | `.github/release.yml` is the release-notes configuration, not a workflow | file header | confirmed |
| 18 | `networlddev/censio` exists on Docker Hub, public, no tag | `hub.docker.com/v2/repositories/networlddev/censio/` | confirmed |
| 19 | The secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` exist | Product Manager's answer (spec, *Open questions*) | confirmed |
| 20 | Related defect: `notify-website.yml` interpolates `tag_name` into the JSON `client-payload` unescaped; a tag with `"` breaks it | `.github/workflows/notify-website.yml`, last line; fact 16 | confirmed |
| 21 | Docker and the .NET SDK run in the session, so the Dev can run `release-check-test.sh` locally | `docker --version` 29.9.2, `dotnet --version` 10.0.403 | confirmed |

## Tests per criterion

The test script is `.github/scripts/release-check-test.sh` (CI job `release-check`); every case asserts the exit
code and the exact annotation line. *Layout* in `.squad/stack.md` covers C# tests only; the script follows the
precedent of `image-check.sh`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC2 | `companion 1.2.3`: exactly `Censio.Companion.exe` in the ZIP and the publish directory; extra `x.pdb` in a copy of the publish directory → E10 text naming `Censio.Companion.exe, x.pdb`; a dll that is not in the bundle → the integrity error | `.github/scripts/release-check-test.sh` |
| AC3 | `image 1.2.3` on the built image passes; a derived image with `CENSIO__DATADIRECTORY=relative` → E6 text | `.github/scripts/release-check-test.sh` |
| AC4 | `tag`: `v0.1.0`, `v10.20.30` print `0.1.0`, `10.20.30`; `v1.2`, `v1.2.3-rc.1`, `1.2.3`, `v01.2.3`, `V1.2.3`, `v1.2.3.4`, empty, `v1.2.3$(touch pwned)` → E1 text, no file `pwned`; `v1.2.3%0A` → the `%` written as `%25` | `.github/scripts/release-check-test.sh` |
| AC5 | `on-main` in a scratch Git repository: the head of `main` and an older `main` commit pass; a commit on a side branch → E2 text with its SHA | `.github/scripts/release-check-test.sh` |
| AC6 | `prerelease`: `true` prints nothing; `false` → E3 warning, exit 0 | `.github/scripts/release-check-test.sh` |
| AC7 | `dockerhub-secrets`: `true true` passes; `false true`, `true false`, `false false` → E4 text | `.github/scripts/release-check-test.sh` |
| AC8 | `check-companion` with the 1.2.3 build and expected `1.2.4` → `Censio.Companion.exe shows version '1.2.3', expected '1.2.4'.`; `check-image` with the 1.2.3 image and expected `1.2.4` → `The server image shows version '1.2.3', expected '1.2.4'.` | `.github/scripts/release-check-test.sh` |
| AC1 | none — release run only (*Verification without tests*) | — |
| AC9 | none — review (*Verification without tests*) | — |
| AC10 | none — documentation (*Verification without tests*) | — |

Documented shell procedures: the README's commands stay single, independent commands without variables, run one at
a time (the existing note); a failed `docker pull` makes the following `docker run` fail with Docker's own error,
nothing half-installed remains. No case runs in CI only: Docker and the SDK run in the session (fact 21).

## Verification without tests

The change holds no production or test code (C#): workflows, CI scripts and documentation only (*Changes without
production or test code*). Steps 4, 5 and the *Coverage gate* are not applicable; step 6 is the Dev's edits, and the
Dev runs `bash .github/scripts/release-check-test.sh` locally before handing back, besides the *Analyzer gate*
(`shellcheck`, `actionlint`).

| AC | Where and by whom |
| -- | ----------------- |
| AC1 | `v0.1.0` after the merge (Product Manager): green run, asset, both tags with one digest (`docker buildx imagetools inspect`), *Latest*; S4 at the next release. Review (Reviewer, Security): only `promote` moves `latest` and *Latest*; the run creates no other tag |
| AC2 | CI cases above; dry run (Product Manager) of the ZIP artifact `companion-dry-run` of the CI job `release-check` on Windows 10 22H2 or 11 x64 without .NET, before step 8 (fact 8); again on `v0.1.0` |
| AC3 | CI cases above; review: `image-publish` pushes the archive `image-build` checked, compared by image ID; `promote` compares digests; `v0.1.0`: the README's `docker run` with `networlddev/censio:0.1.0` |
| AC4–AC8 | CI cases above; review: every build job `needs: check`, the checks run before any upload or push; AC7 also review of the secret scoping and the `v0.1.0` logs |
| AC9 | Review: `promote` `needs` both publish jobs; `--clobber`; a re-push overwrites `<version>`; `promote` refuses an empty digest |
| AC10 | Review of README, user guide and CONTRIBUTING against AC10's list |

## Approach

**`.github/scripts/release-check.sh <command> …`** (bash, `set -euo pipefail`). Every error is printed as
`::error::<text>` and exits 1; the E3 warning is `::warning::<text>`, exit 0. Every value from the event (tag) comes
in as an argument the workflow passes from `env`, never through `${{ }}` inside `run:` (fact 16). `%`, CR and LF of a
value in an annotation are encoded (`%25`, `%0D`, `%0A`).

| Command | Does | Errors (exact text) |
| ------- | ---- | ------------------- |
| `tag <tag>` | prints the version: the tag without `v`, matched whole by `^v(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)\.(0\|[1-9][0-9]*)$` | `Release tag '<tag>' is not of the form vMAJOR.MINOR.PATCH.` |
| `on-main <tag> <main-ref>` | `git rev-parse "refs/tags/<tag>^{commit}"`, then `git merge-base --is-ancestor <sha> <main-ref>` | `Release tag '<tag>' points to commit <sha>, which is not on main.` |
| `prerelease <tag> <true\|false>` | warns on `false` | warning: `Release <tag> was published as a full release; publish releases as pre-releases so they are not shown as the latest release before their artifacts exist.` |
| `dockerhub-secrets <user-set> <token-set>` | both must be `true` | `Docker Hub credentials are missing: set the repository secrets DOCKERHUB_USERNAME and DOCKERHUB_TOKEN.` |
| `companion <version> <out-dir>` | the publish of fact 1 with `-p:Version=<version>` into `<out-dir>/publish`, then `check-companion`, then `zip -j -X <out-dir>/Censio-<version>-win-x64.zip`, then the ZIP's list must be `Censio.Companion.exe` | as `check-companion` |
| `check-companion <publish-dir> <dll> <version>` | file list; `<dll>` (the build's `Censio.Companion.dll`) must occur byte for byte in the exe; the dll's `ProductVersion` (fact 7, read by an inline `python3 -I` heredoc) must equal `<version>` | `The Companion publish output holds <files>, expected Censio.Companion.exe only.` (`<files>`: sorted, joined by `, `); `Censio.Companion.exe does not bundle the checked Censio.Companion.dll.`; `Censio.Companion.exe shows version '<actual>', expected '<version>'.` |
| `image <version> <image>` | `docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=<version> -t <image> .`, then `check-image` | as `check-image` |
| `check-image <image> <version>` | runs it on `127.0.0.1` with a random host port, waits up to 60 s for `/health` = `Healthy`, reads `Version <x>` from `/`, removes the container | `The server image did not answer Healthy on /health within 60 s.`; `The server image shows version '<actual>', expected '<version>'.` |

**`.github/workflows/release.yml`** (`name: Release`), `on: release: types: [published]`, `permissions: {}`, every
action pinned by commit, every checkout `persist-credentials: false`. Jobs:

1. `check` (`contents: read`): checkout with `fetch-depth: 0`; `tag`, `on-main <tag> origin/main`, `prerelease`;
   output `version`.
2. `companion-build` (`contents: read`, needs `check`): setup .NET, `companion`, upload the ZIP as artifact.
3. `image-build` (`contents: read`, needs `check`): `image <version> censio-release:<version>`, output the image ID,
   `docker save` and upload the archive (retention 1 day).
4. `companion-publish` (`contents: write`, needs `companion-build`): download, `gh release upload <tag> <zip>
   --clobber` with `GH_TOKEN` in that step's `env` only. No checkout, no build.
5. `image-publish` (no token permissions, needs `image-build`): `dockerhub-secrets` with the booleans
   `secrets.X != ''` (record 0008's pattern), download, `docker load`, compare the image ID (`The loaded image is
   <id>, expected the checked image <id>.`), `docker login --password-stdin` with both secrets in that step's `env`,
   tag and push `networlddev/censio:<version>`, output the pushed digest, `docker logout` with `if: always()`.
6. `promote` (`contents: write`, needs both publish jobs): refuse an empty digest
   (`No pushed digest for networlddev/censio:<version>; re-run all jobs.`); login as in 5; pull
   `networlddev/censio@<digest>`, tag and push `:latest`; read the registry digest of `:latest` and compare
   (`networlddev/censio:latest has digest <actual>, expected <digest>.`); logout; then `gh release edit <tag>
   --prerelease=false --latest`.

**`.github/workflows/ci.yml`**, new job `release-check` (`contents: read`, no secret): checkout, setup .NET,
`gh --version` (fact 12), `bash .github/scripts/release-check-test.sh`, then upload the ZIP the test built with
version `1.2.3` as artifact `companion-dry-run` (retention 14 days).

User-visible output: the annotation texts above are fixed; the documentation's layout is free for the Dev within
AC10, approved in step 9.

## Options considered

Weighed in [record 0012](../../docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md): one job per
artifact with Buildx and the metadata action (the issue's sketch) against secret-free build jobs with separate
publish and promote jobs (chosen); the Companion built on `windows-latest` against Linux (Linux chosen, facts 1–5,
dry run for fact 8); the metadata action against the plain `docker` CLI. Further:

- Version check of the exe: parsing the assembly metadata (a C# helper or a Python ECMA-335 reader) against the
  dll's `VERSIONINFO` `ProductVersion` plus the byte check that the bundle holds that dll (chosen: facts 5 and 7,
  no new code project, no new tool).
- Single-file compression (`EnableCompressionInSingleFile`): a smaller ZIP, but the bundled dll is no longer
  findable byte for byte and every start decompresses; rejected.
- E4's check in `check` (fails before anything is attached) against `image-publish` (as the spec words E4);
  `image-publish` chosen to match the spec.

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
  re-run failed jobs of the newest release only, `latest`/*Latest* move with the promotion, link record 0012;
  *getting-started* block — new bullets *Release check* (`ci.yml`, job `release-check`, local command) and
  *Release* (`release.yml`).
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
- Script injection: tag and other event values reach scripts only through `env`/arguments (fact 16).
- Secrets: Docker Hub secrets only in the `env` of the login steps of `image-publish` and `promote` and as booleans
  in the check step; `GH_TOKEN` only in the upload and edit steps; no build code runs in those jobs; `docker logout`
  always.
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
- Everything the spec lists under *Out of scope*.
