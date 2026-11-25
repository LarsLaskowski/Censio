# 0111: Profiles are a list the Companion keeps; each live connection's hello names the profile of its messages

- **Status:** Proposed
- **Date:** 2026-11-25
- **Area:** live-sending
- **Source:** Issue #43
- **Supersedes:** —

## Context

Record [0041](0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) has the player select the profile in
the Companion and links figure names to profiles locally; #40 sent everything to one fixed profile `Live` until #43.
Only a hello names a profile, a second hello on a connection is refused, and a message of an identity no hello named
creates the profile without a name ([contract](../areas/contract.md), [ingest](../areas/ingest.md)). Buffered messages
keep the profile they were taken for.

## Options considered

1. **A local list (`profiles.json`), `Live` first; a connection is reopened whenever the next message belongs to
   another named profile than its hello** — every profile reaches the server with its name, also one fed offline;
   one reconnect per switch.
2. **Read the server's profile list** — needs a new contract message and a server; local mode before its start and
   an unreachable server would leave the window without a list.
3. **Swap only the `profileId` of new messages** — no reconnect; a new profile shows up without a name until the
   next connection, and one fed offline and switched away from stays unnamed.
4. **A contract message that names a profile** — a contract version bump for both sides, for what a reconnect does.
5. **Figure links by the figure's name, the file bound raised to 4 MiB** — the file would hold the figure name, and its
   size would follow the pipe's strings (up to about 2 MB escaped); a hash of the name has a fixed length.

## Decision

Option 1 (the list kept locally as the Product Manager chose). The Companion keeps its profiles and figure links in a
fixed file under `%LOCALAPPDATA%\Censio`, `Live` keeping its identity and each figure kept as the SHA-256 of its name,
and opens each live connection with a hello for the profile of the next message to send. A switch closes the old
connection with the close handshake, and a `companionConnected` refusal shortly after it, while the server still ends
the old session, is retried as part of the switch; the rules are in [companion-profiles](../areas/companion-profiles.md) and
[live-sending](../areas/live-sending.md).

## Consequences

- A profile switch costs one reconnect, and the catalog check of every new connection (record 0107); a few refused
  hellos right after it are expected and neither logged nor counted as failures.
- The profiles file holds no figure name, only its hash; a hash of a known name can be recomputed, so the file stays
  private.
- A message whose profile the list no longer knows (a lost profiles file) is sent on the current connection; the server
  creates that profile without a name.
- Profiles are neither renamed nor deleted in the Companion; history is deleted on the pages.
- Record 0102 (the fixed profile until #43) is deleted: unreleased, it no longer applies.
