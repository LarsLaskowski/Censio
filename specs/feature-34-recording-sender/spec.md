# Spec: Send recordings to a server for testing

Source: Issue #34
Status: Draft

## Problem

A tester who runs the Censio server cannot fill its pages without a Companion that sends data, and none sends yet
(forward mode is #40). The Companion replays a recording only locally (`--replay`, `--speed`,
[`companion.md`](../../docs/areas/companion.md), *Replay*), so without the game nobody can try ingest, storage and
pages end to end through the real contract.

## Scenarios

- S1: Given a server of the state of `main` with its token required and the token saved in a text file, when the tester
  starts `Censio.Companion.exe --replay long-session.jsonl --server ws://<host>:8080/ingest --token-file token.txt
  --speed max`, then the tooltip reads `Censio Companion: Replay: Connecting to server`, then the replay states of today
  (`Replay: Connected`, `Replay: Receiving`), and at the end `Censio Companion: Replay finished: <n> ticks sent, 0
  refused`. The server's pages show the profile `Replay <yyyy-MM-dd HH:mm:ss>` (the Companion's local start time)
  with the regions, islands and goods of the recording.
- S2: Given a data page open in a browser, when the tester replays at a speed such as `10`, then the page updates with
  each tick the Companion sends (a replayed tick closes when the next frame is read, record 0036), the Companion banner shows the Companion connected with the replayed game state
  while the replay runs, and not connected after `Replay finished`.
- S3: Given a server with the token switched off (`Censio:Ingest:RequireToken` `false`), when the tester replays with
  `--server` and without `--token-file`, then the replay is accepted as in S1.
- S4: Given a reverse proxy with TLS, when the tester gives a `wss://` address, then the replay is sent over TLS.
- S5: When the tester replays again, a second profile `Replay <time>` is created; the first stays.
- S6: Given a start with `--replay` and without `--server`, then the Companion replays exactly as today and opens no
  network connection.

## Errors and edge cases

Each `Replay error: <reason>` is the tooltip's status text with the *problem* look until `Exit`; nothing is retried.

- E1: Invalid command line: `--server` without `--replay`; `--token-file` without `--server`; a switch given twice;
  `--server` or `--token-file` without a value, with an empty one or with a switch in its place; `--server` together
  with `--record`; an address that is not an absolute `ws://` or `wss://` address with a host, or one that holds a
  user name, a password or a fragment: `Invalid command line` as today, nothing replayed or sent.
- E2: The token file cannot be read (missing, a folder, no access): `Replay error: token file cannot be read`. It holds
  no valid token (by the rules of N4, also a file in an encoding N4 does not name): `Replay error: token file holds no
  valid token`. The server is not contacted.
- E3: The recording cannot be opened or its header is invalid: today's error
  ([`pipe-recording.md`](../../docs/areas/pipe-recording.md), *Limits and errors*), before the server is contacted, so
  no profile is created.
- E4: No WebSocket at that address (unknown host, closed port, an HTTP answer such as the image `0.2.0`'s, a redirect,
  a TLS failure, no upgrade within 30 seconds): `Replay error: server unreachable`.
- E5: The server refuses the hello: a wrong or missing token `Replay error: token refused`; another Companion is
  connected `Replay error: another Companion is connected`; another contract version `Replay error: server does not
  support contract version 1`.
- E6: After the upgrade succeeded, no `helloAccepted` or `helloRefused` within 30 seconds of the hello, or no
  acknowledgement within 30 seconds of a message: `Replay error: server does not answer`; the connection is closed.
- E7: The connection ends after the hello (the server stops, the network fails, a close frame arrives): `Replay
  error: server connection lost`. What the server acknowledged before stays stored; nothing is resent.
- E8: A frame from the server that is not a readable contract message, is binary, is larger than 65,536 bytes, is of
  a type the server does not send at that point, or acknowledges another message than the one awaited, also one that
  arrives while nothing is awaited: `Replay error: invalid server reply`; the connection is closed.
- E9: An acknowledgement with a refusal, or a tick the contract writer refuses (over a bound): counted as refused,
  the replay goes on (`Replay finished: 30 ticks sent, 1 refused`).
- E10: A replayed tick without islands has no message (the contract requires one to 256 islands,
  [`contract.md`](../../docs/areas/contract.md), *Value rules*) and is counted neither as sent nor as refused. A tick
  whose islands carry no entries is sent like any other.
- E11: A later line is invalid or cannot be read: the ticks before it are sent, then the connection is closed with
  today's error (`Replay error: invalid line <n>`).
- E12: `Exit` during the replay closes the connection at once; an unacknowledged message may or may not be stored.
- E13: A profile with newer ticks than the recording (a fixture's times lie 30 to 365 days back) stays the one a page
  shows by default; the tester picks `Replay <time>` in the profile switch
  ([`server-state.md`](../../docs/areas/server-state.md)). The as-of line shows the recorded time.
- E14: A slow server slows the replay (N1); the receive times sent stay the recorded ones.

## Non-functional requirements

- N1: One message in flight: a tick, session start or end is sent only after the previous one was acknowledged, so
  at most one message and one reply are held. A `status` (no reply) is sent without waiting, after `helloAccepted` and
  at each replayed state change, and is not counted.
- N2: Messages are written by the contract's writer and replies read by its reader
  ([`contract.md`](../../docs/areas/contract.md)); a reply is read up to 65,536 bytes and never further (E8).
- N3: Timeouts: 30 seconds for the upgrade (E4), then 30 seconds for the hello reply and for each acknowledgement
  (E6).
- N4: The token is read once, from the file named by `--token-file`, by the server's own rules: at most 65,536 bytes,
  surrounding space, tab, carriage return and line feed trimmed, then 32 to 1,024 visible ASCII characters (`!` to
  `~`) ([`ingest.md`](../../docs/areas/ingest.md), *Token*), so a copy of the server's `ingest-token` works. Unlike the
  server, the file may also start with a UTF-8 byte order mark, or be UTF-16 (little or big endian) with its byte order
  mark (Windows PowerShell 5.1's `>`), decoded by that mark before the trimming; without one it is UTF-8. The token
  is never on the command line, in a log line, status text or any message but the hello. Without `--token-file` the
  hello carries no token.
- N5: The connection: the address as given, directly, never through a configured system proxy, no redirect followed, no `Origin` header (the server refuses one), no
  subprotocol; `wss://` certificates checked by the operating system, never skipped. Over `ws://` token and data
  cross unencrypted; the user guide says so (record 0006).
- N12: Keep-alive: from `helloAccepted` until the close the Companion always has a receive pending, so it answers the
  server's pings at once ([`ingest.md`](../../docs/areas/ingest.md), *One Companion and silent connections*) and the
  connection stays open through any pause of the recording (the fixture has one of about 18 minutes). A close frame
  in that receive is E7, any other frame not awaited E8.
- N6: What crosses is the contract: a hello with the profile `Replay <yyyy-MM-dd HH:mm:ss>` and a new profile identity
  per run, the ticks, session starts and session ends of the recording in replay order, and the replayed pipe state as
  status messages. A session event's `receiveTime` is the recorded read stamp of its frame; a repeated session end
  ([`pipe.md`](../../docs/areas/pipe.md), which carries no new information) is not sent. No figure name, catalog or
  icon is sent; the user guide says to replay a raw recording only into one's own server.
- N7: Receive times are the recorded ones and the pacing is `--speed` as today
  ([`pipe-recording.md`](../../docs/areas/pipe-recording.md), *Replay*).
- N8: The log ([`companion-log.md`](../../docs/areas/companion-log.md)) gets the connection, a refused hello with its
  reason, a lost connection, an invalid reply and the end with its counts, never the token, the address or message
  content.
- N9: Without `--server` no network connection is opened (S6). The logic is platform-neutral and tested on Linux.
- N11: The documented environment: a Windows 10 or 11 PC with the Companion (ZIP or source) and Windows PowerShell
  5.1 or `cmd`; the server in Docker Desktop there, in Docker on a network host (a NAS), or from source on the PC.
  The fixture is a direct download of `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl`; the token a byte-exact
  copy (`docker cp censio:/config/ingest-token <file>`, the host file, or `<data directory>\ingest-token`) or pasted
  into Notepad (N4). The guide gives full paths (Explorer and shortcuts set the working folder). A server from source
  needs `Censio:DataDirectory` set (else every Companion is refused) and is at `ws://localhost:5000/ingest`.
- N10: Needs a server with the contract version 1 ingest (not the image `0.2.0`); no server change.

## Acceptance criteria

- AC1: A replay of `long-session.jsonl` with `--server` leaves the server's profile `Replay <time>` showing the
  regions, islands, names and goods the fixtures' `README.md` states for `ServerState` (three regions; 13, 9 and 2
  islands; after the final tick without data the 24 islands without values). The Companion ends with `Replay finished: <n> ticks sent, 0 refused`, `<n>` the replayed ticks with at least one island
  (S1, S3, E10, N6, N7).
- AC2: Followers of the server state are told of each sent tick, and the Companion shows as connected with the
  replayed game state until the end, then not connected (S2).
- AC3: Each run sends one hello with a new profile identity, the name `Replay <yyyy-MM-dd HH:mm:ss>` of its local start
  time and the token of the file (no token without `--token-file`); a second run creates a second profile (S3, S5, N4).
- AC4: Each command line of E1 is invalid and opens no connection; a command line without `--server` replays as today
  without one (E1, S6, N9).
- AC5: A token file that cannot be read or holds no valid token by N4 ends with its E2 text before any connection; a
  token with surrounding white space is sent trimmed, and one in UTF-8 with a byte order mark and in UTF-16 little and
  big endian with theirs is sent as the same token (E2, N4).
- AC6: A recording whose opening or header fails ends with today's error before any connection (E3).
- AC7: Each server failure of E4 to E8 ends with its text, and the connection is closed; a `wss://` address is
  connected over TLS, a certificate the operating system does not trust is `server unreachable`, and a configured
  system proxy is not used (S4, E4, E5, E6, E7, E8, N3, N5).
- AC8: A message acknowledged with a refusal, or refused by the writer, is counted as refused, the replay goes on, and
  the final text names both counts (E9).
- AC9: The next tick or session event is sent only after the previous acknowledgement, a `status` message is sent
  at `helloAccepted` and each change of the replayed state without waiting, a repeated session end is not sent and a
  session event carries its frame's recorded stamp, and a reply over 65,536 bytes is not read past that bound (N1, N2,
  N6, E14).
- AC10: An invalid line after sent ticks, and `Exit` during the replay, close the connection with the ticks
  acknowledged before stored (E11, E12).
- AC11: The log has the lines of N8, and no log line or status text holds the token, the address or a name (N4, N8).
- AC12: manual — on Windows, the Product Manager follows the README and the user guide (English and German) to replay
  `long-session.jsonl` at `--speed 10` into a Docker server and into one started from source, with the token file
  made as documented, and sees the pages filled and updated while the replay runs (issue's criterion; S2, E13, N5,
  N10, N11).
- AC13: A message sent more than 60 seconds after the previous acknowledgement is acknowledged on the same
  connection, measured on the real server, since .NET's keep-alive runs on no fake clock (N12, E7).

## Out of scope

- Reconnecting, resending and buffering (#40, #41); sending live pipe data (#40).
- The catalog and the icons (#42): without a stored catalog the pages name regions and goods by their fallbacks
  (`Region <GUID>`, `Good <GUID>`, [`web-pages.md`](../../docs/areas/web-pages.md)).
- Server settings (#40, #43); choosing an existing profile (#43); receive times moved to the present; a separate
  sender tool.

## Open questions

- CLI or Companion option? Answered: Companion switches. The issue allows either; the ZIP needs no .NET SDK, and record
  0015 ships two deliverables.
- Token source? Answered: a file (N4); the command line leaks into process lists and history, and the settings keys
  are #40's and #43's.
- Profile? Answered: a new one per run (AC3); selection comes with #43 (record 0041).
- Receive times? Answered: the recorded ones (record 0036).
- Fixture? Answered: `long-session.jsonl` (N11) or the tester's own recording (N6).

## Area documents

- New area `replay-sending`: switches, token file, connection rules, keep-alive, hello and profile, what is sent, one
  message in flight, timeouts, status texts, counts, log lines, privacy.
- [`companion.md`](../../docs/areas/companion.md): the switches in *Command line*, the status texts in *Replay*.
- README and `docs/USER_GUIDE.md` (English, then German): the steps of N11 and the warnings of N5 and N6.
- [`pipe-recording.md`](../../docs/areas/pipe-recording.md) (*Privacy*), [`contract.md`](../../docs/areas/contract.md),
  [`ingest.md`](../../docs/areas/ingest.md), [`companion-log.md`](../../docs/areas/companion-log.md): the sending, the
  new lines.

## Challenge

Spec challenge by the Devil's Advocate (2 major, 6 minor); every objection accepted.

- M1 (keep-alive): accepted. N12 (receive always pending; close E7, other frames E8), AC13 (gap over 60 seconds,
  measured), AC12 at `--speed 10` (the 18-minute pause becomes about 108 seconds).
- M2 (tester environment): accepted. N11 (PC, shells, three server placements, fixture and token transfer, server
  from source), N4 and AC5 (byte order marks), README and user guide steps, AC12 follows them. The environment is a
  Lead decision from the issue's audience; the manual run confirms it.
- m1 (status messages): accepted. N1 and AC9: sent without waiting at `helloAccepted` and each state change, not
  counted.
- m2 (repeated session end, stamps): accepted. N6 and AC9: not sent; session events carry their frame's recorded
  stamp.
- m3 (timeouts overlap): accepted. E4 is the upgrade within 30 seconds; E6 starts after the upgrade; N3 says so.
- m4 (live wording): accepted. S2 promises an update per tick sent, which closes when the next frame is read.
- m5 (download and paths): accepted. N11: direct download of the fixture, full paths in the guide and why.
- m6 (system proxy): accepted. N5 and AC7: the connection goes directly, never through a system proxy, so the token
  goes to no third host.
