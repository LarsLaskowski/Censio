# Spec: Docker image for the server

Source: Issue #4 (with its comment of the solution-skeleton follow-up)
Status: Approved by Lead

## Problem

The server ships as the Docker image `networlddev/censio` (linux/amd64), but the repository has no Dockerfile:
an operator or external tester cannot build or run the server as a container, and the release (#5) and edge
(#6) workflows have no image to push. Two traps are known from the skeleton (#2): inside the official `aspnet`
base image the server does not keep its loopback-only default, and a Docker build that restores before the
sources are copied can lose the Blazor script (record 0007, Vandox#24).

## Scenarios

- S1: Given a checkout of the repository on a Docker host, when the operator runs
  `docker build -f src/Censio.Server/Dockerfile -t censio .` from the repository root, then the build ends
  successfully and leaves a linux/amd64 image `censio`.
- S2: Given that image, when the operator runs
  `docker run -d --name censio -p 8080:8080 -v censio-config:/config censio`, then the console log shows a
  `Now listening on:` line with port 8080, `GET http://<host>:8080/health` answers `200` with the body `Healthy`,
  and `GET http://<host>:8080/` shows the page `Censio` with `Version 0.0.0`, whose `_framework/blazor.web`
  script the server serves (`docs/areas/server.md`, *Start page*).
- S3: Given the running container, when the operator runs `docker exec censio id -u`, then it prints `1654`
  (the base image's `app` user): neither the server process nor `docker exec` runs as root.
- S4: Given the running container with a new named volume on `/config`, when a process of the runtime user
  creates a file in `/config`, then it succeeds; `/config` is the one place the server will keep its database
  and the uploaded catalog (#29, #42).
- S5: Given the running container, when the operator runs `docker ps`, then within 60 seconds of the start
  the container's status reads `(healthy)`, because the image probes `/health` itself on `127.0.0.1` (interval
  10 s, timeout 3 s, start period 30 s, 3 retries); a server that stops answering turns `(unhealthy)` after
  three failed probes.
- S6: Given the image, when the operator starts it with `-e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090`, then
  the server listens on 9090 instead and the container still turns `(healthy)`, because the probe uses the
  first port of `ASPNETCORE_HTTP_PORTS` (so `8080;8081` probes 8080); without that variable it listens on 8080
  because the image sets the port itself, not because the base image happens to. `ASPNETCORE_HTTP_PORTS` is
  the one documented way to change the port; `ASPNETCORE_URLS` is not supported for the image, and the
  documentation says so.
- S7: Given a build with `--build-arg VERSION=1.2.3`, when the operator opens `/`, then it shows `Version 1.2.3`
  (record 0005; the release #5 and edge #6 builds pass their version this way).
- S8: Given the Dockerfile, when `hadolint` checks it, then it reports no finding.
- S9: Given the image, when the operator inspects its labels, then it carries
  `org.opencontainers.image.base.name`, `org.opencontainers.image.base.digest` (the pinned runtime base),
  `org.opencontainers.image.description` and `org.opencontainers.image.licenses=MIT`.
- S10: Given a reader of `README.md` or `docs/USER_GUIDE.md`, when they follow its Docker section (English
  first, then German), then they can build and start the image and reach `/health`; the section says that
  inside the image the server listens on all interfaces of the container, that the `-p` mapping decides who
  can reach it (on Linux a published port bypasses ufw and firewalld), and that TLS belongs in a reverse proxy
  (`SECURITY.md`). Every command is one line that runs
  unchanged in bash and in PowerShell (no `\` continuation, no `$(…)`).
- S11: Given a NAS share owned by another UID and GID (Unraid `nobody:users`, `99:100`; a Synology user such
  as `1026:100`), when the operator runs
  `docker run -d --name censio --user 99:100 -p 8080:8080 -v /mnt/user/appdata/censio:/config censio`, then
  the server starts as UID 99 (`docker exec censio id -u` prints `99`), `/health` answers `200`, and that
  user can create a file in `/config`: the image needs no root for any UID it is started with. The one
  write outside `/config` is ASP.NET Core's data-protection key ring (antiforgery, `app.UseAntiforgery()`):
  as UID 1654 it lands in `/home/app/.aspnet` in the container layer, under another UID (`HOME=/`) it stays in
  memory and the server logs ASP.NET Core's warning about it at start; in both cases the keys are lost when
  the container is recreated, and an open page needs a reload after a restart.

## Errors and edge cases

- E1: The publish output lacks `wwwroot/_framework/blazor.web*.js` (an SDK that drops the property of record
  0007, or a project change): `docker build` fails in the build stage with a message naming
  `wwwroot/_framework/blazor.web*.js`, and no image is produced.
- E2: The working tree holds local build output (`bin/`, `obj/`, `publish/`, `TestResults/`): the build gives
  the same result as from a clean checkout; no host build output reaches the build context's restore or
  publish.
- E3: The host port 8080 is taken: `docker run` fails with Docker's own "port is already allocated" message;
  the documentation shows mapping another host port (`-p 9000:8080`).
- E4: `/config` is a bind mount of a host directory the runtime user cannot write (a NAS share of another
  UID, a directory created by root), or a named volume combined with `--user` of another UID (the volume
  takes the image's `1654` ownership): the server still starts and answers `/health`, because it writes
  nothing to `/config` yet (#29 and #42 report a failed write); the documentation names the two remedies —
  start with `--user <uid>:<gid>` of the directory's owner (S11), or hand the directory to the image's user
  (`chown -R 1654:1654 <dir>`) — and says that `/config` on Docker Desktop for Windows is a named volume.
  The image ships no root entrypoint that changes ownership or drops privileges (Q1).
- E5: `VERSION` holds shell or MSBuild metacharacters (`--build-arg "VERSION=1.0;touch /pwned"`,
  `VERSION=1.0;Foo=bar`): the value is never run as a command, and a value that is not a semantic version
  (`MAJOR.MINOR.PATCH`, optional pre-release and build parts) fails the build before the publish with
  `VERSION is not a semantic version: <value>`; no image is produced.
- E6: The container is stopped (`docker stop`): the server shuts down within Docker's default stop timeout,
  without being killed.

## Non-functional requirements

- Platform: linux/amd64 only, guaranteed by the single-architecture tags, not by `FROM --platform` (hadolint
  DL3029): build stage `mcr.microsoft.com/dotnet/sdk:10.0.<patch>-alpine<X.Y>-amd64`, runtime stage
  `mcr.microsoft.com/dotnet/aspnet:10.0.<patch>-alpine<X.Y>-amd64`, both pinned by their linux/amd64 manifest
  digest (full-version tags of the issue's `10.0-alpine` images for one architecture, record 0010). `base.name` is that runtime tag and
  `base.digest` that manifest digest. The existing Dependabot `docker` entry for `/src/Censio.Server` updates
  both `FROM` lines (major updates ignored); it does not touch the `base.digest` label, so the image check of AC9 fails a Dependabot
  pull request until the label is set to the new digest by hand. The image declares no `VOLUME`: `/config`
  exists in the image, owned by 1654, and every documented command passes `-v`. Multi-stage, no
  docker-compose file.
- Runtime base: the plain `aspnet` Alpine image runs with invariant globalization (no ICU) and has no tzdata,
  so `TZ` (which Unraid passes by default) has no effect and log times are UTC; recorded in
  `docs/areas/server.md`.
- Security: the runtime user is not root, and the image runs under any non-root UID given with `--user`; the image contains only the published server, no SDK, sources or
  test assemblies. Inside the container the listener binds all interfaces on 8080, plain HTTP (record 0006);
  this is a security area (*Network exposure*, `.squad/project.md`) and changes the statements about the
  loopback default in `docs/areas/server.md` and `SECURITY.md` for the image.
- Configuration: no new setting. `ASPNETCORE_HTTP_PORTS` defaults to `8080` in the image. `appsettings.json`
  stays in the application directory and environment variables override its keys (`__` as separator, so
  `CENSIO__<Key>` sets `<Key>` of the section `Censio`); the options themselves are #33.
- Logs: the server logs to the console as today, read with `docker logs`.
- Verification: the session has a Docker client but no daemon, so the restore-order reproduction (*Image build
  check*, `.squad/stack.md`) runs before the review, extended by the check of E1; the criteria about a running
  container (S2–S7, S9, S11) are checked by a CI job that builds the image without pushing it and probes the
  container on every pull request. That job builds the solution on the runner first, so `bin/` and `obj/` with
  the runner's paths are in the build context for AC1. The documented steps get a dry run by the Product
  Manager before the documentation review (*Operator environments*, `.squad/routing.md`): Docker Desktop on
  Windows in PowerShell, and Unraid or Synology; where no NAS is at hand, the stand-in is Linux Docker Engine
  with a bind mount owned by `99:100`, which the CI job runs as AC13.
- Target host (Q1): Linux Docker Engine and Docker Desktop on Windows (Linux containers), Docker 23 or later
  with BuildKit, for building and running; Unraid and Synology NAS hosts for running, their shares owned by
  other UIDs (S11, E4); until #5 publishes the image, the documentation shows building it on the NAS or moving
  it there with `docker save` and `docker load`. The build needs access to `mcr.microsoft.com`. The container starts as non-root in
  every documented command; root (`--user 0`) is never documented.

## Acceptance criteria

- [ ] AC1: `docker build -f src/Censio.Server/Dockerfile .` from the repository root succeeds and produces a
  linux/amd64 image, from a clean checkout and from a working tree after `dotnet build` and `dotnet publish`
  on the host; the plan names the ignore file and where BuildKit reads it with `-f` (S1, E2).
- [ ] AC2: A container started as in S2 answers `GET /health` on port 8080 with `200` and body `Healthy`, and `GET /`
  answers `200` with `Version 0.0.0` and a `_framework/blazor.web` script reference that itself answers `200`
  (S2).
- [ ] AC3: In the container, `id -u` prints `1654` and the server process runs as UID 1654 (S3).
- [ ] AC4: The runtime user can create a file in `/config` of a new named volume (S4).
- [ ] AC5: The image declares a health check on `/health` with the timings of S5; `docker inspect` reports the
  container `healthy` within 60 seconds of the start (S5).
- [ ] AC6: The image sets `ASPNETCORE_HTTP_PORTS=8080` and exposes 8080 itself; started with
  `ASPNETCORE_HTTP_PORTS=9090` the server answers `/health` on 9090 and `docker inspect` reports it `healthy`
  within 60 seconds; started with `ASPNETCORE_HTTP_PORTS=8080;8081` it reports `healthy` too (S6).
- [ ] AC7: Built with `--build-arg VERSION=1.2.3` the start page shows `Version 1.2.3`; built with
  `VERSION=1.0;touch /pwned` or `VERSION=1.0;Foo=bar` the build fails with `VERSION is not a semantic version`,
  and no `/pwned` exists (S7, E5).
- [ ] AC8: `hadolint` reports no finding on the Dockerfile (S8).
- [ ] AC9: `docker inspect` shows the four labels of S9 with `licenses` `MIT`, `base.name` equal to the runtime
  tag and `base.digest` equal to the digest the runtime `FROM` line pins; a check in CI fails when the label
  and the `FROM` digest differ (S9, NFR *Platform*).
- [ ] AC10: Built from a temporary copy whose `Censio.Server.csproj` lacks `RequiresAspNetWebAssets` (record
  0007), so that no file matches `wwwroot/_framework/blazor.web*.js`, the image build fails with a message naming
  that glob; the check matches the glob, not one file name, and the *Image build check* reproduction in
  `.squad/stack.md` fails the same way (E1).
- [ ] AC11: `docker stop` ends the container with exit code 0 within the default timeout (E6).
- [ ] AC12: `README.md` and `docs/USER_GUIDE.md` each hold a Docker section, English first and then German with
  the same content, with the build and run commands of S1/S2 as single lines valid in bash and PowerShell, the
  port mapping (E3), the named volume as the default, the transfer to a NAS host (NFR *Target host*), the NAS
  run command with `--user` of S11 and the two
  ownership remedies of E4, the all-interfaces note and TLS at the reverse proxy (S10); `docs/areas/server.md`
  and `SECURITY.md` state that inside the image the server listens on all interfaces on 8080 (S6, S10).
- [ ] AC13: A container started with `--user 99:100` and a bind mount on `/config` owned by `99:100` reports
  `id -u` `99`, answers `GET /health` with `200`, and its process can create a file in `/config`; the image
  has no `ENTRYPOINT` or script that runs as root (S11, E4).

## Out of scope

- Pushing the image, Docker Hub credentials, tags and the version from the release tag: #5 and #6.
- The configuration reference, the database and catalog files in `/config` and data surviving container
  recreation: #29, #33, #42; persisting the data-protection key ring in `/config` goes with them (S11).
- Other platforms than linux/amd64 (arm64 hosts, including ARM-based Synology models; Windows containers),
  docker-compose, and NAS-specific templates (an Unraid Community Applications template, a Synology project).
- The rest of `README.md` and the user guide (what Censio is, status, the Windows ZIP): #7.
- E3 needs no criterion of its own beyond its documentation in AC12: the message is Docker's.

## Open questions

- Q1: What do the target hosts have and allow — the Docker host type (Linux Docker Engine, Docker Desktop on
  Windows, a NAS such as Unraid or Synology whose shares belong to other UIDs), Docker version and BuildKit,
  access to `mcr.microsoft.com`, the console the operator pastes into (bash or PowerShell), and whether
  `/config` is a named volume or a bind mount the operator must hand to UID 1654?
  Answered: Product Manager, option (b) — Linux Docker Engine and Docker Desktop on Windows, Docker 23+ with
  BuildKit, and NAS hosts (Unraid, Synology) whose shares belong to other UIDs. Reconciled with the issue's
  non-root requirement by keeping the image non-root (`USER 1654`) and serving the NAS case with Docker's own
  `--user <uid>:<gid>` (S11) or a `chown` of the directory (E4). A PUID/PGID entrypoint was rejected: it
  starts the container as root and drops privileges, which breaks the issue's criterion "the container starts
  as non-root". Docker Desktop on Windows implies PowerShell, so the documented commands are single lines
  valid in both consoles (S10); `mcr.microsoft.com` access is a build prerequisite (NFR *Target host*).
- Q2: `README.md` and `docs/USER_GUIDE.md` do not exist yet; does this feature create them?
  Answered: yes, with the Docker section only — issue #4 (*Documentation*) asks for it, and #7 completes both files.
- Q3: Which `CENSIO__…` variables exist? Answered: none — `Program.cs` binds no option and `appsettings.json`
  holds only `Logging` and `AllowedHosts`; the default host already reads environment variables, so the image adds
  nothing for them, and #33 documents the options.
- Q4: What does the base image already provide? Answered: the registry configuration of
  `mcr.microsoft.com/dotnet/aspnet:10.0-alpine-amd64` (manifest `sha256:7e3205ef1189ab2b63a02040e02638a62bd70c67ac3a3a70a5a908be8e5da96c`,
  read 2026-10-31) sets `ASPNETCORE_HTTP_PORTS=8080` and `APP_UID=1654` and no `USER`, so the image runs as root
  unless the Dockerfile switches the user, and binds all interfaces unless it changes the port setting.
- Q5: Does the image probe its own health? Answered: yes — `docs/areas/server.md` (*Scope*) and record 0006
  name the Docker health probe as a user of `/health`.
- Q6: Does the image take a version? Answered: yes — record 0005 makes the build property `Version` the one
  source, and #5 requires the pushed image to show the release version; the default stays `0.0.0`.
- Q7: Does any workflow build an image today? Answered: no — `.github/workflows/ci.yml` builds and tests the
  solution only, so the container criteria get their own CI job (NFR *Verification*); pushing stays with #5/#6.

## Area documents

- `docs/areas/server.md`: a section *Docker image* — port 8080 on all interfaces of the container and how to
  change it, the non-root user 1654 and `--user` for other UIDs, `/config` as the data directory and its two ownership
  remedies, the health check,
  the version build argument, linux/amd64; *Listen address* says the loopback default holds outside the image
  only; *Not here* and its index row in `docs/areas/README.md` drop "Docker image".
  The section also states the probe and its timings, that only `ASPNETCORE_HTTP_PORTS` changes the port,
  where the data-protection keys go (S11), invariant globalization, no tzdata (UTC log times, `TZ` without
  effect), no `VOLUME`, and the by-hand `base.digest` fix of a Dependabot pull request.
- `SECURITY.md` (*Deployment Security Considerations*): the loopback sentence qualified for the image.
- `docs/ARCHITECTURE.md` (*Deployment*): the image name, platform and the two base images, linked to the area.

## Challenge

- M1 (probe port vs S6): accepted — S6 and AC6 require `(healthy)` with `9090` and with `8080;8081`; the probe
  takes the first port of `ASPNETCORE_HTTP_PORTS` on `127.0.0.1`; `ASPNETCORE_URLS` is documented as not
  supported. The probe tool (BusyBox `wget`, as the image has no `curl`) is the plan's choice to verify.
- M2 (`base.digest` vs Dependabot): accepted, option (i) — the issue puts the labels in the Dockerfile, which
  rules out (ii); the label stays a literal, a CI check fails on a mismatch (AC9), and the by-hand fix of a
  Dependabot pull request is documented (NFR *Platform*). The plan records the choice in a decision record.
- m1 (which digest): accepted — single-architecture `-amd64` tags pinned by their manifest digest, both
  stages, no `FROM --platform`; after Security's plan review (B1) full-version tags (`10.0.<patch>-alpine<X.Y>-amd64`)
  so patches arrive as tag updates (NFR *Platform*, record 0010).
- m2 (data-protection keys): accepted — S11 says where the keys go, that they are lost on recreation and that
  the warning under `--user` is expected; persisting them is out of scope with #29/#33/#42.
- m3 (health-check timings): accepted — interval 10 s, timeout 3 s, start period 30 s, 3 retries, healthy
  within 60 s (S5, AC5).
- m4 (dirty tree in CI): accepted — the CI job builds and publishes on the runner first (NFR *Verification*,
  AC1); the ignore file and its location are named by the plan, since the spec states the effect only.
- m5 (AC10 negative state): accepted — produced from a temporary copy without `RequiresAspNetWebAssets`;
  the check matches the glob.
- m6 (docs verified only on bash): accepted — dry run by the Product Manager on Docker Desktop for Windows in
  PowerShell and on Unraid or Synology, stand-in Linux Docker Engine with a `99:100` bind mount (NFR
  *Verification*); the plan schedules it.
- m7 (`VOLUME`): decided — no `VOLUME`, `/config` exists owned by 1654 and every documented command passes
  `-v` (NFR *Platform*); this avoids an anonymous volume on every run without `-v`.
- m8 (globalization, tzdata): accepted — the plain image stays (smaller, no ICU need today); both limits are
  stated in the NFR *Runtime base* and the area document. The plan verifies them against the image
  configuration.
