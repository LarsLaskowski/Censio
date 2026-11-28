# 0117: A profile is renamed in a dialog of the settings window at once, and a live connection under it is reopened with the new name

- **Status:** Accepted
- **Date:** 2026-11-27
- **Area:** companion-profiles
- **Source:** Issue #255
- **Supersedes:** —

## Context

Record [0111](0111-profiles-kept-by-the-companion-each-hello-names-the-next-messages-profile.md) keeps the profiles in
the Companion's own list and has each live connection's hello name the profile of its messages; only a hello names a
profile, and the server renames a profile it knows when a hello gives it another name
([server-state](../areas/server-state.md), *Profiles*). Record
[0115](0115-profile-deleted-in-the-companion-history-on-the-server-by-the-players-choice.md) added deletion and left
renaming and merging open. A connection stays open for hours, so the server would keep a profile's old name for as long
as the connection that named it lasts. The Companion may be offline, in mode `Off` or in a replay when the player renames.

## Options considered

1. **A dialog opened by `Rename profile…` next to the drop-down, taking effect at once like a deletion** (chosen) — one
   action, its own validation message, and no interplay with what `Save` does with the drop-down.
2. **A name box in the window, applied by `Save`** — `Save` already makes the selected profile the active one, so a
   rename of a profile other than the active one would switch to it as well, or `Save` would need a third meaning.
3. **The server learns the new name only at the next hello the Companion sends anyway** — no reconnect, but the pages
   keep the old name while the connection lasts, possibly a whole session.
4. **A rename of the profile the current connection named reopens the connection, as a profile switch does** (chosen)
   — the pages show the new name within seconds when connected, at the cost of one reconnect and the catalog check of
   the new connection.
5. **A contract message that renames a profile** — a contract version for both sides for what a reconnect does; lost
   as option 4 of record 0111.
6. **`Live` can be renamed too** — the window's texts and the documentation name `Live` (`Live and the active profile
   cannot be deleted.`), and a lost or unreadable profiles file brings `Live` back under its old name, which the next
   hello would send to the server again.
7. **A rename to another profile's name merges the two** — merging moves one history into another on the server (ticks,
   hourly sums, trade routes, the held state) and needs a contract message and a server operation of its own; the
   names stay distinct ignoring case, and merging is left to a follow-up issue (record
   [0118](0118-profile-merged-in-the-companion-one-ordered-queue-with-the-deletions.md)).

## Decision

Options 1 and 4: every profile but `Live` is renamed at once in a dialog of the settings window under the name rules of
a new profile, the identity, the order and the figure links staying, and a rename of the profile the current live
connection named ends that connection with the close handshake and opens a new one whose hello names the new name. The
rules, the offline case included, are in [companion-profiles](../areas/companion-profiles.md) and
[live-sending](../areas/live-sending.md).

## Consequences

- No contract or server change: the server's copy follows through the hello, and the pages update without a reload.
- A rename made offline, in mode `Off` or for a profile no connection names reaches the server only with the next hello
  that names that profile; a profile the Companion never sends again keeps its old name on the server.
- Messages waiting in the buffer reach the server under the profile's name at the time they are sent, so those taken
  before a rename arrive under the new name.
- Each rename of the connected profile costs one reconnect; renaming another profile costs none.
- The name is the player's own text as before: written to `profiles.json` and sent in the hello, never logged.
