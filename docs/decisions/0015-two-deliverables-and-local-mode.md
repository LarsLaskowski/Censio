# 0015: Two deliverables, the Companion ZIP and the server image; the Companion hosts the server for local use

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8; Issue #39 (local mode), Issue #40 (forward mode)
- **Supersedes:** —

## Context

The statistics pipe exists only on the Windows PC that runs the game, so something has to run there. The history and
the web pages are worth most where they are always reachable — a home server or a NAS — but many players have no
such machine and want everything on the gaming PC. Two kinds of users, one code base.

## Options considered

1. **One Windows program that reads the pipe and shows the pages itself** — simplest install; no history while the
   PC is off and no server for players who have one.
2. **Only a Companion plus a separately installed server** — one deployment model; players without a server would
   have to run Docker on the gaming PC.
3. **Two deliverables: the Companion as a Windows ZIP, the server as a Docker image; the Companion can also host the
   same server in its own process** — both groups are served by the same server code; two release artifacts.

## Decision

Option 3. Each release ships the Companion ZIP and the server image `networlddev/censio`. The Companion either
forwards its data to a separate server (forward mode, #40) or runs `Censio.Server` in its own process (local mode,
#39).

## Consequences

- The server code must run both in a container and inside the Companion process; nothing in it may assume one of
  the two (paths, ports, lifetime).
- The release builds and checks two artifacts ([0012](0012-release-pre-release-first-and-secret-free-builds.md)).
- `docs/ARCHITECTURE.md` describes local mode once #39 builds it.
