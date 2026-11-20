# Plan: Show trade routes of pipe protocol 3

Source: Issue #225 | [spec.md](spec.md)
Tier: security — the change adds two routes reachable without login (`/trade-routes`, `/profiles/{profile}/trade-routes`), which changes the network exposure, besides new parsed input, storage and shown external names.
Security areas touched: Network exposure (two new data routes); Parsing of external input (`ContractReader`, `ContractScanner`, `ContractRules` for the new message); Server database (three new tables, the deletion and its clean-up, `ServerState`); Logging and showing external data (route, folder and island names on a page, `PageNames`, `ProfileViewBuilder`); Ingest token and Companion authentication (`IngestSession`: the new message type after the accepted hello); Outbound calls (`ReplaySender` sends trade routes to `--server`).

## Problem / root cause

See [spec.md](spec.md), *Problem*: `PipeTickBuilder.AddMessage` (`src/Censio.Pipe/PipeTickBuilder.cs:142-173`) has no case for a `TradeRoutesMessage`, so nothing after the parser sees it.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Only `Censio.Pipe` uses the decoded types | `grep -rln "TradeRoute\|ItemOffer" src`: files under `src/Censio.Pipe` only | confirmed |
| 2 | The builder drops type 4 and 5 messages; tests pin that | `PipeTickBuilder.cs:142-173` (no case); `PipeTickBuilderTests.cs:629`, `:652` assert no item | confirmed |
| 3 | The fixture's stations name only islands its island frames carry; problems and lost ships are 0 throughout | scratch decode of `protocol-3-trade-routes.jsonl`: 25 station keys, all island keys; 627 routes with 0 major, minor, lost | confirmed |
| 4 | Contract version 1 is released and read exactly | `ContractReader.cs:15` `CurrentVersion = 1`, `:86`; `git show v0.3.0:src/Censio.Contracts/ContractReader.cs` exists; record 0058 | confirmed |
| 5 | Version texts that name 1 | `ContractError.cs:20` (`InvalidText`), `:57` (formats `CurrentVersion`), `TrayMenu.cs:373`, `docs/USER_GUIDE.md:363`, `:841`, `docs/ARCHITECTURE.md:61-62` | confirmed |
| 6 | The replay counts refused messages whatever their type and ticks only with `isTick` | `ReplaySender.cs:575-595` (`Count(refusal, isTick)`) | confirmed |
| 7 | A session event's stamp is the last read stamp, taken when the item is sent | `PipeReplayRunner.cs:424-435` | confirmed |
| 8 | Ingest stores ticks, icons and catalogs; other accepted messages are acknowledged without storing | `IngestSession.cs:555-600` | confirmed |
| 9 | Pages re-read on every profile notice and treat a view without tick or regions as no data | `DataPageBase.cs:342-372`, `:300-320` (`ShowRead`) | confirmed |
| 10 | A not-held profile entering the held set by a tick reads only its island list | `server-state.md`, *Held profiles*; `ServerState.HoldTickAsync` | confirmed |
| 11 | The deletion removes a profile's own rows in one transaction and the clean-up checks names in the files with the real schema | `ServerStore.cs:123-150`; `HistoryStoreTests.HistoryStoreClearDeletedLeavesNoNameInFiles` (`:1023`) | confirmed |
| 12 | v0.3.0's schema is `InitialSchema` only | `git ls-tree v0.3.0 src/Censio.Server.Data/Migrations/` | confirmed |
| 13 | `Icons.Material.Filled.DirectionsBoat` exists in MudBlazor 9.13.0 | the name is in `MudBlazor.dll` of the restored package | confirmed |
| 15 | A hello rebuilds a held state from profile, tick and islands only, and both held branches of a tick do the same | `ServerState.cs:225-242` (`StoreProfileAsync`, on every accepted hello, `IngestSession.cs:412`); `HoldTickAsync` near `:856` and `:875` | confirmed |
| 16 | Only an *invalid* or catalog refusal with a readable id is acknowledged; *too large* closes with 1009, *malformed* with 1008, unacknowledged | `IngestSession.cs:535-550`; `ingest.md`, *Closes*; record 0063 | confirmed |
| 17 | `ContractWriter` serializes by a switch on the type whose default throws and is reported invalid | `ContractWriter.cs:36-39`, `:50-95` | confirmed |
| 14 | The largest message the bounds of N1 allow fits under the 24 MiB message bound | to verify: the Tester's bound test of AC7 writes that message with `ContractWriter` and reads it back (Tester, step 5); estimate 52,428 stations of about 60 bytes and 2,048 names of at most 1,530 escaped characters, under 14 MB | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | TradeRoutesFixtureReplayListsLastFrameRoutes; IngestEndpointReplaySentTradeRoutesFixtureStoresLastRoutes; ContractMessageConverterToTradeRoutesKeepsRoutesInOrder; PipeTickBuilderTradeRoutesDeliversItemAndKeepsOpenTick; PipeReplayRunnerReplayWithServerSendsTradeRoutesWithReadStamp | tests/Censio.Server.Tests/TradeRoutesTests.cs; tests/Censio.Server.Tests/IngestEndpointTests.cs; tests/Censio.Companion.Core.Tests/ContractMessageConverterTests.cs; tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs; tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs |
| AC2 | PageNamesOrderTradeRoutesByProblemsThenNameFolderAndId; PageNamesTradeRoutesSummaryCountsRoutesWithProblems; TradeRoutesCountsAboveZeroShowErrorColour | tests/Censio.Server.Tests/PageNamesTests.cs; tests/Censio.Server.Tests/TradeRoutesTests.cs |
| AC3 | MainLayoutListsTradeRoutesEntryAfterRegions; PageAddressTradeRoutesEntryLinkFollowsProfileAddress; TradeRoutesNewestAddressFollowsNewestProfile; TradeRoutesUnknownOrMalformedProfileShowsNotFound; ProgramGetTradeRoutesPagesAnswerWithoutLogin | tests/Censio.Server.Tests/MainLayoutTests.cs; tests/Censio.Server.Tests/PageAddressTests.cs; tests/Censio.Server.Tests/TradeRoutesTests.cs; tests/Censio.Server.Tests/ProgramTests.cs |
| AC4 | TradeRoutesNewerMessageUpdatesOpenPage; ServerStateStoreTradeRoutesAsyncOlderKeepsNewer; ServerStateStoreProfileAsyncHelloKeepsHeldTradeRoutes; ServerStateStoreTickAsyncNewerOrOlderTickKeepsHeldTradeRoutes; ServerStoreStoreTradeRoutesOlderOrResentWritesNothing; RegionsIgnoresTradeRoutesNotice | tests/Censio.Server.Tests/TradeRoutesTests.cs; tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Data.Tests/ServerStoreTests.cs; tests/Censio.Server.Tests/RegionsTests.cs |
| AC5 | TradeRoutesWithoutOrEmptyTradeRoutesShowsSpecTexts; PageNamesTradeRouteStationAndFolderFallbacks; TradeRoutesMarkupNameShownAsText; TradeRoutesNewestWithoutProfileShowsNoData; UiTextsTradeRoutesTextsMatchSpec | tests/Censio.Server.Tests/TradeRoutesTests.cs; tests/Censio.Server.Tests/PageNamesTests.cs; tests/Censio.Server.Tests/UiTextsTests.cs |
| AC6 | ServerStateRestoreAsyncKeepsLatestTradeRoutes; ServerStoreReadProfileStateIncludesTradeRoutes; ServerStateStoreTickAsyncEnteringProfileReadsTradeRoutes; ServerStoreDeleteProfileRemovesTradeRoutes; HistoryStoreClearDeletedLeavesNoRouteNameInFiles; ServerStoreStoreTradeRoutesOfDeletedProfileCreatesProfileAgain; RegionsDeleteDialogShowsSpecTexts (adapted); ProfileViewBuilderWithoutTradeRoutesKeepsNoRoutes; PageReaderWithoutTradeRoutesFlagKeepsNoRoutes | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Data.Tests/ServerStoreTests.cs; tests/Censio.Server.Data.Tests/HistoryStoreTests.cs; tests/Censio.Server.Tests/RegionsTests.cs; tests/Censio.Server.Tests/ProfileViewBuilderTests.cs; tests/Censio.Server.Tests/PageReaderTests.cs |
| AC7 | ContractReaderTradeRoutesAtEveryBoundAccepted; ContractReaderTradeRoutesOverBoundRefusedTooLarge; ContractReaderTradeRoutesBrokenFormOrRuleRefused; ContractReaderTradeRoutesRefusalTextHoldsNoName; ContractWriterTradeRoutesOverBoundRefused; ContractWriterTradeRoutesWrittenReadsBackUnchanged; IngestSessionTradeRoutesRefusedAcknowledgedById; IngestSessionTradeRoutesTooLargeClosesWithoutStoring; ReplaySenderTradeRoutesCountsOnlyRefused | tests/Censio.Contracts.Tests/ContractReaderTests.cs; tests/Censio.Contracts.Tests/ContractWriterTests.cs; tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs |
| AC8 | ContractReaderVersionOneRefusedNamingBothVersions; IngestSessionHelloOfVersionOneRefusedWithSupportedTwo; ReplaySenderRefusedHelloOfVersionOneReportsVersionNotSupported; TrayMenuVersionNotSupportedNamesVersionTwo; ServerDatabaseOpenInitialSchemaWithTicksAddsTradeRoutesAndKeepsTicks | tests/Censio.Contracts.Tests/ContractReaderTests.cs; tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs; tests/Censio.Companion.Core.Tests/TrayMenuTests.cs; tests/Censio.Server.Data.Tests/ServerDatabaseTests.cs |
| AC9 | manual: Product Manager, on the PR's build (server from source or the CI image, Companion ZIP of the PR's CI run), before step 9: record a game with a route the game shows with a problem or a lost ship, replay it with `--server`, and see a count above 0 in the matching column | — |
| AC10 | ProfileViewBuilderTradeRoutesKeepRepeatsNegativesAndEmptyStations; TradeRoutesRepeatedIdAndRouteWithoutStationsShownAsReceived | tests/Censio.Server.Tests/ProfileViewBuilderTests.cs; tests/Censio.Server.Tests/TradeRoutesTests.cs |

AC2, AC10 and the colour part of AC2 use built messages with counts above 0, lost ships above ships and negative counts (spec, *Challenge* 4); the fixture serves AC1 only. AC7's bound test writes its largest message with `ContractWriter` and reads it back, which also settles fact 14. `IngestSessionTradeRoutesRefusedAcknowledgedById` sends a message that breaks a value rule with a usable id (an all-zero `profileId` or a non-UTC receive time) and asserts the acknowledgement by id with its refusal and nothing stored; `IngestSessionTradeRoutesTooLargeClosesWithoutStoring` asserts the 1009 close and nothing stored. The ingest code's refusal handling does not change.

## Approach

**Pipe.** `PipeTickBuilder.AddMessage` gets a case for `TradeRoutesMessage`: it adds `PipeTickResult.FromTradeRoutes(message)` and changes nothing else (no close, no quiet-period restart, `_endable` unchanged). Item offers still produce no item.

**Contract (record 0096).** `CurrentVersion` becomes 2; `InvalidText` reads `Contract message breaks a rule of contract version 2`. The message `tradeRoutes`: `id`, `profileId`, `receiveTime`, `routes`; a route `id`, `name`, `folder`, `majorProblems`, `minorProblems`, `ships`, `lostShips`, `stations`; a station `sessionGuid`, `islandId`, `areaIndex`. Bounds in the scanner (step 2, *too large*, counted by path before anything is built): `routes` items at most `MaxTradeRoutes` (1,024); `stations` items at most `MaxStationsPerRoute` (256) per route and `MaxStations` (52,428) per message; `routes[].name` and `routes[].folder` at most `MaxRouteNameBytes` (765) UTF-8 bytes after unescaping, after the raw-length check of six times the bound. The scanner gets the kinds Routes, Route, Stations, Station and RouteNameValue, classified like Islands and IslandNameValue: `name` and `folder` take 765 bytes only at the route position (1,024 elsewhere, as today), and the per-route station counter resets when each `stations` array opens, as `_pairs` does for a map (`ContractScanner.cs:563-566`). Value rules (`ContractRules`, also on write): `id` and `profileId` not all zero, UTC receive time, the same counts again, names valid UTF-8 (empty allowed); any int32 count, repeated route IDs and names, no routes and a route without stations are allowed. `ContainsNull` covers a null route or station. `ContractReader.GetRefusedId`, `IngestSession.GetId`, `ContractJsonContext`, `ContractReader.Deserialize` and `ContractWriter`'s serialize switch (`ContractWriter.cs:50-95`, with `ContractJsonContext.Default.ContractTradeRoutes`) add the type.

**Companion.** `ContractMessageConverter.ToTradeRoutes` copies routes and stations in order (station key: session GUID, island ID, area index; session ID byte, owner, flags and goods are dropped), receive time to UTC. `ReplaySender.SendTradeRoutesAsync` sends it with `isTick` false, so `<n>` counts ticks only and a refusal counts in `<r>` (E10, fact 6); a message the writer refuses counts as refused unsent, as for ticks. `PipeReplayRunner.SendItemAsync` sends a `TradeRoutes` item with the last read stamp (fact 7). The live connection (`PipeConnection`) ignores the item. `TrayMenu.cs:373` reads `server does not support contract version 2`.

**Database (record 0097).** Migration `AddTradeRoutes` (made with `dotnet tool run dotnet-ef migrations add`) adds `TradeRouteSets` (`ProfileId` key, `MessageId`, `ReceiveTicks`), `TradeRoutes` (`Id`, `ProfileId`, `Position`, `RouteId`, `Name`, `Folder`, the four counts; unique `ProfileId, Position`) and `TradeRouteStations` (`Id`, `TradeRouteId`, `Position`, `SessionGuid`, `IslandId`, `AreaIndex`; unique `TradeRouteId, Position`), internal rows `TradeRouteSetRow`, `TradeRouteRow`, `TradeRouteStationRow`. `ServerStore.StoreTradeRoutesAsync`, in one write transaction: `AlreadyStored` without a write when the stored set has the message's id or a later receive time; otherwise `INSERT OR IGNORE` the profile (as a tick does, so a message during a deletion's steps recreates it), delete the profile's set and insert the new one in order, `Stored`. `ReadTradeRoutesAsync` reads the set in position order or `null`; `ReadProfileStateAsync` reads it in its transaction into `LatestTradeRoutes`. `DeleteProfileAsync` deletes the three tables' rows of the profile in its transaction (at most 53,453 rows by N1), so secure delete and the clean-up's checkpoint cover the names.

**Server state.** `ServerState.StoreTradeRoutesAsync` under the write gate: store; on `Stored`, a held profile's state is replaced by a copy with the new `LatestTradeRoutes`; a profile not in the list is added unnamed as for a tick; notice `TradeRoutes` with the profile, plus `Profile` when the profile is new. Every rebuild of a held state carries `LatestTradeRoutes` over: `StoreProfileAsync` (a hello, `:225-242`) and both held branches of `HoldTickAsync` (a newer tick, and an older tick with the island list read again); for an entering profile `HoldTickAsync` reads `ReadTradeRoutesAsync` beside the island list; `LatestTradeRoutes` is `required` (nullable), so the compiler finds every construction; a failure there counts as the islands-read failure (*Held profiles*). `IngestSession.StoreMessageAsync` stores the message; a `Failed` store is not acknowledged, as for a tick (record 0068).

**Pages.** `DataPageBase` calls `Reader.ReadAsync(profileId, island, ReadsTradeRoutes, token)`, decides no data by `HasData(view)` (default: the current `AsOf`/`Regions` rule) and ignores a `TradeRoutes` notice unless `ReadsTradeRoutes`; the newest-profile rule is unchanged. `ProfileViewBuilder.Build(…, tradeRoutes: true)` sets `ProfileView.TradeRoutes` from `LatestTradeRoutes` (null when none), each stop named from the same name map the regions use (island list, then latest tick), `null` when absent; with `false` the property stays null, so other pages keep no routes in memory (record 0074). The new page `TradeRoutes` (`/trade-routes` following the newest profile, `/profiles/{Profile}/trade-routes`) resolves its target as `Regions` does, `HasData` true, `ReadsTradeRoutes` true. `MainLayout` adds the entry after `Regions` with `Href` from `PageAddress.TradeRoutesEntryLink`, `Match` prefix, icon `Icons.Material.Filled.DirectionsBoat`.

Layout of the page, top to bottom: `PageTitle` `Censio – <heading>`; `h1` (`mud-typography-h4`) `Trade routes` / `Handelsrouten`; in mode Ready the profile label paragraph as on `Regions`; `DataStatus` with `AsOf` = `View.TradeRoutes?.AsOf` (banner always, as-of line only with routes); no deletion button. Then either the status text of `DataPageBase`, or `No trade routes received yet.` when `TradeRoutes` is null, `No trade routes in the latest data.` when it has no routes, else a paragraph with the summary and a `MudSimpleTable` (dense, hover) with the columns `Route`, `Folder`, `Ships`, `Lost ships`, `Major problems`, `Minor problems`, `Stations` (German `Route`, `Ordner`, `Schiffe`, `Verlorene Schiffe`, `Große Probleme`, `Kleine Probleme`, `Stationen`), the four counts `censio-number` with `PageFormat.Count`, a count above 0 in `<span class="mud-error-text">`, rows in `PageNames.OrderTradeRoutes` order, the stations cell the stops' shown names separated by ` → ` or `–` without stops, each name and each separator rendered as text of its own in a loop (no `string.Join` or concatenation per route), and `ProfileViewBuilder` gives each stop the name map's string, not a copy. Shown names: `PageNames.TradeRoute` (`Unnamed route` / `Unbenannte Route` for an empty name), `PageNames.Folder` (`–` for empty), `PageNames.Station` (`PageNames.Island` of the name, or `Island <session GUID>/<island ID>/<area index>` / `Insel …` without one). Every name is rendered as text. The summary takes `PageFormat.Number` for both counts, with the one-route form of S2.

Resource bound: a held state keeps at most one set per profile (two profiles); a page keeps the derived `TradeRoutesView` only on the trade-routes page (`ProfileViewBuilderWithoutTradeRoutesKeepsNoRoutes`, `PageReaderWithoutTradeRoutesFlagKeepsNoRoutes` through `FakeProfileStateSource`); a not-held read goes through `PageReader`'s one gate as for the other pages. Estimate at N1's bounds (52,428 stations, 1,024 routes with names of 255 characters): a held set about 4.5 MB (stations of 24 bytes, 1,024 station arrays of 2 KB, 2,048 names of about 540 bytes), two held about 9 MB; a page's view about 5 MB (stops of 32 bytes, their arrays, the route names; island names shared with the name map) and its render tree about 4 MB more (two text frames a stop), about 9 MB an open tab, against kilobytes for the fixture. The Dev measures both in a scratch program as `server-state.md` *Memory* measured the ticks and records them there and in `web-pages.md` *Reads and memory*. Like a page's island list (record 0074), the number of open tabs is not bounded by the server.

## Options considered

- Contract: a new type under version 1, or a server reading versions 1 and 2 — rejected in record 0096.
- Storage and display: the message's JSON as one value, in memory only, every message as history, a column on island pages — rejected in record 0097.
- Building the routes view on every page read — simpler, but every open page would keep up to 52,428 stops in its view against record 0074's reason for derived views; the flag costs one overload each in `PageReader` and `ProfileViewBuilder`.
- A notice of kind `Profile` for trade routes — every open page of the profile would re-read its trends from the database every two minutes; a kind of its own lets them ignore it.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Pipe | `PipeTickBuilder`, `PipeTickResult`, `PipeTickResultKind` | trade-routes item |
| Censio.Contracts | `ContractTradeRoutes`, `ContractTradeRoute`, `ContractTradeRouteStation` (new); `ContractMessageType`, `ContractReader`, `ContractScanner`, `ContractRules`, `ContractError`, `ContractJsonContext`, `ContractWriter` | version 2, new message |
| Censio.Companion.Core | `ContractMessageConverter`, `ReplaySender`, `PipeReplayRunner`, `TrayMenu` | convert and send |
| Censio.Server.Data | `StoredProfileState`, `ServerStore`, `CensioDbContext`, rows (new), migration `AddTradeRoutes` (new) | store, read, delete |
| Censio.Server | `ServerState`, `ServerStateChangeKind`, `IngestSession`, `ProfileView`, `ProfileViewBuilder`, `PageReader`, `PageAddress`, `PageNames`, `UiTexts`, `TradeRoutesView`, `TradeRouteView`, `TradeRouteStop` (new), `DataPageBase`, `MainLayout`, `TradeRoutes.razor` (new) | page |

## Signatures (for the Dev's skeleton)

Properties of a new class are listed in prose, each public, required and init-only unless said otherwise; a
backticked name is a member the skeleton adds.

Censio.Pipe:
- `PipeTickResultKind.TradeRoutes`, appended after the value EndOfStream.
- `PipeTickResult.TradeRoutes`: public get-only property of type TradeRoutesMessage, nullable; the private constructor takes it as a seventh parameter.
- `public static PipeTickResult FromTradeRoutes(TradeRoutesMessage message)`

Censio.Contracts:
- `ContractMessageType.TradeRoutes`, appended after the value Acknowledgement.
- `ContractReader.CurrentVersion`, now 2; new public int constants `ContractReader.MaxTradeRoutes` (1024), `ContractReader.MaxStationsPerRoute` (256), `ContractReader.MaxStations` (52428), `ContractReader.MaxRouteNameBytes` (765).
- `ContractTradeRoutes : ContractMessage`, a public sealed class: Id (Guid), ProfileId (Guid), ReceiveTime (DateTimeOffset), Routes (IReadOnlyList of ContractTradeRoute); Type overridden as for the tick message (TradeRoutes, property order -2).
- `ContractTradeRoute`, a public sealed class: Id (int), Name (string), Folder (string), MajorProblems, MinorProblems, Ships, LostShips (int each), Stations (IReadOnlyList of ContractTradeRouteStation).
- `ContractTradeRouteStation`, a public sealed class: SessionGuid (int), IslandId (byte), AreaIndex (byte).

Censio.Companion.Core:
- `public static ContractTradeRoutes ToTradeRoutes(TradeRoutesMessage message, Guid profileId, DateTimeOffset receiveTime)` (ContractMessageConverter)
- `public async Task<ReplaySendProblemKind?> SendTradeRoutesAsync(TradeRoutesMessage message, DateTimeOffset receiveTime, CancellationToken cancellationToken)` (ReplaySender)

Censio.Server.Data:
- `StoredProfileState.LatestTradeRoutes`: public required init property of type ContractTradeRoutes, nullable; every construction states it.
- `public Task<ServerStoreResult> StoreTradeRoutesAsync(ContractTradeRoutes routes, CancellationToken cancellationToken)` (ServerStore)
- `public Task<ContractTradeRoutes?> ReadTradeRoutesAsync(Guid profileId, CancellationToken cancellationToken)` (ServerStore)

Censio.Server:
- `ServerStateChangeKind.TradeRoutes`, appended.
- `public async Task<ServerStoreResult> StoreTradeRoutesAsync(ContractTradeRoutes routes, CancellationToken cancellationToken)` (ServerState)
- `ProfileView.TradeRoutes`: public init property of type TradeRoutesView, nullable and not required.
- `TradeRoutesView`, a public sealed class: AsOf (DateTimeOffset), Routes (IReadOnlyList of TradeRouteView).
- `TradeRouteView`, a public sealed class: Id (int), Name (string), Folder (string), MajorProblems, MinorProblems, Ships, LostShips (int each), Stations (IReadOnlyList of TradeRouteStop); and `TradeRouteView.HasProblem`, a public bool expression property, true when a problem count or the lost ships are above 0.
- `TradeRouteStop`, a public sealed class: Island (IslandKey), Name (string, nullable: no island list entry).
- `public static ProfileView Build(StoredProfileState state, PageCatalog? catalog, IReadOnlySet<string>? missingIcons, IslandKey? island, bool tradeRoutes)` (ProfileViewBuilder; the four-parameter form calls it without routes)
- `public async Task<PageRead> ReadAsync(Guid profileId, IslandKey? island, bool tradeRoutes, CancellationToken cancellationToken)` (PageReader; the three-parameter form calls it without routes)
- `public static string TradeRoutesLink(Guid profileId)`, `public static string TradeRoutesEntryLink(string? relativePath)` (PageAddress)
- `public static string TradeRoute(string name, UiLanguage language)`, `public static string Folder(string folder)`, `public static string Station(TradeRouteStop stop, UiLanguage language)`, `public static IReadOnlyList<TradeRouteView> OrderTradeRoutes(IReadOnlyList<TradeRouteView> routes, UiLanguage language)`, `public static string TradeRoutesSummary(IReadOnlyList<TradeRouteView> routes, UiLanguage language)` (PageNames)
- UiTexts, new required string properties: `UiTexts.TradeRoutesEntry`, `UiTexts.ColumnRoute`, `UiTexts.ColumnFolder`, `UiTexts.ColumnShips`, `UiTexts.ColumnLostShips`, `UiTexts.ColumnMajorProblems`, `UiTexts.ColumnMinorProblems`, `UiTexts.ColumnStations`, `UiTexts.UnnamedRoute`, `UiTexts.StationFallbackFormat`, `UiTexts.NoTradeRoutes`, `UiTexts.EmptyTradeRoutes`, `UiTexts.TradeRoutesSummaryFormat`, `UiTexts.TradeRoutesSummaryOneFormat`.
- `DataPageBase.ReadsTradeRoutes`: protected virtual bool property, false by default; `protected virtual bool HasData(ProfileView view)` (DataPageBase).
- Page `TradeRoutes` (public partial class, Components/Pages/TradeRoutes.razor and .razor.cs) with the parameter `TradeRoutes.Profile` (string, nullable).

Existing files the skeleton rewrites: ContractError.cs (the invalid text) and TrayMenu.cs (the version text), both final after the one literal. No name above is a keyword of another .NET language, no method has more than seven parameters, no type is nested.

## Test files

- tests/Censio.Pipe.Tests/PipeTickBuilderTests.cs
- tests/Censio.Contracts.Tests/ContractReaderTests.cs
- tests/Censio.Contracts.Tests/ContractWriterTests.cs
- tests/Censio.Companion.Core.Tests/ContractMessageConverterTests.cs
- tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs
- tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs
- tests/Censio.Companion.Core.Tests/TrayMenuTests.cs
- tests/Censio.Server.Data.Tests/ServerStoreTests.cs
- tests/Censio.Server.Data.Tests/ServerDatabaseTests.cs
- tests/Censio.Server.Data.Tests/HistoryStoreTests.cs
- tests/Censio.Server.Tests/TradeRoutesTests.cs (new)
- tests/Censio.Server.Tests/IngestEndpointTests.cs
- tests/Censio.Server.Tests/IngestSessionTests.cs
- tests/Censio.Server.Tests/ServerStateTests.cs
- tests/Censio.Server.Tests/ProfileViewBuilderTests.cs
- tests/Censio.Server.Tests/PageNamesTests.cs
- tests/Censio.Server.Tests/PageAddressTests.cs
- tests/Censio.Server.Tests/MainLayoutTests.cs
- tests/Censio.Server.Tests/RegionsTests.cs
- tests/Censio.Server.Tests/UiTextsTests.cs
- tests/Censio.Server.Tests/ProgramTests.cs

Existing tests whose input a criterion now handles differently, adapted by the Tester: `PipeTickBuilderTradeRoutesOrItemOffersKeepOpenTick` and `…DoNotRestartQuietPeriod` (the `TradeRoutes` row now gives one item); `PipeTickAssemblerFixturesGiveReadmeTicksAndEvents` and `PipeMessageReaderFixturesGiveReadmeFrameCounts` (the protocol 3 rows gain `TradeRoutes` items); the `contractVersion":1` literals and version assertions in `ContractReaderTests` and `ContractWriterTests` (about 77) and the 15 references to version 1 in the test projects; `RegionsDeleteDialogShowsSpecTexts` (dialog text); `ServerDatabaseTests` assertions on the migration list or the tables; the constructions of `StoredProfileState` in `ProfileViewBuilderTests`, `PageReaderTests`, `RegionIslandsTests` and `RegionsTests` add `LatestTradeRoutes = null` (the property is required). `tests/Censio.Server.Tests/Censio.Server.Tests.csproj` links `protocol-3-trade-routes.jsonl` as `long-session.jsonl` is linked (Tester). Apart from those constructions no production signature that a test calls changes: the new overloads keep every call site.

Files no unit test can cover: none — the generated migration, its designer file and the snapshot run in every database test, as the existing migrations do (no coverage exclusion exists for them).

## Areas

- pipe: *Ticks and sessions* — a trade-routes message delivers a trade-routes item at once, closes nothing; item offers produce none.
- contract: version 2 throughout, the eleventh message, its bounds row(s), value rules, refused id, privacy of route and folder names.
- replay-sending: what is sent, the counts (E10), the version text.
- ingest: the trade-routes message stored and acknowledged; hello of version 1 refused naming 2.
- server-data: the three tables, the latest-set rule, the deletion.
- server-state: `LatestTradeRoutes` in a profile's state, held, entering, restore, the `TradeRoutes` notice.
- history: *Deleting a profile* — trade routes removed in the deletion transaction, names covered by the clean-up.
- web-pages: seven routes (both enumerations of "five routes"), *Trade routes* section (layout above, texts, order, marks), the as-of exception, *Live updates* (the notice).
- web-ui: navigation entry, texts table, deletion dialog text.
- server: the route list at `server.md:42`.

## Documentation updates

All by the Dev once the code is green, against the code:
- the area documents above;
- `docs/areas/history.md` *Measurements*: two rows the Dev measures through the real `ServerStore` at N1's bounds, a full replace by `StoreTradeRoutesAsync` and the deletion's first transaction with 53,453 more rows, against the ingest's per-step guarantee there; `docs/areas/server-state.md` *Memory* and `docs/areas/web-pages.md` *Reads and memory*: the measured held set and page view (*Approach*, resource bound);
- `docs/ARCHITECTURE.md:61-62`: contract version 2, trade routes crossing and shown, records 0096 and 0097 linked;
- `README.md` (English and German): the data pages paragraph (`:16`, `:198`) names the trade-routes page; `:138` and its German twin: replay into a server of the same release;
- `docs/USER_GUIDE.md` (English and German): the pages list (`:440`, `:918`), the version text (`:363`, `:841`), the dialog text (`:456`, `:934`), a paragraph on the trade-routes page;
- `tests/Censio.Pipe.Tests/Fixtures/README.md` (Tester): the replayed sequences of both protocol 3 fixtures, and the link into `Censio.Server.Tests`;
- `.squad/project.md` (Dev reports the text, orchestrator writes): *Network exposure* adds `/trade-routes`; *Server database* names the trade-route tables; *Logging and showing external data* adds `TradeRoutes` (page); *Integration surface*, contract message: `ContractMessageConverter`.

## Architecture check

Guarantees of `.squad/project.md`: plain HTTP and open pages — the new routes need no login like every data page (record 0006); the ingest token still guards every stored message, the trade routes included; history and retention unchanged, and deletion still removes everything of a profile; the tick buffer guarantee is untouched. The pipe format stays confined to `Censio.Pipe` (record 0018): the server sees only the contract message. `docs/ARCHITECTURE.md` gets the flow update above.

## Security considerations

- [x] Limits: the route and folder names' 765 bytes apply after unescaping (UTF-8 of the decoded text; 255 pipe bytes may decode to 765 through U+FFFD), with the scanner's raw check of six times the bound before; route and station counts apply before anything is built; the 24 MiB message bound before parsing.
- [x] Exceptions: none reaches a user. Reader refusals carry fixed texts without content; a store failure is logged with the existing `LogStoreFailed` line (exception only, no message content) and left unacknowledged; a failed page read shows the fixed read-failure text.
- Names are rendered by Razor as text (E7); no name enters a log line, a refusal text or a `ToString` (message classes have none).

## Decision records

- `docs/decisions/0096-contract-version-2-adds-trade-routes-read-exactly.md` (Proposed)
- `docs/decisions/0097-latest-trade-routes-per-profile-stored-as-rows-on-a-page-of-their-own.md` (Proposed)

## Challenge

- 1 (major, held state loses its routes on a hello and on held ticks): accepted. *Approach*, server state: every rebuild carries `LatestTradeRoutes` (`StoreProfileAsync`, both held branches of `HoldTickAsync`), the entering branch reads it, and the property is `required` so the compiler finds every construction; tests `ServerStateStoreProfileAsyncHelloKeepsHeldTradeRoutes` and `ServerStateStoreTickAsyncNewerOrOlderTickKeepsHeldTradeRoutes` (AC4); fact 15.
- 2 (minor, ingest refusals follow the existing closes): accepted with Security finding 1: spec E6 and AC7 now say only an *invalid* message is acknowledged by id; the two ingest tests are described under *Tests per criterion*; the ingest code does not change; fact 16.
- 3 (minor, `ContractWriter` missing): accepted. Added to *Affected projects and types* and *Approach*; test `ContractWriterTradeRoutesWrittenReadsBackUnchanged` (AC7); fact 17.

## Security review

- 1 (blocking, E6 and AC7 promise acknowledgements the reader cannot give): accepted. Spec E6 and AC7 changed verbatim as proposed; `IngestSessionTradeRoutesTooLargeClosesWithoutStoring` added beside the acknowledgement test for *invalid* (AC7).
- 2 (blocking, page render memory unbounded in the plan): accepted. (a) each stop's name and separator are rendered as text of their own, the view takes the name map's strings; (b) the estimate is in *Approach*, and the Dev measures and records it in `server-state.md` *Memory* and `web-pages.md` *Reads and memory* (*Documentation updates*); (c) `ProfileViewBuilderWithoutTradeRoutesKeepsNoRoutes` and `PageReaderWithoutTradeRoutesFlagKeepsNoRoutes` (AC6, N3).
- 3 (non-blocking, write-gate steps unmeasured): fixed now: the Dev measures the full replace and the deletion's larger first transaction through the real `ServerStore` at N1's bounds and adds both rows to `history.md` *Measurements*; a row beyond the ingest's per-step guarantee goes back to the Lead before step 8.
- 4 (non-blocking, security areas): fixed now: *Security areas touched* names Ingest token and Companion authentication and Outbound calls.
- Noted sound: the scanner's per-route station counter reset and the positional 765-byte name bound are written into *Approach*.

## Out of scope / follow-ups

- Item offers: the follow-up issue the orchestrator creates (spec, Q1).
- Live sending (#40) reuses `ContractMessageConverter.ToTradeRoutes`.
