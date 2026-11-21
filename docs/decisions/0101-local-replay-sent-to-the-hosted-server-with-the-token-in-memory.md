# 0101: In local mode a replay without `--server` waits for the hosted server and sends to it with the token held in the process

- **Status:** Accepted
- **Date:** 2026-11-21
- **Area:** replay-sending
- **Source:** Issue #39
- **Supersedes:** —

## Context

Until #40 sends live data, a replay is the only way to fill the local server's pages (Product Manager's answer to the
spec escalation). Only one Companion runs per Windows session, so the replay and the local server share one process.
A replay with `--server` reads its token file first, connects at once and retries nothing
([replay-sending](../areas/replay-sending.md), *Order of a run*). The local server writes its token after the key
ring and listens only after the database and the state are ready. So a replay with `--server` to the own port fails on
the first start (no token file) and can come too early on every later start.

## Options considered

1. **Without `--server`, in local mode, the replay waits for the hosted server and sends to `127.0.0.1` with the
   server's token handed over in the process** (chosen) — no path to type, no race, and the token never travels
   through a file the player handles. Con: a replay without `--server` no longer stays offline in local mode.
2. **Keep `--server` and `--token-file`, and make a replay to the own port wait** — the token file still does not
   exist on the first start, and the player types two paths.
3. **A new switch `--local`** — explicit, but a fourth replay switch for something the settings already say.

## Decision

In local mode, `--replay` without `--server` sends to the hosted server once it runs, and ends with `local server not
running` when it does not start. With `--server`, the replay behaves exactly as before. The rules are in
[replay-sending](../areas/replay-sending.md).

## Consequences

- The local connection is a second outbound call of the Companion, to loopback only. `.squad/project.md` lists it.
- #40 can reuse this for live sending to the local server.
