# Spec: Forward mode — live sending to a server with token and reconnect

Source: Issue #40
Status: Approved by Lead

## Problem

A player whose Censio server runs elsewhere (Docker on a NAS or a home server) gets no live data on its pages: today
only a replay with `--server` sends anything ([`replay-sending.md`](../../docs/areas/replay-sending.md)), it ends at the
first failure and retries nothing, and the settings file refuses `"mode": "forward"`
([`local-mode.md`](../../docs/areas/local-mode.md), *Switching on*). The live Companion reads the pipe but has no
sender (`App.xaml.cs` wires `PipeConnection` to the tray, diagnostics and catalog only). A server restart, a NAS reboot,
Wi-Fi loss or sleep of the gaming PC must not need the player's hand. Local mode has the same gap: its pages fill only
from a replay, and the Product Manager's answer on #39 left its live data to this feature (record 0101, *Consequences*).

## Scenarios

- S1: Given a running server and a settings file with `"mode": "forward"`, `"serverAddress": "ws://nas:8080/ingest"`
  and `"serverToken": "<the server's ingest token>"` (Q2), when the Companion starts, then the menu line reads
  `Server: connecting`, then `Server: connected`, without the game running.
- S2: Given S1 and the game running, when the Companion reads ticks, trade routes and session events from the pipe,
  then each reaches the server in the order read with its live receive time, and the open pages update live under the
  profile `Live` (Q1); the server shows the Companion as connected with the reported pipe state.
- S3: Given a connected Companion, when the server restarts (or the network drops, or the PC sleeps and wakes), then
  the line reads `Server: connection lost, retrying` and the Companion reconnects on its own; what waited is sent after
  the reconnect in order, and the line reads `Server: connected`.
- S4: Given the Companion starts before the network or the server is up (autostart at sign-in), when the server becomes
  reachable, then the next attempt connects; until then the line reads `Server: unreachable, retrying`.
- S5: Given local mode, when the hosted server reads `Server: running at …`, then the live data goes to it as in S2,
  with the token the process holds; the `Server:` line keeps local mode's texts.
- S6: Given forward mode and `--record`, then the Companion records and sends; given `--replay` or an invalid command
  line, no live connection is opened and the replay behaves as today.

## Errors and edge cases

- E1: `mode` `forward` with `serverAddress` missing, not a text, duplicated (also in other case) or breaking the
  `--server` address rules ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Switches*): `Server: settings
  invalid`, nothing sent; the game folder is still read. The warning `Server settings invalid: <problem>` replaces local
  mode's `Local server settings invalid: <problem>` for both modes; the problems are `mode is not local or forward`
  (replacing `mode is not local`), `mode occurs more than once`, the two `localPort` problems of today, `serverAddress
  is missing`, `serverAddress is not a ws:// or wss:// address` (also not a text), `serverAddress occurs more than
  once`, `serverToken is not a valid token` and `serverToken occurs more than once`. No problem quotes a value.
- E2: A `serverToken` that is not a text, duplicated, or not 32 to 1,024 visible ASCII characters after trimming
  space, tab, carriage return and line feed (the server's rule): as E1. Without `serverToken` the hello carries no token.
- E3: The server still holds the vanished connection (up to about 56 seconds, [`ingest.md`](../../docs/areas/ingest.md),
  *One Companion and silent connections*): `Server: another Companion is connected, retrying` until it frees the place.
- E4: Retryable failures, each with its text until the next accepted hello or another failure: `Server: unreachable,
  retrying` (also an untrusted `wss://` certificate), `Server: not answering, retrying`, `Server: connection lost,
  retrying`, `Server: invalid reply, retrying`, `Server: another Companion is connected, retrying`. The causes are those
  of the replay's reasons ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Limits and errors*).
- E5: More messages wait than the memory bound holds (a long outage): the oldest are dropped and the count is logged;
  the history shows a gap, never zeros.
- E6: `helloRefused` `tokenRefused`: `Server: token refused`; no retry until the Companion restarts.
- E7: `helloRefused` `unsupportedVersion`: `Server: does not support contract version 2`; no retry until restart.
- E8: An acknowledgement with a refusal: the message is not resent and a warning names its kind. A tick the contract
  writer refuses is not sent and is logged; a tick without islands has no message.
- E9: `Exit` in any connection state ends the process within 2 seconds (5 in local mode); waiting messages are lost and
  an unacknowledged one may or may not be stored.
- E10: In local mode a lost connection to the running hosted server is retried as in E4 without a menu text; a server
  that stopped (`Server: failed`) is not retried.
- E11: The network drops or the PC sleeps while no message is in flight: the dead connection is found by the bound of
  N3, the line reads `Server: connection lost, retrying` and the reconnect follows as in E4.
- E12: The player loads another savegame or playthrough: its ticks go to `Live` as well (record 0102). Islands with
  the same key (session GUID, island ID, area index; [`pipe.md`](../../docs/areas/pipe.md)) continue and rename each
  other's history, and the other savegame's islands stay listed without new values. No warning is shown; the remedy is
  deleting `Live`'s history on the pages ([`history-deletion.md`](../../docs/areas/history-deletion.md)).

## Non-functional requirements

- N1: Off by default: without `mode` `forward` (or `local`) nothing is sent live. The settings are read once at
  start (a change counts after a restart), keys ignore case, `serverAddress` and `serverToken` are read only with
  `mode` `forward`, and `localPort` only with `local`.
- N2: Memory is bounded: one message in flight, and waiting messages are bounded in count and bytes (values fixed in
  the plan); nothing is written to disk (the persistent buffer is #41).
- N3: The connection obeys the replay's rules ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Connection*,
  *Keep-alive*): direct, never through a system proxy, no redirect, no `Origin`, the system's certificate checks for
  `wss://`, 30-second bounds on the upgrade, the hello's answer, each send and each acknowledgement, a receive always
  pending after the accepted hello. A connection that delivers nothing, also while no message is in flight, is found
  dead within 60 seconds (the Companion's own keep-alive pings with a timeout; mechanism in the plan).
- N4: Retry delays 1, 2, 4, 8, 16 and then 30 seconds; the counter resets after a connection stayed accepted for 60
  seconds. A token or version refusal is never retried, so the server's block of failing hosts is never reached.
- N5: Privacy: the token appears only in the hello — never in a log line, menu or tooltip text or the diagnostics
  window. No log line holds the address, a name or an exception text. The figure name never leaves the PC (record
  0041); island names, the economy and trade routes go to the configured server, over `ws://` unencrypted.
- N6: Log lines are written when the state changes, not per retry, so a long outage adds a bounded number of lines:
  the settings warning of E1, a line for an accepted hello, for each change of failure reason, for a refused hello,
  for a refused message (its kind) and for dropped messages (their count); exact texts in the plan.
- N7: Windows 10 22H2 and Windows 11 x64 as the Companion ([`companion.md`](../../docs/areas/companion.md),
  *Platform*); the server of this release (contract version 2).

## Acceptance criteria

- AC1: `"mode": "forward"` with a valid `serverAddress` (and optional `serverToken`) starts live sending; invalid
  values show `Server: settings invalid`, log `Server settings invalid: <problem>` with a problem of E1, send nothing
  and keep the game folder; `forward` is no longer an invalid `mode` (S1, E1, E2, N1).
- AC2: At start, without waiting for the game, the Companion connects and sends one hello with contract version 2,
  its version, the token and the fixed profile (one identity built into the Companion, the same on every start,
  named `Live`, Q1); the line reads `Server: connecting`, then `Server: connected` (S1, N7).
- AC3: After the accepted hello every live tick with islands, trade-routes item, session start and first session end
  is sent in the order read with its live receive time and a new identity, one in flight; a `status` follows the
  accept and each pipe state change (S2, E8).
- AC4: Each retryable failure shows its text of E4 and is retried with the delays of N4; a server restart, a network
  loss, a connection that goes silent with no message in flight (found within 60 seconds) and a held old connection
  end in `Server: connected` with no action (S3, S4, E3, E4, E11, N3, N4).
- AC5: Waiting messages stay in memory in order across a reconnect within the bound of N2, the unacknowledged one
  first with its identity; beyond the bound the oldest are dropped and their count is logged (S3, E5, N2).
- AC6: `tokenRefused` and `unsupportedVersion` show their texts of E6 and E7 and are not retried (E6, E7).
- AC7: The connection follows N3: no proxy, no redirect, no `Origin`, system certificate checks, the 30-second bounds,
  pings answered while no message is sent, a dead idle connection found within 60 seconds (N3, E11).
- AC8: In local mode, once the hosted server runs, live data goes to `ws://127.0.0.1:<port>/ingest` with the token
  held in the process and reconnects as in E10; the `Server:` line is unchanged (S5, E10).
- AC9: `--record` sends; `--replay` and an invalid command line open no live connection; `Exit` keeps its time
  bound in every connection state (S6, E9).
- AC10: The token, the address and exception texts are in no log line or menu text; the log lines change with the
  state only (N5, N6).
- AC11: `README.md` and `docs/USER_GUIDE.md` (English, then German) describe forward mode — settings, where the token
  comes from, the `Server:` texts, the profile, what is sent and lost — so an external tester can try it; the user
  guide warns that until #43 every playthrough goes to `Live`, so testers use one playthrough per server or delete
  `Live` between playthroughs, and not to share `settings.json`, which holds the token (S1, E12).

## Out of scope

- The persistent buffer, its age and size bounds and the dropped-tick counts in the tray (#41); N2's memory bound is
  replaced by it there.
- The catalog's names and icons on the server (#42): the pages show the fallbacks of
  [`web-pages.md`](../../docs/areas/web-pages.md) until then.
- Choosing or creating profiles, the figure-name notice, and editing the settings in a window (#43).
- A replay in forward mode without `--server` (it stays offline; `--server` and `--token-file` keep working).
- The server connection in the tooltip or the icon look: they keep showing the pipe, as in local mode.

## Open questions

- Q1: Which profile do live ticks go to before #43 lets the player pick one? Answered: (Product Manager) one fixed
  profile identity built into the Companion, the same on every start, with the name `Live`; the history continues
  across restarts and updates without the Companion writing a file, and one Companion per server (record 0020) keeps it
  unique. #43 replaces it with the player's choice. Records 0020 and 0041 are amended for that time by record
  0102 (Challenge, 1).
- Q2: Where does the token live? Answered: (Product Manager) the key `serverToken` in `settings.json`, read with
  `mode` `forward` only, under E2's rule; the user guide warns not to share the file, which now holds a credential.
- Q3: Contract version — the issue says "contract v1". Answered: the current contract is version 2
  (`ContractReader.CurrentVersion`, [`contract.md`](../../docs/areas/contract.md)); a version-1 server refuses (E7).
- Q4: Live data into local mode? Answered: (Product Manager) in scope, kept in #40, as the answer on #39 left it
  (`docs/ARCHITECTURE.md`, `local-mode.md`, record 0101).
- Q5: How is the connection shown? Answered: (Lead) the menu line `Server: <state>` that local mode introduced; the
  tooltip and icon keep the pipe ([`local-mode.md`](../../docs/areas/local-mode.md), *Menu*).
- Q6: What happens to data during an outage? Answered: (Lead) bounded in memory until #41 (AC5).

## Area documents

- New `docs/areas/live-sending.md`: the keys `serverAddress` and `serverToken`, the start and hello, the profile, what
  is sent, the queue and its bound, retry and delays, the `Server:` texts of forward mode, local mode's live sending,
  the log lines, privacy, accepted risks; links `replay-sending.md` for the shared connection rules.
- `companion.md`: the settings file's new keys, the `Server:` line also in forward mode. `local-mode.md`: `forward` no
  longer invalid, live data now sent. `replay-sending.md`, `contract.md`, `ingest.md`: their "#40" pointers replaced by
  links. `companion-log.md`: the new lines and the renamed settings line.
- `.squad/project.md` (written by the orchestrator from the Dev's text): *Security areas*, *Outbound calls*, gets the
  live connection to `serverAddress` and to `127.0.0.1` in local mode, the parsing line `serverAddress` and
  `serverToken`; *Integration surface* the new keys. `SECURITY.md`: the token's place in `settings.json` and live data
  over `ws://` unencrypted, as replay-sending's *Accepted risks* states for a replay.

## Challenge

1. Major — the fixed profile `Live` contradicts released records 0020 and 0041. Accepted: the Product Manager's
   answer stays; record 0102 (`Proposed`) amends both until #43 and states the consequence. It amends rather than
   supersedes them: both hold again once #43 lets the player choose (decisions README, *Rules*), so they stay `Accepted`. Added E12 (mixed islands,
   no warning, remedy deleting `Live`) and the user-guide warning in AC11.
2. Minor — `.squad/project.md` and `SECURITY.md` missing. Accepted: both named under *Area documents*.
3. Minor — a dead idle connection is not found. Accepted: N3 bounds it at 60 seconds, E11 added, AC4 and AC7 name it.
4. Minor — settings log texts unnamed. Accepted: E1 names the warning `Server settings invalid: <problem>`, which
   replaces local mode's line (unreleased: local mode came after `v0.4.0`), and every problem text; AC1 refers to it.
