# Spec: Savegame profile detection from SessionStart

Source: Issue #19
Status: Draft

## Problem

Record [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md) keeps each savegame in
its own profile and leaves to #19 how a tick finds its profile, because the pipe has no savegame ID (confirmed:
[`docs/areas/pipe.md`](../../docs/areas/pipe.md), *Message types*, has no such field; the session GUID is a region).
The profile issues cannot be built until it is decided; the documents call the SessionStart text an *assumed*
"headline". The PM's recordings of #17 (4 recordings, about 1,360 frames, kept local by record 0029) show it is the
player figure's name: two saves of one figure send byte-identical text, another figure another text, and every load
is a SessionEnd then a SessionStart on one connection. So it is no savegame key. A profile means one playthrough
(record 0020's reason: its saves share islands and goods).

## Scenarios

The behavior this feature decides. It is built by #27 (contract), #29 and #30 (server), #31 and #32 (web UI) and
#43 (Companion settings); this feature delivers the decision and the corrected documentation, no code.

- S1: Given the user has selected a profile in the Companion, when the Companion sends ticks, then each tick
  carries that profile and the server stores it there; the pipe text never picks the profile and no tick is held
  back for a question.
- S2: Given the figure name of a session start was last used with the active profile, when it arrives (a load, a
  reload, a Companion restart), then nothing is shown.
- S3: Given the figure name was last used with another profile, when the session start arrives, then the Companion
  shows a notice that does not block and offers to switch to that profile; until the user switches, ticks keep
  going to the active profile.
- S4: Given a figure name never seen before, when the session start arrives, then: if the active profile has no
  figure name yet, the name is linked to it silently; otherwise the Companion shows the notice of S3, offering a
  new profile.
- S5: Given the user switches the active profile in the Companion, when the next tick closes, then it goes to the
  newly selected profile; ticks already sent stay where they are.
- S6: The Companion remembers, per figure name, only the profile it was last used with: the profile that received
  the first tick after its session start, or the profile the user switched to while it was loaded (S5). Ignoring a
  notice therefore links the name to the active profile, and the next load asks no more.
- S7: Given the web UI's profile switch (#31, #32), when the user picks a profile there, then it chooses only which
  profile's data the page shows, never where ticks go.

## Errors and edge cases

- E1: Ticks arrive on a connection before any session start (not observed: each connect to a loaded game sent one
  at once) → they go to the active profile, nothing is shown and no name is linked.
- E2: Two playthroughs of one figure name → the Companion cannot tell them apart and follows S2; the user switches
  the profile by hand (S5). Accepted residual risk, stated in the record and later in the user guide (#43). Two names
  that differ only in non-ASCII characters can arrive identical (the game writes `_`) and fall under this case.
- E3: The figure name is compared ordinally as the reader delivers it (`_` for non-ASCII, U+FFFD for an invalid
  sequence, not trimmed); an empty name is a name like any other.
- E4: After a session end and before the next session start (the main menu) → nothing changes; the next session
  start decides by S2, S3 or S4.

## Non-functional requirements

- N1: Privacy: the figure name may be the player's real name. The name-to-profile memory of S6 lives only in the
  Companion's local data in the user's profile; the name never crosses the contract to the server, is never stored
  in the server's database, and never appears in the log (the rule of `docs/areas/companion.md`, *Log*, stays), in a
  committed file, fixture or record (record 0029), or in an issue. A new profile's name is what the user types;
  the Companion does not prefill it with the figure name.
- N2: Compatibility: no change to the pipe reader, the recording format, the Companion of 0.1.0 or any setting.

## Acceptance criteria

- AC1: A new decision record decides manual profile selection in the Companion with the figure name only as a hint
  for a non-blocking notice, and that no tick is held back (S1, S2, S3, S4, S5, S6, E1, E3, E4); it decides that the
  name-to-profile memory is local to the Companion and the name never crosses the contract (N1); it links records
  0020 and 0031 and edits neither (both released).
- AC2: The record's *Options considered* name at least automatic assignment by figure name, automatic assignment by
  a fingerprint of the ticks (island set, game time), manual selection without a hint, manual selection that holds
  ticks and asks on an unknown name, and the chosen option, each with why it lost; its *Consequences* name E2 as
  accepted risk, S7, and the issues #27, #29, #30, #31, #32 and #43.
- AC3: `docs/areas/pipe.md` and `docs/protocol.md` state the SessionStart payload as the player figure's name,
  *confirmed* by the recordings (two saves of one figure identical, two figures different, a load is end then start
  on one connection), not as an assumed headline. `docs/protocol.md` adds the recordings of #17 to *Sources* next to
  the existing one (counts only), gives both observed connect orders (start right after the version; end first)
  and the observed name lengths (6 and 18 bytes) instead of "18 bytes both times"; `docs/areas/pipe.md` no longer
  says the end right after the connect was "observed once". No name (N1).
- AC4: Every product document that calls the SessionStart text the savegame headline (the user guide's privacy
  paragraph in English and German, `docs/areas/pipe-recording.md`, `docs/areas/companion.md`) names it the player
  figure's name; `README.md` is unchanged, as it never mentions the text.
- AC5: Issues #27, #29, #30, #31, #32 and #43 each carry a comment that links the record.
- AC6: The diff changes no file outside `docs/`, `specs/` and the user guide (N2), and holds no figure name of the
  recordings (N1): the orchestrator greps the diff for the real names before each commit and logs only that the check
  passed.

## Out of scope

- Building S1 to S7, their texts and windows: #27, #29, #30, #31, #32, #43.
- Renaming the code's `Headline` members (a code change without a behavior change).
- Telling save points of one playthrough apart, a game time that goes back after loading an older save (#30, #35).
- Moving ticks already stored from one profile to another.
- Anonymising recordings (#20).

## Open questions

- Does the SessionStart text identify a savegame, and is a profile per save file? Answered: no, and per
  playthrough (*Problem*).
- Is the 8-byte field that differs between two saves of one figure a key? Answered: it is the timestamp (game
  time, the only 8-byte field, `docs/areas/pipe.md`); it differs between save points of one playthrough.
- Does a connect to a loaded game always send a session start? Answered: observed on both such connects, not
  guaranteed; E1 covers its absence.
- The issue's README and user guide requirement? Answered: no behavior users can try changes; the user guide's
  privacy wording is corrected (AC4), the behavior is documented by #43 when it is built.

## Area documents

- `docs/areas/pipe.md`: *Message types* and *Session events* name the payload the player figure's name, not a
  savegame key, and drop "observed once"; *Residual risks* and *Not here* point to the record for the profile
  assignment.
- `docs/protocol.md`: *Sources* adds the recordings of #17 (counts only), *SessionStart* and *Connect sequence*
  confirmed as in AC3.
- `docs/areas/pipe-recording.md` (*Privacy*) and `docs/areas/companion.md` (*Log*): "headline" becomes the player
  figure's name.
- The profile behavior itself goes into the area documents #30 and #43 create.

## Challenge

- M1 (hold-and-ask loses data): accepted. Ticks always go to the active profile (S1, E1); a name shows only a
  non-blocking notice (S3, S4); E1 now matches S4. Holding is a losing option in AC2; #41 leaves AC5.
- M2 (where memory and profiles live): accepted. Profiles stay server entities (#29); the name memory is local to
  the Companion and the name never reaches the server (N1, AC1).
- M3 (missing issues, web switch): accepted: #29, #31, #32 in AC2 and AC5; S7.
- m1: moot, the decision no longer amends 0031. m2: accepted (AC3). m3: accepted (AC6). m4: accepted (E2, E3).
  m5: accepted, one last-used profile per name (S6). m6: accepted (AC4).
