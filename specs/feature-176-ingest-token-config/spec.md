# Spec: Ingest token from a file named by configuration

Source: Issue #176
Status: Draft

## Problem

The server takes its ingest token only from the file `ingest-token` in its data directory. It generates the file on the
first start, or reads one the operator wrote there before the start (`IngestTokenSetup.TryCreateAccess`,
`src/Censio.Server/IngestTokenSetup.cs` lines 73 to 90; [`ingest.md`](../../docs/areas/ingest.md), *Token*). Operators
who keep their secrets in Docker or compose secrets, or in an orchestrator's secret store, get the secret as a file at
a path of their choosing, usually `/run/secrets/<name>`. The server cannot read the token there. Today an operator
can only copy the secret into the data directory, which the documentation does not describe ("There is no other way to
set or change it"). Also, a token the operator writes by hand may be a single character: the file rule asks only for 1
to 1,024 visible ASCII characters.

Claims of the issue checked against the code: generation, comparison by hash and the file rule (`ParseToken`, lines
152 to 175) are confirmed. "Operators cannot hand the server a token of their own" is partly refuted: a valid
`ingest-token` written into the data directory before the start is used unchanged (line 85). That only works at that
one path, and it is not documented. Record 0061 is unreleased: tag `v0.2.0` points to `abc3527`, which comes before
`6a7ca74`.

## Scenarios

- S1: Given the operator set `Censio:Ingest:TokenFile` (`CENSIO__INGEST__TOKENFILE`) to the absolute path of a file with
  a valid token, when the server starts, then it logs
  `The ingest token is read from the file named by Censio:Ingest:TokenFile.`, accepts a hello with that token and
  refuses any other with `tokenRefused`.
- S2: Given the setting is set and the data directory holds an `ingest-token` with a different token, when the server
  starts, then it uses the configured file only. It neither reads, changes nor deletes `ingest-token`, and a Companion
  that presents the old token is refused.
- S3: Given the setting is set and no data directory is set (a server started outside the image), when the server
  starts, then it uses the configured file. It does not log the warning that every Companion is refused.
- S4: Given the operator changed the token in the configured file, when the server is restarted, then the new token is
  required and the old one is refused. The server reads the file only at its start.
- S5: Given the setting is not set, when the server starts, then it behaves as today
  ([`ingest.md`](../../docs/areas/ingest.md), *Token*), except for the minimum length (E6).

## Errors and edge cases

Each case below stops the start before the server listens, with exit code 1 and one `Critical` line, after the
data-protection key ring, as the existing token start failures do. No line holds the token, the setting's value or the
configured path.

- E1: The configured file does not exist, or cannot be read (no permission for the user the server runs as, the path
  is a directory): `The ingest token file named by Censio:Ingest:TokenFile cannot be read; the server stops.` The
  server never creates the file.
- E2: The configured file holds no valid token. A valid token has 32 to 1,024 characters, all visible ASCII (`!` to
  `~`), after trimming surrounding space, tab, carriage return and line feed, so the line break that secret files
  often end with is accepted. The file is read up to 65,536 bytes. Invalid are: an empty file, fewer than 32 or more
  than 1,024 characters, an inner space or another character outside that range, and a file over 65,536 bytes. The
  line is `The ingest token file named by Censio:Ingest:TokenFile holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`
- E3: The setting's value is not an absolute path, the empty string included:
  `The setting Censio:Ingest:TokenFile must be an absolute path; the server stops.` The value is not logged, because an
  operator may have put the token itself into the setting by mistake.
- E4: The setting is set (any value) and `Censio:Ingest:RequireToken` is `false`:
  `The settings Censio:Ingest:TokenFile and Censio:Ingest:RequireToken false exclude each other; the server stops.`
  The server does not run open while a token is configured.
- E5: `Censio:Ingest:RequireToken` holds neither `true` nor `false`: the existing line of that case is logged, also when
  the token file setting is set. That check comes first.
- E6: Without the setting, the data directory's `ingest-token` holds fewer than 32 characters: the start stops as for
  any other invalid token in that file. The line now names the range 32 to 1,024
  (`The ingest token file '<file>' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`).
  A generated token has 43 characters and is not affected.
- E7: The configured path is a symbolic link, as orchestrators mount secrets: the link is followed.

## Non-functional requirements

- N1: The new setting `Censio:Ingest:TokenFile` (environment `CENSIO__INGEST__TOKENFILE`) is unset by default, in
  `appsettings.json` and in the image. Without it, behavior is unchanged apart from E6.
- N2: The token is never logged. The path of the configured file and the setting's value are not logged either (E3):
  a token that begins with `/` is an absolute path on Linux.
- N3: The comparison is unchanged: the server keeps only the SHA-256 of the token and compares it in fixed time
  (record 0061).
- N4: The server neither checks nor changes the mode or the owner of the configured file. Its access rules are the
  operator's, for example the mode `0444` of a swarm secret.
- N5: Compatibility: #28 is unreleased, so the minimum of 32 characters for `ingest-token` (E6) breaks no released
  installation.

## Acceptance criteria

- AC1: With `Censio:Ingest:TokenFile` set to the absolute path of a readable file that holds a valid token, the server
  logs the line of S1 and accepts a hello with that token. A hello with any other token, the token of an existing
  `ingest-token` included, is refused with `tokenRefused`. `ingest-token` is neither read, created, changed nor
  deleted, and this holds with and without a data directory (S1, S2, S3, S4, N1, N3).
- AC2: A configured file that does not exist or cannot be read stops the start with the line of E1, and the file is not
  created (E1).
- AC3: A configured file whose content breaks the rule of E2 stops the start with the line of E2. A content that
  satisfies the rule only after trimming, such as a token followed by a line feed, is accepted. A symbolic link to a
  valid file is accepted (E2, E7).
- AC4: A value of the setting that is not an absolute path, the empty string included, stops the start with the line
  of E3, and the value appears in no log line (E3, N2).
- AC5: The setting together with `Censio:Ingest:RequireToken` `false` stops the start with the line of E4. An invalid
  `RequireToken` value is reported with its existing line first (E4, E5).
- AC6: Without the setting, an `ingest-token` in the data directory with fewer than 32 characters, or another invalid
  token, stops the start with the line of E6. A generated token is still accepted at the next start (S5, E6, N5).
- AC7: In each case of AC1 to AC6, no log line holds the token or the configured path (N2).

## Out of scope

- A setting that holds the token itself (`Censio:Ingest:Token`, `CENSIO__INGEST__TOKEN`): see *Open questions*.
- Checking the mode or the owner of the configured file (N4).
- Reading the file again while the server runs; the token changes with a restart only (S4).
- An estimate of a token's strength beyond its length; the documentation recommends a generated value, for example
  `openssl rand -base64 32`.
- The Companion's side of the token (#40).

## Open questions

- Which source wins when both exist? Answered: the setting. With it, the server ignores `ingest-token` and never
  touches it (S2). The setting is the operator's explicit choice, and writing nothing into the data directory keeps the
  secret in one place.
- An environment value or a file path? Answered: a file path only. A value in the environment is visible in
  `docker inspect`, in the process environment (`/proc/<pid>/environ`) and in a NAS UI's container settings.
  Docker, compose and swarm secrets and orchestrators' secret stores deliver files, and those are the operators
  #176 names. A direct value would be the weaker way to reach the same result.
- Validation and minimum length? Answered: the file rule, plus a minimum of 32 characters for every token, the data
  directory's file included (E2, E6). Strength cannot be measured from one value, but length bounds the worst
  hand-chosen tokens. A generated token (43 characters) and a value from `openssl rand -base64 32` (44) both pass.
- Logging and an invalid value? Answered: the token, the value and the path are never logged (N2), and every invalid
  case stops the start before the server listens (E1 to E4), as an invalid token file does today. A missing or empty
  configured token never switches the token off (record 0019).
- `RequireToken` `false` with a configured file? Answered: the start stops (E4). Ignoring the file would run the server
  open although the operator configured a token.
- How is it documented? Answered: with `docker run`, as the user guide does (record 0026, no compose file): a read-only
  bind mount of a host file the container user `1654` can read, for example
  `-v <host file>:/run/secrets/censio-ingest-token:ro`. Other secret stores are named only as sources of such a file.

## Area documents

- [`docs/areas/ingest.md`](../../docs/areas/ingest.md), *Token*: the two sources and which one wins, the setting, the
  rule of 32 to 1,024 characters for both, reading at start only, rotation for each source, and that the
  configured path is never logged. *Start failures*: the new lines and their order. *Bounds and times*: the minimum.
  *Accepted risks*: the configured file's access rules are the operator's.
- [`docs/areas/server.md`](../../docs/areas/server.md), *Data directory and key ring*: `ingest-token` is kept only
  when the token is required and the setting is unset. A data directory is not needed when the setting is set.
