# Plan: Docker image for the server

Source: Issue #4 | [spec.md](spec.md)
Status: Draft
Tier: security — the image changes the server's network exposure (inside the container the listener binds all interfaces on 8080 instead of loopback) and adds a CI job that builds pull request code.
Security areas touched: Network exposure (`src/Censio.Server/Dockerfile`: `ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`, health probe); Docker and build configuration (`src/Censio.Server/Dockerfile`, `.dockerignore`, two new base images as dependencies); CI workflow (`.github/workflows/ci.yml`, new job `image`, and its script `.github/scripts/image-check.sh`); the *Image build check* command in `.squad/stack.md`

## Problem / root cause

Feature: see [spec.md](spec.md) (*Problem*). The change holds no production or test code: a Dockerfile, an
ignore file, a CI job with its script, and documentation.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine-amd64` is a single manifest `sha256:7e3205ef1189ab2b63a02040e02638a62bd70c67ac3a3a70a5a908be8e5da96c`; `sdk:10.0-alpine-amd64` is `sha256:60a079c9d3ffc98b2968c3153b995ede679f7d6504e3ae7d2b68225a7fd84f5a`; the index digests of `10.0-alpine` differ (`254fe017…`, `cc02b814…`) | `HEAD /v2/dotnet/{aspnet,sdk}/manifests/<tag>` on `mcr.microsoft.com`, 2026-10-31; the Dev takes the digests current at step 6 | confirmed |
| 2 | The runtime base sets `APP_UID=1654`, `ASPNETCORE_HTTP_PORTS=8080`, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, no `User`, `Cmd ["/bin/sh"]`, architecture amd64 | config blob `sha256:5308c8ca…` of fact 1 | confirmed |
| 3 | User `app` is `1654:1654` with home `/home/app` (owned 1654); `nobody` is 65534 | `etc/passwd` and `home/app` in layer `sha256:83301d18…` | confirmed |
| 4 | The runtime base has BusyBox `wget` (`/usr/bin/wget`), no `curl`, no `/usr/share/zoneinfo` | file list of the rootfs layer `sha256:7b229517…`; the `apk add` in the history installs no curl or tzdata | confirmed |
| 5 | `Program.cs` calls `app.UseAntiforgery()` (line 9) and binds no option; `appsettings.json` holds `Logging` and `AllowedHosts` only | `src/Censio.Server/Program.cs`, `appsettings.json` | confirmed |
| 6 | `Censio.Server.csproj` sets `RequiresAspNetWebAssets` (line 5) and references `Censio.Contracts` and `Censio.Server.Data`; `Directory.Build.props`, `Directory.Packages.props` and `global.json` (SDK `10.0.100`, `latestFeature`) are the build files at the root | the files | confirmed |
| 7 | `ci.yml` has one job `build`; workflow permissions `contents: read`, `checks: write`; triggers push/pull_request on `main` | `.github/workflows/ci.yml` lines 3–11 | confirmed |
| 8 | Dependabot has a `docker` entry for `/src/Censio.Server` | `.github/dependabot.yml` lines 26–36 | confirmed |
| 9 | Dependabot updates a `FROM <image>:10.0-alpine-amd64@sha256:…` line (tag with that suffix plus digest) and leaves a `LABEL` alone | to verify: Dev reads the Dockerfile parser and updater of `dependabot-core` (`docker/lib/dependabot/docker/`) in step 6 and reports it; the CI label check of AC9 fails closed either way | unverified |
| 10 | No record is released (no tag) | `git ls-remote --tags origin` prints nothing | confirmed |
| 11 | No `.dockerignore` exists; with `-f src/Censio.Server/Dockerfile` BuildKit reads `src/Censio.Server/Dockerfile.dockerignore` first, else `.dockerignore` at the context root | to verify: Dev, CI job case AC1-dirty (the file lists of the clean and the dirty build are equal, and no `bin/`/`obj/` lies in the build stage) | unverified |
| 12 | `dotnet` as PID 1 (exec-form `ENTRYPOINT`) stops on SIGTERM with exit code 0 within Docker's 10 s | to verify: Dev, CI job case AC11 | unverified |
| 13 | `-p:Version="1.0;touch /pwned"` fails the publish (MSBuild splits at `;`, `touch /pwned` is no property) | to verify: Dev, CI job case AC7-injection | unverified |
| 14 | Under `--user 99:100` Docker sets `HOME=/`; ASP.NET Core then keeps the key ring in memory and logs a warning; the server still answers | to verify: Dev, CI job case AC13 (the log is printed in the job) | unverified |
| 15 | `ubuntu-latest` has Docker with BuildKit as default builder and passwordless `sudo` (for `chown 99:100`) | to verify: Dev, first CI run of the job | unverified |
| 16 | The session has a Docker client (29.9.2) without a daemon, and `hadolint`, `shellcheck`, `actionlint` | `docker version`, `which` | confirmed |

## Acceptance criteria

The spec's AC1–AC13.

## Tests per criterion

No unit test: the change holds no production or test code (*Changes without production or test code*); every
criterion is under *Verification without tests*. Steps 4 and 5 and the *Coverage gate* are not applicable.

## Verification without tests

All container cases run in `.github/scripts/image-check.sh`, called by the CI job `image` on every pull request
(*Criteria verified only in CI*); each case prints its name and fails the job with a message naming the case.
The Dev runs the *Image build check* reproduction and the *Analyzer gate* (`hadolint`, `shellcheck`,
`actionlint`) before the review; the script's local smoke substitute is `bash -n` and `shellcheck`.

| AC | Verification | Where / owner |
| -- | ------------ | ------------- |
| AC1 | clean: `docker build` right after checkout; dirty: after `dotnet build Censio.slnx -c Release` and `dotnet publish src/Censio.Server/Censio.Server.csproj -c Release -o publish` on the runner, `docker build --no-cache`; both succeed, `docker image inspect` gives `amd64`/`linux`, and `find /app -type f` lists equal file sets | CI case AC1 / Dev |
| AC2 | container as in S2 (named volume): `curl` `/health` → `200`, body `Healthy`; `/` → `200`, contains `Version 0.0.0` and a `_framework/blazor.web` script path, that path → `200`; log has `Now listening on:` with `8080` | CI case AC2 |
| AC3 | `docker exec id -u` → `1654`; `stat -c %u /proc/1` → `1654`; `Config.User` is `1654` | CI case AC3 |
| AC4 | `docker exec censio touch /config/check` succeeds on a new named volume | CI case AC4 |
| AC5 | `Config.Healthcheck` has interval 10 s, timeout 3 s, start period 30 s, retries 3; `State.Health.Status` turns `healthy` within 60 s (poll every 2 s) | CI case AC5 |
| AC6 | `Config.Env` holds `ASPNETCORE_HTTP_PORTS=8080`, `Config.ExposedPorts` holds `8080/tcp`; with `-e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090` `/health` → `200` on 9090 and `healthy` within 60 s; with `8080;8081` `healthy` within 60 s | CI case AC6 |
| AC7 | build `--build-arg VERSION=1.2.3` → `/` contains `Version 1.2.3`; build `--build-arg "VERSION=1.0;touch /pwned"`: the build fails, or (if it succeeds) `test -e /pwned` is false in the runtime image and in the stage `build` (`--target build`) | CI case AC7 |
| AC8 | `hadolint src/Censio.Server/Dockerfile` reports nothing | *Analyzer gate* / Dev, step 7 |
| AC9 | `Config.Labels` holds the four labels, `licenses` = `MIT`, `base.name` and `base.digest` equal to the tag and digest parsed from the runtime `FROM` line of the Dockerfile; a mismatch fails the job with both values | CI case AC9 |
| AC10 | a copy of the checkout in `$RUNNER_TEMP` with the `RequiresAspNetWebAssets` line deleted from `Censio.Server.csproj`: `docker build` fails and its output contains `wwwroot/_framework/blazor.web*.js`; the same copy run through the *Image build check* reproduction fails with the same message | CI case AC10; reproduction locally / Dev before the review |
| AC11 | `docker stop` of the AC2 container returns within 10 s, `State.ExitCode` is `0` | CI case AC11 |
| AC12 | read against the documentation updates and the spec; the dry run below | Reviewer, step 8; Product Manager, dry run |
| AC13 | `sudo install -d -o 99 -g 100 "$RUNNER_TEMP/nas"`, `docker run --user 99:100 -v "$RUNNER_TEMP/nas:/config"`: `id -u` → `99`, `/health` → `200`, `touch /config/check` succeeds; `Config.Entrypoint` is `["dotnet","Censio.Server.dll"]` and the image has no `*.sh` under `/app`; the container log is printed | CI case AC13 |

**Dry run** (*Operator environments*, `.squad/routing.md`), scheduled by the orchestrator after the CI job is
green and before the Reviewer's round in step 8: the Product Manager pastes the README's commands
(a) on Docker Desktop for Windows (Docker Engine 23 or later, WSL 2 backend, Linux containers) in Windows
PowerShell 5.1 and in PowerShell 7, and (b) on an Unraid (6.12 or later) or Synology (DSM 7.2, Container
Manager) host with a share of its own UID, including the `docker save`/`docker load` transfer; without a NAS,
the stand-in is the CI case AC13. The result goes into `log.md`; a statement no run has shown is marked so in
the documentation.

**Documented commands** (*Shell procedures*): every command is one line without variables, and each depends
only on the previous one having succeeded. (a) All lines pasted at once: a failed `docker build` is followed
by `docker run`, which fails with Docker's "Unable to find image" and "pull access denied"; no container
starts. (b) One line at a time: the intended path. (c) A line fails: the next fails as in (a); a re-run of
`docker run` fails with "name is already in use", and the documentation gives `docker rm -f censio` first.
(d) A placeholder left unreplaced (`<uid>:<gid>`, `<dir>`): bash and PowerShell both reject the `<` before
running Docker. The documentation says to run the commands one by one. Cases (a)–(d) are covered by the dry
run; CI runs the same commands (AC1, AC2, AC13), and the Reviewer checks that the README's commands equal the
script's.

## Approach

**Dockerfile** `src/Censio.Server/Dockerfile`, two stages:

- `build`: `FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine-amd64@sha256:<digest> AS build`, `WORKDIR /src`;
  `COPY` one line each: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and the three
  `.csproj` files of `Censio.Server`, `Censio.Contracts`, `Censio.Server.Data` to their own paths;
  `RUN dotnet restore src/Censio.Server/Censio.Server.csproj`; `COPY src/ src/`; `ARG VERSION=0.0.0`;
  `RUN dotnet publish src/Censio.Server/Censio.Server.csproj -c Release --no-restore -o /app/publish
  "-p:Version=${VERSION}"` (the value is quoted and expanded once, never evaluated); then a `RUN` that fails
  unless a file matches `/app/publish/wwwroot/_framework/blazor.web*.js`, with the message
  `publish output lacks wwwroot/_framework/blazor.web*.js (see docs/decisions/0007)`.
- runtime: `FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine-amd64@sha256:<digest>`; `LABEL` with
  `org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0-alpine-amd64"`,
  `org.opencontainers.image.base.digest="sha256:<the same digest>"`, `org.opencontainers.image.description`
  ("Censio server: live state and history of Anno 117 statistics"), `org.opencontainers.image.licenses="MIT"`;
  `ENV ASPNETCORE_HTTP_PORTS=8080`; `WORKDIR /app`; `COPY --from=build /app/publish .` (root-owned, readable by
  every UID); `RUN install -d -o 1654 -g 1654 -m 0755 /config`; `EXPOSE 8080`; `USER 1654:1654`;
  `HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=3 CMD wget -q -T 2 -O /dev/null
  "http://127.0.0.1:${ASPNETCORE_HTTP_PORTS%%;*}/health" || exit 1` (shell form: BusyBox `sh` expands the first
  port at probe time); `ENTRYPOINT ["dotnet", "Censio.Server.dll"]`. No `VOLUME`, no script.

**`.dockerignore`** at the repository root (fact 11): `**/bin/`, `**/obj/`, `**/publish/`, `**/TestResults/`,
`.git/`, `.github/`, `.claude/`, `.squad/`, `specs/`, `tests/`, `docs/`, `*.md`. No `Dockerfile.dockerignore`.

**CI job** `image` in `.github/workflows/ci.yml`, beside `build` (no `needs`): `runs-on: ubuntu-latest`,
job-level `permissions: contents: read`, no secret, no `env` from secrets; steps: the pinned `actions/checkout`
of `build` with `persist-credentials: false`, the pinned `actions/setup-dotnet` (`10.0.x`), then
`bash .github/scripts/image-check.sh`. No new action, no push, no registry login.

**Script** `.github/scripts/image-check.sh`: `set -euo pipefail`; a `trap` removes its containers, volumes
and images; cases in the order AC1-clean, AC9, AC6-config, AC5-config, AC2, AC3, AC4, AC5, AC11, AC6, AC13,
AC7, AC10, AC1-dirty (the dirty build last, after `dotnet build`/`publish`). Polls are bounded (60 s); every
failure prints `image-check: <case>: <expected> / <actual>` and exits 1. It reads the digest and tag from the
Dockerfile's runtime `FROM` line, so the label check needs no second copy.

**Image build check** in `.squad/stack.md`: the first copy and the restore follow the Dockerfile (the three
projects and the root build files, `dotnet restore src/Censio.Server/Censio.Server.csproj`), and the last line
becomes the same glob check with the same message as the Dockerfile.

Layout of the README and user-guide section: `## Docker` (English), then `## Docker (Deutsch)` with the same
content; subsections in order *Build*, *Run*, *Port*, *Data directory and NAS hosts*, *Network and TLS*,
*Limits*; each command in its own fenced `text` block of one line. Otherwise layout free for the Dev, approved
in step 9.

## Options considered

1. **User model** — fixed non-root `USER 1654` with `--user`/`chown` for NAS (chosen); a PUID/PGID entrypoint
   that drops root (lost: starts as root, against the issue); `VOLUME /config` (lost: anonymous volumes). Record
   0009.
2. **Base pinning and `base.digest`** — `-amd64` tags by manifest digest, literal label, CI check (chosen); an
   `ARG` (lost: Dependabot stops); labels from #5/#6 (lost: the issue wants them in the Dockerfile); the index
   digest (lost: platform follows the host); the `-extra` variant (lost: size without need). Record 0010.
3. **Restore scope** — the server project (chosen: the image needs only it, and the Companion's `win-x64`
   restore stays out); the solution, as the reproduction did (lost: copies every test and Companion project
   into the image build for nothing).
4. **Container checks** — one script called by the job (chosen: `shellcheck` covers it, readable failures);
   inline workflow steps (lost: fifteen cases in YAML); a third-party test action (lost: a new dependency of
   the build's trust).
5. **Probe port** — first entry of `ASPNETCORE_HTTP_PORTS` at probe time (chosen); a fixed 8080 (lost: S6
   turns unhealthy); support for `ASPNETCORE_URLS` (lost: parsing URLs in `sh` for a setting the image does not
   document).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `src/Censio.Server/Dockerfile` | new |
| — | `.dockerignore` | new |
| — | `.github/workflows/ci.yml` | new job `image` |
| — | `.github/scripts/image-check.sh` | new |
| — | `.squad/stack.md` | *Image build check* |

## Signatures (for the Dev's skeleton)

none — no C# member changes.

## Test files

none — no test project changes (*Verification without tests*).

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: `src/Censio.Server/Dockerfile`, `.dockerignore`,
`.github/workflows/ci.yml`, `.github/scripts/image-check.sh` — no C#, outside `COVERAGE_PATHSPECS`; verified
by the CI job and the *Analyzer gate*. No coverage exclusion is needed.

## Areas

`server`: a new section *Docker image*; scope and *Not here* change (below). No new area.

## Documentation updates

All owned by the Dev, written once the job is green, against the Dockerfile as built:

- `docs/areas/server.md`: *Scope* adds the Docker image; new section *Docker image* — linux/amd64, the two
  base tags, listener on all interfaces of the container on 8080, the port changed only through
  `ASPNETCORE_HTTP_PORTS` (first entry probed, `ASPNETCORE_URLS` not supported), `EXPOSE 8080`, user 1654 and
  `--user` for other UIDs, `/config` (owned 1654, no `VOLUME`, the two ownership remedies), the health check
  and its timings, the `VERSION` build argument (default `0.0.0`), the build failing without the Blazor script,
  data-protection keys (S11), invariant globalization, no tzdata (UTC log times, `TZ` without effect), the
  labels; *Listen address* says the loopback default holds outside the image; *Not here* drops the Docker
  image; *Related decisions* adds 0009 and 0010.
- `docs/areas/README.md`: the server row's scope adds the Docker image, *Not here* drops it.
- `SECURITY.md` (*Deployment Security Considerations*): "without address configuration it listens on loopback
  only" gains "outside the Docker image; the image listens on all interfaces of the container on 8080, and the
  `-p` mapping decides who can reach it (`-p 127.0.0.1:8080:8080` for the host alone)".
- `docs/ARCHITECTURE.md` (*Deployment*): image `networlddev/censio`, linux/amd64, built from
  `src/Censio.Server/Dockerfile` on the two Alpine bases, non-root; links `areas/server.md`, records 0009 and
  0010; *Security model* paragraph links 0006 for the image's listener.
- `README.md` and `docs/USER_GUIDE.md` (new, Docker section only, *Approach* layout): build, run with named
  volume, `/health`, port mapping and E3, `-p 127.0.0.1:…`, `docker rm -f censio` before a re-run, NAS:
  build there or `docker save -o censio.tar censio` / `docker load -i censio.tar`, the `--user` command of
  S11 and `chown -R 1654:1654 <dir>`, Docker Desktop: named volume, all interfaces and TLS at a reverse proxy,
  `ASPNETCORE_URLS` not supported, the limits (amd64 only, UTC, data-protection keys).
- `docs/CONTRIBUTING.md` (*Automated checks*): a bullet for the job `image` (what it builds and checks, that it
  pushes nothing); the Dependabot bullet: a base-image pull request needs the `base.digest` label set by hand.
- `.squad/stack.md`: *Image build check* as in *Approach*; the *Commands* row unchanged.

Sweep: `docs/`, `SECURITY.md`, `.squad/project.md` for "loopback", "localhost", "5000", "Docker", "only";
hits above. `CONTRIBUTING.md` line 35 (`dotnet run` on `localhost:5000`) stays true. `.squad/project.md`
(*Network exposure*) stays true unchanged.

## Architecture check

No guarantee in `.squad/project.md` (none recorded). Record 0006's loopback default is kept outside the image
and amended in place (unreleased, fact 10) for the image's deliberate all-interfaces listener. Record 0007 keeps
holding: the Dockerfile copies only project and build files before the restore, and its check is the fail-closed
gate 0007 names. Record 0005: the version enters only through `-p:Version`.

## Security considerations

- [x] No limits: the change parses no input.
- [x] No exception reaches a user: the build's own messages and the Blazor-check message above.
- The `VERSION` argument reaches the shell only as a quoted, once-expanded word (AC7).
- The job has `contents: read`, no secret and no push; it runs pull request code as `build` already does.
- The container never starts as root in any documented command; `--user 0` is not documented.
- Published files are root-owned and read-only for the runtime user; only `/config` is writable.

## Decision records

- `docs/decisions/0009-server-image-runs-as-a-fixed-non-root-user.md` (Proposed)
- `docs/decisions/0010-server-image-base-pinned-by-amd64-manifest-digest.md` (Proposed)
- `docs/decisions/0006-server-plain-http-and-open-health.md` (Accepted, unreleased): extended in place with the
  image's listener.

## Out of scope / follow-ups

As in the spec. A follow-up issue for the orchestrator: "[Server] Persist the data-protection key ring in
/config" — body: inside the image the key ring is lost on recreation (record 0009); move it with #29/#33.
