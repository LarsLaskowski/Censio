# Plan: Split the game-catalog area document

Source: Issue #148
Tier: trivial — documentation under `docs/areas/` and `docs/decisions/` (not tier `docs`), beyond `PATCH_MAX_LINES`, no behavior change and no new record.
Security areas touched: `.squad/project.md` (*Security areas*): only the area links of the parsing and file-path entries are retargeted; no area is added, removed or narrowed.

## Problem / root cause

`docs/areas/game-catalog.md` is past `AREA_MAX_WORDS` (5,000). Its *Icons* and *Cache* parts become two narrower
areas; behavior and wording stay as they are.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | 5,015 words (`wc -w`: 5,008) | `config-check.py`, `decision-check.py` warning | confirmed |
| 2 | *Icons* 743, *Cache* 1,068 words; with their parts of *Limits and errors*, the icon test and *Privacy*, about 2,850 remain | word count per heading | confirmed |
| 3 | Records 0050–0053 (`Area: game-catalog`) are unreleased, so editable | not in `v0.1.0` (`git cat-file -e v0.1.0:<path>`), the only tag of `git ls-remote --tags origin` | confirmed |
| 4 | Links into the moved parts | `grep -rn game-catalog`: companion.md:112, 139, 146, 227, 235, 315; game-files.md:128, 144; game-installation.md:63, 113; ARCHITECTURE.md:21, 59; `.squad/project.md`:25, 26, 28; records 0050:38, 0051:31, 0052:40, 0053:11, 31; README.md index rows `game-files`, `game-catalog` | confirmed |
| 5 | No code, test or script reads game-catalog.md | grep over `*.cs`, `*.py`, `*.yml`, `*.json`: no hit | confirmed |
| 6 | game-files.md:144 calls the cache future work ("the icon cache … (#25, #26)"); #144 shipped it | game-catalog.md *Cache*; record 0052 source #25 | confirmed |

## Acceptance criteria

- AC1: game-catalog.md, game-catalog-icons.md and game-catalog-cache.md are each below `AREA_MAX_WORDS`.
- AC2: Behavior unchanged: every bullet and table row moved from game-catalog.md on `origin/main` stands once in one of
  the three documents, changed only in its cross-references.
- AC3: The area index lists both new areas; `decision-check.py` and `config-check.py` pass.
- AC4: No reference names game-catalog.md for the icons or the cache.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: `decision-check.py` warns about none of the three | — |
| AC2 | check: Reviewer, `git diff --word-diff origin/main` | — |
| AC3 | check: `decision-check.py`, `config-check.py` | — |
| AC4 | check: `grep -rn "game-catalog.md" --include=*.md .` hits only content still in game-catalog.md | — |

## Verification without tests

No production or test code: steps 4, 5 and the *Coverage gate* do not apply.

- AC1, AC3: check: Dev runs the scripts before hand-back; Reviewer in step 8.
- AC2: check: Reviewer, the word diff.
- AC4: check: Dev and Reviewer, the grep.

## Approach

New documents from `docs/areas/_template.md`, bullets unchanged except cross-references.

- game-catalog-icons.md: *Icons* whole (with its two tables); from *Limits and errors* the *Icons* bullet and the
  icon sentence of *Memory*; the icon test bullet of *Tests with the real files*; the icon sentences of *Privacy*.
- game-catalog-cache.md: *Cache* whole except its last bullet (`Catalog cannot name`, which joins *Use by the
  Companion*); the cache-load sentence of *Memory*; the cache sentence of *Privacy*.
- Stays in game-catalog.md, once: *Time* and *Network* (they span names, icons and cache; references to `*Icons*`
  and `*Cache*` become links), *Failures*, the names test. *Scope* keeps one sentence on loading and the check,
  linking both new documents; *Files and nodes* and *Icons* → *Kept and sent nowhere* link the cache document.
- *Related decisions*: 0050, 0051 to icons; 0052, 0053 to cache; 0023 also in icons. Fact 6 is a related defect,
  fixed here.

## Options considered

- Move only *Cache* (about 3,900 left): lost — icons have their own records, failure kinds, bounds and test.

## Signatures (for the Dev's skeleton)

None: no code changes.

## Test files

None. Files no unit test can cover: none.

## Areas

- New `game-catalog-icons`: the icons of goods and regions — entries, path rule, archives and order, DDS/BC7 to PNG,
  reasons, failure kinds, bounds, the icon test, privacy. Not here: the archives (game-files), the cells (companion),
  the cache (game-catalog-cache).
- New `game-catalog-cache`: the catalog cache — key, start, later checks, what is cached, layout, bounds, validation,
  writes and pruning, privacy. Not here: the cache folder and log lines (companion), the build (game-installation).

## Documentation updates

All by the Dev:

- game-catalog.md: as *Approach*; *Not here* links both new documents.
- `docs/areas/README.md`: rows for both areas after `game-catalog`; that row drops icons and cache from *Scope*; the
  `game-files` row's *Not here* names game-catalog-icons.
- Fact 4 links retargeted (companion.md:315 and game-installation.md:113 name the new documents beside
  game-catalog.md); game-files.md:144 names game-catalog-cache.md, #26 stays.
- `.squad/project.md` (Dev reports the text, orchestrator writes it): line 25 `[game-catalog-icons](../docs/areas/game-catalog-icons.md),
  [game-catalog-cache](../docs/areas/game-catalog-cache.md)` in place of `[game-catalog](…) (*Icons*)`; line 26 the
  same; line 28 both appended after the game-catalog link.

## Architecture check

No guarantee changes.

## Security considerations

- [x] No limits change.
- [x] None.

## Decision records

None new. The Lead set `Area:` and links of 0050, 0051 (`game-catalog-icons`) and 0052, 0053 (`game-catalog-cache`);
`decision-check.py` passes once the index rows exist.

## Out of scope / follow-ups

- PR #153 (#141) edits game-catalog.md links and its index row: whoever merges second resolves the conflict and
  retargets fact 4's companion.md links that #153 moves into its new documents.
