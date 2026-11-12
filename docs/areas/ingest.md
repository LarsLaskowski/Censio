# Ingest

## Scope

The server's WebSocket endpoint for the Companion: the route and its HTTP refusals, the hello and its checks in order,
the ingest token (file, generation, rotation, the switch that turns it off, comparison), one Companion at a time and
how a silent connection is found, what the server answers to each message, the closes, the bounds and times, the log
lines and what never reaches a log. The operator who starts the server and the Companion that connects (#40) rely on it.
The messages themselves, their reader and the refusal texts are in [`contract.md`](contract.md); nothing received is
stored or shown yet (#30, #32).

## Behavior

### Route and HTTP refusals

- The Companion connects with a WebSocket to `/ingest` (`ws://<host>:<port>/ingest`, plain HTTP, no subprotocol;
  record 0006). The route answers every HTTP method.
- A request that is no WebSocket upgrade is answered `400`; an upgrade that carries an `Origin` header (a browser
  always sends one, the Companion none) `403`; a further upgrade while 16 connections wait for their hello `503`. All
  three have an empty body, and they are decided in this order.
- The pages and `/health` are unchanged by the endpoint ([`server.md`](server.md)).

### The hello

The first message of a connection is its hello; it must be complete within 10 seconds of the upgrade, also when it
arrives in parts, and at most 32,768 bytes. The server decides in this order:

1. A first message that is not a readable contract message, a binary frame, a message over 32,768 bytes or one not
   complete in time closes the connection without a reply (*Closes*).
2. A first message of a contract version other than `1`, whatever its type and token, is answered `helloRefused`
   `unsupportedVersion` with `receivedVersion` the version found and `supportedVersions` `[1]`, then the connection is
   closed. This check comes before the token and the second-Companion rule, so Companion and server of different
   versions always name the versions (record 0058). Log line `Companion refused: unsupported contract version <n>.`
3. A first message of version `1` that is no hello is closed without a reply.
4. A hello whose `token` is wrong or `null` while the token is required is answered `helloRefused` `tokenRefused`
   with `receivedVersion` `1` and `supportedVersions` `[1]`, then closed. Log line `Companion refused: token refused.`
5. A valid hello while another Companion is connected is answered `helloRefused` `companionConnected` with the same
   two values, then closed; the connected Companion is not affected. Log line
   `Companion refused: another Companion is connected.`
6. Otherwise the server answers `helloAccepted` with its product version and `catalogIdentity` `null` (the server
   has no catalog yet, #42) and logs `Companion connected.`

### Token

- The token is 32 bytes from a cryptographic random source, written as 43 characters of unpadded Base64url. It is
  required by default. The server keeps it in the file `ingest-token` of the data directory (`Censio:DataDirectory`,
  [`server.md`](server.md); `/config/ingest-token` in the Docker image), without a trailing line break.
- At start with the token required the server reads the file if it exists, logs
  `The ingest token is read from '<file>'.` and uses its token unchanged. Without the file it generates a token, writes
  it and logs `An ingest token was generated and stored in '<file>'.` The token itself is never logged.
- The file is read up to 65,536 bytes; surrounding space, tab, carriage return and line feed are trimmed, and what is
  left must be 1 to 1,024 characters of visible ASCII (`!` to `~`).
- The file is written to `ingest-token.tmp`, flushed and moved to `ingest-token` without overwriting an existing file;
  on a failure the temporary file is deleted, so a partial token is never read at a later start.
- On Linux the file is created with mode `0600`, readable by its owner only; in the Docker image the owner is the
  container user (`1654`, or the `--user` of the container). On Windows (local mode) the file takes the access rules of
  its folder; no owner-only mode is set there.
- Rotation: stop the server, delete `ingest-token`, start the server; it generates a new token, and a hello with the old
  one is refused as in *The hello*. The Companion is given the new token. There is no other way to set or change it
  (a token by configuration is a possible later change; record 0061).
- A presented token is compared by its SHA-256 against the SHA-256 of the stored token with a fixed-time comparison, so
  neither the position of a difference nor the length shows in the time. The server keeps only that hash for the
  comparison.
- The switch `Censio:Ingest:RequireToken` (`CENSIO__INGEST__REQUIRETOKEN`) is `true` by default. Only `false` (any case)
  switches the token off: the server logs the warning
  `The ingest token is switched off: anyone who reaches the server can send data.`, neither reads nor creates the
  file, and accepts a version-1 hello whatever its `token` holds. Anyone who reaches the endpoint can then send data
  (record 0019).
- With the token required and no data directory set, the server starts, logs the warning
  `No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.` and refuses every
  version-1 hello as in step 4 of *The hello*.

### One Companion and silent connections

- Exactly one Companion is connected at a time; a second valid hello is refused (step 5), not allowed to replace the
  first (record 0062). After the first connection closes, or is closed as silent, a new hello is accepted.
- The server sends a WebSocket ping every 15 seconds and aborts a connection whose ping is not answered by a pong within
  35 seconds. A ping is sent only after 15 seconds in which no data arrived, so a connection that receives bytes does
  not get one. The measured worst case from the last received frame to the abort of a connection that vanished is
  56.24 seconds, under 60 (*Measurements*); the next hello is then accepted.
- Silence is counted in WebSocket frames, not in contract messages: a Companion that answers pings but sends no message
  for any time, and one whose message arrives in parts over more than 60 seconds, stay connected (idle at 110 seconds
  and a message over 75 seconds measured).
- After the hello the session has no idle timer of its own; liveness is the keep-alive alone.
- Limit: .NET answers a ping only inside a receive. A Companion that keeps a receive pending, as #40 does, answers at
  once. A Companion without a receive pending, whose ping is still unanswered when a message begins, can answer it only
  after that message, so the message must end within 35 seconds of the ping; otherwise the connection is aborted
  and the Companion reconnects and resends its unacknowledged messages (measured: a message of 20 seconds survives, one
  of 40 seconds is aborted).

### Messages after the hello

The accepted Companion's connection reads one message at a time, at most 25,165,824 bytes each. The type is decided
first:

- A hello, `helloAccepted`, `helloRefused` or `acknowledgement` (a second hello, or a server-to-Companion type), is
  closed with status 1008 and never acknowledged, even when it carries a valid `id`.
- A `status` message is accepted and gets no reply.
- A tick, session start, session end, icon image or catalog upload the reader accepts is answered, in order, with an
  `acknowledgement` naming its `id`, `refusal` `null`, `refusedVersion` `null`. For a catalog upload `missingIcons`
  lists every distinct icon reference of its document, sorted; as the server keeps no icons yet, every one is missing;
  for the other types the list is empty. The server keeps nothing it accepts (#30): an acknowledgement says that the
  message was read and checked.
- A message of one of these five types that the reader refuses as *invalid* or with a catalog kind, whose `id` is a
  valid UUID, is answered with an `acknowledgement` naming that `id` and the refusal kind (`invalid`,
  `catalogMalformed`, `catalogUnsupportedVersion`, `catalogInvalid`, `catalogTooLarge`), `refusedVersion` the
  catalog's schema version for `catalogUnsupportedVersion` and `null` otherwise, `missingIcons` empty. The connection
  stays open (record 0063).
- Everything else closes the connection without an acknowledgement: a message whose `id` cannot be read (*malformed*,
  *unsupported version*, *invalid* with an unusable `id`) with 1008, a binary frame with 1003, a message over the
  bound, or one the reader refuses as *too large* (its own size and count bounds), with 1009.
- At the close of a connection whose hello was accepted the server logs
  `Companion disconnected; <a> messages accepted, <r> refused.` with its counts (a `status` and every message
  acknowledged with `refusal` `null` count as accepted; an acknowledged refusal and a message that closes the
  connection as refused). Refused messages are only counted, so a hostile client cannot write a line per
  message.

### Closes

| Case | Close status | Reply before |
| ---- | ------------ | ------------ |
| After a `helloRefused` | 1000 | the `helloRefused` message |
| Message over its bound (hello 32,768 bytes, later 25,165,824 bytes), or a later message the reader refuses as *too large* | 1009 | none |
| Binary frame | 1003 | none |
| Every other case of the first message and of the message phase that closes | 1008 | none |
| The server stops | 1001 | none |

The reason of every close is empty. A close frame is sent without waiting for the peer; the server then discards what
the peer sends until its close frame arrives or 2 seconds pass (the close wait), and aborts the socket. A refused or
violating peer therefore holds its connection at most 2 seconds after the close, however it keeps sending frames, and
it still reads the close status before the connection ends (measured: a client received the close frame with status
1000 after a refused hello and with 1008 after a second hello, each before its connection ended).

### Bounds and times

| What | Value |
| ---- | ----- |
| Complete first message, from the upgrade | 10 seconds |
| First message | 32,768 bytes |
| Later message | 25,165,824 bytes (24 MiB), never read past one byte beyond |
| Connections that have not completed their hello | 16 |
| Ping interval, pong timeout | 15 seconds, 35 seconds |
| Close wait | 2 seconds |
| Token | 43 characters; file at most 65,536 bytes, token at most 1,024 characters |

A connection waiting for its hello holds a receive buffer of 32,769 bytes; the buffer for larger messages (24 MiB) is
allocated only after `helloAccepted`, so only the one accepted Companion holds one and the server runs at most one large
read at a time.

### Log lines

Besides the lines named above: the category of the session lines is `Censio.Server.Ingest`. No line holds the token,
a profile or island name, a Companion version or any other content of a message, and no remote address; the only value
taken from a message is the found contract version number. A first message that closes the connection without a
reply (the cases of *The hello*, step 1 and 3) is not logged.

## Limits and errors

### Start failures

With the token required, the server stops before it listens, with exit code 1 and one `Critical` line without token
text, when:

- the token file cannot be read: `The ingest token file '<file>' cannot be read; the server stops.`
- the file holds no valid token (empty after trimming, more than 1,024 characters, a character outside `!` to `~`,
  or over 65,536 bytes): `The ingest token file '<file>' holds no valid token (1 to 1,024 visible ASCII characters); the server stops.`
- the file cannot be created: `The ingest token file '<file>' cannot be created; the server stops. Make the data directory writable for the user the server runs as.`

A value of `Censio:Ingest:RequireToken` other than `true` or `false` (any case; the empty string included) also stops the
start, with the line `The setting Censio:Ingest:RequireToken must be true or false; the server stops.` The value is not
logged. These checks run after the data-protection key ring ([`server.md`](server.md)). With the switch `false` none of
the file checks runs.

### Accepted risks

- **Lock-out through the 16 places.** Any host that reaches `/ingest` can hold the 16 places of connections that have not
  completed their hello by reopening connections, and so keep the real Companion out, also with the token required.
  A connection holds its place until its hello is decided, at most 10 seconds, so the attacker must keep opening
  connections. There is no rate limit by host or per address (record 0064); a reverse proxy or the `-p` mapping
  restricts who reaches the port.
- **Log volume.** Each refused hello writes one warning line; without a rate limit a reconnect loop can grow the log
  without bound, and Docker's default `json-file` log driver does not rotate.
- **Token off.** With `RequireToken` `false` anyone who reaches the server can send data, and the first one to connect
  keeps the one Companion place and so keeps the real Companion out. Use the switch only on a network where every host
  is trusted.
- **Plain text.** The token and the data cross the network unencrypted unless a reverse proxy terminates TLS in front of
  the server (`SECURITY.md`, record 0006).
- **A leaked token file.** On Windows the file takes its folder's access rules, and a share that exposes the data
  directory exposes the token.

### Memory

The largest message is a catalog upload ([`contract.md`](contract.md), *Memory of a read*). Only the accepted
Companion's connection reads past 32 KiB, so the server runs at most one such read at a time, also with the token off.
That connection holds a buffer of 25,165,825 bytes, the reader's model of the catalog (kept until the reply is
written), the list of `missingIcons` and the written acknowledgement (*Memory of a catalog upload* below).

## Measurements

The keep-alive cannot be tested on a fake clock (.NET's keep-alive uses its own timer), so its timing was measured on
the real server. The programs were scratch code outside the repository.

### Keep-alive on Kestrel

The published server (`dotnet publish src/Censio.Server -c Release`, ASP.NET Core 11.0.0, Linux) accepts its WebSockets
with a keep-alive interval of 15 seconds and a timeout of 35 seconds (the values of the endpoint). One server per
run, started with

```
dotnet Censio.Server.dll --urls http://127.0.0.1:<port> --Censio:DataDirectory=<empty directory>
```

and a scratch client (`probe`) that opens a raw TCP WebSocket (so that it never answers a ping) or a
`ClientWebSocket` (which answers a ping only inside a receive), sends a hello with the token from the data directory
and then behaves as stated:

```
dotnet probe.dll vanish <port> <token> <delay>   # raw client; delay = seconds between upgrade and hello
dotnet probe.dll idle <port> <token> 110         # receive pending, nothing sent
dotnet probe.dll stream <port> <token>           # receive pending, a session-start message of 183 bytes in 16 fragments, 5 s apart
dotnet probe.dll stream-noreceive <port> <token> # the same, no receive pending
dotnet probe.dll late-75 <port> <token>          # no receive pending, the message begins 19 s after the hello,
                                                 # 76 fragments, 1 s apart
dotnet probe.dll late-short <port> <token>       # as late-75 with 5 fragments, 5 s apart (20 s)
dotnet probe.dll late-long <port> <token>        # as late-75 with 9 fragments, 5 s apart (40 s)
dotnet probe.dll refused <port> <token>          # wrong token
dotnet probe.dll violation <port> <token>        # a second hello after helloAccepted
```

| Case | Result |
| ---- | ------ |
| Vanished peer (never answers a ping), 16 runs with the hello sent 0.01 to 3.74 seconds after the upgrade, which moves the hello against the server's heartbeat | the server aborted the connection after 52.51 to 56.24 seconds from the hello; the one ping came 15.01 to 18.73 seconds after the hello; the largest value, 56.24, belongs to a hello sent at once after the upgrade |
| Idle peer with a receive pending | open at 110.08 seconds, answered every ping |
| A message of 75 seconds with a receive pending | completed at 75.05 seconds and acknowledged |
| The same message, no receive pending, fragments 5 seconds apart from the hello | completed at 75.04 seconds, connection open: no ping is sent while data arrives |
| No receive pending, the ping unanswered, a message beginning at 19 seconds and lasting 75 seconds | aborted: the first send that failed was at 57.06 seconds |
| The same, a message lasting 40 seconds (5-second fragments) | aborted: first failed send at 59.04 seconds (the earlier fragment at 54.04 went through) |
| The same, a message lasting 20 seconds, then a receive | completed at 39.15 seconds, acknowledged, connection open |
| Refused hello (wrong token) | `helloRefused`, then close frame with status 1000 after 0.06 seconds |
| A second hello after `helloAccepted` | close frame with status 1008 |

The heartbeat of .NET's keep-alive runs every `min(interval, timeout) / 4`, that is 3.75 seconds, from the upgrade. A
ping is sent at the first heartbeat 15 seconds after the last data and the connection is aborted at the first heartbeat
more than 35 seconds after the ping, which is 10 heartbeats (37.5 seconds). The worst case is therefore a ping at
18.75 and an abort at 56.25 seconds after the last frame, which the 56.24 seconds measured confirm; the upper bound of
57.5 seconds (15 + 35 + two heartbeats) is not reached. The worst case stays under the 60 seconds of the spec.

### Memory of a catalog upload

One catalog upload at the bound through a real `IngestSession` (a hello accepted, then one message; a
`WebSocket.CreateFromStream` pair over in-memory pipes), .NET 11, Release, workstation non-concurrent garbage
collection, one process per case, the message read from a file before the measurement and peak managed memory
sampled every millisecond, as in [`contract.md`](contract.md), *Memory of a read*. The document: 16,384 entries with 16
names of 42 characters each and a distinct icon reference, 16,604,431 bytes, message 22,139,346 bytes.

```
dotnet bin/Release/net11.0/mem.dll gen catalog.json
dotnet bin/Release/net11.0/mem.dll read      catalog.json   # the reader alone, for comparison
dotnet bin/Release/net11.0/mem.dll transport catalog.json   # the session with a message of 22,139,346 bytes of junk: the cost of buffer and harness
dotnet bin/Release/net11.0/mem.dll session   catalog.json   # the session, three runs
```

| Case | Peak managed (message of the client included) | Allocated | Peak working set |
| ---- | --------------------------------------------- | --------- | ---------------- |
| `ContractReader.Read` alone | 185.9 MiB | 164.4 MiB | 233.7 MiB |
| Session, catalog upload (acknowledgement of 1,097,872 bytes, 16,384 `missingIcons`) | 252.7 MiB | 252.4 MiB | 284.6 MiB |
| Session control, the same size of junk (refused by the reader at once) | 101.3 MiB | 80.0 MiB | 133.6 MiB |

The session minus its control allocates 172.4 MiB, 8.0 MiB more than the reader alone: the retained catalog document
is what the reader builds anyway (no second read), and the list and the written reply of 1.1 MB account for the
rest. The 80 MiB of the control are the server's buffer of 24 MiB and the harness's own copies and buffers on the
client side. By subtraction, not measured directly, the server's share at the peak is its buffer of 24 MiB, the reader's
164.4 MiB and these 8 MiB, about 196 MiB, 10 MiB above the reader alone with its input (the buffer is 2.9 MiB larger than
that input).

The figure of [`contract.md`](contract.md) (the reader's peak of 158.8 MiB) belongs to that document's own catalog; on
the catalog of this measurement the same reader peaks at 185.9 MiB. The shape of a catalog, not only its size, decides
the peak, so the figures of both documents stand for their own input.

## Related decisions

- [`decisions/0019`](../decisions/0019-websocket-transport-with-shared-token.md) — WebSocket transport with a shared token that only an explicit setting switches off.
- [`decisions/0020`](../decisions/0020-one-companion-per-server-with-savegame-profiles.md) — one Companion per server.
- [`decisions/0058`](../decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md) — the version is checked before the token and the second Companion.
- [`decisions/0061`](../decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md) — the token is generated into a file of the data directory and compared by its hash.
- [`decisions/0062`](../decisions/0062-one-companion-second-refused-silence-found-by-websocket-pings.md) — a second Companion is refused, a silent connection is found by pings, every close has a 2-second wait.
- [`decisions/0063`](../decisions/0063-refused-contract-messages-acknowledged-by-id-or-the-connection-closed.md) — a refused message is acknowledged by its id when the reader yields one, else the connection closes.
- [`decisions/0064`](../decisions/0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md) — browser upgrades are refused, pending hellos are bounded.
- [`decisions/0006`](../decisions/0006-server-plain-http-and-open-health.md) — plain HTTP, no web login.

## Not here

- The messages, their reader and writer, bounds of the message fields and the refusal texts: [`contract.md`](contract.md).
- The catalog document inside an upload: [`game-catalog-format.md`](game-catalog-format.md).
- Storing, showing and replaying what arrives (#29, #30, #32), the server's catalog and icons (#42), the Companion's
  sender, buffer and resend (#40, #41, #34).
- The server's HTTP surface, the data directory and the Docker image: [`server.md`](server.md).
- The code: `IngestTokenSetup`, `IngestAccess`, `IngestConnections`, `IngestSession` and `IngestEndpoint` in
  `src/Censio.Server`.
