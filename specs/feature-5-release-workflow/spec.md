# Spec: Release workflow: pre-release, Windows ZIP, Docker image, promote

Source: Issue #5
Status: Approved by Product Manager

## Problem

Every milestone must be testable by people outside the repository, but a release produces nothing today: testers
must build the server image from a checkout (README, *Docker*), the Companion has no download, and a local build of
it needs the .NET 10 Desktop Runtime (`docs/areas/companion.md`). `networlddev/censio` on Docker Hub holds no image,
and every build shows `0.0.0` unless a workflow passes a version (record 0005).

## Scenarios

- S1: Given `main` holds the release workflow, when a maintainer publishes on GitHub a release with the new tag
  `v0.1.0` on the head of `main` and *Set as a pre-release* ticked, then the run of the workflow `Release` ends
  green, the release lists the asset `Censio-0.1.0-win-x64.zip`, Docker Hub holds `networlddev/censio:0.1.0` and
  `networlddev/censio:latest` with the same digest, and the release is no longer a pre-release but is marked
  *Latest*. While the run is in progress the release is shown as a pre-release.
- S2: Given the ZIP of S1 on a Windows x64 machine without any .NET installed, when a tester extracts it to a
  folder of their choice and starts `Censio.Companion.exe`, then the tray icon appears and its menu shows
  `Version 0.1.0`.
- S3: Given a Docker host as described in the README, when a tester runs
  `docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.1.0` (or `:latest`),
  then `http://localhost:8080/health` answers `Healthy` and the start page shows `Version 0.1.0`.
- S4: After `v0.1.0`, a release `v0.2.0` run through S1 moves `latest` and *Latest* to `0.2.0` and leaves the
  `0.1.0` image and ZIP unchanged.

## Errors and edge cases

- E1: The tag is not `v` plus `MAJOR.MINOR.PATCH` without leading zeros (`v1.2`, `v1.2.3-rc.1`, `1.2.3`,
  `v01.2.3`, `V1.2.3`): the run fails in its first job with
  `Release tag '<tag>' is not of the form vMAJOR.MINOR.PATCH.`; nothing is built, attached or pushed.
- E2: The tag points to a commit that is not on `main` (not reachable from the head of `main`, e.g. on a feature
  branch): the run fails in its first job with `Release tag '<tag>' points to commit <sha>, which is not on main.`;
  nothing is built, attached or pushed. A tag on an older commit of `main` that already holds the release workflow
  is accepted. Limits: GitHub runs the workflow of the tagged commit, so a tag on a commit of `main` from before
  the workflow was merged starts no run at all, and a tag on a branch whose workflow differs is not refused. The
  refusal guards against a maintainer's mistake; it is not a security control.
- E3: The release was published as a full release (*Set as a pre-release* not ticked): the run shows the warning
  `Release <tag> was published as a full release; publish releases as pre-releases so they are not shown as the
  latest release before their artifacts exist.` and continues; at its end the release is marked *Latest* as in S1.
  GitHub may mark such a release *Latest* already when it is published; if a job then fails, it stays a full release
  with artifacts possibly missing (only `latest` on Docker Hub stays where it was, as in E7).
- E4: The Docker Hub secrets are not set: the Docker job fails with
  `Docker Hub credentials are missing: set the repository secrets DOCKERHUB_USERNAME and DOCKERHUB_TOKEN.`;
  nothing is pushed. Wrong credentials fail the login with Docker's own error. No log line of any job shows a
  secret's value.
- E5: The artifact built carries another version than the tag's — for the ZIP the informational version of the
  `Censio.Companion` assembly in the published exe (the value the tray menu reads), for the image the version on
  the start page of the built image: the job fails with `<artifact> shows version '<actual>', expected '<version>'.`
  before it attaches or pushes anything.
- E6: The built image does not answer `Healthy` on `/health` within 60 s of its start: the Docker job fails before
  it pushes anything.
- E7: Any job of a release published as a pre-release fails (build, check, upload, push, promote): the release
  stays a pre-release; `latest` and *Latest* stay where they were. The other artifact may already exist. The
  maintainer re-runs the failed jobs within 7 days (later all jobs, E8); that run finishes as S1. Only a failure inside the promotion, after `latest`
  moved and before *Latest* is set, leaves the two apart until that re-run.
- E8: A re-run meets an existing artifact: the ZIP asset is replaced and the tag `<version>` overwritten by the
  re-built image, checked again as in E5 and E6; the re-run does not fail on "already exists".
- E9: The tester's Windows shows the SmartScreen warning for the unsigned `Censio.Companion.exe` ("Windows protected
  your PC"): the user guide says how to start it anyway.
- E10: The published Companion output holds any file besides `Censio.Companion.exe` (a native library left beside
  it, a `.pdb`): the Windows job fails with `The Companion publish output holds <files>, expected
  Censio.Companion.exe only.` before it attaches anything.

## Non-functional requirements

- Version: one string `MAJOR.MINOR.PATCH`, the tag without its `v`, used for every assembly (`-p:Version`,
  record 0005), the ZIP name, the image tag, the start page and the tray menu.
- Image tags: a run creates exactly `<version>` and `latest`, linux/amd64 only (as `docs/areas/server.md`); no
  `MAJOR.MINOR`, `MAJOR` or commit tags. `latest` moves only once both artifacts of the release exist. The image
  pushed as `<version>` is the image that passed the checks of E5 and E6 (same digest), and `latest` is set to that
  digest at the promotion, without a second build.
- ZIP: exactly one file, `Censio.Companion.exe`, at its root, native libraries embedded; extracted anywhere, it
  starts without installer, administrator rights or installed .NET, on Windows 10 22H2 and Windows 11, x64.
- Security: Docker Hub credentials only from the secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`, available
  only to the steps that use them (record 0008), never printed; each job's token has
  the least permissions it needs (write access to the release only where it attaches or promotes); no persisted
  checkout credentials; third-party actions pinned by commit as in `ci.yml`. Only a user with write access to the
  repository can publish a release; a fork pull request cannot start the workflow.
- New configuration: the two repository secrets above, required, no default; both already exist in the repository.
- Logs: every failure of E1, E2, E4–E6 and E10 is an error annotation of the run with the text given there; E3 is a warning
  annotation.
- Compatibility: the existing CI jobs, the image check and the Dockerfile keep their behavior; the image is the one
  of feature 4, built with `VERSION=<version>`.
- GitHub runs a workflow triggered by a release from the tagged commit, so the workflow can run for real only after
  it is merged to `main`. The refusals and checks of E1, E2, E3, E4, E5 and E10 can therefore not wait for a real
  release: CI runs them on every pull request against sample inputs (tags of each form of E1, a commit on and off
  `main`, empty secrets, a full release, artifacts built with another version, a publish output with an extra file),
  each case asserting the exact text of its annotation. A real release shows only the usual path.
- Verification: each criterion names it — `CI` (every pull request), `review` (the workflow read by Reviewer and
  Security) or `v0.1.0` (first real release, by the Product Manager).

## Acceptance criteria

- [ ] AC1: Publishing a pre-release with tag `v<version>` on `main` ends in a green run with the asset
  `Censio-<version>-win-x64.zip` attached, the image tags `<version>` and `latest` pushed with the same digest for
  linux/amd64 and no other tag created, and the release turned into a full release marked *Latest*; a later
  release moves `latest` and *Latest* and leaves the earlier ones' artifacts unchanged (S1, S4). Verified: `v0.1.0`
  (S4 at the next release); `review` that only the promotion moves `latest` and *Latest*.
- [ ] AC2: The ZIP holds exactly `Censio.Companion.exe` at its root, which on Windows x64 without .NET starts from
  any extracted folder and shows `Version <version>` in its tray menu; a publish output with another file list fails
  the Windows job with E10's error before anything is attached (S2, E10). Verified: `CI` for the file list and
  E10; a dry run of the ZIP the CI job builds, on Windows without .NET, before the review, and `v0.1.0`, for the
  start and the tray menu.
- [ ] AC3: The pushed image, started with the README's command, answers `Healthy` on `/health` and shows
  `Version <version>` on its start page; the Docker job checks both on the built image before it pushes, and the
  pushed digest is the checked one (S3, E6). Verified: `CI` for the checks on a built image and for E6's failure;
  `v0.1.0` for the pushed image; `review` for the digest.
- [ ] AC4: A tag not of the form of E1 fails the run with E1's error before anything is built, attached or pushed
  (E1). Verified: `CI` with every example of E1 and a valid tag; `review`.
- [ ] AC5: A tag on a commit not reachable from `main` fails the run with E2's error before anything is built,
  attached or pushed; a tag on an older commit of `main` passes; `docs/CONTRIBUTING.md` states E2's limits (E2).
  Verified: `CI` with a commit on and one off `main`; `review`.
- [ ] AC6: A release published as a full release gets E3's warning and otherwise runs as AC1 (E3). Verified: `CI`
  for the warning; `review` that it does not stop the run.
- [ ] AC7: Without the Docker Hub secrets the Docker job fails with E4's error and pushes nothing; no job log
  contains a secret's value (E4). Verified: `CI` with empty secrets; `review` that the secrets reach only the
  steps that use them; `v0.1.0` by reading the run's logs.
- [ ] AC8: An artifact whose version differs from the tag's fails its job with E5's error before it is attached or
  pushed (E5). Verified: `CI` with an exe and an image built with another version than expected.
- [ ] AC9: After any failed job of a release published as a pre-release, the release stays a pre-release and
  neither `latest` nor *Latest* moves (except E7's failure inside the promotion); re-running the failed jobs replaces existing artifacts without error and ends
  as AC1 (E7, E8). Verified: `review` (the promotion waits for both artifact jobs; the upload replaces an asset of
  the same name; the push overwrites the tag); on a real release only if a job fails.
- [ ] AC10: README and user guide (English first, then German, one file each) are rewritten for the published
  artifacts: the *Docker* section no longer says "Until a published image exists" and starts with
  `docker pull networlddev/censio:<version>` (or `latest`); every `docker run` command uses
  `networlddev/censio:<version>` instead of the local name `censio`; the NAS part pulls the image on the NAS instead
  of building or moving it; building from a checkout stays as a secondary subsection. A new *Companion* section
  (and its German copy) says where to download the ZIP, that it needs no .NET, how to extract and start it, the
  SmartScreen step "More info → Run anyway", and the Windows versions. `docs/CONTRIBUTING.md` describes
  how a maintainer publishes a release (pre-release ticked), the secrets it needs, E2's limits and that only the
  newest release's run is re-run (E9). Verified: `review`.

## Out of scope

- Checksums, release-note categories and text, the image digest in the notes (#44); edge builds (#6).
- Code signing of the exe; arm64 and other platforms; pre-release versions such as `v1.2.3-rc.1` (E1 rejects them).
- A guard against moving `latest` and *Latest* backwards: a release of a lower version, or a re-run of an older
  release's run after a newer promotion, runs as AC1 and moves both back (CONTRIBUTING warns). No ordering between
  two releases published at once.
- The version in the exe's Windows file properties; removing a release, its tag or its image tags.
- A Windows job in CI that starts the exe; AC2 is checked by hand on Windows.
- When `notify-website.yml` fires: it keeps firing on the publication of the pre-release, before its artifacts
  exist.

## Open questions

- Which file is `release.yml`? Answered: `.github/workflows/release.yml`; `.github/release.yml` stays.
- What does "warns" do, and what is "a tag on main"? Answered: a warning annotation, the run continues (E3); the
  tagged commit is reachable from the head of `main` (E2, CONTRIBUTING *Versioning and releases*).
- Can the workflow run before the merge? Answered: no — GitHub runs the tagged commit's workflow (NFR, last bullets).
- Does the promotion start `notify-website.yml` again? Answered: no — it listens to `published` only.
- When does `latest` move? Answered: (Product Manager) with the promotion, after both artifacts exist.
- Target hosts and the first release. Answered: (Product Manager) Windows 10 22H2 and 11 x64 without .NET or
  administrator rights; unsigned exe with the documented SmartScreen step; the secrets `DOCKERHUB_USERNAME` and
  `DOCKERHUB_TOKEN` already exist; the "test tag" is `v0.1.0`, a pre-release after the merge that stays published;
  `notify-website.yml` keeps firing on the pre-release.

## Area documents

- `docs/areas/server.md`, *Docker image*: the image is published to Docker Hub as `networlddev/censio:<version>`
  and `latest` for every release, `latest` being the last promoted release; *Not here* drops #5.
- `docs/areas/companion.md`, *Platform*: the release ZIP `Censio-<version>-win-x64.zip` holds only a self-contained
  single-file `Censio.Companion.exe` (native libraries embedded) that needs no installed .NET, on Windows 10 22H2 and
  Windows 11 x64 (a local build still does); *Not here* drops the
  release ZIP; the index row in `docs/areas/README.md` follows.
- The release process itself is not an area (`docs/areas/README.md`): it goes into `docs/CONTRIBUTING.md`
  (*Versioning and releases*), and `docs/ARCHITECTURE.md` (*Deployment*) names the pre-release and promote steps.

## Challenge

- M1 (failure paths uncheckable): accepted. E1–E5 and E10 run in CI on every pull request against sample inputs
  (NFR), and every criterion names its verification. Left for `v0.1.0`: AC1, the ZIP's start (AC2), the pushed image
  (AC3), AC7's logs; AC9's failure path is shown by review only.
- m1 (E3 vs. E7/AC9): accepted. E7 and AC9 apply to pre-releases only; E3 states what a failed full release keeps.
  Reverting it to a pre-release is not added.
- Plan step (Lead): E7 gained two windows, AC2 a dry run; m4 measured.
- m2 (E2's limits): accepted (E2, AC5, AC10).
- m3 ("product version"): accepted. E5 checks the informational version the tray reads; file properties are out of
  scope.
- m4 (ZIP contents): accepted. Exactly `Censio.Companion.exe`, checked by the job (E10, AC2); the plan verifies that
  a WPF single-file publish yields one file, else the spec returns with the named extra files.
- m5 (tested = pushed image; old re-run): accepted. NFR *Image tags* and AC3 require the checked digest to be pushed
  and promoted without a second build; re-running an older run is out of scope, CONTRIBUTING warns.
- m6 (documentation): accepted (AC10).

