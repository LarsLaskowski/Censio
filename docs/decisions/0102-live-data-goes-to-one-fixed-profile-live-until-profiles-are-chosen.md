# 0102: Until the player can choose a profile, live data goes to one fixed profile `Live`

- **Status:** Proposed
- **Date:** 2026-11-22
- **Area:** companion
- **Source:** Issue #40; Issue #43 (settings window)
- **Supersedes:** 0020 and 0041, amended only for the time until #43: live data is not kept apart per savegame and
  goes to no profile the player selected, but to one fixed profile.

## Context

Forward mode and local mode's live data (#40) need a profile in the hello and in every message
([0059](0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md)). Record
[0020](0020-one-companion-per-server-with-savegame-profiles.md) keeps savegames apart as profiles, and record
[0041](0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) sends every tick to the profile the player
selected in the Companion; the selection and the window it needs are #43, which depends on #40. The Companion never
writes its settings file ([0045](0045-companion-settings-in-a-json-file-under-local-app-data.md)). An island is keyed
by session GUID, island ID (one byte) and area index ([`pipe.md`](../areas/pipe.md)), and a profile's island list
holds every island it ever had ([`server-state.md`](../areas/server-state.md)).

## Options considered

1. **One fixed profile identity built into the Companion, named `Live`** (chosen by the Product Manager) — the
   history continues across restarts and updates without a file the Companion writes; one Companion per server
   (0020) keeps the identity unique. Two playthroughs share it.
2. **A `profileName` key in the settings file, the identity derived from the name** — the player can separate
   playthroughs by hand before #43; a rename starts a new profile, and it designs #43's profile settings ahead of it.
3. **A new profile at each start, as a replay does** — no mixing across starts; every start splits one playthrough's
   history.
4. **Wait for #43** — no mixing; no live data until then.

## Decision

Option 1. Until #43 builds the profile choice, every live message carries the one fixed profile identity of the
Companion and the hello the name `Live`; the rules are in the area document of #40.

## Consequences

- Loading another savegame or playthrough puts its ticks into `Live`: islands with the same key continue and rename
  each other's history, and islands of the other savegame stay listed without new values. No warning is shown; the
  remedy is deleting `Live`'s history on the pages ([`history-deletion.md`](../areas/history-deletion.md)). The user
  guide tells testers to use one playthrough per server, or to delete `Live` between playthroughs.
- #43 replaces the fixed profile by the player's selection of 0041; `Live` stays a profile like any other.
- Revisit when #43 is built: this record is then superseded.
