# 0041: The profile is selected in the Companion; the figure name of a session start is only a hint

- **Status:** Proposed
- **Date:** 2026-11-07
- **Area:** pipe
- **Source:** Issue #19
- **Supersedes:** —

## Context

Record [0020](0020-one-companion-per-server-with-savegame-profiles.md) keeps each savegame in its own profile and left
to #19 how a tick finds its profile: the pipe carries no savegame ID ([`pipe.md`](../areas/pipe.md), *Message
types*). A profile stands for one playthrough, whose save files share islands and goods: this reads 0020's "savegame"
as a playthrough, since its reason, that islands and goods must not be mixed, holds per playthrough.

The only candidate in the stream was the SessionStart text. Four local recordings of #17 (kept off the repository,
record [0029](0029-anonymised-test-fixtures-only.md)) show that it is the name of the player figure: two different
saves of one figure send byte-identical text, a save of another figure sends another text, and every load is a session
end and then a session start on one connection ([`../protocol.md`](../protocol.md), *SessionStart*). The island names
of the two saves of one figure were identical as well; an 8-byte field of their first ticks differed, by the frame
layout the timestamp (the only 8-byte field), which is the game time and differs between save points of one
playthrough too. The figure name can be the player's real name.

## Options considered

1. **Automatic assignment by figure name** — no user action; a player who gives every figure the same name mixes all
   playthroughs in one history without noticing, and the name would have to reach the server as a key.
2. **Automatic assignment by a fingerprint of the ticks (island set, game time)** — no stable key: islands are
   settled and renamed, and game time differs between saves of one playthrough as much as between playthroughs.
3. **Manual selection without a hint** — simplest; a player who loads another playthrough and forgets to switch mixes
   histories with no warning.
4. **Manual selection that holds the ticks and asks on an unknown name** — no tick lands in a wrong profile, but an
   unattended game or an unseen question fills the bounded buffer of record
   [0031](0031-bounded-local-tick-buffer-gaps-accepted.md) and turns into a gap; it needs a question window no issue
   builds.
5. **Manual selection with a non-blocking notice** — the user decides, ticks are never held, and loading a figure
   last used with another profile, or a new figure on a profile that has one, is pointed out.

## Decision

Option 5. Every tick goes to the profile the user selected in the Companion; the figure name of a session start only
decides whether the Companion shows a notice that offers a switch, and the Companion remembers, locally, the profile
each figure name was last used with. The figure name never crosses the contract to the server, and a new profile's
name is what the user types, never prefilled with it. A name not seen before is linked silently to an active profile
that has none yet. Names are compared ordinally as the reader delivers them, not trimmed; an empty name is a name like
any other. Ticks before any session start go to the active profile and link no name, and a session end changes
nothing until the next session start.

## Consequences

- Two playthroughs of one figure name, or of names that differ only in non-ASCII characters (the game writes `_`),
  cannot be told apart: the user switches the profile by hand. This is accepted and stated in the user guide when the
  selection is built.
- A notice the user ignores links the name to the active profile; ticks already sent stay in their profile.
- #27: the contract carries the selected profile, not the figure name. #29 and #30: profiles are server entities
  chosen by the Companion; the server stores no figure name. #31 and #32: the web UI's profile switch chooses only
  what a page shows, never where ticks go. #43: the settings window selects and creates profiles and shows the notice.
- The behavior is written into the area documents of #30 and #43 when it is built; record 0031's buffer is unchanged.
- Revisit if the game ever sends a savegame or playthrough identifier.
