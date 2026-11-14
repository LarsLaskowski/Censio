# Spec: Send recordings to a server for testing

Source: Issue #34
Status: Draft

## Problem

A tester who runs the Censio server (in Docker, or started from source) cannot fill its pages without a Companion
that sends data, and no Companion sends any yet: forward mode is #40 ([`contract.md`](../../docs/areas/contract.md),
*Scope*; `docs/USER_GUIDE.md`, *Ingest endpoint and token*: "No Companion sends data yet"). The Companion can already
replay a recording locally with `--replay` and `--speed` ([`companion.md`](../../docs/areas/companion.md), *Replay*),
but the replayed ticks stay in the tray and the diagnostics window. Without the game, nobody can try the server's
ingest, storage and pages end to end through the real contract.

## Scenarios

- S1: Given a server of the state of `main` with its token required and the token saved in a text file, when the tester
  starts `Censio.Companion.exe --replay long-session.jsonl --server ws://<host>:8080/ingest --token-file token.txt
  --speed max`, then the tooltip reads `Censio Companion: Replay: Connecting to server`, then the replay states of today
  (`Replay: Connected`, `Replay: Receiving`), and at the end `Censio Companion: Replay finished: <n> ticks sent, 0
  refused`. The server logs `Companion connected.` and, at the end, `Companion disconnected; <a> messages accepted, 0
  refused.` Its region overview shows the profile `Replay <yyyy-MM-dd HH:mm:ss>` (the Companion's local start time)
  with the regions, islands and goods of the recording.
- S2: Given a data page open in a browser, when the tester replays at a speed such as `10`, then the page updates with
  each replayed tick as for live data, the Companion banner shows the Companion connected with the replayed game state
  while the replay runs, and not connected after `Replay finished`.
- S3: Given a server with the token switched off (`Censio:Ingest:RequireToken` `false`), when the tester replays with
  `--server` and without `--token-file`, then the replay is accepted as in S1.
- S4: Given a reverse proxy that serves the server over TLS, when the tester gives a `wss://` address, then the replay
  is sent over TLS as in S1, the certificate checked by the operating system.
- S5: Given a server that already holds a replay's profile, when the tester replays again, then a second profile
  `Replay <time>` is created and the first stays unchanged.
- S6: Given a start with `--replay` and without `--server`, then the Companion replays exactly as today and opens no
  network connection.

## Errors and edge cases

Every `Replay error: <reason>` below is the tooltip's status text (`Censio Companion: Replay error: <reason>`), with
the icon's *problem* look, and stays until `Exit`; nothing is retried.

- E1: Invalid command line: `--server` without `--replay`; `--token-file` without `--server`; a switch given twice;
  `--server` or `--token-file` without a value, with an empty one or with a switch in its place; `--server` together
  with `--record`; an address that is not an absolute `ws://` or `wss://` address with a host, or one that holds a
  user name, a password or a fragment. The tooltip reads `Censio Companion: Invalid command line` as today, and nothing
  is read, replayed or sent.
- E2: The token file cannot be read (missing, a folder, no access): `Replay error: token file cannot be read`. It holds
  no valid token (by the rules of N4): `Replay error: token file holds no valid token`. Neither the server is contacted
  nor the recording replayed.
- E3: The recording cannot be opened, or its first line is not a valid header (`file not found`, `not a recording`,
  `unsupported version`, [`pipe-recording.md`](../../docs/areas/pipe-recording.md), *Limits and errors*): the error of
  today, before the server is contacted, so no profile is created.
- E4: The server cannot be reached or does not take a WebSocket at that address (unknown host, refused or closed port,
  an HTTP answer instead of the upgrade such as the image `0.2.0`'s, which has no `/ingest`, a redirect, a TLS
  failure, no answer within 30 seconds): `Replay error: server unreachable`.
- E5: The server refuses the hello: a wrong or missing token `Replay error: token refused`; another Companion is
  connected `Replay error: another Companion is connected`; another contract version `Replay error: server does not
  support contract version 1`.
- E6: No reply to the hello or to a message within 30 seconds: `Replay error: server does not answer`; the connection
  is closed.
- E7: The connection ends after the hello (the server stops, the network fails): `Replay error: server connection
  lost`. What the server acknowledged before stays stored; nothing is resent.
- E8: A reply that is not a readable contract message, is binary, is larger than 65,536 bytes, is of a type the server
  does not send at that point, or acknowledges another message than the one sent: `Replay error: invalid server
  reply`; the connection is closed.
- E9: The server acknowledges a message with a refusal, or the Companion's contract writer refuses to write a tick
  (over a bound): the message counts as refused and the replay goes on; the final text counts it
  (`Replay finished: 30 ticks sent, 1 refused`).
- E10: A replayed tick without islands has no message (the contract requires one to 256 islands,
  [`contract.md`](../../docs/areas/contract.md), *Value rules*) and is counted neither as sent nor as refused. A tick
  whose islands carry no entries is sent like any other.
- E11: A line later in the recording is invalid, or it cannot be read on: the ticks before it are sent and
  acknowledged, then the connection is closed and the status is the replay error of today (`Replay error: invalid line
  <n>`).
- E12: `Exit` during the replay closes the connection at once; a message without an acknowledgement may or may not be
  stored, and nothing is resent.
- E13: The server holds a profile with newer ticks than the recording (an anonymised fixture's times lie 30 to 365 days
  back): the replay's profile is not the newest, so a page without a picked profile shows the other one; the tester
  picks `Replay <time>` in the profile switch ([`server-state.md`](../../docs/areas/server-state.md),
  [`web-pages.md`](../../docs/areas/web-pages.md)). The as-of line shows the recorded time.
- E14: A slow server slows the replay: the next message waits for the acknowledgement of the previous one (N1). The
  receive times sent stay the recorded ones.

## Non-functional requirements

- N1: At most one message is in flight: the Companion sends a message only after the previous one was acknowledged,
  and holds at most one message and one reply at a time, whatever the recording's size.
- N2: Messages are written by the contract's writer and replies read by its reader
  ([`contract.md`](../../docs/areas/contract.md)); a reply is read up to 65,536 bytes and never further (E8).
- N3: Timeouts: 30 seconds for the connection with its hello reply, 30 seconds for each acknowledgement.
- N4: The token is read once, from the file named by `--token-file`, by the server's own rules: at most 65,536 bytes,
  surrounding space, tab, carriage return and line feed trimmed, then 32 to 1,024 visible ASCII characters (`!` to
  `~`) ([`ingest.md`](../../docs/areas/ingest.md), *Token*), so a copy of the server's `ingest-token` works. The token
  is never taken from the command line, never logged, and in no status text, menu entry or message other than the
  hello. Without `--token-file` the hello carries no token.
- N5: The connection: the address as given, no redirect followed, no `Origin` header (the server refuses one), no
  subprotocol; `wss://` certificates checked by the operating system with no switch to skip the check. Over `ws://` the token and the
  data cross unencrypted; the user guide says so and points to TLS at a reverse proxy (record 0006).
- N6: What crosses is the contract: a hello with the profile `Replay <yyyy-MM-dd HH:mm:ss>` and a new profile identity
  per run, the ticks, session starts and session ends of the recording in replay order, and the replayed pipe state as
  status messages. The figure name never crosses; no catalog and no icon is sent. A recording's island names and
  amounts reach the server the tester names, so the user guide says to replay a raw recording only into one's own
  server.
- N7: Receive times are the recorded ones and the pacing is `--speed` as today
  ([`pipe-recording.md`](../../docs/areas/pipe-recording.md), *Replay*).
- N8: The Companion logs the connection, a refused hello with its reason, a lost connection, an invalid reply and the
  end with its counts ([`companion-log.md`](../../docs/areas/companion-log.md)); no line holds the token, the address,
  a profile or island name or other message content.
- N9: Without `--server` the Companion opens no network connection (S6). The switches work in the Windows Companion
  (ZIP and build from source); the logic is platform-neutral and tested on Linux.
- N10: Compatibility: a server with the ingest of contract version 1 (the state of `main`); the image `0.2.0` has none
  (E4). No server change is needed.

## Acceptance criteria

- AC1: A replay of `long-session.jsonl` with `--server` into a server leaves it holding the profile `Replay <time>`
  whose pages show three regions, the session GUIDs 3245, 6627 and 149679 with 13, 9 and 2 islands, each island under
  the name of its latest tick; up to the last tick with data each island shows the goods of its latest tick, and after
  the final tick without data the 24 islands stay listed without values (the facts of the fixtures' `README.md`). The
  Companion ends with `Replay finished: <n> ticks sent, 0 refused`, `<n>` the replayed ticks with at least one island
  (S1, S3, E10, N6, N7).
- AC2: While a replay is sent, followers of the server state are told of each tick as it is stored, the Companion
  shows as connected with the replayed game state, and as not connected after the end (S2).
- AC3: Each run sends one hello with a new profile identity, the name `Replay <yyyy-MM-dd HH:mm:ss>` of its local start
  time and the token of the file (no token without `--token-file`); a second run creates a second profile (S3, S5, N4).
- AC4: Each command line of E1 is invalid and opens no connection; a command line without `--server` replays as today
  without one (E1, S6, N9).
- AC5: A token file that cannot be read or holds no valid token by N4 ends with its E2 text before any connection,
  and a token with surrounding white space is sent trimmed (E2, N4).
- AC6: A recording whose opening or header fails ends with today's error before any connection (E3).
- AC7: Each server failure of E4 to E8 ends with its text, and the connection is closed; a `wss://` address is
  connected over TLS, and a certificate the operating system does not trust is `server unreachable` (S4, E4, E5, E6,
  E7, E8, N3, N5).
- AC8: A message acknowledged with a refusal, or refused by the writer, is counted as refused, the replay goes on, and
  the final text names both counts (E9).
- AC9: The next message is sent only after the previous acknowledgement, and a reply over 65,536 bytes is not read
  past that bound (N1, N2, E14).
- AC10: An invalid line after sent ticks, and `Exit` during the replay, close the connection with the ticks
  acknowledged before stored (E11, E12).
- AC11: No log line, status text or menu entry holds the token, the address or a profile or island name, and the
  log has the lines of N8 (N4, N8).
- AC12: manual — on Windows, the Product Manager follows the README and the user guide (English and German) to replay
  `long-session.jsonl` into a Docker server and into one started from source, and sees the pages filled (issue's
  criterion; E13, N5, N10).

## Out of scope

- Reconnecting, resending and buffering (#40, #41); sending live pipe data (#40).
- The catalog and the icons (#42): without a stored catalog the pages name regions and goods by their fallbacks
  (`Region <GUID>`, `Good <GUID>`, [`web-pages.md`](../../docs/areas/web-pages.md)).
- Server address and token in the settings file or a settings window (#40, #43); choosing an existing profile (#43,
  record 0041).
- Moving the receive times to the present; a separate command-line sender for Linux or a released tool of its own.

## Open questions

- CLI or Companion option? Answered: Companion switches with `--replay`. The issue allows either; the ZIP reaches
  external testers without the .NET SDK, record 0015 ships two deliverables (a released tool would be a third, and an
  unreleased one like `Censio.Anonymiser` needs the SDK), and `--replay`, `--speed` and the replay states exist.
- Where does the token come from? Answered: a file named on the command line, read by the server's rules (N4): not on
  the command line, where the process list, shell history and shortcuts keep it, and not in the settings file, whose
  server keys #40 and #43 design.
- Which profile? Answered: a new one per run (AC3): selecting profiles comes with #43 (record 0041), and the Companion
  has no profile yet (`ContractMessageConverter` takes one from its caller).
- Which receive times? Answered: the recorded ones, as every replay (record 0036; N7, E13).
- Which fixture can testers use? Answered: `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl` from the repository,
  or their own recording (N6); its facts are in that folder's `README.md`.

## Area documents

- New area `replay-sending`: the switches' meaning, the token file, the connection and its rules (N5), the hello and
  profile, what is sent and in which order, one message in flight, timeouts, the status texts and error reasons,
  counts, the log lines and privacy.
- [`companion.md`](../../docs/areas/companion.md): `--server` and `--token-file` in *Command line* with the invalid
  combinations, the new status texts in *Replay*, and its *Not here* pointing at the new area.
- [`pipe-recording.md`](../../docs/areas/pipe-recording.md), *Privacy*: a recording leaves the machine only as contract
  messages when the user replays it with `--server`.
- [`contract.md`](../../docs/areas/contract.md) and [`ingest.md`](../../docs/areas/ingest.md): a replay with
  `--server` sends the messages (scope and *Not here*).
- [`companion-log.md`](../../docs/areas/companion-log.md): the new lines.
