# Spec: Enabling the pipe and recording sessions (README and user guide)

Source: Issue #16
Status: Approved by Product Manager

## Problem

A tester of `v0.1.0` (milestone M1) has to get from the download to a recording of a real session. The game creates
its pipe only when started with `/pipe` ([`pipe.md`](../../docs/areas/pipe.md), *Opening*), and both documents say only
"add `/pipe` to the launch options of your game launcher", not where Ubisoft Connect or Steam take it; without it the
Companion shows `Waiting for game`, as for a game that is not running. The tray states and the recording are
documented as a reference, not as a path to follow, and the state table says what a state means, not what to do. The
README *Status* still says M0 although issues #9 to #15 of M1 are closed.

## Scenarios

- S1: Given a Windows 10 22H2 or Windows 11 x64 PC with Anno 117 and no Censio, when the tester follows the user
  guide's walkthrough step by step — download `Censio-<version>-win-x64.zip` from the release page, extract it, start
  `Censio.Companion.exe` (SmartScreen: "More info", "Run anyway"), find the tray icon, add `/pipe` in the launcher,
  start the game and load a save, choose `Start recording`, play, choose `Stop recording`, `Open recordings folder`,
  choose `Exit` and replay the file (S5) —
  then each step names what the tester sees next: the tooltip `Censio Companion: Waiting for game`, then
  `Censio Companion: Receiving`, then `Censio Companion: Receiving (recording)` with the line
  `Last tick: <time>, <n> islands` once a tick has closed, and finally a file `recording-<yyyyMMdd-HHmmss>Z.jsonl` in
  `%LOCALAPPDATA%\Censio\Recordings`, whose replay ends at `Censio Companion: Replay finished`.
- S2: Given any copy of the game (a Steam copy starts Ubisoft Connect too), when the tester reaches the walkthrough's
  `/pipe` step, then it gives the usual menu path through Ubisoft Connect's labels to the field for launch arguments and
  what to type into it.
- S3: Given the game is bought on Steam, when the tester looks up how to add `/pipe`, then the guide also gives the usual
  menu path to Steam's launch options with Steam's labels, as an alternative to the Ubisoft Connect path.
- S4: Given the tray tooltip shows a state, when the tester looks it up in the guide, then the table gives its meaning
  and what to do next (for example `Waiting for game` while the game runs: check `/pipe` and restart the game).
- S5: Given a finished session, when the tester wants to check the recording, then the guide says to choose `Exit` in
  the tray menu first (a second Companion ends at once without a message), and to start the replay from the Windows
  *Run* dialog (Windows key + R) with the full, quoted paths of the exe and the file, each taken with Explorer's
  "Copy as path": `"<path of Censio.Companion.exe>" --replay "<path of the file>" --speed max`, so no console is
  needed and the line is the same on every Windows; it says that `Replay finished` in the tooltip means the file is
  readable, that `Exit` ends the replay and a normal start brings the Companion back (`Start with Windows` starts it
  only at the next sign-in), and that a recording is private, where it stays and that it is kept until a maintainer
  asks how to hand it in.
- S6: Given a reader opens the README, when they read *Status*, then it names milestone M1 and, until the tag exists,
  "the coming release `v0.1.0`", and states what that release can do (tray
  status, recording, replay, diagnostics window with raw GUIDs, log, optional start with Windows; a server image with
  the start page and `/health` only) and what it cannot (no names or icons, nothing sent to the server, no game data
  in the web application, no history, no Companion settings such as a server address or token, no tool to anonymise a
  recording); the server's two settings in *Configuration* stay.
- S7: Given a German-speaking reader, when they read the German part of the README or the guide, then it holds the
  same walkthrough, launcher steps, table and *Status* in German, with the launcher and Windows labels as a German
  launcher or Windows shows them where they differ; the Companion has no German interface, so its tray texts stay the
  English strings it shows, quoted as the German part already quotes them.

## Errors and edge cases

- E1: The game runs without `/pipe` (the argument was not saved or was typed into the wrong field, or a Steam copy got
  it in Steam's launch options, which may not reach the game through Ubisoft Connect): the tooltip stays at `Censio
  Companion: Waiting for game`; the guide's table names this as the first thing to check, and the Steam alternative and
  the table both say: if it stays at `Waiting for game` with the Steam option set, add `/pipe` in Ubisoft Connect.
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
  span several files, named by the UTC time of their first data, with `-2`, `-3`, … before `.jsonl` when a name is
  taken ([`pipe.md`](../../docs/areas/pipe.md), *Recording and replay*).
- E9: `Protocol mismatch` or `Error` that does not go away: the table says to report it with the version from the tray
  menu and the day's log, as *Reporting problems* describes, and not to attach the recording.
- E10: No release exists yet (before the tag `v0.1.0`): the release page is empty; the README's *Getting Censio* keeps
  sending the reader to the source build, and the walkthrough points there for that case.
- E11: The replay is started while the tray Companion still runs: the second start ends at once without a message and
  nothing replays; the walkthrough's `Exit` step before the replay prevents it, and the replay step names it.
- E12: A path with spaces, or `%LOCALAPPDATA%` typed in a console that does not expand it: the *Run* dialog with the
  quoted full paths from "Copy as path" avoids both; the guide gives no console form of the command.

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
  marked "Not yet tried" (S2, S3, E1, E2). The walkthrough's `/pipe` step uses the Ubisoft Connect path for every copy;
  the Steam path is a separate alternative for a Steam copy.
- [ ] AC3: The guide's table of tray states gives for every state (`Waiting for game`, `Connected`, `Receiving`, `Pipe
  busy`, `Protocol mismatch`, `Error`, and the suffixes `(recording)` and `(recording failed)`) its meaning and what to
  do next (S4, E1, E5, E7, E9).
- [ ] AC4: The guide's recording section says how to start and stop a recording, where the files go and how they are
  named (with the `-2`, `-3` suffixes), when a file appears, that a session can span several files, how to check a file
  as S5 says (`Exit` first, the *Run* dialog, quoted full paths) and what `Replay finished` means, and that a recording
  is private and kept until a maintainer asks for it (S5, E6, E7, E8, E11, E12).
- [ ] AC5: The README *Status* names the current milestone correctly and lists what `v0.1.0` can and cannot do as in S6;
  the README's Companion section keeps the short path (download, extract, start, `/pipe`, `Start recording`) with a link
  to the guide's walkthrough, and the tray-state table and the recording and replay detail it carries today move to the
  guide alone, in both languages (S1, S6).
- [ ] AC6: The German parts of `README.md` and `docs/USER_GUIDE.md` carry the same content as the English parts after
  the change, with the Companion's strings in English and the launcher and Windows labels in German (S7).
- [ ] AC7: The Product Manager (or a stated stand-in) follows the guide on Windows with the real game from the download
  (or the build the plan names, E10) to a recording file, chooses `Exit`, and replays it from the *Run* dialog as S5
  says to `Replay finished`, adding `/pipe` through the launcher they own with it set to German; the launcher path and its
  German labels that run used are confirmed, while the other launcher's path, the English labels (unless the run
  confirmed them), the download, extraction and SmartScreen steps when the run used a source build, and every other
  statement that run did not show stay marked "Not yet tried"; the record of the run names the Windows version and the
  build (S1, S2, S3, S5, S7, E11, E12).

## Out of scope

- Any change to the Companion or the server; a tooltip, dialog or message that would make a missing `/pipe` visible.
- Anonymising recordings (#20) and the procedure for handing in reference recordings (#17).
- Protocol documentation from recordings (#18).
- Removing the "Not yet tried" notes about the published ZIP and image: done by the first release (record 0013,
  *Consequences*).
- Other launchers (Epic and so on) than Ubisoft Connect and Steam.

## Open questions

- Is this already documented? Answered: no; the launcher steps, the walkthrough, the action per state and the
  `v0.1.0` scope are missing (`docs/USER_GUIDE.md` lines 124 to 151, `README.md` lines 98 to 145).
- Does the game need `/pipe`? Answered: yes; Ubisoft's reference reader (`UbisoftMainzAnno/anno117_pipe_example`,
  `README.md` line 37) says only "adding the launch argument `/pipe` to the game", nothing about a launcher.
- What makes a recording "working"? Answered: it replays to `Replay finished` ([`companion.md`](../../docs/areas/companion.md), *Replay*).
- Which build does the dry run use while no release exists? Answered: no `v*` tag exists (`git ls-remote --tags
  origin` is empty); the plan names the build (the README's source build or the pre-release of record 0012) as
  *Criteria verified only in CI* requires.
- Where does each launcher take `/pipe` (Ubisoft Connect's labels; for a Steam copy, Steam's launch options or Ubisoft
  Connect), with the German labels, and which launchers can the Product Manager run in the dry run? Answered: by the
  Product Manager (option b): both launchers' usual menu paths, Ubisoft Connect in the walkthrough for every copy; the
  Product Manager dry-runs the launcher they own in German, the rest stays "Not yet tried" (S2, S3, AC2, AC7).

## Area documents

- None describes new behavior: the Companion does not change. `README.md` and `docs/USER_GUIDE.md` are the
  deliverables.
- [`companion.md`](../../docs/areas/companion.md), *Limits and errors*: where the dry run (AC7) shows a point listed
  there as "Not verified yet" (the second tooltip line, the three icon looks), it moves to the verified points.

## Challenge

- M1 (replay does nothing while the tray Companion runs; no console named): accepted. Confirmed: `OnStartup` takes
  the single-instance guard before it parses `--replay` (`src/Censio.Companion/App.xaml.cs` lines 343 to 377), and a
  second start ends without a message (`docs/USER_GUIDE.md` line 193). S1, S5, AC4 and AC7 now have the `Exit` step
  first and E11 names the case. Instead of choosing a console, the replay starts from the Windows *Run* dialog with
  quoted full paths from "Copy as path": on every Windows 10 and 11, independent of the user's shell (E12), so the
  Product Manager's console does not matter and needs no escalation; the dry run (AC7) confirms it.
- Minor 1 (Steam launch options may not reach the game): accepted in E1; the Steam alternative and the
  `Waiting for game` row send the tester to Ubisoft Connect.
- Minor 2 (`no settings` is wrong for the server, `README.md` lines 157 to 164): accepted in S6.
- Minor 3 (`-2`, `-3` suffixes): accepted in E8 and AC4.
- Minor 4 (no German Companion interface): accepted in S7 and AC6.
- Minor 5 (dry-run build, unreleased wording): accepted; S6 says "the coming release `v0.1.0`" until the tag exists,
  and AC7 keeps the download, extraction and SmartScreen steps "Not yet tried" when the run used a source build. The
  plan still names the build (open question above).
- Minor 6 (README carries the whole tray table and recording section, `README.md` lines 126 to 145): accepted. Record
  0013 gives the README the shortest path and the guide the reference, so AC5 moves that detail to the guide and the
  new "what to do" column is written once per language.
