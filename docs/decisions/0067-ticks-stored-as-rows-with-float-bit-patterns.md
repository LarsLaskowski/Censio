# 0067: A tick is stored as rows per island, entry and map pair in one transaction; floats keep their 32-bit pattern

- **Status:** Proposed
- **Date:** 2026-11-13
- **Area:** server-data
- **Source:** Issue #29
- **Supersedes:** —

## Context

A tick holds up to 256 islands and 524,288 entries and map pairs ([`contract.md`](../areas/contract.md), *Bounds*), and
its floats may be `null` or negative zero, which must read back as they came (record
[0031](0031-bounded-local-tick-buffer-gaps-accepted.md): a gap is never a zero). SQLite stores a value of a column with
REAL or NUMERIC affinity that is a whole number as an integer, so `-0.0` reads back as `0.0`. History (#35) and pages
(#32) will query values per island and good over time. A buffered tick can arrive under the hello of another profile
(record [0059](0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md)).

## Options considered

1. **One row per tick with the message as JSON or a binary blob** — fastest write; every query of #32 and #35 reads and
   decodes whole ticks, and the blob is a format of its own to read strictly.
2. **Rows per island, entry and map pair, written through EF Core's change tracker** — queryable; half a million tracked
   entities per tick cost time and memory far beyond the message itself.
3. **The same rows, written by prepared commands on the context's connection in one transaction** (chosen) — queryable,
   one statement per row without tracking, atomic.

For the floats: **REAL columns** lose negative zero; **REAL values in a column without affinity** keep it but rest on
the provider passing the value through unchanged; **the 32-bit pattern in an INTEGER column, `NULL` for no value**
(chosen) is exact by construction.

## Decision

A tick is written as one row per tick, island, entry and map pair by prepared commands inside one transaction, and each
float as its 32-bit pattern in an INTEGER column; a tick of a profile the store does not hold creates that profile
without a name. The rules are in [`server-data.md`](../areas/server-data.md).

## Consequences

- An SQL aggregate over a float (an hourly average of #35) must decode the pattern first, in code or in a function the
  store registers.
- A tick at the bounds writes about half a million rows; the measured time stands in the area document.
- The latest tick and the island names are decided by receive time, then by storing order, kept as the tick row's number.
