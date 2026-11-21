# Spec: Local mode — the Companion hosts the server in its own process

Source: Issue #39
Status: Draft

## Problem

A player without a home server or NAS cannot use the web pages and the history today: the server ships only as a
Docker image ([record 0015](../../docs/decisions/0015-two-deliverables-and-local-mode.md)), and running Docker on the
gaming PC is more than most players will do. Record 0015 already decided that the Companion can host the same server in
its own process for local use; this feature builds it. Today the Companion reads the pipe and shows the tray and the
diagnostics window, but opens no listener and sends nothing live: the pages fill only from a replay sent with
`--server` (`README.md`, *What works*; [replay-sending](../../docs/areas/replay-sending.md)). Live sending is #40, the
catalog upload #42.

## Scenarios

- S1: Given `%LOCALAPPDATA%\Censio\settings.json` holds `"mode": "local"` and no `localPort`, when the player starts
  the Companion from the extracted ZIP, then the menu shows the greyed line `Server: starting` below `Catalog: …`, then
  `Server: running at http://localhost:5000/`, and `Open Censio`, directly above `Open diagnostics`, becomes enabled;
  the start page shows the Companion's version.
- S2: Given the server runs, when the player chooses `Open Censio`, then the default browser opens
  `http://localhost:<port>/`; the menu does not wait for the browser.
- S3: Given the settings file also holds `"localPort": 5117`, when the Companion starts, then the server listens on port
  5117 and the menu line and `Open Censio` use that port.
- S4: Given the first start in local mode, then the server creates the folder `%LOCALAPPDATA%\Censio\Server` with its
  database `censio.db`, its data-protection key ring and its ingest token file `ingest-token`, under the rules of
  [server](../../docs/areas/server.md), [server-data](../../docs/areas/server-data.md) and
  [ingest-token](../../docs/areas/ingest-token.md); a later start opens them again.
- S5: Given local mode and no Companion running, when the player starts `Censio.Companion.exe --replay <file>`
  without `--server`, then the replay shows `Replay: Connecting to server` until the hosted server runs and has
  answered the hello, sends to it as [replay-sending](../../docs/areas/replay-sending.md) states with the server's
  token taken in the process (no token file), and the pages show the profile `Replay <time>`. With `--server` the
  replay goes to that address as today.
- S6: Given data was stored, when the player chooses `Exit`, then the icon disappears at once, the server stops and
  the process ends with exit code 0 within 5 seconds; after the next start in local mode the pages show the same
  profiles and history.
- S7: Given no settings file, or one without `mode`, when the Companion starts, then it behaves exactly as today: no
  listener, no `Server:` line, no `Open Censio` entry.
- S8: Given local mode, when the Companion runs with `--record` or `--replay`, then the server is hosted as in S1; with
  an invalid command line it is not, and neither line nor entry is shown.

## Errors and edge cases

- E1: The port is taken on `127.0.0.1` or on `[::1]` (another program, a standalone `Censio.Server`, the Companion of
  another signed-in Windows user): the line reads `Server: port <port> in use`, `Open Censio` stays greyed, the rest
  works as without local mode, and the reason is logged.
- E2: `mode` is a text other than `local` (letter case ignored; `forward` too until #40 defines it; a blank text
  counts as no `mode`), or not a text, or occurs twice (also in different case); or, with `mode` `local`, `localPort`
  is not an integer literal from 1 to 65535 (`5117.0`, `5.117e3`, `"5117"`, `null`, 0 and 65536 are refused) or occurs
  twice. Without `mode` `local`, `localPort` is not read. Then no server is started, the line reads `Server: settings
  invalid` and the reason is logged as a warning. The game folder is still used: a wrong value of these keys does not
  make the file invalid for `gameFolder`.
- E3: The settings file as a whole is invalid (too large, not JSON, unreadable): `Game: settings file invalid` as
  today, no server starts and no `Server:` line is shown.
- E4: Any other start failure of the server — the folder cannot be created or written, the database was written by a
  newer Censio version, is not a Censio database or cannot be updated, the key ring or the token file cannot be
  written: the line reads `Server: failed`, `Open Censio` stays greyed, the server's own `Critical` line (the texts of
  [server](../../docs/areas/server.md), [server-data](../../docs/areas/server-data.md),
  [ingest-token](../../docs/areas/ingest-token.md)) is written to the Companion's log, and the Companion keeps
  running. Nothing in the folder is repaired or deleted.
- E5: The server stops on its own while the Companion runs: the line reads `Server: failed`, the reason is logged, and
  the Companion does not restart it; a restart of the Companion tries again.
- E6: `Open Censio` cannot start a browser: a warning is logged, nothing else changes.
- E7: A request whose `Host` is not `localhost`, `127.0.0.1` or `[::1]` (with any port) — a page of another site that
  points its own name at `127.0.0.1` — is answered `400`.
- E8: Sign-out or shutdown ends the Companion without a dialog as today; the server is stopped as far as Windows allows,
  and every message the server acknowledged is in the database at the next start
  ([record 0068](../../docs/decisions/0068-ingest-acknowledges-a-message-only-after-it-is-stored.md)).
- E10: A replay without `--server` in local mode whose server does not start (E1, E2, E4) ends with `Replay error:
  local server not running`; one whose server stops during the replay ends with the reason of a lost connection
  ([replay-sending](../../docs/areas/replay-sending.md)). Nothing is retried.
- E11: IPv6 is switched off on the PC, so `[::1]` does not exist: the server runs on `127.0.0.1` alone. `127.0.0.1`
  that cannot be bound for any reason but a taken port counts as E4.
- E9: `Exit` while the server is still starting, or while it stores a message, runs the retention job or deletes a
  profile: the process still ends within 5 seconds; a write in progress is completed or rolled back, never half
  stored.

## Non-functional requirements

- N1: The server listens on the loopback addresses only (`127.0.0.1` and `[::1]`), plain HTTP; Windows asks for no
  firewall permission and other devices of the network cannot reach it.
- N2: The guarantees of `.squad/project.md` hold unchanged in local mode: the ingest token is required (generated into
  the data folder), the pages need no login, history retention and per-profile deletion work as on the image
  (`localhost` is a trusted host for deletion, [web-pages](../../docs/areas/web-pages.md)).
- N9: Accepted risk, stated in `local-mode.md`, `README.md` and the user guide: every program and every other
  signed-in user of the same PC can reach the loopback port, read every profile and delete history
  ([record 0006](../../docs/decisions/0006-server-plain-http-and-open-health.md)); without the token, which lies in the
  player's private folder, none of them can send data.
- N3: The data folder `%LOCALAPPDATA%\Censio\Server` is fixed, private to the Windows user like the other Companion
  folders, and kept when the Companion is removed. The database is never kept in memory in local mode.
- N4: Compatibility: without `mode` nothing changes for existing players (S7); the settings file is still never
  written by the Companion, a change counts after a restart, and the reading rules of `gameFolder` are unchanged.
- N5: The tray, the menu, the pipe and `Exit` never wait for the server's start, its stop or the browser; the start
  of the server does not delay the tray icon.
- N6: Target host of local mode (Product Manager): Windows 11 x64 only, with no installed .NET, no installer, no
  administrator rights and a default browser. The release ZIP keeps its form: one self-contained file
  `Censio.Companion.exe`, which also serves the pages' assets. The Companion's platform does not
  change: on Windows 10 22H2 it still runs as [companion](../../docs/areas/companion.md) (*Platform*) states. There,
  local mode is not supported: it is neither tried nor refused, and the README and the user guide say so.
- N7: The server's log entries of level warning and above go to the Companion's log file under the rules of
  [companion-log](../../docs/areas/companion-log.md) (length cuts, sanitizing, nothing it forbids); the token is never
  logged.
- N8: `README.md` and `docs/USER_GUIDE.md` describe local mode, English first, then German, so external testers can
  try it (the issue's *Documentation*).

## Acceptance criteria

- AC1: The settings file's `mode` and `localPort` are read as S1, S3, S7, E2 and E3 state: local mode only with `mode`
  `local` (case ignored, blank as absent), port 5000 by default, `localPort` read only then, every invalid value or
  duplicate key yields `Server: settings invalid` without affecting `gameFolder` (S1, S3, S7, E2, E3, N4).
- AC2: In local mode the Companion hosts the server on `127.0.0.1` and `[::1]` at the port (`127.0.0.1` alone without
  IPv6), with the data folder
  `%LOCALAPPDATA%\Censio\Server`, the token required, and the menu shows `Server: starting`, then `Server: running at
  http://localhost:<port>/` (S1, S3, S4, S8, E11, N1, N2, N3, N5).
- AC3: `Open Censio` is shown only in local mode, enabled only while the line reads `Server: running …`, and opens
  `http://localhost:<port>/` in the default browser; a failure is logged (S2, E6, N5).
- AC4: A start failure shows `Server: port <port> in use` (port taken on either address) or `Server: failed` (every
  other reason and a stop while running), logs the reason, and leaves pipe, recording, catalog and replay working (E1,
  E4, E5, N7).
- AC5: `Exit` stops the server and ends the process with exit code 0 within 5 seconds, also while the server starts or
  writes; data stored before is shown after the next start in local mode (S5, S6, E8, E9).
- AC6: A request to the hosted server whose `Host` is not `localhost`, `127.0.0.1` or `[::1]` is answered `400` (E7).
- AC7: The release ZIP, extracted on a Windows 11 x64 machine without .NET, runs S1, S2, S5 and S6 with no further
  installation — manual, by the Product Manager on a build of the branch (N6).
- AC8: `README.md` and `docs/USER_GUIDE.md` describe local mode in English, then German: the two keys, the menu line
  and `Open Censio`, the data folder, the replay into it (S5) and the accepted risk of N9 — checked by the Reviewer
  (N8, N9).
- AC9: In local mode a replay without `--server` waits for the hosted server, sends to it with the token taken in the
  process and fills its pages; a server that does not start ends it with `Replay error: local server not running`;
  with `--server` it behaves as today (S5, E10).
- AC10: The release check and the CI dry run of the Companion ZIP fail when the published `Censio.Companion.exe` does
  not carry the Blazor script (`_framework/blazor.web*.js`) and the server's static assets — CI (N6).

## Out of scope

- Live data from the game into the local server, and the catalog's names and icons on its pages: #40 and #42 (Open
  question 1).
- Reaching the local server from another device (a LAN address, a firewall rule) and TLS.
- A settings window or a menu switch for the mode and the port (#43); a menu entry for the data folder.
- Restarting a failed server without restarting the Companion (E5).
- The tooltip and the icon looks: they keep showing the pipe connection only.
- Local mode on Windows 10 22H2: unsupported, neither tried nor refused (N6).

## Open questions

1. What does local mode show from the running game in this feature? Live sending is #40 and the catalog upload #42,
   neither a dependency of #39. Answered: (Product Manager, option B) #39 hosts the server and adds `Open Censio`.
   Its pages fill from a replay into the hosted server (S5). #40 later sends live data to the local
   server as well, and #42 adds the catalog.
2. How is local mode switched on before the settings window? Answered: (Lead, record 0045) keys `mode` and `localPort`
   in the existing settings file, which #43 will write; a command-line switch would be lost at autostart.
3. Which default port? Answered: (Lead) 5000, the server's own default outside the image
   ([server](../../docs/areas/server.md), *Listen address*), configurable as the issue asks.
4. Which addresses does it listen on? Answered: (Lead) loopback only (N1), the closed default for the security area
   *Network exposure*, confirmed by the Product Manager ("nur lokaler Server"); LAN access is out of scope.
5. Is the ingest token still required? Answered: (`.squad/project.md`, *Guarantees*, record 0019) yes, unchanged.
6. What does the target host have? Answered: (Product Manager, "nur ab Windows 11 x64") Windows 11 x64 only (N6).
   This is narrower than [companion](../../docs/areas/companion.md) (*Platform*) and `README.md`, which name
   Windows 10 22H2 and Windows 11 for the Companion; no decision record fixes Windows 10. The spec applies the answer
   to local mode only. Dropping Windows 10 for the whole Companion would change its platform for every player, which
   is outside #39.

## Area documents

- New `docs/areas/local-mode.md` ([companion](../../docs/areas/companion.md) has 3,389 of 5,000 words): the keys and
  their errors, addresses and host rule, data folder, menu texts, start, stop, failures, logging.
- `docs/areas/companion.md`: the menu order with the two new items (*Tray icon and menu*), the new keys in *Settings
  file* and `Exit` within 5 seconds in local mode, each linking `local-mode.md`.
- `docs/areas/server.md` (*Listen address*, *Data directory*): that the server also runs inside the Companion, linking
  `local-mode.md`; `docs/ARCHITECTURE.md` describes local mode (record 0015, *Consequences*).
- `docs/areas/replay-sending.md`: the replay without `--server` in local mode, its wait and its new reason (S5, E10).

## Challenge

Spec challenge at f37b489 (Devil's Advocate, 2 major, 4 minor).

1. Major, S5 cannot pass: the token file does not exist on the first start, and the replay connects before Kestrel
   listens. Checked: `replay-sending.md` (*Order of a run*) reads the token first and retries nothing; `Program.cs`
   creates the key ring, then the token, and listens only at `RunAsync`. Accepted, with the simpler alternative: in
   local mode a replay without `--server` waits for the hosted server and sends to it with the token taken inside
   the process (S5, E10, AC9). There is no token-file path to type and no race. `--server` keeps today's behavior.
   Sending to the own port with `--server` is not the documented path, and its outcome stays today's: a replay that
   comes too early ends with `server unreachable` or `token file cannot be read`.
2. Major, no criterion catches a ZIP whose pages do not load. Checked: `release-check.sh` `companion` checks the
   bundled dll, the version and the one-file form only, and `ci.yml` (`release-check`) runs that check as a dry run.
   Accepted: AC10 adds the Blazor script and the server's static assets to that check, run in CI on every PR and in
   the release.
3. Minor, E2 and S7 disagree on `localPort` without `mode`. Accepted: `localPort` is read only with `mode` `local`,
   and a blank `mode` counts as absent, as a blank `gameFolder` does (E2, AC1).
4. Minor, "whole JSON number" is ambiguous. Accepted: only an integer literal is accepted; `5117.0` and `5.117e3` are
   refused (E2).
5. Minor, one loopback address that cannot be bound. Accepted: without IPv6 the server runs on `127.0.0.1` alone. A
   port taken on either address is `port <port> in use`, and any other bind failure is `failed` (E1, E11, AC2, AC4).
   Starting on one address while the other is taken was rejected: the browser's `localhost` could reach the other
   program.
6. Minor, other Windows users of the same PC. Checked: `DeletionHostRule.IsTrusted` trusts `localhost` and every IP
   address. Accepted as a stated risk (N9, AC8), as record 0006 accepts it for the image's network. Sending data
   still needs the token in the player's private folder.
