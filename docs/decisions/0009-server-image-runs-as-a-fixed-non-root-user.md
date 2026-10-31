# 0009: The server image runs as a fixed non-root user; other UIDs come from `--user`

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** server
- **Source:** Issue #4
- **Supersedes:** —

## Context

Issue #4 requires that the container start as non-root. The Product Manager also requires NAS hosts (Unraid,
Synology) whose shares belong to other UIDs (`99:100` on Unraid, a DSM user such as `1026:100`). The `aspnet`
base image creates the user `app` (UID and GID 1654, home `/home/app`) but sets no `USER`, so without a switch the
image runs as root. `/config` is where #29 and #42 will keep the database and the uploaded catalog.

## Options considered

1. **`USER 1654`, `/config` owned by 1654; other UIDs through Docker's `--user` or a `chown` of the host
   directory** (chosen) — the container never starts as root, there is no script in the image, and any UID works
   because the application files are only read. Con: the operator must know the share's UID and GID, and a named
   volume with another `--user` is not writable.
2. **A PUID/PGID entrypoint (LinuxServer.io style) that starts as root, changes ownership and drops privileges** —
   what NAS users know. Con: the container starts as root, which breaks the issue's criterion, and the image
   needs a privilege-dropping tool and a script that runs as root.
3. **Run as root** — works on every share. Con: contradicts the issue.
4. **`VOLUME /config`** in addition to option 1 — data lands in a volume even without `-v`. Con: every `docker
   run` without `-v` leaves an anonymous volume behind, and every documented command passes `-v` anyway.

## Decision

Option 1 without `VOLUME`: the image ends with `USER 1654`, creates `/config` owned by 1654, and ships no
entrypoint script. The behavior, the NAS command and the two ownership remedies are in
[`areas/server.md`](../areas/server.md) (*Docker image*).

## Consequences

- An operator on a NAS passes `--user <uid>:<gid>` or gives the directory to 1654; the documentation shows both.
- Under a UID without a home directory ASP.NET Core keeps its data-protection keys in memory and logs a warning;
  under 1654 they lie in the container layer. Both lose the keys when the container is recreated, until the
  key ring moves to `/config` with #29/#33/#42.
- Revisit when a NAS host turns out to have no way to pass `--user`; option 2 then needs the Product Manager,
  because it changes the issue's requirement.
