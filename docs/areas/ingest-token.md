# Ingest token

## Scope

The ingest token of the server's WebSocket endpoint: its two sources (the generated file and a configured file), its form
and bounds, how the generated file is written and its mode, the rotation, the hash comparison, the switch
`Censio:Ingest:RequireToken`, the start failures and the accepted risks of the token. The operator who starts the server
relies on it; the Companion is given the token ([`replay-sending.md`](replay-sending.md)).

## Behavior

### Token

- The token is required by default and has two sources. By default the server generates it: 32 bytes from a
  cryptographic random source, written as 43 characters of unpadded Base64url, kept in the file `ingest-token` of the
  data directory (`Censio:DataDirectory`, [`server.md`](server.md); `/config/ingest-token` in the Docker image), without
  a trailing line break. Alternatively the operator names a file of their own with `Censio:Ingest:TokenFile`
  (`CENSIO__INGEST__TOKENFILE`, unset by default), for example a Docker secret mounted at `/run/secrets/<name>`. The
  setting wins: with it set, `ingest-token` is neither read, created, changed nor deleted, and no data directory is
  needed (record 0061).
- At start with the token required and the setting unset, the server reads `ingest-token` if it exists, logs
  `The ingest token is read from '<file>'.` and uses its token unchanged. Without the file it generates a token, writes
  it and logs `An ingest token was generated and stored in '<file>'.` The token itself is never logged.
- With the setting set to an absolute path (fully qualified, the rule `Censio:DataDirectory` uses) the server reads that
  file, follows a symbolic link, logs `The ingest token is read from the file named by Censio:Ingest:TokenFile.` and
  uses its token. It never creates the file, never checks or changes its mode or owner (its access rules are the
  operator's), and logs neither its path nor the setting's value. The file is read at start only: a changed token takes
  effect with a restart.
- Both files are read up to 65,536 bytes; surrounding space, tab, carriage return and line feed are trimmed, and what is
  left must be 32 to 1,024 characters of visible ASCII (`!` to `~`). A generated token (43 characters) and
  `openssl rand -base64 32` (44) satisfy it.
- The file is written to `ingest-token.tmp`, flushed and moved to `ingest-token` without overwriting; a failure deletes
  the temporary file, so a partial token is never read.
- On Linux the file is created with mode `0600`; in the Docker image the owner is the container user (`1654`, or the
  `--user` of the container). On Windows the file takes the access rules of its folder.
- Rotation of the generated token: stop the server, delete `ingest-token`, start the server; it generates a new token,
  and a hello with the old one is refused as in [`ingest.md`](ingest.md), *The hello*; the Companion is given the new token. A configured token
  is rotated by changing its file and restarting. The token has no setting of its own, because an environment value
  shows in `docker inspect` (record 0061).
- A presented token is compared by its SHA-256 against the SHA-256 of the stored token with a fixed-time comparison, so
  neither the position of a difference nor the length shows in the time. The server keeps only that hash for the
  comparison.
- The switch `Censio:Ingest:RequireToken` (`CENSIO__INGEST__REQUIRETOKEN`) is `true` by default. Only `false` (any case)
  switches the token off: the server logs the warning
  `The ingest token is switched off: anyone who reaches the server can send data.`, neither reads nor creates
  `ingest-token`, and accepts a version-2 hello whatever its `token` holds. Anyone who reaches the endpoint can then send data
  (record 0019). With `Censio:Ingest:TokenFile` set, `false` stops the start instead (*Start failures*).
- With the token required, no data directory and the setting unset, the server starts, logs the warning
  `No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.` and refuses every
  version-2 hello as in step 4 of [`ingest.md`](ingest.md), *The hello*.

## Limits and errors

### Start failures

With the token required, or with `Censio:Ingest:TokenFile` set, the server stops before it listens, with exit code 1
and one `Critical` line without token text. A value of `Censio:Ingest:RequireToken` other than `true` or `false` (any
case; the empty string included) stops the start first, with the line
`The setting Censio:Ingest:RequireToken must be true or false; the server stops.` The value is not logged. Then, in this
order:

1. `Censio:Ingest:RequireToken` `false` together with the setting `Censio:Ingest:TokenFile` set to any value, the empty
   string included: `The settings Censio:Ingest:TokenFile and Censio:Ingest:RequireToken false exclude each other; the server stops.`
   The server does not run open while a token is configured.
2. The setting is not an absolute path, the empty string included: `The setting Censio:Ingest:TokenFile must be an absolute path; the server stops.`
   The value may be a token put there by mistake and is not logged.
3. The configured file cannot be read (missing, a directory, no permission, a value the platform refuses):
   `The ingest token file named by Censio:Ingest:TokenFile cannot be read; the server stops.` The line carries no
   exception, whose text would name the path.
4. The configured file holds no valid token (empty after trimming, fewer than 32 or more than 1,024 characters, a
   character outside `!` to `~`, or over 65,536 bytes):
   `The ingest token file named by Censio:Ingest:TokenFile holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`

Without the setting and with the token required the same two failures, and a file that cannot be created, stop the start
with the file's path in the line: `The ingest token file '<file>' cannot be read; the server stops.`,
`The ingest token file '<file>' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.` and
`The ingest token file '<file>' cannot be created; the server stops. Make the data directory writable for the user the server runs as.`

These checks run after the data-protection key ring ([`server.md`](server.md)). With the switch `false` and the setting
unset, none of the file checks runs.

### Accepted risks

- **Token off.** With `RequireToken` `false` anyone who reaches the server can send data, and the first to connect keeps
  the one Companion place. Use the switch only on a network where every host is trusted.
- **The generated file on Windows.** On Windows the token file takes its folder's access rules, so a share that exposes the data directory exposes the token.
- **The configured file's access rules.** The server neither checks nor changes the mode or owner of the file named by
  `Censio:Ingest:TokenFile`; who can read it is the operator's decision (a swarm secret is `0444`). A FIFO at that path
  blocks the start until it is written.

## Related decisions

- [`decisions/0019`](../decisions/0019-websocket-transport-with-shared-token.md) — WebSocket transport with a shared token that only an explicit setting switches off.
- [`decisions/0061`](../decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md) — the token is generated into a file of the data directory or read from a configured file, and compared by its hash.

## Not here

- The endpoint, the hello with its `tokenRefused` refusal, the session's log lines and what they never hold: [`ingest.md`](ingest.md).
- The data directory and the place of the token file: [`server.md`](server.md).
- The Companion's token file: [`replay-sending.md`](replay-sending.md).
- The code: `IngestTokenSetup` and `IngestAccess` in `src/Censio.Server`.
