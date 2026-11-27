# 0115: A profile is deleted in the Companion, its history on the server only by the player's choice, and its identity filters the buffer

- **Status:** Accepted
- **Date:** 2026-11-26
- **Area:** companion-profiles
- **Source:** Issue #251
- **Supersedes:** —

## Context

Record [0111](0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md) keeps the profiles in
the Companion's own list, which grew without a way to remove an entry; the server's copy of a profile was deleted only
on the pages (record [0084](0084-profile-deleted-in-steps-and-cleared-from-the-file.md)). The Companion may be offline,
in mode `Off` or in a replay when the player deletes, and its buffer holds messages under the profile on disk that name
the profile only inside their JSON; a message of a deleted profile that reaches the server creates the profile again
without a name.

## Options considered

1. **The Companion's list only; the history is deleted on the pages** — no contract change; a mixed-up profile takes
   two steps to clean, and the profile's waiting messages still reach the server.
2. **Every deletion also deletes the history on the server** — one step; a finished playthrough cannot leave the
   drop-down while its history is kept.
3. **The player chooses with a box in the confirmation, cleared by default** (chosen by the Product Manager) — both
   uses, at the cost of option 2 plus one box.
4. **Drop the deleted profile's messages from the buffer once, at the deletion** — a run that does not send live data
   neither reads nor changes the buffer, a resent message in flight escapes it, and every file would have to be read.
5. **Keep the deleted identity, waiting until the server confirms and confirmed after, and drop its messages at every
   send** (chosen) — durable across runs, modes and the confirmation; costs a list of at most 1,000 identities in
   `profiles.json`.
6. **Read the server's profile list so the Companion shows the pages' deletions** — record 0111's option 2, lost for
   the same reasons.

## Decision

Options 3 and 5: the settings window deletes a profile other than `Live` and the active one at once, and a deletion
with the box ticked keeps the profile's identity in `profiles.json`, sends the deletion after the next accepted hello
(record [0116](0116-history-deleted-through-the-ingest-connection-with-the-token.md)) and drops every buffered message
of that identity. The rules are in [companion-profiles](../areas/companion-profiles.md).

## Consequences

- `profiles.json` gets a format version 2, written only while the list of deleted identities is not empty; an older
  Companion reads such a file as `Live` alone, so going back after a ticked deletion resets the list.
- A confirmed identity is forgotten only to make room, oldest first; after 1,000 later ticked deletions a message of
  it that still waits could reach the server again, unnamed.
- A profiles file that cannot be read loses the list with the profiles, as it loses everything else.
- A profile deleted on the pages stays in the Companion's list; renaming and merging are not part of this decision
  (renaming is proposed as a follow-up issue).
