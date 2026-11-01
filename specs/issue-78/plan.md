# Plan: Refuse a release whose tag is not the highest version

Source: Issue #78
Status: Draft
Tier: security — the change adds a guard to the trust of the release (what a writer can make a credentialed release run do with Docker Hub `latest` and GitHub's *Latest*), a security control in `.github/workflows/release.yml`.
Security areas touched: Release publishing (`.github/workflows/release.yml`, jobs `check` and `promote`); CI scripts `.github/scripts/release-check.sh` and `.github/scripts/release-check-test.sh` (run by CI job `release-check`); `.squad/project.md` (*Security areas*, Release publishing entry, wording extended, not narrowed)

## Problem / root cause

Nothing in the run compares the release's tag with the other tags. The `check` job (`.github/workflows/release.yml`
lines 14-45) checks only the tag's form (`release-check.sh tag`), that its commit is on `main` (`on-main`) and the
pre-release flag; `promote` (lines 199-244) moves `latest` to the run's digest (`promote-image`, line 233, script
lines 312-325) and marks the release *Latest* (`gh release edit "$TAG" --prerelease=false --latest`, line 244)
unconditionally. A writer who republishes the release of an older `v*` tag, re-runs an older run, or re-runs only
the older run's `promote` job (whose `check` job does not run again) therefore moves both back.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Only administrators create, move or delete `v*` tags (ruleset `tag-Protection`) | to verify: the bypass actors on the ruleset page, administrator, settings dry run D1 of `docs/CONTRIBUTING.md` *Not yet tried* (already listed); `gh api repos/LarsLaskowski/Censio/rulesets` lists `tag-Protection` (target tag, active), its bypass list is not readable here | unverified |
| 2 | A writer can publish a release on an existing tag and re-run a run, which runs that tag's workflow with the `release` secrets | to verify: at the first release with a second account, administrator, after merge (already listed in `docs/CONTRIBUTING.md` lines 271-272); record 0012 option 7 and Consequences, `docs/CONTRIBUTING.md` lines 252-254 | unverified |
| 3 | Such a run of an older tag moves `latest` and *Latest* back | `release.yml` line 233 (`promote-image` to the run's digest), line 244 (`--latest`); `release-check.sh` lines 312-325 | confirmed |
| 4 | No secret is disclosed by such a run | The run executes the reviewed workflow of the tag's commit; no step prints a secret (`release.yml` lines 172-196, 219-237) | confirmed |
| 5 | No `v*` tag and no release exists yet, so a check merged now is in the workflow of every tag ever released | `git ls-remote --tags origin`: empty; `gh api repos/LarsLaskowski/Censio/releases`: empty | confirmed |
| 6 | `actions/checkout` with `fetch-depth: 0` fetches every tag (`+refs/tags/*:refs/tags/*`), also when `ref` is a SHA | Source of the pinned v7.0.1 (`3d3c42e5`): `src/ref-helper.ts` line 70 (`getRefSpecForAllHistory`), `src/git-source-provider.ts` lines 188-194 | confirmed |
| 7 | The `check` job already relies on the fetched tags | `release-check.sh` line 101 (`git rev-parse "refs/tags/${tag}^{commit}"`) after the checkout with `fetch-depth: 0`, `release.yml` lines 21-26 | confirmed |
| 8 | `promote` checks out with the default depth 1 and so has no tags today | `release.yml` lines 209-213 | confirmed |
| 9 | "Re-run failed jobs" or a re-run of one job does not re-run a succeeded `check` job, and a re-run checkout fetches the repository anew | to verify: observed at the first re-run of a release run, administrator (not measurable before a release); GitHub's documented re-run behavior re-runs the selected or failed jobs and their dependents; the design does not rest on it, since `promote` checks itself | unverified |
| 10 | The tag itself reaches the script only through `env` | `release.yml` lines 30-31, 37-38 | confirmed |
| 11 | `notify-website.yml` dispatches on every published release, also a republished old one | `.github/workflows/notify-website.yml` lines 6-8, 22; left out of scope, see last section | confirmed |

## Acceptance criteria

Every case runs `bash .github/scripts/release-check.sh newest <tag>` in a scratch Git repository; "refused" means
exit code 1 with the given text as the `::error::` annotation on the last line, "passes" exit code 0 and no output.

- [ ] AC1: passes when `<tag>` is a tag of that repository and no tag matching `^v` plus the script's
  `version_pattern` has a higher version. Versions compare numerically field by field (MAJOR, then MINOR, then
  PATCH). Tags not matching the pattern (`v2.0.0-rc.1`, `v3.0`, `V9.0.0`, `v09.0.0`, `x9.0.0`, `v1.0.10.1`) and
  branches (a branch `v99.0.0`) are ignored.
- [ ] AC2: refused with `Release tag '<tag>' is older than tag '<highest>'; only the highest version tag can be
  released.` when a matching tag of a higher version exists; `<highest>` is the highest matching tag, whether it is
  annotated or lightweight, on `main` or not.
- [ ] AC3: a `<tag>` not of the form `vMAJOR.MINOR.PATCH` is refused with the existing text of `tag`:
  `Release tag '<tag>' is not of the form vMAJOR.MINOR.PATCH.` (line breaks and `%` encoded as there).
- [ ] AC4: a well-formed `<tag>` that is not a tag of the repository (also in a repository without any tag) is
  refused with `Release tag '<tag>' is not among the fetched tags.` — this is what a checkout without tags yields,
  so a missing fetch fails closed.
- [ ] AC5: `release.yml` runs `newest "$TAG"` (tag from `github.event.release.tag_name` through `env`) in `check`
  after the on-main step, and in `promote` after *Check the passed values* and before *Log in to Docker Hub*; the
  `promote` checkout has `fetch-depth: 0`.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | step "Issue 78: newest": cases `I78-AC1 only tag` (repo with `v1.0.0` only), `I78-AC1 numeric` (`v0.9.0`, `v0.10.0`, `v1.0.9`, `v1.0.10`: `v1.0.10` passes), `I78-AC1 ignored` (the non-matching tags of AC1 and a branch `v99.0.0` added: `v1.0.10` still passes) | `.github/scripts/release-check-test.sh` |
| AC2 | `I78-AC2 lower patch` (`v1.0.9` names `v1.0.10`), `I78-AC2 names the highest` (`v0.9.0` names `v1.0.10`, not `v0.10.0`), `I78-AC2 not on main, annotated` (annotated `v10.0.0` on a side branch beside `v9.9.9`: `v1.0.10` and `v9.9.9` name `v10.0.0`), `I78-AC2 ignored passes` (after the AC2 cases the highest tag still passes) | `.github/scripts/release-check-test.sh` |
| AC3 | `I78-AC3 '<bad>'` for `v1.2`, `v1.2.3;id`, empty, `v1.2.3` plus LF (expected `v1.2.3%0A`) — the expected text built with the existing `tag_error` helper | `.github/scripts/release-check-test.sh` |
| AC4 | `I78-AC4 absent` (`v1.0.11` in the repo above), `I78-AC4 no tags` (fresh repo with one commit, `v1.0.0`) | `.github/scripts/release-check-test.sh` |
| AC5 | no test: workflow wiring, which runs only at a real release. Verified by *Analyzer gate* `--lint-only` (actionlint), the Reviewer and Security's diff review reading the two steps and the checkout | — |

The new step goes after "AC7: dockerhub-secrets" and before the Companion step, so it needs only `git` and runs
before any build; its repository is a new one under `${work}` (the AC5 repository of feature 5 stays unchanged), with
the same `git config` lines. The script header gains "and the criteria AC1 to AC4 of issue #78" (Tester).

## Approach

New command `newest <tag>` in `release-check.sh`, function `check_newest`, dispatched in the final `case`, listed in
the usage header (`newest <tag>  the tag must be the highest vMAJOR.MINOR.PATCH tag of the repository`). Order:

1. Validate the form exactly as `check_tag` does (reuse it; its error and exit stand).
2. `git rev-parse -q --verify "refs/tags/${tag}"` fails → AC4 error.
3. List `git tag --list`, keep the names matching `^v` + `version_pattern`, find the highest by numeric comparison of
   the three fields (bash arithmetic on `BASH_REMATCH`, no `sort -V`, no locale dependence); if it is higher than the
   tag's version → AC2 error naming it. Equal is only the tag itself.

Messages pass through `error` (annotation encoding). No output on success, so the workflow step needs no capture.

`release.yml`: in `check`, after *Check that the tag is on main*:

```yaml
      - name: Check that the tag is the highest version
        env:
          TAG: ${{ github.event.release.tag_name }}
        run: bash .github/scripts/release-check.sh newest "$TAG"
```

In `promote`, the checkout gains `fetch-depth: 0  # compare the tag with every v* tag right before latest moves`, and
after *Check the passed values* a step *Check that the tag is still the highest version* with the same `env` and
`run`. `image-publish` gets no check: a re-run of it alone re-pushes `<version>` from the run's own checked archive
and leaves `latest` to `promote`. The workflow header comment gains one sentence: a tag that is not the highest
`v*` version is refused in `check` and again in `promote` (record 0012).

No new user-visible output beyond the two annotation texts fixed in AC2 and AC4.

## Options considered

Recorded in record 0012, options 10 to 15: the tag comparison in `check` and `promote` (chosen); comparing against
GitHub's *Latest* release (writer-controlled reference); skipping only the promotion (still re-pushes an old
release's artifacts); required reviewers on the environment with administrator self-approval (an untestable
setting and an approval on every release; stays available); a `triggering_actor` permission check (unmeasured token
reach); accepting it as documented. Checking only in `check` lost to checking in both: a re-run of `promote` alone
skips `check`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/scripts/release-check.sh` | new command `newest`, function `check_newest`, usage line |
| CI | `.github/workflows/release.yml` | step in `check`; `fetch-depth: 0` and step in `promote`; header comment |
| CI test | `.github/scripts/release-check-test.sh` | step "Issue 78: newest", header line |

## Signatures (for the Dev's skeleton)

- `release-check.sh newest <tag>` — exit 0 without output, or 1 with one `::error::` line on stderr; usage error
  (exit 2) never: a missing argument is the empty tag of AC3.
- Bash function `check_newest()` reading `local tag="${1-}"` (shelldre:S7679: positional parameter assigned first).

The skeleton adds the dispatch line `newest) check_newest "$@" ;;` and a `check_newest` that fails, so the Tester's
cases run red. No existing file is final; all three are edited.

## Test files

`.github/scripts/release-check-test.sh` only (the CI job `release-check` runs it; no C# test project is involved).

Existing test code that calls a changed signature: none (no existing command changes).

Files of the change no unit test can cover: `.github/workflows/release.yml` — runs only on a published release, its
wiring is checked by actionlint and review (AC5). Not a coverage-measured file.

The full test script needs Docker, the .NET SDK and a registry image; the Tester runs the new step and those before
it locally (they need only `git`), and the whole script runs in CI job `release-check`: the orchestrator opens the
PR as a draft after step 7 (*Draft until green*).

## Areas

- `docs/areas/server.md` (*Docker image*, lines 98-101): `latest` points to the promoted release of the highest
  version, not "the last promoted release"; a release whose tag is not the highest `vMAJOR.MINOR.PATCH` tag is
  refused and leaves `latest` where it was.
- `docs/areas/companion.md`: none — "Every release attaches the ZIP" stays true of every release that completes.

## Documentation updates

Sweep: `docs/ARCHITECTURE.md`, `docs/areas/*.md`, `docs/CONTRIBUTING.md`, `README.md`, `SECURITY.md`,
`.squad/project.md` searched for "release" and "latest"; hits needing a change below. `README.md` lines 42 and 175
("moves the tag `latest` to the newest release") already state the new behavior; `SECURITY.md` is unaffected.

- `docs/CONTRIBUTING.md`, *Versioning and releases* (Dev):
  - lines 216-218: the list of refusals gains "a tag lower than the highest `vMAJOR.MINOR.PATCH` tag (checked at the
    start and again right before the promotion)";
  - *Who can do what*, lines 252-254: a writer's republish or re-run of any tag but the highest stops before it
    pushes or promotes anything; of the highest it rebuilds and re-promotes the same version; still no secret
    disclosed; drop the "may move `latest` back" link wording, keep the issue link as the source;
  - *Limits and rules*, lines 279-282: replace "Never re-run the run of an older release … does the same" by: the
    run of a lower version is refused once a higher tag exists; to release a lower version, an administrator deletes
    the higher tags first; a failed release cannot be completed after a higher tag was created.
- `docs/areas/server.md`: as in *Areas* (Dev).
- `docs/ARCHITECTURE.md` lines 67-70: "promotes the release, moving `latest` and *Latest* to it" gains "when its tag
  is the highest version" (Dev; record 0012 is already linked there).
- `.squad/project.md`, *Security areas*, Release publishing: append "; a run promotes only a tag that is the highest
  `v*` version, checked in `check` and `promote` (record 0012)" (Dev).
- Test documentation: none beyond the test script's header (Tester).

## Architecture check

`docs/ARCHITECTURE.md` lines 67-70 (pre-release first, secret-free builds, promotion by digest, environment
`release`) are kept: no job gains a secret or a permission, `promote` gains only a full-history checkout of the
tagged commit and one script call before its login, executing nothing new from the checkout beyond the script it
already runs. `.squad/project.md` lists no guarantee. The change narrows what a release run does; it weakens nothing.

## Security considerations

- [ ] Byte or length limits: no limits; the tag is matched whole by the existing anchored pattern before any Git call.
- [ ] Exceptions reaching a user-visible message: none; the only texts are the annotations of AC2, AC3 and AC4. A
  failing `git` (no repository) exits non-zero with Git's own stderr, as `on-main` does today.
- [ ] The comparison reference is the tag set, which only administrators change (fact 1, unverified as D1); a writer
  cannot add a higher tag to block a release, nor remove one to unblock an old release.
- [ ] Fails closed: no tags fetched → AC4; a malformed tag → AC3; any refusal leaves the release a pre-release and
  `latest` unchanged, as every other refusal of the run does.
- [ ] Residual: a writer can still republish or re-run the highest release (same version, same reviewed commit,
  rebuilt); a tag created between the check in `promote` and the move of `latest` is not noticed; issue #77 is
  unaffected. All recorded in record 0012, *Consequences*.

## Decision records

- `docs/decisions/0012-release-pre-release-first-and-secret-free-builds.md` (Accepted, unreleased: no `v*` tag on
  origin) — extended in place: source issue #78, options 10-15, one decision sentence, the concurrency consequence
  rewritten (the older release's `promote` now stops), a new consequence (no lower-version release while a higher
  tag exists), the writer re-run consequence rewritten. Index row unchanged.

## Out of scope / follow-ups

- `notify-website.yml` dispatches for a republished old release as well (fact 11); what the website does with the
  tag is decided in `LarsLaskowski/e-networld`, outside this repository, and it moves neither `latest` nor *Latest*.
  No follow-up issue.
- Issue #77 (a writer's push to `main` bypassing pull requests) stays separate.
