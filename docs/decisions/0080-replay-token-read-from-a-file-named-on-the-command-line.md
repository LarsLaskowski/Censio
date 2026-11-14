# 0080: The replay's ingest token is read from a file named on the command line, with byte order marks accepted

- **Status:** Accepted
- **Date:** 2026-11-14
- **Area:** replay-sending
- **Source:** Issue #34
- **Supersedes:** —

## Context

A replay sent to a server with the token required needs the ingest token in its hello
([0019](0019-websocket-transport-with-shared-token.md), [0061](0061-ingest-token-generated-into-a-file-of-the-data-directory.md)).
The token is a credential. The documented way to read it, `docker exec censio cat /config/ingest-token`, redirected
with `>` in Windows PowerShell 5.1 gives a UTF-16 file with a byte order mark (spec challenge M2). The settings file
([0045](0045-companion-settings-in-a-json-file-under-local-app-data.md)) gets its server keys with #40 and #43.

## Options considered

1. **A file named by `--token-file`, read by the server's token rules plus UTF-8 and UTF-16 byte order marks** — a
   copy of the server's `ingest-token` works as is; nothing of the token in the process list or shell history.
2. **The token on the command line** — simplest; kept in the process list, the shell history and shortcuts.
3. **An environment variable** — out of the command line; still in the process environment and the shell profile,
   which record 0061 rejected for the server.
4. **A key in `settings.json`** — one place for #40 later; designs #40's and #43's settings ahead of them.
5. **The server's exact rule, no byte order mark** — one rule on both sides; fails the file PowerShell 5.1 writes.

## Decision

Option 1. The rule is in [`replay-sending.md`](../areas/replay-sending.md), *Token file*; the token is never logged
and appears in no message but the hello.

## Consequences

- The Companion and the server each hold a copy of the token's character rule; a change to it changes both.
- A token file's access rules are the user's, as for the server's configured file.
