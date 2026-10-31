# 0011: The data-protection key ring lies in the configured data directory; the server stops when it cannot write it

- **Status:** Proposed
- **Date:** 2026-10-31
- **Area:** server
- **Source:** Issue #64
- **Supersedes:** —

## Context

ASP.NET Core protects antiforgery tokens and the Blazor component state with its data-protection key ring. Without
configuration it stores the ring under the user's home directory and, where it cannot create that directory, keeps it
in memory. In the Docker image that means the container layer (user 1654) or memory (another UID through `--user`,
`HOME=/`): the keys are lost whenever the container is recreated, and every value protected with them becomes
unreadable (record [0009](0009-server-image-runs-as-a-fixed-non-root-user.md), *Consequences*). `/config` is the
image's data directory, the only place that survives a recreation; #29 (database) and #33 (the `CENSIO__…`
configuration reference) will keep their data there too. Outside the image the home directory already persists.

## Options considered

1. **A setting `Censio:DataDirectory` (`CENSIO__DATADIRECTORY`), set to `/config` by the image; the ring lies in
   its subdirectory `data-protection-keys`; unset keeps ASP.NET Core's default** (chosen) — explicit, testable
   without Docker, one setting for the one volume that #29 can derive the database path from. Con: one new setting.
2. **`ENV HOME=/config` in the image, no code** — the default lookup then lands in `/config/.aspnet`. Con: a hidden
   directory, `HOME` changes for everything in the container, and an unwritable `/config` still falls back to
   memory with only a warning.
3. **A path hard-coded when the process runs in a container** — no setting. Con: implicit, not testable outside a
   container, and #33 lists the data paths as configuration.
4. **A setting for the key directory alone** (`Censio:DataProtection:KeyDirectory`) — Con: a second setting for the
   same volume once #29 adds the database.
5. **Keys in the database of #29** (`PersistKeysToDbContext`) — Con: a new package and a dependency on #29; the
   file system of the same volume does the job.
6. For a data directory the server cannot write: **fall back to in-memory keys with a warning** (today's
   behavior) or **start and fail on the first page** — Con: the first silently brings back the loss this issue
   removes, the second leaves a container that reports `healthy` with a broken start page. Chosen instead: **stop at
   start** with a critical log line and exit code 1.
7. **Encrypt the keys at rest** (certificate or DPAPI) — Con: the operator would have to provide and keep a
   certificate; the image has none. The key directory is created owner-only instead.

## Decision

The server keeps the key ring in `<data directory>/data-protection-keys` when `Censio:DataDirectory` is set, and
the image sets it to `/config`. It creates the key directory owner-only and the first key at start, and stops with
exit code 1 when the setting is not an absolute path or the ring cannot be written. The behavior and the log lines
are in [`areas/server.md`](../areas/server.md) (*Data directory and key ring*).

## Consequences

- Antiforgery tokens and other protected values stay valid across a recreation of the container that keeps `/config`.
- The README's earlier statement that an unwritable `/config` does not stop the server no longer holds: the operator
  fixes the ownership (`--user` or `chown`, record 0009) before the server runs.
- The keys lie unencrypted in the data directory (ASP.NET Core logs its warning about it); the directory's
  permissions and the operator's backups protect them. ASP.NET Core creates the key files owner-only itself.
- The repository is bound when the options are first resolved, from the application's final configuration, not
  while services are registered: a configuration source added late (tests, hosting) is honored, and registration and
  start check cannot disagree. Registering it eagerly from `builder.Configuration` was rejected for that reason.
- ASP.NET Core reports a refused write of the ring as a `CryptographicException` wrapping the I/O exception, and logs
  its own error entry first; the start check recognizes exactly that wrapped form besides the plain I/O exceptions
  and lets every other exception through.
- In the image the setting can be pointed to another absolute path but not disabled (`docker run -e` cannot unset
  an image `ENV`, and an empty value stops the start); acceptable while the image is unreleased.
- #29 and #33 build on `Censio:DataDirectory`; a separate database path stays possible.
- Revisit when the server runs as more than one instance on one key ring, or when a certificate becomes available
  in the deployment.
