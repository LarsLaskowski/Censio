# Spec: Ingest token from a file named by configuration

Source: Issue #176
Status: Approved by Product Manager

## Problem

The server takes its ingest token only from `ingest-token` in its data directory, generated at the first start or
written there by hand before it ([`ingest.md`](../../docs/areas/ingest.md), *Token*; `IngestTokenSetup.cs` lines 73 to
90). Operators who keep secrets in Docker or compose secrets or an orchestrator's store get them as a file at a path of
their own, usually `/run/secrets/<name>`, which the server cannot read. A hand-written token may be one character long.

Issue claims checked: generation, comparison by hash and the rule of 1 to 1,024 visible ASCII characters (`ParseToken`,
lines 152 to 175) are confirmed. "Operators cannot hand the server a token of their own" is partly refuted: a valid
hand-written `ingest-token` is used (line 85), but only at that path and undocumented. Record 0061 is unreleased
(`v0.2.0` is at `abc3527`, before `6a7ca74`).

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
- S4: Given the token in the configured file changed, when the server restarts, then only the new token is accepted;
  the file is read at start only.
- S5: Given the setting is not set, when the server starts, then it behaves as today
  ([`ingest.md`](../../docs/areas/ingest.md), *Token*), except for the minimum length (E6).
- S6: Given an operator on Linux Docker Engine with the token in a host file that the user the container runs as can
  read, when they follow the user guide, then one `docker run` command mounts the file read-only (for example
  `-v <host file>:/run/secrets/censio-ingest-token:ro`) and sets `CENSIO__INGEST__TOKENFILE` to the mounted path.
  Unraid, Synology and Docker Desktop for Windows are named "not yet tried"; their operators keep the generated
  `ingest-token`.

## Errors and edge cases

Each case stops the start as the existing token start failures do ([`ingest.md`](../../docs/areas/ingest.md), *Start
failures*), with the line given.

- E1: The configured file does not exist, or cannot be read (no permission for the user the server runs as, the path
  is a directory): `The ingest token file named by Censio:Ingest:TokenFile cannot be read; the server stops.` The
  server never creates the file. The line carries no exception text, which would name the path.
- E2: The configured file holds no valid token by the rule of `ingest-token` (65,536 bytes read, surrounding white space
  trimmed, so a trailing line break is accepted, visible ASCII only) with a length of 32 to 1,024 characters: `The ingest token file named by Censio:Ingest:TokenFile holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`
- E3: The setting's value is not an absolute path by the rule `Censio:DataDirectory` uses (fully qualified), the empty
  string included:
  `The setting Censio:Ingest:TokenFile must be an absolute path; the server stops.` The value may be a token put there
  by mistake and is not logged.
- E4: The setting is set (any value) and `Censio:Ingest:RequireToken` is `false`:
  `The settings Censio:Ingest:TokenFile and Censio:Ingest:RequireToken false exclude each other; the server stops.`
  The server does not run open while a token is configured.
- E5: `Censio:Ingest:RequireToken` holds neither `true` nor `false`: the existing line of that case is logged, also when
  the token file setting is set. That check comes first.
- E6: Without the setting, an `ingest-token` of fewer than 32 characters is invalid too, and its line names the range
  32 to 1,024 (`The ingest token file '<file>' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`).
  A generated token (43 characters) is not affected.
- E7: The configured path is a symbolic link, as orchestrators mount secrets: the link is followed.

## Non-functional requirements

- N1: The new setting `Censio:Ingest:TokenFile` (environment `CENSIO__INGEST__TOKENFILE`) is unset by default, in
  `appsettings.json` and in the image. Without it, behavior is unchanged apart from E6.
- N2: The token is never logged. The path of the configured file and the setting's value are not logged either (E3):
  a token that begins with `/` is an absolute path on Linux.
- N3: The comparison by hash in fixed time is unchanged (record 0061).
- N4: The server neither checks nor changes the mode or the owner of the configured file. Its access rules are the
  operator's, for example the mode `0444` of a swarm secret.
- N5: #28 is unreleased, so the minimum for `ingest-token` (E6) breaks no released installation.
- N6: The documented procedure targets Linux Docker Engine only (Product Manager, option (a)); other hosts and NAS
  container UIs are only named "not yet tried".

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
- AC7: In each case of AC1 to AC6, no log line holds the token or the configured path, neither in its message nor in an
  attached exception (N2).
- AC8: `docs/USER_GUIDE.md` (English and German) documents the setting as S6 states, and a manual dry run on Linux Docker
  Engine confirms its statements (S6, N6).

## Out of scope

- A setting that holds the token itself (`Censio:Ingest:Token`, `CENSIO__INGEST__TOKEN`): see *Open questions*.
- Checking the mode or the owner of the configured file (N4).
- Reading the file again while the server runs; the token changes with a restart only (S4).
- An estimate of a token's strength beyond its length; the documentation recommends a generated value, for example
  `openssl rand -base64 32`.
- The Companion's side of the token (#40).
- A procedure for Unraid, Synology, Docker Desktop for Windows or a NAS container UI (N6).

## Open questions

- Which source wins? Answered: the setting; `ingest-token` is then ignored and untouched (S2), so the secret stays in
  one place.
- An environment value or a file path? Answered: a file path only. An environment value shows in `docker inspect`, the
  process environment and NAS container settings, and the secret stores #176 names deliver files.
- Validation and minimum? Answered: the file rule plus 32 characters for every token (E2, E6). Length cannot prove
  strength but bounds the worst hand-chosen tokens; `openssl rand -base64 32` (44) passes.
- Logging and an invalid value? Answered: nothing of the token, value or path is logged (N2); every invalid case stops
  the start (E1 to E4); a missing or empty configured token never switches the token off (record 0019).
- `RequireToken` `false` with the setting? Answered: the start stops (E4) rather than run open.
- Which hosts does the documentation cover? Answered: the Product Manager chose option (a), Linux Docker Engine only
  (S6, N6, AC8).

## Area documents

- [`docs/areas/ingest.md`](../../docs/areas/ingest.md), *Token*: the two sources and which one wins, the setting, the
  rule of 32 to 1,024 characters for both, reading at start only, rotation for each source, and that the
  configured path is never logged. *Start failures*: the new lines and their order. *Bounds and times*: the minimum.
  *Accepted risks*: the configured file's access rules are the operator's.
- [`docs/areas/server.md`](../../docs/areas/server.md), *Data directory and key ring*: `ingest-token` is kept only
  when the token is required and the setting is unset. A data directory is not needed when the setting is set.

## Challenge

- 1 (major, target hosts): accepted. Escalated; the Product Manager chose option (a), recorded as S6, N6 and AC8 with a
  dry run on Linux Docker Engine. The procedure names "the user the container runs as", not `1654` (NAS hosts run
  `99:100` or `1026:100`), and claims nothing about NAS container UIs.
- 2 (exception text holds the path): accepted. E1 now says the line carries no exception or text holding the path, and
  AC7 covers an attached exception.
- 3 (meaning of "absolute path"): accepted. E3 now names the rule of `Censio:DataDirectory` (fully qualified).
- 4 (record 0061): accepted. 0061 is unreleased, so the plan edits it in place, with the file-only choice and the
  minimum of 32 characters; no superseding record.
- 5 (documentation list): accepted for the plan, which names the new row and the `Censio:DataDirectory` rows of both
  README tables, `SECURITY.md`, the user guide's token section and start failures in both languages,
  `docs/ARCHITECTURE.md` and `.squad/project.md`.
- 6 (tests of the old minimum): accepted for the plan: the Tester replaces the `!~` row and the "1 to 1,024" texts in
  `IngestTokenSetupTests` with a 32-character row holding `!` and `~` and a refused 31-character row for both sources.
