# 0010: The image bases are full-version amd64 tags pinned by digest; the `base.digest` label is checked in CI

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** server
- **Source:** Issue #4
- **Supersedes:** —

## Context

The image is linux/amd64 only and must carry the OCI labels `base.name` and `base.digest` of its runtime base
(issue #4). `FROM --platform` triggers hadolint DL3029, and the issue requires a Dockerfile without hadolint
findings. The multi-architecture tag `10.0-alpine` has an index digest that differs from the digest of its
amd64 manifest. MCR publishes single-architecture tags both floating (`10.0-alpine-amd64`) and fully versioned
(`10.0.14-alpine3.24-amd64`, `sdk:10.0.403-alpine3.24-amd64`).

Dependabot (`docker` entry for `/src/Censio.Server`) parses `FROM <image>:<tag>@sha256:…`, never touches a
`LABEL`, and skips a `FROM` line built from an `ARG` (`dependabot-core`, `docker/lib/dependabot/docker/`
`file_parser.rb`, `tag.rb`, `update_checker.rb`, read in Security's plan review). With a floating tag the tag
never changes within .NET 10, so every .NET and Alpine patch could arrive only as a digest-only refresh, which
the server-side experiment `docker_digest_only_update_suppression` suppresses for exactly this case and a digest
cooldown holds back — invisible from the repository. With a full-version tag a .NET patch is a tag update. The
plain `aspnet` Alpine image runs with invariant globalization and contains no tzdata; the `-extra` variant adds
ICU and tzdata.

SonarQube Cloud's rule `docker:S8431` flags every `FROM` that names both a tag and a digest, because the digest
alone selects the image and a tag that no longer matches it misleads the reader; it rates the finding as a
vulnerability and fails the quality gate (pull request #66). A Dockerfile treats `#` as a comment only at the
start of a line, so a same-line `NOSONAR` on a `FROM` line is impossible.

## Options considered

1. **Full-version single-architecture tags (`-alpineX.Y-amd64`) pinned by their manifest digest, the label a
   literal copy, a CI check that fails when label and `FROM` differ** (chosen) — the platform is fixed without
   `--platform`, each .NET patch arrives as a Dependabot tag update, and `base.name` names the exact image. Con:
   each Dependabot pull request fails CI until the label is changed by hand, and a new Alpine minor (new tag
   suffix) is a manual bump.
2. **Floating `10.0-alpine-amd64` tags pinned by digest** — Alpine minors follow automatically. Con: patches
   depend on the digest-only refresh, which may be suppressed; nothing would notice a frozen base.
3. **Option 2 plus a scheduled job that compares the pinned digest with the tag's current one** — fails closed.
   Con: a second workflow and a registry query for what option 1 gets from Dependabot.
4. **One `ARG` for tag and digest, used in `FROM` and `LABEL`** — one source. Con: Dependabot no longer updates it.
5. **Leave `base.digest` to the release and edge builds (#5, #6) through `--label`** — no duplicate. Con: the issue
   puts the labels in the Dockerfile, and a local build would carry none.
6. **Multi-architecture tag pinned by its index digest** — fewer tags. Con: the platform follows the host, and
   `base.digest` would name an index, not the base image the layers come from.
7. **The `-extra` runtime variant** — ICU and time zones. Con: a larger image for a need the server does not have
   yet (it formats no culture-specific text and logs in UTC).

For `docker:S8431` on option 1's `FROM` lines:

8. **Exclude `docker:S8431` for `src/Censio.Server/Dockerfile` with `sonar.issue.ignore.multicriteria`, passed
   by the CI scanner and the *Sonar gate* alike** (chosen) — reviewable in the repository, survives each base
   bump. Con: a second scanner argument kept equal in two places.
9. **Digest only** — satisfies the rule. Con: Dependabot cannot propose a tag update without a tag, and
   `base.name` could no longer name the image.
10. **Tag only** — satisfies the rule. Con: the pull follows a mutable tag; the base image is no longer pinned.
11. **Mark the issue *Accepted* in the SonarQube Cloud UI** — no repository change. Con: invisible in the
    repository, and the issue is matched to its line, so a base bump can raise it again on new code.

## Decision

Option 1 with the plain runtime variant; Dependabot ignores major base-image updates, which a `net10.0` app
cannot take. `docker:S8431` is excluded for this Dockerfile (option 8): the digest keeps the integrity the rule
protects, and the tag is what Dependabot updates. The exact tags, the labels and the limits that follow (invariant globalization, no time zones) are
in [`areas/server.md`](../areas/server.md) (*Docker image*).

## Consequences

- A Dependabot pull request for the base image is completed by hand: set the label to the digest of the new
  `FROM` line (`docs/CONTRIBUTING.md`, *Automated checks*); the CI check makes a forgotten label fail closed.
- Accepted limit: an Alpine fix between two .NET patches reaches the image only through a digest refresh or the
  next .NET patch (MCR rebuilds its tags monthly), and a new Alpine minor, or the end of new tags on the pinned
  one, needs a manual bump of both `FROM` lines (`docs/CONTRIBUTING.md`). Nothing in this change notices the end
  of new tags; a scheduled check for it is a follow-up issue, decided after the plan's security review.
- A hand edit that changes a tag without its digest builds the old image under the new `base.name`; nothing
  checks that a tag still resolves to its pinned digest, because MCR rebuilds its tags. The exclusion covers only
  `docker:S8431` in this one file (`.squad/stack.md`, *Analyzer gate*).
- arm64 hosts are out; adding them needs a multi-platform build and changes this record.
- A feature that needs cultures or local time zones moves the runtime stage to the `-extra` variant; the move to
  .NET 11 changes both tags and the Dependabot `ignore` by hand.
