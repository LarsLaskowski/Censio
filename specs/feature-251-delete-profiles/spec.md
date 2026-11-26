# Spec: Delete profiles in the Companion, and how the server's copy follows

Source: Issue #251
Status: Draft

## Problem

A player who mixed two playthroughs under one profile, or who finished a playthrough, cannot remove the profile from the
Companion: the settings window of #43 creates and selects profiles only, and `profiles.json` keeps every entry
([`companion-profiles.md`](../../docs/areas/companion-profiles.md), *The list and its file*). The server's copy of a
profile (its row and history) is removed only by `Delete history` on the pages
([`history-deletion.md`](../../docs/areas/history-deletion.md)), which leaves the Companion's entry in place; the
Companion never reads the server's profile list (record 0111). How the two copies relate when one side deletes, and
when the Companion is offline, is not stated anywhere.

## Scenarios

The player chooses in the confirmation whether the server deletes the history too (Q1, option C).

- S1: Given the profiles `Live`, `A` (active) and `B`, when the player selects `B` in `Profile`, clicks
  `Delete profile…` and confirms with `Delete` and the box `Also delete its history on the server` cleared, then `B`
  leaves the drop-down and `profiles.json` at once (no `Save` needed), with its figure links; the drop-down shows the
  active profile `A` again. The server keeps `B`'s history, listed on the pages under the name `B`.
- S2: As S1 with the box ticked while the Companion is connected: the Companion also asks the server to delete `B`, and
  the server removes `B` as `Delete history` on the pages does; the pages no longer list `B`. Messages of `B` still
  waiting in the buffer are dropped, not sent.
- S3: As S2 while no server is reachable (offline, `Mode` `Off`, a refused token, another contract version): the
  deletion waits. The settings window shows, below `Profile`, `Deleted profiles whose history the server has not
  deleted yet: <n>`, kept across restarts. After the next accepted hello the Companion sends every waiting deletion,
  and each one the server confirms leaves the count; at zero the line is gone.
- S4: A profile created while offline exists only in the Companion until a hello names it (unchanged: it appears on the
  pages with its name after the next connection that sends its messages). Deleted before that with the box ticked, its
  deletion is sent all the same; the server, which does not know it, confirms it and nothing changes there.
- S5: A profile deleted with `Delete history` on the pages stays in the Companion's list (the Companion does not read
  the server's list, record 0111). The next message of it, selected and played again, starts a fresh history on the
  server under the same name and identity.
- S6: A profile that exists only on the server (a lost `profiles.json`, another installation, a message of an identity
  the list did not know) never shows in the Companion and is deleted on the pages only.
- S7: The confirmation dialog `Delete profile?` reads `The profile <name> is removed from this Companion, with the
  figures linked to it.`, then the box, then `Delete` and `Cancel`. `Cancel` and closing the dialog change nothing.

## Errors and edge cases

- E1: `Delete profile…` is disabled while `Live` or the active profile is selected in the drop-down (also with the
  profiles file unreadable, where the list is `Live` alone), with the tooltip `Live and the active profile cannot be
  deleted.` To delete the active profile the player selects another one and saves first.
- E2: Other unsaved edits in the window (fields, a typed new profile name) stay as typed and unsaved; the deletion writes
  only `profiles.json`. A drop-down selection of the deleted profile returns to the active one.
- E3: `profiles.json` cannot be written: as today, the state stays in memory, the log gets `Could not save the
  settings: <exception type>` and the next change writes again. A waiting deletion that never reached the file is lost
  with an exit before that write; the window shows nothing else.
- E4: Unreachable server, refused token, another contract version: the existing `Server:` lines show it; the deletion
  waits (S3) and is retried after each accepted hello, without a limit in time.
- E5: The server fails to delete (its store fails): it ends the connection as for a message it could not store
  ([`ingest.md`](../../docs/areas/ingest.md)); the deletion keeps waiting and is retried on the next connection.
- E6: The server refuses the deletion message (an acknowledgement with a refusal): the deletion leaves the waiting list,
  the log gets `Profile deletion refused by the server: <refusal>` (Warning), and the history stays on the server, to
  be deleted on the pages.
- E7: The server does not know the profile (never named there, deleted on the pages already or at the same time, another server): it
  confirms the deletion, which leaves the waiting list.
- E8: The server address or the mode changes before a deletion is sent: it goes to the server the Companion connects
  to next; a history under that profile on another server (the other mode's) stays.
- E9: With the box cleared, messages of the deleted profile that still wait are sent; a server that already knows the
  profile keeps its name, one that does not creates it without a name (`Unnamed profile` on the pages), as for any
  message whose profile the list does not know today.
- E10: The deleted profile is the one the current connection sends under (its waiting messages were being drained):
  with the box ticked the connection sends none of them any more and moves on to the next waiting message's profile.
- E11: A replay (`--replay`) never sends a waiting deletion; only live sending does (forward or local mode).
- E12: With 1,000 deletions waiting, the box is disabled with the text `1,000 deletions already wait for the server.`;
  the profile can still be deleted from the Companion alone.
- E13: A figure linked to the deleted profile counts as not linked at its next session start (*Notice* as for a new
  figure).

## Non-functional requirements

- N1: Compatibility: an existing `profiles.json` (version 1) is read as today; a file with waiting deletions stays under
  the reader's 1 MiB bound at 1,000 profiles, 1,000 figures and 1,000 waiting deletions. A waiting deletion holds the
  profile's identity only, never its name.
- N2: The deletion message is a new contract message, so the contract version rises and a Companion and a server of
  different versions refuse each other's hello as today (version row of the user guide).
- N3: Security: the server deletes on the ingest connection only after an accepted hello, so with the token required
  only a token holder can delete, any profile by identity; with the token switched off anyone who reaches `/ingest`
  outside a browser can, as anyone reaching the pages by IP address already can (records 0085, 0064). The guarantee
  "deletable per profile" is unchanged; the trusted-host rule of the pages is not applied to `/ingest`.
- N4: Privacy: no new log line, message or file entry holds a profile name or a figure; the Companion's lines hold no
  identity either.
- N5: Logs (Companion): `Profile deleted` (Information) at a deletion in the window, `Profile deletion confirmed by the
  server` (Information) per confirmed deletion, E6's Warning. The server logs its existing deletion lines.
- N6: Every new text in English and German from the Companion's text table; no restart in any mode.

## Acceptance criteria

- AC1: `Delete profile…` with `Delete` removes the selected profile and its figure links from the list and from
  `profiles.json` without `Save`; other unsaved edits stay; `Cancel` or closing changes nothing (S1, S7, E2, E13).
- AC2: `Delete profile…` is disabled with E1's tooltip while `Live` or the active profile is selected (E1).
- AC3: With the box cleared nothing is sent to the server for the deletion, and the profile's waiting messages are still
  sent (S1, E9).
- AC4: With the box ticked, the profile's waiting messages are dropped and none is sent after the deletion, also on a
  connection draining them (S2, E10).
- AC5: A ticked deletion waits in `profiles.json` by identity, survives a restart, and is sent after the next accepted
  hello in a live run, never in a replay; a failed file write keeps it in memory as E3 says (S3, S4, E3, E4, E8, E11,
  N1).
- AC6: The server, on an accepted connection, deletes the named profile as the pages' deletion does and confirms it, also
  for a profile it does not know (S2, S4, E7, N3).
- AC7: A confirmed deletion leaves the waiting list; a refused one leaves it with E6's log line; a connection ended
  before the confirmation keeps it waiting (E5, E6, E7).
- AC8: The settings window shows S3's count line while deletions wait and none at zero (S3).
- AC9: With 1,000 waiting deletions the box is disabled with E12's text (E12).
- AC10: A version-1 `profiles.json` is read unchanged, the largest file stays under 1 MiB, and the new texts and log
  lines exist in English and German and hold no name or identity (N1, N4, N5, N6).
- AC11: The contract version rises with the new message, whose reader checks it strictly like every message (N2).
- AC12: The README and the user guide (English first, then German) describe deleting a profile, the box, the waiting
  deletions and S5/S6 (issue criterion 2).

## Out of scope

- Reading the server's profile list in the Companion, so S5 and S6 keep their present behavior (record 0111, option 2).
- Renaming a profile: it would follow by itself, since a hello with a known identity renames the profile on the server
  ([`server-state.md`](../../docs/areas/server-state.md), *Profiles*), offline included at the next hello naming it;
  proposed as a follow-up issue.
- Merging two profiles: it moves ticks and hourly points between profiles on the server, with overlapping times, a
  history change of its own; not proposed now.
- Deleting `Live` or the active profile (E1); deleting from the tray menu.

## Open questions

- Q1: Does a deletion in the Companion also delete the profile's history on the server? Options: (A) never — the
  Companion's list only, the history deleted on the pages; no contract change and no new security area; (B) always —
  every deletion sends one; (C) the player chooses with a box in the confirmation, cleared by default.
  Answered: C, by the Product Manager.
- Q2: How does a profile deleted on the pages appear in the Companion? Answered: unchanged in the list (S5); the Companion
  does not read the server's list (record 0111).
- Q3: What happens to a profile only the server has? Answered: S6, by record 0111.
- Q4: Are renaming and merging part of this change? Answered: no, by the Lead (*Out of scope*), renaming proposed as a follow-up.
- Q5: Does the deletion take effect at `Save`? Answered: no, by the Lead: at `Delete` in the dialog, so a deletion never mixes
  with a restart-needing save.

## Area documents

- [`companion-profiles.md`](../../docs/areas/companion-profiles.md): *Deleting* — the rules, the dialog, the figure
  links, the waiting deletions (file format, bound, sending, confirmation, refusal) and the relation to the pages'
  deletion (S5, S6); the sentence "neither renamed nor deleted" replaced.
- [`companion-settings.md`](../../docs/areas/companion-settings.md): the button, its disabled state, the count line.
- [`companion-texts.md`](../../docs/areas/companion-texts.md): the new texts in both languages.
- [`companion-log.md`](../../docs/areas/companion-log.md): the three lines.
- [`contract.md`](../../docs/areas/contract.md): the new message and the version.
- [`ingest.md`](../../docs/areas/ingest.md): handling and acknowledgement of a deletion.
- [`history-deletion.md`](../../docs/areas/history-deletion.md): the second way a deletion starts, and its access rule.
- [`live-sending.md`](../../docs/areas/live-sending.md) (4,818 words, near its limit): one pointer sentence to
  `companion-profiles.md` for when deletions are sent and which waiting messages are dropped.
