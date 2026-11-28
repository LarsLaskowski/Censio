# Spec: Merge two profiles

Source: Issue #262
Status: Draft

## Problem

A player who keeps playing one playthrough under another profile — the notice of
[companion-profiles](../../docs/areas/companion-profiles.md) (*Notice*) ignored, a profile created halfway through a
playthrough — has its history split in two on the server: two entries in the pages' profile switch, two charts that each
show part of the time. Today the only remedy is deleting one part ([history-deletion](../../docs/areas/history-deletion.md)),
which loses it. A rename to the other profile's name is refused (`NameExists`; record
[0117](../../docs/decisions/0117-profile-renamed-in-a-dialog-at-once-the-connection-reopened-with-the-new-name.md),
option 7), and no contract message or server operation moves a history between profiles
([contract](../../docs/areas/contract.md): twelve messages, none of them a merge).

## Scenarios

- S1: Given the profiles `Live`, `Rome` (active) and `Roma`, when the player selects `Roma` in `Profile`, clicks
  `Merge profile…`, chooses `Rome` in the dialog `Merge profile` and clicks `Merge`, then the dialog closes, `Roma` is no
  longer in the drop-down, the figures linked to `Roma` are linked to `Rome`, the other unsaved edits of the window stay,
  and below the drop-down `Merged profiles whose history the server has not moved yet: 1` shows until the server answers.
- S2: Given S1 while connected, then the Companion sends the merge after its next accepted hello (at once when the
  connection idles); the server moves the history of `Roma` into `Rome`, and the line below the drop-down goes away.
- S3: Given S2, then the pages' profile switch lists `Rome` but not `Roma`, `/profiles/<Roma>/regions` shows `Not found.`,
  and the pages of `Rome` show the islands, charts, trends and trade routes of both histories as one, without a reload.
- S4: Given messages of `Roma` still waiting in the buffer when the player merges, then they reach the server as messages
  of `Rome`, in the order they were taken, and no hello names `Roma` again.
- S5: Given the Companion offline, in mode `Off` or in a replay when the player merges, then the merge waits in
  `profiles.json`, also across restarts, and is sent after the first accepted hello of a later run that sends live data.

## Errors and edge cases

- E1: The selection is `Live` or the active profile: `Merge profile…` is disabled with the tooltip
  `Live and the active profile cannot be merged into another.`. The target may be any other profile, `Live` and the
  active one included.
- E2: `Cancel` or closing the dialog changes nothing.
- E3: 1,000 deletions and merges wait for the server together: `Merge` in the dialog and the box of the dialog
  `Delete profile?` are disabled, each with the text `1,000 changes already wait for the server.` (replacing
  `1,000 deletions already wait for the server.`).
- E4: Chains: `A` merged into `B`, later `B` into `C` — messages of `A` and `B` reach the server as messages of `C`, and the
  server ends with one profile `C`, since the waiting deletions and merges are sent in the order the player made them.
  `A` merged into `B`, then `B` deleted with its history: messages of `A` are dropped like those of `B`. `B` deleted
  without its history: messages of `A` are sent under `B`'s identity, which the server then lists unnamed if it does not
  know it.
- E5: The server refuses the merge: the merge counts as answered, `Profile merge refused by the server: <refusal>` is
  logged (Warning), and the Companion's list stays merged; the server keeps both profiles.
- E6: The connection is lost before the answer: the merge stays waiting and is sent again; a merge the server already
  did finds the merged profile gone and is acknowledged without a change.
- E7: A `profileMerge` whose two identities are equal, or either is the all-zero identity, is refused as `invalid`.
- E8: The current connection was opened for the merged profile (buffered messages of `Roma`): the merge ends it as a
  profile switch does, and the new hello names the profile of the next message after the merge.
- E9: The target is not stored on the server (never sent anything, deleted on the pages): the merged history continues
  under the target's identity, listed as `Unnamed profile` until a hello names it.
- E10: Both profiles hold the same island (same session GUID, island ID, area index) or an hourly point of the same hour:
  the island is one island named by the latest tick that carried it, and the two hourly points become one, their counts
  and sums added and their game-time range the union, so the hour's mean is that of all its ticks.
- E11: Both profiles hold trade routes: the set with the later receive time stays, the other is removed (equal receive
  times: the target's stays).
- E12: A merge that fails on the server stores no change and is not acknowledged; the connection is closed with 1011, as
  for a failed deletion, and the Companion sends the merge again.
- E13: A message of the merged identity that reaches the server after the merge (a replay, another Companion with the
  token) creates the profile again without a name, as after a deletion.

## Non-functional requirements

- N1: Compatibility: contract version 3 and the `profiles.json` format of record 0115 are not released (latest tag
  `v0.4.0`, `git ls-remote --tags origin`), so the merge joins them in place; a `profiles.json` written by this branch's
  base is read unchanged.
- N2: Limits: at most 1,000 waiting deletions and merges together; a merged identity is kept until the server answers,
  then forgotten only to make room as a confirmed deletion is. The largest `profiles.json` stays under the reader's
  1 MiB bound, and the reader fails closed on a merge entry it cannot use.
- N3: Duration: the merge runs under the server's write gate; messages arriving meanwhile are acknowledged after it. Its
  duration is measured at the sizes of the deletion's measurements (7 days of fixture ticks; a year of hourly points with
  unfolded ticks) and recorded in the area document; the plan names the bound.
- N4: Memory: the server moves a history in the database and never reads either profile's history into memory whole.
- N5: Privacy: no log line of the Companion or the server holds a profile name, an identity, an island or route name.
- N6: Security: a merge reaches the server only through `/ingest` with the token, the guard of a deletion (record 0116);
  a token holder can merge any stored profile into any identity, accepted as for a deletion. The pages offer no merge.

## Acceptance criteria

- AC1: `CompanionProfiles` merges a profile other than `Live` and the active one into another listed profile at once: the
  source leaves the list, its figure links move to the target, a pending notice clears, the file is written, `Profile
  merged` is logged (Information, no name, no identity) and the merge waits; any other source or target throws
  `ArgumentException` and changes nothing (S1, E1).
- AC2: The settings window shows `Merge profile…` right of `Delete profile…`, disabled with its tooltip for `Live` and the
  active profile; the dialog `Merge profile` lists every other profile in list order, `Cancel` changes nothing, a merge
  keeps the other unsaved edits, the waiting line shows its count, and with 1,000 waiting changes `Merge` and the
  deletion box are disabled with their text (S1, E1, E2, E3).
- AC3: Live sending sends the waiting deletions and merges after each accepted hello, one at a time, in the order they
  were made, before the catalog and the buffer; an acknowledgement confirms a merge and logs `Profile merge confirmed by
  the server`, a refusal confirms it with the warning of E5, and a lost connection or a run without live data keeps it
  waiting (S2, S5, E4, E5, E6).
- AC4: A buffered message of a merged identity is sent as a message of the identity it resolves to through later merges,
  and dropped when that identity is deleted; no hello names a merged identity, and a merge of the connection's profile
  reconnects as a switch (S4, E4, E8).
- AC5: `profiles.json` writes waiting and confirmed merges with source and target and reads them back; the reader rejects
  a malformed merge entry (missing, unknown or repeated property, an identity not in `D` form or all zero, a source
  that is `Live`, listed, repeated or deleted) and stays within 1 MiB at every bound (N1, N2).
- AC6: The contract reads and writes `profileMerge` (`id`, `profileId`, `targetProfileId`), answered by
  `acknowledgement`, and refuses E7 as `invalid` (S2, E7).
- AC7: A `profileMerge` on an accepted `/ingest` connection, and only there, moves the source's ticks, hourly points, islands and trade routes into the target, removes the
  source, keeps the target's name, combines E10 and E11 as stated, handles E9 and a source it does not know (no change,
  acknowledged), and on a failure changes nothing and closes with 1011 (S2, E6, E9, E10, E11, E12, N4, N6).
- AC8: After a merge the server state reads its profiles and held states again and tells the followers: the switch no
  longer lists the source, its page shows `Not found.`, the target's pages show both histories without a reload, and a
  resent tick of either profile is reported as already stored (S3, E13).
- AC9: The server logs `The history of a profile was merged into another.` (Information) and, on a failure, `The history
  of a profile could not be merged into another.` (Warning, with the exception); no Companion or server line holds a
  name or identity (N5).
- AC10: The merge's duration at the sizes of N3 is measured and recorded in the area document (N3).
- AC11: The README and the user guide describe merging, English first, then German, with the texts of *Texts*.

### Texts

| English | German |
| ------- | ------ |
| `Merge profile…` | `Profil zusammenführen…` |
| `Live and the active profile cannot be merged into another.` | `Live und das aktive Profil können nicht in ein anderes Profil überführt werden.` |
| `Merge profile` (dialog title) | `Profil zusammenführen` |
| `Move the profile {0} into:` | `Das Profil {0} überführen in:` |
| `The profile {0} is removed from this Companion; its figures and its history on the server move to the profile chosen. This cannot be undone.` | `Das Profil {0} wird aus diesem Companion entfernt; seine Figuren und sein Verlauf auf dem Server gehen auf das gewählte Profil über. Das lässt sich nicht rückgängig machen.` |
| `Merge` | `Zusammenführen` |
| `Merged profiles whose history the server has not moved yet: {0}` | `Zusammengeführte Profile, deren Verlauf der Server noch nicht übernommen hat: {0}` |
| `1,000 changes already wait for the server.` | `Schon 1.000 Änderungen warten auf den Server.` |

## Out of scope

- A merge on the pages, and merging profiles only the server has (replay profiles, unnamed ones): the pages have no
  token, and deleting on the pages already needs the host rule of record 0085; a follow-up issue if asked for.
- Undoing a merge, merging more than two profiles in one step (merge again), and choosing the merged profile's name
  (it keeps the target's; rename it afterwards).
- Merging `Live` or the active profile into another (E1): switch first.
- The clean-up guarantee of a deletion (names gone from the database file and the write-ahead log): a merge keeps the
  history, and the removed profile row and trade-route set are overwritten by secure delete only.
- Telling apart two playthroughs merged by mistake: islands with the same key continue each other, as on one profile
  today ([live-sending](../../docs/areas/live-sending.md), *Profiles*).

## Open questions

- Is the merge wanted? Answered: yes (Lead, as the issue asks). The product's value is the history; a split history is
  what the profile notice warns about and what a player who ignored it ends up with, and the only remedy today loses data.
  The cost is bounded by reusing the deletion's path: the same queue, the same entry point and guard, the same re-read of
  the server state.
- Where is it offered? Answered: in the Companion's settings window only, the way a profile is renamed and deleted there;
  the Companion knows which profiles belong together and holds the buffer that must follow the merge.
- Does the target keep its name and identity? Answered: yes; only a hello names a profile (record 0111).
- Can a merge leave the server's history alone, like the deletion's box? Answered: no; without moving the history a merge
  is a deletion without history plus moved figure links, which the window offers already.
- Must the contract version change? Answered: no, version 3 is not released (N1).

## Area documents

- `companion-profiles.md`: a section *Merging* — the eligible source and target, what moves in the list, the waiting
  merges in the one ordered queue with the deletions, the buffer's resolution, the file format and its reader rules; the
  sentence "Merging two profiles is not done" goes.
- `companion-settings.md`: the button, its tooltip, the dialog and the waiting line; `companion-texts.md`: *Texts* in both
  languages; `companion-log.md`: the three Companion lines.
- `live-sending.md`: merges sent with the deletions after the hello; buffered messages of a merged identity; the hello.
- `contract.md`: the thirteenth message `profileMerge`; `ingest.md`: how the server answers it and the accepted risk.
- `history-merging.md` (new): what a merge moves and combines, its failure, its log lines and its measurement;
  `server-state.md`: a subsection *Merging profiles* beside *Deleting a profile*; `web-pages.md`: the pages after a merge.
- `README.md` and `docs/USER_GUIDE.md`: merging, English then German.
