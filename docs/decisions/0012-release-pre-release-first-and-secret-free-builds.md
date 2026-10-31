# 0012: Releases run as pre-release first; artifacts are built without secrets and promoted by digest

- **Status:** Accepted
- **Date:** 2026-10-31
- **Area:** —
- **Source:** Issue #5, issue #74
- **Supersedes:** —

## Context

A published GitHub release with a `vMAJOR.MINOR.PATCH` tag on `main` must produce the Companion ZIP and the server
image `networlddev/censio` and only then appear as the latest release (spec of #5). The release workflow needs two
write credentials: the Docker Hub token (`DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`) and a `GITHUB_TOKEN` with
`contents: write`. Building runs code nobody reviewed line by line — the build logic of every restored NuGet package
and every base image layer — and on a GitHub-hosted runner any step of a job can read every secret the job
references (record 0008). GitHub runs a release-triggered workflow from the tagged commit, so the workflow runs
for real only after it is merged, and its refusals must be tested before that.

## Options considered

1. **One job per artifact that builds, checks and publishes (the issue's sketch: Buildx and the metadata action
   push `<version>` and `latest` from the build job)** — fewest jobs; the Docker Hub token and the write token sit
   in jobs that run the build, and `latest` moves before the ZIP exists.
2. **Build-and-check jobs without any secret and with a read-only token; separate publish jobs that only load,
   push or upload the checked artifact; a promote job that moves `latest` to the pushed digest and marks the
   release *Latest*** — no build code runs in a job that holds a write credential; the image is not built twice;
   two more jobs and an image archive passed between jobs.
3. **As 2, with the Companion built on `windows-latest`** — the apphost gets the version resource and manifest of a
   Windows build; the checks and their CI tests would run on two platforms, and the session cannot reproduce it.
   Rejected: a Linux self-contained single-file publish yields one GUI-subsystem exe with the WPF native libraries
   embedded (measured with SDK 10.0.403), the Companion is built on Linux everywhere else (record 0004), and a dry
   run of the ZIP on Windows before the merge covers the residual risk.
4. **The metadata action for tags and labels** — adds a third-party action to a job that pushes; the run creates two
   fixed tags only. Rejected for the plain `docker` CLI.

Where the release's write credentials live (issue #74) — the Docker Hub secrets and `WEBSITE_DISPATCH_TOKEN` of the
release-triggered `notify-website.yml`: as repository secrets, every job of every workflow on every branch can
reference them, so anyone who can push a branch can read them with a workflow of their own.

5. **Repository secrets** (the first version) — no setup; readable from any branch workflow.
6. **Secrets of a GitHub environment `release` whose deployment rule admits only tags `v*`, referenced by the
   jobs `image-publish`, `promote` and `dispatch` alone** — a job on a branch ref is refused before its first step;
   the repository's tag ruleset lets only administrators create, move or delete `v*` tags. The rule is of type Tag:
   a branch rule `v*` would admit any branch named `v…`, which every writer can push. Needs settings the workflow
   cannot make, in an order (`docs/CONTRIBUTING.md`, *Versioning and releases*).
7. **As 6, with required reviewers on the environment** — a second person approves every push; rejected: the
   project has one administrator, who also publishes the release. The tag ruleset decides only who creates, moves or
   deletes `v*` tags; anyone with write access can still publish a release on an existing tag or re-run a release
   run, and a reviewer would gate those — accepted without, as they run the reviewed workflow of an existing tag.
8. **A branch rule `main` instead of the tag rule** — rejected: a run of a `release` event runs on the tag's ref,
   so a branch rule refuses every release.
9. **New secret names that exist only in the environment** — would fail closed when the environment was never set
   up, but repository secrets of the old names would still be readable; deleting them is needed either way, and
   with them deleted, a missing environment fails closed at the credentials check as well. Rejected.

The checks themselves live in `.github/scripts/release-check.sh`, which the workflow and a CI test job
(`release-check-test.sh`, every pull request) both run, so a refusal is tested before a release needs it.

## Decision

Option 2: build jobs hold no secret and a read-only token, publish and promote jobs run no build code, and
`latest` is set at the promotion, registry-side, to the digest that passed the checks. Values passed between jobs
and artifacts are treated as untrusted in the credentialed jobs, and the image is one `linux/amd64` manifest
without a provenance attestation. The Docker Hub secrets and `WEBSITE_DISPATCH_TOKEN` are secrets of the environment `release` only (option 6).
What a release produces and shows is in
[`areas/server.md`](../areas/server.md) (*Docker image*) and [`areas/companion.md`](../areas/companion.md)
(*Platform*); how a maintainer runs a release is in `docs/CONTRIBUTING.md` (*Versioning and releases*).

## Consequences

- Malicious build code can still produce a malicious artifact that a publish job then pushes; it cannot read the
  Docker Hub token or the write token. The checks (version, file list, health) are not a defense against that.
- The tag check refuses mistakes, not attackers: a tag on a branch whose workflow differs is not refused, and a tag
  on a `main` commit from before this workflow starts no run. It checks the tag as the `check` job fetches it while
  the build jobs check out the event's commit; a tag moved during the run is not noticed. The check is not also
  tied to the event's commit: that gap needs the same write access as, and is narrower than, the branch-workflow one.
- Runs of different releases are not serialized: two releases published close together may promote in either order
  and leave `latest` and *Latest* on the older one, as a lower version does. A `concurrency` group was not added; the
  spec of #5 leaves the order of releases out of scope.
- The Companion exe built on Linux carries no Windows version resource of its own; the file properties show no
  product version. Revisit with a Windows build if testers need it, or with code signing.
- Build code can write job outputs and upload artifacts; the publish and promote jobs therefore check the form of
  every output, read one artifact file by fixed name from outside the checkout and execute nothing from it.
- A failed run can be completed with "Re-run failed jobs" only while its artifacts are kept (7 days).
- The promotion moves Docker Hub `latest` before GitHub's *Latest* mark; a failure between the two leaves them
  apart until the promote job is re-run.
- The workflow cannot see the environment's settings. A run that references an environment that does not exist
  creates it without any rule, and a repository secret of the same name stays readable from every branch; the
  protection holds only once the maintainer has set up the environment and deleted the repository secrets. Without
  any Docker Hub secret, the credentials check fails and the release stays a pre-release.
- The environment admits the workflow of whatever commit a `v*` tag points to; it closes branch workflows, not
  `main`. While the ruleset on `main` lets the write and maintain roles bypass its pull request rule, a writer can
  push a changed workflow to `main`, and the administrator's next release runs it with the secrets
  ([issue #77](https://github.com/LarsLaskowski/Censio/issues/77)). Anyone with write access can also re-run a release
  run or publish a release on an existing `v*` tag: that runs the reviewed workflow of that tag with the secrets and
  may move `latest` back, but discloses nothing. The `contents: write` token is no environment secret and is not
  affected.
