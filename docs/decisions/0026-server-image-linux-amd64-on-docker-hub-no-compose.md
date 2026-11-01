# 0026: The server image is published for linux/amd64 on Docker Hub; no compose file is shipped

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** server
- **Source:** Issue #8; Issue #4 (Docker image)
- **Supersedes:** —

## Context

The server runs on a home server or a NAS (Unraid, Synology) whose operators install containers from a registry
through their NAS's UI or with `docker run`. The CI runners build and test on x86-64, and each additional platform
means another build and another set of checks in the release
([0012](0012-release-pre-release-first-and-secret-free-builds.md)). The server is a single container without
companion services.

## Options considered

1. **A multi-architecture image (amd64 and arm64)** — also Raspberry Pi and ARM NAS models; doubles the builds and
   checks, and arm64 cannot be tested on the CI runners without emulation.
2. **GitHub Container Registry** — next to the code; less familiar to NAS UIs, which search Docker Hub.
3. **linux/amd64 only, on Docker Hub, and the `docker run` command in the documentation instead of a compose file** —
   one image to build and check, found where NAS users look; one container needs no compose file.

## Decision

Option 3. The image `networlddev/censio` is built and published for linux/amd64 only, on Docker Hub, and the
repository ships no compose file; the documentation shows `docker run`. The image's rules are in
[`areas/server.md`](../areas/server.md) (*Docker image*).

## Consequences

- arm64 hosts are not supported (README, *Getting Censio*); adding them revisits this record and
  [0010](0010-server-image-base-pinned-by-amd64-manifest-digest.md).
- An operator who wants compose writes the file from the `docker run` command.
