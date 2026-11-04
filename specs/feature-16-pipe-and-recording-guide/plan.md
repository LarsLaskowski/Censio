# Plan: Enabling the pipe and recording sessions (README and user guide)

Source: Issue #16 | [spec.md](spec.md)
Status: Draft
Tier: trivial — documentation that does not qualify as `docs` (a feature, and `docs` is for issues only), with no change to production, test, configuration or workflow files.
Security areas touched: none

## Problem / root cause

See [spec.md](spec.md), *Problem*: the guide says where to add `/pipe` nowhere, offers no step-by-step path to a
recording, gives no action per tray state, and the README *Status* is stale.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A second Companion start, a replay included, ends at once: the single-instance guard is taken before the command line is parsed | `src/Censio.Companion/App.xaml.cs` lines 343 to 377; `docs/USER_GUIDE.md` line 193 | confirmed |
| 2 | The tooltip texts are `Censio Companion: <state>`, with ` (recording)` / ` (recording failed)` appended, `Censio Companion: Replay finished` and `Censio Companion: Replay: <state>` | `src/Censio.Companion.Core/TrayMenu.cs` lines 17, 117, 118, 142, 153 | confirmed |
| 3 | A taken recording name gets `-2`, `-3`, … before `.jsonl` | `docs/areas/pipe.md` line 148 | confirmed |
| 4 | `README.md` *Companion* (lines 98 to 145, German 271 to 318) repeats the guide's *Companion* section (`docs/USER_GUIDE.md` lines 104 to 151, German 314 to 361) almost word for word, tray table and recording detail included | read side by side | confirmed |
| 5 | The README *Status* says milestone M0 (English line 9, German line 182) | `README.md` | confirmed |
| 6 | The server has two settings (`CENSIO__DATADIRECTORY`, `ASPNETCORE_HTTP_PORTS`), the Companion none | `README.md` lines 157 to 164 | confirmed |
| 7 | No `v*` tag exists, so the dry run cannot use a release ZIP | `git ls-remote --tags origin` printed nothing | confirmed |
| 8 | The source-build command of *Build the Companion from source* yields one `Censio.Companion.exe` showing `Version 0.0.0` | `README.md` line 155 (the maintainer's run) | confirmed |
| 9 | `Start with Windows` starts the Companion only at sign-in (a `Run` value), so after `Exit` it stays ended | `README.md` line 109; `docs/areas/companion.md` *Limits and errors* | confirmed |
| 10 | The game creates the pipe only with `/pipe` | Ubisoft reference reader `UbisoftMainzAnno/anno117_pipe_example`, `README.md` line 37 | confirmed |
| 11 | Ubisoft Connect: the game's page, *Properties*, *Game launch arguments*, *Add command line arguments*, a text field taking `/pipe`; German labels (*Eigenschaften*, *Startargumente*, …) | to verify: the Product Manager reads the labels in the German Ubisoft Connect during the dry run (task 3) if it is the launcher they own; the Dev writes the labels marked "Not yet tried" until then | unverified |
| 12 | Steam: *Library*, right-click the game, *Properties…*, *General*, *Launch Options* (German *Bibliothek*, *Eigenschaften…*, *Allgemein*, *Startoptionen*); whether that argument reaches the game through Ubisoft Connect is unknown | to verify: the Product Manager in the dry run (task 3) if Steam is the launcher they own; otherwise it stays "Not yet tried" | unverified |
| 13 | The Windows *Run* dialog (Windows key + R) starts `"<exe path>" --replay "<file path>" --speed max` with both quoted paths from Explorer's "Copy as path" (Shift + right-click on Windows 10), and the line fits the dialog | to verify: the Product Manager in the dry run (task 3), with the paths of their extraction folder and recording | unverified |
| 14 | Windows can put a new tray icon in the overflow area (the arrow `^` next to the clock) | to verify: the Product Manager in the dry run (task 3) | unverified |

## Tests per criterion

No test: the change has no production or test code; each criterion is verified as *Verification without tests*
says.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | none (read-only check) | — |
| AC2 | none (read-only check, dry run) | — |
| AC3 | none (read-only check) | — |
| AC4 | none (read-only check, dry run) | — |
| AC5 | none (read-only check) | — |
| AC6 | none (read-only check) | — |
| AC7 | none (dry run) | — |

## Verification without tests

Steps 4 (*Skeleton*), 5 (*Tests first*) and the *Coverage gate* are not applicable (*Changes without production
or test code*); step 7 runs *Format check*.

- AC1, AC3, AC4, AC5, AC6: the Reviewer checks the diff read-only against each criterion and the spec's scenario
  and edge-case ids it names, every quoted Companion string against `TrayMenu.cs` (fact 2), and the German part
  section by section against the English one.
- AC2, AC4 (the replay step), AC7: the Product Manager's dry run (task 3) **after** the Dev's documentation edits
  and **before** the review. Build: a local publish of the branch head with the command of *Build the Companion from
  source* (fact 8), so the tray shows `Version 0.0.0`; the download, extraction and SmartScreen steps stay "Not yet
  tried". The Product Manager reports the Windows version, the launcher used (German), the labels seen, and whether
  each walkthrough step showed what it names; the Dev then writes the confirmed labels and removes "Not yet tried"
  only from what the run showed (task 4). The orchestrator logs the run.

## Approach

Layout (the Dev does not decide it):

- **`docs/USER_GUIDE.md`, new section `## First recording, step by step`** (German `## Erste Aufzeichnung Schritt
  für Schritt`) between *First start* and *Data and logs*. A numbered list in S1's order, one step per action, each
  ending with what the tester sees: download (E10: no release yet → *Build from source*), extract first (E4), start
  (SmartScreen), find the icon (E3: overflow area; a second start ends silently), add `/pipe` (link to the launcher
  steps, E2), start the game and load a save (`Waiting for game` → `Receiving`), `Start recording` (E6), play until
  the second tooltip line shows, `Stop recording`, `Open recordings folder` (E8), `Exit`, replay (S5, link to
  *Recording and replay*), `Replay finished`, then `Exit` and start the Companion again for normal use.
- **Launcher steps** go into *Connecting to the game* (German *Verbindung zum Spiel*) as a bold lead-in
  "**Adding `/pipe`**" followed by two bullets: *Ubisoft Connect (every copy, also from Steam)* and *Steam
  (alternative for a Steam copy)*, each a path of labels (facts 11, 12) ending in "type `/pipe`", then one sentence
  for E2 and the Steam fallback of E1. The German part gives the German labels; the English part the English labels.
  Each path carries "Not yet tried" until the dry run shows it.
- **Tray table**: a third column `What to do` (German `Was tun`); rows in today's order, plus two rows for the
  suffixes ` (recording)` and ` (recording failed)` at the end. `Waiting for game`: if the game runs, check `/pipe`
  and restart the game; with the Steam option set, add it in Ubisoft Connect. `Pipe busy`: E5. `Protocol mismatch`
  and lasting `Error`: E9, link to *Reporting problems*. `Connected`, `Receiving`, ` (recording)`: "nothing".
- **Recording and replay**: the record bullet gains the `-2`, `-3` suffix and "a session can span several files";
  the replay bullet gains the procedure of S5 as a numbered sub-list (`Exit`, copy both paths, *Run* dialog, the line
  `"<path of Censio.Companion.exe>" --replay "<path of the file>" --speed max`, `Replay finished`, `Exit`). The
  privacy paragraph adds "keep it until a maintainer asks how to hand it in" (as *Reporting problems* says).
- **`README.md`**: *Status* rewritten per S6 (M1, "the coming release `v0.1.0`", "Works in `v0.1.0`" / "Not in
  `v0.1.0`" lists). In *Companion*, the subsections *Connecting to the game* and *Recording and replay* (and the
  German *Verbindung zum Spiel*, *Aufzeichnen und Abspielen*) are replaced by one `#### First recording` (German
  `#### Erste Aufzeichnung`) of at most five numbered lines — add `/pipe` in the launcher, start the game and load a
  save, `Start recording`, `Stop recording`, files in `%LOCALAPPDATA%\Censio\Recordings` and private — with a link
  to the guide's walkthrough (`docs/USER_GUIDE.md#first-recording-step-by-step`; German
  `docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt`). The README's *Status* reference "(*Recording and
  replay*)" points to the guide instead.
- "Not yet tried" uses the wording the documents already use ("Not yet tried: …" / "Noch nicht ausprobiert: …"),
  once per paragraph or list, after its statements.

## Options considered

- Walkthrough placement: a new `##` section (chosen: S1 spans download, start and recording, which sit in three
  sections today) against a list inside *Companion* (lost: it would sit before *Build from source*, which E10 sends
  readers to, and before *First start*).
- Replay command: the *Run* dialog (chosen in the spec's *Challenge*, M1) against a command per console (lost: three
  syntaxes, `%LOCALAPPDATA%` and `&` differ, the Product Manager's console unknown).
- README: replacing its duplicate tray and recording text with a short path (chosen, record 0013) against keeping
  both copies in step (lost: four copies of the new column and the launcher steps).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `docs/USER_GUIDE.md` | walkthrough, launcher steps, table column, recording and replay (English and German) |
| — | `README.md` | *Status*, short *First recording* path replacing two subsections (English and German) |

## Signatures (for the Dev's skeleton)

none

## Test files

none. Existing test call sites affected: none. Files no unit test can cover: none (no code).

## Areas

none: no behavior changes. Task 4 moves, in `docs/areas/companion.md` *Limits and errors*, the points of the
"Not verified yet" list (line 229) that the dry run showed (the second tooltip line, the three icon looks) to a
verified sentence naming the run; points it did not show stay.

## Documentation updates

All by the Dev, after the dry run where marked:

- `docs/USER_GUIDE.md` (English and German): as *Approach*; labels and "Not yet tried" marks finalized after the dry
  run.
- `README.md` (English and German): as *Approach*. The links in *User guide* / *Benutzerhandbuch* stay.
- `docs/areas/companion.md`: as *Areas*, after the dry run.
- PR template checklist: README updated (above); `docs/ARCHITECTURE.md` unchanged (no flow or guarantee changes);
  the project has no changelog.

Sweep: `README.md`, `docs/USER_GUIDE.md`, `docs/areas/*.md` and `docs/ARCHITECTURE.md` were searched for `/pipe`,
`launch`, `Recording and replay` and the tray-state list; the only hits are the sections above and `README.md` line 14
(*Status*). `docs/areas/pipe.md` line 250 and `docs/ARCHITECTURE.md` mention `/pipe` as behavior and stay unchanged.

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` is touched: the documents describe existing
behavior only. The privacy rule (no raw recording shared) is restated, not weakened.

## Security considerations

- [x] no limits: no parsed input changes.
- [x] none: no exception reaches a user.

## Decision records

none: the placement applies record 0013 as written, the language record 0030; the *Run* dialog choice is a
documentation procedure without lasting architectural weight, settled in the spec's *Challenge*.

## Out of scope / follow-ups

As in [spec.md](spec.md), *Out of scope*. No follow-up issue.
