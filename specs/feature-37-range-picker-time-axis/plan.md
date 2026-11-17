# Plan: Range picker and real-time / game-time switch

Source: Issue #37 | [spec.md](spec.md)
Tier: standard — new chart behavior (ranges, hourly resolution, game-time axis, two preference cookies) that creates, changes or removes no security control.
Security areas touched: Network exposure (the preference cookies, `UiPreferences`, `App.razor`); Server database (the history reads in `HistoryStore`); Logging and showing external data (`TrendChart`, `TrendSeriesBuilder`, `TrendReader`)
Plan size: four task groups over the history read, the reader, the drawing and the page wiring, each with its signatures and tests.

## Problem / root cause

The feature of [spec.md](spec.md) (AC1 to AC7).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The range is fixed at the day before the latest tick | `TrendRange.Before`, `src/Censio.Server/TrendRange.cs`; called by `TrendReader.ReadGoodAsync`/`ReadFiguresAsync` | confirmed |
| 2 | The trend reads take the newest `MaxPoints` times and read values in parts of `TrendPartSize` 240 points, each under the trend gate | `HistoryStore.cs` lines 17, 541–591, 911–936 | confirmed |
| 3 | The tick times query and the count use the index `Ticks(ProfileId, ReceiveTicks, Number)`; hourly points `HourlyPoints(ProfileId, HourTicks)` | `CensioDbContextModelSnapshot.cs` lines 240, 452 | confirmed |
| 4 | Ticks store `GameTime`; hourly points `FirstGameTime`/`LastGameTime` over all folded ticks | `HistoryStore.cs` lines 47, 59 (`HourlySeriesSql`, `TickSeriesSql`); `docs/areas/history.md` *What is kept* | confirmed |
| 5 | The trend points carry no game time | `HistoryGoodPoint.cs`, `HistoryFiguresPoint.cs`, `TrendPoint.cs` | confirmed |
| 6 | Ticks in which no island holds an entry are stored (no filter on ingest) | no use of `IsNoDataYet`/empty entries in `src/Censio.Server*` except `ProfileViewBuilder.cs:120` (view only) | confirmed |
| 7 | Floats are never summed in SQL; hourly means are sum ÷ count per island, then summed over islands | `HistoryStore.ReadGoodPartAsync`, `Mean`; `docs/areas/history.md` *The trend reads* | confirmed |
| 8 | MudBlazor 9.13.0 writes time labels only via `TimeLabelFormat` (a `DateTime` format); `TimeLabelSpacing` sets the step | `MudBlazor.xml` (`TimeSeriesChartOptions`), `TrendChart.razor.cs` lines 25, 205–216 | confirmed |
| 9 | Label steps stop at 3 h with a 3-h fallback | `TrendChart.razor.cs` lines 44, 201–204 | confirmed |
| 10 | `TimeLabelFormat = "''"` renders an empty label without an exception, and the vertical grid lines stay | to verify: Dev, step 6, by `TrendChartGameAxisLabelsCarryNoTime` and the rendered markup; an exception or missing grid lines goes back to the Lead | unverified |
| 11 | The theme and language cookies are read in `App.OnInitialized`, passed through `Routes` as cascades, held and written by `MainLayout` via `censio.storePreference`; a failed store logs `PreferenceNotStored` with the kind only | `App.razor.cs`, `Routes.razor`, `MainLayout.razor.cs` lines 205–245, `UiLog.cs:15` | confirmed |
| 12 | A changed `AddressKey` clears the details and runs `LoadAsync`, which keeps the shown `View` for the same profile and island; a read during a read sets a pending read | `DataPageBase.OnParametersSetAsync`, `LoadAsync`, `ReadOnceAsync` | confirmed |
| 13 | `GoodTrend`/`IslandDetail` drop a read of an older generation and stop reading after a failed read until `ClearDetails` | `GoodTrend.Accept`, `ClearDetails`; `IslandDetail.razor.cs` line 190 | confirmed |
| 14 | Records 0087 and 0088 are unreleased; 0071 is released (v0.3.0) | `git tag --contains` of the commits that added them | confirmed |
| 15 | A region read of a week at the contract's bounds, cold cache, takes about two minutes | to verify: Dev, step 6, estimated from 68–74 s for 2,880 ticks (`web-charts.md` *Limits and errors*), measured by a scratch program as in `history.md` *Measurements*, three runs warm and cold, plus the longest part's hold of the in-memory write gate and the `Hours` times transaction (`GROUP BY` and distinct-hour count over every tick of the range) on `All` under that gate; the report names the commands and quotes their output; above five minutes, or a part or the times transaction above 1 s, back to the Lead | unverified |
| 16 | The game time is milliseconds | to verify: the Product Manager, step 9 on the branch build, compares the tooltip's game time over one played hour with the game's speed; `docs/protocol.md` *Timestamp* assumes it (spec N4) | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrendRangeForStartsAtHourOfRangeBack` (DataRows: hour, day, week, a latest tick at `DateTimeOffset.MinValue` and at `MaxValue`), `TrendRangeForAllStartsAtBeginning` | `tests/Censio.Server.Tests/TrendRangeTests.cs` |
| AC1 | `TrendReaderReadGoodPassesRangeAndResolutionOfChoice`, `TrendReaderReadFiguresPassesRangeAndResolutionOfChoice` | `tests/Censio.Server.Tests/TrendReaderTests.cs` |
| AC2 | `HistoryStoreReadGoodTrendByHoursMeansTicksAndFoldedPoints`, `HistoryStoreReadGoodTrendByHoursEqualsFoldedHour`, `HistoryStoreReadFiguresTrendByHoursUsesFirstEntryPerGood`, `HistoryStoreReadTrendByHoursSplitsLongHourAcrossParts`, `HistoryStorePlanHourPartsHoldsAtMostPartSizeRows` | `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC2 | `TrendWindowExtendReplacesNewestHourAndKeepsOlder`, `TrendWindowExtendDropsHoursBeforeRangeStart`, `TrendWindowExtendOverCapKeepsNewestAndTotal` | `tests/Censio.Server.Tests/TrendWindowTests.cs` |
| AC2 | `TrendReaderWideRangeWithHeldViewReadsFromNewestHour`, `TrendReaderNarrowRangeIgnoresHeldView` | `tests/Censio.Server.Tests/TrendReaderTests.cs` |
| AC2 | `GoodTrendTickOnWeekReadsNewestHourOnly` | `tests/Censio.Server.Tests/GoodTrendTests.cs` |
| AC3 | `HistoryStoreReadTrendGivesGameTimesAndNoEntriesFlag` | `tests/Censio.Server.Data.Tests/HistoryStoreTests.cs` |
| AC3 | `TrendSeriesBuilderGameAxisPlacesPointsInGameOrder`, `TrendSeriesBuilderGameAxisLeavesOutNoEntriesTick`, `TrendSeriesBuilderGameAxisJoinsPauseAndBreaksAfter15GameMinutes`, `TrendSeriesBuilderGameAxisHourlyFromLargestToNextSmallest`, `TrendSeriesBuilderGameAxisKeepsNoValuePointAsBreak`, `TrendSeriesBuilderGameAxisTimeMapsInRangeOnly` (DataRows −62,135,596,800,000 → `DateTime.MinValue`, one less → null, `long.MaxValue` → null), `TrendSeriesBuilderBreaksBeforeInGameTimeWithExtremeFirstGameTimeDoesNotWrap` (`long.MinValue`) | `tests/Censio.Server.Tests/TrendSeriesBuilderTests.cs` |
| AC3 | `PageFormatGameTimeWritesHoursAndMinutes` (DataRows 0 → `0:00`, 1,750,000,000 → `486:06`, 359,999,999,999 → `99999:59`, −90,000 → `-0:01`, −62,135,596,800,000 → `-17259888:00`, `long.MinValue` → `-2562047788015:12`) | `tests/Censio.Server.Tests/PageFormatTests.cs` |
| AC3 | `TrendChartGameAxisTooltipShowsRealTimeThenGameTime`, `TrendChartGameAxisLabelsCarryNoTime`, `TrendChartAxisChangeRedrawsSameRead` (labels change from times to none, same read object) | `tests/Censio.Server.Tests/TrendChartTests.cs` |
| AC4 | `GoodTrendRangeChangeRereadsBothChartsWithRange`, `GoodTrendAxisChangeRedrawsWithoutRead` (no call on `FakeHistoryTrends`), `GoodTrendRangeChangeCancelsEarlierRead` (the earlier read's token is cancelled, its result not shown), `GoodTrendQuickChoicesShowOnlyLast`, `GoodTrendChoiceAfterFailedReadReadsAgain` | `tests/Censio.Server.Tests/GoodTrendTests.cs` |
| AC4 | `IslandDetailRangeChangeRereadsOverview`, `IslandDetailAxisChangeRedrawsWithoutRead` | `tests/Censio.Server.Tests/IslandDetailTests.cs` |
| AC4 | `TrendControlsClickInvokesChooseChartWithChoice` | `tests/Censio.Server.Tests/TrendControlsTests.cs` |
| AC5 | `UiPreferencesReadChartChoiceKnownValues`, `UiPreferencesReadChartChoiceUnknownValueGivesDefault` (DataRows: `Week`, `%3Cb%3E`, `week\u0001`, 2,000 × `a`, null), `UiPreferencesStoredValueOfChartChoiceRoundTrips` | `tests/Censio.Server.Tests/UiPreferencesTests.cs` |
| AC5 | `MainLayoutChartChoiceStoresBothCookies`, `MainLayoutChartChoiceWhenStoreFailsKeepsChoiceAndLogsKindOnly`, `MainLayoutChartChoiceCascadesToPage` | `tests/Censio.Server.Tests/MainLayoutTests.cs` |
| AC5 | `AppPassesStoredChartChoiceOfRequest` | `tests/Censio.Server.Tests/AppTests.cs` |
| AC6 | `TrendControlsShowRangesAxesAndRangeLine` (DataRows en/de per range, `All` → `All history up to the latest tick`) | `tests/Censio.Server.Tests/TrendControlsTests.cs` |
| AC6 | `UiTextsChartControlTextsInBothLanguages` | `tests/Censio.Server.Tests/UiTextsTests.cs` |
| AC7 | `TrendSeriesBuilderLabelSpacingGivesSmallestStepOfAtMostEightIntervals` (DataRows: 1 h → 10 min, 25 h → 6 h, 169 h → 1 day, 2,880 h → 30 days, 400 days → 90 days, 5,000 days → 365 days), `TrendSeriesBuilderTimeLabelFormatDependsOnAxisAndSpan` | `tests/Censio.Server.Tests/TrendSeriesBuilderTests.cs` |
| AC7 | `TrendChartWeekSpanShowsDateLabels` | `tests/Censio.Server.Tests/TrendChartTests.cs` |

`manual:` the Product Manager, on a build of the branch with `long-session.jsonl` replayed: the picker, the game-time axis (pause joined, tooltip) and the week view in both themes (step 9, user guide's *Not yet tried*).

## Approach

**Choice.** `ChartChoice` (range, axis) is read from two cookies in `App.OnInitialized` (`UiPreferences.ReadChartChoice`, values `hour`/`day`/`week`/`all` and `real`/`game`, compared whole and ordinal, anything else the default `day`/`real`), passed to `Routes` and cascaded; `MainLayout` takes it as `InitialChartChoice`, holds the current choice and cascades it to the body together with an `EventCallback<ChartChoice>` named `ChooseChart`. Choosing stores both cookies through `censio.storePreference` (`StoreAsync` with the kinds `chart range` and `time axis`, so the Debug line reads `The browser did not store the chart range choice.`). `DataPageBase` takes the cascaded `ChartChoice`; `GoodTrend` and `IslandDetail` add only its range to `AddressKey`, so a range change clears the details (`Loading…` in every chart), keeps the page view, and reads the charts again; the generation counter drops a read of an earlier range and `LoadAsync`'s pending read gives the last one (E3), and `ClearDetails` resets the failure (E4). The axis goes to `TrendChart.Axis` only: an axis change redraws each chart from the read it holds, as a language change does, with no read. **Cancelling.** `DataPageBase` keeps a details `CancellationTokenSource` linked to the page's token; `OnParametersSetAsync` cancels and replaces it before `ClearDetails`, and `ReadOnceAsync` passes its token to `ReadDetailsAsync` and catches the `OperationCanceledException` of a cancelled details token (no log, nothing shown), so an earlier range's read stops at its next part and frees the trend gate.

**Controls (layout).** `TrendControls` replaces the range line where it is today (good page: above both charts; island page: under the heading `Overview`): one row (`div.censio-chart-controls`, flex with a 16 px gap, wrapping) holding first a `MudToggleGroup` of the four ranges (accessible name `Period` / `Zeitraum`), then one of the two axes (`Time axis` / `Zeitachse`), single selection, the current choice selected; under the row a `<p>` with the range line of AC6. No other text.

**Reads.** `TrendRange.For(range, latestTick)` gives the start of the UTC hour that holds the latest tick minus 1 h, 24 h or 7 days, `All` `DateTimeOffset.MinValue`, end latest tick + 1 tick; the start is clamped at `DateTimeOffset.MinValue.UtcTicks` and the end at `DateTimeOffset.MaxValue.UtcTicks`, so no receive time throws. `TrendReader` takes a `TrendRequest` (latest tick, choice, held view). Hour and day query `HistoryTrendResolution.Points`, week and all `Hours`. For a wide range with a held view of at least one point, it reads from the hour of the held view's last point to the end and merges with `TrendWindow.Extend` (held points before that hour and from the range start, then the new ones; total = held total − replaced − dropped + new total; over 2,880 the oldest leave, the total stays). Pages pass their current view as held; `ClearDetails` nulls it, so a choice or address change reads whole.

**History, `Hours`.** The times transaction reads, from the indexes only, the hourly points of the range (hour, number) and the tick count per UTC hour (`GROUP BY` of `ReceiveTicks` floored to the hour), newest 2,880 hours, and the count of distinct hours (`UNION` of both). `PlanHourParts` cuts the hours, oldest first, into parts of at most `TrendPartSize` rows (an hourly point counts one, a tick one); an hour with more ticks continues in the next part. Each part, one read transaction under the trend gate, selects its ticks by hour range and keyset on `(ReceiveTicks, Number)` with `LIMIT`, their game time and whether any island holds an entry, then the values with the existing statements (good) or a statement of the island's entries with good and position (figures, first entry per good, as the fold). It accumulates per hour and island the sums and counts of the three floats in C# (a folded point adds its sums and counts), and for figures per good the entry count, building-count sum and delta sum and count; then forms the hour's point exactly as the hourly point after the fold would read. An hour's game time is the largest, its first game time the smallest over ticks with an entry and the folded point when it has entries; `HasNoEntries` when neither has one. A fold or deletion between parts behaves as `history.md` says today. Memory: the hours list (≤ 2,880) and the accumulators of one part's hours.

**History, `Points`.** Unchanged, plus per part the game times and the entry flag (`Ticks.GameTime`, `HourlyPoints.FirstGameTime`/`LastGameTime`, an `EXISTS` over the tick's or point's islands and entries), so the times transaction reads no more than today.

**Drawing.** `TrendSeriesBuilder.Build(view, names, offset, axis)`: on `RealTime` as today. On `GameTime`, points with `HasNoEntries` or a game time outside `DateTime` are left out, the rest ordered by game time (stable), placed at `GameAxisTime(gameTime)` (`DateTime.UnixEpoch` + milliseconds); a break where a value is missing or the mapped `GameAxisTime(later.FirstGameTime)` (the later point's own mapped game time when that is null or outside `DateTime`) lies more than 15 minutes after the earlier point's mapped game time: the rule compares `DateTime`s, never subtracts raw game times. `GameAxisTime` maps −62,135,596,800,000 ms (`DateTime.MinValue`) up to `DateTime.MaxValue`, null outside. `LabelSpacing` and `TimeLabelFormat` move from `TrendChart` to `TrendSeriesBuilder` (AC7 list; format `HH\:mm`, `MM'-'dd HH\:mm` over a day, `''` on game time). `TrendChart` gets `Axis`, keys its points by the axis's time, and the tooltip shows the real time line as today, `Hourly average` when hourly, then `Game time <h>:<mm>` (`PageFormat.GameTime`, invariant digits, hours unbounded, minutes two digits, a negative game time with a leading `-` and its magnitude truncated toward zero, `long.MinValue` included) on game time, then the values. Equal game times: the first point keeps the tooltip. `TrendChart`'s options key holds the axis and the label format besides kind, theme, language and spacing.

## Options considered

- Hourly means computed in SQL with `SUM`/`AVG` — lost: floats never go through SQL (`history.md`); chosen: C# accumulation in parts.
- Parts of 240 hours instead of 240 scanned rows — lost: up to 7,200 ticks per transaction under the in-memory write gate (Challenge MAJOR 3).
- A separate re-read path in `DataPageBase` for a choice — lost against the choice in `AddressKey`, which reuses the pending-read and generation logic.
- Ranges, the live update of wide ranges, one cookie per choice and the game-time labels: [record 0089](../../docs/decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md), [record 0090](../../docs/decisions/0090-game-time-axis-places-points-without-time-labels.md).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `HistoryTrendResolution` (new), `HistoryGoodTrendQuery`, `HistoryFiguresTrendQuery`, `HistoryGoodPoint`, `HistoryFiguresPoint`, `TrendHourSlice` (new, internal), `HistoryStore` | resolution, game times, flag, hour reads |
| Censio.Server | `ChartRange`, `ChartAxis`, `ChartChoice`, `TrendRequest`, `TrendWindow` (new); `TrendRange`, `TrendPoint`, `TrendReader`, `TrendSeriesBuilder`, `PageFormat`, `UiPreferences`, `UiTexts`, `UiLog` (XML doc of the kinds) | choice, reads, drawing, texts |
| Censio.Server | `Components/TrendControls.razor(.cs)` (new), `TrendChart.razor.cs`, `App.razor(.cs)`, `Routes.razor(.cs)`, `Layout/MainLayout.razor(.cs)`, `Pages/DataPageBase.cs`, `Pages/GoodTrend.razor(.cs)`, `Pages/IslandDetail.razor(.cs)`, `wwwroot/css/censio.css` | wiring and layout |

## Signatures (for the Dev's skeleton)

Properties are written as type and member, with their C# type in prose; every property has get and init accessors unless said.

Censio.Server.Data:
- enum `HistoryTrendResolution` with the members Points and Hours
- `HistoryGoodTrendQuery.Resolution` and `HistoryFiguresTrendQuery.Resolution`: HistoryTrendResolution, not required (default Points)
- `HistoryGoodPoint.GameTime` and `HistoryFiguresPoint.GameTime`: long?; `HistoryGoodPoint.FirstGameTime` and `HistoryFiguresPoint.FirstGameTime`: long?; `HistoryGoodPoint.HasNoEntries` and `HistoryFiguresPoint.HasNoEntries`: bool; none required
- `internal readonly record struct TrendHourSlice(long HourTicks, int Ticks, bool WithHourly);`
- `internal static List<List<TrendHourSlice>> PlanHourParts(IReadOnlyList<(long HourTicks, int Ticks, bool WithHourly)> hours, int partSize)` on HistoryStore

Censio.Server:
- enum `ChartRange` with LastHour, LastDay, LastWeek, All; enum `ChartAxis` with RealTime, GameTime
- sealed record `ChartChoice` with `ChartChoice.Range` (ChartRange, required), `ChartChoice.Axis` (ChartAxis, required) and the static get-only `ChartChoice.Default` (LastDay, RealTime)
- sealed class `TrendRequest` with `TrendRequest.LatestTick` (DateTimeOffset), `TrendRequest.Choice` (ChartChoice), `TrendRequest.Held` (TrendView?), all required
- `public static TrendRange For(ChartRange range, DateTimeOffset latestTick)` and `public static HistoryTrendResolution ResolutionOf(ChartRange range)` on TrendRange
- `TrendPoint.GameTime` (long?), `TrendPoint.FirstGameTime` (long?), `TrendPoint.HasNoEntries` (bool), none required
- `public Task<TrendRead> ReadGoodAsync(Guid profileId, IReadOnlyList<IslandKey> islands, int productGuid, TrendRequest request, CancellationToken cancellationToken)` and `public Task<TrendRead> ReadFiguresAsync(Guid profileId, IslandKey island, TrendRequest request, CancellationToken cancellationToken)` on TrendReader, replacing the overloads that take the latest tick
- static class `TrendWindow` with `public static TrendView Extend(TrendView held, TrendView newest, TrendRange range)`
- on TrendSeriesBuilder: `public static IReadOnlyList<ChartSeries<double>> Build(TrendView view, IReadOnlyList<string> lineNames, int? offsetMinutes, ChartAxis axis)` (replacing the three-parameter one), `public static DateTime? GameAxisTime(long gameTime)`, `public static bool BreaksBeforeInGameTime(TrendPoint earlier, TrendPoint later)`, `public static TimeSpan LabelSpacing(TimeSpan span)`, `public static string TimeLabelFormat(ChartAxis axis, TimeSpan span)`
- `public static string GameTime(long milliseconds)` on PageFormat
- on UiPreferences: the constants `UiPreferences.ChartRangeCookieName` (censio-chart-range) and `UiPreferences.ChartAxisCookieName` (censio-chart-axis), `public static ChartChoice ReadChartChoice(string? storedRange, string? storedAxis)`, `public static string StoredValue(ChartRange range)`, `public static string StoredValue(ChartAxis axis)`
- on UiTexts, each a required string: `UiTexts.ChartRangeFormat`, `UiTexts.RangeLastHour`, `UiTexts.RangeLastDay`, `UiTexts.RangeLastWeek`, `UiTexts.RangeAll`, `UiTexts.RangeAllHistory`, `UiTexts.AxisRealTime`, `UiTexts.AxisGameTime`, `UiTexts.ChartRangeLabel`, `UiTexts.ChartAxisLabel`, `UiTexts.GameTimeFormat`
- sealed partial class `TrendControls` with the cascading parameters `TrendControls.Choice` (ChartChoice), `TrendControls.ChooseChart` (EventCallback of ChartChoice, cascade name ChooseChart) and `TrendControls.Texts` (UiTexts)
- `TrendChart.Axis` (ChartAxis, parameter); `Routes.ChartChoice` (ChartChoice, parameter); `MainLayout.InitialChartChoice` (ChartChoice, cascading); `DataPageBase.ChartChoice` (ChartChoice, cascading); each with get and set and the default ChartChoice.Default
- Deleted: `TrendRange.Before`
- Deleted: `UiTexts.ChartRange`

Files the skeleton rewrites: `TrendRange.cs`, `TrendReader.cs`, `TrendSeriesBuilder.cs`, `UiTexts.cs` (the deleted property and its two uses in `GoodTrend.razor`, `IslandDetail.razor`).

## Test files

The files of *Tests per criterion*; new: `tests/Censio.Server.Tests/TrendWindowTests.cs`, `tests/Censio.Server.Tests/TrendControlsTests.cs`.

Existing call sites, adapted by the Tester: `TrendRangeTests` (`Before`), `TrendReaderTests` (12 calls of `ReadGoodAsync`/`ReadFiguresAsync`), `TrendSeriesBuilderTests` (7 calls of `Build`), `UiTextsTests` line 135 (`ChartRange` row replaced), `GoodTrendTests.GoodTrendShowsHeadingLinksAndChartHeadings` and the island page's range-line assertion (the line now under the controls), `PageTestHost` (cascades `ChartChoice` and `ChooseChart`). Kept unchanged: the other page, chart and history tests run on the default `day`/`real`, which reads as today.

Files no unit test can cover: none (`censio.js` is unchanged; the CSS rule is layout).

## Areas

- `web-charts.md`: *Where the charts are* (controls and range line replace the fixed line), *Range* (four ranges, starts, `All`), *Points, gaps and hourly means* (hourly resolution of the wide ranges), new *Game-time axis* (placement, left-out tick, hourly at its largest, gap rule, order, no labels), *States of a chart* and *Live updates* (choice, newest-hour update, E10), *Tooltip, axes and times* (formats, step list, E9), *Markup* (empty game labels), *The reads* (resolution, parts by scanned rows), *Limits and errors* (measured times of fact 15, the `Hours` times transaction included; a wide read scans every tick of its range, so its time grows with the profile's tick rate, not with the cap), *Game-time axis* also: a folded hour's smallest game time can be the load tick's, so on `All` such an hour joins its predecessor (record 0090), *Related decisions* (0089, 0090), *Not here* without #37.
- `web-ui.md`: *Scope* ("the two cookies" → the cookies of the choices), *Stored choices* (two rows `censio-chart-range`, `censio-chart-axis` with values; "reads both cookies" → "the cookies"), *Limits and errors* (the Debug line's kinds), *Related decisions* (0071's line).
- `history.md`: *Scope* (#37 now calls the reads), *The trend reads* (the resolution `Hours`, game times and the entry flag, parts by scanned rows), *Not here* (drop the range picker).
- `web-pages.md`: line 72 (the good page's range follows the choice), line 283 (drop "a range picker").
- `docs/areas/README.md`: the rows of web-ui (cookies) and web-charts (ranges, game-time axis).

## Documentation updates

- `README.md`: English line 16 (the picker, the switch, the choice kept), line 22 (the limitation sentence on #37 drops), German lines 198 and 204 likewise.
- `docs/USER_GUIDE.md`: English line 439 (four cookies with their values), line 442 (*Charts*: picker, ranges, hourly means of the wide ranges, game-time axis with its tooltip, no labels, left-out load tick, daylight saving over a week), line 445 (*Not yet tried*: the game-time axis with real data); German lines 916, 919, 922 likewise.
- `docs/ARCHITECTURE.md` line 75 already names the preference cookies as untrusted input: no change.
- All by the Dev, against the code as built.

## Architecture check

No guarantee of `.squad/project.md` changes: no route, setting, listener or stored data; the history is only read. The preference cookies stay input compared whole against constants (record 0071), never echoed or logged. The chart reads keep their own gate, the write-gate bound per part and the 2,880-point cap.

## Security considerations

- [x] Limits: the cookie values are compared whole after ASP.NET Core's percent-decoding, so an overlong or decoded value is simply no match; the range end and 2,880-hour cap bound the hour reads; a part holds at most 240 scanned rows.
- [x] Exceptions: a failed read shows the existing reload text and logs the existing warning without arguments; a failed cookie store logs the Debug line with the kind only. No exception text reaches the user.

## Decision records

- `docs/decisions/0089-chart-ranges-wide-ones-as-hourly-means-read-at-request.md` (Proposed)
- `docs/decisions/0090-game-time-axis-places-points-without-time-labels.md` (Proposed)

## Out of scope / follow-ups

As the spec's *Out of scope*.

## Challenge

- MAJOR 1 (an axis switch re-reads) — accepted: only the range enters `AddressKey`; the axis reaches `TrendChart.Axis`, which redraws from the held read (*Approach*, *Choice*); tests split into range and axis cases plus `TrendChartAxisChangeRedrawsSameRead`.
- MAJOR 2 (an earlier read is never cancelled) — accepted: a details token, cancelled and replaced on a range or address change, with the cancellation caught in `ReadOnceAsync` (*Approach*, *Cancelling*); test `GoodTrendRangeChangeCancelsEarlierRead`.
- MINOR 3 (options key) — accepted: the key holds axis and format; covered by `TrendChartAxisChangeRedrawsSameRead`.
- MINOR 4 (a folded hour's smallest game time) — accepted: the rule stays, the limit is stated in `web-charts.md` *Game-time axis* (*Areas*); no test asserts a break there.

## Security review

Verdict APPROVED; the four non-blocking findings are taken now:

1. `TrendRange.For` throwing near the `DateTimeOffset` ends — fixed: start and end clamped (*Approach*, *Reads*); DataRows at both bounds.
2. The game-time gap subtraction wrapping — fixed: the break compares mapped `DateTime`s, with the fallback for an unmappable first game time (*Approach*, *Drawing*); the negative format of `PageFormat.GameTime` is stated, with negative, lower-bound and `long.MinValue` DataRows, and `GameAxisTime` gets its bounds test.
3. The `Hours` times transaction on `All` — added to fact 15 and to *Limits and errors* (*Areas*).
4. A wide read scans every tick of its range — stated in *Limits and errors* (*Areas*).

For the Reviewer (no Security diff review in tier `standard`): the Dev's fact-15 report names the commands it ran and quotes their output; a figure without them is a blocking finding.

## Security notes

Security delta confirmation, feature #37, mode `plan`. I reviewed revision `6b28840` of `/home/user/Censio/specs/feature-37-range-picker-time-axis/plan.md` against `833d11a`.

**Verdict:** the revision adds no new security issue. All four of my earlier findings are resolved. There is one new non-blocking finding.

**My earlier four findings:**
1. **`TrendRange.For` throwing at the time limits.** Resolved. The start is clamped at `DateTimeOffset.MinValue.UtcTicks` and the end at `MaxValue.UtcTicks`, and there are DataRows at both bounds (*Reads*; AC1 row).
2. **Game-time gap rule wrapping on extreme values.** Resolved. The rule now compares mapped `DateTime`s and never subtracts raw game times, with a fallback when the first game time cannot be mapped.
   - `GameAxisTime` has bounds rows: −62,135,596,800,000, one less, and `long.MaxValue`.
   - There is a test for a `long.MinValue` first game time.
   - The negative format of `PageFormat.GameTime` is stated, with rows for −90,000, the lower bound and `long.MinValue`.
3. **Fact 15 did not cover the `Hours` times transaction.** Resolved. It now measures that transaction on `All` under the write gate, sets a 1 s threshold, and requires the report to name the commands and quote their output.
4. **A wide read scans every tick.** Resolved. It is stated for `web-charts.md` *Limits and errors* under *Areas*.

**The new redraw path (axis change).** No issue. An axis change only sets `TrendChart.Axis` and redraws from the read the page already holds. It does no database read, takes no gate, and writes no new log line. It shows no new data: the cookie value is parsed into an enum before it is used, and only the range goes into `AddressKey`. Memory stays the same as today, one held view of at most 2,880 points per chart.

**Cancellation.**
- **Gates are released.** Both gates already release on cancellation:
  - `TrendReader.ReadAsync` (`/home/user/Censio/src/Censio.Server/TrendReader.cs:140-167`) waits for the gate outside its `try`, rethrows `OperationCanceledException` and releases in `finally`.
  - `HistoryStore.ReadTrendTransactionAsync` (`/home/user/Censio/src/Censio.Server.Data/HistoryStore.cs:877-898`) does the same with `TrendGate`.
  - A cancelled read therefore never leaves the trend gate held, and it does not reach `ChartReadFailed`, so no warning is logged.
- **"Stops at its next part" holds.** I ran a scratch program (`/tmp/claude-0/-home-user-Censio/cfc462e1-2c3e-5a4c-b263-c5322e2f56b7/scratchpad/sqlcancel/Program.cs`) against Microsoft.Data.Sqlite 11.0.0, the repository's version, with SQLitePCLRaw 3.0.5. It runs a recursive-CTE `count(*)` up to 300,000,000 through `ExecuteScalarAsync` with a token set to cancel after 300 ms. Command: `dotnet build -o out && timeout 120 dotnet out/sqlcancel.dll`. Output: `completed after 53793 ms`.
  - The library does not interrupt a statement that is already running when the token is cancelled, so there is no `SqliteException` (interrupt) that `TrendReader` would log as a failed chart read.
  - The current part runs to the end, which fact 15 bounds at 1 s or less, and the cancellation takes effect at the next part's wait for the gate.
- **Not run:** there is no detail-read cancellation code yet, so the page-level path is checked only by reading the plan. `GoodTrendRangeChangeCancelsEarlierRead` is the test that pins it.

**New finding [NON-BLOCKING]: the plan does not say how the details `CancellationTokenSource` is replaced or disposed.**
- **Where:** plan, *Approach*, *Cancelling*: "`OnParametersSetAsync` cancels and replaces it before `ClearDetails`, and `ReadOnceAsync` passes its token to `ReadDetailsAsync`".
- **Risk:** `/home/user/Censio/src/Censio.Server/Components/Pages/DataPageBase.cs:21-23` already reads the page token once "so that no notice reads a disposed source". A details source that is disposed while a state-change notice or the pending loop still reads `.Token` from it throws `ObjectDisposedException` in the page. A source that is never disposed leaves one registration on the page token per range change until the page closes.
- **Smallest fix:** add to *Cancelling*: "the old source is cancelled, then disposed; `ReadOnceAsync` reads the details token once at its start into a local and filters its catch on that local; `Dispose` cancels and disposes the current details source."
- **Recommendation:** fix now; it is one sentence in the plan.
