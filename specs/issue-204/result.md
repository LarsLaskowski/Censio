# Result: issue #204

## Tier

`docs`. The server behaves as designed, documented and tested: the replayed recording ends with a tick in which every
island has no entries (the tick the game sends after a load), that tick is the latest one, and the documented `–`
applies. No code changes; the gap is that the user guide never says that the pages show the latest tick only and
when that turns every figure into `–`, which the issue asks for ("the documentation should say which tick counts as
the latest"). The area documents already say it, so the diff touches `docs/USER_GUIDE.md` alone (product
documentation outside `docs/areas/` and `docs/decisions/`). Security areas touched: none.

## Root cause

The reproduced recording is `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl` (or the original it was made from).
Decoded tick by tick (frames grouped by game time): the last tick holds all 24 islands (session GUIDs 3245, 6627,
149679) with 0 entries each; its last frame is received at `2026-06-11T00:21:36.55Z`, which is 02:21 in a browser on
CEST, the as-of time of the issue; session GUID 3245 has 13 islands, `Island 1` to `Island 13`. It comes after a
session end and a new session start (a load). Every earlier tick with data holds the same 24 islands with entries.

- `src/Censio.Server.Data/TickReader.cs` line 22 (`LatestSql`) and `docs/areas/server-data.md`, *Latest tick*: the
  latest tick is the one with the latest receive time, then the one stored last, so the empty tick is the latest.
- `src/Censio.Server/ProfileViewBuilder.cs` lines 59 and 118-121: an island whose tick entry has no entries gets
  `Figures = null`, shown as `–` by `RegionIslands.razor`; `CreateRegion` (line 97, 104) gives the region `null` too.
- This is asserted today by `PageReaderFixtureFinalEmptyTickKeepsIslandsWithoutValues` (`PageReaderTests`, line 332)
  and the replay test in `IngestEndpointTests` (line 320), and stated in `docs/areas/web-pages.md`, *Key figures*,
  and `docs/areas/server-state.md`, *What a profile's state holds* ("A tick without entries (no data yet) … becomes
  the state like any other").

## Claims of the issue

| Claim | Status | Evidence |
| ----- | ------ | -------- |
| Every figure column shows `–` for a region of 13 islands, as of 2026-06-11 02:21, on `main` | confirmed, as designed | decoded fixture (above); tests named above |
| The Companion's `Open diagnostics` shows the values of the same replay correctly | refuted for the end of the replay | `PipeReplayRunner.PublishTick` (lines 443-449) keeps every closed tick as `LatestTick`, the empty one too; `DiagnosticsView.Create` (line 179) then shows `Game time: … (no data yet)` and rows `(no entries)`. The values show only while the replay runs, before its last tick |
| Cause 1: the last tick lacks the islands' values | confirmed (the islands are present, with no entries) | decoded fixture |
| Cause 2: island keys disagree between stored state and tick | refuted | `ProfileViewBuilder.cs` lines 28-39 build both keys from session GUID, island ID and area index; `TickWriter` and `ServerStore.ReadIslandsAsync` (line 344) store and read the same three columns |
| Cause 3: figures lost between ingest and view | refuted | `TickWriter`/`TickReader` round-trip entries; the fixture tests above show values for every tick with data |
| Figures were visible in an earlier run | consistent, unconfirmed | a replay ended with `Exit` before the file's last tick leaves a tick with data as the latest |

No related defect found.

## Options considered

- `NO CHANGE` (works as designed): lost, because the issue asks the documentation to say which tick counts, and the
  user guide, the reader's documentation, says nothing about it; the reporter's confusion shows the gap.
- Behavior change (keep the last tick with data, or skip a tick without data as latest): rejected here; it
  contradicts `docs/areas/web-pages.md`, *Key figures* ("no older tick is used") and `docs/areas/server-state.md`,
  and would be a feature for the Product Manager, not a bug fix. Proposed follow-up only if the Product Manager wants
  it (below).
- Area document edits: not needed; `server-data.md`, `server-state.md` and `web-pages.md` already state it.

## Edits (owner: orchestrator, tier `docs`)

File `docs/USER_GUIDE.md`, two insertions, nothing else.

1. English, section *Web interface*: insert a new line directly after the bullet that begins `- Pages: the navigation
   lists` (line 440) and before `- Profile switch:`:

```markdown
- Latest data only: every figure and goods table comes from the latest tick of the profile, the one received last (its time is the one `As of` shows); no older tick is used. An island the latest tick lacks or has no values for shows `–` in every figure column of its island list (a region none of whose islands has values shows `–` in the region overview too), and its goods table is replaced by `No values for this island in the latest tick.`; nothing is shown as zero. After a savegame is loaded the game first sends a tick without values, so a recording that ends after a load, replayed with `--server`, ends with every island at `–`; the window `Open diagnostics` then keeps the same last tick, with `(no data yet)` after the game time. The figures return with the next tick that has values.
```

2. German, section *Weboberfläche*: insert a new line directly after the bullet that begins `- Seiten: Die
   Navigation listet` (line 902) and before `- Profilschalter:`:

```markdown
- Nur die neuesten Daten: Alle Kennzahlen und Warentabellen stammen aus dem letzten Tick des Profils, dem zuletzt empfangenen (seine Zeit zeigt `Stand:`); ein älterer Tick wird nicht verwendet. Eine Insel, die der letzte Tick nicht enthält oder für die er keine Werte hat, zeigt `–` in jeder Kennzahlspalte ihrer Inselliste (eine Region, deren Inseln alle keine Werte haben, zeigt `–` auch in der Regionsübersicht), und statt ihrer Warentabelle erscheint `Keine Werte für diese Insel im letzten Tick.`; nichts wird als Null gezeigt. Nach dem Laden eines Spielstands sendet das Spiel zuerst einen Tick ohne Werte, daher endet eine Aufzeichnung, die nach einem Laden endet, beim Abspielen mit `--server` mit `–` bei jeder Insel; das Fenster `Open diagnostics` behält dann denselben letzten Tick, mit `(no data yet)` hinter der Spielzeit. Die Kennzahlen kehren mit dem nächsten Tick mit Werten zurück.
```

The quoted UI texts are the existing ones: `UiTexts.cs` lines 39 and 79 (`NoIslandValues`),
`DiagnosticsView.cs` line 179 (`(no data yet)`), the `As of` / `Stand:` line of the same section.

## Acceptance criteria

- AC1: `docs/USER_GUIDE.md`, *Web interface*, has the English bullet of edit 1 between the `Pages:` and
  `Profile switch:` bullets. Check: the Reviewer reads the diff against this file.
- AC2: *Weboberfläche* has the German bullet of edit 2 between the `Seiten:` and `Profilschalter:` bullets, with
  the same content as AC1. Check: the Reviewer reads the diff against this file.
- AC3: the diff outside `specs/` changes `docs/USER_GUIDE.md` only. Check: `scope-check.py --tier docs` and
  *Format check* pass (orchestrator).

## Proposed follow-up (optional, for the Product Manager)

Title: `[UI] Show the last values of an island after a tick without data`. Body: after a load the game sends a tick
in which every island has no entries; it becomes the latest tick and every figure shows `–` until the next tick
with data (about two minutes live, forever at the end of a recording, #204). Decide whether the pages should keep
showing the last tick with data, marked as older, which changes `docs/areas/web-pages.md`, *Key figures* ("no
older tick is used") and needs a decision record.

RESULT: DONE
