# Plan: Detect a frozen pinned Alpine base of the server image

Source: Issue #65
Status: Draft
Tier: standard — a new scheduled CI check (behavior of the CI) that reads a public registry with a read-only token and no secret; it creates, changes or removes no security control (no new write permission, secret, signing or exposure; it only reports).
Security areas touched: CI workflow (new `.github/workflows/base-check.yml`: triggers `schedule`, `workflow_dispatch`, path-filtered `pull_request`; `permissions: contents: read`; pinned `actions/checkout`), a new CI script `.github/scripts/base-check.sh`, and a new outbound call from CI (anonymous HTTPS GET to `mcr.microsoft.com`). The Dockerfile is read, not changed; no dependency.

## Problem / root cause

Not a code defect but a missing check. `src/Censio.Server/Dockerfile` lines 1 and 31 pin
`sdk:10.0.403-alpine3.24-amd64` and `aspnet:10.0.14-alpine3.24-amd64` by digest (record 0010, option 1).
Dependabot (`.github/dependabot.yml`, `docker` entry for `/src/Censio.Server`) proposes a tag update only within
the same tag suffix, so once MCR stops publishing `-alpine3.24-amd64` tags it proposes nothing, and record 0010
(*Consequences*, second bullet) says so: "Nothing in this change notices the end of new tags". The base then stops
receiving .NET and Alpine patches without any signal in the repository.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The Dockerfile has exactly two `FROM` lines, both `mcr.microsoft.com/dotnet/<image>:<M.m.p>-alpine<X.Y>-amd64@sha256:…` | `src/Censio.Server/Dockerfile` lines 1 (`sdk:10.0.403-alpine3.24-amd64 … AS build`) and 31 (`aspnet:10.0.14-alpine3.24-amd64`) | confirmed |
| 2 | Issue: `alpine3.22` ended at `10.0.3`, `alpine3.23` at `10.0.12` | `curl https://mcr.microsoft.com/v2/dotnet/aspnet/tags/list` (2026-10-31), tags `^10\.0\.[0-9]+-alpine[0-9.]+-amd64$`: 3.22 = 10.0.0–10.0.3, 3.23 = 10.0.1–10.0.12, 3.24 = 10.0.9–10.0.14; SDK: 3.22 = 10.0.100–103, 3.23 = 10.0.101–401, 3.24 = 10.0.301–403 | confirmed |
| 3 | A new Alpine minor overlaps the old one for several patches (3.23 and 3.24 both got 10.0.9–10.0.12), so "a newer minor exists" alone is no failure; "a newer patch only on a newer minor" is the signal | same tag lists as row 2 | confirmed |
| 4 | Today nothing is frozen: no `alpine3.25` tag exists, the pinned tags are the newest on every minor | same tag lists (no tag containing `alpine3.25`; aspnet max 10.0.14, SDK max 10.0.403) | confirmed |
| 5 | The MCR tag list is anonymous (no token) and unpaginated: one response holds every tag | `curl -D -` of the aspnet list: `HTTP/2 200`, `content-type: application/json`, no `Link` header; 11 657 aspnet and 7 010 SDK tags, about 450 kB and 230 kB | confirmed |
| 6 | Variants are separate suffixes: `-extra-amd64`, `-composite-amd64`, SDK `-aot-amd64` exist next to `-amd64`, so the check must match the pinned suffix exactly | aspnet list holds `10.0.14-alpine3.24-extra-amd64`, `10.0.13-alpine3.24-composite-amd64`; SDK list `10.0.403-alpine3.24-aot-amd64` | confirmed |
| 7 | Dependabot proposes no update across a tag-suffix change (`alpine3.24` → `alpine3.25`) | record 0010 *Context* (`dependabot-core` `docker/…/tag.rb`, `update_checker.rb`, read in #4's Security plan review) and `docs/CONTRIBUTING.md` line 77–78 ("A new Alpine minor … is a manual bump") | confirmed |
| 8 | `curl` and `jq` exist on `ubuntu-latest`; `jq` is already used by CI | `.github/workflows/ci.yml` step *Check tool versions* uses `jq`; `image-check.sh` uses `curl` on the runner | confirmed |
| 9 | `mcr.microsoft.com` is reachable from GitHub-hosted runners without credentials | to verify: the PR's own run of the new workflow (path trigger) passes; owner orchestrator, step 11 | unverified |
| 10 | A failed scheduled run notifies a maintainer (GitHub notifies the user who last changed the `cron` line), and a public repository's schedules are disabled after 60 days without activity | to verify: GitHub documentation (*Events that trigger workflows*, `schedule`), not reachable from the session (`docs.github.com` 403); owner Reviewer in step 8, by reading the page, else the documentation updates leave both statements out | unverified |
| 11 | No release tag exists, so record 0010 is unreleased and is edited in place | `git ls-remote --tags origin` prints nothing | confirmed |
| 12 | Workflows under `.github/workflows/` are product files: not template-managed and not reported by `scope-check.py` | `.squad/orchestration.md` *Template-managed files* lists none; `.squad/tools/scope-check.py` lines 58–67; precedent #63 added `notify-website.yml` in a product PR | confirmed |

## Acceptance criteria

- [ ] AC1: `.github/scripts/base-check.sh [dockerfile]` (default `src/Censio.Server/Dockerfile`) checks every
  `FROM` line and exits 0 on today's Dockerfile, printing one pass line per `FROM` line (layout in *Approach*).
- [ ] AC2: For a pinned `<M.m.p>-alpine<X.Y><suffix>` it exits 1 when the registry holds a tag
  `<M.m.q>-alpine<A.B><suffix>` with `A.B` newer than `X.Y` and `q` greater than the newest patch on `X.Y`
  (same image, same `M.m`, same suffix), printing the frozen line of *Approach* for that image. It exits 0 when
  every patch newer than the pin also exists on `X.Y` (Dependabot's case) or no newer patch exists.
- [ ] AC3: It fails closed (exit 1, a cannot-check line) when a `FROM` line is not a full-version
  `mcr.microsoft.com/dotnet/<image>` Alpine tag, the file has no `FROM` line, the pinned tag is not in the
  registry's list, the request fails or times out, or the response is no tag list; it checks the remaining
  `FROM` lines before exiting, and prints nothing from the registry except tags that matched its pattern.
- [ ] AC4: `.github/workflows/base-check.yml` runs the script weekly, on `workflow_dispatch` and on pull requests
  against `main` that change the Dockerfile, the script or the workflow; it has `permissions: contents: read`,
  no secret, `actions/checkout` pinned to the SHA `ci.yml` uses with `persist-credentials: false`, a job
  timeout, and passes `actionlint`; the script passes `shellcheck` and the gate's shell rules.
- [ ] AC5: `docs/CONTRIBUTING.md` (*Automated checks*) and record 0010 describe the check as built (see
  *Documentation updates*).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1–AC5 | none: no production or test code; see *Verification without tests* | — |

## Verification without tests

The change touches no production or test code (a workflow and a CI script): steps 4 (*Skeleton*), 5 (*Tests
first*) and the *Coverage gate* are **not applicable**. The Dev runs V1–V4 in step 6 against the real registry,
each on a scratch copy of the Dockerfile outside the working tree (only the `FROM` line differs; the digest may
stay, the check does not read it), and reports each command, exit code and output in the hand-back:

- **V1 (AC1):** today's `src/Censio.Server/Dockerfile` → exit 0, two pass lines (sdk 10.0.403, aspnet 10.0.14,
  both alpine3.24).
- **V2 (AC2), frozen:** runtime `aspnet:10.0.12-alpine3.23-amd64` → exit 1, frozen line naming 3.23, 10.0.12,
  10.0.14 and 3.24; build `sdk:10.0.103-alpine3.22-amd64` → exit 1 naming 10.0.103 and 10.0.403 on 3.24;
  runtime `aspnet:10.0.9-alpine3.23-amd64` (behind on a frozen minor) → exit 1 naming 10.0.12 and 10.0.14;
  runtime `aspnet:10.0.12-alpine3.23-extra-amd64` → exit 1 naming `10.0.14-alpine3.24-extra-amd64` (suffix kept).
  Both `FROM` lines frozen at once → two frozen lines, exit 1.
- **V3 (AC2), passing:** runtime `aspnet:10.0.13-alpine3.24-amd64` (behind, newer patch on the same minor) →
  exit 0; build `sdk:10.0.402-alpine3.24-amd64` → exit 0.
- **V4 (AC3), fail closed:** each of floating `aspnet:10.0-alpine-amd64`, `docker.io/library/alpine:3.24`,
  `aspnet:10.0.14-alpine3.99-amd64` (not published), a file without `FROM`, a missing file, and today's
  Dockerfile with `HTTPS_PROXY=http://127.0.0.1:9 https_proxy=http://127.0.0.1:9` (request fails) → exit 1 with a
  cannot-check line; with the first case on line 1 the second `FROM` line still gets its pass line. A non-JSON
  response cannot be produced without a test hook: the Reviewer checks that branch by reading (`jq` failure and
  an empty tag list both end in the cannot-check line, `set -euo pipefail`).
- **V5 (AC4):** *Analyzer gate* with `--lint-only` (actionlint, shellcheck, `shelldre:S7679`/`S7688`) on the two
  new files — orchestrator, step 7; the PR's own run of *Base image check* (path trigger) is green — orchestrator,
  step 11 (fact 9).
- **V6 (AC5):** Reviewer, step 8, reads `docs/CONTRIBUTING.md` and record 0010 against the script.

## Approach

**Script `.github/scripts/base-check.sh`** (bash, the style of `image-check.sh`: `set -euo pipefail`, functions
with `local` copies of positional parameters, `[[ … ]]`):

1. Argument 1 is the Dockerfile, default `src/Censio.Server/Dockerfile`; a missing file or no line matching
   `^FROM ` is a cannot-check failure.
2. Per `FROM` line, the second field is `<image>:<tag>@<digest>` (the digest is ignored). `<image>` must match
   `^mcr\.microsoft\.com/dotnet/([a-z0-9-]+)$` and `<tag>` must match
   `^([0-9]+)\.([0-9]+)\.([0-9]+)-alpine([0-9]+)\.([0-9]+)(-[a-z0-9-]+)$`; otherwise cannot-check.
3. Fetch `https://mcr.microsoft.com/v2/dotnet/<repository>/tags/list` once per repository with
   `curl -fsS --proto '=https' --max-time 30 --retry 2`, read `.tags[]` with `jq -r`; a failing `curl` or `jq`, or
   an empty list, is cannot-check. The URL is built only from the validated repository name.
4. Keep only the tags matching `^<M>\.<m>\.([0-9]+)-alpine([0-9]+)\.([0-9]+)<suffix>$` (the pin's major, minor and
   suffix, regex-escaped). The pinned tag must be among them, else cannot-check. `newest_pinned` is the highest
   patch on `X.Y`; over the minors newer than `X.Y` (compared numerically, major then minor), take the highest
   patch and, on a tie, the newest minor carrying it. Frozen when that patch is greater than `newest_pinned`.
5. Check every `FROM` line, print one line each, exit 1 when any line failed, else 0. Only the Dockerfile's
   validated values and registry tags that matched step 4 are ever printed.

**Output layout** (one line per `FROM` line; failures on stderr, as GitHub error annotations):

- pass: `base-check: <image>:<tag>: newest <M.m.p'> on alpine<X.Y>; no newer patch only on a later Alpine minor`
- frozen: `::error::base-check: <image>:<tag>: alpine<X.Y> ends at <M.m.p'>, <M.m.q>-alpine<A.B><suffix> exists only on a later Alpine minor; move both FROM lines and the base labels to alpine<A.B> (docs/CONTRIBUTING.md, Automated checks)`
- cannot check: `::error::base-check: <subject>: cannot check: <reason>`, where `<subject>` is the Dockerfile path
  (no `FROM`, missing file) or the validated image and tag, and for an unparsable `FROM` line its line number
  (`line <n>`), never the raw text. Reasons: `no FROM line`, `file not found`, `not a full-version
  mcr.microsoft.com/dotnet Alpine tag`, `tag not in the registry's list`, `registry request failed`, `registry
  answer holds no tag list`.

**Workflow `.github/workflows/base-check.yml`**, name *Base image check*, one job `base` (name *Base image
check*), `runs-on: ubuntu-latest`, `timeout-minutes: 5`; triggers `schedule: cron '30 5 * * 3'` (Wednesday 05:30
UTC, the day after MCR's Patch-Tuesday releases), `workflow_dispatch`, and `pull_request` to `main` with `paths`
`src/Censio.Server/Dockerfile`, `.github/scripts/base-check.sh`, `.github/workflows/base-check.yml`; workflow-level
`permissions: contents: read`; steps: `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1` with
`persist-credentials: false`, then `run: bash .github/scripts/base-check.sh`. A header comment states what it
checks and that it uses no secret. The existing `github-actions` Dependabot entry covers the new file.

## Options considered

1. **Separate workflow: weekly schedule, manual dispatch, path-filtered pull requests** (chosen) — catches the
   freeze within a week of the first missed patch; an unrelated pull request never turns red because MCR moved;
   a base bump or a change of the script is checked in its own pull request.
2. **A step in `ci.yml` (job `image`) on every pull request** — no new workflow. Con: any pull request turns red
   the day a patch ships only on a new minor, for a reason outside it, and nothing runs while no one pushes.
3. **Open or update a GitHub issue on failure** — more visible than a red run. Con: needs `issues: write`, a
   write permission for a check that otherwise only reads; a red scheduled run suffices (fact 10).
4. **Compare the pin with the floating `10.0-alpine-amd64` tag** (resolve its digest, find the full-version tag
   with the same digest) — follows MCR's own choice of minor. Con: two manifest requests per image with `Accept`
   headers for a result the tag list gives directly; a digest match fails during MCR's monthly rebuilds.
5. **Renovate instead of Dependabot**, with a custom versioning that crosses Alpine minors — would propose the
   bump itself. Con: a second update tool, a GitHub App with write access, and the label edit by hand stays.
6. **Python instead of bash** — easier version comparison. Con: the gate lints shell (`shellcheck`, `shelldre`)
   but no Python under `.github/`, and the existing CI script is bash.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/scripts/base-check.sh` | new script (*Approach*) |
| CI | `.github/workflows/base-check.yml` | new workflow (*Approach*) |
| Docs | `docs/CONTRIBUTING.md` | *Automated checks* (*Documentation updates*) |
| Records | `docs/decisions/0010-server-image-base-pinned-by-amd64-manifest-digest.md` | extended by the Lead (done) |

## Signatures (for the Dev's skeleton)

none — no C# API.

## Test files

None (*Verification without tests*). Existing test call sites affected: none. Files no unit test can cover: the
script and the workflow — CI files outside `COVERAGE_PATHSPECS` (`src/*.cs`, `src/*.razor`), so no coverage
exclusion changes.

## Areas

none — the server's behavior and the image do not change. The check is a contribution process, described in
`docs/CONTRIBUTING.md` like the job `image`; record 0010 links it.

## Documentation updates

Sweep: `docs/ARCHITECTURE.md`, `docs/areas/*.md`, `README.md`, `SECURITY.md`, `.squad/stack.md`,
`.squad/project.md` searched for `workflow`, `codeql`, `Dependabot`, `alpine`, `schedul`; hits only in
`docs/CONTRIBUTING.md` (*Automated checks*, lines 50–85) and `docs/areas/server.md` lines 66–67 and 95–96
(tags and the Dependabot label edit, unchanged by this change). Owner Dev, after V1–V4, against the script as built,
inside the `getting-started` project block:

- `docs/CONTRIBUTING.md`, *Automated checks*: a new bullet **Base image check**
  (`.github/workflows/base-check.yml`) after *Image check*: when it runs (weekly Wednesday 05:30 UTC, manual, pull
  requests changing the Dockerfile, the script or the workflow), what turns it red (a newer .NET patch of a pinned
  base only on a newer Alpine minor; or the cannot-check cases), what to do (move both `FROM` lines and the base
  labels to the named minor), that it uses no secret, and the local run `bash .github/scripts/base-check.sh`.
  Who is notified and the 60-day rule only if fact 10 was confirmed.
- same file, *Dependabot* bullet, last sentence: add that the *Base image check* reports when the manual bump to a
  new Alpine minor is due.

`.squad/project.md` *Outbound calls* names the product's calls, not CI's: unchanged. `docs/ARCHITECTURE.md`:
unchanged (no guarantee or flow changes).

## Architecture check

`docs/ARCHITECTURE.md` *Deployment* (bases pinned by digest, record 0010) is kept: the check reads the pins and
changes none. `.squad/project.md` lists no guarantees. Record 0008: the new workflow has no secret, and its
checkout keeps no credentials.

## Security considerations

- [ ] Byte limits: no size limit of its own; the request is bounded by `--max-time 30` and the job by
  `timeout-minutes: 5`. Registry data is printed only as tags that matched the anchored pattern of step 4, so
  no registry text can form a workflow command; Dockerfile text is printed only after validation (or as a line
  number).
- [ ] User-visible failures: the cannot-check reasons of *Approach*; `curl`'s own error text may appear in the log
  above them (stderr of the request), no secret is involved.

## Decision records

- `docs/decisions/0010-server-image-base-pinned-by-amd64-manifest-digest.md` (Accepted, unreleased, extended in
  place as `docs/decisions/README.md` prescribes): *Context* gains the MCR overlap evidence, *Options considered*
  options 12–16 for the check, *Decision* one sentence, and the *Consequences* bullet that said nothing notices the
  end of new tags now names the check. Index row unchanged (title still fits).

## Out of scope / follow-ups

- An Alpine fix between two .NET patches on a still-maintained minor (record 0010's accepted limit), and a check
  that a tag still resolves to its pinned digest (record 0010, third consequence).
- A failure of the check opens no issue (option 3).
- Unrelated: `docs/CONTRIBUTING.md` *Contributions from outside* says the workflows "come from a shared template",
  which is template text outside the project blocks; reported for a `squad` lesson, not changed here.

## Security notes

Non-blocking findings of Security's `APPROVED` plan review, verbatim:

1. Some FROM forms are skipped without failing, which breaks the AC3 claim and record 0010's "fails closed when it cannot read the pins". Step 1 matches `^FROM ` only. Docker's parser also accepts lower or mixed case (`from`), leading whitespace, a tab after the keyword, and a UTF-8 BOM before line 1, which BuildKit strips. A FROM line in any of these forms is silently left out instead of reported. If line 1 carried a BOM, the build stage would go unchecked. These forms already fail safe: `FROM --platform=… img` (field 2 fails the check), `FROM \` with a continuation line (field 2 is empty), `FROM ${ARG}` (fails the image check). Fix: strip a BOM from line 1, select lines with a case-insensitive `^[[:space:]]*FROM[[:space:]]`, and take the first field that does not start with `--`, or reject such lines outright. Add one V4 case for each (lowercase, indented, BOM). Non-blocking because the check is detection, not a control, and today's Dockerfile uses plain `FROM`.
2. Bash arithmetic on captured numbers. Compare with `10#$n`, so a leading-zero component such as `08` is not read as octal (that errors under `set -e` and gives a misleading exit). The captures are digits only, so the `a[$(cmd)]` arithmetic-injection pattern cannot occur. Never pass a raw registry string into `(( ))`.
3. `jq -r '.tags[]'` splits a tag that contains a newline into several lines. A tampered answer could slip in a fake matching tag and change the result, which only flips red and green. Optional hardening: `jq -r '.tags | if type == "array" then .[] | strings | select(test("\n") | not) else error end'`.
4. The response has no size limit, which the plan says openly; it is bounded only by `--max-time 30` and the job timeout. An optional `--max-filesize` (for example 8M; the answers today are about 450 kB) would make the bound explicit. Not required.
5. Plan fact 1 has a wrong line number. It says the aspnet `FROM` is on line 31; on origin/main it is line 28 (`grep -n FROM src/Censio.Server/Dockerfile`: lines 1 and 28). This matters for the Dev's scratch copies in V2–V4.
