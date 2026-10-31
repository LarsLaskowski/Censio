# 0010: The image bases are pinned by their amd64 manifest digest; the `base.digest` label is checked in CI

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** server
- **Source:** Issue #4
- **Supersedes:** —

## Context

The image is linux/amd64 only and must carry the OCI labels `base.name` and `base.digest` of its runtime base
(issue #4). `FROM --platform` triggers hadolint DL3029, and the issue requires a Dockerfile without hadolint
findings. The multi-architecture tag `10.0-alpine` has an index digest that differs from the digest of its
amd64 manifest. Dependabot (`docker` entry for `/src/Censio.Server`) updates the image reference in a `FROM` line,
but not a `LABEL`, and it skips a `FROM` line built from an `ARG`. The plain `aspnet` Alpine image runs with
invariant globalization and contains no tzdata; the `-extra` variant adds ICU and tzdata.

## Options considered

1. **Single-architecture tags (`sdk:10.0-alpine-amd64`, `aspnet:10.0-alpine-amd64`) pinned by their manifest
   digest, the label a literal copy, a CI check that fails when label and `FROM` differ** (chosen) — the
   platform is fixed without `--platform`, and Dependabot keeps updating both `FROM` lines. Con: each Dependabot
   pull request fails CI until the label is changed to the new digest by hand.
2. **One `ARG` for tag and digest, used in `FROM` and `LABEL`** — one source. Con: Dependabot no longer updates it.
3. **Leave `base.digest` to the release and edge builds (#5, #6) through `--label`** — no duplicate. Con: the issue
   puts the labels in the Dockerfile, and a local build would carry none.
4. **Multi-architecture tag pinned by its index digest** — fewer tags. Con: the platform follows the host, and
   `base.digest` would name an index, not the base image the layers come from.
5. **The `-extra` runtime variant** — ICU and time zones. Con: a larger image for a need the server does not have
   yet (it formats no culture-specific text and logs in UTC).

## Decision

Option 1 with the plain runtime variant. The exact tags, the labels and the limits that follow (invariant
globalization, no time zones) are in [`areas/server.md`](../areas/server.md) (*Docker image*).

## Consequences

- A Dependabot pull request for the base image is completed by hand: set the label to the digest of the new
  `FROM` line (`docs/CONTRIBUTING.md`, *Automated checks*); the CI check makes a forgotten label fail closed.
- arm64 hosts are out; adding them needs a multi-platform build and changes this record.
- A feature that needs cultures or local time zones moves the runtime stage to the `-extra` variant.
