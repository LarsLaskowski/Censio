# Plan: Diagnostics window for the last tick

Source: Issue #14 | [spec.md](spec.md)
Status: Draft
Tier: standard — a new behavior (a window, a menu entry, the latest tick kept whole) that creates, changes or removes no security control; the island-name sanitizing reuses the log's existing rule unchanged.
Security areas touched: Parsing of external input and Logging of external data (`.squad/project.md`): pipe content (island names, GUIDs, values) is shown on screen for the first time, with names passed through the existing `LogLine.Sanitize`, unchanged; nothing is logged or written

## Problem / root cause

Feature: see [spec.md](spec.md) (*Problem*); criteria AC1–AC12 live there.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Closing a WPF window does not end the Companion | `src/Censio.Companion/App.xaml:4` `ShutdownMode="OnExplicitShutdown"`; only `Dispatch(Exit)` calls `Shutdown(0)` (`App.xaml.cs`) | confirmed |
| 2 | `TickReceived` handlers run synchronously on the reading thread | `PipeConnection.cs:397-420` and `PipeReplayRunner.PublishTick` call each handler in a loop | confirmed |
| 3 | Connection and replay keep only a summary of the latest tick today | `PipeConnection.cs:401`, `PipeReplayRunner.cs` `PublishTick`: `_lastTick = PipeTickSummary.From(tick)` under `_statusLock` | confirmed |
| 4 | A tick's islands and an island's entries are in frame order | `PipeTickBuilder.cs:238` `_islands.Add`, `:271` `Islands = _islands.ToArray()`; `pipe.md` *Message types* "Entries are delivered in frame order" | confirmed |
| 5 | `TrayStatusUpdater<TStatus>` posts at most one pending update and reads the state when that update runs | `TrayStatusUpdater{TStatus}.cs` `Notify` (`Interlocked.Exchange`) and `ShowCurrent` | confirmed |
| 6 | `LogLine.Sanitize(text, int.MaxValue)` replaces control, format, line and paragraph separator characters and invalid UTF-16 by one `?` each and never cuts | `LogLine.cs:72-104`, `IsReplaced` `:129-135`; already called with `int.MaxValue` at `LogLine.cs:58`; `LogLine` is `public static` | confirmed |
| 7 | The midnight timer refreshes the tray through `updater.Notify()` | `App.xaml.cs` `CreateUpdater` (timer callback calls `updater.Notify()` and `ArmMidnightTimer()`) | confirmed |
| 8 | Menu clicks reach `Dispatch` only for kinds listed in `TrayIcon.BuildItems` | `TrayIcon.cs` `BuildItems`: `if (kind is ToggleRecording or OpenRecordingsFolder or OpenLogFolder or Exit)` | confirmed |
| 9 | A replay reproduces the live ticks with their receive times | `pipe.md` *Replay*; `PipeReplayRunnerTests.PipeReplayRunnerTickSetsLastTickWithRecordedTime` | confirmed |
| 10 | `float.ToString(CultureInfo.InvariantCulture)` on .NET 10 gives the shortest round-trip text, `-0` for negative zero, `NaN`, `Infinity`, `-Infinity`, `1E+38` | to verify: the AC2 float tests on the real runtime, Tester, step 5 (the .NET Core 3.0 formatting change documents it); a failing case is reported and the Lead decides the text | unverified |
| 11 | A WPF `DataGrid` with row virtualization, no sorting or grouping, over a non-generic `IList` asks only for the rows on screen by index (does not enumerate the list or call `IndexOf` per row) | to verify: Dev reads `dotnet/wpf` `release/10.0` `ListCollectionView` (constructor, `InternalList`) and `ItemContainerGenerator` in step 6 and reports it; final check in the maintainer's manual run (AC10 manual part) | unverified |
| 12 | `Application.Shutdown` closes the open windows, and `Exit` stays within 2 seconds with the window open | to verify: maintainer's manual run on Windows (AC6); the WPF documentation of `Application.Shutdown` says it closes the windows | unverified |
| 13 | The shell is outside every coverage measure | `.github/workflows/ci.yml:71` `sonar.coverage.exclusions=…src/Censio.Companion/**`; no test project loads it (record 0004) | confirmed |
| 14 | `TrayMenuTests.TrayMenuCreateListsEntriesInOrder` asserts the full entry list; the other `TrayMenu.Create` tests pick entries with `Single` | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs:56-95`, `:101-160` | confirmed |
| 15 | No real recording fixture exists, and none may be committed raw | `tests/` holds none; record 0029 | confirmed |

Issue claims: "#13 merged" confirmed (50af0d4); "before names and icons exist (M3)" confirmed (#21–#26 open). No claim refuted. Related defect found: none.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrayMenuCreateListsEntriesInOrder` (adapted: `OpenDiagnostics` / `Open diagnostics` / enabled right after the first separator), `TrayMenuCreateOpenDiagnosticsEnabledInEveryMode` (live, recording, replay, invalid snapshot) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC2 | `DiagnosticsViewColumnsInOrder`, `DiagnosticsViewCreateHeaderShowsStatusLastTickAndGameTime`, `DiagnosticsViewCreateNoDataYetAppendsMarker`, `DiagnosticsViewRowsFollowIslandAndEntryOrder`, `DiagnosticsViewIslandWithoutEntriesHasNoEntriesRow`, `DiagnosticsViewFloatCellsUseShortestInvariantText` (cases `0.1`, `-2.5`, `1E+38`, `-0`, `NaN`, `Infinity`, `-Infinity`), `DiagnosticsViewIntegerCellsShowNegativeValues`, `DiagnosticsViewNumbersIgnoreGermanCulture` | `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs` |
| AC3 | `DiagnosticsViewReplayedRecordingShowsFrameValues` (speed `max` and `1`), `DiagnosticsViewLiveConnectionEqualsReplay` | `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs` |
| AC4 | `DiagnosticsWindowSessionNotifyShowsLatestTick`, `DiagnosticsWindowSessionNotifyShowsNewStatusText`, `DiagnosticsWindowSessionNotifyCoalescesToOnePendingUpdate`, `DiagnosticsWindowSessionNotifyAfterMidnightShowsDatedLastTick` | `tests/Censio.Companion.Core.Tests/DiagnosticsWindowSessionTests.cs` |
| AC5 | `PipeConnectionLatestTickHoldsLastTickWhole`, `PipeReplayRunnerLatestTickHoldsLastTickWhole`, `DiagnosticsWindowSessionClosedShowsNothingOnNotify`, `DiagnosticsWindowSessionReopenShowsTickClosedWhileClosed` | `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs`, `PipeReplayRunnerTests.cs`, `DiagnosticsWindowSessionTests.cs` |
| AC7 | `DiagnosticsViewCreateWithoutTickShowsNoneAndNoRows`, `DiagnosticsViewCreateInvalidCommandLineShowsStatus`, `PipeConnectionLatestTickStaysAfterConnectionEnds`, `PipeReplayRunnerLatestTickStaysAfterReplayError` | `DiagnosticsViewTests.cs`, `PipeConnectionTests.cs`, `PipeReplayRunnerTests.cs` (all in `tests/Censio.Companion.Core.Tests/`) |
| AC8 | `DiagnosticsWindowSessionOpenTwiceBringsWindowToFront`, `DiagnosticsWindowSessionOpenAfterCloseCreatesNewWindow` | `tests/Censio.Companion.Core.Tests/DiagnosticsWindowSessionTests.cs` |
| AC9 | `DiagnosticsViewNameReplacesControlAndFormatCharacters` (cases U+0007, U+202E, U+200B, U+2028, U+2029, a lone U+D800), `DiagnosticsViewNameOfMaximumLengthIsNotCut`, `DiagnosticsViewNameKeepsSpaces` | `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs` |
| AC10 | `DiagnosticsViewCreateAtTickLimitsMakesNoRowText`, `DiagnosticsViewIndexOfDoesNotScan`, `DiagnosticsWindowSessionClosedHoldsNoView` | `DiagnosticsViewTests.cs`, `DiagnosticsWindowSessionTests.cs` |
| AC12 | `DiagnosticsWindowSessionStalledWindowLeavesReplayUnchanged`, `DiagnosticsWindowSessionStalledWindowLeavesLiveConnectionUnchanged` | `tests/Censio.Companion.Core.Tests/DiagnosticsWindowSessionTests.cs` |

Test rules for the Tester:

- Frames are synthetic, built with `PipeFrameBuilder` and `PipeTestFrames` (a new helper for an island with given
  entries may be added to `PipeTestFrames`); recordings with `PipeTestFrames.Recording`. No real recording (fact 15).
- AC3 compares the header lines without `Status:` and every cell of every row against literal expected strings
  derived from the values written into the frames — not against the view's own output of another run only.
  `DiagnosticsViewLiveConnectionEqualsReplay` feeds the same frames through `PipeConnection` (`ScriptedPipeConnector`,
  `ControlledPipeStream`, the recorded receive times through `RecordingTimeProvider` or `FakeTimeProvider`) and
  through `PipeReplayRunner`, ends both streams so the last tick closes, and compares the two views.
- The `-0` case builds its input with `BitConverter.Int32BitsToSingle(unchecked((int)0x80000000))` and asserts
  `float.IsNegative(input)` first, so a folded `0` fails the test instead of passing it.
- The AC9 cases build each name from its code point with `char.ConvertFromUtf32` (no escape in a string literal)
  and assert the input's code unit before acting (*Escapes in files*, `.squad/team.md`); the lone surrogate is a `char` of
  `0xD800`. The 255-byte case is 255 ASCII characters with spaces at both ends, compared whole.
- `DiagnosticsViewNumbersIgnoreGermanCulture` sets `CultureInfo.CurrentCulture` and `CurrentUICulture` to `de-DE`
  and restores them in `finally`.
- AC10 builds a tick at the limits cheaply: 256 islands, each with an array of 2,048 references to one shared
  `ProductionEntry` (no maps). `DiagnosticsViewCreateAtTickLimitsMakesNoRowText` asserts `Count` 524,288, that
  `DiagnosticsView.Create` allocates less than 1 MiB (`GC.GetAllocatedBytesForCurrentThread` around the call), and
  that rows 0, 2,048 and 524,287 carry the expected island and good cells. `DiagnosticsViewIndexOfDoesNotScan`
  asserts `IndexOf` of row 524,287 returns 524,287 with less than 64 KiB allocated.
- AC12: the window consumer is stalled by a `post` that queues the action and never runs it. The test asserts that
  `readStatusText` and `readTick` were never called on the reading thread (call counts 0 while stalled), that at most
  one action was queued, and that the ticks (game time, receive time, island count), the status sequence and — for
  the live test — the text of a `PipeRecordingSession` sink (`ControlledWriteStream`) equal those of the same run
  without a session.
- AC4 midnight: `FakeTimeProvider` with a fixed local zone; a tick received at 23:59:00 shows `HH:mm:ss`; after
  `Advance` past midnight and `Notify`, the shown `Last tick:` line has the date.
- A new hand-written fake `FakeDiagnosticsWindow` (`tests/Censio.Companion.Core.Tests/FakeDiagnosticsWindow.cs`)
  implements `IDiagnosticsWindow`: it records each shown view and each `BringToFront` call and raises `WindowClosed`
  on `Close()`. It models no real component. The Tester adds it to `.squad/project.md` *Test doubles*.

## Verification without tests

The change has production and test code; these criteria, or parts of them, are verified without a unit test:

- AC6 (`Exit` with the window open, 2 seconds, exit code 0): the maintainer's manual run on Windows after the PR
  opens (fact 12); listed under "Not verified yet" in `companion.md` until then.
- AC5, AC8, AC10 manual parts (close button and Alt+F4 close only the window; bring to front restored from
  minimized; a tick at the limits scrolls, the tray stays responsive, the scroll position is kept): the same manual
  run, the same list. AC10 also rests on fact 11, which the Dev reads in step 6.
- AC11 (README and user guide, both languages): the Reviewer in step 8 against *Documentation updates*.

## Approach

**Kept tick.** `PipeConnection` and `PipeReplayRunner` store the closed `PipeTick` itself next to the summary, in
`PublishTick` under `_statusLock`, and expose it as `LatestTick`. It is replaced by the next tick only, kept across
connections, and never cleared.

**Window content (`DiagnosticsView`, Core).** Built by `DiagnosticsView.Create(statusText, tick, clock)` on the UI
thread. Layout is the spec's *Window content*, fixed:

- `HeaderLines`: `Status: <statusText>`; `TrayMenu.FormatLastTick(tick is null ? null : PipeTickSummary.From(tick), clock)`;
  with a tick, `Game time: <GameTime>` plus ` (no data yet)` when `tick.IsNoDataYet`.
- `Columns`: the 17 headers of the spec in order.
- Rows: `Create` stores the tick and an `int[]` of each island's first row index (an island without entries counts
  one row), nothing per row. `this[index]` finds the island by binary search over that array and formats that one
  row: region `Key.SessionGuid`, island `Key.IslandId`, area `Key.AreaIndex`, `SessionId`, name
  `LogLine.Sanitize(Name, int.MaxValue)`, good `ProductGuid`, then `Generation`, `Consumption`, `Delta`,
  `PerfectGeneration`, `PerfectConsumption`, `BuildingCount`, `Maintenance`, `Income`, `Profit`,
  `SummedProductivity`, `AverageProductivity`. The empty island's row has `(no entries)` and 11 empty strings.
  Integers with `ToString(CultureInfo.InvariantCulture)`, floats with `ToString(CultureInfo.InvariantCulture)` (fact 10).
- The view is a read-only, fixed-size `IList`: `IndexOf(row)` returns `row.Index` when that index is in range and
  `this[row.Index]` has the same cells, otherwise -1 — it never scans. Mutating members throw `NotSupportedException`.

**Open, update, close (`DiagnosticsWindowSession`, Core).** One per run, created in `App.OnStartup` for every mode
with `readStatusText` (`() => _snapshot().StatusText`), `readTick` (`connection.LatestTick`, `runner.LatestTick`, or
`() => null` for an invalid command line), `TimeProvider.System`, the dispatcher's `BeginInvoke` at `Background`
priority as `post`, and a factory of the WPF window.

- `Open()` (UI thread): with a window open, `BringToFront()`; otherwise create one, subscribe `WindowClosed`, and
  `ShowView` a view of the current state at once.
- `Notify()` (any thread): delegates to a `TrayStatusUpdater` whose read and show both run in the posted action on
  the UI thread: with no window open it reads nothing and shows nothing; otherwise it creates the view and calls
  `ShowView`. So the reading thread only does the updater's `Interlocked.Exchange` and the post.
- `WindowClosed`: the session drops the window and its view.
- `App` calls `Notify()` from `StatusChanged`, `TickReceived`, the recording's `StateChanged` and the midnight timer,
  next to the tray updater's `Notify()`.

**Menu.** `TrayMenuEntryKind.OpenDiagnostics`, `TrayMenu.OpenDiagnosticsText = "Open diagnostics"`, inserted by
`TrayMenu.Create` directly after the first separator, always enabled. `TrayIcon.BuildItems` wires its click;
`App.Dispatch` calls `_diagnostics?.Open()`.

**WPF window (`DiagnosticsWindow.xaml`, shell).** Implements `IDiagnosticsWindow`. `Title` from
`DiagnosticsView.Title`; default size 1000 × 600, resizable, `ShowInTaskbar` true. A header `ItemsControl` over
`HeaderLines`, then a `DataGrid` with `AutoGenerateColumns="False"`, one `DataGridTextColumn` per entry of `Columns`
bound to `Cells[i]`, `IsReadOnly`, `CanUserSortColumns="False"`, `CanUserAddRows="False"`,
`IsSynchronizedWithCurrentItem="False"`, `EnableRowVirtualization`, `EnableColumnVirtualization`,
`VirtualizingPanel.VirtualizationMode="Recycling"`, `ScrollViewer.CanContentScroll="True"`, no grouping.
`ShowView(view)`: remembers the grid's vertical scroll offset, sets the header and `ItemsSource = view`, restores the
offset after layout (clamped by WPF), and calls `Show()` the first time. `BringToFront()`: `WindowState.Minimized` →
`Normal`, then `Activate()`. `Closed` raises `WindowClosed`. Exit: `Shutdown(0)` closes the window (fact 12).

Layout: fixed by the spec's *Window content* and the values above; nothing is left to the Tester.

## Options considered

Recorded in record 0038: a flat table with rows made on demand (chosen); a tree of regions, islands and goods; all
rows built up front as view models; a window that subscribes only while open. For the hand-off, a dispatcher queue
of every tick lost to reusing the coalescing `TrayStatusUpdater` (the spec allows skipping intermediate ticks, E6).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `DiagnosticsView.cs` | new: header, columns, rows on demand, read-only `IList` |
| Censio.Companion.Core | `DiagnosticsRow.cs` | new: index and cells of one row |
| Censio.Companion.Core | `IDiagnosticsWindow.cs` | new: what the session needs of a window |
| Censio.Companion.Core | `DiagnosticsWindowSession.cs` | new: open, notify, close state |
| Censio.Companion.Core | `TrayMenu.cs`, `TrayMenuEntryKind.cs` | new entry and its text |
| Censio.Companion.Core | `PipeConnection.cs`, `PipeReplayRunner.cs` | `LatestTick` kept in `PublishTick` |
| Censio.Companion | `DiagnosticsWindow.xaml`, `DiagnosticsWindow.xaml.cs` | new WPF window implementing `IDiagnosticsWindow` |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs` | create the session, wire notify, dispatch and click |

## Signatures (for the Dev's skeleton)

```csharp
namespace Censio.Companion.Core;

public sealed class DiagnosticsView : IReadOnlyList<DiagnosticsRow>, IList
{
    public const string Title = "Censio Companion: Diagnostics";
    public const string NoEntriesText = "(no entries)";
    public static IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<string> HeaderLines { get; }
    public PipeTick? Tick { get; }
    public int Count { get; }
    public DiagnosticsRow this[int index] { get; }            // ArgumentOutOfRangeException outside 0..Count-1
    public static DiagnosticsView Create(string statusText, PipeTick? tick, TimeProvider clock);
    public IEnumerator<DiagnosticsRow> GetEnumerator();
    // explicit: IList.IsFixedSize (true), IList.IsReadOnly (true), ICollection.IsSynchronized (false),
    // ICollection.SyncRoot (this), object? IList.this[int] (set throws NotSupportedException),
    // IList.Contains, IList.IndexOf, ICollection.CopyTo(Array, int), IEnumerable.GetEnumerator,
    // IList.Add/Clear/Insert/Remove/RemoveAt (throw NotSupportedException)
}

public sealed class DiagnosticsRow
{
    public DiagnosticsRow(int index, IReadOnlyList<string> cells);
    public int Index { get; }
    public IReadOnlyList<string> Cells { get; }
}

public interface IDiagnosticsWindow
{
    event EventHandler? WindowClosed;
    void ShowView(DiagnosticsView view);
    void BringToFront();
}

public sealed class DiagnosticsWindowSession
{
    public DiagnosticsWindowSession(Func<string> readStatusText, Func<PipeTick?> readTick, TimeProvider clock,
                                    Action<Action> post, Func<IDiagnosticsWindow> createWindow);
    public bool IsOpen { get; }
    public void Open();
    public void Notify();
}

// TrayMenuEntryKind: new member OpenDiagnostics
// TrayMenu: public const string OpenDiagnosticsText = "Open diagnostics";
// PipeConnection, PipeReplayRunner: public PipeTick? LatestTick { get; }
```

Shell, not part of the skeleton: `DiagnosticsWindow`, an internal sealed partial WPF window that implements
`IDiagnosticsWindow`, with a parameterless constructor (task 4). Existing files the skeleton rewrites: `TrayMenu.cs`, `TrayMenuEntryKind.cs`,
`PipeConnection.cs`, `PipeReplayRunner.cs` (members only); `App.xaml.cs` and `TrayIcon.cs` change in step 6.

## Test files

- New: `tests/Censio.Companion.Core.Tests/DiagnosticsViewTests.cs`,
  `tests/Censio.Companion.Core.Tests/DiagnosticsWindowSessionTests.cs`,
  `tests/Censio.Companion.Core.Tests/FakeDiagnosticsWindow.cs` (test double).
- Extended: `TrayMenuTests.cs`, `PipeConnectionTests.cs`, `PipeReplayRunnerTests.cs`, optionally `PipeTestFrames.cs`
  (a helper for islands with given entries).

Existing test call sites: no signature changes. `TrayMenuTests.TrayMenuCreateListsEntriesInOrder` asserts the full
menu and is adapted by the Tester as the AC1 test (fact 14); every other existing test has no input a criterion
handles differently.

Files no unit test can cover: `src/Censio.Companion/DiagnosticsWindow.xaml`, `DiagnosticsWindow.xaml.cs` and the
changes in `App.xaml.cs` and `TrayIcon.cs` — Windows shell glue, no test project can load it on Linux; already
excluded by construction and by `sonar.coverage.exclusions` (fact 13, record 0004), no new exclusion.

## Areas

- `docs/areas/companion.md`: changed (see *Documentation updates*). `docs/areas/pipe.md`: changed. No new area.
  `docs/areas/README.md`: the companion row's scope adds "diagnostics window".

## Documentation updates

Owned by the Dev after the code is green, unless noted; both languages where the file has two.

- `docs/areas/companion.md`: *Scope* adds the diagnostics window. *Tray icon and menu*: line 13 becomes "Started, the
  Companion shows one icon in the notification area and opens no window until `Open diagnostics` is chosen"; the menu
  list (line 34) gains `Open diagnostics` after the first separator. New section *Diagnostics window* after
  *Folders*: title, *Window content* (header, columns, rows, number formats), update rules (each tick, status,
  local midnight, intermediate ticks may be skipped), the kept tick, close, single window, `Exit`, name sanitizing,
  memory (rows on screen only), read path, privacy (on screen only, nothing logged or written). *Replay* (line 151):
  the diagnostics window shows the replayed ticks. *Limits and errors*: "Not verified yet" adds the manual parts of
  AC5, AC6, AC8, AC10. *Related decisions*: record 0038.
- `docs/areas/pipe.md`: *Order and end* (lines 310-315): the live connection and the replay keep the latest closed
  tick whole; "The tick itself has no consumer yet" becomes "the diagnostics window is its consumer"; the slow
  consumer sentence names the coalescing hand-off of the diagnostics window.
- `docs/areas/README.md`: index row *companion* scope.
- `docs/ARCHITECTURE.md`: project table, `Censio.Companion.Core` adds "diagnostics window content and session",
  `Censio.Companion` adds "the diagnostics window"; *Data flow* line 46: "and the tray and the diagnostics window
  show the latest closed tick ([`decisions/0038`](decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md))".
- `README.md` (EN and DE): *Status* bullet (line 14 / 174) names `Open diagnostics`; *Companion* step 2 (line 103 /
  263) lists the entry; the replay paragraph (line 129 / 289) replaces "Nothing consumes the replayed data yet …"
  with: the window `Open diagnostics` shows the replayed ticks; a short paragraph after the tooltip paragraph (line
  111 / 271): what the window shows and that closing it leaves the Companion running.
- `docs/USER_GUIDE.md` (EN and DE): *Companion* step 2 (line 109 and its German counterpart); a paragraph on the
  window after the tooltip paragraph (line 117 and counterpart), with the columns and that values are raw GUIDs until
  names exist; *Recording and replay* replay item (line 135 and counterpart) as in the README; the first-start
  description (line 180 and counterpart) "opens no window until `Open diagnostics`"; *Data* (line 186 and
  counterpart): the window stores nothing; *Reporting problems* (line 194) and *Probleme melden*: a screenshot of the
  diagnostics window shows your island names, next to the raw-recording warning.
- `.squad/project.md` (Dev): *Security areas*, *Logging of external data* gains one sentence: the diagnostics window
  shows pipe content on screen only, island names through `LogLine.Sanitize`, nothing logged or written. *Test
  doubles* (Tester): `FakeDiagnosticsWindow`.

Sweep done: `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md` searched for "no window",
"menu", "consumer", "Open log folder" and the menu enumerations; every hit is listed above.

## Architecture check

`.squad/project.md` records no guarantee yet. The architecture's split (record 0004) holds: every rule lives in
`Censio.Companion.Core`, the shell only renders. The pipe's read path guarantee ("the caller must keep requesting")
is preserved by the hand-off (AC12). The privacy rule of recordings (nothing sent, nothing logged) is extended to the
window. No new dependency, project, setting or file path.

## Security considerations

- [ ] Byte or length limits on parsed input: no new limit. Names are at most 255 bytes before decoding (pipe), and
  `LogLine.Sanitize` with `int.MaxValue` never cuts; a replacement makes a name no longer (fact 6).
- [ ] Exceptions that reach a user-visible message: none. The window shows no exception text; an exception in a
  `TickReceived` handler is already swallowed by `PublishTick` (fact 2), and one in the posted UI action is a WPF
  dispatcher exception like any other tray update.

## Decision records

- `docs/decisions/0038-diagnostics-window-flat-table-of-rows-made-on-demand.md` (Proposed), indexed in
  `docs/decisions/README.md`.

## Out of scope / follow-ups

The spec's *Out of scope*. No follow-up issue.
