# Plan: Bring .squad/project.md back under PROJECT_MAX_WORDS

Source: Issue #196
Tier: standard — a documentation-only change to `.squad/project.md` that edits four *Security areas* entries beyond the `patch` budget (81 changed lines against 30) without removing or narrowing an area, which `.squad/routing.md` (*Tiers*, the paragraph under the table) makes `standard`.
Security areas touched: `.squad/project.md` (*Security areas*): the entries Network exposure, Parsing of external input, File paths the Companion writes, prunes or opens, and Logging and showing external data are reworded; no area is added, removed or narrowed.

## Problem / root cause

`.squad/project.md` has 1710 words against `PROJECT_MAX_WORDS` 1500 (`.squad/tools/squad_settings.py:46`), so
`config-check.py` warns. The words are prose inside entries that the file's introduction (lines 8–11) says
belongs in the area documents and records: descriptive glosses beside type names in *Security areas* (lines 22,
26, 27, 29), behavior of test doubles (*Test doubles*, e.g. line 90), and a duplicated test double (lines 89
and 105).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The file has 1710 words, the limit is 1500 | `python3 .squad/tools/config-check.py`: the warning names 1710 and 1500; `squad_settings.py:46` | confirmed |
| 2 | The section sizes are: introduction 111, *Security areas* 505, *Guarantees* 149, *Integration surface* 390, *Test doubles* 555 | whitespace split per `## ` section, as `check_project_size` counts (`config-check.py:392`) | confirmed |
| 3 | `RecordingLoggerProvider` is listed twice | `.squad/project.md:89` and `:105`, both `tests/Censio.Server.Data.Tests/` | confirmed |
| 4 | Related defect: `RecordingLogger` is linked into the Companion and Server.Data tests, `PipeFrameBuilder` into the Companion and Anonymiser tests (the file names only the Companion) | `<Compile Include>` in `tests/Censio.Server.Data.Tests/Censio.Server.Data.Tests.csproj:7`, `tests/Censio.Anonymiser.Tests/Censio.Anonymiser.Tests.csproj:8`, `tests/Censio.Companion.Core.Tests/Censio.Companion.Core.Tests.csproj:10,13` | confirmed |
| 5 | Related defect: the configuration-option tests list omits `IngestTokenSetupTests` and keeps `DatabaseSetupTests` in the key line | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs` exists; `.squad/project.md:49,54` | confirmed |
| 6 | Related defect: "later `Censio.GameData`" (line 63) is stale: the project exists, and the contract and catalog document are read by `ContractReader` and `CatalogDocumentReader` | `src/Censio.GameData/`; `.squad/project.md:26` names both readers | confirmed |
| 7 | Issues #35 and #41 named in *Guarantees* are still open, so their pointers stay | `gh api repos/LarsLaskowski/Censio/issues/35` and `/41`: `open` | confirmed |
| 8 | Without an edit to *Security areas* the file cannot get under 1500 with margin: all other edits of `project-target.md` leave about 1530 words | scratch application of the non-*Security areas* edits only | confirmed |
| 9 | `project-target.md` has 1458 words and drops no backticked name or link target from *Security areas*, *Guarantees* or *Integration surface* (the link to `contract.md` moves from prose into the link list) | scratch comparison of the sets of backticked tokens and link targets per section, original against `project-target.md` | confirmed |
| 10 | No released decision record or area document needs a change: the edit moves no behavior out of the file, it drops glosses the linked area documents already state | read: `docs/areas/web-pages.md`, `companion.md`, `companion-diagnostics.md`, `contract.md` cover the routes, folders, diagnostics window and contract messages | confirmed |

## Acceptance criteria

- AC1: `python3 .squad/tools/config-check.py` prints no warning for `.squad/project.md`, and the file has at most
  1460 words counted as `check_project_size` counts them.
- AC2: No security area is removed or narrowed: every backticked type, file, endpoint and setting and every
  linked document of *Security areas* before the change is still named in the same entry, and the section's
  introduction is unchanged.
- AC3: *Guarantees* keeps each guarantee's wording, record and area-document pointer; *Integration surface* keeps
  every coupling point; *Test doubles* lists every double once, with its file.
- AC4: The related defects of facts 3–6 are fixed: one `RecordingLoggerProvider` entry, the linked test projects
  named in full, `IngestTokenSetupTests` and `DatabaseSetupTests` in the tests line, no "later `Censio.GameData`".
- AC5: Only `.squad/project.md` changes outside `specs/` (`scope-check.py`).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: *Verification without tests* | none |
| AC2 | check: *Verification without tests* | none |
| AC3 | check: *Verification without tests* | none |
| AC4 | check: *Verification without tests* | none |
| AC5 | CI: `scope-check.py` in the step-7 stamp | none |

## Verification without tests

No production or test code changes: steps 4, 5 and the *Coverage gate* are not applicable; step 7 is *Format
check* alone (Markdown only).

- AC1: check: the orchestrator runs `python3 .squad/tools/config-check.py` after writing the file (no
  `project.md` warning) and logs the word count.
- AC2: check: Security in step 3 reviews the four reworded entries in `project-target.md`; the Reviewer in step 8
  compares the *Security areas* section of the diff name by name.
- AC3: check: the Reviewer in step 8 reads the *Guarantees*, *Integration surface* and *Test doubles* hunks
  against the original.
- AC4: check: the Reviewer in step 8, against facts 3–6.
- AC5: CI: `scope-check.py` in the step-7 stamp.

## Approach

The exact text is `specs/issue-196/project-target.md` (CRLF, as the original): the whole file as it should read,
built from `.squad/project.md` at 456c772 (its last change on `main`). The Dev (all sections but *Test doubles*)
and the Tester (*Test doubles*) each report in step 6 that the target stands for their section, or the exact
change they need; the orchestrator then copies it over `.squad/project.md` — if `main` changed the file
meanwhile, it reapplies the hunks of `git diff --no-index` between the two instead — runs *Format* and
`config-check.py`, and deletes `project-target.md` before the PR.

The edits, by section:

- Introduction: the two paragraphs shortened, same rules (no "copy goes stale" reason).
- *Security areas* (introduction untouched): Network exposure moves `IconEndpoint` beside `Program.cs` and drops
  "read on `/`"; Parsing of external input drops the glosses ("the settings file", "Ubisoft Connect's install
  records", the parenthetical on the two projects' contents) and keeps every type, with `contract.md` as a link in
  the list; File paths lists the types without the "recordings (…)" glosses; Logging and showing names "its
  diagnostics window" and "the server's request logging and pages" (both wider than before, never narrower).
- *Guarantees*: introduction shortened; the area-document pointers of the history and tick-buffer lines
  rephrased, the guarantees' words unchanged.
- *Integration surface*: facts 5 and 6; the README table's column list and the second-pass gloss on
  `PipeFrameAnonymiser` dropped (record 0042 stays).
- *Test doubles*: introduction shortened, same rule; fact 3 and 4; behavior clauses dropped (what a double does
  is read in its code); the "checked against" notes of modelled components kept (`.squad/team.md`, *Test doubles
  that model a real component*).

## Options considered

- Edit only the sections outside *Security areas*, tier `docs`: lost — it ends near 1530 words (fact 8), still
  over the limit, and the next feature crosses it again.
- `patch/security` within `PATCH_MAX_LINES`: lost — the 15 bullet edits that fit save about 150 words, not 210.
- Raise `PROJECT_MAX_WORDS`: lost — the issue asks to shorten, and the warning exists because five members read
  the file on every launch.
- Move content into area documents: not needed — the dropped glosses are already stated there (fact 10).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| none | `.squad/project.md` | shortened as `project-target.md` |

## Signatures (for the Dev's skeleton)

None: no API changes.

## Test files

None: no test code changes.

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

None (no change in behavior).

## Documentation updates

- `.squad/project.md`: the text of `specs/issue-196/project-target.md`; content owners Dev (all but *Test
  doubles*) and Tester (*Test doubles*) report it, the orchestrator writes it (`.squad/team.md`, *Files*).

## Architecture check

No guarantee of `docs/ARCHITECTURE.md` or `.squad/project.md` changes; *Guarantees* keeps every guarantee as
worded.

## Security considerations

- [x] No limits: no parsed input changes.
- [x] No exception reaches a user: none.

## Decision records

- none: no decision beyond the obvious fix.

## Out of scope / follow-ups

- The *Security areas* introduction and the remaining entries; a later shortening, when the file grows again, is
  its own issue.
