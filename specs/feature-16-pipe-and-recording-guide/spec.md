# Spec: Enabling the pipe and recording sessions (README and user guide)

Source: Issue #16
Status: Draft

## Problem

A tester of the first release `v0.1.0` (milestone M1, "so that sessions can be recorded") has to get from the
release download to a recording of a real game session. The game creates its statistics pipe only when it is
started with the launch argument `/pipe` (Ubisoft's reference reader, `UbisoftMainzAnno/anno117_pipe_example`,
`README.md` line 37: "adding the launch argument `/pipe` to the game"; [`pipe.md`](../../docs/areas/pipe.md),
*Opening*). Today `docs/USER_GUIDE.md` (*Connecting to the game*, *Recording and replay*) and `README.md` say only
"add `/pipe` to the launch options of your game launcher": neither says where Ubisoft Connect or Steam take it, and
a game started without it looks exactly like a game that is not running (`Waiting for game`). The tray states,
the recording and the folders are documented (issues #13 to #15), but as a reference spread over several sections,
not as a path a tester follows, and the table of states says what a state means, not what to do about it. The
README *Status* still says "milestone M0 (Foundation)" although issues #9 to #15 of M1 are closed, and lists what
`main` does, not what `v0.1.0` can and cannot do.

## Scenarios

- S1: Given a Windows 10 22H2 or Windows 11 x64 PC with Anno 117 and no Censio, when the tester follows the user
  guide's walkthrough step by step — download `Censio-<version>-win-x64.zip` from the release page, extract it, start
  `Censio.Companion.exe` (SmartScreen: "More info", "Run anyway"), find the tray icon, add `/pipe` in the launcher,
  start the game and load a save, choose `Start recording`, play, choose `Stop recording`, `Open recordings folder` —
  then each step names what the tester sees next: the tooltip `Censio Companion: Waiting for game`, then
  `Censio Companion: Receiving`, then `Censio Companion: Receiving (recording)` with the line
  `Last tick: <time>, <n> islands` once a tick has closed, and finally a file `recording-<yyyyMMdd-HHmmss>Z.jsonl` in
  `%LOCALAPPDATA%\Censio\Recordings`.
- S2: Given the game is installed through Ubisoft Connect, when the tester looks up how to add `/pipe`, then the guide
  gives the path through Ubisoft Connect's own labels to the field for launch arguments and what to type into it.
- S3: Given the game is bought on Steam, when the tester looks up how to add `/pipe`, then the guide says where a Steam
  copy takes it (Steam's launch options or Ubisoft Connect, which a Steam copy also starts) with the launcher's labels.
- S4: Given the tray tooltip shows a state, when the tester looks it up in the guide, then the table gives its meaning
  and what to do next (for example `Waiting for game` while the game runs: check `/pipe` and restart the game).
- S5: Given a finished session, when the tester wants to check the recording, then the guide shows how to replay it
  (`Censio.Companion.exe --replay <file> --speed max`) and that `Replay finished` means the file is readable, and says
  that a recording is private, where it stays and that it is kept until a maintainer asks how to hand it in.
- S6: Given a reader opens the README, when they read *Status*, then it states what the release `v0.1.0` can do (tray
  status, recording, replay, diagnostics window with raw GUIDs, log, optional start with Windows; a server image with
  the start page and `/health` only) and what it cannot (no names or icons, nothing sent to the server, no game data
  in the web application, no history, no settings, no tool to anonymise a recording).
- S7: Given a German-speaking reader, when they read the German part of the README or the guide, then it holds the
  same walkthrough, launcher steps, table and *Status* in German, with the launcher labels as a German launcher shows
  them where they differ.

## Errors and edge cases

- E1: The game runs without `/pipe` (the argument was not saved, typed into the wrong field, or into the other
  launcher's field for a Steam copy): the tooltip stays at `Censio Companion: Waiting for game`; the guide's table
  names this as the first thing to check.
- E2: `/pipe` is added while the game runs: nothing changes until the game is quit and started again; the guide says
  the argument takes effect at the next game start.
- E3: The tray icon does not show: Windows can hide a new icon in the overflow area of the notification area; the
  guide says where to look, and that a second start of `Censio.Companion.exe` ends at once without a message.
- E4: The tester starts the exe from inside the ZIP without extracting it: it runs from a temporary folder, and `Start
  with Windows` then registers a path Windows later removes ([`companion.md`](../../docs/areas/companion.md),
  *Limits and errors*); the walkthrough says to extract first.
- E5: `Pipe busy`: another pipe tool holds the pipe, or the Companion was restarted while the game is in the main menu;
  the table says to close the other tool or load a save.
- E6: `Start recording` before the game sends: the tooltip shows `(recording)` at once, but no folder or file exists
  until the game's first data; the guide says so, so an empty or missing folder is not taken as a failure.
- E7: `(recording failed)` (disk full or too slow): the guide says recording stopped, the Companion keeps working, and
  `Start recording` tries again with a new file.
- E8: The game is quit and started again during a recording: a new file per connection; the guide says a session can
  span several files, named by the UTC time of their first data.
- E9: `Protocol mismatch` or `Error` that does not go away: the table says to report it with the version from the tray
  menu and the day's log, as *Reporting problems* describes, and not to attach the recording.
- E10: No release exists yet (before the tag `v0.1.0`): the release page is empty; the README's *Getting Censio* keeps
  sending the reader to the source build, and the walkthrough points there for that case.

## Non-functional requirements

- Documentation only: no production, test, configuration or workflow file changes; no new setting.
- Placement follows record 0013: the walkthrough and the full launcher steps live in `docs/USER_GUIDE.md`; the README
  keeps the short path (download, start, `/pipe`, record) with a link to them, and is the only home of *Status*.
- Language follows record 0030: every change in both files is made in the English and the German part in the same
  pull request.
- Every statement no run has shown is marked "Not yet tried" in the document (`.squad/routing.md`, *Operator
  environments and documented procedures*, *Dry run*); a launcher step nobody has run stays marked.
- Privacy: the guide never asks a tester to post or attach a raw recording, a screenshot with island names or a log
  with the Windows user name (*Reporting problems* stays the rule).
- The texts quoted from the Companion are the strings it shows (`TrayMenu`, `PipeRecordingFormat` in the source), and
  no statement contradicts [`companion.md`](../../docs/areas/companion.md) or [`pipe.md`](../../docs/areas/pipe.md).

## Acceptance criteria

- [ ] AC1: `docs/USER_GUIDE.md` holds a numbered walkthrough from the release download to a recording file, in the order
  of S1, each step naming what the tester sees, with the warnings of E3, E4 and E10 at the step they apply to (S1, E3,
  E4, E10).
- [ ] AC2: The guide gives the steps to add `/pipe` in Ubisoft Connect and for a Steam copy, with the launcher's labels,
  the exact text to type, and that the argument takes effect at the next game start; each step not shown by a run is
  marked "Not yet tried" (S2, S3, E1, E2).
- [ ] AC3: The guide's table of tray states gives for every state (`Waiting for game`, `Connected`, `Receiving`, `Pipe
  busy`, `Protocol mismatch`, `Error`, and the suffixes `(recording)` and `(recording failed)`) its meaning and what to
  do next (S4, E1, E5, E7, E9).
- [ ] AC4: The guide's recording section says how to start and stop a recording, where the files go and how they are
  named, when a file appears, that a session can span several files, how to check a file with `--replay` and what
  `Replay finished` means, and that a recording is private and kept until a maintainer asks for it (S5, E6, E7, E8).
- [ ] AC5: The README *Status* names the current milestone correctly and lists what `v0.1.0` can and cannot do as in S6;
  the README's Companion section names `/pipe` and `Start recording` with a link to the guide's walkthrough (S1, S6).
- [ ] AC6: The German parts of `README.md` and `docs/USER_GUIDE.md` carry the same content as the English parts after
  the change (S7).
- [ ] AC7: The Product Manager (or a stated stand-in) follows the guide on Windows with the real game from the download
  (or the build the plan names, E10) to a recording file whose replay with `--replay <file> --speed max` ends at
  `Replay finished`; every statement that run did not show is marked "Not yet tried" (S1, S5).

## Out of scope

- Any change to the Companion or the server; a tooltip, dialog or message that would make a missing `/pipe` visible.
- Anonymising recordings (#20) and the procedure for handing in reference recordings (#17).
- Protocol documentation from recordings (#18).
- Removing the "Not yet tried" notes about the published ZIP and image: done by the first release (record 0013,
  *Consequences*).
- Other launchers (Epic and so on) than Ubisoft Connect and Steam.

## Open questions

- Is this already documented, so the outcome is no change? Answered: no. The tray states, recording, folders and
  privacy are documented (`docs/USER_GUIDE.md` lines 124 to 151, `README.md` lines 98 to 145); the launcher steps, the
  walkthrough, the action per state and the `v0.1.0` scope are missing, and the README *Status* is stale (M0).
- Does the game need `/pipe`, and what does the source say about adding it? Answered: yes; Ubisoft's reference reader
  says only "adding the launch argument `/pipe` to the game" (`README.md` line 37 of
  `UbisoftMainzAnno/anno117_pipe_example`), nothing about a launcher.
- Is #13 done? Answered: yes, closed (merged in #95); #14 and #15 too (#99, #102).
- What makes a recording "working"? Answered: a file that the Companion replays to `Replay finished` without `Replay
  error` ([`companion.md`](../../docs/areas/companion.md), *Replay*); AC7 uses it.
- Which build does the dry run use while no release exists? Answered: no `v*` tag exists (`git ls-remote --tags
  origin` is empty); the plan names the build (the README's source build or the pre-release of record 0012) as
  *Criteria verified only in CI* requires.
- Where does each launcher take `/pipe` (Ubisoft Connect's labels; for a Steam copy, Steam's launch options or Ubisoft
  Connect), with the German labels, and which launchers can the Product Manager run in the dry run? Answered: not
  yet — only someone with the game can see the launchers; escalated to the Product Manager.

## Area documents

- None describes new behavior: the Companion does not change. `README.md` and `docs/USER_GUIDE.md` are the
  deliverables.
- [`companion.md`](../../docs/areas/companion.md), *Limits and errors*: where the dry run (AC7) shows a point listed
  there as "Not verified yet" (the second tooltip line, the three icon looks), it moves to the verified points.
