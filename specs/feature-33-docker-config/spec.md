# Spec: Docker configuration: /config volume and environment variables

Source: Issue #33
Status: Draft

## Problem

An external tester runs the image `networlddev/censio` and needs to know how to start it with a volume and a port,
which settings exist, and that the data survives when the container is removed and created again (to change a
setting or move to a newer image).

Most of the issue is already on `main` (checked at `877bc0c`): the configuration table of `README.md` lists every
`Censio:` key the server reads and `ASPNETCORE_HTTP_PORTS`; the user guide covers token, database path and port; both
show `docker run -d --name censio -p 8080:8080 -v censio-config:/config …`, in English and German. The CI image check
(`.github/scripts/image-check.sh`) starts that command on an empty named volume, then a second container on the same
volume, and checks the same key files and `was opened.` for the database. Missing: the documents never say how to
recreate or update the container and what the volume keeps, and nothing shows that the ingest token survives a
recreation — a new token would silently lock out the Companion.

## Scenarios

- S1: Given no volume `censio-config`, when the tester runs the documented `docker run`, then `/health` answers
  `Healthy` and the log shows `An ingest token was generated and stored in '/config/ingest-token'.` and
  `The database '/config/censio.db' was created.`
- S2: Given that server, when the tester recreates it with the documented steps (stop, remove, the same
  `docker run`), then the log shows `The ingest token is read from '/config/ingest-token'.` and
  `The database '/config/censio.db' was opened.`, and the token, the key ring and the stored profiles are the same.
- S3: Given that server, when the tester updates it with the documented steps (pull the newer tag, remove, the same
  `docker run` with that tag), then S2 holds, with `... was updated to the current schema.` when the schema changed.
- S4: Given a server started with `CENSIO__INGEST__TOKENFILE`, when the tester recreates it, then the documents say
  to pass the same `-e` and `--mount` again.

## Errors and edge cases

- E1: Without `-v` the server writes into the container, and removing it deletes token, key ring and database; the
  documents say so beside the steps.
- E2: A deleted volume makes the next start a first start, with a new token the Companion must be given again; the
  documents say so.
- E3: A `--user` that cannot write the volume stops the start with exit code 1, as today; the steps link to *Data
  directory and NAS hosts*.
- E4: An older image refuses a database a newer one wrote, as today; the steps say that going back is not supported
  on the same volume.

## Non-functional requirements

- N1: Target host, from accepted records and the current documents: Docker 23 or later on linux/amd64, Linux Docker
  Engine, Docker Desktop for Windows (WSL 2) or a NAS that starts containers with `docker run`; commands pasted one at
  a time, one block each; no compose file
  ([0026](../../docs/decisions/0026-server-image-linux-amd64-on-docker-hub-no-compose.md),
  [0009](../../docs/decisions/0009-server-image-runs-as-a-fixed-non-root-user.md)).
- N2: The steps use `docker` commands only and lead through no third-party page.
- N3: No new setting and no change of the image or the server.
- N4: No check, log or document prints the ingest token.
- N5: English first, then German, in each file
  ([0030](../../docs/decisions/0030-documentation-language-rules.md)); what no run showed is marked *Not yet tried*.

## Acceptance criteria

- AC1: The CI image check, on a new empty named volume, finds `/config/ingest-token` with mode `600` owned by `1654`
  and the line `An ingest token was generated and stored in '/config/ingest-token'.` (S1, N4).
- AC2: The CI image check, in a second container on the same volume after the first was removed, finds the same token
  without printing it and the line `The ingest token is read from '/config/ingest-token'.`, besides its existing key
  file and `was opened.` checks (S2, N4).
- AC3: `README.md` and `docs/USER_GUIDE.md`, in English and German, give the steps to recreate and to update the
  container on the same volume, say what the volume keeps (key ring, ingest token, database) and what is lost
  without it or when it is deleted, and that a token file is passed again (S2, S3, S4, E1, E2, E3, E4, N1, N2, N5).

## Out of scope

- A check in the container that stored profiles survive: nothing sends data in CI yet (#40); the database being
  opened, not created, shows the same file.
- A compose file and arm64 (record 0026), backup and restore (documented in *Data and logs*), ASP.NET Core's
  `Logging__…` settings, retention (#35), a warning when `/config` is no volume (N3).

## Open questions

- Is the feature already there? Answered: in large part (*Problem*); this spec covers the gaps.
- What does "keeps data" cover? Answered: from the code, the server keeps the key ring, the ingest token and the
  database in the data directory (`DataProtectionSetup`, `IngestTokenSetup`, `DatabaseSetup`), so `/config` holds
  all of it.
- What may the target host have? Answered: by records 0026 and 0009 and the current documents (N1).

## Area documents

- [server](../../docs/areas/server.md), *Docker image*: "The key ring survives a recreation of a container that
  keeps `/config`" names the ingest token and the database as well.
- [ingest](../../docs/areas/ingest.md), [server-data](../../docs/areas/server-data.md): unchanged.
