# Live sending

## Scope

Live sending from the Companion to a server: forward mode's settings `serverAddress` and `serverToken`, the profile
each connection's hello names and the switch between profiles, the start and the hello, what is sent, the buffer of the waiting messages and its bounds, the upload of
the game catalog and its icons, the reconnect delays and the final refusals, the bound on finding a dead connection, the `Server:` texts of forward mode, live sending in local
mode, the log lines, privacy and the accepted risks. A player whose server runs elsewhere (Docker on a NAS or a home
server) and an operator who reads the log rely on it. The connection rules shared with the replay are in
[`replay-sending.md`](replay-sending.md).

## Behavior

### Switching on

- Forward mode is off by default. The Companion sends live data to a configured server only when the settings file
  `%LOCALAPPDATA%\Censio\settings.json` ([`companion.md`](companion.md), *Settings file*) holds `"mode": "forward"`
  (matched ignoring case). The file is read once per start; the settings window writes it and restarts the Companion for a
  change of these keys ([`companion-settings.md`](companion-settings.md)), a change made by hand counts after a restart.
- `serverAddress`, `serverToken`, `bufferHours` and `bufferMegabytes` are matched ignoring case and read with `mode` `forward`, the two buffer keys also with `local`; `localPort` is ignored
  in forward mode. Without `mode`, or with `local`, nothing is sent to an address of the file.
- `serverAddress` is required and follows the rules of the replay's `--server` address
  ([`replay-sending.md`](replay-sending.md), *Switches*): an absolute `ws://` or `wss://` address with a host and
  without user name, password or fragment, no `#`, white space or control character in its text, a query allowed, used as
  given; the path is part of it (`ws://nas:8080/ingest`).
- `serverToken` is optional: 32 to 1,024 visible ASCII characters (`!` to `~`) after trimming space, tab, carriage return
  and line feed, the server's rule ([`ingest-token.md`](ingest-token.md), *Token*), checked after the JSON text is
  unescaped. Without it the hello carries no token, which a server with the token switched off accepts.
- `bufferHours` and `bufferMegabytes` are optional and set the limits of the buffer (*Buffer*): a whole number written as
  decimal digits only, from 1 to 168 and from 1 to 10,240; without them the limits are 24 hours and 100 megabytes. `24.0`,
  `2.4e1`, `"24"`, `null`, `true`, 0, a negative number, a number above the range and a key that occurs twice, also in
  another case, make the setting invalid.
- An invalid setting shows `Server: settings invalid`, sends nothing and logs `Server settings invalid: <problem>` (*Log*;
  local mode's line, renamed). The problem is one of `mode is not local or forward`, `mode occurs more than once`,
  `localPort is not a port from 1 to 65535`, `localPort occurs more than once`, `serverAddress is missing`,
  `serverAddress is not a ws:// or wss:// address` (also a value that is not a text), `serverAddress occurs more than
  once`, `serverToken is not a valid token` (also a value that is not a text), `serverToken occurs more than once`,
  `bufferHours is not a whole number from 1 to 168`, `bufferHours occurs more than once`, `bufferMegabytes is not a whole
  number from 1 to 10240` and `bufferMegabytes occurs more than once`. No problem quotes a value. The game folder is still read.
- An invalid setting takes the path of an invalid local-mode setting: the greyed `Open Censio` stays in the menu, and a
  replay without `--server` ends with `Replay error: local server not running` ([`local-mode.md`](local-mode.md),
  *Switching on*).
- Live data is sent in a normal run and with `--record`. With `--replay`, and with an invalid command line, no live
  connection is opened; a replay in forward mode stays offline unless it names `--server`.

### Start and hello

- At start, without waiting for the game, the Companion connects and sends one hello per connection with the contract
  version 2, the Companion's version, the token of the setting (none without `serverToken`), and the identity and the name
  of a profile ([`contract.md`](contract.md), *Versions and the frozen form*), the profile of *Profiles on the connection*. The connection obeys the rules of
  [`replay-sending.md`](replay-sending.md) (*Connection*): direct, never through a system proxy, no redirect, no `Origin`,
  the system's certificate checks for `wss://`, 30 seconds for the upgrade and for the hello's answer.
- The menu line reads `Server: connecting` from the start until the first attempt ends, then `Server: connected` after
  `helloAccepted`.
- The profiles are the Companion's own ([`companion-profiles.md`](companion-profiles.md)): `Live` with the built-in
  identity `b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94`, the same on every start and for every server, and the profiles the
  player created, each with an identity the Companion made. The player selects the active profile in the settings window
  ([`companion-settings.md`](companion-settings.md)); until then, and without a `profiles.json`, it is `Live`. One Companion per
  server keeps an identity unique ([`ingest.md`](ingest.md)).
- Every playthrough goes to the active profile: loading another savegame of the same figure, or another figure, puts its
  ticks there, and islands with the same key (session GUID, island ID, area index; [`pipe.md`](pipe.md)) continue and
  rename each other's history. The notification of [`companion-profiles.md`](companion-profiles.md) (*Notice*) hints at a
  figure played with another profile and never moves the ticks; the remedy for a mixed history is deleting it on the pages
  ([`history-deletion.md`](history-deletion.md)).

### Profiles on the connection

- Only a hello names a profile and a second hello is refused ([`contract.md`](contract.md), [`ingest.md`](ingest.md)), so a
  connection belongs to one profile: its hello names the profile of the next message to send (the one resent first after
  a lost connection, else the active profile), and each message carries its own profile identity.
- A message of another named profile than the connection's (taken before a switch, also offline, or after a switch)
  ends the connection before that message is sent, and a new connection follows with a hello naming that profile; the
  message is resent first there. The messages the player took offline under a profile and switched away from reach the server under
  that profile's name, in the order they were taken. A message whose profile the list does not know is sent on the current
  connection and the server makes the profile without a name.
- A switch in the window while a connection idles reconnects at once. The old connection ends with the close handshake
  (at most 2 seconds) before the new hello, `Server: connecting` shows until the new hello is accepted, no `Live sending
  failed` line is logged and no delay is waited. The server frees the Companion's place only when it ends the old session, so
  a `companionConnected` refusal within 5 seconds of the switch is retried after 250 ms, as part of the switch, with no line
  and no step of the reconnect delays; after those 5 seconds the refusal is handled as in *Reconnect*.
- A switch costs one reconnect and the catalog check of the new connection (*Catalog*; record 0111). No tick is held for it:
  the messages taken meanwhile wait in the buffer.

### What is sent

- After `helloAccepted`, in the order the pipe delivered them, each with a new message identity, the profile identity and
  the time the Companion read it: every tick with at least one island (a tick without islands has no message), every
  trade-routes item, every session start and the first session end (a repeated one is not sent). A message the contract
  writer refuses is not sent and is logged. The messages are those of [`contract.md`](contract.md).
- A `status` message with the pipe connection's state follows `helloAccepted` and each change of that state, also while
  the game is not running; it gets no reply, waits for nothing and is not kept across a reconnect: the current state is
  sent again after the next `helloAccepted`.
- The game catalog and its icons are sent as well, outside the order above (*Catalog*). No figure name is sent (record
  0041). Island names, the economy, trade routes, the catalog and the icons go to the configured server, over `ws://`
  unencrypted.

### Buffer

- **What is buffered.** Every message of *What is sent* except `status`: ticks, trade-routes items and session events.
  Taking one from the pipe never waits for the server or the disk: the Companion stamps it with the time it took it and
  a background writer writes it to its own file. The same holds in local mode ([`local-mode.md`](local-mode.md)) and when
  a connection is never accepted, and a start without live sending (no `mode`, an invalid server setting, `--replay`, an
  invalid command line) neither reads, sends, changes nor removes the buffer and shows no `Buffer:` line.
- **Folder and files.** The folder is `%LOCALAPPDATA%\Censio\Buffer`, fixed like the log, cache and recordings folders,
  never a path of the settings file or of received data (record 0105). It is created when live sending starts and kept
  when the Companion is removed; every Windows user has their own. A message is one file `<sequence>.msg`, the sequence in
  19 decimal digits (`0000000000000000042.msg`): written as `<sequence>.tmp`, flushed to disk and then renamed, so a
  finished file is never partly written. The file is a header of 68 bytes (`CNSB`, format version 1, the time taken, the
  identity, the length of the JSON and a SHA-256 of those fields and the JSON), then the JSON exactly as sent. The
  contract and the server are unchanged. A file or folder of another name is left alone; a link with a buffer name is
  deleted as a link, never followed or opened, and counted as unreadable.
- **Order and resend.** One message is in flight: the next is sent after the previous one's acknowledgement, which the
  Companion waits for at most 30 seconds. After each `helloAccepted` the buffer is sent oldest first, before messages
  taken later, each message's file deleted once it is acknowledged. The message in flight at a lost connection, at
  `Exit` or at a crash is sent first with the same identity, which a server that stored it acknowledges without storing
  it again ([`ingest.md`](ingest.md), *Messages after the hello*). An acknowledgement with a refusal removes its message;
  it is not resent and `Server refused a live message: <kind>` is logged.
- **Limits.** `bufferHours` (default 24) and `bufferMegabytes` (default 100; a megabyte is 1,048,576 bytes of written
  JSON, the headers not counted) bound the waiting messages, whichever is reached first; the oldest is removed and counted
  first. The age is the time since the Companion took the message by the PC's clock, a clock set back keeps messages
  longer, one set forward drops them earlier. No message older than the age limit is sent: it is checked when a message is
  taken, at the start and before each send. The newest message always stays, so one larger than the size limit is still
  sent, and the one in flight is not removed. The JSON therefore exceeds the size limit at most by the newest message and
  the one in flight, and the files add 68 bytes each plus at most one `.tmp` file being written. The history shows a gap
  for a dropped tick, never zeros ([`contract.md`](contract.md); record 0031); the charts break where more than 15 minutes
  pass without a point ([`web-charts.md`](web-charts.md)).
- **Start.** Reading the folder runs in the background, so the tray icon, the menu and the pipe do not wait for it.
  Messages taken meanwhile come after the stored ones. Only the header and length of a file are checked then, and its
  hash when it is sent. The Companion holds an index of about 64 bytes per stored message (about 320 KB for 168 hours of
  ticks every two minutes) and no stored JSON: a message is read from its file when it is sent. At most 16 MiB of messages
  that wait for their write are in memory, the oldest beyond that dropped. `Live buffer holds <n> messages from an earlier
  run` is logged for n > 0, after the age limit was applied.
- **Unreadable files.** A file whose header, length or hash is wrong, whose identity differs from the index, that grew,
  shrank or vanished after the start, or a link, is deleted, counted as dropped and logged as `Live buffer entries
  unreadable: <n>; discarded`: once with the count at the start, and with 1 for each file found when it is sent. The
  others are sent in order. A `.tmp` file of an earlier run is deleted and counted as dropped. A name with a sequence
  from 10^18 is foreign.
- **A buffer that cannot be written.** When the folder cannot be created or read, or a file cannot be written or renamed
  (disk full, access denied, a file of that name), the buffer is memory only for the rest of the run: what is on disk is
  still sent first, later messages wait in memory, at most 64 messages and 16,777,216 bytes, the oldest dropped and
  counted, and `Live buffer cannot be written; waiting messages are kept in memory until Exit` is logged once.
- **Menu line.** `Buffer: <n> waiting` below the `Server:` line, greyed, in every mode that sends live data; n counts the
  messages that wait, the one in flight included. `, <d> dropped` follows once this run dropped messages and `, not saved
  to disk` for a buffer that is memory only; the digits carry no separator.
- **Dropped messages.** The number is logged as `Live messages dropped: <n>` once at the next acknowledgement after a
  drop, and, when none follows, when live sending ends: at a final refusal, a stopped local server or `Exit` (where the
  Companion waits at most 250 ms, so the line may be missing).
- **Final refusals and a hosted server that does not run.** The buffer keeps taking messages within its limits until
  `Exit`; the next start with the setting or the server fixed sends them.
- **Exit.** The written files stay, the message in flight included. A message that still waits for its write is lost,
  and a `.tmp` file left behind is deleted at the next start. An unacknowledged message may or may not be stored.
- **Another server.** The buffer is sent to the server of the start that sends it; see *Privacy*.

### Catalog

- **When it is ready.** A catalog is ready when the start's cache load or extraction, or a later re-extraction after a
  game update, has ended with names: the menu lines `Catalog: <n> entries`, `Catalog: <n> entries, not cached` and
  `Catalog: <n> entries, icons failed` ([`game-catalog.md`](game-catalog.md)). The statuses before them change nothing;
  with no game or a failed catalog nothing is sent and no line is logged. This holds in forward and in local mode.
- **Identity.** The Companion's catalog identity is the SHA-256 of its catalog document's bytes
  ([`game-catalog-format.md`](game-catalog-format.md)); the server names the same kind of identity in `helloAccepted`
  when it stores a complete catalog ([`ingest.md`](ingest.md), *Messages after the hello*).
- **Upload.** After `helloAccepted`, before the waiting messages, a ready catalog whose identity differs from the one
  the hello names (`null`, or the catalog of another installation) is sent as one `catalogUpload`. For each reference
  in the `missingIcons` of its acknowledgement the Companion then sends one `iconImage` with that icon's PNG, one message
  in flight, in the order the acknowledgement lists them (the server sorts them, [`contract.md`](contract.md)). A
  reference the Companion holds no icon for is counted as refused and not sent. After the last icon it logs `Catalog sent
  to the server: <i> icons sent, <r> refused`, with `0 icons` too: `<i>` counts the icons the server accepted, `<r>` those
  it refused and those the contract writer refused. The waiting messages follow. The server replaces a catalog it holds.
- **Skip.** A catalog whose identity equals the server's is not sent, nor are icons; `Catalog already on the server` is
  logged once per connection and catalog.
- **A catalog that becomes ready while connected** (the first extraction of a start, or a re-extraction) is compared with
  the identity the server holds for this connection (named at the hello, or of the last catalog it accepted here): a
  different one is uploaded after the message in flight and before the next, an equal one sends nothing. A newer catalog
  stops the icons of an older one: the older one's remaining icons are not sent and no `Catalog sent` line is logged for
  it.
- **No icons.** A catalog whose icons failed is uploaded with every icon reference `null` and no icon follows; the pages
  show names without icons.
- **Refusals.** An acknowledgement of the catalog with a refusal sends no icon, logs `Server refused the catalog: <kind>`
  and closes this catalog for the run, also across a reconnect, until a newer catalog or a restart. A catalog the contract
  writer refuses sends nothing, logs `Catalog not sent: the contract writer refused it` and is closed the same way. Live
  messages go on in both cases.
- **Failed connections.** A connection that ends while the catalog or one of its icons is in flight (no acknowledgement
  within 30 seconds, a close, a lost connection) counts one failure and defers the catalog: after the next accepted hello
  the messages that wait at that hello are sent first, and the catalog follows once they are acknowledged or the buffer is
  empty. After the third failure it is not sent again in this run and `Catalog not sent: the connection failed 3 times
  during its upload` is logged. A connection that fails before the deferred catalog's turn neither counts nor resets the
  count; a completed upload or a newer catalog starts it again (record 0107). At the next hello after a failure during the
  icons the server names `null`, so the catalog is sent again and only the icons its acknowledgement lists.
- **Not buffered.** Catalog and icon messages are never written to the buffer folder, are not counted in the `Buffer:`
  line and are not limited by `bufferHours` or `bufferMegabytes`. No menu or tooltip text reports the upload.
- **Memory.** While the upload message is written the document's bytes (at most 16 MiB) and the message (at most 24 MiB)
  exist together; the document model and its bytes are released before the send, the message alone is held until its
  acknowledgement, then the list of missing references and one written icon message at a time.
- **Replay.** A replay sends no catalog, with or without `--server`, also into the local server; it shows the catalog a
  live connection stored in that server earlier.
- **Exit.** `Exit` during an upload keeps the bounds of *Exit*; the next start continues as after a lost connection.

### Reconnect

- A failed attempt is retried after 1, 2, 4, 8 and 16 seconds, then every 30 seconds, as long as the Companion runs. The
  first attempt at start has no delay. A connection that stayed accepted for at least 60 seconds starts the delays again
  at 1 second.
- Each failure sets the menu line until the next accepted hello or another failure. In a live run every `Server:` line of this section ends with ` (profile <name>)`, the whole name of the active profile ([`companion-texts.md`](companion-texts.md)), except `Server: settings invalid`, which adds nothing:

| Line | Cause |
| ---- | ----- |
| `Server: unreachable, retrying` | No WebSocket at the address, also a TLS failure such as an untrusted `wss://` certificate, or no upgrade within 30 seconds |
| `Server: not answering, retrying` | No answer to the hello, no acceptance of a message or no acknowledgement within 30 seconds |
| `Server: connection lost, retrying` | The connection ended after the hello (a close frame, a failed send or receive, a server that stops), also a fault the Companion did not expect |
| `Server: invalid reply, retrying` | A reply that breaks the rules of [`replay-sending.md`](replay-sending.md) (*Limits and errors*), except that the acknowledgement of a catalog upload may be larger (*Limits and errors* below) |
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
  the active profile (*Profiles on the connection*). Messages wait in the buffer until then.
- The `Server:` line keeps local mode's texts; a lost connection is retried as above without a menu text. A hosted server
  that does not start, or that stopped, is not retried and gets no live-sending line of its own, except the `Buffer:` line and the count of dropped messages;
  a connection that its stop ended is logged once as `Live sending failed: server connection lost; retrying`.

### Exit

- `Exit` aborts the connection at once, waits at most 250 ms for the sender before the hosted server stops, and ends the
  process within 2 seconds, in local mode within 5, in every connection state ([`companion.md`](companion.md)).

### Log

Fifteen lines ([`companion-log.md`](companion-log.md)); a line is written when the state changes, not per retry:

| Line | Level | When |
| ---- | ----- | ---- |
| `Server settings invalid: <problem>` | Warning | At start, for an invalid setting (also local mode's) |
| `Live sending connected to the server` | Information | At each accepted hello |
| `Live sending failed: <reason>; retrying` | Warning | At a failure whose reason differs from the last one logged since the last accepted hello |
| `Live sending refused by the server: <reason>; not retried` | Warning | At `tokenRefused` or `unsupportedVersion` |
| `Server refused a live message: <kind>` | Warning | At an acknowledgement with a refusal, the kind by its name |
| `Live message not sent: the contract writer refused it` | Warning | When a message could not be written |
| `Live messages dropped: <n>` | Warning | As in *Buffer* |
| `Live buffer holds <n> messages from an earlier run` | Information | At the start, when the buffer holds stored messages after the age limit was applied |
| `Live buffer cannot be written; waiting messages are kept in memory until Exit` | Warning | Once, when the buffer turns memory only |
| `Live buffer entries unreadable: <n>; discarded` | Warning | As in *Buffer*, *Unreadable files* |
| `Catalog sent to the server: <i> icons sent, <r> refused` | Information | After the last icon of an upload, also with 0 icons |
| `Catalog already on the server` | Information | At most once per accepted hello and ready catalog |
| `Server refused the catalog: <kind>` | Warning | At a refusal of the catalog, the kind by its name |
| `Catalog not sent: the contract writer refused it` | Warning | Once per catalog the writer refuses |
| `Catalog not sent: the connection failed 3 times during its upload` | Warning | Once, when the catalog is given up |

The `<reason>` is a text of [`replay-sending.md`](replay-sending.md) (*Limits and errors*). A hosted server that is not
running ends local mode's live sending without a line of its own, except the count of dropped messages.

### Privacy

- The token is in the hello only: in no log line, menu or tooltip text, diagnostics window or other message. The settings
  file holds it in plain text in the player's private folder, so the file is not shared.
- The buffer holds the messages as sent (island names, the economy, trade routes), never the token, in files readable for
  whoever can read the player's folder. No log line holds a message's content or a path.
- Waiting messages go to the server of the start that sends them: after `serverAddress` changed, island names, the economy and trade routes taken for the old server go to the new one. Delete `%LOCALAPPDATA%\Censio\Buffer` before switching to a server that should not receive them.
- The catalog and the icons go only to the server live sending connects to, unencrypted with `ws://`, under TLS with
  `wss://` (record 0023); the cache file is never sent. No catalog line holds a name, an icon reference or a path.
- No log line holds the address, a name or an exception text. Every failure of the connection ends as one of the reasons
  above, and the sender lets no exception text reach the log.

## Limits and errors

- Replies are read as in [`replay-sending.md`](replay-sending.md) (*Limits and errors*): up to 65,536 bytes, never further,
  with one exception: while the acknowledgement of a catalog upload is awaited, a reply may be up to 1,179,648 bytes (the
  contract's 16,384 `missingIcons`). The bound counts the received bytes before parsing and ends with that wait, so the
  replies to the icons after it keep 65,536 bytes; a reply above the bound is `Server: invalid reply, retrying`
  (record 0108).
- A server of release 0.3.0 reads contract version 1 and refuses the hello: `Server: does not support contract version 2`.

### Accepted risks

- Over `ws://` the Companion sends the ingest token unencrypted at every start and every retry, unattended, to whatever
  host answers `serverAddress`; a name without a domain (such as `nas`) may be answered by any device of the network the PC
  is on, so use `wss://`, or an address on a network you trust, when the PC leaves your home network.
- **The token in `settings.json`.** The file holds the ingest token in plain text, readable for whoever can read the
  player's folder; the Companion neither checks nor sets its access rules, and the settings window's replacement takes
  those of the folder ([`companion-settings.md`](companion-settings.md), *Writing the file*).
- **The buffer.** A power cut may tear the file being written, which is then discarded and counted; a clock change moves
  the age limit (*Buffer*); a writer of the player's folder can change or delete files, which ends as an unreadable file; an
  outage beyond the limits and a message still waiting for its write at `Exit` lose messages. Two Windows sessions of one
  user run two Companions with one folder and may disturb each other's writes, which then ends as a buffer that cannot be
  written or an unreadable file.

## Measurements

The dead idle connection was measured once during the development of #40, against a loopback endpoint of the test project
that accepts the upgrade and never reads, with the keep-alive of this document (interval 15 seconds, timeout 30
seconds): the pending receive failed after 48.75 seconds. It was not measured on Windows or over a network. The server's
own mechanism, the same one, took at most 56.24 seconds for a sum of 15 and 35 seconds ([`ingest.md`](ingest.md), *Measurements*, *Keep-alive on Kestrel*), which
is the reason for the bound of 60 seconds rather than 45.

## Related decisions

- [`decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md`](../decisions/0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md) — profiles are kept by the Companion and each hello names the next message's profile.
- [`decisions/0103-forward-server-address-and-token-are-keys-of-the-settings-file.md`](../decisions/0103-forward-server-address-and-token-are-keys-of-the-settings-file.md) — the address and the token are keys of the settings file.
- [`decisions/0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md`](../decisions/0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md) — reconnect delays, the memory bound of waiting messages (now the bound of memory-only mode) and own pings.
- [`decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md`](../decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md) — `mode` in the settings file, extended by `forward`.
- [`decisions/0031-bounded-local-tick-buffer-gaps-accepted.md`](../decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) — a bounded buffer, gaps accepted.
- [`decisions/0105-unsent-live-messages-kept-one-file-each-in-a-fixed-buffer-folder.md`](../decisions/0105-unsent-live-messages-kept-one-file-each-in-a-fixed-buffer-folder.md) — one file per message in a fixed folder.
- [`decisions/0106-buffer-limits-are-settings-keys-in-hours-and-megabytes.md`](../decisions/0106-buffer-limits-are-settings-keys-in-hours-and-megabytes.md) — the limits are settings keys in hours and megabytes.
- [`decisions/0107-catalog-uploaded-first-on-each-live-connection-deferred-after-a-failure.md`](../decisions/0107-catalog-uploaded-first-on-each-live-connection-deferred-after-a-failure.md) — the catalog goes first, is deferred after a failed upload and given up after three.
- [`decisions/0108-companion-reads-a-larger-reply-only-for-a-catalog-acknowledgement.md`](../decisions/0108-companion-reads-a-larger-reply-only-for-a-catalog-acknowledgement.md) — a larger reply only for a catalog acknowledgement.

## Not here

- The connection rules shared with the replay, the status reasons and the token rule: [`replay-sending.md`](replay-sending.md).
- The messages, their reader and writer and the refusal texts: [`contract.md`](contract.md).
- The server's endpoint and answers: [`ingest.md`](ingest.md); the token: [`ingest-token.md`](ingest-token.md).
- The settings file and the menu in general: [`companion.md`](companion.md); the hosted server: [`local-mode.md`](local-mode.md); the log file: [`companion-log.md`](companion-log.md).
- Building the catalog and its document: [`game-catalog.md`](game-catalog.md), [`game-catalog-format.md`](game-catalog-format.md); storing and showing it: [`ingest.md`](ingest.md), [`web-pages.md`](web-pages.md); the choice of a profile (#43).
- The code: `LiveSender`, `LiveCatalog`, `LiveBuffer`, `LiveBufferFile`, `IngestClient`, `ForwardServerSetting` and `ClientWebSocketConnector` in `src/Censio.Companion.Core`.
