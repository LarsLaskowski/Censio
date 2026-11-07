# Spec: Savegame profile detection from SessionStart

Source: Issue #19
Status: Draft

## Problem

Record [0020](../../docs/decisions/0020-one-companion-per-server-with-savegame-profiles.md) keeps each savegame in
its own profile and leaves to #19 how a tick finds its profile, because the pipe has no savegame ID (confirmed:
[`docs/areas/pipe.md`](../../docs/areas/pipe.md), *Message types*, has no such field; the session GUID is a region).
The profile issues (#27, #30, #41, #43) cannot be built until it is decided. Today the documents call the
SessionStart text an *assumed* "headline", the name of the loaded game.

The PM's recordings of #17 (4 recordings, about 1,360 frames, kept local by record 0029) show: the SessionStart text
is the name of the player figure. Two different saves of one figure send byte-identical text, a save of another
figure sends another text, and every load of another save is a SessionEnd and then a SessionStart on the same
connection. So the text separates figures, never two saves of one figure, and cannot be a savegame key on its own.

A profile here means one playthrough (a game the player continues across save files): its saves share islands and
goods, which is what record 0020 keeps apart.

## Scenarios

The behavior this feature decides. It is built by #27 (contract), #30 (server), #41 (buffer) and #43 (settings);
this feature delivers the decision and the corrected documentation, no code.

- S1: Given the user has selected a profile in the Companion, when the Companion sends ticks, then each tick
  carries that profile and the server stores it there; the pipe text never picks the profile.
- S2: Given the active profile has seen the figure name before, when a session start with that name arrives (a
  load, a reload, a Companion restart), then ticks keep going to the active profile without a question.
- S3: Given the active profile has seen other figure names but not this one, when the session start arrives, then
  the Companion sends none of the following ticks and asks the user which profile they belong to, proposing the
  profile that knows the name, or a new profile; once the user chooses, the held ticks go to that profile in order
  and the profile remembers the name.
- S4: Given a profile that has seen no figure name yet, when the first session start arrives, then the profile
  remembers that name and ticks go to it without a question.
- S5: Given the user switches the active profile in the Companion, when the next tick closes, then it goes to the
  newly selected profile, and that profile remembers the current figure name; ticks already sent stay where they are.

## Errors and edge cases

- E1: Ticks arrive on a connection before any session start (not observed: each connect to a loaded game sent one
  at once) → handled as S3 without a proposal.
- E2: The user does not answer → the ticks wait in the bounded buffer of record 0031 (#41); beyond its bound the
  oldest are dropped and shown as a gap.
- E3: Two playthroughs of one figure name → the Companion cannot tell them apart and follows S2; the user switches the
  profile by hand (S5). Accepted residual risk, stated in the record and later in the user guide (#43).
- E4: An empty figure name, or one with `_` for non-ASCII characters → compared byte for byte as delivered, an
  empty name like any other.
- E5: After a session end and before the next session start (the main menu) → nothing is sent; the next session
  start decides by S2 to S4.

## Non-functional requirements

- N1: Privacy: the figure name may be the player's real name. It never appears in the log (the rule of
  `docs/areas/companion.md`, *Log*, stays), in a committed file, fixture or record (record 0029), or in an issue;
  the documents of this feature state lengths and comparisons only.
- N2: Compatibility: no change to the pipe reader, the recording format, the Companion of 0.1.0 or any setting.

## Acceptance criteria

- AC1: A new decision record decides manual profile selection in the Companion, with the figure name of the session
  start only as a hint compared byte for byte, which holds the ticks and asks on an unknown name (S1, S2, S3, S4,
  S5, E1, E2, E4, E5); it links record 0020 and record 0031 and does not edit the released record 0020.
- AC2: The record's *Options considered* name at least automatic assignment by figure name, automatic assignment by
  a fingerprint of the ticks (island set, game time), manual selection without a hint, and the chosen option, each
  with why it lost; its *Consequences* name E3 as accepted risk and the issues #27, #30, #41 and #43 (S1 to S5).
- AC3: `docs/areas/pipe.md` and `docs/protocol.md` state the SessionStart payload as the player figure's name,
  *confirmed* by the recordings (two saves of one figure identical, two figures different, a load is end then start
  on one connection), not as an assumed headline; `docs/protocol.md` gives both observed connect orders (start right
  after the version; end first) and no name (N1).
- AC4: Every product document that calls the SessionStart text the savegame headline (the user guide's privacy
  paragraph in English and German, `docs/areas/pipe-recording.md`, `docs/areas/companion.md`) names it the player
  figure's name; `README.md` is unchanged (N2).
- AC5: Issues #27, #30, #41 and #43 each carry a comment that links the record.
- AC6: The diff changes no file outside `docs/`, `specs/` and the user guide, and holds no figure name of the
  recordings (N1, N2).

## Out of scope

- Building S1 to S5, their texts and windows: #27, #30, #41, #43.
- Renaming the code's `Headline` members (a code change without a behavior change).
- Telling save points of one playthrough apart, a game time that goes back after loading an older save (#30, #35).
- Moving ticks already stored from one profile to another.
- Anonymising recordings (#20).

## Open questions

- Does the SessionStart text identify a savegame? Answered: no, it is the figure's name (the PM's recordings,
  *Problem*).
- Is a profile per save file or per playthrough? Answered: per playthrough, from record 0020's reason (islands and
  goods must not be mixed).
- Is the 8-byte field that differs between the first ticks of two saves of one figure a key? Answered: it is the
  only 8-byte field of an island frame, the timestamp (game time, `docs/areas/pipe.md`); it differs between save
  points of one playthrough, so it is no playthrough key.
- Does a connect to a loaded game always send a session start? Answered: observed on both such connects, not
  guaranteed; E1 covers its absence.
- The issue's README and user guide requirement? Answered: no behavior users can try changes; the user guide's
  privacy wording is corrected (AC4), the behavior is documented by #43 when it is built.

## Area documents

- `docs/areas/pipe.md`: *Message types* and *Session events* name the payload the player figure's name, not a
  savegame key; *Residual risks* and *Not here* point to the record for the profile assignment.
- `docs/protocol.md`: *Sources* adds the recordings of #17 (counts only), *SessionStart* and *Connect sequence*
  confirmed as in AC3.
- `docs/areas/pipe-recording.md` (*Privacy*) and `docs/areas/companion.md` (*Log*): "headline" becomes the player
  figure's name.
- The profile behavior itself goes into the area documents #30 and #43 create.
