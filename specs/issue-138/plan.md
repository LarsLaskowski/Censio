# Plan: Names with an apostrophe or umlaut arrive from the game with `_`

Source: Issue #138
Tier: trivial — the game's pipe already sends the `_`, so the fix is documentation, area documents included (not tier `docs`).
Security areas touched: none

## Problem / root cause

Censio writes no `_`. Island, route and folder names are pipe strings, decoded as UTF-8 by
`PipeMessageParser.TryReadString` (`src/Censio.Pipe/PipeMessageParser.cs:569-591`, an invalid sequence becomes
U+FFFD). After that they pass through unchanged, except that `LogLine.Sanitize` turns control characters into `?`
(facts 1–6). So the `_` is in the frame bytes. This matches the third-party capture (non-ASCII becomes `_`) and the
version 3 recording in `docs/protocol.md` (`_` in route names, no non-ASCII byte). The apostrophe may be the
typographic `’`.

The Product Manager's trade-route umlauts come from the same `TryReadString` (route, folder, station island names), so they are
in scope. The character cannot be restored; what is missing is documentation (today `_` is an *assumed* umlaut rule,
and the user guide is silent).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The `Name` cell is the island name through `LogLine.Sanitize` (issue) | `DiagnosticsView.cs:248` | confirmed |
| 2 | `Sanitize` turns only Cc, Cf, Zl, Zp and unpaired surrogates into `?` (issue) | `LogLine.cs:83-86`, `134-140` | confirmed |
| 3 | No code in `src/` writes `_` (issue) | grep `'_'`, `"_"`, `Replace(` in `src`: none writes `_` | confirmed |
| 4 | Invalid UTF-8 becomes U+FFFD, not `_` | `PipeMessageParser.cs:587` | confirmed |
| 5 | The window cell is a one-time bound `TextBlock` | `DiagnosticsWindow.xaml.cs:85-90`, `173-178` | confirmed |
| 6 | Names pass the converter, contract and page unchanged | `ContractMessageConverter.cs:107`, `139`; `ContractRules.cs:236-259` (size check only); `PageNames.cs:134-162` | confirmed |
| 7 | The fixtures cannot show it (issue): names become `Island <n>`, `Route <n>` | record 0042; `docs/USER_GUIDE.md`, *Anonymising a recording* | confirmed |
| 8 | The Product Manager's trade-route umlauts show as `_` | to verify: the orchestrator asks the Product Manager before step 9 (AC5) | unverified |

## Acceptance criteria

- AC1: `docs/protocol.md` has one bullet under *Framing* on characters in names (figure, island, route, folder). It
  says that an apostrophe arrives as `_`, *observed* (#138, inferred because Censio's reading writes no `_`), and keeps
  the umlaut rule as *assumed* (third-party capture, the Product Manager's trade-route report). *SessionStart*
  points to the bullet.
- AC2: `docs/areas/pipe.md`, *Residual risks*: the UTF-8 bullet names the apostrophe beside the umlauts, points to
  `docs/protocol.md`, and says that Censio cannot restore the character (`_` stands for several characters and for
  itself).
- AC3: `docs/areas/companion-diagnostics.md`, *Area names*: a `_` in a name comes from the game and is shown as sent.
- AC4: `docs/USER_GUIDE.md`, English and German, at the end of the diagnostics paragraph (EN line 285, DE line 1011)
  and of the trade-routes bullet of *Web interface* (EN line 625, DE line 1351): the game sends island, route and
  folder names with `_` in place of an apostrophe or a letter such as ä, ö, ü or ß, and Censio shows them as sent.
- AC5: the Product Manager confirms fact 8.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | none | none |
| AC2 | none | none |
| AC3 | none | none |
| AC4 | none | none |
| AC5 | none | none |

## Verification without tests

- AC1: check: the Reviewer reads the diff (step 8).
- AC2: check: the Reviewer reads the diff (step 8).
- AC3: check: the Reviewer reads the diff (step 8).
- AC4: check: the Reviewer reads both languages (step 8).
- AC5: manual: the Product Manager, on their own game, before step 9; the orchestrator logs the answer.

## Approach

Documentation only: steps 4, 5 and the *Coverage gate* do not apply. The user-guide sentence promises no fix.

The issue's display mapping (guessing the character back) is rejected: one `_` stands for several characters and for
itself, route names are the player's, and guessing breaks record 0033.

Early draft PR: no

## Affected projects and types

None: documentation only.

## Signatures (for the Dev's skeleton)

None: no API.

## Test files

None. Call sites to adapt: none. Files no unit test can cover: none.

## Areas

- `docs/areas/pipe.md (+30 words)`: AC2.
- `docs/areas/companion-diagnostics.md (+20 words)`: AC3.
- `docs/areas/web-pages.md (+0 words)`: not edited (at `AREA_MAX_WORDS`; lines 119-124 stay true).

## Documentation updates

Owner: the Dev.

- `docs/protocol.md`: AC1.
- `docs/areas/pipe.md`: AC2.
- `docs/areas/companion-diagnostics.md`: AC3.
- `docs/USER_GUIDE.md`: AC4, four places.
- Sweep: record 0033 (released) stays true; `docs/ARCHITECTURE.md` is unchanged. Label `documentation`.

## Architecture check

No guarantee is touched.

## Security considerations

- [x] No limits.
- [x] None: no exception path changes.

## Decision records

None: this documents the game's behavior.

## Out of scope / follow-ups

- If AC5 is answered no (for example `�` or `Ã¤` on the page), the orchestrator opens a separate issue,
  *[Pipe] Trade-route names with umlauts are shown garbled*. Body: the page shows `<symptom>` for an umlaut;
  `TryReadString` decodes UTF-8 (invalid bytes become U+FFFD); check the bytes in a raw recording kept outside the
  repository (record 0029). AC1 and AC4 then name the apostrophe only.
