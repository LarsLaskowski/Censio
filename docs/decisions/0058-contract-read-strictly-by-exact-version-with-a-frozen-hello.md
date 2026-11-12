# 0058: The contract is read strictly by exact version; the hello and its refusal keep one form for every version

- **Status:** Accepted
- **Date:** 2026-11-12
- **Area:** contract
- **Source:** Issue #27
- **Supersedes:** —

## Context

Record [0018](0018-pipe-parsed-in-companion-versioned-json-contract.md) asks that a Companion and a server with
incompatible contract versions refuse each other with a clear error instead of misreading. The catalog document is
already read strictly by exact version ([0056](0056-catalog-document-read-strictly-by-exact-schema-version.md)). A strict
reader of version 1, though, cannot read anything a version-2 server writes, so a newer server's refusal would reach an
older Companion as *malformed*, and the Companion could not say which side to update. The ingest endpoint (#28) refuses
for a wrong token or a second Companion as well, and its readers are strict too.

## Options considered

1. **Tolerant reader, ignore unknown properties** — cross-version messages pass; a changed meaning is misread and
   unknown data is dropped silently, against 0018.
2. **Major and minor version with additive minors** — the same tolerance with a rule; two numbers to keep right.
3. **Negotiation: the server answers with the highest version both list** — mixed pairs work; every side keeps the
   rules of every old version, and with one version so far it buys nothing.
4. **Exact version, strict reader, and a frozen form for the hello and the refused hello reply** (chosen) — nothing is
   misread; each reader finds the version of any hello and reads a refusal of any version, so both sides can name the
   versions and the side to update.
5. **Refusals outside the contract (WebSocket close status and reason)** — no message form to freeze; the close reason
   is free text of at most 123 bytes, and a Companion would parse a server's text.

## Decision

Option 4. A reader accepts exactly contract version 1 and refuses unknown properties; the version is found before the
structure is checked, and the hello and the refused hello reply, with its three reasons, keep one form that every later
version must also read. The rules are in [`contract.md`](../areas/contract.md).

## Consequences

- Every change of what a message may hold raises the version; a mismatched pair is refused until one side is updated.
- A later version must check the version before the token and the second Companion, so a cross-version pair only ever
  sees the *unsupported version* refusal; a new refusal reason is unreadable by older Companions.
- Negotiation stays possible later: a server that lists two versions reads each by its own rules.
