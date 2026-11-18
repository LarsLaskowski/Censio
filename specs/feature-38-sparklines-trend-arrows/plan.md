# Plan: Sparklines and trend arrows in tables

Source: Issue #38 | [spec.md](spec.md)
Tier: security — two new history reads hold the trend gate and, on the in-memory database, the ingest, so their part bound is a resource control of the server database, and the pages show stored history beside external names.
Security areas touched: Server database (`HistoryStore`, `IHistoryTrends`: two new trend reads under the trend gate); Logging and showing external data (`TrendReader`, the pages `IslandDetail` and `RegionIslands`, the new `TrendCell` markup)

## Problem / root cause

Feature: [spec.md](spec.md) (approved by the Lead, the window and values by the Product Manager).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The goods table and the island list have no trend column today | `src/Censio.Server/Components/Pages/IslandDetail.razor` (columns icon, Good … Productivity), `RegionIslands.razor:24-30` | confirmed |
| 2 | Each chart read serves one good (over a set of islands) or one island | `TrendReader.cs:56` `ReadGoodAsync`, `:87` `ReadFiguresAsync`; `IHistoryTrends.cs` has only these two | confirmed |
| 3 | A trend read is one times transaction, then parts of `TrendPartSize` (240) points, each one transaction under `TrendGate` | `HistoryStore.cs:1473-1499` `ReadTrendAsync`, `:1439` `ReadTrendTransactionAsync`, `:17` | confirmed |
| 4 | The figures read scans every entry of one island per tick; no island total is stored | `HistoryStore.cs:231` `TickFiguresFromSql`; `TickIslands` columns in `Migrations/CensioDbContextModelSnapshot.cs:370-401` (no building column) | confirmed |
| 5 | The goods table shows the first 500 entries of the island in the latest tick, a repeated good twice | `ProfileViewBuilder.cs:77` (`entries.Take(ProfileView.MaxRows)`), `ProfileView.cs:13` | confirmed |
| 6 | The good read takes a good's first entry by position; the fold keeps one hourly entry per good | `HistoryStore.cs:215` (`ORDER BY x."Position" LIMIT 1`); `history.md`, *The series read* | confirmed |
| 7 | An hourly buildings figure is `Math.Round(Σ BuildingCountSum / EntryCount, AwayFromZero)`, a tick's the sum of `BuildingCount` | `HistoryStore.cs:939-960` `ReadHourlyFiguresAsync`, `:912-936`, `:893` | confirmed |
| 8 | A range choice changes the island page's `AddressKey`, which cancels the details' read and calls `ClearDetails` and `ReadDetailsAsync` again | `IslandDetail.razor.cs` `AddressKey` (includes `ChartChoice.Range`); `DataPageBase.OnParametersSetAsync` | confirmed |
| 9 | Details are read only when the page is interactive, so a prerender reads none | `DataPageBase.ReadOnceAsync` (`RendererInfo.IsInteractive`) | confirmed |
| 10 | `ReadDetailsAsync` of the island page returns at once after a failed overview read | `IslandDetail.razor.cs:183` (`if (_isOverviewFailed …) return;`) | confirmed |
| 11 | The gap rule is `later.Time - EndOf(earlier) > GapAfter` (15 min), an hourly point ending an hour after its start | `TrendSeriesBuilder.cs:54`, `:168`, `:190` | confirmed |
| 12 | The reveal animation applies only to `.censio-chart path.mud-chart-line` | `wwwroot/css/censio.css` | confirmed |
| 13 | The palette's secondary colour reaches 3:1 on the surface in both themes | `TrendPaletteTests.TrendPaletteLinesReachThreeToOneOnSurface` covers it as a line colour of both kinds | confirmed |
| 14 | The theme, language and texts cascade to every page | `Components/Layout/MainLayout.razor:38-40` | confirmed |
| 15 | `TrendReader` runs one chart read at a time across the server for a whole read and logs a failure as `ChartReadFailed` | `TrendReader.cs` `_gate`, `ReadAsync`; `UiLog.cs:24` | confirmed |
| 16 | Ticks arrive about 120 s apart | `docs/protocol.md:80` | confirmed |
| 17 | A zero-length subpath with round caps is drawn as a dot | SVG 2, *Painting*, `stroke-linecap` (zero-length subpaths) | confirmed |
| 18 | A part of 240 island points of the islands read, and the one part of the goods read, take about as long as a part of the figures read (0.14 to 0.29 s in memory at the bounds) | to verify: Dev, step 6, scratch program at the contract's bounds (256 islands of 512 entries) as `history.md` *Measurements*, three runs each, warm and with the page cache dropped, and the in-memory write-gate hold of one part: one part of each read; the whole opening read of an island list of 256 islands by 240 points; a refresh read of one new tick; the goods opening read of 500 goods by 240 points; results into `history.md` | unverified |
| 19 | A goods table of 500 rows by 240 points renders its trend cells in about 2 MB and adds a few MB of heap | to verify: Dev, step 6, scratch render as `web-charts.md` *Memory and size of a page*: the render batch of the trend cells; the heap held by the page's trends; the peak during one goods read (the `HistoryGoodsPoint` dictionaries of 240 points by up to 500 goods plus the sample lists); the heap while a refresh is in flight and the held read is still shown; results into `web-charts.md` | unverified |
| 21 | A page's load loop awaits `ReadDetailsAsync`, so a notice during it only marks one more load and the next tick's figures wait for the details | `DataPageBase.cs:191-210` `LoadAsync`, `ReadOnceAsync` (`await ReadDetailsAsync`) | confirmed |
| 22 | A range choice cancels the details' token through `ReplaceDetailsCancellation`, and `ClearDetails` raises the island page's `_generation` | `DataPageBase.OnParametersSetAsync`; `IslandDetail.razor.cs` `ClearDetails` | confirmed |
| 20 | Records 0086 to 0090 are unreleased | `git ls-remote --tags origin`: `v0.3.0` is `7646dd5`, before #212 | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `HistoryStoreReadGoodsTrendGivesFirstEntryDeltaPerGoodAndTick`, `HistoryStoreReadGoodsTrendGivesHourlyMeanDeltaOfFoldedHour`, `HistoryStoreReadGoodsTrendGivesNewestPointsUpToMax` | `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC1 | `TrendRangeForSparklinesStartsAtTheHourFourHoursBeforeTheLatestTick` | `tests/Censio.Server.Tests/TrendRangeTests.cs` |
| AC1 | `TrendReaderReadGoodSparklinesAsksForTheWindowAndShownGoods` | `tests/Censio.Server.Tests/TrendReaderTests.cs` |
| AC1 | `IslandDetailTrendColumnDrawsEachShownGood`, `IslandDetailSecondRowOfRepeatedGoodShowsDash`, `IslandDetailReadsTrendsOfShownRowsOnly` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC2 | `HistoryStoreReadIslandsTrendSumsBuildingsPerIslandAndTick`, `HistoryStoreReadIslandsTrendGivesHourlyBuildingsOfFoldedHour` | `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC2 | `RegionIslandsTrendColumnDrawsEachIsland`, `RegionIslandsTrendShownForIslandLatestTickLacks` | `tests/Censio.Server.Tests/RegionIslandsTests.cs` |
| AC2 | `IslandDetailWithoutValuesReadsNoTrends` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC3 | `SparklineBuilderDirectionComparesRoundedOldestAndNewest` (rows: rising, falling, equal, delta -0.004 to 0.004, buildings 0 decimals), `SparklineBuilderDirectionSkipsPointsWithoutValue` | `tests/Censio.Server.Tests/SparklineBuilderTests.cs` |
| AC3 | `TrendCellArrowCarriesItsNameInBothLanguages`, `TrendCellSparklineHiddenFromAccessibilityAndNotAnimated` | `tests/Censio.Server.Tests/TrendCellTests.cs` |
| AC3 | `TrendPaletteSparklineReachesThreeToOneOnSurface` | `tests/Censio.Server.Tests/TrendPaletteTests.cs` |
| AC4 | `SparklineBuilderFewerThanTwoValuedPointsGivesNone`, `SparklineBuilderBreaksAtMissingValueAndAfterFifteenMinutes` | `tests/Censio.Server.Tests/SparklineBuilderTests.cs` |
| AC4 | `TrendCellWithoutLineShowsDash` | `tests/Censio.Server.Tests/TrendCellTests.cs` |
| AC5 | `IslandDetailTrendCellsEmptyBeforeReadAndWhenPrerendered`, `IslandDetailFailedTrendReadShowsDashAndLine` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC5 | `RegionIslandsFailedTrendReadShowsDashAndLine` | `tests/Censio.Server.Tests/RegionIslandsTests.cs` |
| AC5 | `TrendReaderSparklineReadFailureLogsChartWarningOnce`, `TrendReaderSparklineReadCancelledLogsNothing` | `tests/Censio.Server.Tests/TrendReaderTests.cs` |
| AC5 | `IslandDetailCancelledTrendReadShowsAndLogsNothing` (an address change during a held trend read) | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC6 | `IslandDetailStoredTickRereadsTrends`, `IslandDetailRangeChoiceReadsNoTrends` (a page whose trend read has answered: no further trend call, the shown sparklines stay), `IslandDetailRangeChoiceDuringTrendReadKeepsIt` (the running read is not cancelled and is shown), `IslandDetailLanguageSwitchRedrawsTrendsWithoutRead` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC6 | `TrendReaderSparklineRefreshReadsOnlyAfterNewestHeldPoint`, `TrendReaderSparklineRefreshDropsPointsBeforeWindowAndKeepsNewest240`, `TrendReaderSparklineChangedKeySetReadsWhole` | `tests/Censio.Server.Tests/TrendReaderTests.cs` |
| AC6 | `SparklineLoaderRequestDuringReadReadsOnceMoreWithNewest`, `SparklineLoaderSameAsOfReadsNothing`, `SparklineLoaderAddressChangeCancelsAndDropsHeld`, `SparklineLoaderCancelledReadKeepsNothing` | `tests/Censio.Server.Tests/SparklineLoaderTests.cs` |
| AC6 | `RegionIslandsTickOfOtherProfileReadsNoTrends`, `RegionIslandsThemeSwitchRedrawsTrendsWithoutRead` | `tests/Censio.Server.Tests/RegionIslandsTests.cs` |
| AC7 | `HistoryStorePlanIslandPartsHoldAtMostPartSizeIslandPoints` (rows of points, islands, parts: 240, 1, 1; 240, 24, 24; 240, 240, 240; 240, 241, 241; 240, 300, 300; 0, 24, 0; 5, 0, 0; each part at most 240 island points), `HistoryStoreReadIslandsTrendPartWaitsForTrendGate`, `HistoryStoreReadIslandsTrendPartsGiveSamePointsAsOneRead` | `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC7 | `IslandDetailTableShowsWhileTrendReadHeld`, `IslandDetailStoredTickUpdatesFiguresWhileTrendReadHeld`, `IslandDetailTrendsReadAfterOverviewAndDespiteItsFailure` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC7 | `RegionIslandsStoredTickUpdatesFiguresWhileTrendReadHeld` | `tests/Censio.Server.Tests/RegionIslandsTests.cs` |
| AC8 | `SparklineBuilderPathOfMaxPointsStaysWithinMarkupLimit` | `tests/Censio.Server.Tests/SparklineBuilderTests.cs` |
| AC8 | `TrendCellMarkupHoldsOneSvgPathOfCoordinatesOnly` | `tests/Censio.Server.Tests/TrendCellTests.cs` |

Page tests read through `FakeHistoryTrends`, store tests the real `HistoryStore` on a file database. AC8 measures the
whole cell at 240 points whose coordinates all need a decimal.

## Approach

**Window.** `TrendRange.ForSparklines(latestTick)` gives the start of the UTC hour holding the latest tick minus 4 hours
and the end just after the tick, the rule of `TrendRange.For` (share the computation in a private method). Reads ask for
`TrendRange.SparklineMaxPoints` (240) points at `Points` resolution, so the newest 240 points and one part of values.

**History reads (`HistoryStore`, `IHistoryTrends`).**
- `ReadGoodsTrendAsync`: the times through `ReadTrendAsync` (fact 3), then per part one statement over the ticks of the part
  joined to the island's `TickIslands` row (session, island, area as in `TickFiguresFromSql`) and its `TickEntries`
  whose `ProductGuid` is in a JSON parameter read by `json_each` (never one parameter per good), ordered by tick and
  `Position`; the C# code keeps a good's first entry per tick and its `Delta` (a no-value delta stays no value, the second
  entry is never used). Hourly points: the island's `HourlyEntries` of the goods, `Mean(DeltaSum, DeltaCount)`. A point's
  `Deltas` holds only goods with a value.
- `ReadIslandsTrendAsync`: the times through the same times transaction, then the parts that `PlanIslandParts` gives:
  islands in chunks of 240 filled first (the last chunk takes the rest), and per chunk of `c` islands the points in slices
  of `240 / c` (integer division) points, so a part covers at most 240 island points (a tick or hourly point of one
  island) and 241 islands by 240 points take 241 parts; no points or no islands give no part. An island without a row in
  a part's ticks costs an index probe and no entry scan, so the plan does not leave such islands out. Each part is one `ReadTrendTransactionAsync`. Per tick the buildings of an
  island are the sum of its entries' `BuildingCount` (an integer `sum` in SQL is allowed, `history.md`); an island with no
  entries in the tick is absent. Per hourly point: fact 7's formula over the island's hourly entries. The island set is a
  JSON parameter as `SerializeIslands` writes it.
- Both use the trend gate as every trend read, so the clean-up of a deleted profile waits for a part as before.
- **Seam of the bound:** `PlanIslandParts` (internal, tested directly) and the trend gate, which a test holds to see a part
  wait (as `HistoryStoreTrendPartWaitsForTrendGate`).

**Server (`TrendReader`).** `ReadGoodSparklinesAsync` and `ReadIslandSparklinesAsync` build the query from
`TrendRange.ForSparklines`, run in the reader's one-read gate (the private `ReadAsync` becomes generic over its result so the
failure path stays one: `UiLog.ChartReadFailed`, a cancellation rethrows), convert the points into one list of
`SparklineSample` per requested key and call `SparklineBuilder.Build`, decimals 2 for delta and 0 for buildings. The
result holds every requested key: a `SparklineView`, or `null` for fewer than two valued points, and per key its samples
(one per point of the window, a no-value sample included, so every key shares the timeline).
- **Refresh.** Given a held read whose keys equal the requested ones and that has points, the read starts just after
  the held newest point (its time plus one tick, never before the window start), the pattern of `TrendReader.ReadStart`
  and `TrendWindow.Extend` for the wide ranges: the held samples at or after the new window start are kept, the new ones
  appended, the newest 240 kept, and every key's path is built again over the new window. Without a held read, with other
  keys (a good or island added or gone) or with a held read without points, the whole window is read. A tick folded inside the held part
  (a replayed recording older than 7 days) shows as held until the next whole read.

**Geometry (`SparklineBuilder`).** `viewBox="0 0 80 20"`, width 80, height 20. x = (time − range start) / (range end −
range start) × 80; y maps the valued points' minimum to 18 and maximum to 2 (all equal: 10). Coordinates are written with
at most one decimal, invariant culture, no exponent. The path starts with `M` at the first valued point and after every
break (a no-value point, or `TrendSeriesBuilder.BreaksBefore` between consecutive points), `L` otherwise; a point alone
between breaks gets `h0` so its round cap draws a dot (fact 17). The direction rounds the oldest and newest valued
values with `Math.Round(value, decimals, MidpointRounding.AwayFromZero)` and compares them.

**Cell (`TrendCell`, layout fixed here).** Not read yet: empty cell. Read, no line: `–`. Line: `<svg class="censio-sparkline"
width="80" height="20" viewBox="0 0 80 20" aria-hidden="true" focusable="false"><path d="…" fill="none" stroke="<hex>"
stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" /></svg>`, a space, then `<span role="img"
aria-label="<name>" title="<name>">↑</span>` (`↓`, `→`), in the text colour. The stroke is `TrendPalette.Sparkline(theme)`
(the palette's secondary colour) from the cascaded theme, so a theme switch re-renders without a read. The cell has the
class `censio-trend` (no wrap, a CSS rule beside `censio-number`); nothing of the cell carries `mud-chart-line` or sits in
`censio-chart`, so the reveal never applies. No name or other text of the data reaches the cell.

**Pages.**
- Goods table: columns icon, `Good`, `Production`, `Consumption`, `Delta`, `Trend`, `Buildings`, `Productivity`. Island
  list: `Island`, `Buildings`, `Trend`, `Produced goods`, `Goods in deficit`. The heading is `<th class="censio-trend"
  title="<tooltip>">Trend</th>`. Under the table (after the `Showing 500 of <n> goods.` line when it shows), the line
  `TrendsReadFailed` as a `<p>` after a failed read.
- **Off the load loop (fact 21).** The trends are never awaited in the page's load loop: `SparklineLoader<TKey>`, one per
  page, runs them. `ReadDetailsAsync` calls its `Request` with the address key (profile and island, or profile and region),
  the as-of time and the read, and returns; the island page first awaits its overview read as today (without the early
  return of fact 10 for the trends), so the trends start after it. `Request` does nothing when the held read or the read
  in flight is for the same address and as-of time, or when the trends of this address failed; it marks one more read when
  a read is in flight (the newest request wins, like `LoadAsync`); otherwise it starts the read with its own cancellation,
  linked to the page's. The read gets the held read of the same address for the refresh. Its answer is kept with its key
  and re-renders the page through the page's `InvokeAsync(StateHasChanged)`.
- **Keys, not generations (m2).** The held read is matched by its stored key, never by `_generation`. An address change
  (another profile, island or region) cancels the read in flight and drops the held read and the failure; a range choice,
  which raises `_generation` and cancels the details' token (fact 22), touches neither, so a running trend read goes on
  and the shown sparklines stay. A cancelled read stores nothing and logs nothing; disposing the page disposes the loader.
- The product set is the distinct `ProductGuid` of `view.Rows`; the row that gets a product's line is the first row of
  that product in `view.Rows` (tick order); the others show `–` (E8). A view without rows (`No values …`) requests no
  trends. `RegionIslands` gains `ReadDetailsAsync` for the region's islands and its own loader.
- A new as-of time (a stored tick) shows the held sparklines, keyed by product or island, until the refresh answers; a key
  missing from the held read shows an empty cell.

**Image build check.** `TrendCell.razor` is a new `.razor` file, so the Dev runs the *Image build check* of
`.squad/stack.md` (the restore-order reproduction in the sandbox) before step 7 and reports its result.

## Options considered

- **One existing read per row** — lost: 500 serialized reads per goods table and tick (record 0092).
- **A batch read with parts of 240 ticks** — lost: an island-list part scans every entry of every island, about 30 s at
  the bounds under the in-memory write gate, past the Companion's 30 s (record 0092).
- **An island building total stored at ingest** — lost: a migration with a backfill and an ingest change for a cost only
  the contract's extremes reach (record 0092).
- **MudBlazor's chart per row, or another library's sparkline** — lost: render size and a dependency (record 0093).
- **The window of the page's chart choice, 24 hours or hourly means** — lost: the Product Manager's answer (record 0091).
- **Reading the whole window on every refresh** — lost: about 160 parts per tick for 256 islands (record 0092).
- **Awaiting the trends in the page's load loop** (the first plan) — lost: the next tick's figures wait for them (fact 21).
- **One read shared by the pages of one profile, region and as-of time** — lost: a refresh is a part or two, and a cache
  with its own lifetime and invalidation is a design of its own.
- **Leaving out islands without a row in the window** — lost: their parts scan no entries.
- **Building the sparklines from `TrendPoint` and the existing `BreaksBefore`** — lost: 120,000 point objects with value
  arrays per goods read; `SparklineSample` is a struct and `BreaksBefore` gets an overload on times.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `IHistoryTrends.cs` | two members |
| Censio.Server.Data | `HistoryStore.cs` | the two reads, their statements, `PlanIslandParts` |
| Censio.Server.Data | `HistoryGoodsTrendQuery.cs`, `HistoryGoodsPoint.cs`, `HistoryIslandsTrendQuery.cs`, `HistoryIslandsPoint.cs`, `TrendIslandSlice.cs` | new |
| Censio.Server | `TrendRange.cs` | `ForSparklines`, two constants |
| Censio.Server | `TrendReader.cs` | two reads, the generic private `ReadAsync` |
| Censio.Server | `TrendSeriesBuilder.cs` | `BreaksBefore` overload; the existing one calls it |
| Censio.Server | `SparklineSample.cs`, `SparklineView.cs`, `SparklineRead{TKey}.cs`, `SparklineLoader{TKey}.cs`, `TrendDirection.cs`, `SparklineBuilder.cs` | new |
| Censio.Server | `TrendPalette.cs` | `Sparkline` |
| Censio.Server | `UiTexts.cs` | six texts, English and German |
| Censio.Server | `Components/TrendCell.razor`, `TrendCell.razor.cs` | new |
| Censio.Server | `Components/Pages/IslandDetail.razor(.cs)`, `RegionIslands.razor(.cs)` | column, reads |
| Censio.Server | `wwwroot/css/censio.css` | `.censio-frame .censio-trend { white-space: nowrap; }` |

## Signatures (for the Dev's skeleton)

One backticked declaration per new or changed member; the properties of a new type follow it in prose (all public,
required and init-only), and every new class is sealed.

Censio.Server.Data:
- `Task<HistoryTrend<HistoryGoodsPoint>> ReadGoodsTrendAsync(HistoryGoodsTrendQuery query, CancellationToken cancellationToken)` on IHistoryTrends, implemented by HistoryStore in its region named after the interface.
- `Task<HistoryTrend<HistoryIslandsPoint>> ReadIslandsTrendAsync(HistoryIslandsTrendQuery query, CancellationToken cancellationToken)` on IHistoryTrends, implemented the same way.
- `public class HistoryGoodsTrendQuery` — Guid ProfileId, HistoryIslandKey Island, IReadOnlyList<int> ProductGuids, DateTimeOffset RangeStart, DateTimeOffset RangeEnd, int MaxPoints.
- `public class HistoryGoodsPoint` — DateTimeOffset Time, bool IsHourly, IReadOnlyDictionary<int, double> Deltas.
- `public class HistoryIslandsTrendQuery` — Guid ProfileId, IReadOnlyList<HistoryIslandKey> Islands, DateTimeOffset RangeStart, DateTimeOffset RangeEnd, int MaxPoints.
- `public class HistoryIslandsPoint` — DateTimeOffset Time, bool IsHourly, IReadOnlyDictionary<HistoryIslandKey, long> Buildings.
- `internal readonly record struct TrendIslandSlice(int PointStart, int PointCount, int IslandStart, int IslandCount)`
- `internal static List<TrendIslandSlice> PlanIslandParts(int pointCount, int islandCount, int partSize)` on HistoryStore.

Censio.Server:
- `public static TrendRange ForSparklines(DateTimeOffset latestTick)` on TrendRange, with the constants SparklineMaxPoints = 240 and SparklineHours = 4 (public const int).
- `public static bool BreaksBefore(DateTimeOffset earlierTime, bool isEarlierHourly, DateTimeOffset laterTime)` on TrendSeriesBuilder (an overload; the existing one calls it).
- `public readonly record struct SparklineSample(DateTimeOffset Time, bool IsHourly, double? Value)`
- `public enum TrendDirection` — Stable (0), Rising, Falling.
- `public class SparklineView` — string Path, TrendDirection Direction.
- `public class SparklineRead<TKey>` where TKey : notnull — IReadOnlyDictionary<TKey, SparklineView?> Lines, IReadOnlyDictionary<TKey, IReadOnlyList<SparklineSample>> Samples, bool IsFailed.
- `public class SparklineLoader<TKey>` where TKey : notnull, IDisposable (SparklineLoader{TKey}.cs) — its members below.
- `public void Request(object addressKey, DateTimeOffset asOf, Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> read, Func<Task> changed)` on SparklineLoader.
- `public SparklineRead<TKey>? HeldFor(object addressKey)` on SparklineLoader (the held read only when it belongs to that address).
- `public bool IsFailedFor(object addressKey)` on SparklineLoader.
- `public static SparklineView? Build(IReadOnlyList<SparklineSample> samples, TrendRange range, int decimals)` on the static class SparklineBuilder, with the constants Width = 80 and Height = 20 (public const int).
- `public Task<SparklineRead<int>> ReadGoodSparklinesAsync(Guid profileId, IslandKey island, IReadOnlyList<int> productGuids, DateTimeOffset latestTick, SparklineRead<int>? held, CancellationToken cancellationToken)` on TrendReader.
- `public Task<SparklineRead<IslandKey>> ReadIslandSparklinesAsync(Guid profileId, IReadOnlyList<IslandKey> islands, DateTimeOffset latestTick, SparklineRead<IslandKey>? held, CancellationToken cancellationToken)` on TrendReader.
- `public static string Sparkline(UiTheme theme)` on TrendPalette.
- UiTexts gains six string properties: ColumnTrend, TrendColumnTooltip, TrendRising, TrendFalling, TrendStable, TrendsReadFailed.
- `public class TrendCell` (partial) (Components/TrendCell.razor with its code-behind) — parameters SparklineView? Line and bool IsRead, cascading parameters UiTheme Theme and UiTexts Texts.

Texts, English / German: Trend / Trend; Last 4 hours up to the latest tick / Letzte 4 Stunden bis zum letzten Tick;
Rising / Steigend; Falling / Fallend; Stable / Gleichbleibend; The trends cannot be read right now. Reload the page to try
again. / Die Trends können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.

The page members stay private. Checked against *Writing code* of the stack file: no nested type, no method over seven
parameters, no keyword of another language as a name, no enum named with a Flags or Enum suffix; TrendDirection has its
zero member; the generic files follow HistoryTrend{TPoint}.cs (SparklineRead{TKey}.cs, SparklineLoader{TKey}.cs); the
loader's two delegates keep Request at four parameters. Existing files the skeleton
rewrites: IHistoryTrends.cs, HistoryStore.cs (stubs), TrendRange.cs, TrendReader.cs, TrendSeriesBuilder.cs,
TrendPalette.cs, UiTexts.cs (both language blocks).

## Test files

`tests/Censio.Server.Data.Tests/HistoryStoreTests.cs`, `tests/Censio.Server.Tests/TrendRangeTests.cs`,
`TrendReaderTests.cs`, `SparklineBuilderTests.cs` (new), `SparklineLoaderTests.cs` (new), `TrendCellTests.cs` (new), `TrendPaletteTests.cs`,
`IslandDetailTests.cs`, `RegionIslandsTests.cs`.

Existing test code a changed signature or behavior affects, adapted by the Tester:
- `tests/Censio.Server.Tests/FakeHistoryTrends.cs` implements the two new members (held, failing and recorded like the
  others, with a failure switch per read so AC7's "despite the overview's failure" can be set up), and `PageTestHost.cs` if
  it builds the fake.
- `IslandDetailTests.IslandDetailShowsColumnsInOrder` and the column test of `RegionIslandsTests` gain the `Trend` column.
- Island page tests that count or wait for trend calls (`IslandDetailTickReadsOverviewAgain`,
  `IslandDetailOverviewFailedReadShowsReloadTextAndReadsNoMore`, `IslandDetailRangeChangeRereadsOverview`,
  `IslandDetailAxisChangeRedrawsWithoutRead`, `IslandDetailOverviewReadOfIslandLeftBehindIsDropped`) count only figures
  reads, since the trend read is one more call.
- `UiTextsTests`, if it lists the texts, gains the six.

Files no unit test can cover: `wwwroot/css/censio.css` (one rule; outside `COVERAGE_PATHSPECS`, no exclusion needed).

## Areas

- `web-charts`: new section *Sparklines and trend arrows* (window, points, cap, arrow rule, cell layout, texts, states,
  live updates, reads and their order, the refresh of the new points only, colour, no animation, accessibility, sizes of
  fact 19), and under *Limits and errors* the measured opening read of an island list at the bounds (fact 18): every open
  island list holds `TrendReader`'s gate that long when opened and for its refresh at every tick, and the time grows with
  the region's islands, whose number the island list does not limit ([`server-state.md`](../../docs/areas/server-state.md)); *Scope*, *The reads*
  ("Three reads feed the charts" gains the two), *Not here* drop #38; *Related decisions* 0091, 0093.
- `web-pages`: the column lists of the island list and goods table; *Live updates* and *Reads and memory* name the trends;
  *Not here* drops sparklines; *Logging* names the warning for a trend read too.
- `history`: *The trend reads* ("Two reads serve the charts" becomes four) with the two reads, the island-point parts and
  the measurements of fact 18 under *Measurements*; *Scope* line 10 and *Not here* drop #38; *Related decisions* 0092.
- `docs/areas/README.md`: the rows of web-charts and history drop "(#38)" and name the trends.

## Documentation updates

Dev, once the code is green:
- the four area documents above;
- `README.md` lines 22 and 204 (English and German): the limit names the trend column, its fixed 4 hours and the newest
  240 points of a fast replay instead of "no sparklines or trend arrows";
- `docs/USER_GUIDE.md`, English and German: the pages item describes the `Trend` column (sparkline, arrow and its names,
  the window, `–`), *Latest data only* (line 441, "the one place that shows the history") names it, and the limit of a
  fast replay;
- `docs/ARCHITECTURE.md`: none (its project rows name no single read; line 22 lists the history's stores in general);
- `.squad/project.md` (orchestrator writes the Dev's and Tester's text): *Security areas*, Logging and showing external
  data, adds `TrendCell` and `SparklineBuilder`; *Test doubles*, `FakeHistoryTrends`, unchanged wording unless the Tester
  adds the per-read failure switch (then its description).

No changelog exists; the PR template's checklist asks for README and the area documents, both listed.

## Architecture check

`docs/ARCHITECTURE.md` and the guarantees of `.squad/project.md` are kept: the pages stay read-only and need no login,
the history read stays bounded per transaction under the trend gate (record 0087's rule, extended by record 0092), no
name reaches markup unescaped, and no setting or schema changes (no migration). No flow changes, so `ARCHITECTURE.md`
needs no new paragraph.

## Security considerations

- [x] Limits: the product set is at most 500 GUIDs (the shown rows) and the island set is the region's islands, unbounded
  in count but read in chunks of 240 per part; both are JSON parameters read by `json_each`, never formatted into SQL. No
  byte limits on parsed input.
- [x] Exceptions: a failed trend read reaches the user only as the fixed text `TrendsReadFailed`; the exception goes to
  the log through `UiLog.ChartReadFailed` without arguments. None else.
- The island list's opening read at the bounds is tens of seconds (fact 18) and holds `TrendReader`'s gate, so a chart
  read can wait behind it; a refresh reads only the new points; each transaction is bounded as before. Stated in
  `web-charts.md` and record 0092.
- The cell writes only numbers, the hex colour and fixed texts; `Path` is built from doubles in invariant culture.

## Decision records

- `docs/decisions/0091-row-trends-show-delta-and-buildings-over-a-fixed-four-hours.md` (Proposed)
- `docs/decisions/0092-row-trends-read-once-per-table-in-island-point-parts.md` (Proposed)
- `docs/decisions/0093-sparklines-drawn-as-one-svg-path-of-our-own.md` (Proposed)

## Out of scope / follow-ups

The spec's *Out of scope*. A stored island building total (record 0092, option 4) only if the measurement of fact 18
shows the island list too slow; the Lead decides then.

## Challenge

- M1 (figures wait for the trends on every refresh): accepted. Fact 21 confirms it. The trends run in `SparklineLoader`,
  started by `ReadDetailsAsync` and never awaited in the load loop, with their own cancellation and key; tests that a
  stored tick updates the figures while a trend read is held on both pages (AC7 rows).
- M2 (the whole window on every tick): accepted. A refresh reads only the points after the held newest one and merges
  them as `TrendWindow.Extend` does (*Approach*, Refresh); a whole read on opening, on an address change and when the keys
  change. Fact 18 measures the opening and the refresh read apart; record 0092 states it. Tests in `TrendReaderTests`
  (AC6 rows).
- m1 (a range choice cancels the running trend read): accepted. The loader's cancellation is its own, not the details'
  token, so a range choice neither cancels nor repeats the read; tests `IslandDetailRangeChoiceReadsNoTrends` against an
  answered read and `IslandDetailRangeChoiceDuringTrendReadKeepsIt`; a cancelled read stores and logs nothing
  (`SparklineLoaderCancelledReadKeepsNothing`, `IslandDetailCancelledTrendReadShowsAndLogsNothing`).
- m2 (match held trends by key, not `_generation`): accepted. The loader stores the key with the read and `HeldFor`
  compares it; the range-choice test checks the sparklines stay.

## Security review

Verdict `APPROVED` with three non-blocking findings, all fixed now:
1. Fact 18 now measures the whole opening read of 256 islands by 240 points, warm and cold, and the refresh read; the
   `web-charts.md` *Limits and errors* sentence (*Areas*) says every open island list holds that time when opened and at
   each refresh and that it grows with the region's unlimited islands. Leaving out islands without rows and sharing one
   read across pages are not taken (*Options considered*); the incremental refresh cuts the per-tick cost to a part or two.
2. `PlanIslandParts` fills chunks of 240 islands first, and its test rows assert the part count (241 islands by 240
   points: 241 parts) besides the part size, with rows for 0 points and 0 islands.
3. Fact 19 now samples the peak during one goods read and the heap while a refresh is in flight with the held read shown.
