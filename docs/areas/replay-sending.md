# Replay sending

## Scope

Sending a replayed recording to a server: the Companion's switches `--server` and `--token-file`, the token file, the
connection and its rules, the hello and the profile a run creates, what is sent and in which order, one message in
flight, the keep-alive, the timeouts, the status texts and counts, the log lines, and what never leaves the machine.
A tester who runs the server without the game relies on it. In local mode a replay without `--server` sends to the server the Companion hosts (*Local mode*). It is no live sending ([`live-sending.md`](live-sending.md)); the persistent buffer is part of live sending ([`live-sending.md`](live-sending.md), *Buffer*) and so is the upload of the catalog and icons (*Catalog*). The messages themselves are in [`contract.md`](contract.md),
what the server does with them in [`ingest.md`](ingest.md).

## Behavior

### Switches

- `--server <address>` and `--token-file <file>` are valid only together with `--replay` (the table is in
  [`companion.md`](companion.md), *Command line*); the case of a switch does not matter. `--token-file` is valid only
  with `--server`. Every other combination is an invalid command line, nothing is replayed or sent: a switch given
  twice, a switch without a value, with an empty one or with a switch in its place, and `--server` together with
  `--record`.
- The address must be an absolute `ws://` or `wss://` address with a host and without user name, password or fragment;
  its text holds no `#`, no white space and no control character. A query is allowed. The address is used as given.
- Without `--server` a replay behaves as before and opens no network connection, except in local mode (*Local mode*).

### Token file

- `--token-file` names the file the token is read from, once, before the recording is opened. Without it the hello
  carries no token, which a server with the token switched off accepts ([`ingest-token.md`](ingest-token.md), *Token*).
- The file is opened for reading only and read up to 65,536 bytes; a larger file holds no valid token. It is decoded by
  its byte order mark: UTF-8 (`EF BB BF`), UTF-16 little endian (`FF FE`) or UTF-16 big endian (`FE FF`); without a
  mark it is UTF-8. A byte sequence that is invalid in its encoding holds no valid token. This is the one difference
  from the server's own rule, so that the file Windows PowerShell 5.1 writes with `>` works.
- After decoding, space, tab, carriage return and line feed are trimmed from both ends, and what is left must be 32 to
  1,024 characters of visible ASCII (`!` to `~`), the server's rule, so a byte-exact copy of the server's `ingest-token`
  works.
- A file that cannot be opened or read (missing, a folder, no access, an I/O error) ends the replay with `Replay error:
  token file cannot be read`, one that holds no valid token with `Replay error: token file holds no valid token`; the
  server is not contacted and the recording is not opened.
- The token is held by the sender for the hello only. It is never on the command line, in a log line, a status text, the
  diagnostics window or any message but the hello.

### Order of a run

1. The token file is read (above).
2. The recording is opened and its header and first line are read once; a failure ends the run with today's reason
   ([`pipe-recording.md`](pipe-recording.md), *Limits and errors*) before the server is contacted, so no profile is
   created. An invalid first frame line is reported later, in the replay, like a later invalid line.
3. The status text reads `Replay: Connecting to server` while the connection is opened and the hello answered.
4. The recording is opened a second time and replayed as before, paced by `--speed`; each item is sent as below. If that
   second opening fails, the connection is closed (step 6) before the run ends with `Replay error: file not found`.
5. At the end of the recording the connection is closed.
6. Closing: the Companion sends a close frame with status 1000 and waits at most 2 seconds for the server's close, then
   aborts the connection.

### Local mode

- In local mode ([`local-mode.md`](local-mode.md)), `--replay` without `--server` sends to the hosted server; `--server`
  and `--token-file` keep the rules above, and `--token-file` without `--server` stays an invalid command line. A replay
  without `--server` is therefore no longer offline in local mode.
- The run is the one of *Order of a run* with these differences: there is no token file, so step 1 does nothing. In step 3 the status text
  reads `Replay: Connecting to server` until the hosted server runs, then the Companion connects to
  `ws://127.0.0.1:<port>/ingest` (the port of the setting) and the hello is answered; the hello carries the token the hosted
  server holds in the process, not read from a file. The hello and everything after it are as above, so the pages show the
  profile `Replay <time>`.
- The wait has no time limit of its own: it ends when the server runs or when it is known not to start. A server that
  does not start (`Server: port <port> in use`, `Server: failed` or `Server: settings invalid`) ends the replay with
  `Replay error: local server not running`; nothing is retried. A server that stops after it ran ends the replay with
  the reason of a lost connection (*Limits and errors*).
- Only one Companion runs per Windows session ([`companion.md`](companion.md), *Single instance*), so a replay is started
  after `Exit`; the Companion of the replay hosts the server and keeps it running after `Replay finished` until `Exit`.

### Connection

- The Companion connects directly to the address, never through a configured system proxy, follows no redirect, sends no
  `Origin` header and asks for no subprotocol (the server refuses an `Origin`, [`ingest.md`](ingest.md), *Route and HTTP
  refusals*). It presents no client certificate.
- A `wss://` address is connected over TLS with the certificate checks of the operating system; there is no switch to
  skip them, and a certificate the system does not trust is `server unreachable`. Over `ws://` the token and the data
  cross the network unencrypted ([`SECURITY.md`](../../SECURITY.md), record 0006).
- The WebSocket upgrade must complete within 30 seconds.
- It needs a server with the contract version 3 ingest; the image `0.2.0` has none and answers an HTTP page, and a server
  of release 0.3.0 reads version 1 and refuses the hello (*Limits and errors*).

### Hello and profile

- The Companion sends one hello per run with the contract version 3, the Companion's version, the token of the file
  (none without `--token-file`), a new profile identity and the profile name `Replay <yyyy-MM-dd HH:mm:ss>`, the local
  time at which the run began, in the invariant culture. A second run creates a second profile; the first stays
  ([`server-state.md`](server-state.md)). No figure name is sent (record 0041).
- It waits at most 30 seconds for the answer. `helloAccepted` starts the replay; `helloRefused` ends the run with its
  reason (*Limits and errors*).

### What is sent

- Every tick of the replay with at least one island, as a `tick` message with a new message identity, the profile
  identity, the recorded receive time and the islands as read (record 0036). A tick without islands has no message (the
  contract needs one to 256 islands) and is counted neither as sent nor as refused; a tick the contract writer refuses
  is counted as refused without being sent.
- Every trade-routes item of the replay (a recording of pipe protocol 3 holds them, [`pipe.md`](pipe.md)), as a `tradeRoutes` message
  with a new message identity, the profile identity, the recorded read stamp of its frame as receive time and its routes with
  their stations' island keys in the order read ([`contract.md`](contract.md)); one without routes is sent too. It goes out
  where it stands in the recording, between the ticks. Item offers are not sent; a trade-routes message the contract writer
  refuses is counted as refused without being sent.
- A `sessionStart` and a `sessionEnd` of the replay in their order, each with the recorded read stamp of its frame as
  receive time; a repeated session end ([`pipe.md`](pipe.md)) carries no new information and is not sent.
- A `status` message with the replayed pipe state after `helloAccepted` and at each change of that state; it gets no
  reply and is not counted. Statuses are sent without waiting, also while an acknowledgement is awaited.
- No catalog, icon or figure name is sent: a replay never uploads a catalog, also into the local server, and shows the one a live connection stored there earlier. A raw recording holds the figure name, island names and the economy as the
  game sent them, so a recording is replayed only into one's own server.

### One message in flight

- A tick, trade-routes message, session start or session end is sent only after the previous one was acknowledged, so at most one message and
  one reply are held and a slow server slows the replay; the receive times stay the recorded ones.
- An acknowledgement without a refusal counts a sent tick (a session event and a trade-routes message are not counted); an
  acknowledgement with a refusal, of whatever message, or a message the writer refused, counts as refused and the replay
  goes on. So `<n>` of the status text counts ticks only and `<r>` also a refused trade-routes message.
- The Companion waits at most 30 seconds for each acknowledgement, and writing a message is bounded by the same 30
  seconds.

### Keep-alive

- From `helloAccepted` until the close the Companion always has a receive pending, so it answers the server's pings at
  once ([`ingest.md`](ingest.md), *One Companion and silent connections*) and the connection stays open through any
  pause of the recording; the fixture has one of about 18 minutes (*Measurements*). A close frame in that receive ends
  the run as `server connection lost`, any other frame that is not the awaited acknowledgement as `invalid server reply`.

### Ending a run

- At the end of the recording the connection is closed and the status text reads `Replay finished: <n> ticks sent, <r>
  refused` (`1 tick sent` for one), `<n>` the ticks sent and `<r>` the messages refused. After an invalid line or a
  failed read the ticks before it are sent and the connection is closed, then the run ends with today's `Replay error:
  invalid line <n>` or `Replay error: read failed`.
- A server failure ends the run with its reason when the replay next sends a message, or at the end of the recording
  when none follows, so a pause of the recording delays it; the connection itself is aborted at once, what the server
  acknowledged before stays stored and nothing is resent or retried.
- `Exit` during a run aborts the connection at once and raises no further status; an unacknowledged message may or may
  not be stored.

### Status texts and looks

The tooltip is `Censio Companion: ` and the status text ([`companion.md`](companion.md), *Replay*). With a server the
status texts are `Replay: Connecting to server` (*waiting* look), then the replay states of today, `Replay finished: <n>
ticks sent, <r> refused` (*waiting* look) and `Replay error: <reason>` (*problem* look) until `Exit`. A send problem wins
over a problem of the recording.

### Log

Four lines ([`companion-log.md`](companion-log.md)): `Replay connected to the server` (Information), `Replay refused by
the server: <reason>` (Warning), `Replay sending failed: <reason>` (Warning), `Replay sent <n> ticks, <r> refused`
(Information); the reasons are those of the status text. A failure is logged once. No log line holds the token, the
address, a name or an exception text, which can carry the address.

## Limits and errors

Each `Replay error: <reason>` is the status text with the *problem* look until `Exit`; nothing is retried. The closes
below are done by the Companion at once.

| Reason | Cause |
| ------ | ----- |
| token file cannot be read | The token file is missing, a folder, not accessible or fails to read |
| token file holds no valid token | The file breaks the rules of *Token file* |
| server unreachable | No WebSocket at the address: unknown host, closed port, an HTTP answer instead of an upgrade, a redirect, a TLS failure, no upgrade within 30 seconds |
| token refused | The server answered `helloRefused` `tokenRefused` |
| another Companion is connected | `helloRefused` `companionConnected` |
| server does not support contract version 3 | `helloRefused` `unsupportedVersion`, a server of release 0.3.0 that reads version 1 among them |
| server does not answer | No `helloAccepted` or `helloRefused` within 30 seconds of the hello, a message the server does not take within 30 seconds, or no acknowledgement within 30 seconds of a message |
| server connection lost | The connection ends after the hello (a close frame, a failed send or receive, a server that stops) |
| local server not running | Local mode only: the hosted server did not start (its port is in use, it failed, or the settings are invalid) |
| invalid server reply | A frame that is not a readable contract message, is binary, is larger than 65,536 bytes (the acknowledgement of a live catalog upload may be larger, [`live-sending.md`](live-sending.md), *Limits and errors*), is of a type the server does not send at that point, or acknowledges another message than the awaited one, also one that arrives while nothing is awaited |

- A reply is read up to 65,536 bytes and never further; only live sending reads a larger acknowledgement, of a catalog upload ([`live-sending.md`](live-sending.md), *Limits and errors*).
- Every failure of the connection, whatever its exception, ends as one of the reasons above; no exception text reaches
  the status or the log.
- The replay's own reasons ([`pipe-recording.md`](pipe-recording.md)) are unchanged and checked before the server is
  contacted, except an invalid line after the first one.

### Accepted risks

- **UNC paths.** A UNC path given for `--replay` or `--token-file` is read over SMB with the user's credentials; it is the
  user's own path.
- **A FIFO or device as token file** blocks the start of the replay until it is written; the same holds for the server's
  configured token file ([`ingest-token.md`](ingest-token.md), *Accepted risks*).
- **The token file's access rules** are the user's; the Companion neither checks nor changes them. A token file left in a
  folder others can read leaks the token (`SECURITY.md`).
- **No buffer.** The replay is no live Companion: it buffers nothing and a lost connection loses the rest of the run.
- **Growth.** Every run adds a profile `Replay <time>` and its ticks. A replay keeps the recorded receive times, so the
  ticks of an old recording are folded into hourly points at the next retention run, except those of the profile's
  latest hour, which stay as ticks, and the operator removes a profile in the web interface ([`history-deletion.md`](history-deletion.md)).

## Measurements

Made on 2026-11-14 on Linux with the published server (`dotnet publish src/Censio.Server -c Release`, ASP.NET Core
11.0.0) and a scratch program outside the repository that used `ReplaySender` and `ClientWebSocketConnector` as built.

### A gap of more than 60 seconds

Server started with `dotnet Censio.Server.dll --urls http://127.0.0.1:5057 --Censio:DataDirectory=<empty directory>`; the
program read the token file `ingest-token` of that directory, connected, sent one tick and waited 75 seconds before it
sent a second tick:

| Step | Result |
| ---- | ------ |
| hello and first tick | accepted; the tick acknowledged 0.09 seconds after the start |
| 75 seconds without a message, the receive pending | the second tick acknowledged 75.10 seconds after the start, on the same connection |
| close | accepted; the server logged `Companion disconnected; 2 messages accepted, 0 refused.` and answered the upgrade once, after 75,241 ms |

The server pings after 15 seconds of silence and aborts a connection that does not answer within 35 seconds; the
connection survived 75 seconds, so the pending receive answered the pings.

### `wss://`

The same program against the published server on `https://127.0.0.1:5058` (`--Kestrel:Certificates:Default:Path`
and `KeyPath` of a certificate for `localhost` signed by a scratch CA created for the test):

| Step | Result |
| ---- | ------ |
| the CA not trusted | `server unreachable`; nothing was sent |
| the scratch CA trusted for the process only (`SSL_CERT_FILE=<CA file>`, OpenSSL's store of .NET on Linux) | connected over TLS, two ticks acknowledged |

Not run: the check in a disposable container of the .NET SDK image (the container runtime had no running daemon in the
environment of the measurement), so the trust of a CA by the container's own store and by the Windows certificate store
was not tried.

The Product Manager ran the documented steps on Windows at `--speed 10` with `long-session.jsonl`: a Docker server
(image built from this state of the repository) and a server from source (`ws://localhost:5000/ingest`) both filled
the pages (regions and islands), and a token file written by Windows PowerShell 5.1 with `>` was accepted, so that
shell's encoding is confirmed. Not tried: `docker cp` for the token file, a token file saved by Notepad as UTF-16 BE,
`wss://` on Windows, the Windows certificate store, a configured system proxy and a remote Docker host.

## Related decisions

- [`decisions/0079-recordings-sent-to-a-server-by-companion-replay-switches.md`](../decisions/0079-recordings-sent-to-a-server-by-companion-replay-switches.md) — the Companion's replay switches send a recording, one message at a time, without reconnect.
- [`decisions/0080-replay-token-read-from-a-file-named-on-the-command-line.md`](../decisions/0080-replay-token-read-from-a-file-named-on-the-command-line.md) — the token is read from a file, byte order marks accepted.
- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — the replay's receive times are the recorded ones.
- [`decisions/0019-websocket-transport-with-shared-token.md`](../decisions/0019-websocket-transport-with-shared-token.md) — the WebSocket with a shared token the sender presents.
- [`decisions/0096-contract-version-2-adds-trade-routes-read-exactly.md`](../decisions/0096-contract-version-2-adds-trade-routes-read-exactly.md) — version 2 adds the trade-routes message a replay sends.
- [`decisions/0101-local-replay-sent-to-the-hosted-server-with-the-token-in-memory.md`](../decisions/0101-local-replay-sent-to-the-hosted-server-with-the-token-in-memory.md) — in local mode a replay without `--server` waits for the hosted server and sends with the token held in the process.

## Not here

- The messages, their reader and writer and the refusal texts: [`contract.md`](contract.md).
- The server's endpoint and answers: [`ingest.md`](ingest.md); token rules: [`ingest-token.md`](ingest-token.md); what it keeps: [`server-state.md`](server-state.md).
- The recording file and the replay's pacing: [`pipe-recording.md`](pipe-recording.md); the tray and the command line: [`companion.md`](companion.md); the hosted server: [`local-mode.md`](local-mode.md).
- Live sending, which shares this connection's rules: [`live-sending.md`](live-sending.md); the persistent buffer ([`live-sending.md`](live-sending.md), *Buffer*); the catalog and the icons ([`live-sending.md`](live-sending.md), *Catalog*).
- The code: `ReplaySender`, `ClientWebSocketConnector`, `ReplayTokenFile` and `PipeReplayRunner` in `src/Censio.Companion.Core`.
