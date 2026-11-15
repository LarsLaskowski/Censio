# Ingest

## Scope

The server's WebSocket endpoint for the Companion: the route and its HTTP refusals, the hello and its checks, the ingest
token, one Companion at a time and how a silent connection is found, the limits per host, the answers to each message,
the closes, the bounds and times, the log lines and what never reaches a log. The operator who starts the server and the
Companion that connects rely on it.
The messages themselves, their reader and the refusal texts are in [`contract.md`](contract.md). A message is stored
before it is acknowledged (record 0068), the state kept from it is in [`server-state.md`](server-state.md), and the data
pages that show it are in [`web-pages.md`](web-pages.md).

## Behavior

### Route and HTTP refusals

- The Companion connects with a WebSocket to `/ingest` (`ws://<host>:<port>/ingest`, plain HTTP, no subprotocol;
  record 0006). The route answers every HTTP method.
- A request that is no WebSocket upgrade is answered `400`; an upgrade that carries an `Origin` header (a browser
  always sends one, the Companion none) `403`; an upgrade of a blocked host, or of a host that already has 4 connections
  waiting for their hello, `429`; a further upgrade while 16 connections wait for their hello `503`. All four have an
  empty body, write no log line and are decided in this order (*Limits per host*).
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
6. Otherwise the server stores the hello's profile identity with its name (a new profile is created, a known one
   renamed; [`server-state.md`](server-state.md)), answers `helloAccepted` with its product version and logs
   `Companion connected.` The `catalogIdentity` is the stored catalog's identity only when every icon the catalog
   refers to is stored, else `null` (record 0070). If the profile cannot be stored, the hello gets no reply, the
   connection is closed with 1011 (*Closes*), the error line of *Log lines* is logged, neither `Companion connected.`
   nor a disconnect line follows, and the Companion's place is free again; a refused hello stores nothing.

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
  and a hello with the old one is refused as in *The hello*; the Companion is given the new token. A configured token
  is rotated by changing its file and restarting. The token has no setting of its own, because an environment value
  shows in `docker inspect` (record 0061).
- A presented token is compared by its SHA-256 against the SHA-256 of the stored token with a fixed-time comparison, so
  neither the position of a difference nor the length shows in the time. The server keeps only that hash for the
  comparison.
- The switch `Censio:Ingest:RequireToken` (`CENSIO__INGEST__REQUIRETOKEN`) is `true` by default. Only `false` (any case)
  switches the token off: the server logs the warning
  `The ingest token is switched off: anyone who reaches the server can send data.`, neither reads nor creates
  `ingest-token`, and accepts a version-1 hello whatever its `token` holds. Anyone who reaches the endpoint can then send data
  (record 0019). With `Censio:Ingest:TokenFile` set, `false` stops the start instead (*Start failures*).
- With the token required, no data directory and the setting unset, the server starts, logs the warning
  `No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.` and refuses every
  version-1 hello as in step 4 of *The hello*.

### One Companion and silent connections

- A Companion whose hello is accepted counts as connected from the moment the server logs `Companion connected.`; when
  its connection ends the server marks the Companion as disconnected before it frees the place, so a Companion that
  connects next is never overwritten ([`server-state.md`](server-state.md)). A refused hello changes nothing.
- Exactly one Companion is connected at a time; a second valid hello is refused (step 5), not allowed to replace the
  first (record 0062). After the first connection closes, or is closed as silent, a new hello is accepted. The place is
  freed when the session ends, so while a message of a connection that is already closed is still being stored (up to
  about 3 seconds at the bounds, *Messages after the hello*) a hello is refused `companionConnected`; the Companion
  reconnects as after any refusal.
- The server sends a WebSocket ping every 15 seconds and aborts a connection whose ping is not answered by a pong within
  35 seconds. A ping is sent only after 15 seconds in which no data arrived, so a connection that receives bytes does
  not get one. The measured worst case from the last received frame to the abort of a vanished connection is
  56.24 seconds (*Measurements*); the next hello is then accepted.
- Silence is counted in WebSocket frames, not in contract messages: a Companion that answers pings but sends no message,
  and one whose message arrives in parts over more than 60 seconds, stay connected (idle at 110 seconds and a message
  over 75 seconds measured). The session has no idle timer of its own; liveness is the keep-alive alone.
- Every send of the server (a reply or an acknowledgement) is bound to 30 seconds: one not finished by then aborts the
  connection without a close frame, and the session ends as after any lost connection, the place freed. A connection whose peer stops reading while the server's replies back up is thus gone about 33 seconds after the last frame the server read (the store, about 3 seconds, plus 30); one whose replies still fit and that then sends nothing is ended by the keep-alive (56.18 seconds measured). A Companion that reads under about 37 KB/s cannot take the largest acknowledgement (a catalog
  upload with 16,384 `missingIcons`, about 1.1 MB) in time: every such upload is aborted and the Companion reconnects
  and resends in a loop.
- Limit: .NET answers a ping only inside a receive. A Companion that keeps a receive pending, as #40 does, answers at
  once; one without, whose ping is unanswered when a message begins, answers only after that message, which must end
  within 35 seconds of the ping, else the connection is aborted and the Companion reconnects and resends its
  unacknowledged messages (measured: 20 seconds survives, 40 is aborted).

### Limits per host

- A host is the TCP peer's IP address. An IPv4-mapped IPv6 address is its IPv4 address, a scope id is dropped, every
  other IPv6 address is a host of its own (no `/64` grouping), connections without an address are one host.
  `X-Forwarded-For` never changes the address: the server clears that part of ASP.NET Core's forwarded-headers
  options, so `ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true` affects the scheme only; .NET does not process the
  `Forwarded` header (record 0082).
- A host has at most 4 connections waiting for their hello, each holding its place until the hello is decided; a fifth
  upgrade is answered `429`.
- A *failed hello* is a connection that got a place and whose hello was not accepted: `unsupportedVersion`,
  `tokenRefused`, a first message that is no hello, binary, unreadable or not complete in 10 seconds, or a connection
  its peer ended first. It counts when the hello is decided, before the reply, so a refused connection in its close
  wait counts. Not counted: a `companionConnected` refusal, a profile that could not be stored, a stopping server and a
  failed WebSocket accept. An accepted hello sets the host's count to zero; a block stays.
- The 10th failed hello within 60 seconds (each counts for 60) blocks the host for 60 seconds: its upgrades get `429`
  and do not extend the block, and a connection already waiting is decided as usual, its failed hello not counted.
  After the block the count starts at zero; a restart forgets every count and block.
- Block lines, none naming an address: at the start, `Warning`, `A host was refused 10 times within 60 seconds; its
  connections are refused for 60 seconds.`; at the end, `Information`, from a timer (so also for a host that went
  quiet), `A host's block ended; <n> connections were refused while it lasted.`, `<n>` the `429` of the block. A host
  writes at most 16 lines in any 60 seconds: 10 refusals, the start line, up to 4 connections already waiting and the
  end line of an earlier block.
- At most 1,024 hosts have an entry; one is forgotten when nothing of it waits and 60 seconds have passed since its last
  failed hello and its block's end. Hosts beyond the table share one entry and its limits. The host of the last accepted
  hello keeps an entry of its own outside the table; an accepted hello on the shared entry gives its address such an
  entry and leaves the shared entry's failures alone.

### Messages after the hello

The accepted Companion's connection reads one message at a time, at most 25,165,824 bytes each. The type is decided
first:

- A hello, `helloAccepted`, `helloRefused` or `acknowledgement` (a second hello, or a server-to-Companion type), is
  closed with status 1008 and never acknowledged, even when it carries a valid `id`.
- A `status` message is accepted and gets no reply; its pipe state is kept as the Companion's game state until the
  Companion disconnects ([`server-state.md`](server-state.md), *Companion connection*), and the pages that follow the
  state are told when it changes (record 0075).
- A tick, icon image or catalog upload the reader accepts is stored, and only then answered, in order, with an
  `acknowledgement` naming its `id`, `refusal` `null`, `refusedVersion` `null`: an acknowledgement says that the message
  is stored (record 0068). A message already stored (a resend after a lost acknowledgement) is acknowledged the same way
  and changes nothing: a tick is a resend when its `id` is stored, an icon image when its `reference` is stored, a
  catalog upload when the SHA-256 of its document equals the stored catalog's. For a catalog upload `missingIcons`
  lists exactly the distinct icon references of its document, sorted, that have no stored icon, a resend included; for
  the other types the list is empty. A later catalog upload replaces the earlier one, whatever its age.
- A session start or session end the reader accepts is answered the same way and stores nothing.
- A message whose storing fails (disk full, file removed) is not acknowledged: the connection is closed with 1011 and an
  empty reason, the error line of *Log lines* is logged and the message counts as refused. The Companion reconnects and
  resends it.
- A message of one of these five types that the reader refuses as *invalid* or with a catalog kind, whose `id` is a
  valid UUID, is answered with an `acknowledgement` naming that `id` and the refusal kind (`invalid`,
  `catalogMalformed`, `catalogUnsupportedVersion`, `catalogInvalid`, `catalogTooLarge`), `refusedVersion` the
  catalog's schema version for `catalogUnsupportedVersion` and `null` otherwise, `missingIcons` empty. The connection
  stays open (record 0063).
- Everything else closes the connection without an acknowledgement: a message whose `id` cannot be read with 1008, a
  binary frame with 1003, a message over the bound or one the reader refuses as *too large* with 1009.
- At the close of a connection whose hello was accepted the server logs
  `Companion disconnected; <a> messages accepted, <r> refused.` with its counts (a `status` and every message
  acknowledged with `refusal` `null` count as accepted; an acknowledged refusal and a message that closes the
  connection as refused). Refused messages are only counted, so a hostile client cannot write a line per
  message.

### Closes

| Case | Close status | Reply before |
| ---- | ------------ | ------------ |
| After a `helloRefused` | 1000 | the `helloRefused` message |
| A message or a hello's profile could not be stored | 1011 | none |
| Message over its bound (hello 32,768 bytes, later 25,165,824 bytes), or a later message the reader refuses as *too large* | 1009 | none |
| Binary frame | 1003 | none |
| Every other case of the first message and of the message phase that closes | 1008 | none |
| The server stops | 1001 | none |
| A send not finished within 30 seconds | none: the connection is aborted | none |

The reason of every close is empty. A close frame is sent without waiting for the peer; the server then discards what
the peer sends until its close frame arrives or 2 seconds pass (the close wait), then aborts. A refused or
violating peer therefore holds its connection at most 2 seconds after the close, and still reads the close status
(measured: 1000 after a refused hello, 1008 after a second hello).

### Bounds and times

| What | Value |
| ---- | ----- |
| Complete first message, from the upgrade | 10 seconds |
| First message | 32,768 bytes |
| Later message | 25,165,824 bytes (24 MiB), never read past one byte beyond |
| Connections that have not completed their hello | 16 in total, 4 per host |
| Failed hellos that block a host; the window; the block | 10; 60 seconds; 60 seconds |
| Hosts with an entry | 1,024 |
| Send of the server | 30 seconds |
| Ping interval, pong timeout | 15 seconds, 35 seconds |
| Close wait | 2 seconds |
| Token | generated 43 characters; every token 32 to 1,024 characters; file at most 65,536 bytes |

A connection waiting for its hello holds a receive buffer of 32,769 bytes; the buffer for larger messages (24 MiB) is
allocated only after `helloAccepted`, so only the one accepted Companion holds one and the server runs at most one large
read at a time.

### Log lines

Besides the lines named above: a message or a hello's profile that could not be stored logs, at `Error` and with the
exception, `A message could not be stored; the connection is closed.`; the two block lines of *Limits per host* are logged too; the category of the session lines is `Censio.Server.Ingest`. No line holds the token,
a profile or island name, a Companion version or any other content of a message, no remote address, and no path or value
of `Censio:Ingest:TokenFile`; the only value
taken from a message is the found contract version number. A first message that closes the connection without a
reply (the cases of *The hello*, step 1 and 3) is not logged.

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

- **Hosts that look alike.** Behind a reverse proxy all clients are one host, so one client's failures block the
  Companion behind the same address. Docker Engine on Linux with `-p 8080:8080` (measured): a client from another host
  keeps its own address, a client on the Docker host itself (`127.0.0.1`, a container) arrives as the bridge gateway,
  one shared host. IPv6 on Docker is not measured. Docker Desktop for Windows is not measured: all clients may arrive
  as one host. A device with many IPv6 addresses escapes the limits per host, and only the total of 16 holds against it.
  A flood of more hosts than the table holds refuses a Companion that arrives on a new address while it lasts (record
  0082).
- **Log volume.** Many hosts or IPv6 addresses can still grow the log by up to 16 lines per host and minute; Docker's
  default `json-file` driver does not rotate (remedy: `--log-opt max-size`).
- **A token holder that never reads.** A Companion with the token that keeps sending frames the server does not answer (pings, `status` messages) without reading keeps the one place as long as it likes: .NET answers pings inside the receive, out of the session's reach, so the send bound has nothing to time out; one whose acknowledged messages back up is ended by the send bound. Measured: a client sending a ping every 10 ms and never reading was open at 200 seconds.
  It was so before the limits and no bound is added (record 0082); rotating the token ends it.
- **Token off.** With `RequireToken` `false` anyone who reaches the server can send data, and the first to connect keeps
  the one Companion place. Use the switch only on a network where every host is trusted.
- **Plain text.** The token and the data cross the network unencrypted unless a reverse proxy terminates TLS
  (`SECURITY.md`, record 0006). On Windows the token file takes its folder's access rules, so a share that exposes the
  data directory exposes the token.
- **The configured file's access rules.** The server neither checks nor changes the mode or owner of the file named by
  `Censio:Ingest:TokenFile`; who can read it is the operator's decision (a swarm secret is `0444`). A FIFO at that path
  blocks the start until it is written.
- **Growth by a token holder.** Every stored tick, icon and profile stays until retention (#35): a Companion with the token (or anyone, with the token off) can grow the database file, the profile list and a profile's island list (up to 256 new islands per tick) without a bound; the server holds those lists in memory.

### Memory

The largest message is a catalog upload ([`contract.md`](contract.md), *Memory of a read*). Only the accepted
Companion's connection reads past 32 KiB, so at most one such read runs at a time. It holds a buffer of 25,165,825 bytes, the reader's model of the catalog (kept until the reply is
written), the list of `missingIcons` and the written acknowledgement (*Memory of a catalog upload* below).

The limits per host hold at most 1,024 entries plus the shared and the protected one, each with at most 10 failure
times and one timer, plus at most 14 refused connections per entry in their close wait, each with a 32,769-byte buffer.

## Measurements

Timing of the keep-alive and the send bound, and the client address under Docker, were measured on the published Release server (Linux; the keep-alive uses .NET's own timer, not a fake clock); the programs were scratch code outside the repository.

### Keep-alive on Kestrel

The published server (`dotnet publish src/Censio.Server -c Release`, ASP.NET Core 11.0.0, Linux) accepts its WebSockets
with a keep-alive interval of 15 seconds and a timeout of 35 seconds. One server per run (`dotnet Censio.Server.dll
--urls http://127.0.0.1:<port> --Censio:DataDirectory=<empty directory>`); a scratch client, a raw TCP WebSocket (never
answers a ping) or a `ClientWebSocket` (answers only inside a receive), sends a hello with the token and then behaves as
stated. The probe's modes were a vanished peer (hello
after a delay), idle, messages in 16 or 76 fragments with and without a receive pending, a wrong token and a second
hello.

| Case | Result |
| ---- | ------ |
| Vanished peer (never answers a ping), 16 runs with the hello 0.01 to 3.74 seconds after the upgrade | aborted 52.51 to 56.24 seconds after the hello; the one ping came 15.01 to 18.73 seconds after it |
| Idle peer with a receive pending | open at 110.08 seconds, answered every ping |
| A message of 75 seconds with a receive pending | completed at 75.05 seconds and acknowledged |
| The same message, no receive pending, fragments 5 seconds apart from the hello | completed at 75.04 seconds, connection open: no ping is sent while data arrives |
| No receive pending, the ping unanswered, a message beginning at 19 seconds and lasting 75 seconds | aborted: the first send that failed was at 57.06 seconds |
| The same, a message lasting 40 seconds (5-second fragments) | aborted: first failed send at 59.04 seconds (the earlier fragment at 54.04 went through) |
| The same, a message lasting 20 seconds, then a receive | completed at 39.15 seconds, acknowledged, connection open |
| Refused hello (wrong token); a second hello after `helloAccepted` | close frame with status 1000 after 0.06 seconds; with status 1008 |

The keep-alive heartbeat runs every 3.75 seconds from the upgrade: a ping goes at the first heartbeat 15 seconds after
the last data, the abort at the first more than 35 seconds after it, so the worst case is 56.25 seconds after the last
frame (56.24 measured), under 60.

### Send bound, limits per host and client address

A raw TCP WebSocket client that never answers a ping sends session-start messages (each acknowledged) after reading its `helloAccepted` once.

| Case | Result |
| ---- | ------ |
| Messages sent, never read, until the write fails (3 runs) | aborted 30.74, 30.85 and 30.93 seconds after the first message, the server logging `Companion disconnected; 28573 messages accepted, 0 refused.` in the first; the next hello was accepted |
| 1, 10, 100 and 1,000 messages, then nothing | aborted 56.15 to 56.18 seconds after the last frame |
| Receive pending, nothing sent | open at 110.05 seconds |
| A ping every 10 ms, never reading | open at 200 seconds (19,650 pings) |

The client address was read in a scratch copy of the endpoint (a temporary line, never committed) in a container of
Docker Engine 29.9.2 on Linux, started with `-p 8080:8080`. A client on the host to `127.0.0.1` or `172.17.0.1`, and a
client in a container on a Docker bridge to the host's address, arrived as `172.17.0.1`, the bridge gateway; a client on
the host to the host's own address as that address; a client in a separate network namespace joined to the host by a
virtual link, which stands for a host of the LAN, as its own address (`10.99.0.2`). Not measured: IPv6 and Docker Desktop
for Windows.

### Memory of a catalog upload

One catalog upload at the bound through a real `IngestSession` (a hello accepted, then one message; in-memory pipes),
.NET 11, Release, one process per case, peak managed memory sampled every millisecond, as in
[`contract.md`](contract.md), *Memory of a read*. The document: 16,384 entries with 16 names of 42 characters each,
16,604,431 bytes, message 22,139,346 bytes. The control sends the same size of junk.

| Case | Peak managed (message of the client included) | Allocated | Peak working set |
| ---- | --------------------------------------------- | --------- | ---------------- |
| `ContractReader.Read` alone | 185.9 MiB | 164.4 MiB | 233.7 MiB |
| Session, catalog upload (acknowledgement of 1,097,872 bytes, 16,384 `missingIcons`) | 252.7 MiB | 252.4 MiB | 284.6 MiB |
| Session control, the same size of junk (refused by the reader at once) | 101.3 MiB | 80.0 MiB | 133.6 MiB |

The session minus its control allocates 172.4 MiB, 8.0 MiB more than the reader alone (the list and the written reply);
the server's share at the peak is about 196 MiB by subtraction. The figures of [`contract.md`](contract.md), *Memory of
a read*, belong to its three catalog shapes, the highest peak 333.8 MiB; this catalog gave 185.9 MiB. The shape of a
catalog, not only its size, decides the peak.

## Related decisions

- [`decisions/0019`](../decisions/0019-websocket-transport-with-shared-token.md) — WebSocket transport with a shared token that only an explicit setting switches off.
- [`decisions/0020`](../decisions/0020-one-companion-per-server-with-savegame-profiles.md) — one Companion per server.
- [`decisions/0058`](../decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md) — the version is checked before the token and the second Companion.
- [`decisions/0061`](../decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md) — the token is generated into a file of the data directory or read from a configured file, and compared by its hash.
- [`decisions/0062`](../decisions/0062-one-companion-second-refused-silence-found-by-websocket-pings.md) — a second Companion is refused, a silent connection is found by pings, every send is bound in time, every close has a 2-second wait.
- [`decisions/0063`](../decisions/0063-refused-contract-messages-acknowledged-by-id-or-the-connection-closed.md) — a refused message is acknowledged by its id when the reader yields one, else the connection closes.
- [`decisions/0064`](../decisions/0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md) — browser upgrades are refused, pending hellos are bounded.
- [`decisions/0082`](../decisions/0082-ingest-limits-connections-per-host-and-blocks-failing-hosts.md) — limits per host and the block of a host whose hellos keep failing.
- [`decisions/0006`](../decisions/0006-server-plain-http-and-open-health.md) — plain HTTP, no web login.
- [`decisions/0068`](../decisions/0068-ingest-acknowledges-a-message-only-after-it-is-stored.md) — a message is acknowledged only after it is stored; a failed store closes the connection with 1011.
- [`decisions/0070`](../decisions/0070-hello-names-the-stored-catalog-only-when-all-its-icons-are-stored.md) — the hello names the catalog only when all its icons are stored.

## Not here

- The messages, their reader and writer, bounds of the message fields and the refusal texts: [`contract.md`](contract.md).
- The catalog document inside an upload: [`game-catalog-format.md`](game-catalog-format.md).
- The state kept from what arrives: [`server-state.md`](server-state.md); the database: [`server-data.md`](server-data.md).
- Showing what arrives: [`web-pages.md`](web-pages.md). The Companion's replay sending: [`replay-sending.md`](replay-sending.md); live sending, buffer and resend (#40, #41), the catalog and
  icons it uploads (#42).
- The server's HTTP surface, the data directory and the Docker image: [`server.md`](server.md).
- The code: `IngestTokenSetup`, `IngestAccess`, `IngestConnections`, `IngestHosts`, `IngestHost`, `IngestAdmission`, `IngestSession` and `IngestEndpoint` in
  `src/Censio.Server`.
