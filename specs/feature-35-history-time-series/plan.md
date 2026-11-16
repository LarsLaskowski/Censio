# Plan: History time series storage and retention

Source: Issue #35 | [spec.md](spec.md)
Tier: security — the deletion adds a guard that decides which pages may start a state-changing action (N7, record 0085 amending 0006), beside new tables, a retention job and a deletion in the server database.
Security areas touched: Server database (`ServerDatabase`, the new schema version, secure delete, `ServerStore`, the new `HistoryStore`, `ServerState`); Network exposure (a state-changing action on `/profiles/{profile}/regions`, reachable without login, the host rule, the setting `AllowedHosts`); Logging and showing external data (five new log lines, the profile name in the dialog)

## Problem / root cause

See [spec.md](spec.md): AC1 to AC12.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A tick is stored as rows in `Ticks`, `TickIslands`, `TickEntries`, `TickMapPairs` without foreign keys; floats as 32-bit patterns, `NULL` for none | `CensioDbContext.cs` (no `HasOne`), `TickWriter.cs:44-90` | confirmed |
| 2 | `Ticks` has the index (`ProfileId`, `ReceiveTicks`, `Number`), `TickIslands` (`TickNumber`, `Position`), `TickEntries` (`TickIslandId`, `Position`), `TickMapPairs` (`EntryId`): every read and fold of one profile and tick runs on an index | `CensioDbContext.cs:28-82` | confirmed |
| 3 | Every write of `ServerStore` takes `ServerDatabase.Gate` and runs `BEGIN IMMEDIATE` on the kept connection; a file read uses its own connection | `ServerStore.cs` `WriteAsync`, `ReadAsync` | confirmed |
| 4 | "Already stored" is decided only by `SELECT 1 FROM Ticks WHERE Id = $id`; `ReadTickAsync` reads by identity with no profile condition and is called only by tests | `ServerStore.StoreTickAsync`, `TickReader.ByIdSql` (`TickReader.cs:17`), `grep ReadTickAsync src` | confirmed |
| 5 | A receive time is the Companion's read stamp; a replay keeps the recorded times, and the fixture `long-session.jsonl` is from 2026-06-10 | `contract.md` *Values*; `replay-sending.md` *What is sent*; the fixture's first line | confirmed |
| 6 | The contract refuses repeated island keys, not repeated product GUIDs within an island | `ContractRules.CheckIsland` (`ContractRules.cs:417-453`) | confirmed |
| 7 | .NET ticks of a UTC time are whole hours apart at hour starts (`r - r % 36,000,000,000` is the hour start) | `DateTime` ticks count from 0001-01-01 00:00 | confirmed |
| 8 | A page reads a profile the store does not hold as `Not found.`; `/regions` follows `ServerState.NewestProfile` | `DataPageBase.ReadOnceAsync`, `Regions.razor.cs` | confirmed |
| 9 | The root parameters of an interactive server component travel in a descriptor protected by the data-protection key ring | aspnetcore `release/11.0` `src/Components/Server/src/Circuits/ServerComponentDeserializer.cs:29-34,248` (`_dataProtector.Unprotect`); measured by Security in step 3 | confirmed |
| 10 | `AllowedHosts` is read by the default host builder, split at `;`, `*` turning filtering off, and filtering applies to every request | aspnetcore `release/11.0` `src/DefaultBuilder/src/WebHost.cs:260-268`, `HostFilteringStartupFilter.cs` | confirmed |
| 11 | The image's health check calls `http://127.0.0.1:<port>/health` | `src/Censio.Server/Dockerfile:42` | confirmed |
| 12 | `PRAGMA secure_delete = ON` on the kept connection plus `PRAGMA wal_checkpoint(TRUNCATE)` leave no deleted name in the file, `-wal` or `-shm` | Security's scratch measurement in step 3; the Tester's AC8 test on the real schema stays required, and a name it finds goes to the Lead (`VACUUM` fallback, record 0084) | confirmed |
| 13 | A truncating checkpoint with a reader holding an older snapshot returns `busy` = 1 without an exception (about 1 ms), and succeeds once the reader ends | Security's scratch measurement in step 3; pinned by `HistoryStoreClearDeletedHeldReadReportsBlocked` | confirmed |
| 14 | `SemaphoreSlim.Release` hands the count to the first queued `WaitAsync` caller, so a store queued during a step runs before the job's next step | to verify: Tester, AC11 test; the Dev reads `SemaphoreSlim.cs` of `dotnet/runtime` `release/11.0` before relying on it | unverified |
| 15 | One fold step and one removal step at the contract's bounds, and the deletion's first transaction for a profile of a year of hourly points plus several weeks of unfolded ticks, take at most 10 seconds with secure delete | to verify: Dev, measurement task 10 in step 6; a step over 10 seconds → Lead decides (steps per island, record 0083); the first transaction over 10 seconds → Lead decides (the tombstone set in steps, record 0084) | unverified |
| 16 | An inline `MudDialog` of MudBlazor 9.13 needs a `MudDialogProvider` in the layout | to verify: Dev with the bUnit tests of AC9 in step 6 | unverified |
| 18 | `MainLayout` has `MudPopoverProvider` and no dialog provider | `MainLayout.razor:6-7` | confirmed |
| 19 | nginx sends `Host: $proxy_host` unless told otherwise, so behind `proxy_pass http://127.0.0.1:8080` every request reaches the server with an address as its host | nginx `proxy_set_header` default; `USER_GUIDE.md:127` sends operators to a proxy | confirmed |
| 20 | The framework answers an interactive page with `Content-Security-Policy: frame-ancestors 'self'`, so another site cannot frame a trusted page | Security's measurement on `/regions` in step 3; pinned by `ProgramGetDataPageForbidsFramingByOtherSites` | confirmed |
| 21 | Services cannot be added after `builder.Build()`; the state is registered by a late-bound factory | `Program.cs:18-22` | confirmed |
| 22 | `using var database` disposes the gate after `app.RunAsync` returns (`ServerDatabase.Dispose`) | `Program.cs:38`, `ServerDatabase.cs:86` | confirmed |

## Tests per criterion

Data tests build ticks with the helpers of `ServerStoreTests` and use a `FakeTimeProvider`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | HistoryStoreFoldNextTickHourOlderThanSevenDaysFoldsIntoHourlyPoint, HistoryStoreFoldNextTickHourEndingWithinSevenDaysKeepsTick, HistoryStoreFoldNextTickLatestHourOfProfileKeepsTicks, HistoryStoreFoldNextTickWritesOneRowPerIslandGoodAndMapGuid | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |
| AC2 | HistoryStoreReadSeriesHourlyPointGivesMeansOverCarryingTicks, HistoryStoreReadSeriesValueNoTickCarriedHasNoMean, HistoryStoreReadSeriesAbsentMapGuidCountsAsZero, HistoryStoreReadSeriesHourlyPointCarriesHourGameTimesCountAndLatestName | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |
| AC3 | HistoryStoreFoldNextTickLateTickEqualsFoldingAllAtOnce, HistoryStoreStoreTickFoldedIdentityReportsAlreadyStored | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |
| AC4 | HistoryStoreReadSeriesTicksGiveOnePointPerTickOldestFirst, HistoryStoreReadSeriesMissingIslandOrGoodGivesPointWithoutValues, HistoryStoreReadSeriesSpanningFoldedHoursGivesPointsInTimeOrder, HistoryStoreReadSeriesLateTickReturnedBesideHourlyPointUntilFolded, HistoryStoreReadSeriesRangeEndIsExclusive | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |
| AC5 | HistoryStoreFoldNextTickFailureLeavesTickWhole, HistoryRetentionRunFoldFailureLogsWarningAndNextRunContinues, HistoryRetentionRunFoldedTicksLogsReducedLine, HistoryRetentionStartRunsAfterStartAndEveryHour, HistoryRetentionStopDuringRunLogsNoWarningAndDisposeWaits | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs, tests/Censio.Server.Tests/HistoryRetentionTests.cs |
| AC6 | ServerStoreDeleteProfileRemovesProfileAndIslandsAndHidesTicks, HistoryRetentionDeleteProfileDuringFoldsSweepsBeforeNextFoldStep, HistoryStoreSweepNextRemovesEveryRowOfDeletedProfile, HistoryStoreSweepNextKeepsRowsOfRecreatedProfile, ServerStoreDeleteProfileKeepsOtherProfilesCatalogAndIcons, ServerStoreDeleteProfileUnknownProfileChangesNothing | tests/Censio.Server.Data.Tests/ServerStoreTests.cs, tests/Censio.Server.Data.Tests/HistoryStoreTests.cs, tests/Censio.Server.Tests/HistoryRetentionTests.cs |
| AC7 | ServerStateDeleteProfileDropsHeldStateAndNotifies, ServerStateDeleteProfileHoldsNextNewestProfile, ServerStateDeleteProfileFailedRebuildDropsProfileAndNotifies, ServerStateStoreTickAfterDeletionCreatesUnnamedProfile, RegionsDeletedProfileShowsNotFound | tests/Censio.Server.Tests/ServerStateTests.cs, tests/Censio.Server.Tests/RegionsTests.cs |
| AC8 | HistoryStoreClearDeletedLeavesNoNameInFiles, HistoryStoreClearDeletedHeldReadReportsBlocked, HistoryRetentionRunBlockedClearLogsWarningAndNextRunClears, HistoryRetentionRunFailingFoldStillClearsDeletedHistory | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs, tests/Censio.Server.Tests/HistoryRetentionTests.cs |
| AC9 | RegionsProfileAddressOffersDeleteHistory, RegionsNewestAddressOffersNoDeleteHistory, RegionsDeleteConfirmedDeletesProfileOfDialog, RegionsDeleteCancelledDeletesNothing, RegionsDeleteFailedShowsFailureText, RegionsUntrustedHostDisablesButtonAndIgnoresEvent, DeletionHostRuleIsTrustedDecidesByHostForm, AppPassesDeletionAllowedOfRequestHost, UiTextsForDeletionTextsMatchSpec, ProgramGetDataPageForbidsFramingByOtherSites | tests/Censio.Server.Tests/RegionsTests.cs, tests/Censio.Server.Tests/DeletionHostRuleTests.cs, tests/Censio.Server.Tests/AppTests.cs, tests/Censio.Server.Tests/UiTextsTests.cs, tests/Censio.Server.Tests/ProgramTests.cs |
| AC10 | HistoryRetentionDeleteProfileLogsLinesWithoutName, HistoryRetentionDeleteProfileFailureLogsWarning | tests/Censio.Server.Tests/HistoryRetentionTests.cs |
| AC11 | HistoryStoreFoldNextTickWaitingStoreRunsBetweenSteps | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |
| AC12 | HistoryStoreReadSeriesRepeatedGoodUsesFirstEntry, HistoryStoreFoldNextTickRepeatedGoodFoldsFirstEntry | tests/Censio.Server.Data.Tests/HistoryStoreTests.cs |

Cases the tests must hold (the G1 cases are built and pinned by their tests):
- `DeletionHostRuleIsTrustedDecidesByHostForm` (data rows): `127.0.0.1`, `192.168.1.10`, `[::1]`, `[fe80::1]`,
  `localhost`, `LOCALHOST` → trusted; `evil.example`, `localhost.`, `foo.localhost`, `192.168.1.10.nip.io`, `nas`,
  empty, `null` → not trusted with `AllowedHosts` `*`, unset or empty; `nas` with `nas;127.0.0.1` and
  `censio.example.com` with `*.example.com` → trusted, `example.com` with `*.example.com` → not.

## Approach

**Schema** (new migration `AddHistory`, generated with the pinned `dotnet-ef`, `docs/CONTRIBUTING.md`):
- `HourlyPoints` (`Number` key, `ProfileId`, `HourTicks`, `TickCount`, `FirstGameTime`, `LastGameTime`; unique
  `ProfileId`, `HourTicks`).
- `HourlyIslands` (`Id`, `PointNumber`, island key, `Name`, `NameReceiveTicks`, `NameTickNumber`; unique point and key).
- `HourlyEntries` (`Id`, `HourlyIslandId`, `ProductGuid`, `EntryCount`, per float a REAL sum and an INTEGER count,
  per integer an INTEGER sum; unique island and good).
- `HourlyMapPairs` (`Id`, `EntryId`, `Kind`, `Guid`, `AmountSum`; unique entry, kind, GUID).
- `FoldedTicks` (`Number` key, `Id` unique, `PointNumber` indexed: the hourly point that holds the tick).
- `DeletedProfiles` (`TombstoneId` key): one fresh random identity per deletion.

`StoreTickAsync` reports "already stored" when `Ticks` or `FoldedTicks` holds the identity. A deleted profile's ticks
and hourly points get its tombstone as `ProfileId`; no profile row carries a tombstone, so no read or fold by profile
ever sees them. The one read by tick identity, `TickReader.ByIdSql` (`ReadTickAsync`), joins `Profiles`, so a
tombstoned tick reads as none; every other read goes by a profile identity. Row numbers are not used as marks: SQLite reuses the highest number of a
deleted row without `AUTOINCREMENT`.

**Secure delete.** `ServerDatabaseOpener` sets `PRAGMA secure_delete = ON;` on the kept connection, file and memory,
right after the journal mode (start step 6 in `server-data.md`), so every deletion of this version, retention included,
overwrites what it removes.

**Fold step** (`HistoryStore.FoldNextTickAsync(profileId, now)`), one write transaction:
1. Cutoff `C` = hour start of `now − 7 d`; the latest hour `L` = hour start of the profile's latest tick. The tick to
   fold is the profile's tick with the smallest (`ReceiveTicks`, `Number`) and `ReceiveTicks < min(C, L)`; none →
   `NothingToDo`.
2. Read the tick (as `TickReader` does, by number), upsert its `HourlyPoints` row (count + 1, game time minimum and
   maximum), per island its `HourlyIslands` row (name taken when its receive time and number are not older), per island
   the first entry of each product GUID in stored position into `HourlyEntries` (entry count + 1; each float present
   adds its value as a double to its sum and 1 to its count; integers to their sums), and that first entry's map pairs,
   no other entry's, to `HourlyMapPairs`.
3. Insert its identity into `FoldedTicks` with the point's number, delete its pairs, entries, islands and tick row; return `Done` with the hour.

Each statement is prepared once per step as in `TickWriter`; memory is one tick, as for the ingest.

**Series read** (`HistoryStore.ReadSeriesAsync(query)`), one deferred read transaction: the hourly points of the
profile with `RangeStart ≤ hour start < RangeEnd`, each left-joined to the island and good of the query, then the
ticks with `RangeStart ≤ receive time < RangeEnd`, left-joined to the first entry of the good on the island; merged by
time (hourly point first on equal times), then by number. A tick point has `TickCount` 1, both game times equal, its
floats converted to double and its integers as doubles; an hourly point has each mean as sum / count, `null` when the
count is 0, integer means over `EntryCount`, map means as `AmountSum / EntryCount`. Missing island or good: name `null`,
every value `null`, empty maps.

**Deletion.** `ServerStore.DeleteProfileAsync(profileId)`, one write transaction: unknown profile → `NothingToDo`;
otherwise delete its `Profiles` and `Islands` rows, insert a fresh tombstone into `DeletedProfiles` and set it as
`ProfileId` of the profile's `Ticks` and `HourlyPoints` rows (one row per tick or hour, through their indexes) →
`Done`. `HistoryStore.SweepNextAsync()` removes, in one transaction, one tombstoned tick with its islands, entries and
pairs, else one tombstoned hourly point with its islands, entries, pairs and `FoldedTicks` rows, else a
`DeletedProfiles` row with nothing left; `NothingToDo` when no `DeletedProfiles` row exists. `HistoryStore.ClearDeletedAsync()` runs
`PRAGMA wal_checkpoint(TRUNCATE);` under the gate: `busy` 1 → `Blocked`, an exception → `Failed`, otherwise (and always
for the in-memory database) `Done`.

**State.** `ServerState.DeleteProfileAsync(profileId)` takes its write gate, calls the store and, on `Done`, rebuilds
the held profiles and the profile list as `RestoreAsync` does (catalog kept), then notifies `Profile` with the profile.
Once the store reported `Done` the deletion counts as done (E9 covers only a failure before the rows are gone): the
rebuild reads with `CancellationToken.None`, and a `SqliteException` from it is logged as a failed state read and
replaced by removing the profile from the held list, the held states and the marks in memory; it still notifies and
returns the store's `Done` (lead decision, G2).

**Job** (`HistoryRetention`, logger category `Censio.Server.History`). `Start` begins one loop on the injected
`TimeProvider`: a run at once, then one an hour after each run ends, and one more whenever a deletion asks; runs never
overlap. `Program.cs` registers the job before `Build` by a late-bound factory, as it does the state (fact 21), creates
`HistoryStore` and `HistoryRetention` after the restore with `using var` after the database, so the job is disposed
first (fact 22), and calls `Start` from `ApplicationStarted`, so the start never waits for it. `Dispose` cancels the
loop and waits for it to end; a step cut off by that cancellation rolls back and logs nothing. A run:
1. Sweep steps until `NothingToDo`; a failed step logs the clean-up warning (E10) with the exception and ends the run.
2. Per profile of `ServerStore.ListProfilesAsync`, fold steps until `NothingToDo`; a failed step logs the E6 warning
   and ends the run. Before each fold step the run checks for a deletion asked meanwhile and, if one is, does step 1
   first, so a deletion waits at most for one fold step, not for a backlog of folds (N6). Counts distinct (profile, hour) pairs and ticks; logs the reduced line when ticks > 0
   (after the E6 warning when a later step failed).
3. When a clean-up is requested (a swept step or a deletion): `ClearDeletedAsync` right after step 1, before any fold,
   and again after step 2 when that completed; `Done` logs the cleared line, `Blocked` or `Failed` the clean-up warning
   (with the exception when failed) and keeps the request for the next run, so a fold that keeps failing never holds
   back the clean-up (N5, E10).

`HistoryRetention.DeleteProfileAsync` calls `ServerState.DeleteProfileAsync`, logs the deleted line on `Done` or the
deletion warning on `Failed` (nothing for `NothingToDo`), asks for a run with clean-up and returns the outcome. Log
texts are the spec's (N3, E6, E10) as `LoggerMessage` methods without arguments except the two counts.

**Host rule.** `DeletionHostRule.IsTrusted(host, allowedHosts)`: `host` is `HttpRequest.Host.Host` (brackets kept for
IPv6). Trusted when `Uri.CheckHostName` of the host without brackets is `IPv4` or `IPv6`, or the host is `localhost`
(ordinal, ignoring case, nothing else), or `allowedHosts` is neither empty nor `*` and one of its `;`-separated entries
equals the host ignoring case or is `*.<suffix>` and the host ends with `.<suffix>`. `App` reads the request's host and
`Configuration["AllowedHosts"]` once when rendered and passes `DeletionAllowed` to `Routes` (a root parameter, fact 9),
which cascades it under the name `DeletionAllowed`; without an `HttpContext` it is `false`. Behind a reverse proxy the
rule sees the host the proxy sends (fact 19): a proxy that replaces it with an address or a listed name makes every page
it serves trusted, rebinding pages included, so the documentation requires the proxy to pass the client's host
(`proxy_set_header Host $host;`) and to answer only its own names, which are then listed in `AllowedHosts`. `X-Forwarded-Host`
would be no safer.

**Page layout** (`Regions` on `/profiles/{profile}/regions` only, in every mode but `NotFound`): after the as-of status
and before the table or status text, an outlined `MudButton` in the error colour with `Delete history`; when
`DeletionAllowed` is false the button is disabled and followed, on the same line, by the host text of AC9. The click
captures the page's profile identity and shown name (`PageNames.Profiles` over `State.Profiles`, as `ProfileLabel`)
and opens an inline `MudDialog`: title, the text with the name, then — after a failure — the failure text in a second
paragraph; actions `Cancel` (text button), then `Delete` (filled, error colour, disabled while the deletion runs). The
handler returns at once when `DeletionAllowed` is false. `Done` and `NothingToDo` close the dialog and navigate to
`/regions`; `Failed` keeps it open with the failure text. `MainLayout` gets a `MudDialogProvider` (fact 16).

**Measurement** (task 10, Dev, scratch program outside the repository, Release build, file database with
`synchronous=FULL`, as `server-data.md` *Measurements*): store the fixture's ticks and a tick at the bounds with old
receive times, then time one fold step, one sweep step, and the deletion's first transaction for a profile of 7 days of fixture
ticks and for one of a year of synthetic hourly points plus several weeks of unfolded ticks (N2, fact 15), a
`StoreTickAsync` started during the step at the bounds (AC11), the bytes of a folded hour of the fixture against its
ticks and the bytes per folded tick of `FoldedTicks` with its unique index (N4), and the series read over 7 days of
ticks and over a year of synthetic hourly points (N8); three runs each, figures into `history.md`.

## Options considered

- Retention, deletion and the guard: the options of records 0083, 0084 and 0085.
- Folding in SQL with a function that decodes the bit patterns (record 0067): loses to C# over the read tick, which
  reuses `TickReader` and holds one tick, as the ingest does.
- Deleting by a form post with antiforgery: a rebinding page passes antiforgery too; the event keeps the dialog's
  failure text.
- A `BackgroundService`: it starts before the server listens unless deferred (N1).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `HourlyPointRow`, `HourlyIslandRow`, `HourlyEntryRow`, `HourlyMapPairRow`, `FoldedTickRow`, `DeletedProfileRow` (new, internal), `CensioDbContext`, `Migrations/*_AddHistory*`, snapshot | the schema above |
| Censio.Server.Data | `ServerDatabaseOpener` | secure delete |
| Censio.Server.Data | `StoreAccess` (new, internal static) | `WriteAsync`, `ReadAsync`, `CreateCommand`, `ExecuteAsync`, `Rollback` moved from `ServerStore` unchanged, used by both stores |
| Censio.Server.Data | `ServerStore` | already stored with `FoldedTicks`, `DeleteProfileAsync` |
| Censio.Server.Data | `HistoryStore`, `HistoryFolder` (new, internal: the prepared fold commands), `HistorySeriesQuery`, `HistoryPoint`, `HistoryAmount`, `HistoryOutcome`, `HistoryResult` (new) | fold, sweep, clear, series |
| Censio.Server | `ServerState` | `DeleteProfileAsync` |
| Censio.Server | `HistoryRetention` (new), `Program.cs` | the job |
| Censio.Server | `DeletionHostRule` (new), `App.razor(.cs)`, `Routes.razor(.cs)`, `Regions.razor(.cs)`, `MainLayout.razor`, `UiTexts` | host rule, button, dialog, texts |

## Signatures (for the Dev's skeleton)

Censio.Server.Data:
- public sealed `class HistoryStore` with `public HistoryStore(ServerDatabase database)` and `HistoryStore.FullResolution` (a public static readonly TimeSpan of 7 days)
- `public Task<HistoryResult> FoldNextTickAsync(Guid profileId, DateTimeOffset now, CancellationToken cancellationToken)`
- `public Task<HistoryResult> SweepNextAsync(CancellationToken cancellationToken)`
- `public Task<HistoryResult> ClearDeletedAsync(CancellationToken cancellationToken)`
- `public Task<IReadOnlyList<HistoryPoint>> ReadSeriesAsync(HistorySeriesQuery query, CancellationToken cancellationToken)`
- `public Task<HistoryResult> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)` on ServerStore
- `public enum HistoryOutcome` with the members Done, NothingToDo, Blocked and Failed
- public sealed `class HistoryResult` with `public HistoryResult(HistoryOutcome outcome, Exception? failure, DateTimeOffset? hour)` and the get-only properties Outcome, Failure and Hour
- public sealed `class HistorySeriesQuery` with the required init properties ProfileId (Guid), SessionGuid (int), IslandId (byte), AreaIndex (byte), ProductGuid (int), RangeStart and RangeEnd (DateTimeOffset)
- public sealed `class HistoryPoint` with the required init properties Time (DateTimeOffset), IsHourly (bool), TickCount (int), FirstGameTime and LastGameTime (long), IslandName (string?), Generation, Consumption, Delta, PerfectGeneration, PerfectConsumption, Income, SummedProductivity, AverageProductivity, BuildingCount, Maintenance and Profit (double?), WorkforceAmounts and BuildingAmounts (IReadOnlyList of HistoryAmount)
- public sealed `class HistoryAmount` with the required init properties ItemGuid (int) and Amount (double)

Censio.Server:
- `public Task<HistoryResult> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)` on ServerState
- public sealed partial `class HistoryRetention : IDisposable` with `public HistoryRetention(ServerState state, HistoryStore store, TimeProvider timeProvider, ILogger logger)` and `HistoryRetention.RunInterval` (a public static readonly TimeSpan of one hour)
- `public void Start(CancellationToken stoppingToken)`
- `public Task RunAsync(CancellationToken cancellationToken)`
- `public Task<HistoryOutcome> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken)` on HistoryRetention
- `public void Dispose()`
- `public static class DeletionHostRule` with `public static bool IsTrusted(string? host, string? allowedHosts)`
- `Routes.DeletionAllowed`: a public bool parameter with get and set
- `App.Configuration`: a public injected IConfiguration with get and set
- on Regions, a cascading parameter named DeletionAllowed of the same declaration, and `Regions.Retention`: a public injected HistoryRetention with get and set
- on UiTexts, seven required init strings: DeleteHistory, DeleteHistoryTitle, DeleteHistoryTextFormat (`{0}` the name), DeleteHistoryConfirm, DeleteHistoryCancel, DeleteHistoryFailed, DeleteHistoryHostRefused

Checked against *Writing code*: no nested type, no enum name ending in Flags or Enum, at most four parameters, no
member named from, to, step or End, and ItemGuid instead of a member named after the type Guid.

## Test files

- tests/Censio.Server.Data.Tests/HistoryStoreTests.cs (new)
- tests/Censio.Server.Data.Tests/ServerStoreTests.cs
- tests/Censio.Server.Tests/HistoryRetentionTests.cs (new)
- tests/Censio.Server.Tests/ServerStateTests.cs
- tests/Censio.Server.Tests/RegionsTests.cs
- tests/Censio.Server.Tests/DeletionHostRuleTests.cs (new)
- tests/Censio.Server.Tests/AppTests.cs
- tests/Censio.Server.Tests/UiTextsTests.cs
- tests/Censio.Server.Tests/ProgramTests.cs

Existing test code a change affects, adapted by the Tester: `PageTestHost` registers a `HistoryRetention` over its state
and an `IConfiguration`, and cascades `DeletionAllowed`; `UiTextsTests` that enumerate every text take the seven new
ones; `CensioDbContextFactoryCreateDbContextHasNoPendingModelChanges` passes once the migration exists (Dev). A test that
stores and reads ticks keeps its inputs: no stored tick is older than 7 days unless a test makes it so.

Files no unit test can cover: none (generated migration code, row classes: as `InitialSchema`'s). Accepted uncovered
lines: the retention factory's guard in `Program.cs` (as the state's); the held-state condition in
`ServerState.ReadProfilesAsync` whose false branch needs a tick removed between two reads the write gate serializes.

## Areas

- New area `history` (Dev writes `docs/areas/history.md` and its index row): *Scope* the stored time series per island
  and good, retention, deleting a profile's history and its clean-up; *Not here* the schema and file (server-data), the
  page and its button (web-pages), charts (#36 to #38). It states the series read and its points, gaps, the fold rule
  and its cutoff, the latest hour, late and resent ticks, repeated goods, the job's schedule and log lines,
  tombstones and the removal steps, secure delete and the checkpoint, the legacy residue, and the measurements; that
  `FoldedTicks` grows by one row per folded tick without end (bytes per tick measured); that a resent tick of a deleted
  profile is reported as already stored until its tick is swept, and a deletion asked during a run waits at most for
  one fold step.
- `server-data.md`: the six tables under *What is stored*, secure delete in *Start* step 6, already stored with folded
  identities, `history.md` instead of "(#35)" in *Scope*, *Measurements* growth sentence and *Not here*.
- `server-state.md`: deleting a profile rebuilds the held profiles and notifies; *Not here* points to `history.md`.
- `web-pages.md`: the button, the host rule and its text, the dialog, navigation after a deletion, and the deletion in
  *Logging* (none from the page); that another site cannot frame a data page (`frame-ancestors 'self'`), which the
  guard relies on; *Related decisions* 0085.
- `web-ui.md`: the seven texts in the *Texts* table, the dialog provider.
- `server.md`: `AllowedHosts` (unset `*`; listing names turns host filtering on for every request, so list every name
  and address used, `127.0.0.1` for the health check), next to the line on host filtering; the reverse proxy rule of record 0085 (*Consequences*: pass `Host $host`, answer only its own names).
- `replay-sending.md` *Growth*, `ingest.md` *Growth by a token holder*, `pipe.md` *Not here*: point to `history.md`
  (every stored tick is a point of its own, also with a repeated game time).

## Documentation updates

All by the Dev once the code is green:
- the area documents of *Areas*, `docs/areas/README.md` (the `history` row; the `server-data` row's "retention (#35)").
- `docs/ARCHITECTURE.md`: the server keeps the history with retention and deletes a profile on request (record 0083,
  0084), and the paragraph on routes without login gets the deletion and its host rule (record 0085).
- `README.md`, English and German: the server bullet (retention instead of "until the retention of a later release"),
  "There is no history" becomes the stored history without charts (#36 to #38), the configuration table gets
  `AllowedHosts`, deleting a profile in the web interface section.
- `docs/USER_GUIDE.md`, English and German: a section *History and deleting a profile* (retention rule, the button and
  dialog, the host rule and `AllowedHosts`, the reverse proxy rule of record 0085 (*Consequences*: pass `Host $host`, answer only its own names), the clean-up line, the legacy residue and `VACUUM` with the server stopped);
  the replay lines 335 and 369 (and German 798, 832) and the data paragraph 450 (913) lose "until retention (#35)".
- `SECURITY.md`: anyone who reaches the server by its address can delete a profile's history; a page of another site
  cannot, given the reverse proxy rule of record 0085 (*Consequences*: pass `Host $host`, answer only its own names).
- `.squad/project.md` (the Dev reports the text, the orchestrator writes it): *Security areas* server database line
  gets `HistoryStore`, `HistoryRetention` and secure delete; network exposure line gets the deletion on
  `/profiles/{profile}/regions`, `DeletionHostRule` and `AllowedHosts`; *Guarantees* history line points to
  `history.md` instead of "(retention with #35)"; *Integration surface* configuration line gets `AllowedHosts` in
  `DeletionHostRule`.

## Architecture check

The ingest still stores before it acknowledges and waits at most for one step (AC11); a gap stays a gap (AC2); history
is deletable per profile (record 0021); record 0006's open pages stay open for reading, and record 0085 guards the one
state-changing action, as the Product Manager asked.

## Security considerations

- [x] No limits on parsed input: the series query comes from code, not from a request; the host is matched, not parsed
  into a structure.
- [x] Exceptions that reach a user: a failed deletion shows only `The history could not be deleted.`; store exceptions
  go to the log (`history.md`), never to the page. A fold, sweep or clean-up failure reaches only the log.
- The host rule trusts only host forms an attacker's name cannot take (fact 9: the result cannot be changed by the
  client); the page's handler checks it again before deleting.
- Every SQL value travels as a parameter; the statements are constants.

## Decision records

- `docs/decisions/0083-history-folded-tick-by-tick-into-hourly-sums-and-counts.md` (Proposed)
- `docs/decisions/0084-profile-deleted-in-steps-and-cleared-from-the-file.md` (Proposed)
- `docs/decisions/0085-deletion-only-from-pages-opened-by-a-trusted-host.md` (Proposed, amends 0006)

## Out of scope / follow-ups

- Charts and their gap drawing (#36 to #38); the read API here is what they call.
- Shrinking the file and removing residue of older versions (user guide: `VACUUM` with the server stopped).

## Challenge

Plan challenge by the Devil's Advocate (1 major, 8 minor), all accepted:

1. (major) The host rule behind a reverse proxy: accepted. Nothing a proxy sends is safer than `Host`, so the rule stays
   and the requirement is documented: the proxy passes the client's host and answers only its own names (Approach
   *Host rule*, fact 19; `server.md`, user guide, `SECURITY.md`, record 0085 *Consequences*).
2. (minor) AC1 said "more than 7 days" while the cutoff folds an hour ending exactly 7 days back: the spec now says "7
   days or more" (S2, AC1); the AC1 case tests both sides.
3. (minor) `FoldedTicks` grows forever: counted in N4 (spec) and the measurement, stated in `history.md` and record 0083.
4. (minor) `ReadTickAsync` returned a deleted profile's ticks: `TickReader.ByIdSql` joins `Profiles`; asserted in the AC6
   test.
5. (minor) A deletion waiting behind a fold backlog: a run checks for a deletion before each fold step and sweeps and
   clears first; the resend window is stated in `history.md`; test `HistoryRetentionDeleteProfileDuringFoldsSweepsBeforeNextFoldStep`.
6. (minor) Registration after `Build`: a late-bound factory, as for the state (fact 21); `PageTestHost` registers it alike.
7. (minor) Shutdown mid-step: the job is disposed before the database, `Dispose` waits for the loop, a cancelled step
   logs nothing; test `HistoryRetentionStopDuringRunLogsNoWarningAndDisposeWaits`.
8. (minor) Map pairs of a repeated good: only the first entry's pairs are folded; the AC12 case asserts the second
   entry's GUIDs are absent.
9. (minor) The deletion's first transaction: measured for a year of hourly points plus weeks of unfolded ticks, held to
   10 seconds in fact 15 with the Lead deciding above it.

## Security review

1. (blocking) Host rule behind a reverse proxy: resolved as challenge 1; record 0085 gets the consequence in Security's
   words, and the plan's edits of `SECURITY.md`, `server.md` and the user guide (English and German) carry it.
2. (fix now) Framing: `ProgramGetDataPageForbidsFramingByOtherSites` pins `frame-ancestors 'self'` (or `'none'`) on a data
   page (fact 20); one sentence each in record 0085 and `web-pages.md`.
3. (fix now) `FoldedTicks` bytes: measured per folded tick with its index in task 10; `history.md` states the permanent
   growth.
4. (fix now) The deletion's first transaction: in fact 15 with the 10-second limit; above it the Lead decides (the
   tombstone set in steps).
5. Security's measurements of facts 9, 12 and 13 are taken: the facts are confirmed, the AC8 test stays required.
