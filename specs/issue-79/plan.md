# Plan: Fail the website notification with a clear message when WEBSITE_DISPATCH_TOKEN is missing

Source: Issue #79
Status: Draft
Tier: security — the change adds a second reference to the release secret `WEBSITE_DISPATCH_TOKEN` inside the credentialed job `dispatch` (handling of a secret in the release trust path), and Security's diff review must confirm that only a boolean of it reaches the new step.
Security areas touched: CI workflow `.github/workflows/notify-website.yml` (security area *Release publishing*: `WEBSITE_DISPATCH_TOKEN` is a secret of the environment `release`, record 0012)

## Problem / root cause

`.github/workflows/notify-website.yml:16-19`: the only step of job `dispatch` passes
`${{ secrets.WEBSITE_DISPATCH_TOKEN }}` to `peter-evans/repository-dispatch` without checking it first. When the
environment `release` exists but holds no `WEBSITE_DISPATCH_TOKEN`, the expression is the empty string and the
action fails with a message that does not name the secret (fact 3). `release.yml` already guards its Docker Hub
secrets with a presence check (`release.yml:170-174` and `220-224`, `release-check.sh` `check_dockerhub_secrets`);
the dispatch job has no such check.

Claims of the issue: the secret is an environment secret since #74 (confirmed, fact 1); an empty token reaches
the action (confirmed, fact 2); "unclear authentication error" (confirmed as unclear, the exact text depends on
runner behavior, fact 3); `check_dockerhub_secrets` matches the pattern and could be reused (confirmed it matches;
reuse rejected under *Options considered*). Related defect found on the way: none.

Other squad run: PR #84 (issue #78, unmerged) edits `release-check.sh`, `release-check-test.sh`, `release.yml`,
record 0012 and `docs/CONTRIBUTING.md`. This plan touches none of the first four; in `docs/CONTRIBUTING.md` it
changes only the paragraph "The workflow cannot read the environment's settings …" (lines 245-247 on
`origin/main`), which #84's hunks (around lines 216-218 and from line 252 on) do not touch or border.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `WEBSITE_DISPATCH_TOKEN` is a secret of the environment `release`, job `dispatch` names that environment | `notify-website.yml:2-3,15`; record 0012 *Decision* (option 6); `docs/CONTRIBUTING.md:222-225` | confirmed |
| 2 | Today the token is passed to the action without any prior check; the job has `permissions: {}` and one step | `notify-website.yml:10,16-22` | confirmed |
| 3 | With an empty token the action fails without naming the secret | `peter-evans/repository-dispatch` at `28959ce8df70de7be546dd1250a005dd32156697` (tag v4.0.1, shallow clone read): `action.yml` gives `token` the default `${{ github.token }}`; `src/main.ts` calls `github.getOctokit(inputs.token)` inside `try`, `dist/index.js:2090-2093` throws "Parameter token or opts.auth is required" for an empty token, and a 404 becomes "Repository not found, OR token has insufficient permissions." Which of the two a run shows depends on whether the runner applies the input default to an explicit empty value; the fix makes that irrelevant (the new step fails first), so the runner's default handling is left unmeasured | confirmed |
| 4 | `secrets.NAME != ''` in a step's `env` yields the string `true` or `false`, and an unset secret is the empty string | to verify: the administrator, at the first release, confirms it for this environment secret (the same expression is already relied on in `release.yml:172-173,222-223` and `ci.yml`, record 0008 option 2, and an unset secret is documented to be the empty string) at `v0.1.0` (`docs/CONTRIBUTING.md`, setup step 6) by seeing the new step pass in job `dispatch`. Fails closed if wrong: the job stops with the message instead of dispatching | unverified |
| 5 | A `run` step without `shell:` on `ubuntu-latest` runs with `bash -e`, so `[[ … ]]` works | GitHub's documented default shell; `release.yml` uses `run:` steps the same way | confirmed |
| 6 | The Docker Hub check's message form is "… are missing: set … as secrets of the environment release." | `release-check.sh` `check_dockerhub_secrets` | confirmed |

## Acceptance criteria

- [ ] AC1: When `secrets.WEBSITE_DISPATCH_TOKEN` is empty or not set, the first step of job `dispatch`, named
  `Check the website token`, prints exactly
  `::error::WEBSITE_DISPATCH_TOKEN is missing: set WEBSITE_DISPATCH_TOKEN as a secret of the environment release.`
  on stdout and exits with code 1, so the dispatch step does not run. The decision is the step's own condition
  `[[ "$TOKEN_SET" != "true" ]]`: every value of `TOKEN_SET` other than exactly `true` (`false`, empty, `True`,
  unset) takes the failing branch (fail closed).
- [ ] AC2: When the secret is set (`TOKEN_SET` is `true`), the check step prints nothing and exits 0, and the
  dispatch step runs with unchanged `uses` pin, `token`, `repository`, `event-type` and `client-payload`.
- [ ] AC3: The secret's value reaches only the `token` input of the dispatch step: the check step's `env` holds
  `TOKEN_SET: ${{ secrets.WEBSITE_DISPATCH_TOKEN != '' }}` and nothing else, its `run` contains no `${{ }}`
  expression, and no job- or workflow-level `env`, permission or checkout is added.
- [ ] AC4: `docs/CONTRIBUTING.md` (*Versioning and releases*) says that a missing `WEBSITE_DISPATCH_TOKEN` fails
  the job `dispatch` at its token check.

## Tests per criterion

No production or test code changes; every criterion is verified under *Verification without tests*.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | none — see *Verification without tests* | — |
| AC2 | none — see *Verification without tests* | — |
| AC3 | none — see *Verification without tests* | — |
| AC4 | none — see *Verification without tests* | — |

## Verification without tests

Steps 4 (*Skeleton*), 5 (*Tests first*) and the *Coverage gate* are not applicable (*Changes without production or
test code*): the diff is one workflow and one Markdown file.

- AC1, AC2 — Dev, step 6/7: extract the check step's `run` text from the edited workflow (for example with
  `python3 -c` and `yaml.safe_load`, or by copying the block) into a scratch file and run it with `bash -e` for
  `TOKEN_SET` = `true` (expect exit 0, no output), `false`, empty, `True` and unset (each: exit 1 and exactly the
  AC1 line on stdout). Report the five results. `git diff origin/main -- .github/workflows/notify-website.yml`
  shows the dispatch step's lines unchanged (AC2). A real run happens only on a published release: fact 4's
  check at `v0.1.0`; the failing branch cannot be exercised in CI without removing the secret, so the local run
  is its verification.
- AC1-AC3 — *Analyzer gate* with `--lint-only` on the changed workflow (actionlint, which also runs shellcheck on
  the `run` script), Dev in step 7.
- AC3 — Reviewer and Security (diff review, step 8): `grep -n "WEBSITE_DISPATCH_TOKEN" .github/workflows/notify-website.yml`
  lists only the header comment, the check step's `env` line, the error text and the `token:` input.
- AC4 — Reviewer reads the sentence against the workflow.

## Approach

Insert as the first step of job `dispatch` in `.github/workflows/notify-website.yml` (before the existing
`uses:` step, which stays byte for byte):

```yaml
      - name: Check the website token
        env:
          TOKEN_SET: ${{ secrets.WEBSITE_DISPATCH_TOKEN != '' }}
        run: |
          if [[ "$TOKEN_SET" != "true" ]]; then
            echo "::error::WEBSITE_DISPATCH_TOKEN is missing: set WEBSITE_DISPATCH_TOKEN as a secret of the environment release."
            exit 1
          fi

```

The message contains the issue's text verbatim and follows the form of the Docker Hub check (fact 6). It is a
fixed literal: no `%`, CR or LF, so no annotation encoding is needed. Extend the header comment (lines 1-3) by one
sentence: "Its first step fails the job with a clear message when that secret is missing." Layout of the
user-visible output: the one annotation line above, nothing else.

## Options considered

1. **Inline check step in `notify-website.yml` (chosen)** — no checkout, no permission, no repository code runs in
   the job that holds the token; does not touch the files PR #84 edits.
2. **Reuse `release-check.sh`** (a new command, or a generalised `dockerhub-secrets`) — the refusal would be tested
   by `release-check-test.sh` on every PR, but the job would need `actions/checkout`, `contents: read` and
   repository code running next to the token, for a three-line condition; it also edits the usage header and the
   command `case` of `release-check.sh` and the test script, both changed by PR #84. Lost on those costs.
3. **Skip the job when the secret is missing (job-level `if:`)** — `secrets` is not available in a job `if:`, and a
   skip would hide a missing notification instead of failing; it fails open. Lost.
4. **No change** — the action's own message does not name the secret (fact 3). Lost: the issue's request is sound.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/workflows/notify-website.yml` | new first step `Check the website token`; header comment one sentence longer |
| Docs | `docs/CONTRIBUTING.md` | one sentence (see *Documentation updates*) |

## Signatures (for the Dev's skeleton)

none

## Test files

none (no production or test code).

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none of the coverage measure; the workflow is verified as in
*Verification without tests*.

## Areas

none — release behavior is described in `docs/CONTRIBUTING.md` (*Versioning and releases*); records 0012 and
0008 carry `Area: —`, and no area document mentions the dispatch (sweep: `grep -rn "dispatch\|WEBSITE_DISPATCH" docs/areas`
has no hit).

## Documentation updates

Sweep: `grep -rn "WEBSITE_DISPATCH\|notify-website\|credentials check" docs .squad/project.md README.md SECURITY.md`
— hits in `docs/CONTRIBUTING.md:223-245`, record 0012 and `.squad/project.md:20`; `docs/ARCHITECTURE.md` and the
area documents have none. Only the CONTRIBUTING paragraph states what happens when a secret is missing.

- `docs/CONTRIBUTING.md`, *Versioning and releases*, paragraph "The workflow cannot read the environment's
  settings …" (lines 245-247): after "a missing Docker Hub secret fails the credentials check and the release
  stays a pre-release" add that a missing `WEBSITE_DISPATCH_TOKEN` fails the job `dispatch` of
  `notify-website.yml` at its token check, with the message naming the secret. Dev, once the workflow is edited.
  Keep the edit inside those lines (PR #84, see *Problem*).
- `.squad/project.md:20` and record 0012: stay true, unchanged.

## Architecture check

`docs/ARCHITECTURE.md` and `.squad/project.md` (*Guarantees*: none recorded) are not touched. The properties of
record 0012 hold: the token stays an environment secret referenced only by job `dispatch`, which still runs no
repository code. Record 0008's rule (a secret only in the `env` of the step that uses it, presence tested as a
boolean) is applied, not changed.

## Security considerations

- [ ] Byte or length limits on parsed input: no limits.
- [ ] Exceptions reaching a user-visible message: none; the only new output is the fixed AC1 annotation, which
  contains no value of the secret.

## Decision records

- none: the check applies record 0008's pattern to record 0012's secret; the inline-versus-script choice is an
  implementation detail recorded under *Options considered*. Record 0012 is not edited (PR #84 changes it).

## Out of scope / follow-ups

- Whether the runner applies an input's default to an explicitly empty value (fact 3) — irrelevant once the check
  runs first.
