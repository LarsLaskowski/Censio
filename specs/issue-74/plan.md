# Plan: Release secrets in a GitHub environment limited to v* tags

Source: Issue #74
Status: Revised (1)
Tier: security — moves the release's write credentials (Docker Hub, website dispatch) behind a new control, an environment whose deployment rule admits only `v*` tags: the trust of the release (CI secrets).
Security areas touched: Release publishing (`.github/workflows/release.yml`, `.github/scripts/release-check.sh`, record 0012); CI configuration (`release.yml`, `.github/workflows/notify-website.yml`, the CI test `.github/scripts/release-check-test.sh`); repository settings the maintainer changes (environment `release`, repository secrets)

## Problem / root cause

The release-triggered workflows read write credentials as repository secrets: `release.yml` reads
`DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` in `image-publish` (`:168-184`) and `promote` (`:217-227`), and
`notify-website.yml:18` reads `WEBSITE_DISPATCH_TOKEN` in `dispatch` (its header, lines 2-3, says "repository
secret"). No job names an `environment`. A repository secret is readable by any job of any workflow on any ref, so
anyone who can push a branch can add a workflow that prints it. The issue's claim is confirmed, and the same
defect holds for the website token. The fix has two halves: the three jobs reference an environment `release`
(this PR), and the maintainer creates it with a tag rule `v*`, puts the three secrets into it and deletes the
repository secrets (settings no agent can change). The workflow half alone protects nothing (facts 4, 5).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The write credentials are read only by `image-publish`, `promote` (`release.yml`) and `dispatch` (`notify-website.yml`); no job of any workflow has an `environment` | `release.yml:168-184`, `:217-227`; `notify-website.yml:18`; `grep -rn "environment:" .github/workflows/` is empty | confirmed |
| 2 | The missing-secret message tells the maintainer to set *repository* secrets, and the CI test pins that text | `release-check.sh:132`; `release-check-test.sh:212` | confirmed |
| 3 | No release exists yet: nothing is released, record 0012 is edited in place | `git ls-remote --tags origin` is empty | confirmed |
| 4 | A run that references an environment that does not exist creates it, with no protection rule | to verify: the Product Manager reads GitHub's *Managing environments for deployment* in the dry run (D4); not reachable from the session | unverified |
| 5 | Repository secrets stay readable by every workflow while they exist; in a job with an environment, an environment secret of the same name wins | to verify: probe job `plain` in the dry run (D3) shows whether a repository secret is still readable; precedence from GitHub's *Using secrets in GitHub Actions* | unverified |
| 6 | A job on a ref the environment's rule refuses fails before its first step and gets no environment secret | to verify: probe job `env` in the dry run (D2), negative case; positive case in the first release (R1) | unverified |
| 7 | A `release` event runs on `refs/tags/<tag>`, so a tag rule `v*` admits it and a branch rule `main` refuses it | to verify: the first release run (R1); `GITHUB_REF` of `release` per GitHub's *Events that trigger workflows* | unverified |
| 8 | A job needs no `deployments` permission of the `GITHUB_TOKEN` to run in an environment | to verify: the first release run (R2); GitHub creates the deployment itself | unverified |
| 9 | The repository is public, so environments with deployment rules are available on the free plan | `gh api repos/larslaskowski/censio --jq .visibility` → `public` | confirmed |
| 10 | Ruleset `tag-Protection` (active) restricts creation, update and deletion of `refs/tags/v*`; its only bypass actor is `RepositoryRole` 5. Ruleset `main-Protection` requires a pull request on `main` with `RepositoryRole` 2, 4 and 5 as bypass actors, mode `always` | `gh api repos/larslaskowski/censio/rulesets/26232655` and `/26232639` | confirmed |
| 11 | `RepositoryRole` 5 is admin, 4 write, 2 maintain: only administrators create `v*` tags, and writers and maintainers can push to `main` without a pull request (issue #77) | to verify: the Product Manager reads both bypass lists under *Settings, Rules* in the dry run (D1); IDs per GitHub's REST docs on ruleset bypass actors | unverified |
| 12 | The workflow cannot read the environment's configuration (that needs an administration token the run does not have), so no step can check that the rule exists | the jobs hold `contents` permissions or none | confirmed |
| 13 | Besides the two workflows, the scripts and `docs/CONTRIBUTING.md`, no file names the three secrets. The first sweep (`dockerhub\|repository secret` without `.github/workflows/`) missed `notify-website.yml`; refuted and redone | `grep -rni "dockerhub\|WEBSITE_DISPATCH\|repository secret"` over `.github/`, `docs/`, `AGENTS.md`, `.squad/project.md`, `SECURITY.md`, `README.md` | confirmed |
| 14 | In both the environment's rule and the ruleset's `refs/tags/v*`, `*` does not match `/`, so they admit the same tags | to verify: the Product Manager reads GitHub's *Deployment branches and tags* and the ruleset pattern syntax in the dry run (D4) | unverified |
| 15 | Re-running a workflow and publishing a release on an existing tag need only the write role; such a run on a `v*` tag passes the environment rule | to verify: Security against GitHub's docs on re-running workflows and on releases; no dry run (it would publish) | unverified |

## Acceptance criteria

- [ ] AC1: The jobs `image-publish` and `promote` of `.github/workflows/release.yml` and `dispatch` of
  `.github/workflows/notify-website.yml`, and no other job, declare `environment: release`; apart from that only the
  header comments of both files change.
- [ ] AC2: `release-check.sh dockerhub-secrets` with either value not `true` fails with exactly
  `Docker Hub credentials are missing: set DOCKERHUB_USERNAME and DOCKERHUB_TOKEN as secrets of the environment release.`;
  with both `true` it passes silently (unchanged).
- [ ] AC3: `docs/CONTRIBUTING.md`, *Versioning and releases*, says: the three secrets are secrets of the
  environment `release` and must not exist as repository secrets; the one-time setup in the order of *Approach*;
  who can do what (only administrators create, move or delete `v*` tags; anyone with write access can publish a
  release on an existing `v*` tag or re-run a release run, which then gets the environment secrets; writers and
  maintainers can push to `main` without a pull request while issue #77 is open); and which setup statements the
  dry run has not shown, marked as not yet tried until it or the release `v0.1.0` has.
- [ ] AC4: `.squad/project.md` (*Release publishing*) and `docs/ARCHITECTURE.md` (*Deployment*) name the
  environment `release` as where the release's write credentials live.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC2 | step `AC7: dockerhub-secrets`: the three `expect_error` cases compare the new text; `AC7 both set` unchanged | `.github/scripts/release-check-test.sh` |

The changed expectation fails on the current `release-check.sh` (step 5). The script needs Docker, `dotnet` and
`zip` for its later steps; where the session lacks one, the Tester runs it as far as it gets (AC7 comes before
the Companion and image steps), and the CI job `release-check` runs it in full on the draft PR.

## Verification without tests

Step 4 (no API) and the *Coverage gate* are not applicable: no C# changes, shell scripts are outside the coverage
measure. Step 5 is the one changed expectation above.

- AC1: Reviewer, read-only: `grep -n "environment:" .github/workflows/*.yml` shows exactly three lines, in those
  jobs; `actionlint` through the *Analyzer gate* (orchestrator, step 7).
- AC3, AC4: Reviewer against this plan, after the dry run below has been logged.

**Settings dry run** by the Product Manager after setup steps 1-3 and before the review round (the orchestrator
asks and logs each answer; the Dev writes the results into CONTRIBUTING, anything not shown as not yet tried):

- D1: the bypass lists of `tag-Protection` and `main-Protection` under *Settings, Rules*, by role name (fact 11).
- D2: probe, negative case. On a throwaway branch `probe/env-74` push `.github/workflows/probe-74.yml`:
  `on: push` for that branch, `permissions: {}`, job `env` with `environment: release` whose one step runs
  `echo "set: $SET"` with `env: SET: ${{ secrets.DOCKERHUB_TOKEN != '' }}` (a boolean, never a value). Expected:
  the job is refused ("not allowed to deploy to release"), no step runs. Fact 6.
- D3: the same file has a job `plain` without environment printing the same boolean for each of the three secrets.
  Before step 5 it shows `true` for a secret still at repository level (fact 5); the PM re-runs it after step 5
  and expects `false` for all three. Then deletes the branch and its runs.
- D4: reading facts 4 and 14 on GitHub's documentation pages, and the labels of the settings pages the document uses,
  including whether *Allow administrators to bypass configured protection rules* appears for the environment and
  its state after step 2.

Runtime details only a release can show (*Criteria verified only in CI*), each failing closed:

- R1: `image-publish`, `promote` and `dispatch` of the first release (`v0.1.0`) run in the environment; the first
  two pass the credentials check (facts 6, 7). A refused ref fails the job before its first step: nothing is pushed,
  `promote` is skipped, the release stays a pre-release, the website is not notified.
- R2: no permission is missing (fact 8); otherwise the job fails before the push, as R1.
- R3: a missing Docker Hub secret fails the credentials check with the AC2 text before any login.

## Approach

Workflows: add `environment: release` after `runs-on: ubuntu-latest` of `image-publish` and `promote` in
`release.yml` and of `dispatch` in `notify-website.yml` (job level). `release.yml`'s header comment gets one
sentence (the Docker Hub secrets are secrets of the environment `release`, which admits only `v*` tags, record
0012); `notify-website.yml`'s lines 2-3 say "environment secret ... of the environment `release`" instead of
"repository secret". No step changes; the credentials check stays the fail-closed guard of a missing secret.

Script: `release-check.sh:132` gets the AC2 text; usage line 9 stays.

Maintainer setup, in this order (GitHub, *Settings*; the documented list of AC3):

1. *Environments*, *New environment* `release` — before the merge, so no run creates it unprotected (fact 4); if
   one already did, edit that one.
2. *Deployment branches and tags*: *Selected branches and tags*, one rule of type **Tag**, pattern `v*`; no branch
   rule, no required reviewers. Tag, not branch: a branch rule `v*` would admit any branch named `v…`, which every
   writer can push. Clear *Allow administrators to bypass configured protection rules* if the page shows it (D4).
3. *Environment secrets*: add `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` and `WEBSITE_DISPATCH_TOKEN`. A stored
   secret cannot be read back; a token not at hand is created anew (Docker Hub access token, read and write;
   fine-grained GitHub token as in `notify-website.yml`) and the old one revoked after step 5.
4. Merge the PR.
5. *Secrets and variables*, *Actions*, *Repository secrets*: delete the three secrets (and any *Dependabot*
   secrets of those names).
6. Publish `v0.1.0` as documented; check that the three jobs show the environment `release`.

Why the order cannot break the first release: none exists. A run after the merge finds the secrets in the
environment (steps 1-3 done) or, if the merge came first, still at repository level (an auto-created environment
does not block). Deleting the repository secrets before step 3 only makes a release in between fail closed. Steps
1-3 before 4, step 5 after 3.

The Dev's result passes these steps unchanged to the orchestrator for the PR description and the user.

Layout: no new user-visible output beyond the AC2 text; the CONTRIBUTING list is a numbered list after the
paragraph on the secrets, layout free for the Dev, approved in step 9.

## Options considered

In record 0012 (options 5-9): repository secrets (status quo); the environment with a `v*` tag rule (chosen); the
same with required reviewers (one administrator, who also releases); a branch rule `main` (refuses every release
run, fact 7); new secret names only in the environment (old repository secrets still need deleting; with them
deleted a missing environment fails closed anyway). Leaving `WEBSITE_DISPATCH_TOKEN` to a follow-up lost: same
defect, same fix, no side effect (Challenge, MAJOR 1). `companion-publish` gets no environment: its write token is
no secret an environment holds.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/workflows/release.yml` | `environment: release` on two jobs, header comment (Dev) |
| CI | `.github/workflows/notify-website.yml` | `environment: release` on `dispatch`, header comment (Dev) |
| CI | `.github/scripts/release-check.sh` | message at line 132 (Dev) |
| CI test | `.github/scripts/release-check-test.sh` | AC7 expectation at line 212 (Tester) |

## Signatures (for the Dev's skeleton)

None.

## Test files

`.github/scripts/release-check-test.sh` (existing). Existing test call sites of a changed signature: none.
Files no unit test can cover: `release.yml` and `notify-website.yml` — workflows only a release event runs (R1-R3).

## Areas

None: the release process is not an area (`docs/areas/README.md`); what a release produces does not change.

## Documentation updates

Sweep as fact 13. All owned by the Dev, written once the script is green:

- `docs/CONTRIBUTING.md`, *Versioning and releases*: line 188 "a maintainer (write access to the repository)" →
  an administrator creates the tag (fact 11); lines 199-200, the repository-secrets sentence → the environment
  `release` with all three secrets; the setup list; the who-can-what sentence of AC3 with a link to issue #77; the
  dry-run results (D1-D4), not-shown statements marked not yet tried. Line 74 and lines 109-117 (`SONAR_TOKEN`,
  Dependabot) stay.
- `.squad/project.md` line 20: the Docker Hub credentials and `WEBSITE_DISPATCH_TOKEN` as secrets of the environment
  `release` (tags `v*` only); the `contents: write` token unchanged (AC4).
- `docs/ARCHITECTURE.md` *Deployment*, lines 67-70: one clause that the release's write credentials are secrets of
  the environment `release`, which only runs on `v*` tags reach (AC4).

## Architecture check

`.squad/project.md` records no guarantee. The release flow of record 0012 is unchanged; the credentialed jobs only
get narrower. `docs/ARCHITECTURE.md` gets the clause above, linked to 0012.

## Security considerations

- [x] No limits: no parsed input changes.
- [x] The one message reaching a user is the AC2 text in the job log; it names no secret value.
- Residual, recorded in 0012: the workflow cannot check the environment (fact 12), so the protection holds only
  after setup steps 1-3 and 5. The environment admits the workflow of whatever commit a `v*` tag points to, and
  `main` is not closed to writers: with role 4 (and 2) bypassing `main-Protection`, a writer can push a changed
  workflow to `main` without a pull request, and the administrator's next release runs it with the secrets (issue
  #77, settings only). Writers can also re-run a release run or publish a release on an existing `v*` tag, which
  runs the reviewed workflow of that tag with the secrets (no disclosure; may move `latest` back — follow-up below).

## Decision records

- `docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md` (Accepted, unreleased) — extended in
  place: options 5-9, one decision sentence, consequences on what the environment does not stop (with #77); source
  adds issue #74. Index row unchanged.

## Challenge

- MAJOR 1 (scope: `WEBSITE_DISPATCH_TOKEN`) — accepted. `dispatch` gets `environment: release`, the token moves
  with the others (setup steps 3 and 5), AC1 covers three jobs, `notify-website.yml`'s header changes, fact 13
  corrected (refuted, redone), record 0012 names it. A Lead decision: the issue asks for the release's secrets
  out of branch reach, and this is one of them; no product behavior changes.
- MINOR 1 (ruleset overstated) — accepted. Record 0012 option 7 and AC3 now say that the ruleset limits only who
  creates, moves or deletes `v*` tags; anyone with write access can publish a release on an existing tag or re-run
  a release run (fact 15).
- MINOR 2 (administrator bypass setting) — accepted: setup step 2 clears it if shown; its presence and state are
  part of D4 and written down, else marked not yet tried.

## Security review

- B1 (negative case never measured) — accepted. Dry run D2 (refusal of a branch job naming the environment, no step
  runs, boolean only) and D3 (repository secrets gone after step 5); CONTRIBUTING marks the protection not yet
  tried until D2 is logged. Fact 4 is confirmed by reading the documentation (D4), not by a probe: creating a
  second environment to watch it appear unprotected would leave one more environment to delete, and steps 1-3
  before the merge make the order independent of it.
- B2 (`main-Protection` writer bypass) — accepted. Record 0012's consequence no longer says the remaining path needs
  the administrator role: it names the writer's push to `main` and links issue #77; the same in *Security
  considerations* and in CONTRIBUTING (AC3). The settings change itself stays in #77 (it changes how every
  maintainer works on `main`, a Product Manager decision there); no setup step here. Fact 11 is confirmed in D1.
- N1 — taken: setup step 2 and record 0012 say why the rule is of type Tag.
- N2 — taken: fact 14, confirmed in D4 by reading both pattern syntaxes.
- N3 — agreed as a follow-up issue (below); not in this change.

## Out of scope / follow-ups

- Issue #77 (exists): remove roles 2 and 4 from `main-Protection`'s bypass list.
- Follow-up issue for the orchestrator to open — title "[CI] Writers can republish or re-run an old release and
  move latest back"; body: "Only administrators can create, move or delete `v*` tags (ruleset `tag-Protection`),
  but anyone with write access can delete the release of an existing `v*` tag and publish it again, or re-run an
  older release run. Either runs the reviewed `Release` workflow of that tag with the secrets of the environment
  `release` and moves Docker Hub `latest` and GitHub's *Latest* back to the older version (record 0012). No secret
  is disclosed. Decide whether to refuse a release older than the current latest in `release-check.sh`, restrict
  who may publish releases, or accept it as documented (found while planning #74)."
