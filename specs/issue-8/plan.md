# Plan: Decision records for the agreed project decisions

Source: Issue #8
Status: Draft
Tier: trivial — documentation that needs decision records (`docs/decisions/` excludes the `docs` tier); no code, no behavior or control flow changes.
Security areas touched: Authentication and Ingest token (`.squad/project.md`, *Security areas*: record 0019 fixes the design #28 will build — token enabled by default, switchable off); Network exposure (record 0006 extended: the web UI has no login). Records only; no security control changes in this diff.

## Problem / root cause

The planning decisions listed in issue #8 exist only in the issue text and the backlog issues (#9–#53). Only some of
them have a decision record (0004, 0005, 0006, 0010, 0012, 0013 touch them), so the squad has no record to check a
plan against for the rest.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | No record is released, so 0005 and 0006 may be edited in place | `git ls-remote --tags origin` prints nothing; `decision-check.py` PASS | confirmed |
| 2 | The licence is MIT | `LICENSE` line 1; README *Licence* | confirmed |
| 3 | All production projects target .NET 10; the server is Blazor Server | `src/*/*.csproj` `net10.0` / `net10.0-windows`; `src/Censio.Server/Program.cs:6,21` interactive server components | confirmed |
| 4 | The Companion is a WPF tray shell | record 0004; `src/Censio.Companion/App.xaml`, `TrayIcon.cs`; `docs/areas/companion.md` | confirmed |
| 5 | The image is linux/amd64 only, on Docker Hub; no compose file exists | `docs/areas/server.md:65,98`; Glob `**/*compose*` finds nothing | confirmed |
| 6 | The ZIP holds one self-contained single-file exe, not signed | `docs/areas/companion.md` *Platform*; README *Companion* | confirmed |
| 7 | Pre-release then promote | record 0012; `docs/CONTRIBUTING.md` *Versioning and releases* | confirmed |
| 8 | First version 0.1.0 | README *Status*; milestone M1 "(v0.1.0)" | confirmed |
| 9 | Issue claim "edge builds" | #6 closed `not_planned`; README "Edge builds: none are offered"; records 0005, 0013; recorded as "no edge builds" | refuted |
| 10 | Documentation language rules | `AGENTS.md:23-24,72` | confirmed |
| 11 | No web login | record 0006 *Consequences*; `SECURITY.md` deployment note; `docs/areas/server.md:40` | confirmed |
| 12 | Token is optional, enabled by default | #28 *Scope* ("can be switched off by configuration"); contradicts `.squad/project.md:16` "accepts a Companion only with a valid token" and `docs/ARCHITECTURE.md` *Data flow*, left to #28 (Options 2) | confirmed |
| 13 | The other decisions (local mode, one Companion, profiles, SQLite and retention, time axis, game data, RDA, MudBlazor/ApexCharts, fixtures, tick buffer, Telegram/mobile later) are not built yet | `src/` holds no ingest, persistence, pipe or game-data code; issue bodies #15, #19, #21, #25, #28, #31, #35, #37, #39, #41, #52, #53 | confirmed |
| 14 | Package licences of MudBlazor and Blazor-ApexCharts permit use (not stated in any record) | to verify: licence check when the package is added, Dev of #31 / #36 | unverified |

## Acceptance criteria

- [ ] AC1: Each of the 21 decisions of issue #8 maps to exactly one record of the table in *Approach*, and each
  record has its row in the index of `docs/decisions/README.md`.
- [ ] AC2: Every new record (0014–0032) follows `docs/decisions/_template.md`: status `Proposed`, date, an `Area:`
  that is a listed area or `—`, a *Source* naming issue #8, *Supersedes* `—`; *Context*, *Options considered*,
  *Decision* (one to three sentences), *Consequences*; no *Area assignment* section.
- [ ] AC3: Records about behavior that holds today agree with it: 0026 with `docs/areas/server.md` (*Docker image*),
  0027 with `docs/areas/companion.md` (*Platform*), 0028 with `LICENSE`, 0030 with `AGENTS.md`, 0005 with README
  *Status* and *Getting Censio*, 0006 with `SECURITY.md` and `docs/areas/server.md`.
- [ ] AC4: Every record about planned behavior names its implementing issue and states no rule that contradicts that
  issue's body.
- [ ] AC5: `docs/areas/server.md` and `docs/areas/companion.md` list 0026 and 0027 under *Related decisions*, the
  server's 0006 line names the UI without login, and `docs/ARCHITECTURE.md` *Deployment* links 0026 and 0027.
- [ ] AC6: `decision-check.py`, `config-check.py`, the *Format check* and `scope-check.py` pass.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1–AC6 | none (no production or test code), see *Verification without tests* | — |

## Verification without tests

No production or test code: steps 4, 5 and the *Coverage gate* are not applicable.

- AC1: Reviewer, read-only: the table in *Approach* against the issue's scope list and the index; `decision-check.py`
  (step 7) checks every file has a row.
- AC2: Reviewer, read-only, per record; `decision-check.py` checks the `Area:` values.
- AC3: Reviewer, read-only, against the named files.
- AC4: Reviewer, read-only, against the issue bodies (`gh api repos/LarsLaskowski/Censio/issues/<n>`).
- AC5: Reviewer, read-only diff of the three documents.
- AC6: orchestrator, step 7 (`config-check.py`, *Format check*) and `scope-check.py` before the PR.

## Approach

Records written by the Lead in this step (status `Proposed`; the orchestrator sets `Accepted` at step 9). Where an
unreleased record on the same topic exists it is extended in place instead (`docs/decisions/README.md`, *Rules*):

| # | Decision of issue #8 | Record | Area |
| - | -------------------- | ------ | ---- |
| 1 | Product name Censio | 0014 new | — |
| 2 | Two deliverables, server embedded in the Companion | 0015 new | — |
| 3 | C#/.NET 10/Blazor Server only | 0016 new | — |
| 4 | WPF tray Companion, optional autostart, automatic connect | 0017 new (build split stays 0004) | — |
| 5 | Pipe parsing in the Companion, versioned JSON contract | 0018 new | — |
| 6 | WebSocket with shared token, optional, on by default | 0019 new | — |
| 7 | One Companion per server, savegame profiles | 0020 new | — |
| 8 | SQLite, 7 days full then hourly averages, delete per profile | 0021 new | — |
| 9 | Chart axis real time / game time | 0022 new | — |
| 10 | Names and icons extracted locally, cached, never distributed, DE/EN | 0023 new | — |
| 11 | RDA via existing NuGet library if possible | 0024 new (#21 adds its result) | — |
| 12 | MudBlazor, Blazor-ApexCharts, light theme default | 0025 new | — |
| 13 | No web login | 0006 extended (title, option, decision sentence, consequence) | server |
| 14 | Docker linux/amd64 on Docker Hub, no compose | 0026 new | server |
| 15 | Windows ZIP self-contained single-file, unsigned | 0027 new | companion |
| 16 | Pre-release then promote, first version 0.1.0, edge builds | 0012 (unchanged) + 0005 extended: first release `v0.1.0`, no edge builds | — |
| 17 | MIT licence | 0028 new | — |
| 18 | Anonymised fixtures only | 0029 new | — |
| 19 | Documentation language rules | 0030 new | — |
| 20 | Bounded local tick buffer, gaps accepted | 0031 new | — |
| 21 | Telegram alerts and mobile layout later | 0032 new | — |

So 19 new records, 0014–0032, and two extended (0005, 0006); the index has the 19 rows and the new titles of 0005 and
0006. `Area:` names `server` or `companion` only where that area's scope already covers the behavior; for planned
behavior the existing areas list it under *Not here*, so the record carries `—` and the implementing issue changes
the field in place when it introduces the area (while the record is unreleased). No user-visible output.

## Options considered

1. **One new record per item (21)** against **extend 0005 and 0006, which cover the same topic** — extension chosen:
   the index rules require it, and a second record on "no login" or "version line" would split one decision.
2. **Align `.squad/project.md`, `docs/ARCHITECTURE.md` and `SECURITY.md` with the optional token now** against
   **leave them to #28** — #28 chosen: the switch does not exist yet, those files describe what holds today, and
   narrowing a security area's wording ahead of the code would make this a `security` change; record 0019 names the
   follow-up in its *Consequences*, so the gap is not silent.
3. **Create area documents (ingest, history, UI, game data) now** — rejected: they would describe nothing that holds
   today (`docs/areas/README.md`, *Size*).
4. **Area `server`/`companion` for planned records** — rejected where the area's *Not here* excludes the topic.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| — | `docs/decisions/0014`…`0032`, `0005`, `0006`, `README.md` | records and index (Lead, done) |
| — | `docs/areas/server.md`, `docs/areas/companion.md`, `docs/ARCHITECTURE.md` | links (Dev) |

## Signatures (for the Dev's skeleton)

none

## Test files

None. Existing test call sites affected: none. Files no unit test can cover: none (no code).

## Areas

- `server`: *Related decisions* gains 0026; the 0006 line reads "plain HTTP, no web login, open `/health`, host
  filtering off". No behavior change.
- `companion`: *Related decisions* gains 0027. No behavior change.

## Documentation updates

Owner: Dev.

- `docs/areas/server.md` *Related decisions*: add the 0026 line ("linux/amd64 only, on Docker Hub, no compose
  file"); reword the 0006 line as in *Areas*.
- `docs/areas/companion.md` *Related decisions*: add the 0027 line ("self-contained single-file exe in a ZIP, not
  signed").
- `docs/ARCHITECTURE.md` *Deployment* (inside the project block): link 0026 after "linux/amd64" and 0027 after
  "published as a Windows ZIP".

Sweep: the 0006 title appears nowhere outside the index; `.squad/project.md:19` and `docs/ARCHITECTURE.md:56-58` cite
0006 by number and stay true; no enumeration of records exists outside the index.

## Architecture check

No guarantee in `.squad/project.md` changes (none recorded). Record 0019 states the token can be switched off,
which the current wording of `.squad/project.md` and `docs/ARCHITECTURE.md` does not yet say; #28 aligns them when it
builds the control (Options 2).

## Security considerations

- [x] no limits.
- [x] none.

## Decision records

- `docs/decisions/0014-product-name-censio.md` … `docs/decisions/0032-telegram-alerts-and-mobile-layout-later.md`
  (19 records, Proposed; file names in the index)
- `docs/decisions/0005-product-version-from-one-build-property.md`, `docs/decisions/0006-server-plain-http-and-open-health.md` (extended in place, stay Accepted)

## Out of scope / follow-ups

- The milestone M0 description on GitHub still says "release workflow with edge builds … Test builds after every
  merge", contradicting #6's closure: the orchestrator may correct the milestone text (not a repository file).
- #28 updates `.squad/project.md` (*Security areas*: Authentication), `docs/ARCHITECTURE.md` (*Data flow*,
  *Security model*) and `SECURITY.md` for the token switch (named in 0019).
- #21 records its result in 0024; #19 records profile detection (extending 0020 or its own record).

## Security notes

Security plan review: APPROVED, no blocking findings. Non-blocking, to be applied in step 6:

1. 0006 understates what an unauthenticated visitor can do. Suggested wording: "Anyone who reaches the server can read every page and use every action the UI offers." and extend the rebinding bullet so that "the first change that puts game data or a state-changing action on a page revisits this choice."
2. 0019's *Decision* does not say the token stays on when none is configured. Suggested addition: "only an explicit setting switches it off; a missing or empty token never does."
