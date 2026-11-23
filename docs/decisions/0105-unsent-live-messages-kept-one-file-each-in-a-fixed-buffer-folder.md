# 0105: Unsent live messages are kept one file each in a fixed buffer folder

- **Status:** Proposed
- **Date:** 2026-11-23
- **Area:** live-sending
- **Source:** Issue #41
- **Supersedes:** 0031 — amends its consequence "The buffer lies in a path from configuration, never from received data": the path is fixed, from neither the settings file nor received data.

## Context

Record [0031](0031-bounded-local-tick-buffer-gaps-accepted.md) asks for a persistent buffer of unsent ticks bounded by
age and size, delivered in order; record [0104](0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md)
holds the waiting messages in memory only, so `Exit`, a crash or an outage beyond 64 messages or 16 MiB loses them.
Ticks arrive about every two minutes and are tens to hundreds of kilobytes of JSON; a message is acknowledged by its
identity, which the server deduplicates ([`ingest.md`](../areas/ingest.md)). The Companion's other folders
(`Recordings`, `Logs`, `Cache`, `Server`) are fixed under `%LOCALAPPDATA%\Censio`, and every path it writes is a
security area (`.squad/project.md`). Taking a message from the pipe never waits for the server or the disk.

## Options considered

1. **One file per message, named by a sequence number, written to a temporary name, flushed and renamed; removed
   after its acknowledgement** (chosen) — acknowledging or dropping a message is one delete, a torn write leaves only a
   temporary file, an unreadable file costs one message, and only a small index is held in memory. Against: one file
   system entry per message, and an allocation unit of slack per file.
2. **One append-only log file with a separate acknowledgement pointer** — fewer files; dropping the oldest needs a
   rewrite or a compaction, and a torn pointer write can resend or lose a range.
3. **SQLite in the Companion** — transactions for free; a new dependency in the Companion and a database file for a
   queue of a few hundred entries.
4. **A path from the settings file** (record 0031's wording) — the player chooses the disk; a new parsed path in a
   security area, which no issue asks for.
5. **Keep the memory queue and persist it only at `Exit`** — no disk traffic while connected; a crash or power cut
   loses everything waiting.

## Decision

Option 1 in the fixed folder `%LOCALAPPDATA%\Censio\Buffer`: a message is written there (a header with its identity, the
time it was taken and the SHA-256 of its JSON) before it is first sent, read back only when it is sent, and deleted
after its acknowledgement or when a bound drops it; a file that fails a check is discarded and counted as dropped. The
format and the rules are in [`live-sending.md`](../areas/live-sending.md), *Buffer*.

## Consequences

- An outage within the bounds, a crash and `Exit` lose no message already written; a message still waiting for its
  write at `Exit` is lost.
- A file whose delete fails is sent again at the next start; the server acknowledges it as known.
- Whoever can write the player's `%LOCALAPPDATA%` can put messages in the buffer, as they can change `settings.json`;
  the hash finds damage, not forgery.
- When the folder cannot be written, the rest of the run keeps record 0104's memory bound.
