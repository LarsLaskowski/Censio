# Plan: Pages re-read trends and rebuild the chart on every stored icon

Source: Issue #247
Tier: standard — a behavior change of the pages' live updates and of the server state's notices; no security control is created, changed or removed.
Security areas touched: Server database — the in-memory state (`ServerState`: `StoreIconAsync` tells a new notice kind)

## Problem / root cause

`ServerState.StoreIconAsync` (`src/Censio.Server/ServerState.cs:503-536`) swaps the catalog through `SwapCatalog`,
which tells `ServerStateChangeKind.Catalog` without a profile (`ServerState.cs:813-822`), the same notice as a stored
catalog. `DataPageBase.ReadAfterNoticeAsync` (`src/Censio.Server/Components/Pages/DataPageBase.cs:372-388`) treats every
`Catalog` notice as concerning the page and runs `LoadAsync`, whose `ReadOnceAsync` (`DataPageBase.cs:241-290`) always
calls `ReadDetailsAsync` once the view is ready. `IslandDetail.ReadDetailsAsync` then reads the overview chart
(`Trends.ReadFiguresAsync`) and raises `_overviewNumber` (`IslandDetail.razor.cs`, *DataPageBase* region);
`GoodTrend.ReadDetailsAsync` reads both charts and raises `_islandNumber` / `_regionNumber`. The number is the chart's
`ReadNumber`, the `@key` of its `censio-chart` element (`TrendChart.razor:23`), so each icon re-creates the element and
runs the reveal animation (`docs/areas/web-charts.md`, *Animation*). One icon per missing reference (`docs/areas/live-sending.md`,
*Catalog*) gives hundreds of rebuilds per open chart.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A stored icon tells a `Catalog` notice, the same kind as a stored catalog | `ServerState.cs:519-528` → `SwapCatalog` → `Notify(ServerStateChangeKind.Catalog, null)` (`:821`); `StoreCatalogAsync` uses the same `SwapCatalog` (`:422`, `:438`) | confirmed |
| 2 | A Companion sends one `iconImage` per missing icon | `docs/areas/live-sending.md` lines 143-146 | confirmed |
| 3 | Every data page answers a `Catalog` notice with a full `LoadAsync` | `DataPageBase.cs:379-386` (`Kind == Catalog` or `ProfileId is null`) | confirmed |
| 4 | Issue: the load "re-reads the profile" from the database for a held profile | `PageReader.ReadAsync` (`PageReader.cs:73-76`) builds from the held state without a database read; only a profile that is not held is read from the store (`:78-93`) | refuted |
| 5 | Issue: the load re-reads the sparklines | `SparklineLoader<TKey>.Request` returns without a read when the address is the same and `asOf` equals the held, in-flight or pending one (`src/Censio.Server/SparklineLoader{TKey}.cs`, `Request`); an icon does not move the latest tick | refuted |
| 6 | The load re-reads the trend figures of the charts | `IslandDetail.ReadDetailsAsync` calls `ReadFiguresAsync` whenever the overview has not failed; `GoodTrend.ReadDetailsAsync` calls `ReadGoodAsync` twice | confirmed |
| 7 | Each chart read gives the chart a new key | `_overviewNumber++`, `++_readCount`; `TrendChart.razor:23` `@key="drawn.ReadNumber"`; `TrendChart.OnAfterRenderAsync` builds a new `DrawnTrend` with the new number for every new `Read` reference | confirmed |
| 8 | A stored icon changes no figure, name or region of a view: only `GoodRow.IconReference` | `StoreIconAsync` keeps `Lookup` (`ServerState.cs:521-526`); `ProfileViewBuilder.CreateRow` uses `missingIcons` only for `IconReference` (`ProfileViewBuilder.cs:204-212`) | confirmed |
| 9 | A re-render with the same `DrawnTrend` keeps the chart element and does not rerun the reveal | `web-charts.md`, *Animation*: "a theme or a language switch keeps the element and so does not run it" — those re-render `MudChart` with the same key | confirmed |
| 10 | Only `DataPageBase`, `MainLayout` (`Profile` only) and `DataStatus` (`Companion` only) follow the state in production | `grep "Changed +="` over `src/` | confirmed |
| 11 | Tests with a fake source still get their notices from the real `ServerState` | `docs/UNIT_TESTS.md` line 71; `RegionsTests.RegionsTickDuringReadReadsOnceMore` | confirmed |
| 12 | `docs/areas/web-pages.md` has 4,991 words of `AREA_MAX_WORDS` 5,000 | `decision-check.py`'s count (`read_text(path).split()`) | confirmed |

## Acceptance criteria

- AC1: A stored icon tells the followers the notice kind `Icons` without a profile, with or without a usable catalog;
  a stored catalog still tells `Catalog`.
- AC2: On an open island page, a stored icon shows the icon in its row without a reload, reads neither the overview
  chart nor the row trends, and keeps the overview chart's `ReadNumber` and its `MudChart` component instance.
- AC3: On an open good page, a stored icon reads neither chart and keeps both charts' `ReadNumber` and `MudChart`
  instances.
- AC4: A catalog stored while the read of an `Icons` notice runs still reads the charts and the row
  trends once that read ends (the path: `LoadAsync`'s pending loop with the details flag ORed, *Approach*).
- AC5: An `Icons` notice whose read shows a view with another latest tick (`ProfileView.AsOf`) than the one shown
  before, or after no view was shown, reads the charts and row trends as any notice does.
- AC6: A stored catalog on an open island page still reads the overview chart again (the full load stays for
  `Catalog`).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `ServerStateStoreIconAsyncTellsFollowersIconsChanged`, `ServerStateStoreIconAsyncWithoutCatalogTellsFollowersIconsChanged` (the two existing `…CatalogChanged` tests renamed and adapted); the existing `Catalog` assertion of the catalog store (`ServerStateTests.cs:584`) stays | `tests/Censio.Server.Tests/ServerStateTests.cs` |
| AC2 | `IslandDetailStoredIconReadsNoTrendsAndKeepsChart` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC3 | `GoodTrendStoredIconReadsNoTrendsAndKeepsCharts` | `tests/Censio.Server.Tests/GoodTrendTests.cs` |
| AC4 | `IslandDetailCatalogDuringIconReadReadsTrends` (passes on today's code, which reads on every notice: deferred to step 6) | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC5 | `IslandDetailIconNoticeWithNewTickReadsTrends` (passes on today's code: deferred to step 6) | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC6 | `IslandDetailStoredCatalogReadsOverviewAgain` (passes on today's code: deferred to step 6) | `tests/Censio.Server.Tests/IslandDetailTests.cs` |

Cases for the Tester: AC2/AC3 wait until the icon (AC2) or a re-render (AC3) is shown, then assert
`FakeHistoryTrends.FiguresQueries` / `GoodQueries` and `RowTrendCallCount` unchanged, `FindComponent<TrendChart>()`
`ReadNumber` unchanged and `FindComponent<MudChart<double>>().Instance` reference-equal to the one before. AC4 holds the
fake source's read of a profile that is not held (`FakeProfileStateSource(true)`), stores the icon, then a catalog on
`host.State`, releases, and waits for a second figures read. AC5 lets the fake source answer the `Icons` notice's read
with a state of a later tick while only the icon is stored on `host.State`.

## Approach

1. `ServerStateChangeKind` gets the member `Icons`. `StoreIconAsync` swaps the catalog as today but tells `Icons`:
   `SwapCatalog` takes the kind to tell (`StoreCatalogAsync` passes `Catalog`), so the swap stays one helper.
2. `DataPageBase` gets a private field `_isDetailsDue`. `LoadAsync` takes `bool readsDetails` and ORs it into the field
   before anything else (also when a read runs and it only sets `_isPending`). Each pass of its loop takes the field
   (`var details = _isDetailsDue; _isDetailsDue = false;`) together with clearing `_isPending` and passes it to
   `ReadOnceAsync(bool readsDetails)`.
3. `ReadOnceAsync` remembers the view shown before the read (`Mode == PageMode.Ready ? View : null`, after today's
   null-out of a view of another profile or island). It calls `ReadDetailsAsync` as today only when `readsDetails` is
   true or that earlier view's `AsOf` differs from the shown view's (none shown counts as different).
4. `OnParametersSetAsync` calls `LoadAsync(true)`. `ReadAfterNoticeAsync` keeps its `concerns` rule (an `Icons` notice
   has no profile, so it concerns every page) and calls `LoadAsync(arguments.Kind != ServerStateChangeKind.Icons)`.
   `OnStateChanged` is unchanged: `Companion` and the foreign `TradeRoutes` notices still read nothing.
5. Nothing changes in `TrendChart`, `IslandDetail`, `GoodTrend` or `SparklineLoader`: with no `ReadDetailsAsync` call
   the page passes the same `Read` and `ReadNumber`, so the chart keeps its `DrawnTrend` and its element (fact 9).

No new user-visible output. No new resource bound: a view-only read takes the same `PageReader` gate as today.

Early draft PR: no

## Options considered

- **Server merges icon notices** (issue option 2): each merged notice still reads the charts and reruns the reveal;
  adds a timer and an interval to `ServerState`, and the final notice needs to know when the upload ended. Lost.
- **Chart keeps its key when its figures did not change** (issue option 1, second half): still one trend read per
  icon and open chart under the trend gate, plus an equality over every point. Lost.
- **Read charts only when the latest tick moved**: would also stop the whole re-read after a late tick of the page's
  profile, which `web-charts.md` (*Live updates*) shows at once on the ranges read whole. Lost; kept only as the AC5
  guard on `Icons` notices.
- **Keep the `Catalog` kind and detect icons by an unchanged `Lookup` reference in the page**: rests on reference
  identity of a field the state happens to keep; an explicit kind is what record 0097 did for trade routes. Lost.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `ServerStateChangeKind.cs` | member `Icons` |
| Censio.Server | `ServerState.cs` | `SwapCatalog` takes the kind; `StoreIconAsync` tells `Icons` |
| Censio.Server | `Components/Pages/DataPageBase.cs` | details flag through `LoadAsync` / `ReadOnceAsync` |

## Signatures (for the Dev's skeleton)

- `ServerStateChangeKind.Icons` — new enum member, XML doc "The icons of the catalog changed."; appended after
  `TradeRoutes`.

Private, described for the Dev and not skeleton API: `ServerState.SwapCatalog(ServerStateSnapshot snapshot,
CatalogHold catalog, ServerStateChangeKind kind)`, `DataPageBase.LoadAsync(bool readsDetails)`,
`DataPageBase.ReadOnceAsync(bool readsDetails)`, field `bool _isDetailsDue`. Each stays within seven parameters; no
name collides with a keyword (CA1716 applies to public members only). The skeleton holds the enum member for real, so
AC1's tests compile and fail; AC2 and AC3 compile against today's code and fail, AC4–AC6 are deferred.

## Test files

`tests/Censio.Server.Tests/ServerStateTests.cs`, `tests/Censio.Server.Tests/IslandDetailTests.cs`,
`tests/Censio.Server.Tests/GoodTrendTests.cs`.

Existing test code that calls a changed signature: none (only private signatures change). Existing tests whose input
the change handles differently: `ServerStateStoreIconAsyncTellsFollowersCatalogChanged` (`ServerStateTests.cs:643`)
and `ServerStateStoreIconAsyncWithoutCatalogTellsFollowersCatalogChanged` (`:1248`) expect `Catalog` for an icon —
the Tester renames and adapts both in step 5 (AC1). `IslandDetailStoredIconAppearsLive` stays unchanged and must stay
green. The `Catalog`-notice tests of `RegionsTests` and `RegionIslandsTests` store a catalog, not an icon: unchanged.

Files of the change no unit test can cover: none.

## Areas

- `docs/areas/web-pages.md` (+5 words) — *Live updates*, first bullet: a stored icon rebuilds the view and reads no
  trends, linking `web-charts.md`. The document has 4,991 words: the Dev keeps the net growth at most 5 words by
  shortening within the bullet (e.g. drop "(an older message replaces nothing, so nothing is told)", which
  `server-state.md` states), so no split is due.
- `docs/areas/server-state.md` (+30 words) — *Followers and notices*: a stored icon tells the kind `Icons` without a
  profile, a stored catalog `Catalog`; the sentence "told as a third kind of notice" names the kinds correctly.
- `docs/areas/web-charts.md` (+40 words) — *Live updates, theme and language*: a stored icon reads no chart and keeps
  the chart element (no reveal), unless the page's latest tick moved; *Animation*'s "whenever the points change" stays
  true.
- `docs/areas/web-trends.md` (+10 words) — *Sparklines and trend arrows*, *Live updates*: a stored icon reads no
  trends, beside the tick of another profile and the range and axis choices.

## Documentation updates

The four area documents above, by the Dev after the code is green, and nothing else: no setting, route, text or
guarantee changes, so `README.md`, `docs/USER_GUIDE.md`, `docs/ARCHITECTURE.md` and `.squad/project.md` stay.

## Architecture check

No guarantee of `.squad/project.md` is touched. `docs/ARCHITECTURE.md` names no notice kinds. The pages still keep
nothing but the view (`DataPageBase` summary); the ingest's acknowledgement does not wait for followers.

## Security considerations

- [ ] No limits: no parsed input changes.
- [ ] No exception reaches a user-visible text: the change adds no throw and no message.

## Decision records

- `docs/decisions/0109-a-stored-icon-tells-a-notice-of-its-own-that-reads-no-trends.md` (Proposed)

## Out of scope / follow-ups

- A page whose profile is not held still reads its state from the database once per merged `Icons` notice; bounded by
  the `PageReader` gate and the pending-read merge, recorded in record 0109's *Consequences*.

## Challenge notes

Devil's Advocate, plan mode, issue #247 (`/home/user/Censio/specs/issue-247/plan.md`, record `/home/user/Censio/docs/decisions/0109-a-stored-icon-tells-a-notice-of-its-own-that-reads-no-trends.md`)

The approach holds up. I found no major objection.

**What I checked in the code:**
- `ServerState.StoreIconAsync` and `SwapCatalog` (`src/Censio.Server/ServerState.cs:503-536, 813-822`) tell the same `Catalog` notice as a stored catalog.
- The `concerns` rule and the call to `LoadAsync` are at `DataPageBase.cs:379-386`.
- `OnParametersSetAsync` (`DataPageBase.cs:403-418`) is the only other caller of `LoadAsync`. A range change goes through it, because `AddressKey` includes `ChartChoice.Range` (`IslandDetail.razor.cs:215`, `GoodTrend.razor.cs:140`). So it gets `LoadAsync(true)` and keeps its re-read.
- `TrendChart` (`TrendChart.razor:23`, `TrendChart.razor.cs:409-466`) builds a new `DrawnTrend` only when it gets a new `Read` reference, the `Texts` change or the `Axis` changes. A view-only re-render passes the same `_overviewRead`, `_islandRead` and `_regionRead`, so fact 9 holds.
- Only three pages override `ReadDetailsAsync`: IslandDetail, GoodTrend and RegionIslands. RegionIslands only asks for sparklines, which `SparklineLoader` already skips for the same `asOf`, so skipping its details loses nothing.
- Apart from the two tests the plan names, no follower in `src/` or `tests/` relies on an icon telling `Catalog`.

**Minor objections (each written as an instruction):**

1. **AC4's test does not check the ORed flag.** As planned, `IslandDetailTickDuringIconReadReadsTrends` stores a tick during the `Icons` read. That tick moves `ProfileView.AsOf`, so step 3's AsOf rule (the AC5 path) already starts the second figures read. A build that drops or overwrites `_isDetailsDue` while a read runs would still pass.
   - Instruction for the Tester: in AC4, while the `Icons` read is held by `FakeProfileStateSource(true)`, store a **catalog** on `host.State` (a `Catalog` notice with the same latest tick), not a tick.
   - Release the read with a state of the same `AsOf`, then wait for a second figures read.
   - A tick test may be kept as well, but it does not stand in for this one.

2. **AC3's wait point is undefined, so the test can pass on today's code without checking anything.** `GoodTrend.razor` shows no row icon (no `IconReference` in `GoodTrend.razor*`), so "wait until a re-render is shown" has nothing visible to wait for. Also, on both pages `Show()` renders before `ReadDetailsAsync` starts (`DataPageBase.cs:278-283`). An assertion made right after the render could therefore run before today's code has recorded its chart read.
   - Instruction for the Tester: wait until the page's render count has grown after `StoreIconAsync` (AC3) or until the icon appears (AC2).
   - Then compare the query counts and `ReadNumber` with the values from before.
   - In the red run, confirm that AC2 and AC3 fail on the query count or `ReadNumber` assertion, not on a timeout and not intermittently.

3. **The early return in `ReadOnceAsync` loses the pass's details flag.** When the target changes during the read, the pass returns early (`DataPageBase.cs:272-277`) and the `readsDetails` it took is gone. Today the next pass rescues this: it nulls out a view of another profile or island, so "none shown" counts as different, or an address change brings its own `LoadAsync(true)`. That rescue depends on two separate rules.
   - Instruction for the Dev: at that early return, set `_isDetailsDue |= readsDetails` together with `_isPending = true`, so a pass that was due to read its details hands that on to the next one.

## Amendments

| Criterion | Change | Reason | Step | Decided by |
| --------- | ------ | ------ | ---- | ---------- |
| AC4 | "A tick of the page's profile" became "A catalog"; test renamed `IslandDetailCatalogDuringIconReadReadsTrends` | Challenge note 1: a tick moves `ProfileView.AsOf` and passes through the AC5 path, so only a catalog checks the ORed details flag; the criterion's wording was not brought in line when the note was folded in | 8 | Lead |
