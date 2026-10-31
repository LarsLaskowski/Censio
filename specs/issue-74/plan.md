# Plan: Release secrets in a GitHub environment limited to v* tags

Source: Issue #74
Status: Draft
Tier: security — moves the Docker Hub credentials of the release behind a new control (an environment whose deployment rule admits only `v*` tags), which is the trust of the release (CI secrets).
Security areas touched: Release publishing (`.github/workflows/release.yml`, `.github/scripts/release-check.sh`, record 0012); CI configuration (`release.yml`, the `release-check` CI test `.github/scripts/release-check-test.sh`); repository settings the maintainer changes (environment `release`, repository secrets)

## Problem / root cause

`release.yml` reads `secrets.DOCKERHUB_USERNAME` and `secrets.DOCKERHUB_TOKEN` in the jobs `image-publish`
(`.github/workflows/release.yml:168-184`) and `promote` (`:217-227`), and neither job names an `environment`, so the
secrets can only be repository secrets (CONTRIBUTING.md:199-200 says so). A repository secret is readable by any job
of any workflow on any ref, so anyone who can push a branch can add a workflow that prints it. The issue's claim is
confirmed. The fix has two halves: the workflow references an environment `release` (this PR), and the
maintainer creates that environment with a `v*` tag rule, puts the secrets into it and deletes the repository
secrets (settings no agent can change). The workflow half alone protects nothing (facts 4, 5).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Only `image-publish` and `promote` reference the Docker Hub secrets; no job of `release.yml` has an `environment` | `release.yml:168-184`, `:217-227`; `grep -rn "environment:" .github/workflows/` is empty | confirmed |
| 2 | The missing-secret message tells the maintainer to set *repository* secrets, and the CI test pins that text | `release-check.sh:132`; `release-check-test.sh:212` | confirmed |
| 3 | No release exists yet: nothing is released, record 0012 is edited in place | `git ls-remote --tags origin` is empty | confirmed |
| 4 | A run that references an environment that does not exist creates it, with no protection rule | to verify: Security in step 3 against the GitHub documentation (source: GitHub docs, *Managing environments for deployment* (not reachable from the session)) | unverified |
| 5 | Repository secrets stay readable by every workflow while they exist; in a job with an environment, an environment secret of the same name wins | to verify: Security in step 3 against the GitHub documentation (source: GitHub docs, *Using secrets in GitHub Actions* (environment secret precedence)) | unverified |
| 6 | Environment secrets reach only a job that names the environment, and only after its deployment rules passed; a job on a refused ref fails before its first step | to verify: Security in step 3; at runtime by the maintainer in the first release (R1) (source: GitHub docs, *Deployment branches and tags*) | unverified |
| 7 | A `release` event runs on `refs/tags/<tag>`, so a tag rule `v*` admits it and a branch rule `main` would refuse it | to verify: maintainer in the first release run (R1) (source: GitHub docs, *Events that trigger workflows*, `release` (`GITHUB_REF` is the tag ref)) | unverified |
| 8 | A job needs no `deployments` permission of the `GITHUB_TOKEN` to run in an environment; `contents: read` of `image-publish` and `contents: write` of `promote` stay as they are | to verify: maintainer in the first release run (R2) (source: GitHub creates the deployment itself) | unverified |
| 9 | The repository is public, so environments with deployment rules are available on the free plan | `gh api repos/larslaskowski/censio --jq .visibility` → `public` | confirmed |
| 10 | Ruleset `tag-Protection` (active) restricts creation, update and deletion of `refs/tags/v*`; the only bypass actor is `RepositoryRole` 5 | `gh api repos/larslaskowski/censio/rulesets/26232655` | confirmed |
| 11 | `RepositoryRole` 5 is the admin role, so only administrators create `v*` tags; CONTRIBUTING.md:188 ("a maintainer (write access)") is therefore stale | to verify: the Product Manager reads the bypass list of `tag-Protection` under *Settings, Rules* during the settings dry run (source: GitHub REST docs on ruleset bypass actors) | unverified |
| 12 | The workflow cannot read the environment's configuration (that needs an administration token the run does not have), so no step can check that the rule exists | `release.yml` jobs hold `contents` permissions only | confirmed |
| 13 | No other workflow, area document, `AGENTS.md` project block or `SECURITY.md` names the Docker Hub secrets | `grep -rni "dockerhub\|repository secret"` over `.github/`, `docs/`, `AGENTS.md`, `.github/copilot-instructions.md`, `SECURITY.md`, `README.md` | confirmed |

## Acceptance criteria

- [ ] AC1: In `.github/workflows/release.yml` the jobs `image-publish` and `promote`, and no other job, declare
  `environment: release`; nothing else in the workflow changes except its header comment.
- [ ] AC2: `release-check.sh dockerhub-secrets` with either value not `true` fails with exactly
  `Docker Hub credentials are missing: set DOCKERHUB_USERNAME and DOCKERHUB_TOKEN as secrets of the environment release.`;
  with both `true` it passes silently (unchanged).
- [ ] AC3: `docs/CONTRIBUTING.md`, *Versioning and releases*, says the Docker Hub credentials are secrets of the
  environment `release` and must not exist as repository secrets, lists the one-time setup in the order of
  *Approach*, says that only administrators can create `v*` tags (ruleset `tag-Protection`), and marks the
  setup as not yet tried until the release `v0.1.0`.
- [ ] AC4: `.squad/project.md` (*Security areas*, *Release publishing*) and `docs/ARCHITECTURE.md` (*Deployment*)
  name the environment `release` as where the Docker Hub credentials live.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC2 | step `AC7: dockerhub-secrets`: the three `expect_error` cases compare the new text; `AC7 both set` unchanged | `.github/scripts/release-check-test.sh` |

The changed expectation fails on the current `release-check.sh` (step 5). `release-check-test.sh` needs Docker,
`dotnet` and `zip` for its later steps; where the session lacks one, the Tester runs it as far as it gets (the AC7
step comes before the Companion and image steps) and the CI job `release-check` runs it in full on the draft PR.

## Verification without tests

Steps 4 (no API) and the *Coverage gate* are not applicable: no C# changes and shell scripts are outside the
coverage measure. Step 5 is the one changed expectation above.

- AC1: read-only check by the Reviewer (`grep -n "environment:" .github/workflows/release.yml` shows exactly two
  lines, in `image-publish` and `promote`); `actionlint` through the *Analyzer gate* by the orchestrator in step 7.
- AC3, AC4: Reviewer against this plan; the settings steps of *Approach* are dry-run by the Product Manager
  before the review round (below).

Runtime details only a release can show (*Criteria verified only in CI*), named now; each fails closed:

- R1: the jobs `image-publish` and `promote` of the first release (`v0.1.0`) run in the environment and pass the
  credentials check (facts 6, 7). If the rule refuses the ref, the job fails before its first step, nothing is
  pushed, `promote` is skipped and the release stays a pre-release.
- R2: no permission is missing (fact 8). If one is, the job fails before the push; same outcome as R1.
- R3: a missing environment secret makes the credentials check fail with the AC2 text before any login.

Dry run (*Operator environments and documented procedures*): the orchestrator asks the Product Manager to do
setup steps 1-3 of *Approach* before the review round and to confirm the labels of the GitHub settings pages
the document uses, and fact 11; statements no one has shown stay marked as not yet tried in the document.

## Approach

Workflow: in `release.yml` add `environment: release` after `runs-on: ubuntu-latest` of `image-publish` and of
`promote` (four-space indent, job level). Extend the header comment by one sentence: the Docker Hub secrets are
secrets of the environment `release`, which admits only `v*` tags (record 0012). No step changes; the credentials
check stays as the fail-closed guard for a missing environment secret.

Script: `release-check.sh:132` gets the AC2 text; the usage line 9 stays.

Maintainer setup, in this order (the documented list of AC3; GitHub UI, *Settings*):

1. *Environments*, *New environment*, name `release`. Before the PR is merged, so no run creates it unprotected
   (fact 4); if a run already created it, edit that one.
2. *Deployment branches and tags*: *Selected branches and tags*, add a rule of type *Tag* with the pattern `v*`;
   no branch rule, no required reviewers.
3. *Environment secrets*: add `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`. GitHub does not show a stored secret;
   if the token is not at hand, create a new Docker Hub access token (read and write) and revoke the old one after
   step 5.
4. Merge the PR.
5. *Secrets and variables*, *Actions*, *Repository secrets*: delete `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`
   (also from *Dependabot* secrets, if present).
6. Publish `v0.1.0` as documented and check that both jobs show the environment `release`.

Why this order cannot break the first release: no release exists, so any run after the merge finds the secrets in
the environment (steps 1-3 done) or, if the merge came first, still the repository secrets (an auto-created,
unprotected environment does not block). Deleting the repository secrets before step 3 only makes a release in
between fail closed at the credentials check. Steps 1-3 before step 4, step 5 after step 3.

The Dev's result passes these six steps, unchanged, to the orchestrator for the PR description and the user.

Layout: no new user-visible output beyond the AC2 text; the CONTRIBUTING list is a numbered list after the
paragraph on the secrets, layout free for the Dev, approved in step 9.

## Options considered

Weighed in record 0012 (options 5-9): repository secrets (status quo); the environment with a `v*` tag rule
(chosen); the same with required reviewers (one administrator who also releases; the tag ruleset already decides
who starts a release); a branch rule `main` (refuses every release run, fact 7); new secret names that exist only
in the environment (old repository secrets would still need deleting, and with them deleted a missing environment
fails closed anyway). The environment on `companion-publish` was not weighed: its write token is no secret an
environment holds.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/workflows/release.yml` | `environment: release` on two jobs, header comment (Dev) |
| CI | `.github/scripts/release-check.sh` | message at line 132 (Dev) |
| CI test | `.github/scripts/release-check-test.sh` | AC7 expectation at line 212 (Tester) |

## Signatures (for the Dev's skeleton)

None.

## Test files

`.github/scripts/release-check-test.sh` (existing). Existing test call sites of a changed signature: none.
Files no unit test can cover: `.github/workflows/release.yml` — a workflow only a release event runs (R1-R3).

## Areas

None: the release process is not an area (`docs/areas/README.md`); what a release produces does not change.

## Documentation updates

Sweep: `grep -rni "repository secret\|dockerhub\|secret"` over `docs/`, `README.md`, `SECURITY.md`, `AGENTS.md`,
`.squad/project.md`, `.github/` (fact 13). All owned by the Dev, written once the script is green:

- `docs/CONTRIBUTING.md`, *Versioning and releases*: line 188 "a maintainer (write access to the repository)" →
  an administrator (fact 11); lines 199-200 the repository-secrets sentence → environment `release` (AC3), plus the
  setup list of *Approach* marked not yet tried. Line 74 ("missing Docker Hub secrets") and lines 109-117 (about
  `SONAR_TOKEN` and Dependabot) stay.
- `.squad/project.md` line 20: the Docker Hub credentials as secrets of the environment `release` (tags `v*` only),
  the `contents: write` token unchanged (AC4).
- `docs/ARCHITECTURE.md` *Deployment*, lines 67-70: one clause that the Docker Hub credentials are secrets of the
  environment `release`, which only runs of `v*` tags reach (AC4).

## Architecture check

`.squad/project.md` records no guarantee. The release flow of record 0012 (secret-free builds, promote by digest)
is unchanged; the credentialed jobs only get narrower. `docs/ARCHITECTURE.md` gets the clause above, linked to 0012.

## Security considerations

- [x] No limits: no parsed input changes.
- [x] The one message reaching a user is the AC2 text in the job log; it names no secret value.
- Residual, recorded in 0012: the workflow cannot check the environment (fact 12), so the protection holds only
  after setup steps 1-3 and 5; an administrator's `v*` tag on a branch commit would admit that commit's workflow.

## Decision records

- `docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md` (Accepted, unreleased) — extended in
  place: options 5-9, one decision sentence, two consequences; source adds issue #74. Index row unchanged.

## Out of scope / follow-ups

- Related finding, settings only: ruleset `main-Protection` lists `RepositoryRole` 2, 4 and 5 as bypass actors
  with `always`; if 4 is the write role, a writer can push to `main` without a pull request and change
  `release.yml` before an administrator's next release. Proposed follow-up issue for the Product Manager (below in
  the result), not part of this PR.
