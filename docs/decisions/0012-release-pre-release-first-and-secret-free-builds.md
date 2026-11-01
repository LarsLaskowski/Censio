# 0012: Releases run as pre-release first; artifacts are built without secrets and promoted by digest

- **Status:** Accepted
- **Date:** 2026-10-31
- **Area:** —
- **Source:** Issue #5, issue #74, issue #78
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
   cannot make, in an order (`docs/CONTRIBUTING.md`, *Versioning and releases*). The refusal on a branch ref and the
   creation of an unknown environment without a rule (below) are GitHub's documented behavior, not yet measured here
   (see `docs/CONTRIBUTING.md`, *Versioning and releases*, *Not yet tried*).
7. **As 6, with required reviewers on the environment** — a second person approves every push; rejected: the
   project has one administrator, who also publishes the release. The tag ruleset decides only who creates, moves or
   deletes `v*` tags; anyone with write access can still publish a release on an existing tag or re-run a release
   run, and a reviewer would gate those — the harm they can do is answered by option 10 instead.
8. **A branch rule `main` instead of the tag rule** — rejected: a run of a `release` event runs on the tag's ref,
   so a branch rule refuses every release.
9. **New secret names that exist only in the environment** — would fail closed when the environment was never set
   up, but repository secrets of the old names would still be readable; deleting them is needed either way, and
   with them deleted, a missing environment fails closed at the credentials check as well. Rejected.

Which release may move `latest` (issue #78): anyone with write access can delete the release of an existing `v*` tag
and publish it again, or re-run any release run, including *Re-run failed jobs* of only its `promote` job. Either
runs the reviewed workflow of that tag with the environment secrets and, for an older tag, moves Docker Hub `latest`
and GitHub's *Latest* back. No `v*` tag exists yet, so a check in the workflow is part of every tag ever released.
GitHub's *Latest* mark is a release setting: a writer also sets it without any run, by hand, by publishing an old
tag's release as a full release (marked *Latest* before any job) or by deleting the highest release.

10. **`release-check.sh newest`: the run refuses a tag when a `vMAJOR.MINOR.PATCH` tag of a higher version exists,
    in the `check` job and again in the `promote` job right before `latest` moves** — tags are created only by
    administrators, so the comparison rests on nothing a writer can change; the check in `promote` also covers a
    re-run of that job alone, whose `check` job does not run again, and two releases published close together;
    tested offline like `on-main`. Versions compare field by field by length, then as strings, so no field length
    overflows. Releasing a lower version is not supported while a higher tag exists, and a writer can still
    republish the highest release, which rebuilds and re-promotes the same version of the same commit.
11. **Compare against GitHub's current *Latest* release instead of the tags** — a writer can delete or edit releases,
    so the reference would be writer-controlled, and the check needs the API in the test. Rejected.
12. **Skip only the promotion of an older tag and publish the rest** — still re-pushes `<version>` and re-attaches
    the ZIP of an old release on a writer's demand; refusing in `check` stops before any build. Rejected.
13. **Required reviewers on the environment, the administrator approving his own runs (option 7 without a second
    person)** — gates every writer-started run whatever its workflow, but adds approvals to every release and is a
    setting the workflow cannot see and no test can exercise. Rejected for option 10; it stays available if
    writer-started runs must stop altogether.
14. **The workflow checks that `github.triggering_actor` is an administrator** — needs a permission lookup whose
    reach with the job's read-only token is unmeasured, and holds only for workflows of tags created after it,
    like option 10. Rejected.
15. **Accept and document it** (the first version) — rejected for Docker Hub `latest`: option 10 closes it at the
    cost of one script command.
16. **Also protect GitHub's *Latest* mark** — not possible from the workflow: a check runs only when a run does, and
    setting the mark needs none. Accepted as a residual; the administrator restores it on the highest release.

The checks themselves live in `.github/scripts/release-check.sh`, which the workflow and a CI test job
(`release-check-test.sh`, every pull request) both run, so a refusal is tested before a release needs it.

## Decision

Option 2: build jobs hold no secret and a read-only token, publish and promote jobs run no build code, and
`latest` is set at the promotion, registry-side, to the digest that passed the checks. Values passed between jobs
and artifacts are treated as untrusted in the credentialed jobs, and the image is one `linux/amd64` manifest
without a provenance attestation. The Docker Hub secrets and `WEBSITE_DISPATCH_TOKEN` are secrets of the environment `release` only (option 6).
A run whose tag is not the highest `vMAJOR.MINOR.PATCH` tag stops in `check` and, again, in `promote` before
`latest` moves (option 10); GitHub's *Latest* mark stays unprotected (option 16).
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
- Runs of different releases are not serialized, and need not be: of two releases published close together, the
  older one's `promote` finds the newer tag and stops, so Docker Hub `latest` ends on the newer one. Only a tag
  created in the seconds between the check in `promote` and the move of `latest` is not noticed.
- Releasing a lower version (a fix for an older line) is not supported while a higher `v*` tag exists, and a failed
  release can no longer be completed once a higher tag was created.
- GitHub's *Latest* mark is not protected: anyone with write access can set it on any release (option 16).
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
  run or publish a release on an existing `v*` tag: that runs the reviewed workflow of that tag with the secrets but
  discloses nothing; for any tag but the highest it never moves Docker Hub `latest` (option 10) — a re-run of only
  `image-publish` or `companion-publish` re-pushes that run's `<version>` or re-attaches its ZIP from its own checked
  artifacts (within 7 days) and stops at `promote` — and for the highest it rebuilds and re-promotes the same version. The `notify-website.yml` dispatch runs for such a release as well.
  The `contents: write` token is no environment secret and is not affected.
