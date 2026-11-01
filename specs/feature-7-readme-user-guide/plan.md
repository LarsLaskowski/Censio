# Plan: README and user guide from the start (English, then German)

Source: Issue #7 | [spec.md](spec.md)
Status: Approved
Tier: trivial — documentation that needs a decision record (0013) and touches an area document, with no change to behavior, production or test code.
Security areas touched: none

## Problem / root cause

Feature: see [spec.md](spec.md) (*Problem*).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | No release, no `v*` tag and no Docker Hub tag exist yet | `gh api repos/LarsLaskowski/Censio/releases` returns `[]`; `git ls-remote --tags origin` prints nothing; `hub.docker.com/v2/repositories/networlddev/censio/tags` returns `count` 0 | confirmed |
| 2 | Edge builds do not exist: #6 is closed as not planned and `ci.yml` pushes no image and builds no edge ZIP | `gh api …/issues/6`: `state_reason` `not_planned`; `.github/workflows/ci.yml` jobs `build`, `image`, `release-check` — none logs in to Docker Hub or pushes | confirmed |
| 3 | The release build publishes the Companion with `-c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false`, and its output folder then holds only `Censio.Companion.exe` | `.github/scripts/release-check.sh` lines 213-216 (`companion`), lines 137-147 (`check_file_list` fails on any other file); CI job `release-check` runs it on Linux | confirmed |
| 4 | Without `-p:Version` the Companion and the server show `0.0.0` | `Directory.Build.props` line 7 `<Version>0.0.0</Version>`; `docs/areas/companion.md` and `server.md`; the Dockerfile's `VERSION` default `0.0.0` (`server.md`, *Docker image*) | confirmed |
| 5 | The Companion builds on Linux too (only running needs Windows) | `src/Censio.Companion/Censio.Companion.csproj` line 7 `EnableWindowsTargeting`; `docs/areas/companion.md` *Platform*; record 0004 | confirmed |
| 6 | The SDK required is .NET 10, `10.0.100` or a later feature band | `global.json`: `version` `10.0.100`, `rollForward` `latestFeature` | confirmed |
| 7 | The publish command of AC3 runs unchanged in Windows PowerShell 5.1 and PowerShell 7 (no argument holds a `.` after `-p:`, which 5.1 is known to split) | to verify: the Product Manager's dry run (step 6, before the review), PowerShell 7 at least; 5.1 stays marked "Not yet tried" unless the run uses it | unverified |
| 8 | The Companion stores no file and writes no log | `docs/areas/companion.md`, *Limits and errors* | confirmed |
| 9 | Settings for the configuration table: `Censio:DataDirectory` / `CENSIO__DATADIRECTORY` (unset outside the image, `/config` in it) and `ASPNETCORE_HTTP_PORTS` (`8080` in the image; outside it the server listens on `http://localhost:5000`); the Companion has none | `docs/areas/server.md`, *Listen address*, *Data directory and key ring*, *Docker image* | confirmed |
| 10 | GitHub's heading anchors: `# Censio (Deutsch)` → `#censio-deutsch`, `# Censio-Benutzerhandbuch` → `#censio-benutzerhandbuch` | to verify: the Dev opens both links on the branch's GitHub view (step 6); the Reviewer re-checks in step 8 | unverified |
| 11 | `docker run … censio` after a failed local build does not start a foreign image: with `--pull never` Docker refuses a missing image | to verify: the dry run runs the source command once without a local `censio` image and shows Docker's refusal, owner Product Manager, step 6 (basis: Docker CLI reference, `docker run --pull` with `always`, `missing` or `never`, since Docker 20.10) | unverified |
| 12 | `README.md` and `docs/USER_GUIDE.md` today hold the same sections; the guide lacks the description sentence; the German part keeps the headings `### Pull`, `### Run`, `### Port` | `diff README.md docs/USER_GUIDE.md`; Lead read of both files | confirmed |
| 13 | Stale references to edge builds: `docs/areas/server.md` line 124 (*Not here*), record 0005 *Consequences* | `grep -rn -i edge docs` (0005 fixed by the Lead in this plan; `server.md` listed under *Documentation updates*) | confirmed |

## Acceptance criteria

The spec's AC1–AC8.

## Tests per criterion

No production or test code changes; every criterion is verified as *Verification without tests* says.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | none — *Verification without tests* | none |
| AC2 | none — *Verification without tests* | none |
| AC3 | none — *Verification without tests* | none |
| AC4 | none — *Verification without tests* | none |
| AC5 | none — *Verification without tests* | none |
| AC6 | none — *Verification without tests* | none |
| AC7 | none — *Verification without tests* | none |
| AC8 | none — *Verification without tests* | none |

Documented shell procedures (*Operator environments and documented procedures*): every command block holds one
command, and the text says to run them one at a time (as today).

- (a) All lines pasted at once: each block is a single command, so a paste is one command; pasting several blocks
  runs them in order, and a failed `git clone` makes `cd Censio` and the build fail with "no such file" errors —
  nothing is installed.
- (b) One line at a time: the intended use; the dry run follows it.
- (c) One line fails: a failed `docker build` leaves no `censio` image, and the documented source run command carries
  `--pull never`, so it fails with Docker's missing-image error instead of pulling a foreign `censio` (fact 11); a
  failed `dotnet publish` leaves an incomplete output folder that the next run overwrites; nothing starts.
- (d) A variable is lost: the commands use no variables.
- No unit or CI test can hold these answers (documentation only); the dry run covers (b) and (c) for `docker build`
  where the Product Manager can provoke it (e.g. without network), otherwise those notes stay as written from the
  Docker reference.

## Verification without tests

Steps 4 (*Skeleton*), 5 (*Tests first*) and the *Coverage gate* are not applicable: the diff changes `README.md`,
`docs/USER_GUIDE.md`, `docs/areas/server.md` and records only.

- AC1, AC2, AC3, AC5, AC7: the Reviewer reads both files against the layout in *Approach* and the spec (step 8).
- AC4: the Product Manager's dry run, after the Dev's edits and **before** the review (step 6 exit): on a Windows 10
  22H2 or 11 x64 machine with the .NET 10 SDK (10.0.100 or later), `git`, PowerShell 7 (5.1 if at hand) and
  Docker 23+ (Docker Desktop with WSL 2 and Linux containers, or a Linux Docker Engine), using only `README.md` of
  the branch: clone, build and run the server image (`/health` answers `Healthy`, start page `Version 0.0.0`),
  publish and start the Companion (tray menu `Version 0.0.0`). The orchestrator logs the result; every step the
  run did not show is marked "Not yet tried" in both languages by the Dev before the review. The release steps stay
  marked "Not yet tried" (no release exists, fact 1).
- AC6: the Reviewer compares the English and German parts of each file section by section; the Dev confirms before
  handing back that the code blocks of the two parts of each file are byte-identical (extract the fenced blocks per
  part and `diff` them — a read-only command, in the scratch directory).
- AC8: the Dev resolves every relative link of each file from that file's folder (`test -e` per target) and opens the
  two language anchors on GitHub (fact 10); the Reviewer re-checks.
- The Reviewer confirms that the diff holds no production or test code.

## Approach

Record 0013: the README is the quick start, the guide the reference. Layout (fixed, not left to anyone):

`README.md`

```text
# Censio
<one sentence: what Censio is>
<line: German version → link to #censio-deutsch>
## Status            milestone M0 – Foundation, no release yet; bullets "Works today", "Not yet"
## Getting Censio    releases page; no edge builds; the state of main is built from source:
                     git clone https://github.com/LarsLaskowski/Censio.git, then cd Censio
                     (or the source ZIP: Code > Download ZIP), commands run in that folder
## Server (Docker)   opening paragraph (image, tags, Docker 23+, linux/amd64 only, release "Not yet tried");
                     pull, run, health, logs, start page, docker rm -f, port taken (-p 9000:8080),
                     loopback binding (-p 127.0.0.1:8080:8080) with the firewall note;
                     link to the user guide for data directory, NAS, TLS and limits
### Build the server from source   docker build (as today, optional VERSION);
                     run with --pull never and the local name censio
## Companion         opening paragraph (ZIP from Assets, Windows 10 22H2/11 x64, no .NET, release
                     "Not yet tried"); extract, start, tray Version; SmartScreen note
### Build the Companion from source   .NET 10 SDK on any OS, run on Windows only; one-line
                     dotnet publish src/Censio.Companion/Censio.Companion.csproj with the release flags
                     except -p:Version and -o publish; publish holds only Censio.Companion.exe;
                     tray shows Version 0.0.0; PowerShell 5.1 "Not yet tried" unless the dry run used it
## Configuration     table: Setting | Environment variable | Default | Meaning;
                     rows Censio:DataDirectory, ASPNETCORE_HTTP_PORTS; "The Companion has no settings yet."
## User guide        link docs/USER_GUIDE.md; vulnerabilities: link SECURITY.md
## Licence           MIT, link LICENSE
# Censio (Deutsch)
<same, headings: Status, Censio beziehen, Server (Docker), Server aus dem Quellcode bauen, Companion,
 Companion aus dem Quellcode bauen, Konfiguration, Benutzerhandbuch, Lizenz>
```

`docs/USER_GUIDE.md`

```text
# Censio user guide
<one sentence: what Censio is; status and quick start: link ../README.md>
<line: German version → link to #censio-benutzerhandbuch>
## Installation
### Server (Docker)   today's Docker text; #### Pull, Run, Port, Data directory and NAS hosts,
                      Network and TLS, Limits
### Companion         today's Companion text
### Build from source  the README's two source builds, same commands
## First start        server: start page Version, /health, docker ps (healthy), first key and its warning;
                      Companion: icon, tooltip Censio Companion, menu Version / Exit, no window,
                      a second start does nothing, SmartScreen once
## Data and logs      server: /config, data-protection-keys (private, unencrypted), console log via
                      docker logs censio, UTC; Companion: no data, no log yet
## Reporting problems  link ../.github/ISSUE_TEMPLATE/bug_report.md; include version (start page or tray),
                      the docker run command without secrets, host OS, docker logs excerpt;
                      vulnerabilities: link ../SECURITY.md, never a public issue
# Censio-Benutzerhandbuch
<same, headings: Installation, Server (Docker) with #### Image laden, Starten, Port, Datenverzeichnis und
 NAS-Hosts, Netzwerk und TLS, Einschränkungen; Companion; Aus dem Quellcode bauen; Erster Start;
 Daten und Logs; Probleme melden>
```

Every fact is taken from the area documents as they stand; release-dependent "Not yet tried" notes stand only in
the opening paragraph of each *Server (Docker)* and *Companion* section (S10). Wording is the Dev's within this
layout; no criterion fixes exact text beyond the commands, error texts quoted from `docs/areas/server.md` and the
SmartScreen labels of the spec.

## Options considered

Recorded in [0013](../../docs/decisions/0013-readme-quick-start-user-guide-reference.md): README as quick start
and guide as reference (chosen); both files with the full text; README linking to the guide for installation
(fails AC4); the CI artifact `companion-dry-run` for unreleased states (a `1.2.3` fixture, 14 days, login needed).
For the source run command: `--pull never` (chosen, fails closed) against the plain local name (falls back to a
pull from Docker Hub after a failed build).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `README.md` | rewritten to the layout in *Approach* |
| — | `docs/USER_GUIDE.md` | rewritten to the layout in *Approach* |
| — | `docs/areas/server.md` | *Not here*: drop "and the edge builds (#6)" |
| — | `docs/decisions/0013-…`, `0005-…`, `README.md` (index) | Lead, done in this plan |

## Signatures (for the Dev's skeleton)

none

## Test files

none — no production or test code.

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none (no code).

## Areas

`docs/areas/server.md` — *Not here* no longer names edge builds (#6 closed as not planned). No behavior changes.

## Documentation updates

Sweep: `grep -rn -i "edge\|README\|USER_GUIDE" docs/areas docs/ARCHITECTURE.md docs/CONTRIBUTING.md` — the only
hit is `docs/areas/server.md` line 124. No enumeration elsewhere lists the documentation files.

- `README.md` (Dev): layout of *Approach*, English then German.
- `docs/USER_GUIDE.md` (Dev): layout of *Approach*, English then German; relative links with `../`.
- `docs/areas/server.md` (Dev): line 124 becomes "- Data ingest from the Companion (later issue)."

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` is touched; the documents only describe the existing
listener, data directory and release artifacts. The loopback binding note keeps the network-exposure guidance of
`SECURITY.md` in the README.

## Security considerations

- [x] No limits: no input is parsed.
- [x] No exception reaches a user: no code changes.
- Examples contain no secret or private path; the bug-report guidance tells users to redact tokens and private
  paths, and sends vulnerabilities to private reporting.

## Decision records

- `docs/decisions/0013-readme-quick-start-user-guide-reference.md` (Proposed) — indexed.
- `docs/decisions/0005-product-version-from-one-build-property.md` — unreleased (no `v*` tag on origin), edited in
  place: its consequence no longer names #6 as a version source.

## Out of scope / follow-ups

- Proposed for the orchestrator (no repository change): correct the GitHub description of milestone M0, which
  still promises edge builds and test builds after every merge.
