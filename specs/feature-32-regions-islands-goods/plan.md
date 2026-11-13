# Plan: Regions, islands and goods with live updates

Source: Issue #32 | [spec.md](spec.md)
Tier: security — the change adds routes reachable without login (three page routes and `/icons/{reference}`), which is network exposure by the table of `.squad/routing.md`.
Security areas touched: Network exposure (new page routes and the icon route in `Program.cs`, the icon response headers); Logging and showing external data (profile, island and catalog names and Companion-uploaded PNGs shown in a browser); Server database (`ServerState` gains the Companion's connection state, the held catalog lookup and an icon read; reads on demand from pages anyone can open); Parsing of external input (the page addresses, parsed by own code; the PNG header check)

## Problem / root cause

Feature: see [spec.md](spec.md).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The fixture has 24 island keys, 13/9/2 over session GUIDs 3245/6627/149679, and (6627, 10, 1) carries `Island 21` and `Island 25` | read-only decode of the type-3 frames of `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl` (Lead, scratch Python) | confirmed |
| 2 | The fixture's last tick has no data | `tests/Censio.Pipe.Tests/Fixtures/README.md`, *Facts* ("the last, without data") | confirmed |
| 3 | `ServerState` holds only identity and missing icons of the catalog, not the document; the document is read at restore and passed to `StoreCatalogAsync` | `src/Censio.Server/CatalogHold.cs:24-29`; `ServerState.cs` `RestoreCatalogAsync`, `StoreCatalogAsync(upload, document, …)` | confirmed |
| 4 | A catalog region entry is a session asset with `regionGuid` | `docs/areas/game-catalog-format.md` (*Document*), `CatalogDocumentEntry.RegionGuid` | confirmed |
| 5 | The ingest accepts `status` and keeps nothing; the Companion's place is taken in `DecideHelloAsync` and freed in `ReleasePlace` | `src/Censio.Server/IngestSession.cs:392-418, 496-501, 800-804` | confirmed |
| 6 | Followers are told only `Profile` and `Catalog` changes | `src/Censio.Server/ServerStateChangeKind.cs` | confirmed |
| 7 | `ServerState` is created after `app.Build()` and is not in DI | `src/Censio.Server/Program.cs` | confirmed |
| 8 | Every page runs interactive on the server (`Routes @rendermode="InteractiveServer"`) | `src/Censio.Server/Components/App.razor:12` | confirmed |
| 9 | bUnit is referenced by `Censio.Server.Tests`; it references Server and Contracts only and runs with invariant globalization | `tests/Censio.Server.Tests/Censio.Server.Tests.csproj` | confirmed |
| 10 | `Censio.Companion.Core` references Contracts, Pipe and GameData and offers `ContractMessageConverter.ToTick(PipeTick, Guid)` | its `.csproj`; `ContractMessageConverter.cs:19` | confirmed |
| 11 | `ServerStore.ReadIconAsync(string, …)` returns the stored PNG bytes or `null` | `src/Censio.Server.Data/ServerStore.cs:297` | confirmed |
| 12 | An `<img alt="">` whose source answers `404` shows no broken-image glyph in current Chrome, Edge and Firefox | to verify: manual, PM in Chrome or Edge and Firefox on the step-9 build (task 15); HTML says an image with empty alt represents nothing | unverified |
| 13 | The palette values of *Approach* for the delta colours reach 4.5:1 on the surface | to verify: the contrast test of AC10 (Tester, step 5); the Dev adjusts a failing value | unverified |
| 14 | `-new Date(iso).getTimezoneOffset()` gives the browser's offset in minutes at that instant, daylight saving included | ECMAScript specification of `Date.prototype.getTimezoneOffset` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PageReaderFixtureWithoutCatalogGivesThreeRegions`, `PageReaderFixtureGivesLatestNamesAndGoods`, `PageReaderFixtureFinalEmptyTickKeepsIslandsWithoutValues`, `PageReaderFixtureCatalogMergesSessionsOfOneRegion` | `tests/Censio.Server.Tests/PageReaderTests.cs` |
| AC2 | `ProfileViewBuilderGroupsSessionsByRegionGuid`, `ProfileViewBuilderSessionWithoutEntryIsOwnRegion`, `ProfileViewBuilderNamesRegionBySmallestSession`, `ProfileViewBuilderSumsFiguresOverIslandsWithValues`, `ProfileViewBuilderRegionWithoutValuesHasNoFigures`; `PageNamesOrdersByNameIgnoringCaseThenGuid`, `PageNamesEqualRegionNamesGetGuid` | `ProfileViewBuilderTests.cs`, `PageNamesTests.cs` |
| AC3 | `RegionIslandsShowsIslandsWithFiguresAndLinks`, `RegionIslandsIslandWithoutValuesShowsDash`, `RegionIslandsEmptyIslandNameShowsUnnamed` | `RegionIslandsTests.cs` |
| AC4 | `IslandDetailShowsColumnsInOrder`, `IslandDetailNullValueShowsDash`, `IslandDetailDeltaHasSignAndColour`, `IslandDetailTinyDeltaIsNeutralAndNoDeficit` (-0.004, 0.004), `IslandDetailShowsFirst500EntriesByName`; `PageFormatFormatsRatesPerLanguage`, `PageFormatFormatsDeltaWithSign` (incl. -0.004 gives `0.00`), `PageFormatFormatsPercentAsStored` (87.4, 435) | `IslandDetailTests.cs`, `PageFormatTests.cs` |
| AC5 | `PageNamesTakesShownLanguageThenOtherThenFallback`, `IslandDetailIconOnlyWhenStored`, `IslandDetailLanguageSwitchChangesNames`; `IconEndpointServesStoredPng` (a `PngWriter.Write` image; asserts `image/png`, `nosniff`, the cache header and the content security policy), `IconEndpointRefusesNonPng`, `IconEndpointRefusesMalformedReference` (DataRows: 63 and 65 characters, 64 uppercase hex, a non-hex character at position 64), `IconEndpointUnknownReferenceIsNotFound`, `IconEndpointAbortedRequestCancelsRead`; `PngHeaderAcceptsSignatureAndIhdr` (a `PngWriter.Write` image), `PngHeaderRefusesShortOrWrongBytes`; manual: fact 12 | `PageNamesTests.cs`, `IslandDetailTests.cs`, `IconEndpointTests.cs`, `PngHeaderTests.cs` |
| AC6 | `RegionsTickOfShownProfileUpdatesWithoutReload`, `RegionsTickOfOtherProfileKeepsPickedProfile`, `RegionsCatalogChangeUpdatesNames`, `MainLayoutProfileSwitchListsNewProfileLive`; manual: N3's 2 seconds, PM on the step-9 build with a replayed recording | `RegionsTests.cs`, `MainLayoutTests.cs` |
| AC7 | `MainLayoutProfileSwitchDisabledWithoutProfiles` (replaces `MainLayoutProfileSwitchIsDisabledWithLabelOnly`), `MainLayoutProfileSwitchListsByNameWithIdentityForEqualNames`, `MainLayoutProfileSwitchListsAtMost100`, `RegionsWithoutPickFollowsNewestProfile`, `RegionsPickedProfileComesFromAddress` | `MainLayoutTests.cs`, `RegionsTests.cs` |
| AC8 | `DataStatusShowsAsOfAtBrowserOffset`, `DataStatusShowsUtcWithoutOffset`, `DataStatusOutOfRangeOffsetShowsUtc` (841, -841, `int.MaxValue`), `DataStatusInteropFailureShowsUtc` (`JSException`, `JsonException`), `DataStatusBannerPerCompanionState`, `DataStatusBannerChangesLive`; `ServerStateCompanionChangesNotifyFollowers`, `IngestSessionStatusSetsCompanionState`, `IngestSessionDisconnectClearsCompanionBeforePlaceIsFree` | `DataStatusTests.cs`, `ServerStateTests.cs`, `IngestSessionTests.cs` |
| AC9 | `RegionsWithoutDataShowsNoDataText`, `RegionsUnknownOrMalformedAddressShowsNotFound` (DataRows: unknown GUID, `x`, 2,000 characters, `%3Cb%3E`), `RegionsReadFailureShowsReloadText`, `RegionsWaitingReadShowsLoading`, `RegionsDisposedPageCancelsItsRead`; `ProgramGetRegionsShowsNoDataInFrame`, `ProgramGetMalformedProfileShowsNotFoundInFrame`, `ProgramGetUnknownIconIsEmptyNotFound` | `RegionsTests.cs`, `ProgramTests.cs` |
| AC10 | `PageReaderNotHeldReadsOneAtATime`, `PageReaderEvictedBetweenCheckAndReadReadsInsideGate`, `PageReaderCancelledWaiterLeavesWithoutRead`, `ProfileViewBuilderViewHoldsNoState`, `RegionsPageHoldsNoState` (weak reference to the fake source's state collected after render), `IslandDetailNameWithMarkupShowsCharacters`, `RegionsRegionNameWithMarkupShowsCharacters` (table, `h1`, `PageTitle`), `MainLayoutProfileNameWithMarkupShowsCharacters`, `UiTextsEveryPageTextHasBothLanguages`, `CensioThemeDeltaColoursMeetTextContrast` | `PageReaderTests.cs`, `ProfileViewBuilderTests.cs`, `RegionsTests.cs`, `IslandDetailTests.cs`, `MainLayoutTests.cs`, `UiTextsTests.cs`, `CensioThemeTests.cs` |

All files lie in `tests/Censio.Server.Tests/`.

## Approach

**State (G1).**
- `CompanionConnection` (immutable: connected or not, last `ContractPipeState?`). `ServerState` keeps one under its lock and gains `CompanionState`, `SetCompanionConnected()`, `SetCompanionPipeState(state)`, `SetCompanionDisconnected()`; each swaps and notifies `ServerStateChangeKind.Companion` only when the value changes. `IngestSession` calls `SetCompanionConnected` after `LogCompanionAccepted`, `SetCompanionPipeState` for an accepted `status`, and in `ReleasePlace`, when the hello was accepted, `SetCompanionDisconnected` **before** `ReleaseCompanion()`, so a next Companion's connect cannot be overwritten.
- `CatalogHold` gains `Lookup` (`PageCatalog?`): built from the document in `RestoreCatalogAsync` and `StoreCatalogAsync`, carried over in the icon path of `StoreIconAsync`, `null` in `CatalogHold.None`. `PageCatalog` keeps per GUID the kind, the `en` and `de` names, the region GUID and the icon reference (no region types).
- `ServerState.NewestProfile`: the held profile with the greatest (`ReceiveTicks`, `Sequence`), or `null`. `ServerState.Catalog` exposes the snapshot's `CatalogHold`. `ServerState.ReadIconAsync` reads through the store behind its own one-at-a-time gate.
- `ServerState` implements the new `IProfileStateSource` (`TryGetHeld`, `ReadProfileStateAsync`, `Catalog`), so `PageReader` can be tested with a hand-written fake. `TryGetHeld` answers from one snapshot read, so the held check and the held state cannot disagree. `CatalogHold` becomes public (it is `internal`, `CatalogHold.cs:8`, and the interface exposes it).

**Views (G2).**
- Rows: the first 500 entries of the island in tick order are kept and shown ordered by name (E10 reworded so); a sort before the cut would keep every entry for each language.
- `ProfileViewBuilder.Build(state, catalog, missingIcons, island)` derives a `ProfileView` and returns nothing that references the state: profile identity and stored name, as-of time, regions (`RegionView`: region GUID or session GUID, its session GUIDs, names in both languages or `null`, island count, `KeyFigures?`), islands (`IslandView`: key, name, `KeyFigures?`), and only for the requested island its `GoodRow`s (the first 500 in tick order before sorting, plus the total count). Names are resolved for both languages at build, so a language switch needs no read. A row's icon reference is kept only when the catalog names it and it is not missing.
- Key figures per island: buildings = sum of `BuildingCount`, produced goods = entries with `BuildingCount > 0`, goods in deficit = entries with `Delta < 0` (`null` not counted); an island the latest tick lacks or that has no entries has none. Region figures sum its islands with figures, `null` when none has.
- `PageNames`: shown name per language with the spec's fallbacks (E3, E4, E5), ordinal case-ignoring sort with ties by GUID or key, and the appended GUID for equal region names / first eight hex digits of the identity for equal profile names.
- Rounding rule (written into `web-pages.md`): delta, its sign, its colour and the "goods in deficit" count follow the value rounded to two decimals (`Math.Round`, midpoint away from zero), so -0.004 shows `0.00`, neutral, and is no deficit; a rounded negative zero shows `0.00`.
- `PageFormat`: rates `N2` of the invariant culture, separators swapped for German (`1.234,50`); delta adds `+` when the rounded value is above zero; productivity is already a percent (`docs/protocol.md`, up to 435) and is shown rounded to a whole number, not multiplied (`87%` / `87 %`); time `yyyy-MM-dd HH:mm` at a given offset, or in UTC with ` UTC` when the offset is `null`.
- `PageReader` (singleton): `ReadAsync(profileId, island, cancellationToken)` builds the view from `TryGetHeld` without waiting; otherwise it waits on one `SemaphoreSlim(1, 1)` with the token and calls `ReadProfileStateAsync` only inside it (that call may still find the profile held and read nothing). It builds the view inside the gate and returns a `PageRead` (view, unknown, or failed). A cancelled token ends the call with `OperationCanceledException` before or during the read; the page catches it and does nothing (no log, no E8). The state is a local and is dropped on return.
- Each page owns a `CancellationTokenSource`, cancelled and disposed in `Dispose`, and passes its token to every read, so a prerender or a closed tab leaves the gate.

**Icons (G3).** `IconEndpoint.MapCensioIcons(endpoints, state)` maps `GET /icons/{reference}`, passing `HttpContext.RequestAborted` to `ReadIconAsync` and its gate: a reference that is not 64 lowercase hex, an unknown icon, or bytes that fail `PngHeader.IsPng` (8-byte signature, then chunk length 13 and type `IHDR`) answer `404` with an empty body; otherwise `200`, `Content-Type: image/png`, `X-Content-Type-Options: nosniff`, `Cache-Control: public, max-age=31536000, immutable` (the reference is the content hash), `Content-Security-Policy: default-src 'none'; sandbox`. The read bytes stay in memory after the gate is released until the response is written: up to 17,825,792 bytes per request in flight (`contract.md`, *Bounds*), unbounded in number like the ingest's accepted risks (`ingest.md`, *Accepted risks*); documented in `web-pages.md`. A database failure is not caught (`500`, empty body, by `server.md`).

**Pages (G4).**
- Routes: `/regions` (newest profile, follows), `/profiles/{profile}/regions`, `/profiles/{profile}/sessions/{session}` (the island list of the region holding that session; links use the region's smallest session GUID), `/profiles/{profile}/islands/{session}/{island}/{area}`. Parameters are strings parsed by `PageAddress.TryParse…` (E7's `Not found.` covers only addresses that match one of the four routes; any other path keeps the empty `404` of `server.md`, as `Routes.razor` has no not-found content) (`Guid.TryParseExact` format `D`; `int`/`byte` invariant, digits and an optional leading minus for the GUID only); any failure, or a profile, region or island the view lacks, shows `Not found.` and nothing is logged.
- Each page subscribes to `ServerState.Changed` in `OnInitialized` and unsubscribes in `Dispose`. The handler only calls `InvokeAsync` without awaiting, never blocks; a page re-reads on a `Profile` notice of its profile (the overview without a pick: on any `Profile` notice whose `NewestProfile` differs or is its own) and on `Catalog`; it re-renders on `Companion`. A notice during a read sets a pending flag and causes one more read afterwards (E13). A page keeps only its `ProfileView`.
- `DataStatus` component: banner (`MudAlert`, severity warning) above the as-of line `As of <time>`; it calls `censio.utcOffsetMinutes(isoInstant)` in `OnAfterRenderAsync` for each new as-of time and re-renders with the offset. The answer is untrusted: an offset outside -840 to 840 minutes counts as none (`DateTimeOffset.ToOffset` throws beyond 14 hours), and a `JSException`, `JsonException`, `JSDisconnectedException` or cancellation keeps UTC; neither is logged. `PageFormat.Time` applies the same bound.
- `MainLayout`: the profile switch becomes a `MudMenu` (label `Profile`, disabled without profiles) listing at most 100 profiles by shown name as links to `/profiles/{id}/regions`, then the E9 line; it follows `Profile` notices live. The navigation gains `Regions` (`/regions`).
- `UiTexts` gains every text of the spec and the layout below; `CensioTheme` sets palette `Error` `#A4161A` / `#FF8A80` and `Success` `#1B6B2F` / `#7FD08E` (light / dark); negative delta uses `Error`, positive `Success`.
- `Program.cs`: `ServerState? state = null;` before `builder.Build()`, `builder.Services.AddSingleton(_ => state ?? throw new InvalidOperationException(...))` and `AddSingleton<IProfileStateSource>` likewise, `AddSingleton<PageReader>()`; the factory runs only when a component is created, after the restore. `app.MapCensioIcons(state)` beside the ingest.

**Layout** (all pages inside the frame):
- Region overview: `h1` `Regions` / `Regionen`; a line with the profile's shown name; `DataStatus`; a table with the columns `Region`, `Islands` / `Inseln`, `Buildings` / `Gebäude`, `Produced goods` / `Produzierte Waren`, `Goods in deficit` / `Waren im Defizit`, the region name a link, numbers right-aligned.
- Island list: `h1` the region's name; the profile line; a link `All regions` / `Alle Regionen`; `DataStatus`; a table `Island` / `Insel` and the three figure columns, the island name a link.
- Island detail: `h1` the island's name; a line `<region> · <profile>`; a link back to the region; `DataStatus`; a table with an unlabelled 24-pixel icon column (`<img alt="">`), `Good` / `Ware`, `Production` / `Produktion`, `Consumption` / `Verbrauch`, `Delta`, `Buildings` / `Gebäude`, `Productivity` / `Produktivität`; the E10 line under the table; the E2 text instead of the table.
- `PageTitle` `Censio – <h1 text>`. E1, E7, E8 texts replace the content below `h1`.

## Options considered

1. **Group by session GUID** (the first spec) — lost to the spec challenge: several sessions share a region asset (record 0073).
2. **One read per page, no shared gate; or a shared cache of the last not-held state** — the first leaves reads unbounded (server-state, *Reading on demand*), the second keeps a state alive past its pages and must be invalidated; the gate with derived views wins (record 0074).
3. **Banner state in a separate `CompanionStatus` service** instead of `ServerState` — a second notification path for the pages to follow; one `Changed` event is simpler (record 0075).
4. **Icons inline as `data:` URIs** instead of a route — no new route, but every row's PNG travels through the circuit at every update and is never cached (record 0076).
5. **The profile choice in a cookie** (as theme and language) — record 0071's cookies carry no identity, and a cookie cannot give two tabs two profiles; the address keeps it (record 0077).
6. **The server's time zone, or UTC only** — the image has no tzdata and the server's zone is not the player's; the browser's offset per instant (record 0078).
7. **Hold the parsed `CatalogDocument`** instead of `PageCatalog` — keeps region types and the document's lists alive for nothing; one option otherwise.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `ServerState.cs`, `ServerStateChangeKind.cs`, `CatalogHold.cs` | Companion state, `NewestProfile`, `Catalog`, `ReadIconAsync`, `Lookup`, kind `Companion` |
| Censio.Server | `CompanionConnection.cs`, `IProfileStateSource.cs`, `PageCatalog.cs`, `PageCatalogEntry.cs` | new |
| Censio.Server | `IngestSession.cs` | status and connection calls |
| Censio.Server | `ProfileView.cs`, `RegionView.cs`, `IslandView.cs`, `GoodRow.cs`, `KeyFigures.cs`, `IslandKey.cs`, `ShownName.cs`, `PageRead.cs`, `ProfileViewBuilder.cs`, `PageReader.cs`, `PageNames.cs`, `PageFormat.cs`, `PageAddress.cs` | new |
| Censio.Server | `IconEndpoint.cs`, `PngHeader.cs` | new |
| Censio.Server | `Components/Pages/Regions.razor(.cs)`, `RegionIslands.razor(.cs)`, `IslandDetail.razor(.cs)`, `Components/DataStatus.razor(.cs)` | new |
| Censio.Server | `Components/Layout/MainLayout.razor(.cs)`, `UiTexts.cs`, `CensioTheme.cs`, `wwwroot/js/censio.js`, `Program.cs` | changed |
| tests | `Censio.Server.Tests.csproj` | reference `Censio.Companion.Core`; link `..\Censio.Pipe.Tests\Fixtures\long-session.jsonl` as `Fixtures\Pipe\long-session.jsonl`, copied to output (as `Censio.GameData.Tests.csproj:9`) |

## Signatures (for the Dev's skeleton)

All types in namespace Censio.Server (components under Censio.Server.Components), sealed unless static or an interface. Properties are written as name (type, accessors); "required" means a required init-only property.

- ServerStateChangeKind: new member `Companion`.
- ServerState: `public void SetCompanionConnected()`, `public void SetCompanionPipeState(ContractPipeState pipeState)`, `public void SetCompanionDisconnected()`, `public Task<byte[]?> ReadIconAsync(string reference, CancellationToken cancellationToken)`, `public bool TryGetHeld(Guid profileId, out StoredProfileState? state)`; properties `CompanionState` (CompanionConnection, get), `NewestProfile` (Guid?, get), `Catalog` (CatalogHold, get); it implements `IProfileStateSource`.
- `public interface IProfileStateSource`: `bool TryGetHeld(Guid profileId, out StoredProfileState? state)`, `Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)`, property `Catalog` (CatalogHold, get).
- CatalogHold: changes from internal to public.
- `public class CompanionConnection`: `Disconnected` (static, get), `IsConnected` (bool, required), `PipeState` (ContractPipeState?, required).
- CatalogHold: `Lookup` (PageCatalog?, required).
- `public class PageCatalog`: `public static PageCatalog From(CatalogDocument document)`, `public PageCatalogEntry? Find(int assetGuid)`.
- `public class PageCatalogEntry`: `Kind` (CatalogEntryKind), `English` (string?), `German` (string?), `RegionGuid` (int?), `IconReference` (string?), all required.
- `public readonly record struct IslandKey(int SessionGuid, byte IslandId, byte AreaIndex)`.
- `public class ShownName`: `English`, `German` (string, required), `public string For(UiLanguage language)`.
- `public class KeyFigures`: `Buildings`, `ProducedGoods`, `GoodsInDeficit` (int, required).
- `public class IslandView`: `Key` (IslandKey), `Name` (string), `Figures` (KeyFigures?), all required.
- `public class RegionView`: `RegionGuid` (int), `IsSession` (bool), `SessionGuids` (IReadOnlyList of int), `Name` (ShownName), `Islands` (IReadOnlyList of IslandView), `Figures` (KeyFigures?), all required.
- `public class GoodRow`: `ProductGuid` (int), `Name` (ShownName), `IconReference` (string?), `Production`, `Consumption`, `Delta`, `Productivity` (float?), `Buildings` (int), all required.
- `public class ProfileView`: `ProfileId` (Guid), `ProfileName` (string?), `AsOf` (DateTimeOffset?), `Regions` (IReadOnlyList of RegionView), `Island` (IslandKey?), `Rows` (IReadOnlyList of GoodRow), `RowCount` (int), all required; constant `MaxRows` = 500.
- `public static class ProfileViewBuilder`: `public static ProfileView Build(StoredProfileState state, PageCatalog? catalog, IReadOnlySet<string>? missingIcons, IslandKey? island)`.
- `public class PageRead`: `View` (ProfileView?), `IsFailed` (bool), both required.
- `public class PageReader`: `public PageReader(IProfileStateSource source)`, `public Task<PageRead> ReadAsync(Guid profileId, IslandKey? island, CancellationToken cancellationToken)`.
- `public static class PageNames`: `public static ShownName Good(int productGuid, PageCatalog? catalog)`, `public static ShownName Region(int regionGuid, bool isSession, int smallestSession, PageCatalog? catalog)`, `public static string Island(string name, UiLanguage language)`, `public static IReadOnlyList<KeyValuePair<Guid, string>> Profiles(IReadOnlyList<StoredProfile> profiles, UiLanguage language, int limit)`, `public static IReadOnlyList<RegionView> OrderRegions(IReadOnlyList<RegionView> regions, UiLanguage language)`, `public static string RegionText(RegionView region, IReadOnlyList<RegionView> regions, UiLanguage language)`.
- `public static class PageFormat`: `public static string Rate(float? value, UiLanguage language)`, `public static string Delta(float? value, UiLanguage language)`, `public static string Percent(float? value, UiLanguage language)`, `public static string Count(int? value)`, `public static string Time(DateTimeOffset value, int? offsetMinutes)`.
- `public static class PageAddress`: `public static bool TryParseProfile(string? text, out Guid profileId)`, `public static bool TryParseSession(string? text, out int sessionGuid)`, `public static bool TryParseByte(string? text, out byte value)`.
- `public static class PngHeader`: `public static bool IsPng(ReadOnlySpan<byte> data)`.
- `public static class IconEndpoint`: constant `IconPath` = "/icons/{reference}", `public static IEndpointConventionBuilder MapCensioIcons(IEndpointRouteBuilder endpoints, ServerState state)` (an extension method).
- Components: `public class DataStatus` with parameter `AsOf` (DateTimeOffset?); `public class Regions` with parameter `Profile`; `public class RegionIslands` with `Profile`, `Session`; `public class IslandDetail` with `Profile`, `Session`, `Island`, `Area` (all string?, component parameters).
- UiTexts: one public string property per new text (PascalCase, chosen by the Dev). censio.js: `utcOffsetMinutes(isoInstant)`.

The skeleton rewrites `ServerState.cs`, `CatalogHold.cs` (every construction site of `CatalogHold` in `ServerState.cs` sets `Lookup`), `IngestSession.cs`, `MainLayout.razor(.cs)`, `UiTexts.cs` (constructor grows; the Dev may switch to an object initializer), `Program.cs`.

## Test files

`tests/Censio.Server.Tests/`: `PageReaderTests.cs`, `ProfileViewBuilderTests.cs`, `PageNamesTests.cs`, `PageFormatTests.cs`, `PageAddressTests.cs` (covering the parsers behind AC9), `ProgramTests.cs` (the real host through `CensioServerFactory`), `PngHeaderTests.cs`, `IconEndpointTests.cs`, `RegionsTests.cs`, `RegionIslandsTests.cs`, `IslandDetailTests.cs`, `DataStatusTests.cs`, and the existing `ServerStateTests.cs`, `IngestSessionTests.cs`, `MainLayoutTests.cs`, `UiTextsTests.cs`, `CensioThemeTests.cs`. AC1 replays the fixture through `PipeReplayStream`, `PipeTickAssembler` and `ContractMessageConverter.ToTick` into a real `ServerState` over an in-memory database.

Existing call sites: `MainLayoutTests.CreateContext` and `AppTests` render the layout, which now injects `ServerState` — the Tester registers a `ServerState` over an in-memory database there. The Tester rewrites the placeholder assertions: `MainLayoutTests.cs:195` (`button[disabled]` in the language test, now the menu's label) and `MainLayoutProfileSwitchIsDisabledWithLabelOnly` (`:261`, replaced by `MainLayoutProfileSwitchDisabledWithoutProfiles`). `ProgramTests.cs:236` (unknown path, empty `404`) stays unchanged: its path matches no new route. `CatalogHold` gains a required member: only `ServerState.cs` constructs it (Dev). `UiTextsTests` assertions on the constructor are unaffected (it is private).

Files no unit test can cover: `wwwroot/js/censio.js` (no JavaScript test runner; its new function is exercised in bUnit through an injected interop result and by hand at step 9) — already outside the .NET coverage measure, no new exclusion.

## Areas

- New `docs/areas/web-pages.md`, created with its index row early (task 1a, so `decision-check.py` finds the area of records 0073, 0074, 0076–0078) and filled in G5. Its *Limits and errors* states: the rounding rule; the first 500 entries in tick order, shown by name; E7 only for addresses matching a route; the browser offset bounded to -840..840 minutes, otherwise UTC; icon bytes held until the response is written (up to 17,825,792 bytes each, pointer to `ingest.md` *Accepted risks*); a page's memory proportional to the island count of its profile, which is unbounded (`server-state.md`, *Memory*). (Index row in `docs/areas/README.md`: scope "the data pages: region overview, island list, island detail, key figures, names and fallbacks, number and time formats, row and profile limits, live updates, the as-of line and banner, addresses and their failures, the bounded reads, the icon route, logging"; not here: the frame (web-ui), the state (server-state)).
- `web-ui.md`: profile switch (function, limit, live), `Regions` entry, palette `Error`/`Success` and the contrast pairs, the new texts, `censio.js`'s third function; scope and *Not here*; the index row of `web-ui` loses "profile switch placeholder".
- `server-state.md`: *Privacy* gains "and sends none anywhere except to the data pages, which show them to anyone who reaches the server ([`web-pages.md`](web-pages.md))"; the Companion connection and its notice kind, `NewestProfile`, the held catalog lookup and its memory, the icon read gate; the pages as callers (*Reading on demand*, *Followers*); *Scope* and *Not here*.
- `ingest.md`: `status` and connect/disconnect set the Companion state (*Messages after the hello*, *One Companion*); line 11 and *Not here*.
- `server.md`: `/icons/{reference}` and the page routes among the routes without login; *Limits and errors*, first bullet: the data pages read their address segments and `/icons/{reference}` its reference, parsed as `web-pages.md` says, and the browser's UTC offset is bounded there.
- `contract.md`: *Security considerations for the endpoint*: the PNG check is #32's, at the icon route.

## Documentation updates

All by the Dev once the code is green:
- the area documents above, and `docs/areas/README.md` (rows of web-pages, web-ui, ingest, server-state);
- `docs/ARCHITECTURE.md` line 63: replace "no page shows it yet (#32)" with the pages, linking `areas/web-pages.md` and records 0073–0078; line 93: the pages and `/icons/` need no login;
- `README.md` lines 24 and 26 and `docs/USER_GUIDE.md` *Web interface* / *Weboberfläche* (around 363 and 752), line 99 and its German counterpart ("no page shows data yet"), line 370 (switch) and the *Data and logs* sentences "No page shows the data yet (#32)" in both languages: the three pages, the switch, the banner and as-of line, that no login protects them, and that they stay empty until a Companion sends (#40, #34);
- `SECURITY.md`: *Deployment Security Considerations* gains "Its pages, among them the data pages that show every profile's regions, islands, figures and names, the icons under /icons/, and /health need no login: anyone who reaches the port reads all stored data, so restrict who reaches it"; *Scope* lists the endpoints: the start page, the data pages under `/regions` and `/profiles/`, `/icons/{reference}`, `/health`, the Blazor hub;
- `.squad/project.md` (exact text reported to the orchestrator): *Security areas*, Network exposure: add "the data pages (`/regions`, `/profiles/…`) and `/icons/{reference}` (`IconEndpoint`)"; Logging and showing external data: add "the server's pages showing names and icons (`ProfileViewBuilder`, `PageNames`, `IconEndpoint`, `PngHeader`)"; Parsing of external input: add "the data pages' addresses (`PageAddress`), the icon header check (`PngHeader`) and the browser's UTC offset answer (`DataStatus`) — [web-pages](../docs/areas/web-pages.md)";
- Tester: `docs/UNIT_TESTS.md` (*Test fixtures*: the pipe fixture is linked into `Censio.Server.Tests`) and `tests/Censio.Pipe.Tests/Fixtures/README.md` (the server test that reads it).

## Architecture check

Record 0006 (no login) stays: the pages add readers, not writers, and the docs say so. Record 0069's bounded memory is kept: pages add one read at a time and only derived views (record 0074); the catalog lookup is bounded by the catalog document's bounds. Record 0072: no culture or time-zone data is used. Record 0041: the switch chooses only the shown profile. Followers never block (`InvokeAsync` without await).

## Security considerations

- [x] Limits: the page addresses are parsed whole after ASP.NET Core's routing has percent-decoded them; the GUID form is exactly 36 characters, the session an `int`, island and area a `byte`. The icon reference is exactly 64 characters of `0-9a-f`, checked before any read. The PNG check reads the first 16 bytes. Rows: 500 per island; profiles: 100 in the switch. Reads of not-held profiles and of icons: one at a time each, cancelled with the page or the request. The browser's UTC offset: -840 to 840 minutes, anything else is UTC.
- [x] Exceptions reaching the user: none. A failed profile read shows the E8 text (the existing `ServerState` error line logs the exception); a JS interop failure or an out-of-range offset keeps UTC; a cancelled read ends quietly; an icon read failure is an empty `500`.
- Names render through Razor's encoding only (no `MarkupString`); the icon response carries `image/png`, `nosniff` and `Content-Security-Policy: default-src 'none'; sandbox`, and a stored non-PNG is never served.

## Decision records

- `docs/decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md` (Proposed)
- `docs/decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md` (Proposed)
- `docs/decisions/0075-companion-connection-and-game-state-held-in-the-server-state.md` (Proposed)
- `docs/decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md` (Proposed)
- `docs/decisions/0077-page-address-carries-the-shown-profile.md` (Proposed)
- `docs/decisions/0078-times-shown-at-the-browsers-utc-offset.md` (Proposed)

## Challenge

- M1 (no test runs the real host): accepted. `ProgramTests` gains `ProgramGetRegionsShowsNoDataInFrame`, `ProgramGetMalformedProfileShowsNotFoundInFrame`, `ProgramGetUnknownIconIsEmptyNotFound` through `CensioServerFactory` (AC9 row).
- M2 (no real PNG): accepted. `PngHeaderAcceptsSignatureAndIhdr` and `IconEndpointServesStoredPng` use `PngWriter.Write` output, reachable through the new `Censio.Companion.Core` reference; hand-made bytes only for refusals.
- m3 (`CatalogHold` is internal): accepted. It becomes public (*Signatures*).
- m4 (atomic held check): accepted with Security B1: `TryGetHeld` replaces `IsHeld`.
- m5 (productivity is already a percent): accepted. Shown rounded as stored; tests for 87.4 and 435.
- m6 (E10 order): accepted the second way: the first 500 entries in tick order, shown by name; spec E10 reworded, `IslandDetailShowsFirst500EntriesByName` asserts it. Sorting before the cut would keep every entry per language and break N2.
- m7 (placeholder tests): accepted. *Test files* names `MainLayoutTests.cs:195` and `MainLayoutProfileSwitchIsDisabledWithLabelOnly` for the Tester.
- m8 (unmatched addresses): accepted. *Approach* and `web-pages.md` state that E7 covers only addresses matching a route; other paths keep the empty `404` (`ProgramTests.cs:236` unchanged).
- m9 (rounding): accepted. Sign, colour and deficit count follow the value rounded to two decimals; tests with -0.004 and 0.004.
- m10 (page-level reachability): accepted. `RegionsPageHoldsNoState` renders a page over a fake source whose state is reachable only through a weak reference and asserts it collected.

## Security review

- B1 (gate bypass between `IsHeld` and the read): resolved. `IProfileStateSource.TryGetHeld` answers from one snapshot; `PageReader` calls `ReadProfileStateAsync` only inside the gate. `PageReaderEvictedBetweenCheckAndReadReadsInsideGate` uses a fake that reports held, then evicts before the read.
- B2 (abandoned reads): resolved. Each page cancels its token in `Dispose`; `PageReader` waits and reads with it; a cancellation ends quietly. `PageReaderCancelledWaiterLeavesWithoutRead`, `RegionsDisposedPageCancelsItsRead`; the icon route passes `RequestAborted` (`IconEndpointAbortedRequestCancelsRead`).
- B3 (untrusted offset): resolved. Offsets outside -840..840 count as none; `DataStatusOutOfRangeOffsetShowsUtc` (841, -841, `int.MaxValue`); documented in `web-pages.md` and the `.squad/project.md` text.
- B4 (documentation): resolved. `SECURITY.md`, `server-state.md` *Privacy*, `server.md` *Limits and errors* and `.squad/project.md` *Parsing of external input* are in *Documentation updates* and *Areas*.
- N1 (icon bytes after the gate): documented in `web-pages.md` with the pointer to `ingest.md` *Accepted risks*.
- N2 (page memory by island count): documented in `web-pages.md`.
- N3 (markup names elsewhere): `MainLayoutProfileNameWithMarkupShowsCharacters`, `RegionsRegionNameWithMarkupShowsCharacters`.
- N4 (reference forms): DataRows of `IconEndpointRefusesMalformedReference`.
- N5 (CSP on the icon): `Content-Security-Policy: default-src 'none'; sandbox`, asserted in `IconEndpointServesStoredPng`.
- N6 (interop result of the wrong type): `JsonException` is caught as well; `DataStatusInteropFailureShowsUtc`.

## Out of scope / follow-ups

As in the spec. Sending data to try the pages is #40 and #34.
