# Plan: Split the history and ingest area documents past AREA_MAX_WORDS

Source: Issue #230
Tier: standard — documentation only, but it edits area documents and two unreleased decision records (not `docs`) and adds links to two entries of `.squad/project.md` *Security areas*, which *Tiers* (`.squad/routing.md`, the paragraph after the table) makes `standard` beyond the `patch` budget.
Security areas touched: `.squad/project.md` (*Security areas*: links added to the entries *Ingest token and Companion authentication* and *Server database*; no area added, removed or narrowed); the area documents of those two security areas, moved text only — no security control is created, changed or removed.

## Problem / root cause

`decision-check.py` (`check_area_sizes`, `.squad/tools/decision-check.py:427`) counts whitespace-separated words of each
area document and warns past `AREA_MAX_WORDS` (5,000, `squad_settings.py:47`): `docs/areas/history.md` has 5,093,
`docs/areas/ingest.md` 5,008. `docs/areas/README.md`, *Size*, asks for a split into narrower areas.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | history.md is over the limit (issue: about 5,085; today 5,093) | `decision-check.py`: 5,093 words | confirmed |
| 2 | ingest.md has 5,008 words | `decision-check.py` | confirmed |
| 3 | The issue's tier `docs` fits | `.squad/routing.md`, *Tiers*: `docs` excludes Markdown under `docs/areas/` and `docs/decisions/` | refuted |
| 4 | history.md *Deleting a profile* (lines 162–187), its log rows (197–200), its limits (206–208, 210–211) and its measurement rows (228–230, 232–234, 238, 240–241) are about 900 words | counted per line range | confirmed |
| 5 | ingest.md *Token* (49–87), *Start failures* (223–249) and the token's risks (264–271) are about 1,100 words | counted per line range | confirmed |
| 6 | Links that name a moved section from outside: replay-sending.md:28 (*Token*), :165 (*Accepted risks*, the configured file), server-data.md:52, web-charts.md:230, record 0097:17 (*Deleting a profile*) | `grep -rn` over the repository | confirmed |
| 7 | Records 0084 and 0097 are unreleased; 0061 (Area `ingest`, links ingest.md) is released in v0.3.0 | `git log --diff-filter=A` + `git tag --contains`; `git ls-remote --tags origin`: v0.1.0–v0.3.0 | confirmed |
| 8 | No code, test or tool refers to the moved sections | `grep` over `src/`, `tests/`, `.squad/tools/` | confirmed |
| 9 | `.squad/project.md` is already over `PROJECT_MAX_WORDS` (1,620 of 1,500) | `config-check.py` warning on `main` | confirmed |
| 10 | `plan-check.py` cannot state a shrinking area (`+N words` only), so it warns for history.md and ingest.md on *Areas* | `plan-check.py:121`, `area_size_warning` | confirmed |

## Acceptance criteria

- AC1: `decision-check.py` and `config-check.py` print no `AREA_MAX_WORDS` warning; each of history.md, ingest.md,
  history-deletion.md and ingest-token.md is at most 5,000 words, and `decision-check.py` passes (no released record changed).
- AC2: `docs/areas/history-deletion.md` and `docs/areas/ingest-token.md` exist with the template's sections (*Scope*,
  *Behavior*, *Limits and errors*, *Related decisions*, *Not here*), each has its row in the area index of
  `docs/areas/README.md` directly after its parent's row, and the *Not here* of history.md and ingest.md link them.
- AC3: each moved behavior is stated once: these strings occur in exactly one file under `docs/areas/` — `The history of a
  profile was deleted.`, `The deleted history was cleared from the database file.`, `Residue of older versions`,
  `An ingest token was generated and stored in`, `exclude each other; the server stops.`, `cannot be created; the server
  stops.`, `created with mode` — and that file is the new document.
- AC4: moved text is unchanged apart from link targets and the reworded lead-ins named under *Approach*; every link that
  named a moved section, or the old document for moved content, points at the new document (the list under
  *Documentation updates*), and every relative link in the changed files resolves.
- AC5: *Format check* passes on the changed files.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | check: decision-check.py and config-check.py (below) | none |
| AC2 | check: read-only reading (below) | none |
| AC3 | check: grep (below) | none |
| AC4 | check: moved-lines diff and link resolution (below) | none |
| AC5 | check: Format check (below) | none |

## Verification without tests

Steps 4, 5 and the *Coverage gate* are not applicable: Markdown only (step 7: *Format check*, `gate-stamp.py`).

- AC1: check: the Dev runs `python3 .squad/tools/decision-check.py` and `config-check.py` after the edits and reports the
  four word counts; the Reviewer re-runs them. The pre-existing `PROJECT_MAX_WORDS` warning stays (*Out of scope*).
- AC2: check: the Reviewer reads both new documents and the index.
- AC3: check: the Dev runs `grep -rlF '<string>' docs/areas` per string and reports one file each; the Reviewer repeats it.
- AC4: check: the Reviewer reads `git diff --color-moved=plain main...HEAD -- docs/areas`: every moved block shows as
  moved, other changed lines are links, headings, *Scope*, *Not here*, index rows and the lead-ins below; the Dev
  resolves every relative link of the changed files (a short read-only script in the scratchpad, not committed) and
  reports the result.
- AC5: check: *Format check* (`.squad/stack.md`), run by the Dev in step 7.

## Approach

Move text verbatim; write new only the *Scope*, *Not here*, index rows and these lead-ins. Section names are those of
the template; the Dev writes each new document from `docs/areas/_template.md`.

**`docs/areas/history-deletion.md`** (`# History deletion`, about 1,250 words):
- *Scope*: deleting a profile's history — what is removed, the tombstone, the removal steps, secure delete and the
  truncating checkpoint, the residue of older versions, a deletion's log lines, failures and measurements; operators rely on it.
- *Behavior*, `### Deleting a profile`: history.md lines 164–187 as they stand, under the same bold leads (*Clean-up*,
  *Residue of older versions*); "(*The trend reads*)" becomes a link to history.md, *The trend reads*.
- *Behavior*, `### Log lines`: one sentence that the category and what no line holds are those of history.md, *Log
  lines*, then the table header and the four deletion rows (197–200).
- *Limits and errors*: the bullets of lines 206–208 and 210–211; `### Measurements`: one sentence that the sandbox, the
  fixture and the limit are those of history.md, *Measurements*, measured before the index of record 0087, then the rows
  228–230, 232–234 and 238 (row 238 names its throwaway test itself instead of "The same") and the paragraph 240–241.
- *Related decisions*: 0021, 0084, 0085 (0084 and 0085 leave history.md, 0021 stays in both).
- *Not here*: the retention job that runs the steps and the clean-up, the trend gate (history.md); the tables and the
  tombstone column (server-data.md); the state the deletion drops (server-state.md); the button, dialog and host rule
  (web-pages.md), `AllowedHosts` (server.md); shrinking the file and the residue: the user guide (moved from history.md).

**`docs/areas/history.md`** keeps the rest. *Scope* drops the deletion clause and links history-deletion.md; the
references in *The trend reads* (line 113) and *The retention job* (line 147) to *Deleting a profile* link
history-deletion.md; *Log lines* keeps its category sentence, the two fold rows and the sentence after the table;
*Limits and errors* keeps line 209; the second measurement table (with and without the index) stays whole, the
removal rows included, as it measures the index's cost. *Not here* drops the button line and the user-guide line and
gains "Deleting a profile's history, its clean-up and its measurements: history-deletion.md".

**`docs/areas/ingest-token.md`** (`# Ingest token`, about 1,250 words):
- *Scope*: the ingest token — its two sources, form and bounds, writing and mode, rotation, the hash comparison, the
  switch `Censio:Ingest:RequireToken`, the start failures and the token's accepted risks; the operator relies on it.
- *Behavior*, `### Token`: ingest.md lines 51–87 as they stand; "as in *The hello*" and "step 4 of *The hello*" link
  ingest.md, *The hello*.
- *Limits and errors*: `### Start failures` (lines 225–249), and `### Accepted risks` with **Token off** (264–265), **The
  configured file's access rules** (269–271), and the second sentence of **Plain text** (the Windows share), which
  joins the access-rules bullet.
- *Related decisions*: 0019, 0061 (0061 leaves ingest.md, 0019 stays in both).
- *Not here*: the endpoint, the hello and the `tokenRefused` refusal, the session's log lines and their privacy
  (ingest.md); the data directory and the place of the token file (server.md); the Companion's token file
  (replay-sending.md); the code: `IngestTokenSetup` and `IngestAccess` in `src/Censio.Server`.

**`docs/areas/ingest.md`** keeps the rest. *Scope*'s "the ingest token" becomes a link to ingest-token.md; step 4 of
*The hello* links ingest-token.md for the token; the bounds table drops its *Token* row (stated in ingest-token.md);
**Plain text** keeps its first sentence; *Related decisions* drops 0061; *Not here* gains "The token, its file and switch,
the start failures: ingest-token.md" and its code pointer drops `IngestTokenSetup` and `IngestAccess`.

No user-visible output.

## Options considered

- **Trivial tier, `.squad/project.md` untouched.** Lost: the *Ingest token* security area would point Security at a
  document that no longer states the token's rules, and the guarantees of the token and of the deletion at documents
  that no longer state them.
- **Other cuts.** From history, *The trend reads* (about 1,300 words with its measurements) into a trend-reads area:
  lost, because web-charts.md and web-trends.md link it in eight places and its rows share the history's measurement
  setup, a larger and riskier move. From ingest, *Limits per host* with its measurements: lost, because the issue names
  the token cut and *Limits per host* is wired into the HTTP refusals and *Closes* of the endpoint itself.
- **Trimming instead of splitting** (shortening wording under 5,000): lost, `docs/areas/README.md`, *Size*, asks for a
  split, and both documents would cross the limit again with the next change.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| docs | `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `docs/USER_GUIDE.md`, `docs/decisions/0084`, `0097`, `.squad/project.md` | documentation only |

## Signatures (for the Dev's skeleton)

None: no code changes.

## Test files

None. Existing test code that calls a changed signature: none. Files no unit test can cover: none.

## Areas

- New area `history-deletion`: name and scope as the index row under *Documentation updates* (Lead decision).
- New area `ingest-token`: likewise.
- docs/areas/history.md (5,093 words now, about 900 move out): the deletion parts leave.
- docs/areas/ingest.md (5,008 words now, about 1,000 move out): the token parts leave.
- Link edits only in server.md, server-data.md, server-state.md, web-pages.md, web-charts.md, replay-sending.md,
  contract.md (*Documentation updates*); README.md: the index.

`plan-check.py` warns for history.md and ingest.md here: it reads only added words, and both shrink (fact 10).

## Documentation updates

All by the Dev, once; the Dev reports the exact text of the `.squad/project.md` edits, which the orchestrator writes in.

- The two new documents and the two shortened ones (*Approach*).
- `docs/areas/README.md`, area index: history row loses "deleting a profile's history (tombstone, removal steps,
  secure delete, the truncating checkpoint, the residue of older versions)" from *Scope*; its *Not here* replaces "the
  button, dialog and host rule (see web-pages)" by "deleting a profile's history (see history-deletion)". New row after
  it: `history-deletion` | scope as its *Scope* | "The retention job and the trend gate (see history), the tables (see
  server-data), the held state (see server-state), the button, dialog and host rule (see web-pages)". Ingest row loses
  "the token (file, generation, rotation, switch, comparison)", its *Not here* gains "the token and the start failures
  (see ingest-token)". New row after it: `ingest-token` | scope as its *Scope* | "The endpoint and the hello (see ingest),
  the data directory (see server), the Companion's token file (see replay-sending)".
- Links to history-deletion.md instead of history.md: server-data.md:52 (drop "*Deleting a profile*"), :99, :139, and
  its *Not here* (:200) gains "deleting a profile's history: history-deletion.md"; server-state.md:89, :99, and :212
  split into retention (history.md) and deletion (history-deletion.md); web-pages.md:222, :259 (*Log lines*), :354;
  web-charts.md:230 (drop the section name); replay-sending.md:171.
- Links to ingest-token.md instead of ingest.md: replay-sending.md:28 (*Token*), :165 (*Accepted risks*), and :224 gains
  "token rules: ingest-token.md"; server.md:46, :73, :106, and :168 splits into ingest and token; contract.md:280 gains
  "its token: ingest-token.md".
- `docs/ARCHITECTURE.md`: line 22 the deletion of a profile links areas/history-deletion.md; line 23 the token links
  areas/ingest-token.md beside ingest.md; line 64 the deletion sentence adds areas/history-deletion.md beside record
  0084; line 71 links areas/ingest-token.md instead of ingest.md; line 92 adds areas/ingest-token.md beside ingest.md.
- `docs/USER_GUIDE.md`:177 and :656 (English and German) link `docs/areas/ingest-token.md` instead of ingest.md.
- `docs/decisions/0084-…`: `Area:` becomes `history-deletion`, its link (line 37) points at history-deletion.md;
  `docs/decisions/0097-…`:17 links `[history-deletion](../areas/history-deletion.md)` without the section name. Both
  unreleased (fact 7). Released 0061 stays unedited: its link to ingest.md still resolves and ingest.md links on.
- `.squad/project.md`: *Ingest token and Companion authentication* adds `[ingest-token](../docs/areas/ingest-token.md)`
  after `[ingest](…)`; *Server database* adds `[history-deletion](../docs/areas/history-deletion.md)` after
  `[history](…)`; the guarantees of the token and of the history add the same links after their area link.
- Unchanged: SECURITY.md (its ingest link covers the endpoint risks that stay), record 0087 (the trend gate stays in
  history.md), the user guide's history links (the fold measurements stay).

## Architecture check

No guarantee changes: the text moves between documents. `docs/ARCHITECTURE.md` gains links only.

## Security considerations

- [x] No limits: no parsed input changes.
- [x] None: no exception path changes.

Security's plan review covers the project.md links and that the token and deletion rules move whole, with nothing
dropped (AC3, AC4).

## Decision records

None: a new area is a Lead decision in this plan (`docs/areas/README.md`). Records 0084 and 0097 get link and `Area:`
edits only.

## Out of scope / follow-ups

- `.squad/project.md` over `PROJECT_MAX_WORDS` (1,620 of 1,500) already on `main`; this change adds about 8 words. Proposed
  issue: "[squad] Shorten .squad/project.md under PROJECT_MAX_WORDS" — make the long entries (*Security areas*,
  *Integration surface*) pure pointers.
- Squad lesson (step 12): `plan-check.py` *Areas* cannot express a document that shrinks (`+N words` only), so a split
  plan always gets the size warning for the document it splits.

## Security notes

Security plan review: specs/issue-230/plan.md (mode plan, round 1)

I read the plan and checked it against the sources: /home/user/Censio/docs/areas/ingest.md (lines 1-100, 186-290, 340-371), /home/user/Censio/docs/areas/history.md (lines 1-260), /home/user/Censio/.squad/project.md (*Security areas*, *Guarantees*), /home/user/Censio/.squad/routing.md (*Tiers*), /home/user/Censio/SECURITY.md, and every `ingest.md`/`history.md` link in the repository (`grep -rn 'ingest\.md\|history\.md' --include=*.md .`). No measurement is needed because nothing in the plan rests on measured behavior.

**Tier.** `standard` is correct. The diff changes no security control. It adds links to two *Security areas* entries and narrows or removes none, and routing.md lines 99-102 make such an edit `standard` once it is beyond the `patch` budget, with `.squad/project.md` listed under *Security areas touched*. The plan does that. Fact 3 is right that `docs` does not apply, because the diff edits Markdown under `docs/areas/` and `docs/decisions/`.

**Token statements (ingest.md).**
- *Moved:* the Token section (lines 51-87: sources, form and bounds, the temporary file and the move, mode `0600`, rotation, the SHA-256 fixed-time comparison, the `RequireToken` switch, the case with no data directory), *Start failures* (225-249) and the risks *Token off* (264-265) and *configured file's access rules* (269-271).
- *Stays:* hello step 4 and the refusal, the log-privacy paragraph (215-219), and the risks *A token holder that never reads* (262-263) and *Growth by a token holder* (272). These are endpoint rules, and the plan links ingest-token.md from step 4 and from *Not here*.
- *Bounds-table row:* dropping the *Token* row (line 206) loses nothing. Its three values (43 characters; 32 to 1,024; 65,536 bytes) are in lines 52 and 66-67, which move.
- *Log privacy:* stated consistently in both documents and not contradicted. Lines 60 and 64 move; line 215-217 stays.
- *Internal references:* `(*Start failures*)` at line 84 moves with its target. The two references to *The hello* become links, as the plan says.
- *Links:* server-state.md:160 and web-pages.md:322 point at *Accepted risks* in ingest.md for the token-holder growth risk. That risk stays, so they stay correct. SECURITY.md:36 states the token-off risk itself and links ingest.md only for the endpoint risks that stay, so leaving it unchanged is right. Released record 0061 is left unedited and its link still resolves.

**Deletion statements (history.md).**
- *Moved:* lines 164-187 (removal, tombstone, steps, *Clean-up*, *Residue of older versions*), the four deletion log rows, the limits at 206-208 and 210-211, and the deletion measurement rows.
- *Stays:* the deletion rules the retention job carries out. These are the sweep and clean-up order and their retry (147-153), the trend gate held through the truncation (111-115), resent ticks of a deleted profile (137-139), and a deletion between two read parts (116-120). history-deletion's *Not here* points back to them, and the Server database entry will link both documents.
- *Clean-up rule:* record 0087:44 and server-data.md:111 point at history.md for the gate that the clean-up holds. Lines 111-114 keep stating that rule, so leaving 0087 unchanged is right.
- *Internal references:* "the warning below" in the moved text stays true, because the log table moves after it in *Behavior*.
- *Release status:* records 0084 and 0097 are unreleased. I checked that `git tag --contains` on the commit that added each is empty; the tags are v0.1.0 to v0.3.0.

**Security areas entries.**
- *Ingest token and Companion authentication* will link ingest + ingest-token + record 0019. That covers every document that states the token's rules.
- *Server database* will link server-data, server-state, history + history-deletion, web-trends and 0097. That covers the deletion and clean-up rules.
- *Network exposure* covers the deletion's host rule. It needs no new link: web-pages.md and record 0085 state that rule, and history.md:210-211 (moving) only points there.
- Both guarantees, the token one and the history one, get the new links.

**AC3 strings.** I ran `grep -rnF '<s>' docs/areas` for each of the seven strings. Each occurs today in exactly one file: history.md:197, 199 and 185, then ingest.md:60, 232, 246 and 71. So the check works as planned.

Findings:

1. [NON-BLOCKING] The plan (*Approach*, ingest-token.md, *Accepted risks*) moves the second sentence of **Plain text** ("On Windows the token file takes its folder's access rules, so a share that exposes the data directory exposes the token.") into the bullet **The configured file's access rules**. That sentence is about the generated `ingest-token` in the data directory, not the file named by `Censio:Ingest:TokenFile`. Under that lead it would read as a rule for the configured file, whose access rules the server never touches (ingest.md:63, 269). The Windows-share exposure would then sit under the wrong file.
   - Smallest fix: keep the sentence as a bullet of its own in ingest-token.md *Accepted risks*. Use the lead-in "**The generated file on Windows.**" and add that lead-in to the list of reworded lead-ins under *Approach* so AC4 allows it.
   - Recommendation: fix now, in the Lead's revision.

2. [NON-BLOCKING] AC3 pins the moved log lines, start failures and mode, but not two of the security statements you asked to keep whole: the token-off risk, and the rule that a failed deletion stores no change. AC4's moved-lines diff covers them only by reading.
   - Smallest fix: add to AC3's string list `Use the switch only on a network where every host is trusted.` (today only in ingest.md:265), `A deletion counts as done once its rows are gone` (only in history.md:207) and `compared by its SHA-256` (only in ingest.md:77).
   - Recommendation: fix now; it is three strings.

No blocking findings. No security statement is lost or left contradictory. The only duplication is the log-privacy rule, which the two documents already state the same way. Both entries point to every document that states their rules, and the tier is right.
