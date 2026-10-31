# 0008: CI secrets are scoped to the steps that use them

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** —
- **Source:** Issue #3
- **Supersedes:** —

## Context

The CI job builds and tests pull request code, which includes repository code and the build logic of every
NuGet package it restores. `SONAR_TOKEN` was in the job-level `env`, so every step had it in its environment,
and `actions/checkout` kept the job's `checks: write` token in `.git/config`. Pushes to `main` and maintainers'
branches run with the secret; fork and Dependabot runs get none. The SonarQube Cloud scanner for .NET needs
`begin`, the build and `end` in one job, and both `begin` and `end` take the token as an argument.

Within one job, step scoping cannot stop malicious code: GitHub-hosted runners give the job passwordless `sudo`
and deliver every secret the job references at job start, so code in any step can read `SONAR_TOKEN` and the
`GITHUB_TOKEN` from the runner's memory, read `/proc/<pid>` of the SonarQube steps (the token is in their command
line) from a lingering process (MSBuild node reuse, the compiler server), or tamper with later steps through
`$GITHUB_ENV`, `$GITHUB_PATH`, the scanner or the files of later actions. The realistic attacker is the build
code of a compromised NuGet dependency, merged for example from a Dependabot pull request, running on `main`.
What scoping does stop is accidental disclosure: an environment dump, a crash or telemetry report, a log line.

The scanner downloads a JRE and its engine into `~/.sonar/cache` and runs them without a checksum check; a
cache saved after the build would let build code plant an executable that every later run, with every later
token, executes.

## Options considered

1. **Keep the job-level token** — simplest; every step's environment, and so every dump or report, holds it.
2. **The token only in the `env` of the two SonarQube steps; whether they run decided by a job-level `env` that
   holds only the boolean `secrets.SONAR_TOKEN != ''`; no persisted checkout credentials** — no step that runs
   pull request code has a secret in its environment or in `.git/config`; the skip test needs no extra step.
3. **As 2, but the skip decided by a step that has the token in its `env` and writes a boolean output** — one
   more step that holds the secret, for no gain over 2.
4. **Split analysis and build into separate jobs** — impossible with the .NET scanner (see Context).

For the scanner cache: a restore before `begin` and a save directly after it, before the build, against
**no cache** — chosen: a cache entry saved by an earlier workflow version under the same key could still be
restored, and the download costs seconds.

## Decision

Option 2 in `.github/workflows/ci.yml`, `persist-credentials: false` on every `actions/checkout` of `ci.yml`
and `codeql.yml`, and no `actions/cache` in `ci.yml`, so nothing the build can write is cached for a later run.
What each check does is described in `docs/CONTRIBUTING.md`.

## Consequences

- Accepted residual exposure: malicious code in the job (a compromised dependency on `main` or a maintainer's
  branch) can obtain `SONAR_TOKEN` and the job's `checks: write` `GITHUB_TOKEN` as described in Context. A leaked
  SonarQube token carries the SonarQube Cloud permissions of the account that created it; the `GITHUB_TOKEN`
  expires with the job.
- A maintainer who pushes to a Dependabot branch (to format it, or to run SonarQube on a raised scanner) gives
  that run the secrets while the raised packages build, so the Dependabot diff is reviewed first
  (`docs/CONTRIBUTING.md`). `SONAR_TOKEN` is never added as a Dependabot secret.
- A new workflow step that needs a secret gets it in its own `env`, never at job or workflow level.
- Revisit if the scanner can run `end` in a separate job, or if outside branches ever get secrets.
