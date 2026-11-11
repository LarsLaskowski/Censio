# Plan: Split the companion area document

Source: Issue #141
Tier: trivial — documentation under `docs/areas/` and `docs/decisions/` (not tier `docs`), far beyond `PATCH_MAX_LINES`, no behavior change and no new record.
Security areas touched: `.squad/project.md` (*Security areas*): only the area links of the file-path and logging entries are retargeted to the new documents; no area is added, removed or narrowed.

## Problem / root cause

`docs/areas/companion.md` is past `AREA_MAX_WORDS` (5,000). Its *Log* and *Diagnostics window* sections become two
narrower areas; behavior and wording stay as they are.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | companion.md has 5,192 words | `wc -w docs/areas/companion.md` | confirmed |
| 2 | *Log* 1,014, *Diagnostics window* 965, *Settings file* 256 words; moving the first two leaves about 3,250 | word count per `###` heading | confirmed |
| 3 | The decision records' `Area:` fields can be updated | 0004, 0017, 0027, 0037, 0038 (`Area: companion`) are in `v0.1.0` (`git tag --contains`), released, so append-only; 0045 and 0048 are not | refuted |
| 4 | Every link into the moved sections | `grep -rn companion.md`: pipe.md:121; pipe-connection.md:53, 75; game-files.md:78, 145; game-catalog.md:170, 243, 249, 347; game-installation.md:85; ARCHITECTURE.md:57; AGENTS.md:137 and copilot-instructions.md:136 (project block `code-style`); `.squad/project.md`:26, 28, 76; record 0048:30; README.md index rows `companion` and `game-catalog` | confirmed |
| 5 | No code, test or script reads companion.md | grep over `*.cs`, `*.py`, `*.yml`: no hit | confirmed |

## Acceptance criteria

- AC1: companion.md, companion-log.md and companion-diagnostics.md are each below `AREA_MAX_WORDS`.
- AC2: Behavior unchanged: every bullet of *Log* and *Diagnostics window* on `origin/main` stands in the new document,
  changed only in its cross-references.
- AC3: The area index lists both new areas; `decision-check.py` and `config-check.py` pass.
- AC4: No reference anywhere names companion.md for the log or the diagnostics window.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: `decision-check.py` warns about none of the three | — |
| AC2 | check: Reviewer, `git diff --word-diff origin/main -- docs/areas` | — |
| AC3 | check: `decision-check.py`, `config-check.py` | — |
| AC4 | check: `grep -rn "companion.md" --include=*.md .` hits only content still in companion.md | — |

## Verification without tests

No production or test code: steps 4, 5 and the *Coverage gate* are not applicable.

- AC1: check: Dev runs `decision-check.py` before hand-back; Reviewer in step 8.
- AC2: check: Reviewer, word diff against `origin/main`.
- AC3: check: Dev runs both scripts before hand-back.
- AC4: check: Dev and Reviewer, the grep above.

## Approach

Move both sections whole into documents from `docs/areas/_template.md`, bullets under *Behavior*, unchanged
except cross-references (a `(*Log*)` becomes a link to companion-log.md, a reference to a section that stays,
such as *Tray icon and menu* or *Folders*, becomes a link to companion.md with its section). companion.md keeps the
menu entry `Open diagnostics`, *Folders* (the log folder) and *Settings file*, and its *Scope*, *Limits and errors*
and *Not here* point at the new documents. The 0.5-second exit bound and "a log that cannot be written" stay
stated once: the writer rules in companion-log.md, the general no-message rule in companion.md linking there.

## Signatures (for the Dev's skeleton)

None: no code changes.

## Test files

None. Files no unit test can cover: none.

## Areas

- New `companion-log` (`companion-log.md`): the Companion's log file — path and daily files, size limit and
  continuation, pruning, line format, what is logged (catalog, cache and icon lines included), what is never logged,
  sanitizing and length cuts, exception texts, the background writer and its bounds. Not here: the log folder entry
  (companion).
- New `companion-diagnostics` (`companion-diagnostics.md`): the diagnostics window — header, table, numbers, catalog
  names, icons, area names, update, kept tick, single window, read path, memory, privacy. Not here: the menu entry
  (companion), the catalog and icons (game-catalog).
- *Settings file* stays in companion: it is Companion-wide (later settings share it), alone it would be a few
  sentences (README: fold those into a neighbor), and game-installation is only its first reader.

## Documentation updates

All by the Dev:

- companion.md: sections removed, *Scope*, *Limits and errors*, *Not here* and in-text `(*Log*)` references
  retargeted; *Related decisions* 0037 moves to companion-log.md, 0038 to companion-diagnostics.md, 0048 is
  listed in all three.
- `docs/areas/README.md`: rows for both new areas after `companion`; the `companion` row drops the log file;
  the `game-catalog` row's *Not here* names companion-diagnostics and companion-log for the cells and log lines.
- The links of fact 4 in pipe.md, pipe-connection.md, game-files.md, game-catalog.md, game-installation.md and
  ARCHITECTURE.md:57 retargeted.
- AGENTS.md:137 and `.github/copilot-instructions.md`:136, identically: "what may be logged is in
  `docs/areas/companion-log.md`."
- `.squad/project.md` (Dev reports the text, orchestrator writes it): line 26 `(*Recording*, *Folders*, *Settings
  file*), [companion-log](../docs/areas/companion-log.md)`; line 28 `[companion-log](../docs/areas/companion-log.md),
  [companion-diagnostics](../docs/areas/companion-diagnostics.md)` in place of the companion link; line 76
  `docs/areas/companion-log.md`.

## Architecture check

No guarantee changes.

## Security considerations

- [x] No limits change.
- [x] None.

## Decision records

None new. Released records stay unedited; their companion.md links still resolve.
Record 0048 (unreleased): the Lead changed its closing sentence to name the three documents; `Area:` stays
`companion`.

## Out of scope / follow-ups

- game-catalog.md (5,008 words) is over the limit too; not this issue.

## Security notes

- Non-blocking (Security, plan review): AC4 reads "no reference anywhere"; released records 0037 and 0038 still link companion.md for the log and the diagnostics window and stay unedited. Read AC4 as "outside released decision records".
