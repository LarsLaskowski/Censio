# Spec: Release workflow: pre-release, Windows ZIP, Docker image, promote

Source: Issue #5
Status: Draft

## Problem

Every milestone must be testable by people outside the repository, but today a release produces nothing: the only
workflow on a published release is `notify-website.yml`, the README tells testers to build the server image from a
checkout ("Until a published image exists"), and the Companion has no download at all — a local build writes a
framework-dependent `Censio.Companion.exe` that needs the .NET 10 Desktop Runtime (`docs/areas/companion.md`,
*Platform*). The Docker Hub repository `networlddev/censio` exists but holds no image. Every build carries the
version `0.0.0` unless a workflow passes another one (record 0005), so nothing today shows which release a
tester runs.

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
- S4: Given an older release `v0.1.0` and a new one `v0.2.0` run through S1, then `latest` on Docker Hub and the
  *Latest* mark on GitHub both point to `0.2.0`, and `networlddev/censio:0.1.0` and the `v0.1.0` asset are
  unchanged.

## Errors and edge cases

- E1: The tag is not `v` followed by `MAJOR.MINOR.PATCH` without leading zeros (`v1.2`, `v1.2.3-rc.1`, `1.2.3`,
  `v01.2.3`, `V1.2.3`): the run fails in its first job with the error
  `Release tag '<tag>' is not of the form vMAJOR.MINOR.PATCH.`; nothing is built, attached or pushed, and the
  release stays as it was published.
- E2: The tag points to a commit that is not on `main` (not reachable from the head of `main`, e.g. on a feature
  branch): the run fails in its first job with `Release tag '<tag>' points to commit <sha>, which is not on main.`;
  nothing is built, attached or pushed. A tag on an older commit of `main` is accepted.
- E3: The release was published as a full release (*Set as a pre-release* not ticked): the run shows the warning
  `Release <tag> was published as a full release; publish releases as pre-releases so they are not shown as the
  latest release before their artifacts exist.` and continues; at its end the release is marked *Latest* as in S1.
- E4: The Docker Hub secrets are not set: the Docker job fails with
  `Docker Hub credentials are missing: set the repository secrets DOCKERHUB_USERNAME and DOCKERHUB_TOKEN.`;
  nothing is pushed. Wrong credentials fail the login with Docker's own error. No log line of any job shows a
  secret's value.
- E5: The artifact built carries another version than the tag's (the exe's product version, or the start page
  of the built image): the job fails with `<artifact> shows version '<actual>', expected '<version>'.` before it
  attaches or pushes anything.
- E6: The built image does not answer `Healthy` on `/health` within 60 s of its start: the Docker job fails before
  it pushes anything.
- E7: Any job fails (build, check, upload, push, promote): the release stays a pre-release, `latest` on Docker Hub
  and the *Latest* mark on GitHub stay where they were. The other artifact may already exist (the ZIP attached, or
  the tag `<version>` pushed). The maintainer re-runs the failed jobs in the Actions run; that run finishes as S1.
- E8: A re-run meets an artifact that already exists: the ZIP asset of the same name is replaced and the image tag
  `<version>` is overwritten; the re-run does not fail on "already exists".
- E9: The tester's Windows shows the SmartScreen warning for the unsigned `Censio.Companion.exe` ("Windows protected
  your PC"): the user guide says how to start it anyway.

## Non-functional requirements

- Version: one string `MAJOR.MINOR.PATCH`, the tag without its `v`, used for every assembly (`-p:Version`,
  record 0005), the ZIP name, the image tag, the start page and the tray menu.
- Image tags: a run creates exactly `<version>` and `latest`, linux/amd64 only (as `docs/areas/server.md`); no
  `MAJOR.MINOR`, `MAJOR` or commit tags. `latest` moves only once both artifacts of the release exist.
- ZIP: holds `Censio.Companion.exe` at its root; extracted to any folder, it starts without an installer, without
  administrator rights and without an installed .NET. Windows versions: as the Product Manager answers (see *Open
  questions*).
- Security: Docker Hub credentials come only from the repository secrets `DOCKERHUB_USERNAME` and
  `DOCKERHUB_TOKEN`, available only to the steps that use them (record 0008), never printed; each job's token has
  the least permissions it needs (write access to the release only where it attaches or promotes); no persisted
  checkout credentials; third-party actions pinned by commit as in `ci.yml`. Only a user with write access to the
  repository can publish a release; a fork pull request cannot start the workflow.
- New configuration: the two repository secrets above, required, no default; the maintainer sets them before the
  first release.
- Logs: every failure of E1–E6 is an error annotation of the run with the text given there; E3 is a warning
  annotation.
- Compatibility: CI (`ci.yml`), the image check and the Dockerfile keep their behavior; the image is the one of
  feature 4, built with `VERSION=<version>`.
- GitHub runs a workflow triggered by a release from the tagged commit, so the workflow can run for real only after
  it is merged to `main`.

## Acceptance criteria

- [ ] AC1: Publishing a pre-release with tag `v<version>` on `main` ends in a green run with the asset
  `Censio-<version>-win-x64.zip` attached, the image tags `<version>` and `latest` pushed with the same digest for
  linux/amd64 and no other tag created, and the release turned into a full release marked *Latest*; a later
  release moves `latest` and *Latest* and leaves the earlier ones' artifacts unchanged (S1, S4).
- [ ] AC2: The ZIP holds `Censio.Companion.exe` at its root, which on Windows x64 without .NET starts from any
  extracted folder and shows `Version <version>` in its tray menu (S2).
- [ ] AC3: The pushed image, started with the README's command, answers `Healthy` on `/health` and shows
  `Version <version>` on its start page; the Docker job checks both on the built image before it pushes (S3, E6).
- [ ] AC4: A tag not of the form of E1 fails the run with E1's error before anything is built, attached or pushed
  (E1).
- [ ] AC5: A tag on a commit not reachable from `main` fails the run with E2's error before anything is built,
  attached or pushed; a tag on an older commit of `main` passes (E2).
- [ ] AC6: A release published as a full release gets E3's warning and otherwise runs as AC1 (E3).
- [ ] AC7: Without the Docker Hub secrets the Docker job fails with E4's error and pushes nothing; no job log
  contains a secret's value (E4).
- [ ] AC8: An artifact whose version differs from the tag's fails its job with E5's error before it is attached or
  pushed (E5).
- [ ] AC9: After any failed job the release stays a pre-release and neither `latest` nor *Latest* moves; re-running
  the failed jobs replaces existing artifacts without error and ends as AC1 (E7, E8).
- [ ] AC10: README and user guide (English first, then German, one file each) tell testers how to download and
  start the ZIP, including the SmartScreen step, and how to pull and run the published image by version or
  `latest`; `docs/CONTRIBUTING.md` describes how a maintainer publishes a release and which secrets it needs (E9).

## Out of scope

- Checksums, release-note categories and text, the image digest in the notes (#44); edge builds (#6).
- Code signing of the exe; arm64 and other platforms; pre-release versions such as `v1.2.3-rc.1` (E1 rejects them).
- A release with a version lower than the current *Latest* (a hotfix of an older line): it runs as AC1 and moves
  `latest` and *Latest* back to it; no guard.
- Two releases published at the same time: no ordering is promised between their runs.
- Removing a test release, its tag or its image tags; that stays a manual step.
- A Windows job in CI that starts the exe; AC2 is checked by hand on Windows.

## Open questions

- Which file is `release.yml`? Answered: `.github/release.yml` already exists and configures GitHub's generated
  release notes; the workflow is `.github/workflows/release.yml` and leaves that file alone.
- What does "warns if not published as pre-release" do? Answered: a warning annotation, and the run continues
  (E3); the issue says "warns", not "fails".
- What is "a tag on main"? Answered: the tagged commit is reachable from the head of `main` at run time (E2);
  CONTRIBUTING: "a `v<major>.<minor>.<patch>` tag created manually on `main`".
- Can the workflow be tried before the merge? Answered: no — GitHub takes a release-triggered workflow from the
  tagged commit, and E2 refuses commits not on `main`; the issue's "test tag" criterion is met by a release after
  the merge.
- Does the promotion start `notify-website.yml` a second time? Answered: no — it listens to `published`, which a
  promotion does not raise, and events raised with the workflow's own token start no workflow.
- Does the issue's "Docker job pushes `:latest`" allow `latest` to move before the ZIP exists? The Lead's
  reading, for the Product Manager to confirm: the issue's model is "pre-release first, then promote after the artifacts
  exist", so `latest` moves with the promotion (NFR *Image tags*, AC9). Answered: no (Lead).
- Target hosts and the first release. Answered: not yet — escalated to the Product Manager: which Windows versions
  the ZIP must run on and whether the SmartScreen warning with a documented "More info → Run anyway" is
  acceptable; whether the secrets `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` (a Docker Hub access token with write
  access to `networlddev/censio`) are the names to use; which version the first real release uses and whether it
  stays published; whether `notify-website.yml` may keep firing when the pre-release is published, before its
  artifacts exist.

## Area documents

- `docs/areas/server.md`, *Docker image*: the image is published to Docker Hub as `networlddev/censio:<version>`
  and `latest` for every release, `latest` being the last promoted release; *Not here* drops #5.
- `docs/areas/companion.md`, *Platform*: the release ZIP `Censio-<version>-win-x64.zip` holds a self-contained
  single-file `Censio.Companion.exe` that needs no installed .NET (a local build still does); *Not here* drops the
  release ZIP; the index row in `docs/areas/README.md` follows.
- The release process itself is not an area (`docs/areas/README.md`): it goes into `docs/CONTRIBUTING.md`
  (*Versioning and releases*), and `docs/ARCHITECTURE.md` (*Deployment*) names the pre-release and promote steps.
