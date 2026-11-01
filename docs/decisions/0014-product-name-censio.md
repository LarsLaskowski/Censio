# 0014: The product is named Censio

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8
- **Supersedes:** —

## Context

The product needs one name for the repository, the solution and its projects, the Docker image, the Companion ZIP,
the documentation and the user interface. Anno 117 is set in the Roman Empire, and the tool counts what a
settlement has and produces. A name that contains the game's or its publisher's trademark could be read as an
official product.

## Options considered

1. **A name that contains "Anno"** — instantly recognisable; borrows a trademark and suggests an official tool.
2. **Censio** (Latin *census*, the Roman count of citizens and property) — fits the setting and the purpose, is
   short and carries no third-party trademark; it has to be explained once.

## Decision

Option 2. The product, the repository, the solution `Censio.slnx`, every project (`Censio.*`), the Docker image
`networlddev/censio` and the Companion ZIP `Censio-<version>-win-x64.zip` carry the name Censio.

## Consequences

- The README says in its first sentence what Censio does, because the name alone does not.
- Renaming later would touch every project, namespace, the image repository on Docker Hub and the release assets.
