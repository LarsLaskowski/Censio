# Plan: Shorten .squad/project.md below PROJECT_MAX_WORDS

Source: Issue #214
Tier: standard — a documentation-only rewording of `.squad/project.md` that edits *Security areas* beyond the `patch/security` budget (`.squad/routing.md`, *Tiers*, the paragraph on `.squad/stack.md` and `.squad/project.md`), removing and narrowing no area.
Security areas touched: `.squad/project.md` (*Security areas*): wording of the section's introduction and of the areas Ingest token and Companion authentication, Server database, Parsing of external input, Logging and showing external data and Outbound calls; no area removed or narrowed, no named type, file, endpoint or link dropped

## Problem / root cause

`.squad/project.md` has 1620 words on this branch (off `main` 24c33de) against `PROJECT_MAX_WORDS` 1500, so every
feature that adds an entry must trim others first. Wording was trimmed twice already (#198, #210); the remaining
prose sits in all four sections, *Security areas* included, so a cut that leaves headroom has to reach that section.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The issue's count of 1516 words on `main` is outdated: today 1620 | `config-check.py` on this branch: `.squad/project.md: 1620 words (PROJECT_MAX_WORDS 1500)`; #232 added 117 words after the issue | refuted |
| 2 | `config-check.py` counts `len(text.split())` | `.squad/tools/config-check.py` line 392 | confirmed |
| 3 | Words per section today: intro 71, *Security areas* 517, *Guarantees* 117, *Integration surface* 429, *Test doubles* 482 | `split()` per `## ` section, read-only | confirmed |
| 4 | Trimming every section but *Security areas* reaches only about 1513 words | the Lead's trial text, counted with `split()` | confirmed |
| 5 | An edit to *Security areas* is `patch/security` within 30 changed lines, otherwise `standard`; `security` only when it removes or narrows an area | `.squad/routing.md` lines 99–102; `PATCH_MAX_LINES` 30 counts added plus deleted lines (`scope-check.py` line 23) | confirmed |
| 6 | The behavior dropped from *Security areas* lines is stated in its area document: acknowledging every message in ingest.md line 10, names shown as text in web-pages.md lines 36 and 124, what replay sends in replay-sending.md (*What is sent*, lines 76–92), the browser's UTC offset in web-pages.md line 147 | read | confirmed |
| 7 | The eight `Ingest*` types of the first area are exactly `src/Censio.Server/Ingest*.cs` | `find src -name 'Ingest*'`; not used (option 3) | confirmed |
| 8 | Every test double the section names exists at the path or folder given | `find tests -name <name>.cs` for each of the 41 names | confirmed |
| 9 | `InMemoryDuplexStream` is also linked into the Companion tests, which its entry does not say | `tests/Censio.Companion.Core.Tests/Censio.Companion.Core.Tests.csproj` line 17; a related defect, fixed here (AC5) | confirmed |
| 10 | `.squad/project.md` is written by the orchestrator from the owner's exact text: the Tester for *Test doubles*, the Dev for the rest | `.squad/team.md` line 75 | confirmed |
| 11 | The PR template asks for no changelog; no other document states the file's word count | `.github/pull_request_template.md`; no `CHANGELOG` | confirmed |

## Acceptance criteria

- AC1: `.squad/project.md` has at most 1450 words as `config-check.py` counts them, and `config-check.py` prints
  no warning for it.
- AC2: Every backticked name and every Markdown link target of the old file is still in the new one, except the
  two words `docs/areas/` (*Integration surface*, service block) and `security` (the *Security areas* introduction's
  tier clause).
- AC3: *Guarantees* is unchanged byte for byte.
- AC4: *Security areas* keeps its eleven areas with their names (the text before the first colon) unchanged, and
  each area still names every type, file, endpoint, setting and link it names today.
- AC5: Every test double keeps its file or folder, its "linked into" note and its "checked against" or "checked by"
  note; `InMemoryDuplexStream` says it is linked into the Companion tests.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: `config-check.py` (below) | none |
| AC2 | check: token comparison (below) | none |
| AC3 | check: `git diff` (below) | none |
| AC4 | check: Security's diff reading (below) | none |
| AC5 | check: Reviewer's reading (below) | none |

## Verification without tests

Documentation only: steps 4, 5 and the *Coverage gate* are not applicable; step 7 is the *Format check* alone.

- AC1: check: the Dev runs `python3 .squad/tools/config-check.py` (no `.squad/project.md` line) and
  `python3 -c "print(len(open('.squad/project.md', encoding='utf-8').read().split()))"` (at most 1450); the
  Reviewer repeats both in step 8.
- AC2: check: the Reviewer extracts every match of the regular expressions `` `[^`]+` `` and `\]\([^)]+\)` from
  `git show origin/main:.squad/project.md` and from the new file and compares the sets: the only names missing are
  the two AC2 allows.
- AC3: check: the Reviewer confirms `git diff origin/main -- .squad/project.md` has no hunk in *Guarantees*.
- AC4: check: Security reads the *Security areas* hunks in its plan-review delta or the Reviewer in step 8: area
  names unchanged, eleven bullets, every name of each old line in its new line.
- AC5: check: the Reviewer compares the *Test doubles* entries old against new.

## Approach

The Lead's draft of the whole file is `specs/issue-214/project-draft.md` (1444 words). The Dev checks it against
AC1–AC4 and reports its text outside *Test doubles* as the exact text; the Tester checks and reports *Test doubles*
(AC5); the orchestrator writes both into `.squad/project.md` with CRLF line endings and deletes the draft from the
work folder before the PR opens. A member that finds a wording that changes meaning restores the old words and
shortens elsewhere, staying within AC1.

What the draft does:

- Intro: one sentence for the file's purpose; the pointer rule kept.
- *Security areas*: introduction cut to what a plan does and what a line holds; behavior phrases leave the lines
  (Verified fact 6): `IngestSession` "storing and acknowledging every message type after the accepted hello" becomes
  "with every message type it stores, `ContractTradeRoutes` included"; the trade-route clause lists its members
  without "removed by"; "(the browser's UTC offset)", "(route, folder and island names are shown as text)" and
  "which sends ticks, trade routes, session events and status" are dropped. Network exposure, Data-protection key
  ring, Local pipe connection, File paths, Windows autostart and Release publishing are unchanged.
- *Guarantees*: unchanged.
- *Integration surface*: introduction shortened, labels and articles cut, every name kept.
- *Test doubles*: descriptions cut to what each stands in for (their behavior is in their code); every path,
  "linked into" and provenance note kept; the `InMemoryDuplexStream` link added.

Headroom: 56 words. Wording alone gets no further without dropping names (fact 4 and the draft); a larger margin
needs a structural choice the options below reject for this product PR.

## Options considered

1. Trim every section but *Security areas* (tier `docs`): lost, reaches about 1513 words (fact 4).
2. Raise `PROJECT_MAX_WORDS` in `squad_settings.py`: lost, the issue's comment scopes Censio's part to shortening
   the file; the caps are item 2 of Squad-Spec-Repository-Template#434.
3. Replace the eight `Ingest*` names by the glob `src/Censio.Server/Ingest*.cs` (same set, fact 7): lost, a
   member looking up a type by name no longer finds it in a security area, for 7 words.
4. Move *Test doubles* to `docs/UNIT_TESTS.md` with a pointer (about 400 words of headroom): lost, `.squad/team.md`
   (*Files*), `squad-tester.md` and the hint of `config-check.py` keep one line per test double in this file, which a
   product PR cannot change; proposed for the step-12 squad lesson instead.
5. Move the per-kind lists of *Integration surface* into the area documents: lost, the section is the Reviewer's
   checklist of coupling points, and `ingest.md` and `history.md` are already at `AREA_MAX_WORDS`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| none | `.squad/project.md` | wording shortened |

## Signatures (for the Dev's skeleton)

None: the change has no API.

## Test files

None. Existing test code that calls a changed signature: none. Files no unit test can cover: none (documentation
only).

## Areas

none (no change in behavior).

## Documentation updates

- `.squad/project.md`: the draft's text, reported by the Dev (all but *Test doubles*) and the Tester (*Test
  doubles*), written by the orchestrator.

## Architecture check

No guarantee of `docs/ARCHITECTURE.md` or `.squad/project.md` is touched: *Guarantees* stays byte-identical (AC3),
and no security area loses scope (AC4).

## Security considerations

- [x] Every byte or length limit on parsed input says whether it applies before or after decoding — no limits.
- [x] Every exception that can reach a user-visible failure reason or message is listed — none.

## Decision records

- none: no decision beyond the obvious fix

## Out of scope / follow-ups

- Step-12 squad lesson (for Squad-Spec-Repository-Template#434): wording trims leave a project of this size about
  50 words under `PROJECT_MAX_WORDS`; a durable margin needs the template to allow *Test doubles* outside
  `.squad/project.md` or a higher cap.
