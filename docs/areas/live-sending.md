# Live sending

## Scope

Live sending from the Companion to a server: forward mode's settings `serverAddress` and `serverToken`, the fixed
profile `Live`, the start and the hello, what is sent, the waiting messages and their bounds, the reconnect delays and
the final refusals, the bound on finding a dead connection, the `Server:` texts of forward mode, live sending in local
mode, the log lines, privacy and the accepted risks. A player whose server runs elsewhere (Docker on a NAS or a home
server) and an operator who reads the log rely on it. The connection rules shared with the replay are in
[`replay-sending.md`](replay-sending.md).

## Behavior

### Switching on

- Forward mode is off by default. The Companion sends live data to a configured server only when the settings file
  `%LOCALAPPDATA%\Censio\settings.json` ([`companion.md`](companion.md), *Settings file*) holds `"mode": "forward"`
  (matched ignoring case). The file is read once per start, never written, and a change counts after a restart.
- `serverAddress` and `serverToken` are matched ignoring case and read only with `mode` `forward`; `localPort` is ignored
  there. Without `mode`, or with `local`, nothing is sent to an address of the file.
- `serverAddress` is required and follows the rules of the replay's `--server` address
  ([`replay-sending.md`](replay-sending.md), *Switches*): an absolute `ws://` or `wss://` address with a host and
  without user name, password or fragment, no `#`, white space or control character in its text, a query allowed, used as
  given; the path is part of it (`ws://nas:8080/ingest`).
- `serverToken` is optional: 32 to 1,024 visible ASCII characters (`!` to `~`) after trimming space, tab, carriage return
  and line feed, the server's rule ([`ingest-token.md`](ingest-token.md), *Token*), checked after the JSON text is
  unescaped. Without it the hello carries no token, which a server with the token switched off accepts.
- An invalid setting shows `Server: settings invalid`, sends nothing and logs `Server settings invalid: <problem>` (*Log*;
  local mode's line, renamed). The problem is one of `mode is not local or forward`, `mode occurs more than once`,
  `localPort is not a port from 1 to 65535`, `localPort occurs more than once`, `serverAddress is missing`,
  `serverAddress is not a ws:// or wss:// address` (also a value that is not a text), `serverAddress occurs more than
  once`, `serverToken is not a valid token` (also a value that is not a text) and `serverToken occurs more than once`. No
  problem quotes a value. The game folder is still read.
- An invalid setting takes the path of an invalid local-mode setting: the greyed `Open Censio` stays in the menu, and a
  replay without `--server` ends with `Replay error: local server not running` ([`local-mode.md`](local-mode.md),
  *Switching on*).
- Live data is sent in a normal run and with `--record`. With `--replay`, and with an invalid command line, no live
  connection is opened; a replay in forward mode stays offline unless it names `--server`.

### Start and hello

- At start, without waiting for the game, the Companion connects and sends one hello per connection with the contract
  version 2, the Companion's version, the token of the setting (none without `serverToken`), the profile identity and the
  profile name `Live` ([`contract.md`](contract.md), *Versions and the frozen form*). The connection obeys the rules of
  [`replay-sending.md`](replay-sending.md) (*Connection*): direct, never through a system proxy, no redirect, no `Origin`,
  the system's certificate checks for `wss://`, 30 seconds for the upgrade and for the hello's answer.
- The menu line reads `Server: connecting` from the start until the first attempt ends, then `Server: connected` after
  `helloAccepted`.
- The profile identity is one built into the Companion, `b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94`, the same on every
  start and for every server, so the history continues across restarts and updates without a file the Companion writes
  (record 0102). One Companion per server keeps it unique ([`ingest.md`](ingest.md)).
- Until the player can choose a profile, every playthrough goes to `Live`: loading another savegame puts its ticks there.
  Islands with the same key (session GUID, island ID, area index; [`pipe.md`](pipe.md)) continue and rename each other's
  history, and the other savegame's islands stay listed without new values. No warning is shown; the remedy is deleting
  `Live`'s history on the pages ([`history-deletion.md`](history-deletion.md)).

### What is sent

- After `helloAccepted`, in the order the pipe delivered them, each with a new message identity, the profile identity and
  the time the Companion read it: every tick with at least one island (a tick without islands has no message), every
  trade-routes item, every session start and the first session end (a repeated one is not sent). A message the contract
  writer refuses is not sent and is logged. The messages are those of [`contract.md`](contract.md).
- A `status` message with the pipe connection's state follows `helloAccepted` and each change of that state, also while
  the game is not running; it gets no reply, waits for nothing and is not kept across a reconnect: the current state is
  sent again after the next `helloAccepted`.
- No catalog, icon or figure name is sent (record 0041). Island names, the economy and trade routes go to the configured
  server, over `ws://` unencrypted.

### Waiting messages

- One message is in flight: the next is sent after the previous one's acknowledgement, which the Companion waits for at
  most 30 seconds. A message that waits for a connection is held in memory, in order, whether or not a connection is
  accepted; taking one from the pipe never waits for the server.
- At most 64 messages and 16,777,216 bytes of written JSON wait, the one in flight included. Beyond that the oldest
  message that is not in flight is removed and counted; the newest always stays. The history shows a gap for a dropped
  tick, never zeros ([`contract.md`](contract.md); record 0031).
- After a reconnect the message that was in flight and not acknowledged is sent first with the same identity, which a
  server that stored it acknowledges without storing it again ([`ingest.md`](ingest.md), *Messages after the hello*);
  then the others follow in order.
- An acknowledgement with a refusal removes its message; it is not resent and `Server refused a live message: <kind>` is
  logged.
- Nothing is written to disk. `Exit` loses the waiting messages, and an unacknowledged one may or may not be stored; a
  persistent buffer is #41.
- The number of dropped messages is logged as `Live messages dropped: <n>` once at the next acknowledgement after a
  drop, and, when no acknowledgement follows, when live sending ends: at a final refusal, a stopped local server or
  `Exit` (where the Companion waits at most 250 ms for the sender, so the line may then be missing).

### Reconnect

- A failed attempt is retried after 1, 2, 4, 8 and 16 seconds, then every 30 seconds, as long as the Companion runs. The
  first attempt at start has no delay. A connection that stayed accepted for at least 60 seconds starts the delays again
  at 1 second.
- Each failure sets the menu line until the next accepted hello or another failure:

| Line | Cause |
| ---- | ----- |
| `Server: unreachable, retrying` | No WebSocket at the address, also a TLS failure such as an untrusted `wss://` certificate, or no upgrade within 30 seconds |
| `Server: not answering, retrying` | No answer to the hello, no acceptance of a message or no acknowledgement within 30 seconds |
| `Server: connection lost, retrying` | The connection ended after the hello (a close frame, a failed send or receive, a server that stops), also a fault the Companion did not expect |
| `Server: invalid reply, retrying` | A reply that breaks the rules of [`replay-sending.md`](replay-sending.md) (*Limits and errors*) |
| `Server: another Companion is connected, retrying` | `helloRefused` `companionConnected`: the server still holds a vanished connection, for up to about 56 seconds ([`ingest.md`](ingest.md), *One Companion and silent connections*) |
| `Server: token refused` | `helloRefused` `tokenRefused`; not retried |
| `Server: does not support contract version 2` | `helloRefused` `unsupportedVersion`; not retried |

- `Server: token refused` and `Server: does not support contract version 2` end live sending until the Companion is
  restarted: neither is cured without a changed setting or an update, and not retrying keeps the server's block of failing
  hosts out of reach ([`ingest.md`](ingest.md), *Limits per host*). Any other state shows `Server: not connected`.
- A connection that delivers nothing, also while no message is in flight, is found dead within 60 seconds: the Companion
  sends WebSocket pings every 15 seconds and aborts the connection when no answer comes within 30 seconds, which fails its
  pending receive; the line then reads `Server: connection lost, retrying` and the reconnect follows.
- The tooltip and the icon keep showing the pipe connection.

### Local mode

- In local mode ([`local-mode.md`](local-mode.md)) the Companion sends live data in a normal run and with `--record` to
  the server it hosts: once that server runs, to `ws://127.0.0.1:<port>/ingest` with the token the process holds, under
  the same profile `Live`. Messages wait in memory until then.
- The `Server:` line keeps local mode's texts; a lost connection is retried as above without a menu text. A hosted server
  that does not start, or that stopped, is not retried and gets no live-sending line of its own, except the count of dropped messages;
  a connection that its stop ended is logged once as `Live sending failed: server connection lost; retrying`.

### Exit

- `Exit` aborts the connection at once, waits at most 250 ms for the sender before the hosted server stops, and ends the
  process within 2 seconds, in local mode within 5, in every connection state ([`companion.md`](companion.md)).

### Log

Seven lines ([`companion-log.md`](companion-log.md)); a line is written when the state changes, not per retry:

| Line | Level | When |
| ---- | ----- | ---- |
| `Server settings invalid: <problem>` | Warning | At start, for an invalid setting (also local mode's) |
| `Live sending connected to the server` | Information | At each accepted hello |
| `Live sending failed: <reason>; retrying` | Warning | At a failure whose reason differs from the last one logged since the last accepted hello |
| `Live sending refused by the server: <reason>; not retried` | Warning | At `tokenRefused` or `unsupportedVersion` |
| `Server refused a live message: <kind>` | Warning | At an acknowledgement with a refusal, the kind by its name |
| `Live message not sent: the contract writer refused it` | Warning | When a message could not be written |
| `Live messages dropped: <n>` | Warning | As in *Waiting messages* |

The `<reason>` is a text of [`replay-sending.md`](replay-sending.md) (*Limits and errors*). A hosted server that is not
running ends local mode's live sending without a line of its own, except the count of dropped messages.

### Privacy

- The token is in the hello only: in no log line, menu or tooltip text, diagnostics window or other message. The settings
  file holds it in plain text in the player's private folder, so the file is not shared.
- No log line holds the address, a name or an exception text. Every failure of the connection ends as one of the reasons
  above, and the sender lets no exception text reach the log.

## Limits and errors

- Replies are read as in [`replay-sending.md`](replay-sending.md) (*Limits and errors*): up to 65,536 bytes, never further.
- A server of release 0.3.0 reads contract version 1 and refuses the hello: `Server: does not support contract version 2`.

### Accepted risks

- Over `ws://` the Companion sends the ingest token unencrypted at every start and every retry, unattended, to whatever
  host answers `serverAddress`; a name without a domain (such as `nas`) may be answered by any device of the network the PC
  is on, so use `wss://`, or an address on a network you trust, when the PC leaves your home network.
- **The token in `settings.json`.** The file holds the ingest token in plain text, readable for whoever can read the
  player's folder; the Companion neither checks nor changes its access rules.
- **Memory only.** A restart of the Companion, `Exit` and an outage beyond the bounds of *Waiting messages* lose
  messages; the persistent buffer is #41.
- **One profile.** Playthroughs mix in `Live` until the player can choose a profile (*Start and hello*).

## Measurements

The dead idle connection was measured once during the development of #40, against a loopback endpoint of the test project
that accepts the upgrade and never reads, with the keep-alive of this document (interval 15 seconds, timeout 30
seconds): the pending receive failed after 48.75 seconds. It was not measured on Windows or over a network. The server's
own mechanism, the same one, took at most 56.24 seconds for a sum of 15 and 35 seconds ([`ingest.md`](ingest.md), *Measurements*, *Keep-alive on Kestrel*), which
is the reason for the bound of 60 seconds rather than 45.

## Related decisions

- [`decisions/0102-live-data-goes-to-one-fixed-profile-live-until-profiles-are-chosen.md`](../decisions/0102-live-data-goes-to-one-fixed-profile-live-until-profiles-are-chosen.md) — live data goes to the fixed profile `Live` until the player can choose.
- [`decisions/0103-forward-server-address-and-token-are-keys-of-the-settings-file.md`](../decisions/0103-forward-server-address-and-token-are-keys-of-the-settings-file.md) — the address and the token are keys of the settings file.
- [`decisions/0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md`](../decisions/0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md) — reconnect delays, memory-bound waiting messages and own pings.
- [`decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md`](../decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md) — `mode` in the settings file, extended by `forward`.
- [`decisions/0031-bounded-local-tick-buffer-gaps-accepted.md`](../decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) — a bounded buffer, gaps accepted.

## Not here

- The connection rules shared with the replay, the status reasons and the token rule: [`replay-sending.md`](replay-sending.md).
- The messages, their reader and writer and the refusal texts: [`contract.md`](contract.md).
- The server's endpoint and answers: [`ingest.md`](ingest.md); the token: [`ingest-token.md`](ingest-token.md).
- The settings file and the menu in general: [`companion.md`](companion.md); the hosted server: [`local-mode.md`](local-mode.md); the log file: [`companion-log.md`](companion-log.md).
- The persistent buffer (#41), the catalog (#42), the choice of a profile (#43).
- The code: `LiveSender`, `IngestClient`, `ForwardServerSetting` and `ClientWebSocketConnector` in `src/Censio.Companion.Core`.
