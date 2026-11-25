# Local mode

## Scope

Local mode: the Companion hosts the Censio server in its own process for the player's PC alone. It covers the settings
`mode` and `localPort`, the addresses the hosted server listens on and its host rule, the data folder, the menu line
`Server:` and the entry `Open Censio`, start, stop and failures with their texts, what is logged, the accepted risk of
other users of the PC and the target host. A player without a home server or NAS and a tester who wants the pages
without Docker rely on it.

## Behavior

### Switching on

- Local mode is off by default. It runs only when the settings file `%LOCALAPPDATA%\Censio\settings.json`
  ([`companion.md`](companion.md), *Settings file*) holds `"mode": "local"`. The file is read once per start; the
  settings window writes it and restarts the Companion for a change of these keys
  ([`companion-settings.md`](companion-settings.md)), a change made by hand counts after a restart. Without the key the Companion behaves as without local mode: no
  listener, no `Server:` line, no `Open Censio` entry.
- `localPort` is read only with `mode` `local`: an integer literal from 1 to 65535, default 5000. `5117.0`, `5.117e3`,
  `"5117"`, `null`, a negative number, 0 and 65536 are refused.
- The keys are matched ignoring case, and `local` is matched ignoring case. A blank `mode` counts as no `mode`.
- A `mode` that is not a text, a text other than `local` and `forward` or that occurs twice (also in different case),
  and, with `mode` `local`, an invalid or duplicated `localPort`, `bufferHours` or `bufferMegabytes` ([`live-sending.md`](live-sending.md), *Switching on*), start no server. The menu line reads `Server: settings
  invalid` and the reason is logged as a warning (*Log*). The game folder is still read: a wrong value of these keys does
  not make the file invalid for `gameFolder`.
- `"mode": "forward"` hosts no server: the Companion sends live data to the server named by `serverAddress`
  ([`live-sending.md`](live-sending.md)). An invalid forward setting (its `serverAddress` or `serverToken`) takes the path
  of an invalid setting above: the line reads `Server: settings invalid`, no server starts, the entry `Open Censio` is shown
  greyed, and a replay without `--server` ends with `Replay error: local server not running`.
- A settings file that is invalid as a whole (too large, not JSON, unreadable) shows `Game: settings file invalid` as
  without local mode; no server starts and no `Server:` line is shown.
- The server is hosted with `--record` and `--replay` too. With an invalid command line it is not hosted, and neither
  line nor entry is shown.

### Menu

- Below `Catalog: …` the menu shows the greyed line `Server: <state>`: `Server: starting`, `Server: running at
  http://localhost:<port>/`, `Server: port <port> in use`, `Server: failed` or `Server: settings invalid`; the port is
  written in decimal digits. After the first separator comes the entry `Open Censio`, directly above `Settings…` and `Open diagnostics`.
  It is shown only in local mode, enabled only while the line reads `Server: running …`, and opens
  `http://localhost:<port>/` in the default browser. The menu does not wait for the browser; if no browser can be
  started a warning is logged and nothing else changes.
- In forward mode ([`live-sending.md`](live-sending.md), *Reconnect*) the same place holds the line of the connection to the
  server, such as `Server: connected`, and there is no `Open Censio`, except greyed for an invalid forward setting.
- The tooltip and the icon show the pipe connection only.

### Addresses and host rule

- The hosted server listens on plain HTTP on `127.0.0.1` and on `[::1]` at the port. Where the system has no IPv6 it
  listens on `127.0.0.1` alone. It listens on nothing else, so Windows asks for no firewall permission and no other
  device of the network reaches it.
- A request is answered only when its `Host` is `localhost`, `127.0.0.1` or `[::1]`, with any port and in any letter
  case; every other `Host` (`example.com`, `127.0.0.2`, `localhost.`, `[0:0:0:0:0:0:0:1]`, `127.1`) and a request without
  `Host` (HTTP/1.0) is answered `400`. This keeps a page of another site that points its own name at `127.0.0.1` from
  reading the pages. The rule is the setting `AllowedHosts` of [`server.md`](server.md) set in memory; `localhost` is
  a trusted host for deleting a profile's history ([`web-pages.md`](web-pages.md)).

### Hosted server and its data

- The hosted server is the server of [`server.md`](server.md) with the same pages, `/health` and `/ingest`, and shows
  the version of the build. It takes no setting from outside: no environment variable, no configuration file, no command
  line takes effect; the tests show `ASPNETCORE_URLS` (also with `ASPNETCORE_PREFERHOSTINGURLS` set to `true`),
  `ASPNETCORE_WEBROOT`, `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` and `CENSIO__INGEST__REQUIRETOKEN` changing nothing. The
  host settings come from the Companion, the rest from memory.
- The data folder is `%LOCALAPPDATA%\Censio\Server`. It is fixed, private to the Windows user like the other Companion
  folders, and kept when the Companion is removed. The server creates it at the first start with its database `censio.db`,
  its data-protection key ring and its ingest token file `ingest-token`, under the rules of [`server.md`](server.md),
  [`server-data.md`](server-data.md) and [`ingest-token.md`](ingest-token.md), and opens them again at every later
  start. The database is never kept in memory.
- The guarantees of the server hold: the ingest token is required, the pages need no login, history retention and
  per-profile deletion work as on the image. The Companion reads the token from its file once the start checks have
  passed and holds it in memory for the local replay and for live sending; it is never logged.
- The pages' scripts and styles are bundled in `Censio.Companion.exe`, so the one-file ZIP needs nothing next to it.
  The exe is about 232 MB in a measured build (record 0098).
- The pages fill from the live data the Companion sends to the hosted server ([`live-sending.md`](live-sending.md),
  *Local mode*) and from a replay ([`replay-sending.md`](replay-sending.md), *Local mode*). The catalog's names and icons
  come from the live connection, which uploads the catalog to the hosted server as to any server
  ([`live-sending.md`](live-sending.md), *Catalog*); a replay sends none.

### Start

- The server starts in the background; the tray icon, the menu, the pipe and `Exit` never wait for it. The menu reads
  `Server: starting` until the start has finished. The start runs the checks of [`server.md`](server.md) in their order
  (key ring, ingest token, database, state restore), reads the token back and then listens.
- When it listens the line reads `Server: running at http://localhost:<port>/` and `Open Censio` becomes enabled.

### Stop

- `Exit` removes the icon at once, stops the server and ends the process with exit code 0 within 5 seconds, also while
  the server starts, stores a message, runs the retention job or deletes a profile. A write in progress is completed or
  rolled back, never half stored, and every message the server acknowledged is in the database at the next start
  ([`ingest.md`](ingest.md)). Sign-out and shutdown end the Companion without a dialog and stop the server as far as
  Windows allows.
- After the next start in local mode the pages show the same profiles and history.

## Limits and errors

| Menu line | Cause | What else |
| --------- | ----- | --------- |
| `Server: settings invalid` | A value of `mode`, `localPort`, `bufferHours` or `bufferMegabytes`, or in forward mode of `serverAddress` or `serverToken`, breaks *Switching on* | No server; warning logged |
| `Server: port <port> in use` | Another program, a standalone server or the Companion of another signed-in Windows user holds the port on `127.0.0.1` or on `[::1]` | `Open Censio` greyed; warning logged; nothing is retried, except by a Companion the settings window restarted, which retries a port in use every 500 ms for up to 10 seconds and shows `Server: starting` meanwhile ([`companion-settings.md`](companion-settings.md), *Restart*) |
| `Server: failed` | Any other start failure: the folder cannot be created or written, the database was written by a newer Censio, is not a Censio database or cannot be updated, the key ring or the token file cannot be written or read back, an address that cannot be bound for another reason; also a server that stops on its own while the Companion runs. A port that Windows reserves is expected to end here (not yet verified) | `Open Censio` greyed; the reason is logged; the Companion does not restart the server; a restart of the Companion tries again |

- A failed start leaves pipe, recording, catalog and replay working as without local mode, releases everything it
  created, and repairs or deletes nothing in the data folder.
- A server whose start fails ends a replay without `--server` with `Replay error: local server not running`
  ([`replay-sending.md`](replay-sending.md)).

### Log

- The Companion logs `Server settings invalid: <problem>` (Warning; the problems `mode is not local or forward`, `mode
  occurs more than once`, `localPort is not a port from 1 to 65535`, `localPort occurs more than once`, and the problems
  of forward mode in [`live-sending.md`](live-sending.md)), `Local
  server running on port <port>` (Information), `Local server port <port> in use` (Warning), `Local server failed`
  with the exception (Error), `Local server stopped unexpectedly` (Error) and `Could not open the browser` with the
  exception (Warning) ([`companion-log.md`](companion-log.md)).
- Of the hosted server only lines of level Warning and above reach the Companion's log, under the rules of
  [`companion-log.md`](companion-log.md): sanitized, cut, and without the token. They include the `Critical` lines of the
  start checks, which are the reason of a `Server: failed` without an exception, among them `The ingest token file could
  not be read back; the server stops.` ([`server.md`](server.md), [`server-data.md`](server-data.md),
  [`ingest-token.md`](ingest-token.md)).

### Accepted risk: other users and programs of the PC

Every program and every other signed-in Windows user of the same PC can reach the loopback port, read every profile and
delete history (record 0006). Without the ingest token, which lies in the player's private folder, none of them can
send data. The host rule stops other sites in the player's browser, not programs on the PC.

### Target host

- Local mode is for Windows 11 x64 with no installed .NET, no installer, no administrator rights and a default
  browser. On Windows 10 22H2 the Companion still runs as [`companion.md`](companion.md) (*Platform*) states; local mode
  is neither supported, nor tried, nor refused there.
- Verified on Windows 11 x64 without .NET: the one-file ZIP, that a loopback bind raises no firewall prompt, and that a
  program bound to all interfaces of the port makes the bind fail (`Server: port <port> in use`).

## Related decisions

- [`decisions/0098-companion-hosts-the-server-from-a-reference-with-bundled-web-assets.md`](../decisions/0098-companion-hosts-the-server-from-a-reference-with-bundled-web-assets.md) — the Companion references the server and bundles its web assets; the host takes nothing from outside.
- [`decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md`](../decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md) — `mode` and `localPort` in the settings file, off by default, port 5000.
- [`decisions/0100-local-server-on-loopback-only-with-host-filtering.md`](../decisions/0100-local-server-on-loopback-only-with-host-filtering.md) — loopback addresses only and the host rule.
- [`decisions/0015-two-deliverables-and-local-mode.md`](../decisions/0015-two-deliverables-and-local-mode.md) — the Companion can host the server for local use.

## Not here

- The server's own behavior: [`server.md`](server.md), [`server-data.md`](server-data.md), [`ingest-token.md`](ingest-token.md).
- The replay into the hosted server: [`replay-sending.md`](replay-sending.md); live sending, to the hosted server and to a server elsewhere: [`live-sending.md`](live-sending.md).
- The menu and the settings file in general: [`companion.md`](companion.md); the log file: [`companion-log.md`](companion-log.md).
