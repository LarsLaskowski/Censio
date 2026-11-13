# Plan: Current state per profile and restore after restart

Source: Issue #30 | [spec.md](spec.md)
Tier: security — the ingest (a security area) starts writing untrusted messages into the database, and a new memory bound on the state that any token holder can grow is a security control (N2).
Security areas touched: Ingest token and Companion authentication (`IngestSession`, `IngestEndpoint`: what is answered and when, a new close 1011, profile stored at the hello); Server database (`ServerStore` gets two reads; written by the ingest from now on); Logging (new server log lines, none with message content).

## Problem / root cause

See [spec.md](spec.md): the ingest stores nothing and nothing restores a state at start.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The ingest stores nothing today and acknowledges every accepted message at once | `IngestSession.cs:483-484` (`AcknowledgeAsync` right after the read); no `ServerStore` use in `src/Censio.Server` (grep) | confirmed |
| 2 | `helloAccepted` always carries `CatalogIdentity = null`; `missingIcons` lists every reference of the document | `IngestSession.cs:391-395`, `GetMissingIcons` `:229-237` | confirmed |
| 3 | Issue: "ticks and catalog per profile" — refuted for the catalog, ticks per profile hold (`ContractTick.ProfileId`) | `catalogUpload` carries no profile (`contract.md`, table); the store keeps one catalog row (`ServerStore.StoreCatalogAsync`, `Id` 1) | refuted |
| 4 | Issue: profile handling per the ADR of #19 | Record 0041 *Decision*; record 0059: tick and session events carry `profileId`, the hello `profileName`; no figure name in the contract | confirmed |
| 5 | Dependencies #19, #28, #29 are merged | `git log origin/main`: 6a7ca74 (#28), f28a496 (#29); record 0041 Accepted | confirmed |
| 6 | The store reports a known tick as `AlreadyStored`, a known icon reference as `AlreadyStored`, and overwrites the catalog every time | `ServerStore.cs` `StoreTickAsync` (`SELECT 1 … WHERE Id`), `StoreIconAsync` (`INSERT OR IGNORE`, `changed == 0`), `StoreCatalogAsync` (`ON CONFLICT … DO UPDATE`, always `Stored`) | confirmed |
| 7 | A write catches only `SqliteException` and returns `Failed` with it; a cancellation propagates | `ServerStore.WriteAsync` (`catch (SqliteException)`) | confirmed |
| 8 | A read does not wrap exceptions; `ReadLatestTickAsync` and `ListIslandsAsync` run in separate transactions | `ServerStore.ReadAsync`; `TickReader.ReadAsync` begins its own transaction (`TickReader.cs:55`), `ListIslandsAsync` none | confirmed |
| 9 | The store's latest tick is ordered by receive ticks, then row number, and an index `(ProfileId, ReceiveTicks, Number)` exists | `TickReader.LatestSql`; `CensioDbContext.cs:40-45` | confirmed |
| 10 | An island row is updated by a tick not older than the one it came from, so an older tick can still rename an island the latest tick lacks | `server-data.md`, *Storing and reading*, *Tick*; `TickWriter` | confirmed |
| 11 | The Companion's place is released only in `EndAsync`, after the message phase returns | `IngestSession.cs:199-202`, `:730-734` | confirmed |
| 12 | A store at the contract's bounds takes 2.6 to 2.9 s; the keep-alive aborts only after 35 s without a pong | `server-data.md`, *Measurements*; `ingest.md`, *Bounds and times* | confirmed |
| 13 | One tick at the contract's bounds held 124.1 MiB in the Companion's pipe model (the server's `ContractTick` is row 14) | `pipe.md`, *Limits and errors* | confirmed |
| 14 | Two held `ContractTick`s at the bounds, plus the missing-icon set, fit the server's memory | to verify: Dev, step 6, peak managed memory of a `ServerState` holding two bound ticks (`CreateBoundTick` shape of `ServerStoreTests`) by a scratch program; the figure goes into `server-state.md` | unverified |
| 15 | A database whose `Ticks` table is renamed by a second connection passes the start classification and fails the restore with a `SqliteException` | to verify: Tester, step 5, in `ServerStateSetupTests` and `ProgramTests` (AC9); classification reads only the history (`server-data.md`, *Start*, step 3) | unverified |
| 16 | A trigger `RAISE(ABORT, …)` created by a second connection on the file makes the store's write fail with `SqliteException` | to verify: Tester, step 5 (AC7); SQLite's `RAISE` raises `SQLITE_CONSTRAINT` | unverified |
| 17 | Server tests reach `Microsoft.Data.Sqlite` without a new package reference | `ProgramTests.cs` already uses `SqliteConnection.ClearAllPools` | confirmed |
| 18 | `Program.cs` runs in the tests (`CensioServerFactory`, the entry point invoked by `ProgramTests`) | `ProgramTests.cs:384-414` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | ServerStateRestoreAsyncAfterReopenEqualsStateBeforeStop; ServerStateSetupTryRestoreAsyncLogsProfileCount; ServerStateSetupTryRestoreAsyncInMemoryLogsZero | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Tests/ServerStateSetupTests.cs |
| AC2 | ServerStateStoreTickAsyncOfOtherProfileLeavesStateUnchanged; ServerStateRestoreAsyncAfterReopenEqualsStateBeforeStop | tests/Censio.Server.Tests/ServerStateTests.cs |
| AC3 | IngestSessionRunAsyncAcknowledgesTickOnlyWhenStored; IngestSessionRunAsyncResentMessagesAcknowledgedWithoutChange; IngestSessionRunAsyncSessionEventsAndStatusStoreNothing | tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC4 | ServerStateStoreTickAsyncOlderTickKeepsNewerState; ServerStateStoreTickAsyncEqualReceiveTimeStoredLaterWins; ServerStateStoreTickAsyncTickWithoutEntriesOrSomeIslandsBecomesState; ServerStateStoreTickAsyncOlderTickIslandNamesEqualStore | tests/Censio.Server.Tests/ServerStateTests.cs |
| AC5 | IngestSessionRunAsyncHelloStoresProfileBeforeHelloAccepted; IngestSessionRunAsyncHelloWithKnownProfileRenamesIt; ServerStateStoreTickAsyncOfUnknownProfileCreatesNamelessProfile | tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Server.Tests/ServerStateTests.cs |
| AC6 | IngestSessionRunAsyncHelloNamesCatalogOnlyWhenAllIconsStored; IngestSessionRunAsyncCatalogUploadListsOnlyIconsNotStored; ServerStateStoreCatalogAsyncResentCatalogListsStillMissingIcons; ServerStateStoreCatalogAsyncOlderCatalogReplacesNewer; ServerStateRestoreAsyncWithIconsMissingNamesNoCatalog | tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Server.Tests/ServerStateTests.cs |
| AC7 | IngestSessionRunAsyncTickStoreFailureCloses1011WithoutAcknowledgement; IngestSessionRunAsyncHelloProfileStoreFailureCloses1011AndFreesPlace | tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC8 | ServerStateStoreTickAsyncTellsFollowersProfileChanged; ServerStateStoreIconAsyncTellsFollowersCatalogChanged; ServerStateChangedThrowingFollowerLogsWarningAndOthersAreTold; ServerStateChangedBlockingFollowerDelaysNoOther; ServerStateReadProfileStateAsyncEarlierStateUnchangedByLaterTick; IngestSessionRunAsyncBlockingFollowerDelaysNoAcknowledgement | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC9 | ServerStateSetupTryRestoreAsyncUnreadableStateLogsCriticalAndReturnsFalse; ProgramStartWithUnreadableStateExitsWithCodeOne | tests/Censio.Server.Tests/ServerStateSetupTests.cs; tests/Censio.Server.Tests/ProgramTests.cs |
| AC10 | ServerStateRestoreAsyncHoldsProfilesWithNewestTicks; ServerStateStoreTickAsyncNewerTickOfUnheldProfileTakesOldestPlace; ServerStateReadProfileStateAsyncOfUnheldProfileEqualsStore; ServerStateReadProfileStateAsyncFailureLogsErrorAndReturnsFailure; ServerStoreListLatestProfilesAsyncOrdersByLatestTick; ServerStoreReadProfileStateAsyncReadsNameTickAndIslands | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Data.Tests/ServerStoreTests.cs |

Cases the Tester covers within these tests:

- AC1: a file database, profiles A (named) and B (unnamed, created by a tick), each with a tick whose every value is set
  (`CreateEveryValueTick` shape), an older tick of A that renames an island A's latest tick lacks, a catalog with all its
  icons; a second `ServerStore`/`ServerState` over a reopened `ServerDatabase` of the same file gives equal profiles,
  ticks (every value, floats compared by bits, negative zero and `null` included) and islands, and
  `CompleteCatalogIdentity`. The log tests use `RecordingLogger` and compare the text
  `Profiles restored from the database: 2.` and, in memory, `Profiles restored from the database: 0.`
- AC3: the acknowledgement of a tick arrives, and then `ServerStore.ReadTickAsync` of a second store over the file
  returns it; a resent tick, icon (same reference) and catalog (same document) are each acknowledged with `refusal`
  `null`, and no follower is called (a counting follower, checked after the acknowledgement of a later tick of another
  profile, which it must count); session start, session end and `status` leave `ListProfilesAsync` empty and call no
  follower.
- AC6: a hello before any catalog, after a catalog with one of two icons, and after both icons: `null`, `null`, the
  identity; a second session (new connection) after the interruption also gets `null`, and its re-upload lists the one
  missing reference.
- AC7: the tick case and the hello case each check: no acknowledgement or reply, close status 1011, one `Error` entry
  `A message could not be stored; the connection is closed.` with an exception, no `Companion connected.` in the hello
  case, the disconnect line with one refused in the tick case and none in the hello case, and a next hello accepted at
  once in the hello case. Failure injected as in fact 16 (a trigger on `Profiles` for the hello, on `Ticks` for the tick).
- AC8: the blocking follower waits on a `TaskCompletionSource` the test releases at its end; a second follower and the
  acknowledgement must arrive while it blocks; the throwing follower's warning text is compared.
- AC9 / AC10 failure: fact 15 (rename `Ticks` through a second connection). The read failure logs `Error`
  `The current state of a profile cannot be read from the database.` with the exception, returns a `ProfileStateRead`
  with `Failure` set and `State` null, and a later read after the rename is undone succeeds.
- AC10: three profiles with latest receive times t1 < t2 < t3: after restore `HeldProfiles` is {2, 3}; a new tick of
  profile 1 at t4 makes it {1, 3}; equal receive times decide by storing order; a hello that renames profile 2 does not
  change `HeldProfiles`.

## Approach

**Data (`Censio.Server.Data`).**
- `ServerStore.ReadProfileStateAsync` reads, in one deferred read transaction on one connection (the gate for an
  in-memory database, as `ReadAsync` does), the profile row, its latest tick by `TickReader.LatestSql` and its island
  list; `null` for an unknown profile. `TickReader` gets an internal variant that reads inside a transaction the caller
  began; `ReadTickAsync` and `ReadLatestTickAsync` keep their behavior.
- `ServerStore.ListLatestProfilesAsync(count)` returns the identities of at most `count` profiles that have ticks,
  ordered by their latest tick, newest first, by the same (receive ticks, number) order as `LatestSql`, one statement on
  the existing index. No schema change (N5).

**State (`Censio.Server`, new `ServerState`, singleton for the process).**
- Holds: the profile list (identity, name) whole; for at most `MaxHeldProfiles` (2) profiles a `StoredProfileState`
  (immutable, swapped whole under a `Lock`) with an in-memory order key (receive time, a storing sequence counted by the
  state; restore assigns sequences in the list's order); the catalog identity and the set of its icon references
  without a stored icon, or "unusable".
- `RestoreAsync`: `ListProfilesAsync`, `ListLatestProfilesAsync(MaxHeldProfiles)`, `ReadProfileStateAsync` for each,
  `ReadCatalogAsync`; a stored catalog is read with `CatalogDocumentReader.Read`; its distinct references go through
  `FindMissingIconsAsync`; a refused document marks the catalog unusable (record 0070). Returns the number of profiles.
  Exceptions propagate to `ServerStateSetup`.
- `StoreProfileAsync`: stores; on `Stored`, sets the name in the list and in a held state; notifies `Profile`. The order
  is untouched.
- `StoreTickAsync`: stores; on `Stored`, adds an unknown profile to the list without a name; then for profile P:
  - P held and the tick's receive time is not before P's latest: the new state is the tick, with P's islands merged
    (each island of the tick takes the tick's name, the others stay; ordered by key as `ListIslandsAsync` orders);
  - P held and the tick is older: P's state is re-read with `ReadProfileStateAsync` (fact 10);
  - P not held and fewer than two held, or the tick's receive time is not before the oldest held order key: P's state
    is read with `ReadProfileStateAsync` and replaces the oldest held one;
  - otherwise nothing is held for P.
  Every `Stored` tick notifies `Profile` for P. A read failure here logs the E13 line and leaves the held set as it was;
  the result stays `Stored` (the tick is stored, so it is acknowledged).
- `StoreCatalogAsync(upload, document)`: when `upload.ComputeIdentity()` equals the held identity and the catalog is
  usable, nothing is written and the outcome is `AlreadyStored`; otherwise the catalog is stored, and on `Stored` the
  identity and the missing set (from `FindMissingIconsAsync` over the document's distinct references) are replaced and
  `Catalog` is notified. Both outcomes return the missing references from `FindMissingIconsAsync`, ordinally sorted. A
  `SqliteException` of that read is returned as `Failed` with the exception.
- `StoreIconAsync`: stores; on `Stored`, removes the reference from the missing set and notifies `Catalog`.
- `CompleteCatalogIdentity`: the identity while the catalog is usable and the missing set is empty, else `null`.
- `ReadProfileStateAsync`: a held profile returns its state without a database read; another known one reads
  `ServerStore.ReadProfileStateAsync`; `SqliteException` gives `Failure` and the E13 line; an unknown profile gives
  `State` null and `Failure` null.
- Notification: `Changed` is raised after the state is updated; each handler of the invocation list runs on its own
  thread-pool work item (`Task.Run`), whose exception is caught and logged as the warning
  `A follower of the current state failed.` with the exception. Followers get no order guarantee across changes.
- Log category of `ServerState`: `Censio.Server.State`.

**Ingest (`IngestSession`, `IngestEndpoint`).**
- The session takes the `ServerState` and keeps the stopping token of `RunAsync` for every store call; a stop during a
  write cancels it, which rolls the transaction back, and the existing catch ends the session without an acknowledgement.
- Hello: after `TryTakeCompanion`, `StoreProfileAsync(hello.ProfileId, hello.ProfileName)`. `Failed`: release the
  place, log the E3 error line, close with 1011 (`CloseAndDiscardAsync(WebSocketCloseStatus.InternalServerError)`), no
  `Companion connected.`, `_companionTaken` stays false. Otherwise as today, with
  `CatalogIdentity = _state.CompleteCatalogIdentity`.
- Messages: a tick, icon image or catalog upload is stored through the state; `Stored` or `AlreadyStored` is
  acknowledged with `refusal` `null` (a catalog with the state's missing list, the others with an empty list) and
  counted accepted; `Failed` logs the E3 line with the exception and returns 1011, which the message loop counts as
  refused and closes. Session events are acknowledged as today, `status` as today. `GetMissingIcons` is removed.
- New `LoggerMessage` in `IngestSession`: `Error`, `A message could not be stored; the connection is closed.` with the
  exception.

**Start (`ServerStateSetup`, `Program.cs`).**
- `ServerStateSetup.TryRestoreAsync` calls `RestoreAsync`; success logs `Information`
  `Profiles restored from the database: {Count}.` and returns `true`; a `SqliteException` logs `Critical`
  `The current state cannot be read from the database '{DatabaseFile}'; the server stops.` with the exception and returns
  `false`. For an in-memory database the path value is `null` as in `DatabaseSetup`.
- `Program.cs`: after the database opens, create `ServerStore` and `ServerState` (logger `Censio.Server.State`), call
  `TryRestoreAsync` (return 1 on `false`), then `app.MapCensioIngest(ingestAccess, state)`. Registering the state for
  pages is #32's.

Layout of the new log lines: as stated above, one line each; no other user-visible output.

## Options considered

- **Read the database for every page request** instead of holding state — lost: the issue asks for an in-memory state
  for fast updates; record 0069.
- **Hold every profile** — lost: memory without bound (N2); record 0069.
- **Merge island values across ticks** — lost: differs from the store's rule, a restart would change the state; record 0069.
- **Acknowledge before storing; or a new refusal kind on a failed write** — lost: loses messages or changes the
  contract; record 0068.
- **Always name the stored catalog; or a hello field with missing icons** — lost: icons never arrive, or a contract
  change; record 0070 (the Devil's Advocate's spec objection 1).
- **Free the Companion's place when the socket ends, not after the store** — lost: two connections of one Companion
  would overlap, against record 0062; spec E14.
- **A store interface with a failing fake for AC7** — lost: a trigger on the real file fails the real write (fact 16),
  and the project prefers real objects (`docs/UNIT_TESTS.md`).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `ServerStore.cs` | two reads |
| Censio.Server.Data | `TickReader.cs` | read inside a caller's transaction |
| Censio.Server.Data | `StoredProfileState.cs` | new |
| Censio.Server | `ServerState.cs`, `ServerStateSetup.cs`, `ProfileStateRead.cs`, `CatalogStoreResult.cs`, `ServerStateChangedEventArgs.cs`, `ServerStateChangeKind.cs` | new |
| Censio.Server | `IngestSession.cs`, `IngestEndpoint.cs`, `Program.cs` | store through the state, catalog identity, 1011, wiring |

## Signatures (for the Dev's skeleton)

One declaration per code span; a property is written as Type.Member with its type, and every new type is sealed
(the enum aside), members with init-only required properties unless stated.

Censio.Server.Data:
- `public Task<StoredProfileState?> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)` in ServerStore
- `public Task<IReadOnlyList<Guid>> ListLatestProfilesAsync(int count, CancellationToken cancellationToken)` in ServerStore
- `class StoredProfileState`, with `StoredProfileState.Profile` (StoredProfile), `StoredProfileState.LatestTick`
  (ContractTick, nullable), `StoredProfileState.Islands` (IReadOnlyList of StoredIsland)

Censio.Server:
- `class ServerState` (sealed partial)
- `ServerState.MaxHeldProfiles` (public const int, 2)
- `public ServerState(ServerStore store, ILogger logger)`
- `ServerState.Changed` (public event, EventHandler of ServerStateChangedEventArgs, nullable)
- `ServerState.Profiles` (IReadOnlyList of StoredProfile, get only)
- `ServerState.HeldProfiles` (IReadOnlyCollection of Guid, get only)
- `ServerState.CompleteCatalogIdentity` (string, nullable, get only)
- `public Task<int> RestoreAsync(CancellationToken cancellationToken)`
- `public Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken)` in ServerState
- `public Task<ServerStoreResult> StoreProfileAsync(Guid profileId, string name, CancellationToken cancellationToken)` in ServerState
- `public Task<ServerStoreResult> StoreTickAsync(ContractTick tick, CancellationToken cancellationToken)` in ServerState
- `public Task<CatalogStoreResult> StoreCatalogAsync(ContractCatalogUpload upload, CatalogDocument document, CancellationToken cancellationToken)` in ServerState
- `public Task<ServerStoreResult> StoreIconAsync(ContractIconImage icon, CancellationToken cancellationToken)` in ServerState
- `class ProfileStateRead`, with `ProfileStateRead.State` (StoredProfileState, nullable) and `ProfileStateRead.Failure`
  (Exception, nullable)
- `class CatalogStoreResult`, with `CatalogStoreResult.Result` (ServerStoreResult) and `CatalogStoreResult.MissingIcons`
  (IReadOnlyList of string)
- `class ServerStateChangedEventArgs` (derives from EventArgs), with
  `public ServerStateChangedEventArgs(ServerStateChangeKind kind, Guid? profileId)`, `ServerStateChangedEventArgs.Kind`
  and `ServerStateChangedEventArgs.ProfileId` (Guid, nullable), both get only
- `enum ServerStateChangeKind`, members `ServerStateChangeKind.Profile` and `ServerStateChangeKind.Catalog`
- `class ServerStateSetup` (static partial)
- `public static Task<bool> TryRestoreAsync(ServerState state, string? databaseFile, ILogger logger, CancellationToken cancellationToken)`
- Changed: `public IngestSession(WebSocket webSocket, IngestAccess access, IngestConnections connections, ServerState state, TimeProvider timeProvider, ILogger logger, string serverVersion)`
- Changed: `public static IEndpointConventionBuilder MapCensioIngest(this IEndpointRouteBuilder endpoints, IngestAccess access, ServerState state)`

Names follow *Writing code* in .squad/stack.md: PascalCase, the Async suffix on every task method, an EventArgs type
for the event, no abbreviations.

## Test files

- tests/Censio.Server.Tests/ServerStateTests.cs (new)
- tests/Censio.Server.Tests/ServerStateSetupTests.cs (new)
- tests/Censio.Server.Tests/IngestSessionTests.cs
- tests/Censio.Server.Tests/ProgramTests.cs
- tests/Censio.Server.Data.Tests/ServerStoreTests.cs

Existing call sites of a changed signature: the session factory of `IngestSessionTests.cs` (line 1132) passes a
`ServerState` over an in-memory `ServerDatabase` — Tester. `IngestEndpointTests` and `ProgramTests` reach the ingest
through `Program.cs` and need no change; `IngestEndpointTests` line 232 (`CatalogIdentity` `null` on a fresh server)
stays true. Tests kept unchanged whose input a criterion now handles differently:
`IngestSessionRunAsyncCatalogUploadListsSortedDistinctIcons` (an empty store still lacks every icon: unchanged) and
`IngestSessionRunAsyncAcceptedMessagesAcknowledgedInOrder` (the messages are now stored first: unchanged result, the
Tester confirms its ticks are of valid profiles). The tick builders of `ServerStoreTests` (`CreateTick`,
`CreateEveryValueTick`, `CreateBoundTick`) are copied as private helpers into `ServerStateTests` — Tester.

Files no unit test can cover: none.

## Areas

- New area **server-state** (`docs/areas/server-state.md`), index row: "The server's current state per profile: what a
  profile's state holds, the latest-tick rule, the two held profiles and their order, the read on demand and its
  failure, restore at start and its log lines, followers and their notices, the catalog's completeness, profiles as the
  Companion chooses them, memory, privacy | Storing and the database (see server-data), the ingest's answers (see
  ingest), pages (#32)". Record 0069's `Area:` names it.
- `ingest.md`: changes listed under *Documentation updates*.
- `server-data.md`: changes listed under *Documentation updates*.

## Documentation updates

All by the Dev, against the code as built:

- `docs/areas/server-state.md` (new) and its row in `docs/areas/README.md`: spec N1, N2 (two profiles, the order, the
  measured memory of fact 14), E2, E7, E8, E10, E13, restore and its lines (AC1, AC9), notices (AC8), catalog
  completeness (record 0070), record 0041's consequence (profiles chosen by the Companion; no figure name); links to
  records 0041, 0068, 0069, 0070.
- `docs/areas/ingest.md`: *Scope* and *Messages after the hello* (stored before acknowledged, resend identities of spec
  E1, the catalog's `missingIcons` from the store, session events and `status` unstored); *The hello* step 6 (profile
  stored; `catalogIdentity` per record 0070) and the failure of the profile store; *Closes* table (row 1011, store
  failure); *Log lines* (the new line); *One Companion* (spec E14); *Not here* (drop "storing … #30"); related decisions
  0068, 0070.
- `docs/areas/server-data.md`: *Scope* (written by the ingest), *Start* (the restore after the database is opened), the
  sentence "what the ingest then answers is #30's" replaced by a link to `ingest.md`, the growth of the file per tick
  (spec *Area documents*), *Not here*.
- `docs/areas/contract.md`, *Scope*: no change (the Companion still does not send); the sweep found no enumeration the
  change extends there.
- `docs/ARCHITECTURE.md`: line 23 (`Censio.Server` row: "later the live state" → the current state per profile), line
  62 ("stores nothing until #30" → stores before it acknowledges, record 0068) and line 63 ("nothing writes to it until
  #30"), plus one sentence on the state held in memory and restored at start linking `areas/server-state.md` and record 0069.
- `README.md` line 24 (English) and its German counterpart: the server stores what it receives per profile, restores
  the last state at start and logs `Profiles restored from the database: <n>.`; the database file grows with every tick
  until retention (#35); still no Companion sends and no page shows data.
- `docs/USER_GUIDE.md` (English and German): the same in the server section, with the new log lines (start line, E3,
  E4, E13, E10).
- `.squad/project.md`: the *Security areas* line "Server database" gains `ServerState` (the in-memory state of the
  stored data); the Dev reports the exact text, the orchestrator writes it. *Integration surface*, "new service":
  no change (no new interface).

## Architecture check

- Guarantee "the server stores data from a Companion only with the shared ingest token": unchanged, every write follows
  an accepted hello.
- Guarantee "history … deletable per profile" (record 0021): ticks stay keyed by profile; deletion is #35's and must
  also drop the profile's held state (noted for #35 in record 0069's consequences via the area document).
- Guarantee "dropped ticks show as a gap, never as zeros" (record 0031): spec E8, an island a tick lacks has no values.
- Plain HTTP and `/health`: untouched. No new setting, so the configuration table is unchanged.

## Security considerations

- [x] Limits: no new limit on parsed input; the store takes what the contract reader accepted (its limits are after
  decoding, `contract.md`). The new bound is on memory: two held ticks (each at most the contract's tick bound), the
  profile list (one name of at most 1,024 UTF-8 bytes per profile, unbounded in count: an accepted risk of a token
  holder, as disk growth already is), the missing-icon set (at most 16,384 references of 64 characters).
- [x] Exceptions reaching a user-visible text: none. `SqliteException` goes to log entries as the attached exception
  (operator only), never into a WebSocket message or close reason (the reason stays empty); SQLite's error texts
  carry no parameter values.
- Restore reads the stored catalog through the catalog reader again: its peak at the bounds (`contract.md`, *Memory of
  a read*) is paid once at start; a stored document the reader refuses is treated as unusable, never trusted.
- A follower that blocks holds a thread-pool thread per notice; the pages of #32 must not block (record 0069).
- No new log line carries a name, value or content (spec N4); the Reviewer checks the `LoggerMessage` templates.

## Decision records

- `docs/decisions/0068-ingest-acknowledges-a-message-only-after-it-is-stored.md` (Proposed)
- `docs/decisions/0069-current-state-of-the-newest-profiles-held-in-memory-and-restored-at-start.md` (Proposed; its
  `Area:` `server-state` needs the area's index row, which the Dev adds — until then `decision-check.py` reports it)
- `docs/decisions/0070-hello-names-the-stored-catalog-only-when-all-its-icons-are-stored.md` (Proposed)

## Out of scope / follow-ups

- Pages, DI registration of the state for pages, the profile switch and the connection banner: #31, #32.
- Session events stored, retention, deleting a profile and its held state: #35.
- The Companion's sending, buffer and catalog upload: #40, #41, #42.

## Challenge notes

Devil's Advocate, plan mode, feature #30 (specs/feature-30-state-restore). I read `plan.md`, `spec.md`, `tasks.md`, the issue (REST) and the code. The plan holds up on the main points:
- Facts 1, 2, 6–11 and 17–18 match the code.
- WAL is on (`ServerDatabaseOpener.cs:409`), so on-demand reads do not wait on a 3 s write.
- The contract enforces UTC receive times (`ContractRules.cs:209`) and distinct island keys (`:421`), so the in-memory merge can match the store's rule in `TickWriter.cs:106-108`.

There are no major objections. Each minor one below is written as an instruction.

**Minor objections**

1. **minor — the AC3 test case for session events cannot pass on a correct build.** The plan says session start, session end and `status` "leave `ListProfilesAsync` empty". But the plan has the hello store its profile before `helloAccepted` (Approach, *Hello*; AC5), and no event can be sent before an accepted hello (`IngestSession.cs:189-191`). So the list always holds the hello's profile.
   *Instruction (Tester):* assert that after the events and `status` the profile list holds only the hello's profile, unchanged. Assert that the profile named by the events gets no tick (`ListLatestProfilesAsync` empty, `ReadLatestTickAsync` null) and that no follower is called.

2. **minor — the AC3 "acknowledged only after it is stored" test would also pass on a wrong build.** The plan reads the tick back through a second store after the acknowledgement arrives. If the server acknowledged first and stored second, that read would still usually find the tick, because a small tick stores in milliseconds. Also, `ServerDatabase.Gate` is internal and only visible to `Censio.Server.Data.Tests` (`Censio.Server.Data.csproj:10`), so the server tests cannot use it to hold the store back.
   *Instruction (Tester):* use a file database. Hold a write lock from a second connection (`BEGIN IMMEDIATE`) while the tick is sent, assert that no acknowledgement arrives while the lock is held, release it, then assert the acknowledgement arrives and the tick can be read. The same test needs a file database for the fact-16 triggers in AC7, because the plan's session factory (`IngestSessionTests.cs:1132`) uses an in-memory database that a second connection cannot reach.

3. **minor — reading a newly held profile back from the database adds a full tick read to every acknowledgement and raises the memory peak.** In `StoreTickAsync`, the plan re-reads a whole state with `ReadProfileStateAsync` in two cases: P is not held but its tick is newer than the oldest held one, and P is held but the tick is older. That read happens before the acknowledgement. At the bounds it costs 1.0–1.3 s on top of the 2.6–2.9 s store (`server-data.md:146`), which breaks the "about 3 s" of spec E14/N3. It also holds a fourth bound tick for a moment: the incoming one, its copy read back, and the two held ones.
   In both cases the tick to hold is already known:
   - P not held: the incoming tick is P's latest, because P's stored latest is older than the oldest held one.
   - P held, tick older: P's held tick stays.

   Only the island list can change (fact 10).
   *Instruction (Dev):*
   - In both cases, read only `ListIslandsAsync(P)` after the commit. The ingest is the only writer, so the island list matches the tick.
   - Measure fact 14 as the peak during a swap (incoming tick plus two held ones), not just two held ticks, and put that figure in `server-state.md`.

4. **minor — ties at restore can come out in the wrong order.** "Restore assigns sequences in the list's order", but `ListLatestProfilesAsync` returns the newest first. Counting up in that order gives the newest held profile the lower sequence. When two held profiles have equal receive times, a third profile's tick at that same time would then push out the wrong one, against N2/AC10.
   *Instruction (Dev):* assign ascending sequences from the oldest held profile to the newest, and start the live counter above them. The Tester adds the equal-receive-time case to the restore test of AC10, not only after a tick.

5. **minor — the order of `ServerState.Profiles` may differ before and after a restart.** Restore fills the list from `ListProfilesAsync` (`ORDER BY "Id"`, `ServerStore.cs:71`). The plan does not say where a profile that arrives by hello or tick goes in the list. If it is appended, the AC1 comparison "equal profiles" fails on order alone.
   *Instruction (Dev):* keep `Profiles` ordered by `Id` as the store orders it. Return it, the held states and the missing-icon set as immutable snapshots replaced whole under the `Lock`, because #32's pages read them from other threads.

6. **minor — a failure after a catalog is written leaves memory and the database out of step.** `StoreCatalogAsync` writes the catalog (`ServerStore.cs:177-190`, commits on its own), then calls `FindMissingIconsAsync`. If that read throws a `SqliteException`, the plan returns `Failed` and replaces nothing. Until the next upload or restart, `CompleteCatalogIdentity` can name the old catalog while the database holds the new one, and no follower is told.
   *Instruction (Dev):* when the write was `Stored` but the missing-icon read failed, mark the held catalog unusable before returning `Failed` (so `CompleteCatalogIdentity` is `null` and the Companion uploads again) and notify `Catalog`. The Tester covers it with a second connection that holds the `Icons` table back.

7. **minor — the documentation list misses sentences that become false.** Neither the Documentation updates nor task 10 name these, and each states that nothing is stored:
   - `docs/USER_GUIDE.md:99`: "the server stores and shows nothing it receives".
   - `docs/USER_GUIDE.md:365`: "Nothing the server receives is stored yet (#30)".
   - The German counterpart at `docs/USER_GUIDE.md:743`: "Noch wird nichts gespeichert … (#30)", and the German text that matches line 99.
   - The ingest row in `docs/areas/README.md:46`: "storing and showing data (#30, #32)".
   - `docs/areas/ingest.md:114`: "The server keeps nothing it accepts (#30)".

   *Instruction (Dev):* rewrite each of these in task 10.

Relevant files: /home/user/Censio/specs/feature-30-state-restore/plan.md, /home/user/Censio/src/Censio.Server/IngestSession.cs, /home/user/Censio/src/Censio.Server.Data/ServerStore.cs, /home/user/Censio/src/Censio.Server.Data/TickWriter.cs, /home/user/Censio/docs/USER_GUIDE.md

## Security notes

## Security plan review: feature #30 (specs/feature-30-state-restore), round 1

I read `/home/user/Censio/specs/feature-30-state-restore/spec.md`, `plan.md`, `tasks.md`, records 0068 and 0069, `docs/areas/ingest.md`, `docs/areas/server-data.md`, `docs/areas/contract.md` (*Bounds*, *Memory of a read*), `SECURITY.md`, and the code in `src/Censio.Server/IngestSession.cs`, `IngestEndpoint.cs`, `Program.cs`, `src/Censio.Server.Data/ServerStore.cs` and `TickReader.cs`.

**Tier:** `security` is right. It covers the first writes of untrusted input, the authenticated hello now storing data, and the memory bound the plan calls a control.

### What holds
- The profile is stored only after the token check and `TryTakeCompanion`. A refused hello writes nothing.
- Close 1011 has an empty reason. A `SqliteException` only reaches the operator's log, never the socket.
- The new log lines (E3, E4, E13, E10, the start count) carry no message content. SQLite error texts name tables and constraints, not parameter values.
- Icons are content-addressed: the reader checks that the SHA-256 of the PNG equals `reference`, and the store uses `INSERT OR IGNORE`, so one icon cannot overwrite another.
- The catalog resend check compares SHA-256 identities. A stored document the reader refuses at restore is treated as unusable, not trusted.
- Stores are atomic: one immediate transaction each, and cancellation rolls back. E6 holds.

### Blocking

**B1. The N2 memory bound leaves out each held profile's island list, and the unbounded growth the plan accepts is not written down anywhere.**
- Evidence:
  - The plan (Approach, `ServerState` "Holds"; Security considerations, lines 285–288) and record 0069 (*Consequences*: "two latest ticks plus the profile list and the catalog's missing-icon references") both describe a bounded held state.
  - But `StoredProfileState.Islands` is the profile's whole island list (`ServerStore.ListIslandsAsync`, no limit).
  - Island rows build up per key (session GUID, island ID, area index) over all ticks (`server-data.md`, *What is stored*).
  - One tick may add 256 new keys, each with a name of up to 765 UTF-8 bytes (`contract.md`, *Bounds*).
  - So a token holder, or anyone when the token is off, can grow a held profile's islands by about 0.4 MB per small tick without limit. That memory is held, copied on every merge, loaded at restore and loaded on every on-demand read. N2 ("bounded whatever the number of stored profiles") and the area document would state a bound that does not exist.
- A related gap: the plan calls the unbounded profile list and database growth "an accepted risk of a token holder, as disk growth already is". No document records disk growth as an accepted risk today, because nothing was stored before this change. The Documentation updates only mention growth in README and the user guide.
- Smallest fix (documentation only; no cap needed, since real games have a finite set of islands):
  - **Plan, Security considerations, and record 0069 Consequences:** say the held state is two latest ticks plus each held profile's full island list plus the profile list plus the missing-icon set. The island lists and the profile list are unbounded in count.
  - **`server-state.md` *Memory*:** state the same.
  - **`ingest.md` *Accepted risks*:** add a new entry, verbatim: "**Growth by a token holder.** Every stored tick, icon and profile stays until retention (#35): a Companion with the token, or anyone with the token switched off, can grow the database file, the profile list and a profile's island list (up to 256 new islands per tick) without a bound, and the server holds the profile list and the island lists of the held profiles in memory."
  - **`SECURITY.md`, the `RequireToken` `false` sentence:** extend it to "anyone who reaches the server can then send data, which the server stores, and fill its database file".

**B2. No test checks that a refused hello or a refused message stores nothing.**
- This is the first change that writes to the store. The plan's *Architecture check* says the guarantee "stores data only with the shared ingest token" is unchanged ("every write follows an accepted hello"), but no test in the AC table checks it.
- Fix: add cases under AC5/AC3 in `IngestSessionTests.cs`:
  - A hello refused with `tokenRefused`, and one refused with `unsupportedVersion`, leave `ListProfilesAsync` empty.
  - A second hello refused with `companionConnected`, with a valid token, the connected profile's id and another name, does not rename that profile.
  - A tick the reader refuses as `invalid` (with a valid id), and a catalog upload refused as `catalogInvalid`, are acknowledged with their refusal and store nothing: no tick, no catalog row, `CompleteCatalogIdentity` unchanged, no follower called.

### Non-blocking (my recommendation: fix now unless stated)

1. **The Companion place can leak if the hello store throws something other than a `SqliteException`.**
   - The plan keeps `_companionTaken` false until the store succeeds, but `TryTakeCompanion` has already run. An `OperationCanceledException` at stop, an `ObjectDisposedException` from the gate, or an `InvalidOperationException` goes to the `RunAsync` catch, and then `EndAsync` never calls `ReleaseCompanion` (`IngestSession.cs:194-202`, `:730-734`).
   - At stop this does no harm. Otherwise every later hello is refused `companionConnected` until a restart.
   - Fix: record that the place was taken right after `TryTakeCompanion`, release it in `EndAsync` from that flag, and keep the disconnect line on a separate "hello accepted" flag.

2. **Fact 14 should measure the peak of the store path, not the steady state.**
   - Fact 14 measures "a `ServerState` holding two bound ticks" in the `CreateBoundTick` shape. During `StoreTickAsync` for an unheld profile, or for an older tick, these are alive at once: two held states, the session's 24 MiB buffer and the incoming tick's reader model, and the state re-read from the database before the swap.
   - The worst retained tick shape is the four-pairs tick (43.3 MiB retained, `contract.md`), not necessarily `CreateBoundTick`.
   - Fix: change fact 14's text to measure the peak managed memory of `StoreTickAsync` of a worst-shape tick for an unheld profile while two worst-shape ticks are held, and state that figure in `server-state.md`.

3. **On-demand reads have no concurrency bound.**
   - `ServerState.ReadProfileStateAsync` for an unheld profile costs over a second and a whole tick model at the bounds. #32's pages need no login (`server.md`), so parallel page requests for old profiles could pile up these reads.
   - It is unused in this change. Fix now: one sentence in `server-state.md` and record 0069's Consequences: "Reads on demand are not limited in number; a caller reachable without login (#32) must serialize or bound them." Or serialize them in `ServerState` with a `SemaphoreSlim(1)`.

4. **The AC7 failure injection needs a file database.**
   - The trigger needs a second connection on a file. The plan's session factory uses an in-memory `ServerDatabase` (plan, *Test files*), so the AC7 tests must build theirs over a temporary file.
   - I checked the engine behaviour once (fact 16; below).

5. **`TryRestoreAsync` only catches `SqliteException`.**
   - A stored value that fails conversion (`GetGuid`, `GetByte`) would crash the start with an unhandled exception instead of the E4 line and exit code 1.
   - Reaching this needs write access to the owner-only file, which `SECURITY.md` puts out of scope. My recommendation: a linked issue, or leave it.

6. **Security areas touched could add one entry.**
   - Restore runs `CatalogDocumentReader.Read` again on the stored, externally supplied document. That is a new use of the parser in "Parsing of external input", with no change to the reader.
   - The plan's Security considerations already discuss it. Fix: add "Parsing of external input (the catalog reader re-reads the stored document at restore; reader unchanged)".

### Measurement run (fact 16, the SQLite engine side)
Script: `/tmp/claude-0/-home-user-Censio/d173ea8b-e94c-5e17-99f4-f472f6438c1a/scratchpad/f16/f16.py`. Setup: WAL mode; a long-lived connection A; connection B creates `BEFORE INSERT` triggers with `RAISE(ABORT, 'injected')` on `Profiles` and `Ticks`; A then runs the store's statement shapes inside `BEGIN IMMEDIATE`.

Command: `python3 -I f16.py t.db` (Python's SQLite 3.53.1, not the e_sqlite3 bundle the server uses). Output:
```
upsert known profile -> IntegrityError injected SQLITE_CONSTRAINT_TRIGGER
upsert new profile -> IntegrityError injected SQLITE_CONSTRAINT_TRIGGER
insert or ignore profile -> IntegrityError injected SQLITE_CONSTRAINT_TRIGGER
insert tick returning -> IntegrityError injected SQLITE_CONSTRAINT_TRIGGER
[(b'p1', 'Rome')]
```
The trigger fires for the upsert (both the known-profile and new-profile paths), for `INSERT OR IGNORE`, and for the tick insert with `RETURNING`. The earlier name is kept. Microsoft.Data.Sqlite turns any error code into a `SqliteException`.

Not run:
- Fact 14 (memory): it is not a precondition for approval. The bound is structural (two held profiles), and the figure is the Dev's, which I will check in the diff review against non-blocking 2.
- Fact 15: a test technique the Tester verifies in step 5.
