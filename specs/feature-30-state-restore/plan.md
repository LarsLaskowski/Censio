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
