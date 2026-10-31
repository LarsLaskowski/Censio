# 0008: CI secrets are scoped to the steps that use them

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** —
- **Source:** Issue #3
- **Supersedes:** —

## Context

The CI job builds and tests pull request code, which includes repository code and the build logic of every
NuGet package it restores. `SONAR_TOKEN` was in the job-level `env`, so every step, that code included, had it in
its environment, and `actions/checkout` kept the job's `checks: write` token in `.git/config`. Fork and Dependabot
runs get no secrets, so only maintainers' branches and pushes to `main` were exposed. The SonarQube Cloud scanner
for .NET needs `begin`, the build and `end` in one job, so the build cannot move to a job without the token.

## Options considered

1. **Keep the job-level token** — simplest; every step holds the secret.
2. **The token only in the `env` of the two SonarQube steps; whether they run decided by a job-level `env` that
   holds only the boolean `secrets.SONAR_TOKEN != ''`; no persisted checkout credentials** — no step that runs
   pull request code has a secret in its environment or in `.git/config`; the skip test needs no extra step.
3. **As 2, but the skip decided by a step that has the token in its `env` and writes a boolean output** — one
   more step that holds the secret, for no gain over 2.
4. **Split analysis and build into separate jobs** — impossible with the .NET scanner (see Context).

## Decision

Option 2 in `.github/workflows/ci.yml`, and `persist-credentials: false` on every `actions/checkout` of
`ci.yml` and `codeql.yml`. What each check does is described in `docs/CONTRIBUTING.md`.

## Consequences

- Accepted residual exposure: pull request code runs in the same job before SonarQube's `end` step and could
  append to `$GITHUB_ENV` or `$GITHUB_PATH` or replace the scanner, and so reach the token in that step. Only
  maintainers' branches are affected; a leaked token carries the SonarQube Cloud permissions of the account that
  created it.
- A new workflow step that needs a secret gets it in its own `env`, never at job or workflow level.
- Revisit if the scanner can run `end` in a separate job, or if outside branches ever get secrets.
