# 0065: The database schema is kept by EF Core migrations applied at start in one transaction; a foreign, newer or failed database stops the start

- **Status:** Accepted
- **Date:** 2026-11-13
- **Area:** server-data
- **Source:** Issue #29
- **Supersedes:** —

## Context

Record [0021](0021-sqlite-history-with-retention.md) put the server's data into one SQLite file in the data directory.
Every later version must open the file an earlier one wrote and keep its data, while an operator may start an older image
on a newer file, point the setting at a file of another program, or stop the container in the middle of an update. The
operator runs no database tool: whatever the server does at start is all that happens to the file. No schema is
released yet, so the first one can be chosen freely.

## Options considered

1. **Create the schema when the file is missing, never change it** — simplest; the first schema change of #30 or #35
   would need a second mechanism anyway, and an earlier file would be read with the wrong schema.
2. **Own versioned SQL scripts and a version number in the file** — full control; re-implements what EF Core's
   migrations already do (history, ordering, model snapshot) and needs its own tests for each step.
3. **EF Core migrations applied at start, after a classification of the file** (chosen) — the history table names each
   applied step, so a file with an unknown step is newer and one without the history is foreign; EF Core's script of
   all pending steps runs in one transaction of the server's own, so a failed update leaves the file as it was.
   EF Core's own `Migrate` lost: it commits each step on its own, and its lock row, which a stopped update leaves
   behind, makes every later `Migrate` wait forever.
4. **Update a copy and replace the file on success** — keeps the original even if a step cannot run in a transaction;
   needs twice the space, and replacing a SQLite file beside its write-ahead log is a failure mode of its own.

## Decision

The server keeps its schema with EF Core migrations, which it applies at start in one transaction after it has
classified the file read-only (missing, empty, current, earlier, newer, foreign, unreadable); a newer, foreign, unreadable
or unwritable file and a failed update stop the start and leave the file unchanged. A migration whose commands cannot
run inside that transaction is refused at start, so every test that creates a database fails, and option 4 is needed
only if such a migration ever becomes necessary. The behavior is in [`server-data.md`](../areas/server-data.md).

## Consequences

- An older image never writes to a newer file; the operator starts the newer version or restores a copy.
- The server makes no backup before an update: a failed update keeps the file, and a successful one is not undone.
- The server never takes EF Core's migration lock; the transaction alone keeps an update whole, which suffices with
  one server per file ([0020](0020-one-companion-per-server-with-savegame-profiles.md)). A file this server created
  has no lock table, and the history table is created inside the transaction, so a failed first update leaves no
  table; a file holding only EF Core's two bookkeeping tables (from its own tools) is treated as empty.
- A migration EF Core cannot run inside the transaction (a SQLite table rebuild may be one) fails those tests and needs a
  decision (option 4, or a migration written by hand) when the first one comes.
- Adding a migration needs the `dotnet-ef` tool and the design package (`docs/CONTRIBUTING.md`).
