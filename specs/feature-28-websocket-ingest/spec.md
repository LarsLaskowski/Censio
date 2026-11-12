# Spec: WebSocket ingest endpoint with shared token

Source: Issue #28
Status: Draft

## Problem

The Companion is to push its data to the server over a WebSocket (records
[0019](../../docs/decisions/0019-websocket-transport-with-shared-token.md),
[0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md)), in the messages of contract version 1
([`contract.md`](../../docs/areas/contract.md)). Today the server has no endpoint for them (`src/Censio.Server/Program.cs`
maps only the pages, `/health` and static assets), so nothing can be sent (#40) and nothing can be stored (#30). The server
runs in home networks and on NAS hosts, often reachable by other machines: it must take data only from the operator's own
Companion, read every message as untrusted input within bounds, and survive any input.

## Scenarios

- S1: Given a server with a data directory (`/config` in the image) and no token yet, when it starts, then it generates an
  ingest token, stores it in the file `ingest-token` of the data directory and logs
  `An ingest token was generated and stored in '<file>'.`; the operator reads the token from that file
  (`docker exec censio cat /config/ingest-token`) and enters it in the Companion.
- S2: Given that file, when the server starts again, then it uses the token from the file unchanged and logs
  `The ingest token is read from '<file>'.`
- S3: Given a running server, when a Companion opens a WebSocket to `/ingest` and sends a hello of contract version 1 with
  the token, then the server answers `helloAccepted` with its product version and `catalogIdentity` `null`, and logs
  `Companion connected.`
- S4: Given an accepted Companion, when it sends a tick, a session start or end, an icon image or a catalog upload, then
  the server answers each in order with an `acknowledgement` naming its `id`, `refusal` `null`; for a catalog upload
  `missingIcons` lists every distinct icon reference of the document, sorted (the server keeps no icons yet). A `status`
  message gets no answer.
- S5: Given an accepted Companion, when it closes the connection, then the server logs
  `Companion disconnected; <a> messages accepted, <r> refused.` and accepts the next Companion's hello.
- S6: Given the setting `Censio:Ingest:RequireToken` `false` (`CENSIO__INGEST__REQUIRETOKEN=false`), when the server
  starts, then it logs the warning
  `The ingest token is switched off: anyone who reaches the server can send data.`, neither reads nor creates the token
  file, and accepts a version-1 hello whatever its `token` holds.
- S7: Given an operator who deletes the token file, when the server starts, then it generates a new token as in S1, and a
  hello with the old token is refused (E1).

## Errors and edge cases

- E1: A hello with a wrong token or `token` `null` while the token is required: reply `helloRefused` `tokenRefused`, then
  the server closes the connection; log warning `Companion refused: token refused.`
- E2: A first message of a contract version other than 1, whatever its type and token: reply `helloRefused`
  `unsupportedVersion` with `receivedVersion` and `supportedVersions` `[1]`, then close; checked before the token and the
  second-Companion rule; log warning `Companion refused: unsupported contract version <n>.`
- E3: A valid hello while another Companion is connected: reply `helloRefused` `companionConnected`, then close; the
  connected Companion is not affected; log warning `Companion refused: another Companion is connected.`
- E4: A first message that is not a readable version-1 hello (malformed, another type, a binary frame, over 32 KiB), or no
  first message within 10 seconds: closed without a reply (close statuses below).
- E5: After the hello, a message the contract reader refuses as *invalid* or with a catalog kind, whose `id` is a valid
  UUID: an `acknowledgement` with that `id` and the refusal kind, `refusedVersion` `null`, `missingIcons` empty; the
  connection stays open.
- E6: After the hello, a message whose `id` cannot be read (*malformed*, *too large*, *unsupported version*, *invalid*
  with an unusable `id`), a server-to-Companion type, a second hello or a binary frame: closed without a reply. A message
  over 25,165,824 bytes is not read past that bound.
- E7: A Companion that vanishes without closing (network loss, sleep): the server closes its connection within 60 seconds
  and accepts the next hello.
- E8: More than 16 connections at once that have not completed their hello: a further `/ingest` request is answered `503`
  with an empty body.
- E9: A request to `/ingest` that is no WebSocket upgrade is answered `400`; an upgrade that carries an `Origin` header
  (a browser) `403`; both with an empty body.
- E10: The token is required and the token file cannot be read, holds no token after trimming surrounding white space,
  holds more than 1,024 characters or a character outside visible ASCII (`!` to `~`), or cannot be created: one
  `Critical` line naming the file and the reason, no token text, exit code 1 before listening.
- E11: The token is required and no data directory is set: warning
  `No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.`; every version-1 hello
  is refused as in E1.
- E12: `Censio:Ingest:RequireToken` set to anything but `true` or `false` (any case), empty included: one `Critical` line
  naming the setting, exit code 1 before listening.
- E13: The server stops while a Companion is connected: the connection is closed with status 1001.

Close statuses, all with an empty reason: 1000 after a `helloRefused`; 1009 for a message over its bound; 1003 for a
binary frame; 1008 for every other case of E4 and E6.

## Non-functional requirements

- N1: Bounds: the first message 32 KiB, every later one the contract's 25,165,824 bytes; one message read at a time per
  connection, and only the accepted Companion's connection reads past 32 KiB, so the server runs at most one large read
  at a time ([`contract.md`](../../docs/areas/contract.md), *Memory of a read*). The hello within 10 seconds; a silent
  connection closed within 60 seconds.
- N2: The token: 32 bytes from a cryptographic random source, written as 43 characters of unpadded Base64url; the file is
  created owner-only (mode `0600` on Linux). Compared in a time that does not depend on where a presented token differs.
  Never in a log line, a reply, an exception text or the web UI.
- N3: Configuration: `Censio:Ingest:RequireToken` (`CENSIO__INGEST__REQUIRETOKEN`), default `true`; only `false`
  switches the token off (record 0019).
- N4: Log lines hold no token, profile name, island name, Companion version or other message content, and no remote
  address; refused messages are only counted (S5), so a hostile client cannot flood the log per message.
- N5: Compatibility: `/`, `/health` and the pages are unchanged; an existing data directory gets its token file at the
  next start; with the token required, a Companion must be given the token.
- N6: Nothing received is stored or shown (#30, #32).

## Acceptance criteria

- AC1: With a data directory and the token required, the first start creates the token file with a 43-character token and
  logs the S1 line, a later start reads the same token and logs the S2 line, and after the file is deleted a new,
  different token is created; no log line holds the token (S1, S2, S7, N2, N5).
- AC2: A version-1 hello with the stored token is answered `helloAccepted` with the server's product version and
  `catalogIdentity` `null`, and `Companion connected.` is logged (S3).
- AC3: While the token is required, a hello with a wrong token, a token differing only in its last character, or `token`
  `null` is answered `helloRefused` `tokenRefused`, the connection is closed with 1000, no later message of it is read,
  and the warning holds no token (E1, E11).
- AC4: The token comparison takes a time independent of where the tokens differ, and the token reaches no log line, reply
  or exception text (N2, N4).
- AC5: A first message of contract version 2 is answered `helloRefused` `unsupportedVersion` with `receivedVersion` 2 and
  `supportedVersions` `[1]`, with a wrong token as well (E2).
- AC6: While a Companion is connected, a second valid hello is answered `helloRefused` `companionConnected` and the first
  connection keeps working; after the first closes (S5) or goes silent (E7), a new hello is accepted (S5, E3, E7).
- AC7: `RequireToken` `false` (any case) logs the S6 warning, creates and reads no token file, and accepts a version-1
  hello with any `token`; unset or `true` requires it; any other value, empty included, stops the start (S6, E12, N3).
- AC8: A token file that is unreadable, empty, too long or holds a character outside `!` to `~`, or that cannot be
  created, stops the start with exit code 1 and a `Critical` line without token text; without a data directory every
  hello is refused and the E11 warning is logged (E10, E11).
- AC9: An accepted tick, session start, session end, icon image and catalog upload are each acknowledged in order with
  their `id` and `refusal` `null`, a catalog upload with its sorted distinct icon references in `missingIcons`; a `status`
  gets no reply; at the close the S5 counts are logged (S4, S5).
- AC10: Malformed and oversized messages never stop the server: E5's messages are acknowledged with their refusal kind and
  the connection stays open; E4's and E6's inputs close the connection with the listed status, the server keeps
  serving `/health` and the next Companion (E4, E5, E6, E13).
- AC11: A first message over 32 KiB and a later one over 25,165,824 bytes are refused without being read past the bound,
  a hello later than 10 seconds closes the connection, a 17th connection without hello gets `503`, a plain request and
  an upgrade with `Origin` get `400` and `403` (E4, E8, E9, N1).
- AC12: README and the user guide, English first, then German, describe the endpoint address, where the token is and how
  to rotate it, the switch with its risk, and the E10 to E12 start failures; the server's configuration table lists
  `Censio:Ingest:RequireToken` (N3, N5, N6).

## Out of scope

- Storing, showing or replaying what arrives (#29, #30, #32), the server's catalog and icons (#42; hence `catalogIdentity`
  `null` and all icons missing), the Companion's sender, buffer and resend (#40, #41, #34).
- A token set through configuration or a settings page, several tokens or per-Companion credentials (record 0019);
  showing the token anywhere but its file.
- Rate limiting of failed hellos beyond E8 (a 256-bit token is not guessable); TLS (record 0006).

## Open questions

- Reject or replace a second Companion? Answered: reject. The contract has the reason `companionConnected` for it (record
  0058); replacing would let two Companions with the token take the connection from each other in turn, and a broken
  connection is freed by E7 instead.
- The issue says the token is "shown once in the logs/settings" and also "never logged". Answered: never logged (record
  0019, accepted and released). The web UI needs no login (record 0006), so it would show the token to anyone; the
  operator reads it from the file, which the log names.
- `SECURITY.md` asks to "set a strong ingest token through an environment variable". Answered: the token is generated
  (issue #28); `SECURITY.md` is corrected. A configured token can follow as its own issue.
- How can the server acknowledge a refused message by `id`, when the contract reader returns no message on a refusal
  (`ContractReadResult`, `src/Censio.Contracts/ContractReader.cs`)? Answered: the plan decides how the `id` of an *invalid*
  or catalog-refused message is obtained; E5 and E6 fix the behavior.
- Acknowledging data the server does not keep. Answered: an acknowledgement means checked and taken; until #30 the server
  keeps nothing (N6), and no Companion sends before #40.
- No data directory (outside the image, e.g. `dotnet run`). Answered: fail closed without stopping the start (E11), so the
  pages keep working as documented in [`server.md`](../../docs/areas/server.md).

## Area documents

- `docs/areas/ingest.md` (new area, index row): the endpoint `/ingest`, the hello and its checks in order, the token's
  file, generation, rotation and switch, the single-Companion rule and silent connections, acknowledgements, bounds,
  times, close statuses, log lines and what they never hold.
- `docs/areas/server.md`: `/ingest` among the routes reachable without login, the setting, the token file in the data
  directory; *Not here* points to ingest.
- `docs/areas/contract.md`: *Scope* and *Not here* no longer say nothing receives the messages; *Security considerations
  for the endpoint (#28)* points to ingest.
