# 0096: Contract version 2 adds the trade routes and is read exactly

- **Status:** Accepted
- **Date:** 2026-11-20
- **Area:** contract
- **Source:** Issue #225
- **Supersedes:** —

## Context

Feature #225 shows the trade routes of pipe protocol 3 on a server page, so they must cross from the Companion to the
server. Contract version 1 has no message for them, and record
[0058](0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md) reads exactly one version and says that
every change of what a message may hold raises it. Version 1 is released (v0.3.0). Only a replay with `--server`
sends today; live sending is #40. The pipe accepts trade-routes frames up to its caps and 1 MiB
([pipe](../areas/pipe.md), *Limits and errors*), and the contract must carry every frame the pipe accepts.

## Options considered

1. **Version 2 with a `tradeRoutes` message, read exactly** (chosen) — record 0058 unchanged in spirit; a pair of
   different versions refuses each other at the hello with both versions named. Con: a v0.3.0 Companion cannot replay
   into the new server, and the other way round.
2. **A new type under version 1** — an older server refuses the new type as *malformed* instead of naming a version,
   and a reader of "version 1" no longer knows what it may hold; contradicts record 0058.
3. **A server that reads versions 1 and 2** — older Companions keep replaying. Con: two rule sets in the reader and
   the endpoint for a sender that only replays recordings, which a user re-runs with the new Companion at no cost.

## Decision

The contract becomes version 2, which adds the `tradeRoutes` message; Companion and server write and read version 2
only. Its form, bounds and value rules are in [contract](../areas/contract.md).

## Consequences

- The version texts (`supported: 2`, `server does not support contract version 2`) change with it.
- The message carries the routes and their stations' island keys, not their goods, flags or owner: a later page
  that shows them raises the version again.
- The bounds follow the pipe's caps (1,024 routes, 256 stations a route) and a 1 MiB frame (52,428 stations), so no
  frame the pipe accepts is refused by the contract.
