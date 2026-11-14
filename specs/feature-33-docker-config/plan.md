# Plan: Docker configuration: /config volume and environment variables

Source: Issue #33 | [spec.md](spec.md)
Tier: standard — two new checks in the CI image script and new operator documentation, with no change to the server, the image or a security control.
Security areas touched: CI configuration (`.github/scripts/image-check.sh`, run by the CI job `image`); Ingest token and Companion authentication (the check reads a hash of the file `ingest-token`, and the documents describe how it is kept)

## Problem / root cause

See [spec.md](spec.md), *Problem*: the configuration reference, the port, the database path and the example
`docker run` exist. Missing are the recreate and update steps and a check that the ingest token survives a recreation.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The server reads only the `Censio:` keys `DataDirectory`, `Database:File`, `Ingest:RequireToken`, `Ingest:TokenFile`, all in the README table | `grep -rhoE '"Censio:[A-Za-z:]+"' src`; `README.md:151-155`, `:329-333` | confirmed |
| 2 | The image sets `CENSIO__DATADIRECTORY=/config` and port 8080 | `src/Censio.Server/Dockerfile`, `ENV` lines | confirmed |
| 3 | The image check already starts the documented form on an empty named volume, stops and removes it, and starts a second container on that volume, checking the key files and `was opened.` | `.github/scripts/image-check.sh:191-270` (steps AC2, AC11, I64-AC7b, I29-AC2) | confirmed |
| 4 | The server logs `An ingest token was generated and stored in '{TokenFile}'.` and `The ingest token is read from '{TokenFile}'.` | `src/Censio.Server/IngestTokenSetup.cs:432`, `:440` | confirmed |
| 5 | In the image the token file is created with mode `0600`, owner the container user (outside a container: `docs/areas/ingest.md`, the file rules) | to verify: step F33-AC1 on the early draft PR (Dev, step 6, CI) | unverified |
| 6 | The image `0.2.0` has no ingest token and no database | `git ls-tree v0.2.0 src/Censio.Server/`: `DataProtectionSetup.cs` only | confirmed |
| 7 | `docker exec` runs as user 1654, which can read the `0600` file | `image-check.sh:214` (`id -u` is `1654`) | confirmed |
| 8 | BusyBox in the runtime image has `sha256sum` | to verify: the Dev runs `docker run --rm --entrypoint sha256sum <local image> /app/Censio.Server.dll` in the local substitute (step 6); if it is missing, the step copies the file out with `docker cp` and hashes it on the host | unverified |
| 9 | `fail` and `expect_equal` print the expected and the actual value | `image-check.sh:40-64` | confirmed |
| 10 | A plain docker run uses a local image of the tag without pulling | Docker's documented default `--pull missing` | confirmed |
| 11 | `docker stop` ends the server within 10 s with exit code 0 | `image-check.sh:249-256` (AC11) | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | CI: image check step `F33-AC1` | `.github/scripts/image-check.sh` |
| AC2 | CI: image check step `F33-AC2` | `.github/scripts/image-check.sh` |
| AC3 | check: Reviewer reads both documents against spec AC3 and *Approach* (step 8) | `README.md`, `docs/USER_GUIDE.md` |

Documented shell procedure (the recreate and update steps), the cases of *Operator environments*:

- (a) all lines pasted at once: a failed `docker pull` does not stop the following lines; `docker stop` and
  `docker rm` then remove the old container, and `docker run` with the new tag fails as well. The volume is
  untouched; the documents keep their rule "run the commands one at a time" and say that the `docker run` with the
  old tag brings the server back. No test: the data stays in the volume, which F33-AC2 and I29-AC2 show.
- (b) one at a time: the usual path; CI `image-check.sh` runs stop, remove and run on one volume.
- (c) one line fails: `docker pull` fails — stop, nothing removed; `docker stop` fails (no container) — continue;
  `docker rm` fails — `docker run` reports the name in use, as *Run* says today.
- (d) a variable lost: the steps use no variables.
- CI-only rows: F33-AC1, F33-AC2. Local substitute: the Dev builds the image with the sandbox recipe of `.squad/stack.md`
  (*Known pitfalls*), runs the two new steps' commands by hand on it, and reports the output without the token.

## Verification without tests

Steps 4 and 5 and the *Coverage gate* are not applicable: no production or test code changes.

- AC1: CI: step `F33-AC1` of `image-check.sh` in the job `image` of the early draft PR (Dev writes, orchestrator reads
  the run); local substitute as above.
- AC2: CI: step `F33-AC2` of the same run; local substitute as above.
- AC3: check: the Reviewer in step 8, against the CI run of F33-AC2 for each "shown" statement.

## Approach

Early draft PR: yes. The two new image-check steps run only in CI, and the documents' sentence about what the
check shows rests on that run.

`image-check.sh`, two steps, after the existing ones of the same containers:

- `F33-AC1` after `I29-AC6`, in container `$main`: `docker exec test -f /config/ingest-token`; `stat -c %a:%u` equals
  `600:1654`; the log of `$main` holds `An ingest token was generated and stored in '/config/ingest-token'.`
  (`grep -Fq`). Then the token hash: `docker exec "$main" sha256sum /config/ingest-token`, its first field kept in
  `token_hash`; a value that is not 64 lowercase hex characters fails with `"a sha256 of /config/ingest-token"` /
  `"none"`, never the value.
- `F33-AC2` after `I29-AC2`, in `$reuse_container`: the hash of the file taken the same way equals `token_hash`
  (`expect_equal`, which prints hashes only); the log holds `The ingest token is read from '/config/ingest-token'.`
  and does not hold the generated line.
- The token itself is never read by `cat`, never stored in a variable and never echoed (N4). The header comment
  names the cases of feature 33 ("F33-AC1", "F33-AC2") beside those of issues 64 and 29.

Documents, layout fixed here:

- `docs/USER_GUIDE.md`: a new `####` section *Recreate and update* (German *Neu erstellen und aktualisieren*) right
  after *Data directory and NAS hosts* (German *Datenverzeichnis und NAS-Hosts*). Order: one sentence on when
  (changing a setting, a new version); the update's `docker pull networlddev/censio:<version>` block with the
  sentence that `latest` must be pulled because `docker run` reuses a local `latest`; `docker stop censio`;
  `docker rm censio`; the same `docker run` block as *Run* (one command per block); a list: what the volume keeps
  (key ring, ingest token, database; the Companion's token keeps working; log lines `The ingest token is read from
  '/config/ingest-token'.` and `The database '/config/censio.db' was opened.` or `… was updated to the current
  schema.`), a token file passed again with the same `-e` and `--mount`, without `-v` everything is lost with the
  container, a deleted volume is a first start with a new token, an update from `0.2.0` keeps the key ring only
  (read the new token and enter it in the Companion), going back to an older image on the same volume is refused,
  and an unwritable volume links to *Data directory and NAS hosts*; then "run the commands one at a time; if
  `docker run` with the new tag fails, run it with the old tag"; then the tried/not-yet-tried sentence.
- `README.md`, *Server (Docker)* (English and German): one paragraph after the token block: recreate or update with
  `docker stop censio`, `docker rm censio` and the same `docker run`, the volume keeps key ring, token and database,
  pull `latest` first, from `0.2.0` a new token; link to the guide section. No new command blocks.
- *Ingest endpoint and token* (both languages): the "Not yet tried: the commands of this section in a container"
  sentence states that the image check showed, in a container on a new volume, the file with mode `0600` and the same
  token after a recreation; *Not yet tried* stays for the other commands of the section in a container, NAS hardware,
  reading the file through a NAS share, and an update from a published tag.

## Options considered

One option: the spec fixes a hash comparison in the existing image check; a separate script would repeat its
container setup, and comparing contents (`docker cp` and `cmp`) is kept only as the fallback of fact 8.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| CI | `.github/scripts/image-check.sh` | steps F33-AC1, F33-AC2, header comment |
| Docs | `README.md`, `docs/USER_GUIDE.md`, `docs/areas/server.md` | *Documentation updates* |

## Signatures (for the Dev's skeleton)

None: no production or test code changes.

## Test files

None: the checks are steps of `.github/scripts/image-check.sh`.

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none (no production code).

## Areas

- `docs/areas/server.md`, *Docker image*: the line "The key ring survives a recreation of a container that keeps
  `/config`." becomes "The key ring, the ingest token file and the database survive a recreation of a container that
  keeps `/config`; an image without a token or database (`0.2.0`) creates them at its first start on such a volume."

## Documentation updates

All by the Dev, after the script is green, as *Approach* lays out:

- `docs/USER_GUIDE.md`: the new section in English and German; the *Ingest endpoint and token* sentence in both.
- `README.md`: the paragraph in English and German.
- `docs/areas/server.md`: the line of *Areas*.
- Sweep result: `docs/ARCHITECTURE.md:104` already says the image keeps key ring, database and token in `/config`
  (unchanged); `SECURITY.md:34,44` name the token file (unchanged); the README configuration table needs no row (N3).

## Architecture check

No guarantee of `.squad/project.md` is touched: no setting, route or default changes. The token stays unlogged and
unprinted (record 0061).

## Security considerations

- [ ] No limits: no parsed input changes.
- [ ] None: no exception reaches a user; the CI check prints only case names, hashes and fixed texts.

## Decision records

- none: no decision beyond the spec; the token's storage is record 0061.

## Out of scope / follow-ups

As [spec.md](spec.md), *Out of scope*.
