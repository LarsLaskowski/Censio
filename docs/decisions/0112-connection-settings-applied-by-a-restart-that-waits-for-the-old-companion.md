# 0112: Connection and game settings are applied by a restart that waits for the old Companion

- **Status:** Proposed
- **Date:** 2026-11-25
- **Area:** companion
- **Source:** Issue #43
- **Supersedes:** —

## Context

The Companion wires the hosted server, live sending, the buffer and the game search once at start (`App.OnStartup`),
and its single-instance guard ends a second start at once (`SingleInstanceGuard.TryAcquire`). The Product Manager chose
that `Save` restarts the Companion for a changed mode, port, address, token, buffer limit or game folder. `Exit` takes
up to 5 seconds in local mode, and the new hosted server can bind the port only after the old one stopped.

## Options considered

1. **The old Companion starts its own exe with an internal switch `--restarted`, then exits; the new one waits up to
   10 seconds for the single instance** — the start-time wiring stays; the guard is released last, after the server
   stopped, and a port Windows frees only with the old process is retried.
2. **Reconfigure the running Companion** — no restart; every start-time part would need a stop and a new start while
   the pipe and the tray run.
3. **The new Companion is started by a helper after the old one ended** — a second program in the one-file ZIP.
4. **Release the guard after the bounded waits of `Exit`, as before** — a hosted server slower than its wait still
   holds the port when the new Companion binds it, and a bind failure is never retried.

## Decision

Option 1. Only a start with `--restarted` waits for the single instance, at most 10 seconds; every other second start
still ends at once. On the restart exit the old Companion releases the guard only after its hosted server stopped and
its profiles were written, else leaves it to its process exit, and the new one retries a port in use within the same
10 seconds; the rules are in [companion-settings](../areas/companion-settings.md) and
[companion](../areas/companion.md), *Command line*.

## Consequences

- A replay or recording ends with the restart, and a memory-only buffer loses its messages; the window warns first.
- A player may start the exe with `--restarted` by hand: it then only waits before it ends like any second start.
- A new Companion whose wait runs out ends without an icon; the player starts it again. Its log line goes to the day's
  next log file, because the old Companion still holds the day's file open; logging nothing would leave that end
  unexplained.
