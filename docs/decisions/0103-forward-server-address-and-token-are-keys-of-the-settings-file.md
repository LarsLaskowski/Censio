# 0103: Forward mode's server address and ingest token are keys of the settings file

- **Status:** Proposed
- **Date:** 2026-11-22
- **Area:** live-sending
- **Source:** Issue #40; Issue #43 (settings window)
- **Supersedes:** —

## Context

Forward mode (#40) needs the server's address and its ingest token at every start, also when Windows starts the
Companion without arguments ([0017](0017-companion-tray-app-autostart-and-connect.md)). The settings file
`%LOCALAPPDATA%\Censio\settings.json` is hand-edited, never written by the Companion and ignores unknown keys
([0045](0045-companion-settings-in-a-json-file-under-local-app-data.md)); `mode` already selects local mode
([0099](0099-local-mode-switched-on-in-the-settings-file-off-by-default.md)). The replay reads its token from a file
named on the command line ([0080](0080-replay-token-read-from-a-file-named-on-the-command-line.md)), which left the
settings file's server keys to #40 and #43. #43's window lists "server URL" and "token" among the settings it writes.
The generated token is 43 characters of Base64url ([`ingest-token.md`](../areas/ingest-token.md)).

## Options considered

1. **`"mode": "forward"` with the keys `serverAddress` and `serverToken`** (chosen; token place by the Product
   Manager) — one file for every setting, which #43 can write; the generated token needs no JSON escape (a token from
   the server's `TokenFile` may hold `"` or `\`, which the user escapes). The file now holds a credential, so it must
   not be shared.
2. **A key `serverTokenFile` naming a file read by the replay's token-file rules** — the settings file stays free of
   secrets; two files to keep in step, and #43 would have to write a second file.
3. **A fixed token file beside `settings.json`** — no path to type; a second file with its own reading rules.
4. **Command-line switches** — lost at every autostart.

## Decision

Option 1. With `mode` `forward` the Companion reads `serverAddress` by the `--server` address rules and the optional
`serverToken` by the server's token rule; a wrong value stops live sending only, never the game search. The rules are
in [`live-sending.md`](../areas/live-sending.md).

## Consequences

- `settings.json` holds the ingest token in plain text in the user's private folder: the user guide and `SECURITY.md`
  say not to share the file, and nothing of it is logged.
- A changed token or address counts after a restart, like the other keys.
- The Companion and the server each keep a copy of the token's character rule, as for the replay (0080).
