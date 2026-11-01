# Server

## Scope

The server's HTTP surface: the address it listens on, the start page, the health check, how unknown paths and
plain HTTP are treated, the version it shows, the data directory with its data-protection key ring, and the Docker image it ships in. Operators who run the server and
the Docker health probe rely on it.

## Behavior

### Listen address

- Outside the Docker image, without address configuration the server listens on `http://localhost:5000` (loopback
  only) and logs the line
  `Now listening on: http://localhost:5000`. The image listens on all interfaces of the container on port 8080
  (*Docker image*). No launch profile is committed, so `dotnet run` and `dotnet
  Censio.Server.dll` both start in the `Production` environment on that address.
- Another address is set with the standard ASP.NET Core settings (`--urls`, `ASPNETCORE_URLS`,
  `ASPNETCORE_HTTP_PORTS`); the server has no setting of its own for it.

### Start page

- `GET /` answers `200` with an HTML page titled `Censio`, with the heading `Censio` and the line
  `Version <version>`, in the interactive server render mode. The page loads the Blazor script under
  `_framework/blazor.web` (a fingerprinted name is possible), which the server serves.
- `<version>` is the informational version the server was built with: `0.0.0` without a version, otherwise the
  build property `Version` exactly as passed (`-p:Version=<v>`), without a source-revision suffix.

### Health check

- `GET /health` answers `200`, content type `text/plain`, body exactly `Healthy`, in every environment. It checks
  no dependency and reveals nothing else (no version, no check results, no exception text).

### Plain HTTP and unknown paths

- The server speaks plain HTTP only. It adds no redirect to HTTPS and no `Strict-Transport-Security` header;
  TLS is the job of a reverse proxy in front of it (`SECURITY.md`).
- A request for an unknown path answers `404` with an empty body. In `Production` an unhandled error answers
  `500` with an empty body; the server has no exception page, status-code page or error handler of its own.
- `/`, `/health` and the Blazor hub need no login. Antiforgery protection is on.
- Host filtering is off (`AllowedHosts` is `*`), a deliberate choice (record 0006).

### Data directory and key ring

- The setting `Censio:DataDirectory` (environment variable `CENSIO__DATADIRECTORY`) names the data directory. It must
  be an absolute path. Unset, the server sets no key repository of its own: ASP.NET Core's default applies
  (outside the image, the user's home directory) and the server creates nothing and logs nothing about it.
- With the setting, ASP.NET Core's data-protection key ring lies in `<data directory>/data-protection-keys`. The
  server creates the key directory and the first key at start, before it serves a request. A second start on the
  same directory reads the existing ring and creates no further key.
- When the server creates the key directory on Linux, its mode is `0700`; an existing directory keeps its mode.
  ASP.NET Core creates the key files owner-only itself. The keys lie unencrypted; ASP.NET Core logs its warning
  `No XML encryptor configured` when it creates the first key.
- A value that is not an absolute path (including the empty string) stops the start: the server logs one `Critical`
  entry `The data directory (Censio:DataDirectory) must be an absolute path, not '<value>'; the server stops.`,
  creates no directory and exits with code 1 before it listens.
- A data directory where the key directory cannot be created or written stops the start: the server logs one
  `Critical` entry `The data-protection key ring cannot be stored in '<key directory>'; the server stops. Make the data directory writable for the user the server runs as.`
  with the exception and exits with code 1 before it listens. A refused write is reported by ASP.NET Core as a
  `CryptographicException` wrapping the I/O exception, and ASP.NET Core logs its own `Error` entry before the
  `Critical` one. Any other exception is not caught.

### Docker image

- The image `networlddev/censio` is built from `src/Censio.Server/Dockerfile`, for linux/amd64 only. Build stage:
  `mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24-amd64`; runtime stage:
  `mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24-amd64`; both are pinned by their amd64 manifest digest. It
  contains only the published server.
- The image sets `ASPNETCORE_HTTP_PORTS=8080` and exposes 8080: the server listens on all interfaces of the
  container, on plain HTTP. Which host addresses can reach it is decided by the `-p` mapping
  (`-p 127.0.0.1:8080:8080` for the host alone). The port is changed only through `ASPNETCORE_HTTP_PORTS`;
  `ASPNETCORE_URLS` is not supported for the image.
- The container runs as user `1654:1654` and never starts as root. `--user <uid>:<gid>` runs it as another
  non-root user; the application files are readable by every UID.
- `/config` exists in the image, owned by 1654, and is the data directory: the image sets
  `CENSIO__DATADIRECTORY=/config`, so the key ring lies in `/config/data-protection-keys` (*Data directory and key
  ring*). The image declares no `VOLUME`. The setting can be pointed to another absolute path but not disabled
  (`docker run -e` cannot unset an image `ENV`, and an empty value stops the start). A directory the runtime user
  cannot write (a bind mount owned by another UID, a named volume combined with another `--user`) stops the
  container at start with exit code 1; it is made writable by starting with `--user <uid>:<gid>` of the
  directory's owner or by `chown -R 1654:1654 <dir>` on the host.
- The key ring survives a recreation of a container that keeps `/config`.
- Health check: the image probes `http://127.0.0.1:<port>/health` with BusyBox `wget`, where `<port>` is the first
  entry of `ASPNETCORE_HTTP_PORTS` (`8080;8081` probes 8080). Interval 10 s, timeout 3 s, start period 30 s,
  3 retries; the container reads `healthy` within 60 s of the start.
- The build argument `VERSION` (default `0.0.0`) sets the product version shown on the start page. A value that
  is not a semantic version (`MAJOR.MINOR.PATCH`, optional pre-release and build parts) fails the build with
  `VERSION is not a semantic version: <value>`; no image is produced.
- The build fails, with a message naming `wwwroot/_framework/blazor.web*.js`, when the published output lacks the
  Blazor script.
- The runtime base runs with invariant globalization (no ICU) and has no time zone data: `TZ` has no effect and
  log times are UTC.
- Labels: `org.opencontainers.image.base.name` (the runtime tag), `…base.digest` (its manifest digest),
  `…description` and `…licenses` (`MIT`). The two base labels are literal copies of the runtime `FROM` line; the
  CI job `image` fails when they differ, so a base-image pull request from Dependabot needs the `base.digest`
  label (and `base.name`) set by hand to the new `FROM` line.
- The server logs to the console (`docker logs`).
- Published on Docker Hub as `networlddev/censio:<version>` for every release and as `latest`, which points to the
  promoted release of the highest version: a release whose tag is not the highest `vMAJOR.MINOR.PATCH` tag is
  refused and leaves `latest` where it was, and a re-run of only the publish jobs of such a run re-pushes its own
  `<version>`. GitHub's *Latest* mark is not protected: anyone with write access can set it by hand or by publishing
  an old tag's release as a full release. `<version>` and `latest` resolve to the same single linux/amd64 image manifest (no attestation index), the
  image that passed the release checks (the start page shows `Version <version>`, `/health` answers `Healthy`). A
  release creates no other tag, and an earlier release's tag stays unchanged unless its run is re-run.

## Limits and errors

- The server reads no request body and takes no input on `/` or `/health`.
- If the port is taken, the server ends with the ASP.NET Core startup error in the console; it has no handling of
  its own.
- A data directory that is not an absolute path, or a key directory the server cannot create or write, stops the
  start with exit code 1 and a `Critical` log line (*Data directory and key ring*).
- Request paths and hosts are not logged at the default levels (`Microsoft.AspNetCore` logs warnings only).

## Related decisions

- [`decisions/0005-product-version-from-one-build-property.md`](../decisions/0005-product-version-from-one-build-property.md) — the version comes from the build property `Version`.
- [`decisions/0006-server-plain-http-and-open-health.md`](../decisions/0006-server-plain-http-and-open-health.md) — plain HTTP, no web login, open `/health`, host filtering off.
- [`decisions/0007-server-web-assets-independent-of-restore-inputs.md`](../decisions/0007-server-web-assets-independent-of-restore-inputs.md) — the project requests the Blazor scripts itself, so the image contains them.
- [`decisions/0009-server-image-runs-as-a-fixed-non-root-user.md`](../decisions/0009-server-image-runs-as-a-fixed-non-root-user.md) — user 1654, other UIDs through `--user`, no `VOLUME`.
- [`decisions/0010-server-image-base-pinned-by-amd64-manifest-digest.md`](../decisions/0010-server-image-base-pinned-by-amd64-manifest-digest.md) — full-version amd64 base tags pinned by digest, the `base.digest` label checked in CI.
- [`decisions/0011-data-protection-key-ring-in-the-data-directory.md`](../decisions/0011-data-protection-key-ring-in-the-data-directory.md) — the key ring lies in `<data directory>/data-protection-keys`; the server stops when it cannot write it.
- [`decisions/0012-release-pre-release-first-and-secret-free-builds.md`](../decisions/0012-release-pre-release-first-and-secret-free-builds.md) — releases run as pre-release first, artifacts are built without secrets and `latest` is set by digest at the promotion.
- [`decisions/0026-server-image-linux-amd64-on-docker-hub-no-compose.md`](../decisions/0026-server-image-linux-amd64-on-docker-hub-no-compose.md) — linux/amd64 only, on Docker Hub, no compose file.

## Not here

- Data ingest from the Companion (later issue).
- Web layout and theming (#31).
