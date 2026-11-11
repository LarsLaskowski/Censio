# 0056: The catalog document is read strictly, by exactly the schema versions a reader lists

- **Status:** Accepted
- **Date:** 2026-11-11
- **Area:** game-catalog-format
- **Source:** Issue #26
- **Supersedes:** —

## Context

Issue #26 asks for compatibility rules for later versions of the catalog document. Companion and server are released
together but not always updated together, and a mismatch is to be refused, not misread
([0018](0018-pipe-parsed-in-companion-versioned-json-contract.md)); readers of external input are strict and fail
closed ([0033](0033-pipe-reader-strict-from-the-reference-source.md)). The server will read the document from the
network (#27, #42).

## Options considered

1. **Tolerant reader: ignore unknown properties, raise the version only for breaking changes** — an older server reads
   a newer Companion's document; it silently drops what it does not know, a changed meaning under an unchanged
   property is misread, and every reader must decide what "breaking" means.
2. **Major and minor version, minor changes additive** — the same tolerance with a rule for it; two numbers to keep
   right, and the same silent drop.
3. **One integer version raised on every change; a reader accepts exactly the versions it lists and refuses unknown
   properties** — nothing is misread or dropped; a mismatched pair is refused with a clear error until one side is
   updated, and sending a version the other side knows is the contract's handshake (#27).

## Decision

Option 3, with System.Text.Json source generation and a reader that checks size, syntax, version, structure and value
rules in that order and returns a typed result. The rules and the check order are in
[`game-catalog-format.md`](../areas/game-catalog-format.md).

## Consequences

- Any new property, kind or value range is a new schema version, and a reader that should read both keeps the old
  rules beside the new.
- An older server shows GUIDs instead of names until it is updated, unless #27 negotiates an older version.
- The document type, writer and reader live in `Censio.Contracts`, which references no other project; the conversion
  from the Companion's catalog lives in `Censio.Companion.Core`.
