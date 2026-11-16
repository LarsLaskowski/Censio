# Plan: Trend charts per good, island and region

Source: Issue #36 | [spec.md](spec.md)
Tier: standard — new pages, reads and an index that change behavior; the trend gate keeps the existing deletion guarantee and creates, changes or removes no security control.
Security areas touched: Network exposure (a new data page under `/profiles/…`); Server database (two history reads, an index migration, a gate the clean-up shares in `src/Censio.Server.Data`); Parsing of external input (`PageAddress` good segment, the browser's offset moved from `DataStatus` to `BrowserOffset`); Logging and showing external data (names beside charts, one new log line)
Plan size: three task groups over two projects, a migration, a new page, a component and their measurements.

## Problem / root cause

Feature: [spec.md](spec.md), AC1 to AC16.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | MudBlazor 9.13.0 has a time-series chart (`MudChart`, `ChartType.Timeseries`, `TimeSeriesChartOptions`) | MudBlazor `v9.13.0` source, `Components/Chart/Charts/TimeSeries.razor(.cs)`, `MudChart.razor:107` | confirmed |
| 2 | It breaks a line at a point whose `ChartPoint.HasValue` is false (`ConnectNullPoints` default false) | `Base/MudAxisLineChartBase.cs:296-303`, `Base/DefaultAxisLineChartOptions.cs:60` | confirmed |
| 3 | A point without value still counts for the value axis range (`Y` of every point) | `Charts/TimeSeries.razor.cs` `GetYRangeWithPadding` | confirmed |
| 4 | It has no animation; the only transition is a 0.2 s stroke colour change | `Styles/components/_charts.scss:140-143`, no `animat` in `Components/Chart` | confirmed |
| 5 | Time-axis labels are `DateTime.ToString(TimeLabelFormat)`, tooltip labels `ToString(TooltipTimeLabelFormat)`; value labels take `YAxisToStringFunc` | `TimeSeries.razor.cs:302`, `:345-365`, `Base/DefaultAxisChartOptions.cs:69` | confirmed |
| 6 | The legend (`Parts/Legend.razor`) and a `TooltipTemplate` (`RenderFragment<(SvgPath, string)>`) are Blazor render output, text escaped; the value- and time-axis labels are written unescaped as `MarkupString` | `Base/BaseAxisChart.razor:46` and `:76`, `TimeSeries.razor:183-188`, `Base/MudChartBase.cs:72` | confirmed |
| 7 | `ChartData<T>.Points` holds mutable `ChartPoint<T>` with a settable `HasValue` | `Models/ChartData.cs:52`, `Models/ChartPoint.cs` | confirmed |
| 8 | ApexCharts licence facts of the spec's M1 | spec *Challenge* M1, packages read | confirmed |
| 9 | `ReadSeriesAsync` has no caller outside tests; one good on one island | `src/Censio.Server.Data/HistoryStore.cs:237`, grep | confirmed |
| 10 | Tick entries are indexed only by (island, position); hourly entries by (island, good) | `CensioDbContext.cs` `TickEntryRow`, `HourlyEntryRow` | confirmed |
| 11 | On the in-memory database every read takes `ServerDatabase.Gate` | `StoreAccess.cs:127-140` | confirmed |
| 12 | The fixture's ticks end 22:54:38 to 00:21:36 UTC (2026-06-10/11), pause 23:08:43 to 23:27:03, session change 00:12:12 to 00:21:36 | scratch script over `long-session.jsonl`; tick receive time is its last island frame (`pipe.md`) | confirmed |
| 13 | `Routes.razor` cascades the initial `UiTheme`, which `MainLayout` takes as `InitialTheme`; the toggled `_theme` (`MainLayout.razor.cs:209`) is not cascaded: `@Body` gets only language and texts, so a page sees the initial theme only | `Routes.razor:1`, `MainLayout.razor.cs:84-85, 207-211, 256`, `MainLayout.razor` `MudContainer` block | confirmed |
| 14 | `PageFormat` formats `float?` only and rounds with `Math.Round(x, 2, AwayFromZero)`; `Round` is internal to `Censio.Server`; numbers hold only digits, `,`, `.`, `+`, `-` (no value `–`) | `PageFormat.cs:59, 113-120, 129, 141` | confirmed |
| 15 | A CSS reveal of the chart's line paths (`clip-path: inset` keyframes) runs in current desktop browsers | to verify: Product Manager in AC14 on the branch build | unverified |
| 16 | The trend reads meet N3 at the bounds (256 islands per tick); the index's bytes per entry and its cost on store, fold and removal steps at the bounds; the migration's build time; server memory and first render batch of one good page at 2,880 points | to verify: Dev, scratch program in task 7, as `history.md` *Measurements* | unverified |
| 17 | Every page is prerendered: `Routes @rendermode="InteractiveServer"`; `OnParametersSetAsync` returns `LoadAsync()`, so the response waits for every read in `ReadOnceAsync`; `RendererInfo.IsInteractive` is false while prerendering; bUnit 2.15.2 has `BunitContext.SetRendererInfo` and throws `MissingRendererInfoException` when it is unset | `App.razor:13`, `DataPageBase.cs:315-327`, `bunit.xml` of 2.15.2 | confirmed |
| 18 | `ReadAfterNoticeAsync` stops only when disposed or `Mode == Failed`; `ReadOnceAsync` keeps the view on a re-read of the same profile and island | `DataPageBase.cs:286, 205-213` | confirmed |
| 19 | `PRAGMA wal_checkpoint(TRUNCATE)` runs once per clean-up (`HistoryStore.cs:214-219`); a reader open on the latest snapshot blocks it (`(1,9,9)`, `-wal` stays 4,120,032 bytes; `(0,0,0)` and 0 bytes after the reader ends) | Security's probe, plan security review | confirmed |
| 20 | The tick entries' floats are stored as `int?` bit patterns and decoded with `BitConverter.Int32BitsToSingle`; `BuildingCount` is `int`; the key figures round the delta with `PageFormat.Round` | `TickEntryRow.cs:33, 73`, `HistoryStore.cs:386`, `ProfileViewBuilder.cs:118-131` | confirmed |
| 21 | A region's islands are every stored island of the profile in that region, not bounded by 256; `json_each` of one parameter is already used | `ProfileViewBuilder.cs:27`, `ServerStore.cs:354` | confirmed |
| 22 | MudBlazor draws one hover `circle` per point of each line when tooltips are on: 17,280 on a good page at 2,880 points | `TimeSeries.razor:121-158` | confirmed |
| 23 | `Program.cs` constructs the retention's `HistoryStore` inline; `ServerDatabase.Gate` is internal, visible to `Censio.Server.Data.Tests` | `Program.cs:54`, `ServerDatabase.cs:42`, `Censio.Server.Data.csproj:10` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrendReaderFixtureIslandGoodMatchesStoredTicks`, `TrendSeriesBuilderFixturePauseBreaksSessionChangeJoins` | `tests/Censio.Server.Tests/TrendReaderTests.cs`, `tests/Censio.Server.Tests/TrendSeriesBuilderTests.cs` |
| AC2 | `TrendReaderFixtureRegionTotalIsSumOfCarryingIslands` (sums the decoded floats as doubles in island key order), `HistoryStoreGoodTrendPointWithoutCarrierHasNoValue` | `TrendReaderTests.cs`, `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC3 | `TrendReaderFixtureFiguresMatchKeyFiguresPerTick`, `HistoryStoreFiguresTrendHourlyPointFollowsHourlyRules`, `HistoryStoreFiguresTrendDeficitRoundsDelta` (a delta of −0.004 is no deficit, −0.005 is) | `TrendReaderTests.cs`, `HistoryStoreTests.cs` |
| AC4 | `TrendReaderFixtureAfterRetentionShowsJoinedHourlyMeans` | `TrendReaderTests.cs` |
| AC5 | `GoodTrendShowsHeadingLinksAndChartHeadings`, `GoodTrendMalformedOrUnknownAddressShowsNotFound`, `IslandDetailGoodNameLinksToGoodPage`, `PageAddressTryParseGoodAcceptsSessionForm`, `PageAddressGoodLinkCarriesProfileIslandAndGood` | `tests/Censio.Server.Tests/GoodTrendTests.cs`, `IslandDetailTests.cs`, `PageAddressTests.cs` |
| AC6 | `TrendRangeBeforeStartsAtHourOf24HoursBack`, `HistoryStoreGoodTrendReadsRangeOnly`, `TrendSeriesBuilderHourlyPointsJoinAndMissingHourBreaks` | `tests/Censio.Server.Tests/TrendRangeTests.cs`, `HistoryStoreTests.cs`, `TrendSeriesBuilderTests.cs` |
| AC7 | `GoodTrendStatesShowTexts`, `GoodTrendNoticeAfterFailedReadReadsNoMore`, `TrendChartOverCapShowsNewestAndCapLine`, `UiTextsChartTextsInBothLanguages` | `GoodTrendTests.cs`, `tests/Censio.Server.Tests/TrendChartTests.cs`, `UiTextsTests.cs` |
| AC8 | `GoodTrendTickOfProfileRedrawsCharts`, `GoodTrendTickOfOtherProfileChangesNothing`, `GoodTrendNoticeDuringReadReadsOnceMore`, `GoodTrendThemeSwitchRedrawsWithoutRead`, `GoodTrendLanguageSwitchRedrawsWithoutRead`, `GoodTrendOtherGoodShowsLoadingUntilItsRead`, `TrendChartNewReadKeepsChartUntilOffsetAnswers` | `GoodTrendTests.cs`, `TrendChartTests.cs` |
| AC9 | `TrendChartTooltipShowsTimeHourlyMarkAndValues`, `TrendSeriesBuilderUsesOffsetOrUtc` | `TrendChartTests.cs`, `TrendSeriesBuilderTests.cs` |
| AC10 | `TrendPaletteLinesReachThreeToOneOnSurface`, `TrendPaletteLinesOfAChartDifferInHue`, `TrendChartCarriesThemePaletteAndAnimationClass` | `tests/Censio.Server.Tests/TrendPaletteTests.cs`, `TrendChartTests.cs` |
| AC11 | `TrendChartMarkupInNamesIsText`, `TrendChartAxisLabelsHoldOnlyNumberAndTimeText`, `GoodTrendMarkupInNamesIsText`; check: Reviewer, no `MarkupString` in `TrendChart` | `TrendChartTests.cs`, `GoodTrendTests.cs` |
| AC12 | `GoodTrendLoadsResourcesFromServerOnly`; check: Reviewer, no package added to `Directory.Packages.props` or a project file | `GoodTrendTests.cs` |
| AC13 | `HistoryStoreGoodTrendReadsNewestMaxPointsAndCount`, `HistoryStoreTrendPartsGiveSamePointsAsOneRead`, `TrendReaderReadsOneAtATime`, `TrendReaderDisposedPageCancelsRead`, `TrendReaderFailedReadLogsWithoutValues`, `GoodTrendPrerenderReadsNoChart`; manual: Dev, the N3 measurement in task 7 | `HistoryStoreTests.cs`, `TrendReaderTests.cs`, `GoodTrendTests.cs` |
| AC14 | manual: Product Manager, on a local build of the branch head (user guide *Build the server image* or `dotnet run --project src/Censio.Server`, the Companion from *Build the Companion*), `long-session.jsonl` replayed with `--server` after the server's start run, before its next run | — |
| AC15 | check: Reviewer reads README and user guide against the spec | — |
| AC16 | `HistoryStoreClearDeletedWaitsForTrendPartThenTruncates`, `HistoryStoreTrendPartWaitsForTrendGate` | `HistoryStoreTests.cs` |

## Approach

**Data reads** (`Censio.Server.Data`, record 0087). `HistoryStore` implements a new `IHistoryTrends`:

- `ReadGoodTrendAsync`: per tick of the range, the first entry (in position) of the good on each island of `Islands`,
  generation, consumption and delta each summed over the islands that carry it, no value where none does; per hourly
  point the islands' means summed the same way. `ReadFiguresTrendAsync`: per tick, the island's entries as
  `ProfileViewBuilder.Figure` counts them (no value when the tick lacks the island or it has no entries); per hourly
  point the rules of spec S3 (buildings `Math.Round(sum of BuildingCountSum / EntryCount, AwayFromZero)`, produced
  `BuildingCountSum > 0`, deficit `Math.Round(DeltaSum / DeltaCount, 2, AwayFromZero) < 0` with `DeltaCount > 0`).
- Floats: the SQL returns the stored bit patterns; C# decodes them with `BitConverter.Int32BitsToSingle`, widens to
  `double` and sums in island key order; a tick's deficit is `Math.Round((double)delta, 2, AwayFromZero) < 0`, the
  expression of `PageFormat.Round` (not reachable from this project). No float column goes through SQL `SUM`, `AVG` or
  `ROUND`; integer columns may.
- The island set is one JSON parameter read through `json_each` (as `ServerStore`'s icon read), never one parameter per
  island and never formatted into the SQL text. Rows per tick stay bounded by the tick's islands (256, the contract).
- First one read transaction takes the times of the newest `MaxPoints` points (ticks by `(ReceiveTicks, Number)`
  descending, hourly points by hour) and the count of all; then the values in parts of at most 240 consecutive points,
  oldest first, one read transaction each. A part keeps only its points; ticks are never materialized.
- **Trend gate.** `ServerDatabase.TrendGate` (`SemaphoreSlim(1, 1)`) is taken by every transaction of a trend read
  (outside `StoreAccess.ReadAsync`, so before the in-memory write gate) and by `ClearDeletedAsync` before the write
  gate, held through the checkpoint. Both take the trend gate first: no deadlock. The clean-up waits for the part in
  progress, and no part opens while it truncates. Seam of AC16: the test holds `TrendGate` itself.
- A migration (`dotnet ef migrations add AddTickEntryProductIndex`) adds the index (`TickIslandId`, `ProductGuid`,
  `Position`) on `TickEntries`; the reads use it, the series read keeps its SQL.

**Server**:

- `Program.cs` creates one `HistoryStore` for the retention and registers it as `IHistoryTrends`.
- `TrendRange.Before(latestTick)`: start = UTC hour start of `latestTick − 24 h`, end = `latestTick` + 1 tick (end
  exclusive). `TrendRange.MaxPoints` = 2,880.
- `TrendReader` (singleton): its own `SemaphoreSlim(1, 1)` for a whole read, never `PageReader`'s; maps islands to
  `HistoryIslandKey`, calls `IHistoryTrends`, converts to `TrendView` (values as `double?` in line order: production,
  consumption, delta / buildings, produced goods, goods in deficit). A cancelled read rethrows; any other exception
  logs `UiLog.ChartReadFailed` — Warning, `A chart could not be read from the database.`, the exception and no other
  argument — and returns `IsFailed`. The bound's seam is `IHistoryTrends` (`FakeHistoryTrends` blocks, fails and counts
  calls and concurrent calls).
- `DataPageBase`: `ReadOnceAsync` calls `ReadDetailsAsync(view, token)` after `Show(PageMode.Ready, view)` only when
  `RendererInfo.IsInteractive`; the prerender shows the charts as `Loading…` and reads none. Notice coalescing and
  cancellation cover the chart reads. `OnParametersSetAsync` calls `ClearDetails()` when the address key changes,
  before `LoadAsync`.
- The pages keep one `TrendRead` per chart, a read number and a failed flag. `ClearDetails` drops the reads
  (`Loading…`), clears the flag and counts the read number up. Once a chart read fails, every chart of the page shows
  the reload text and `ReadDetailsAsync` returns at once until `ClearDetails` or a reload (spec E3).
- `GoodTrend` page, route of spec S1: parses the good with `PageAddress.TryParseGood` (the session rule); known when the
  island is (as `IslandDetail`); reads the island chart (`[island]`) then the region chart (the region's island keys
  of the view). Name: `PageNames.Good(productGuid, catalog)`.
- `IslandDetail`: the good name cell becomes `<MudLink Href="@PageAddress.GoodLink(...)">`; the overview chart reads
  `ReadFiguresAsync`.
- `TrendChart` component: parameters heading, kind, read, as-of, read number. Shows `Loading…` while the read is
  null, the reload text when failed, `No values in this period.` when no point has a value, else the chart once the
  browser's offset is settled (`BrowserOffset.AskAsync` for the as-of instant, shared with `DataStatus`, which keeps its
  behavior). A new read of the same address keeps the drawn series and offset until the new read's offset is answered,
  then swaps both. The chart is keyed by the read number, so a new read re-creates it (the animation restarts) and a
  theme or language switch does not.
- `TrendSeriesBuilder.Build`: one `ChartSeries<double>` per line; X = `time.ToOffset(offset).DateTime` (UTC without a
  usable offset); a no-value point where the line lacks a value, and one at the earlier point's end where
  `BreaksBefore` (later start > earlier end + 15 min; tick end = its time, hourly end = start + 1 h). A no-value
  point's `Y` is the line's first value, so it never stretches the axis (fact 3).
- Options: `TimeLabelFormat` `HH\:mm` (digits and a literal colon), `TimeLabelSpacing` 3 h with rounding;
  `TooltipTimeLabelFormat` `yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff` (culture-free); `YAxisToStringFunc` returns only
  `PageFormat.Rate` / `Count` text; `XAxisLabels` and `YAxisTitle` unset; `ShowDataMarkers` false; `ChartPalette` from
  `TrendPalette` for the cascaded theme. Axis labels reach the page as markup (fact 6), so nothing but those number and
  time texts is passed to them. `TooltipTemplate` finds the hovered point by series and X label and writes SVG text
  through the render tree, never `MarkupString`: the time (`PageFormat.Time` with the offset), `Hourly average` for an
  hourly point, one line per series `<name>: <value>` (rates by `PageFormat.Rate`/`Delta` with new `double?` overloads,
  counts by `Count`).
- `TrendPalette`: good lines production `Success`, consumption `Error`, delta `Secondary`; figures buildings
  `Secondary`, produced `Success`, deficit `Error` — values of `CensioTheme`'s palettes, which reach 4.5:1 on surface;
  the lines of one chart differ in hue by 37° or more in both themes (`Primary` lies within 10° of `Error`).
- `MainLayout` wraps `@Body` in a `UiTheme` cascade of `_theme`, which hides `Routes`' initial one. `censio.css`:
  `.censio-chart path.mud-chart-line` animates once per element, 0.8 s, `clip-path: inset(0 100% 0 0)` to `inset(0)`
  with opacity 0 to 1 (spec Q8).

Layout of the good page, top to bottom: `h1` good name; `<p>` island link · region link · profile; banner and as-of
line; `<p>` range line; per chart an `h2` (`mud-typography-h6`) heading, the chart (height 300 px, legend below), then
the cap line when E6 applies. The overview sits on the island detail between the as-of line and the table, with its
`h2`, the range line under it. Tooltip layout free for the Tester, approved in step 9.

**Measurements** (task 7, fact 16): the N3 reads with 256 islands per tick; store, fold and removal steps of the tick
at the bounds with the index (131,072 entries); the index's bytes per entry; the migration over 7 days of fixture ticks;
server memory held by one open good page and the size of its first render batch at 2,880 points per line.

## Options considered

- Chart library: record 0086 (ApexCharts 8, pinned 6.0.2, own SVG, MudBlazor — the Product Manager chose MudBlazor).
- Reads: record 0087 (series read per island, aggregate reads without index, with index, pre-computed series; the
  clean-up retrying an hour later against chart reads).
- Range and gap: record 0088.
- Offset per point (one browser call per point, exact across a daylight-saving change) — lost: up to 2,880 answers per
  read for an hour's error once a season; one offset for the latest tick, as the as-of line uses.
- Line colours as CSS variables in the SVG `stroke` attribute — lost: presentation attributes do not resolve `var()`
  reliably; the cascaded theme is certain.
- Animation by a script — lost: a new script for a style effect; a CSS rule needs none (spec Q8, the Product Manager's choice).
- Chart reads in `OnAfterRenderAsync` instead of skipping them in the prerender — lost: it leaves `LoadAsync`'s notice
  coalescing; prerendering switched off for the data pages — lost: changes every page for the charts' sake.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `IHistoryTrends`, `HistoryIslandKey`, `HistoryGoodTrendQuery`, `HistoryFiguresTrendQuery`, `HistoryGoodPoint`, `HistoryFiguresPoint`, `HistoryTrend` | new |
| Censio.Server.Data | `HistoryStore`, `ServerDatabase`, `CensioDbContext`, `Migrations/*_AddTickEntryProductIndex.cs`, snapshot | the two reads, the trend gate, the index |
| Censio.Server | `TrendRange`, `TrendView`, `TrendPoint`, `TrendRead`, `TrendReader`, `TrendKind`, `TrendSeriesBuilder`, `TrendPalette`, `BrowserOffset` | new |
| Censio.Server | `Components/TrendChart.razor(.cs)`, `Components/Pages/GoodTrend.razor(.cs)` | new |
| Censio.Server | `DataPageBase`, `IslandDetail.razor(.cs)`, `DataStatus.razor.cs`, `MainLayout.razor`, `PageAddress`, `PageFormat`, `UiTexts`, `UiLog`, `Program.cs`, `wwwroot/css/censio.css` | changed |

## Signatures (for the Dev's skeleton)

Censio.Server.Data, one type per file:

- `public interface IHistoryTrends` with `Task<HistoryTrend<HistoryGoodPoint>> ReadGoodTrendAsync(HistoryGoodTrendQuery query, CancellationToken cancellationToken);` and `Task<HistoryTrend<HistoryFiguresPoint>> ReadFiguresTrendAsync(HistoryFiguresTrendQuery query, CancellationToken cancellationToken);`, implemented by `public sealed class HistoryStore : IHistoryTrends`, which adds `public const int TrendPartSize = 240;`
- In `ServerDatabase`: `internal SemaphoreSlim TrendGate { get; }`, disposed with the write gate
- `public readonly record struct HistoryIslandKey(int SessionGuid, byte IslandId, byte AreaIndex);`
- `public sealed class HistoryGoodTrendQuery` with `public required Guid ProfileId { get; init; }`, `public required IReadOnlyList<HistoryIslandKey> Islands { get; init; }`, `public required int ProductGuid { get; init; }`, `public required DateTimeOffset RangeStart { get; init; }`, `public required DateTimeOffset RangeEnd { get; init; }`, `public required int MaxPoints { get; init; }`
- `public sealed class HistoryFiguresTrendQuery` with the same profile, range and maximum properties and `public required HistoryIslandKey Island { get; init; }`
- `public sealed class HistoryGoodPoint` with `public required DateTimeOffset Time { get; init; }`, `public required bool IsHourly { get; init; }`, `public required double? Generation { get; init; }`, `public required double? Consumption { get; init; }`, `public required double? Delta { get; init; }`
- `public sealed class HistoryFiguresPoint` with the same time and hourly flag and `public required long? Buildings { get; init; }`, `public required int? ProducedGoods { get; init; }`, `public required int? GoodsInDeficit { get; init; }`
- `public sealed class HistoryTrend<TPoint>` with `public required IReadOnlyList<TPoint> Points { get; init; }`, `public required int TotalCount { get; init; }`

Censio.Server:

- `public sealed class TrendRange` with `public const int MaxPoints = 2880;`, `public required DateTimeOffset Start { get; init; }`, `public required DateTimeOffset End { get; init; }`, `public static TrendRange Before(DateTimeOffset latestTick)`
- `public sealed class TrendPoint` with the time and hourly flag as above and `public required IReadOnlyList<double?> Values { get; init; }`
- `public sealed class TrendView` with `public required IReadOnlyList<TrendPoint> Points { get; init; }` and the total count as above
- `public sealed class TrendRead` with `public required TrendView? View { get; init; }`, `public required bool IsFailed { get; init; }`
- `public enum TrendKind` with the members Good and Figures
- `public sealed class TrendReader` with `public TrendReader(IHistoryTrends trends, ILogger<TrendReader> logger)`, `public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, DateTimeOffset latestTick, CancellationToken cancellationToken)`, `public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, DateTimeOffset latestTick, CancellationToken cancellationToken)`
- `public static class TrendSeriesBuilder` with `public static readonly TimeSpan GapAfter;` (15 minutes), `public static IReadOnlyList<ChartSeries<double>> Build(TrendView view, IReadOnlyList<string> lineNames, int? offsetMinutes)`, `public static bool BreaksBefore(TrendPoint earlier, TrendPoint later)`
- `public static class TrendPalette` with `public static IReadOnlyList<string> For(TrendKind kind, UiTheme theme)`
- `public static class BrowserOffset` with `public static Task<int?> AskAsync(IJSRuntime jsRuntime, DateTimeOffset instant, CancellationToken cancellationToken)`
- In `PageAddress`: `public static bool TryParseGood(string? text, out int productGuid)`, `public static string GoodLink(Guid profileId, IslandKey island, int productGuid)`
- In `PageFormat`: `public static string Rate(double? value, UiLanguage language)`, `public static string Delta(double? value, UiLanguage language)`
- In `UiLog`: `public static partial void ChartReadFailed(ILogger logger, Exception exception);`
- In `UiTexts`, each with the required-init pattern of its neighbours, the texts of spec S1, S2, S3, E2, E6 and S6: `public required string ChartRange { get; init; }`, `public required string ChartThisIsland { get; init; }`, `public required string ChartRegionTotal { get; init; }`, `public required string ChartOverview { get; init; }`, `public required string ChartNoValues { get; init; }`, `public required string ChartCapFormat { get; init; }`, `public required string HourlyAverage { get; init; }`
- In `DataPageBase`: `protected virtual Task ReadDetailsAsync(ProfileView view, CancellationToken cancellationToken)`, `protected virtual void ClearDetails()`
- `public sealed partial class TrendChart` (component, in Components), each a component parameter: `public string Heading { get; set; }`, `public TrendKind Kind { get; set; }`, `public TrendRead? Read { get; set; }`, `public DateTimeOffset? AsOf { get; set; }`, `public int ReadNumber { get; set; }`
- `public partial class GoodTrend` (page, in Components/Pages, base DataPageBase), each a route parameter: `public string? Profile { get; set; }`, `public string? Session { get; set; }`, `public string? Island { get; set; }`, `public string? Area { get; set; }`, `public string? Good { get; set; }`

Files the skeleton rewrites: none whole; the types named "In …" get members.

## Test files

`tests/Censio.Server.Data.Tests/HistoryStoreTests.cs`; `tests/Censio.Server.Tests/` `TrendReaderTests.cs`,
`TrendSeriesBuilderTests.cs`, `TrendRangeTests.cs`, `TrendChartTests.cs`, `TrendPaletteTests.cs`, `GoodTrendTests.cs`,
`IslandDetailTests.cs`, `PageAddressTests.cs`, `UiTextsTests.cs`; new double `tests/Censio.Server.Tests/FakeHistoryTrends.cs`.

Existing call sites: `PageTestHost` registers `IHistoryTrends` (a `HistoryStore` over its database) and `TrendReader`,
sets the renderer info to interactive (`SetRendererInfo(new RendererInfo("Server", true))`, fact 17; the prerender test
sets `("Static", false)`), and hosts pages where a theme or language switch is tested under `MainLayout`, so the switch
goes through its buttons — the Tester, task 5. `DataStatusTests` stay unchanged (no input handled differently).
`UiTextsTests` that enumerate every text: the Tester adds the new ones.

Files no unit test can cover: none (`censio.css` is no measured code; its effect is AC14).

## Areas

- New area `web-charts` ("The charts of the data pages: the good page's charts, the island overview, the range, gaps,
  hourly means, region totals, the cap, tooltips, colours, animation, the chart reads' bounds and failures, texts";
  not here: the pages' frame and routes (web-pages), the history reads (history)).
- `web-pages`, `web-ui`, `history`, `server-data`: *Documentation updates*.

## Documentation updates

All by the Dev, against the code as built:

- `docs/areas/web-charts.md` (new, from `_template.md`) and its row in `docs/areas/README.md`; links records 0086, 0088;
  states that axis labels reach the page as markup and take only number and time text, that the prerender reads no
  chart, that the N3 figures cover 256 islands per tick, and the memory and first render batch of task 7.
- `docs/areas/web-pages.md`: *Pages and routes* "Four routes" → five, with the good page's row and heading; *Island
  detail*: good name links, the overview chart (pointer); *Logging*: "A failed chart read logs the warning `A chart
  could not be read from the database.` with the exception and nothing else"; *Reads and memory*: pointer to the chart
  reads; *Not here*: drop #36.
- `docs/areas/web-ui.md`: *Texts* sentence on where data page texts live adds `web-charts.md`; *Contrast*: chart line
  colours reuse the listed pairs.
- `docs/areas/history.md`: *Scope* and *Not here* (#36 now `web-charts.md`); *The series read*: the two trend reads,
  their points, bound, parts, the trend gate and the fold-between-parts case; *Deleting a profile*, *Clean-up*: "A chart
  read's part never overlaps the truncation: the clean-up waits for the part in progress."; *Measurements*: the rows of
  task 7 but the page's memory and render batch.
- `docs/areas/server-data.md`: the tick entries' new index where the tables are described; the trend gate beside the
  write gate.
- `README.md` (English and German): the "no trend charts" line becomes what #37 and #38 still add; the web
  application paragraph names the good page, region total and overview.
- `docs/USER_GUIDE.md` (English and German): *Web interface* gains the charts (range, gaps, hourly average, tooltip,
  old recordings as hourly means after the next run, "Not yet tried" for AC14 until done).
- `.squad/project.md` (exact text reported, the orchestrator writes it): *Logging and showing external data* adds
  `TrendChart`, `TrendSeriesBuilder`, `TrendReader`; *Parsing* adds `BrowserOffset`; *Test doubles* adds
  `FakeHistoryTrends` (the Tester's text).

## Architecture check

No guarantee of `.squad/project.md` changes: pages stay open without login (record 0006); gaps stay gaps (record 0031)
— the charts never draw a missing value as zero; history retention and deletion keep their guarantees: the trend gate
lets no chart read hold the truncation back (records 0021, 0084), and a deleted profile's page shows `Not found.`.
`docs/ARCHITECTURE.md` names no chart library; no update.

## Security considerations

- [x] No limits on parsed input beyond the good segment, which is the session rule (ten ASCII digits, sign) on the
  decoded segment.
- [x] Exceptions: a failed chart read shows the reload text of `web-pages.md`; a JavaScript or timeout failure of the
  offset shows UTC with ` UTC`; no exception text reaches a page.
- Names never reach the chart: legend and tooltip take `UiTexts` and numbers through the render tree; the axis labels,
  which MudBlazor writes as raw markup (fact 6), take only `PageFormat` numbers and `HH:mm` (AC11).
- The reads are bounded by points (2,880), gated one at a time, run only on an interactive page, and pass the island
  set as one JSON parameter; the in-memory write gate is held per part (N3, fact 16). The clean-up waits for a trend
  part and blocks the next, so the deleted names leave the `-wal` file as before (AC16).

## Decision records

- `docs/decisions/0086-charts-drawn-with-mudblazors-own-time-series-chart.md` (Proposed; amends 0025)
- `docs/decisions/0087-chart-reads-aggregate-per-point-bounded-in-parts-with-a-product-index.md` (Proposed)
- `docs/decisions/0088-charts-show-the-day-up-to-the-latest-tick-with-gaps-after-15-minutes.md` (Proposed)

`decision-check.py` fails on the area `web-charts` until the Dev adds the area document and its index row (G3).

## Out of scope / follow-ups

The spec's *Out of scope*; a second value axis for the overview (MudBlazor has one) if AC14 finds it unreadable.

## Challenge

- M1 (prerendering runs every chart read) — accepted: fact 17; `ReadDetailsAsync` runs only when
  `RendererInfo.IsInteractive`, the prerender shows `Loading…`; test `GoodTrendPrerenderReadsNoChart` (AC13, spec N2).
- Minor 1 (failed read retried) — accepted: the pages' failed flag stops `ReadDetailsAsync` until `ClearDetails` or a
  reload; test `GoodTrendNoticeAfterFailedReadReadsNoMore` (AC7).
- Minor 2 (Primary and Error alike) — accepted: buildings take `Secondary`; test `TrendPaletteLinesOfAChartDifferInHue`
  asserts 30° or more between any two lines of a chart (the chosen ones differ by 37° or more).
- Minor 3 (float bits) — accepted: decoded in C#, deficit by the expression of `PageFormat.Round`, no SQL aggregate of a
  float column (fact 20); test `HistoryStoreFiguresTrendDeficitRoundsDelta`.
- Minor 4 (island set unbounded) — accepted: one JSON parameter through `json_each` (fact 21); rows per tick stay bounded
  by the tick's 256 islands; `web-charts.md` states the measurement covers 256 islands per tick.
- Minor 5 (same-route navigation) — accepted: `ClearDetails` on an address change; `TrendChart` keeps the drawn chart
  and offset on a re-read until both are in; tests `GoodTrendOtherGoodShowsLoadingUntilItsRead`,
  `TrendChartNewReadKeepsChartUntilOffsetAnswers` (AC8).
- Minor 6 (AC8 tests) — accepted: `GoodTrendNoticeDuringReadReadsOnceMore`, `GoodTrendLanguageSwitchRedrawsWithoutRead`,
  counting calls on `FakeHistoryTrends`.
- Minor 7 (fact 13 wrong) — accepted: fact 13 rewritten; `MainLayout` cascades `_theme` around `@Body`; the theme and
  language tests switch through the layout.

## Security review

- 1 (chart reads hold up the clean-up) — accepted, the smallest fix: `ServerDatabase.TrendGate`, taken per trend
  transaction and by `ClearDeletedAsync` before the write gate through the checkpoint; on `ServerDatabase` rather than
  `HistoryStore` so that every instance shares it (fact 23), and `Program.cs` uses one `HistoryStore`. Spec AC16 and
  N9, tests `HistoryStoreClearDeletedWaitsForTrendPartThenTruncates` and `HistoryStoreTrendPartWaitsForTrendGate`,
  `history.md` sentence as proposed, record 0087 names the rejected alternative.
- 2 (axis labels raw markup) — accepted: fact 6 rewritten; options and *Security considerations* restrict the axis
  labels to `PageFormat` numbers and `HH\:mm`; test `TrendChartAxisLabelsHoldOnlyNumberAndTimeText` (digits, `:.,+-`,
  U+2013 and spaces only); Reviewer check of no `MarkupString` in `TrendChart`; record 0086 option 4 corrected.
- 3 (index cost on fold and removal) — accepted: fact 16 and the *Measurements* update.
- 4 (island set as parameters) — accepted: one JSON parameter through `json_each`, never in the SQL text (*Approach*).
- 5 (log text) — accepted: `A chart could not be read from the database.`, Warning, the exception only; spec N8 and the
  `web-pages.md` *Logging* text reworded.
- 6 (memory and render batch) — accepted: measured in task 7 at 2,880 points per line, recorded in `web-charts.md`.
