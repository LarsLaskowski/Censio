# 0118: A profile is merged into another in the Companion, the merge waits in one ordered queue with the deletions, and buffered messages follow it

- **Status:** Accepted
- **Date:** 2026-11-28
- **Area:** companion-profiles
- **Source:** Issue #262
- **Supersedes:** —

## Context

A playthrough fed into two profiles leaves two histories on the server, and the only remedy is deleting one of them
(record [0115](0115-profile-deleted-in-the-companion-history-on-the-server-by-the-players-choice.md)). Record
[0117](0117-profile-renamed-in-a-dialog-at-once-the-connection-reopened-with-the-new-name.md) keeps names distinct
ignoring case and left merging to a follow-up issue. The Companion may be offline, in mode `Off` or in a replay when the
player merges, and its buffer holds messages of the merged profile that name the profile only inside their JSON.

## Options considered

1. **A `Merge profile…` dialog in the settings window, the source chosen as for a deletion** (chosen) — the Companion
   holds the buffer that must follow the merge and the list the merge changes.
2. **A merge on the pages** — reaches profiles only the server has, but the Companion's list would still hold the
   source, and its buffered messages would create the source again on the server.
3. **A rename to another profile's name merges the two** (record 0117, option 7) — a typing slip would merge for good.
4. **Merges in a list of their own beside the deleted identities** — the order between a merge and a later deletion of
   its target is lost at a restart; sent deletion first, the merge would leave the history the player deleted.
5. **One ordered queue of deletions and merges in `profiles.json`, sent in that order** (chosen) — the order survives
   restarts; merges share the bound of 1,000 waiting entries.
6. **Rewrite the merged profile's buffered messages on disk at the merge** — record 0115's option 4: a run that sends no
   live data never reads the buffer, and a message in flight escapes it.
7. **Resolve a buffered message's profile through the merges when it is sent** (chosen) — durable across runs and
   modes, at the cost of one write of the message per send.
8. **A merge that leaves the server's history alone** — a deletion without history already removes a profile from the
   list; a merge that does not move the history would be that deletion with moved figure links.

## Decision

Options 1, 5 and 7: every profile but `Live` and the active one is merged at once into another listed profile, its
figure links move to the target, the merge waits in the one ordered queue of `profiles.json` with the deletions until the
server answers, and a buffered message is sent under the profile its own resolves to through the merges, or dropped
when that profile is deleted. The rules are in [companion-profiles](../areas/companion-profiles.md) and
[live-sending](../areas/live-sending.md).

## Consequences

- `profiles.json` keeps format version 2; an entry of the queue with a `target` is a merge. A file with a merge
  entry is read by no earlier release, as for a deletion.
- A confirmed merge is forgotten only to make room; once forgotten, a message of the merged profile still in the buffer
  reaches the server under the old identity and creates it again, unnamed.
- A refused merge leaves the Companion's list merged and the server's profiles apart.
- Sending a buffered message of a merged profile reads and writes it once more.
