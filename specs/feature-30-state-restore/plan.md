# Plan: Current state per profile and restore after restart

Source: Issue #30 | [spec.md](spec.md)
Tier: security — the ingest (a security area) starts writing untrusted messages into the database, and a new memory bound on the state that any token holder can grow is a security control (N2).
Security areas touched: Ingest token and Companion authentication (`IngestSession`, `IngestEndpoint`: stored before acknowledged, close 1011, profile stored at the hello); Server database (`ServerStore` gets two reads and is written by the ingest); Parsing of external input (the catalog reader re-reads the stored document at restore; reader unchanged); Logging (new server lines, none with message content).

## Problem / root cause

See [spec.md](spec.md): the ingest stores nothing and nothing restores a state at start.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The ingest stores nothing and acknowledges every accepted message at once | `IngestSession.cs:483-484`; no `ServerStore` use in `src/Censio.Server` | confirmed |
| 2 | `helloAccepted` always carries `CatalogIdentity = null`; `missingIcons` lists every reference | `IngestSession.cs:391-395`, `:229-237` | confirmed |
| 3 | Issue: "ticks and catalog per profile" — refuted for the catalog, ticks per profile hold | `catalogUpload` has no profile (`contract.md`); one catalog row (`ServerStore.StoreCatalogAsync`) | refuted |
| 4 | Issue: profile handling per the ADR of #19 | records 0041, 0059: ticks carry `profileId`, the hello `profileName`, no figure name | confirmed |
| 5 | Dependencies #19, #28, #29 are merged | `git log origin/main`: 6a7ca74, f28a496; record 0041 Accepted | confirmed |
| 6 | Known tick and icon are `AlreadyStored`; the catalog is overwritten every time | `ServerStore.cs` `StoreTickAsync`, `StoreIconAsync` (`INSERT OR IGNORE`), `StoreCatalogAsync` (upsert, always `Stored`) | confirmed |
| 7 | A write catches only `SqliteException` (`Failed`); a cancellation propagates | `ServerStore.WriteAsync` | confirmed |
| 8 | A read does not wrap exceptions; latest tick and islands are read in separate transactions | `ServerStore.ReadAsync`; `TickReader.cs:55`; `ListIslandsAsync` | confirmed |
| 9 | Latest tick order: receive ticks, then row number, on index `(ProfileId, ReceiveTicks, Number)` | `TickReader.LatestSql`; `CensioDbContext.cs:40-45` | confirmed |
| 10 | An older tick can still rename an island the latest tick lacks | `server-data.md`, *Tick*; `TickWriter.cs:106-108` | confirmed |
| 11 | The Companion's place is released only in `EndAsync`, and only when `_companionTaken` | `IngestSession.cs:194-202`, `:730-734` | confirmed |
| 12 | A store at the bounds takes 2.6 to 2.9 s; the keep-alive aborts after 35 s without a pong; WAL is on, so reads do not wait for a write | `server-data.md`, *Measurements*; `ingest.md`; `ServerDatabaseOpener.cs:409` | confirmed |
| 13 | A trigger `RAISE(ABORT, …)` created by a second connection on the file fails upsert, `INSERT OR IGNORE` and the tick insert, keeping earlier data | Security's scratch run (SQLite 3.53.1, *Security review*); Microsoft.Data.Sqlite maps every error code to `SqliteException` | confirmed |
| 14 | The peak managed memory of `StoreTickAsync` of a worst-shape tick (the four-pairs tick of `contract.md`, 43.3 MiB retained) for an unheld profile while two worst-shape ticks are held fits the server | to verify: Dev, step 6, scratch program; the figure goes into `server-state.md` *Memory* | unverified |
| 15 | A database whose `Ticks` table a second connection renamed passes the start classification and fails the restore with `SqliteException` | to verify: Tester, step 5 (AC9); classification reads only the history (`server-data.md`, *Start*) | unverified |
| 16 | A cancelled stopping token ends a store call that waits on a write lock held by a second connection | to verify: Tester, step 5 (AC7, place freed); if Microsoft.Data.Sqlite's busy wait ignores the token, the Tester uses a closed socket instead and says so | unverified |
| 17 | Server tests reach `Microsoft.Data.Sqlite`, and `Program.cs` runs in them | `ProgramTests.cs` (`SqliteConnection.ClearAllPools`, entry point at `:384-414`) | confirmed |
| 18 | `ReleaseCompanion` frees the place without checking who holds it, so a second release frees a later session's place | `IngestConnections.cs:75-78` (`Volatile.Write(ref _companionTaken, 0)`) | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | ServerStateRestoreAsyncAfterReopenEqualsStateBeforeStop; ServerStateSetupTryRestoreAsyncLogsProfileCount; ServerStateSetupTryRestoreAsyncInMemoryLogsZero | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Tests/ServerStateSetupTests.cs |
| AC2 | ServerStateStoreTickAsyncOfOtherProfileLeavesStateUnchanged; ServerStateRestoreAsyncAfterReopenEqualsStateBeforeStop | tests/Censio.Server.Tests/ServerStateTests.cs |
| AC3 | IngestSessionRunAsyncAcknowledgesTickOnlyWhenStored; IngestSessionRunAsyncResentMessagesAcknowledgedWithoutChange; IngestSessionRunAsyncSessionEventsAndStatusStoreNothing; IngestSessionRunAsyncRefusedMessagesStoreNothing | tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC4 | ServerStateStoreTickAsyncOlderTickKeepsNewerState; ServerStateStoreTickAsyncEqualReceiveTimeStoredLaterWins; ServerStateStoreTickAsyncTickWithoutEntriesOrSomeIslandsBecomesState; ServerStateStoreTickAsyncOlderTickIslandNamesEqualStore | tests/Censio.Server.Tests/ServerStateTests.cs |
| AC5 | IngestSessionRunAsyncHelloStoresProfileBeforeHelloAccepted; IngestSessionRunAsyncHelloWithKnownProfileRenamesIt; IngestSessionRunAsyncRefusedHelloStoresNothing; IngestSessionRunAsyncCompanionConnectedHelloRenamesNothing; ServerStateStoreTickAsyncOfUnknownProfileCreatesNamelessProfile | tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Server.Tests/ServerStateTests.cs |
| AC6 | IngestSessionRunAsyncHelloNamesCatalogOnlyWhenAllIconsStored; IngestSessionRunAsyncCatalogUploadListsOnlyIconsNotStored; ServerStateStoreCatalogAsyncResentCatalogListsStillMissingIcons; ServerStateStoreCatalogAsyncOlderCatalogReplacesNewer; ServerStateStoreCatalogAsyncMissingIconReadFailureMarksCatalogUnusable; ServerStateRestoreAsyncWithIconsMissingNamesNoCatalog | tests/Censio.Server.Tests/IngestSessionTests.cs; tests/Censio.Server.Tests/ServerStateTests.cs |
| AC7 | IngestSessionRunAsyncTickStoreFailureCloses1011WithoutAcknowledgement; IngestSessionRunAsyncHelloProfileStoreFailureCloses1011AndFreesPlace; IngestSessionRunAsyncHelloStoreCancelledFreesPlace | tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC8 | ServerStateStoreTickAsyncTellsFollowersProfileChanged; ServerStateStoreIconAsyncTellsFollowersCatalogChanged; ServerStateChangedThrowingFollowerLogsWarningAndOthersAreTold; ServerStateChangedBlockingFollowerDelaysNoOther; ServerStateReadProfileStateAsyncEarlierStateUnchangedByLaterTick; IngestSessionRunAsyncBlockingFollowerDelaysNoAcknowledgement | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC9 | ServerStateSetupTryRestoreAsyncUnreadableStateLogsCriticalAndReturnsFalse; ProgramStartWithUnreadableStateExitsWithCodeOne | tests/Censio.Server.Tests/ServerStateSetupTests.cs; tests/Censio.Server.Tests/ProgramTests.cs |
| AC10 | ServerStateRestoreAsyncHoldsProfilesWithNewestTicks; ServerStateStoreTickAsyncNewerTickOfUnheldProfileTakesOldestPlace; ServerStateStoreTickAsyncAfterIslandReadFailureHoldsStoredLatest; ServerStateReadProfileStateAsyncOfUnheldProfileEqualsStore; ServerStateReadProfileStateAsyncFailureLogsErrorAndReturnsFailure; ServerStoreListLatestProfilesAsyncOrdersByLatestTick; ServerStoreReadProfileStateAsyncReadsNameTickAndIslands | tests/Censio.Server.Tests/ServerStateTests.cs; tests/Censio.Server.Data.Tests/ServerStoreTests.cs |

Cases within these tests (Tester):

- Database: every test that needs a second connection (write lock, trigger, renamed table, reopen) builds its
  `ServerDatabase` over a temporary file; the shared session factory may stay in memory.
- AC1: profiles A (named) and B (created by a tick), each with an every-value tick, an older tick of A that renames an
  island A's latest tick lacks, a catalog with all icons; a second `ServerState` over the reopened file gives equal
  `Profiles` (same order), states (floats by bits, negative zero, `null`), islands and `CompleteCatalogIdentity`. Log
  texts `Profiles restored from the database: 2.` and, in memory, `…: 0.`
- AC3, stored first: a second connection holds `BEGIN IMMEDIATE`; no acknowledgement arrives while it holds; after the
  release the acknowledgement arrives and the tick reads back. Resends: tick, icon (same reference), catalog (same
  document) acknowledged with `refusal` `null`, and a counting follower, checked after a later tick of another profile
  that it must count, saw no resend. Session events and `status`: the profile list holds only the hello's profile,
  unchanged; the events' profile has no tick (`ListLatestProfilesAsync` empty, `ReadLatestTickAsync` null); no follower.
  Refused: a tick refused `invalid` (valid id) and a catalog refused `catalogInvalid` are acknowledged with their
  refusal and store nothing (no tick, no catalog row, `CompleteCatalogIdentity` unchanged, no follower).
- AC5, refused hellos: `tokenRefused` and `unsupportedVersion` leave `ListProfilesAsync` empty; a `companionConnected`
  hello with a valid token, the connected profile's id and another name does not rename it.
- AC6: hello before a catalog, after a catalog with one of two icons, after both: `null`, `null`, identity; a new
  connection after the interruption gets `null`, and its re-upload lists the missing reference. Unusable: rename `Icons`
  through a second connection, upload a new catalog: `Failed`, `CompleteCatalogIdentity` `null`, `Catalog` notified.
- AC7: no acknowledgement or reply, close 1011, one `Error` entry `A message could not be stored; the connection is
  closed.` with an exception; hello case: no `Companion connected.`, no disconnect line; a second Companion's hello is
  accepted during the failed session's close wait, and after the failed session ends a third hello is still refused
  `companionConnected`; tick case: disconnect line with one refused. Triggers on `Profiles` (hello) and `Ticks` (tick). Cancelled hello store
  (fact 16): the next hello is accepted.
- AC8: the blocking follower waits on a `TaskCompletionSource` released at the end; another follower and the
  acknowledgement arrive meanwhile; the throwing follower's warning text is compared.
- AC9, AC10 failure: fact 15. The read failure logs `Error` `The current state of a profile cannot be read from the
  database.` with the exception, returns `Failure` set and `State` null; a read after undoing the rename succeeds.
- AC10: latest receive times t1 < t2 < t3: after restore `HeldProfiles` is {2, 3}; a tick of 1 at t4 gives {1, 3}; the
  same with t2 = t3 at restore and a tick of 1 at that time (the older-sequenced one leaves); a renaming hello of 2
  changes nothing. Marked profile (`ServerStateStoreTickAsyncAfterIslandReadFailureHoldsStoredLatest`): held {A}, P's
  t3 stored while its islands read fails, then P's t1 stored: P's held state is t3 with its islands. The Tester finds a
  real failure of only the islands read; if none exists it says so, and the branch is reported as uncovered.

## Approach

**Data (`Censio.Server.Data`).**
- `ServerStore.ReadProfileStateAsync` reads the profile row, its latest tick (`TickReader.LatestSql`) and its island list
  in one deferred read transaction on one connection (under the gate in memory, as `ReadAsync`); `null` for an unknown
  profile. `TickReader` gets an internal variant that reads inside the caller's transaction.
- `ServerStore.ListLatestProfilesAsync(count)`: at most `count` profiles with ticks, newest latest tick first, by the
  `LatestSql` order, one statement on the existing index. No schema change.

**State (`Censio.Server`, `ServerState`, one per process).** Everything a reader gets is an immutable snapshot replaced
whole under one `Lock`: `Profiles` (ordered by `Id` as `ListProfilesAsync`), the held states with their order keys
(receive time, a storing sequence), the catalog identity with its missing-reference set or "unusable".
- `RestoreAsync`: `ListProfilesAsync`; `ListLatestProfilesAsync(MaxHeldProfiles)`; `ReadProfileStateAsync` for each, with
  sequences ascending from the oldest held to the newest and the live counter above them; `ReadCatalogAsync`, the
  document through `CatalogDocumentReader.Read` (refused: unusable, record 0070), its distinct references through
  `FindMissingIconsAsync`. Returns the profile count; exceptions propagate.
- `StoreProfileAsync`: on `Stored` the name goes into `Profiles` (inserted in `Id` order) and a held state; notify
  `Profile`; the order keys stay.
- `StoreTickAsync`: on `Stored`, a new profile enters `Profiles` without a name, then for P:
  - held, tick not older than P's latest: state = the tick, islands merged (the tick's islands take its names, ordered by key);
  - held, tick older: the tick stays; islands from `ListIslandsAsync(P)` (fact 10);
  - not held, fewer than two held or the tick not older than the oldest held key: state = the tick with
    `ListIslandsAsync(P)` and P's name; it replaces the oldest held state (P's stored latest is older than that key, so
    the tick is P's latest);
  - otherwise nothing is held.
  Every `Stored` tick notifies `Profile` for P. A failed islands read logs the E13 line and leaves the held set; the
  result stays `Stored`. A failure in the not-held branch also marks P, since P's stored latest may then be newer than
  every later tick of P: a stored tick of a marked P takes neither branch above but reads P's whole state
  (`ServerStore.ReadProfileStateAsync`) and decides by that latest tick's key; it enters, by the not-held rule, and the
  mark clears; a failed read logs E13 and keeps the mark. Otherwise no whole tick is read back on this path.
- `StoreCatalogAsync(upload, document)`: an identity equal to a usable held one writes nothing (`AlreadyStored`);
  otherwise store, and on `Stored` replace identity and missing set and notify `Catalog`. The acknowledgement's list
  comes from `FindMissingIconsAsync` over the document's distinct references, ordinally sorted. If that read throws
  `SqliteException` after a `Stored` write, the catalog is marked unusable, `Catalog` notified and `Failed` returned.
- `StoreIconAsync`: on `Stored` remove the reference from the missing set and notify `Catalog`.
- `CompleteCatalogIdentity`: the identity while usable with an empty missing set, else `null`.
- `ReadProfileStateAsync`: held → the snapshot; other known → `ServerStore.ReadProfileStateAsync`, a `SqliteException`
  gives `Failure` and the E13 line; unknown → both null. Not limited in number (record 0069).
- `Changed` is raised after the snapshot swap; each handler runs on its own `Task.Run` work item; an exception is
  logged as `Warning` `A follower of the current state failed.` No order across changes. Category `Censio.Server.State`.

**Ingest.**
- The session takes the `ServerState` and passes the stopping token of `RunAsync` to every store call; a cancellation
  rolls back and the existing catch ends the session without an acknowledgement.
- Hello: right after `TryTakeCompanion` an `int` place flag is set to 1. One private release method frees the place at
  most once per session, `if (Interlocked.Exchange(ref _placeTaken, 0) == 1) { _connections.ReleaseCompanion(); }`
  (fact 18), called by `EndAsync` whatever ended the session and by the `Failed` path; the disconnect line follows a
  separate "hello accepted" flag. Then `StoreProfileAsync`; `Failed`: that release method, the E3 line, close 1011
  (`WebSocketCloseStatus.InternalServerError`), no `Companion connected.`.
  Otherwise as today with `CatalogIdentity = CompleteCatalogIdentity`.
- Tick, icon image, catalog upload: stored through the state; `Stored`/`AlreadyStored` acknowledged with `refusal`
  `null` (a catalog with its missing list), counted accepted; `Failed` logs `Error` `A message could not be stored; the
  connection is closed.` with the exception and returns 1011 (counted refused). Session events and `status` as today;
  `GetMissingIcons` is removed.

**Start.** `ServerStateSetup.TryRestoreAsync` logs `Information` `Profiles restored from the database: {Count}.` and
returns `true`, or on `SqliteException` logs `Critical` `The current state cannot be read from the database
'{DatabaseFile}'; the server stops.` with the exception and returns `false` (path `null` in memory). `Program.cs`
builds `ServerStore` and `ServerState` after the database, returns 1 on `false`, and maps the ingest with the state.
Registering the state for pages is #32's.

New output: the log lines above, one line each.

## Options considered

- Read the database per page request; hold every profile; merge island values across ticks — lost, record 0069.
- Acknowledge before storing; a refusal kind for a failed write — lost, record 0068.
- Always name the stored catalog; a hello field with missing icons — lost, record 0070.
- Free the Companion's place when the socket ends, not after the store — lost: overlapping connections against record
  0062; spec E14.
- A store interface with a failing fake for AC7 — lost: a trigger fails the real write (fact 13).
- A cap on islands per profile (Security B1) — lost: a real game has a finite island set; the growth is an accepted
  risk, written down (*Documentation updates*).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server.Data | `ServerStore.cs`, `TickReader.cs`, `StoredProfileState.cs` (new) | two reads |
| Censio.Server | `ServerState.cs`, `ServerStateSetup.cs`, `ProfileStateRead.cs`, `CatalogStoreResult.cs`, `ServerStateChangedEventArgs.cs`, `ServerStateChangeKind.cs` (new) | state, restore |
| Censio.Server | `IngestSession.cs`, `IngestEndpoint.cs`, `Program.cs` | storing, 1011, wiring |

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

Existing call sites: the session factory of `IngestSessionTests.cs` (line 1132) passes a `ServerState` over an
in-memory `ServerDatabase`, with a variant over a temporary file for the tests that need one — Tester.
`IngestEndpointTests` (line 232, `null` identity on a fresh server, still true) and `ProgramTests` go through
`Program.cs` unchanged. Kept unchanged, same result: `IngestSessionRunAsyncCatalogUploadListsSortedDistinctIcons` (an
empty store lacks every icon) and `IngestSessionRunAsyncAcceptedMessagesAcknowledgedInOrder` (now stored first; the
Tester confirms its ticks are valid). `ServerStoreTests`' tick builders are copied into `ServerStateTests` — Tester.

Files no unit test can cover: none.

## Areas

- New area **server-state** (`docs/areas/server-state.md`), index row: "The server's current state per profile: what a
  profile's state holds, the latest-tick rule, the two held profiles and their order, the read on demand and its
  failure, restore at start and its log lines, followers and their notices, the catalog's completeness, profiles as the
  Companion chooses them, memory, privacy | Storing and the database (see server-data), the ingest's answers (see
  ingest), pages (#32)". Record 0069's `Area:` names it.
- `ingest.md`, `server-data.md`: below.

## Documentation updates

All by the Dev, against the code as built:

- `docs/areas/server-state.md` (new) and its row in `docs/areas/README.md`: spec N1, N2, E2, E7, E8, E10, E13; restore
  and its lines; notices; catalog completeness (record 0070); profiles chosen by the Companion, no figure name (record
  0041); links to records 0041, 0068, 0069, 0070; after a failed islands read a not-held profile enters at its next
  stored tick, with its stored latest state. *Memory*: the held state is two latest ticks plus each held profile's
  full island list plus the profile list plus the missing-icon set; island lists and the profile list are unbounded in
  count; fact 14's figure; reads on demand are not limited in number, and a caller reachable without login (#32) must
  serialize or bound them.
- `docs/areas/README.md` line 46 (ingest row, "storing and showing data (#30, #32)"): storing is now in ingest and
  server-state; showing stays #32.
- `docs/areas/ingest.md`: *Scope* and *Messages after the hello* (line 114 "keeps nothing it accepts": stored before
  acknowledged, resend identities of spec E1, `missingIcons` from the store, session events and `status` unstored);
  *The hello* step 6 (profile stored, `catalogIdentity` per record 0070, the store failure); *Closes* (1011); *Log
  lines*; *One Companion* (spec E14); *Not here*; related decisions 0068, 0070; *Accepted risks*, new entry verbatim:
  "**Growth by a token holder.** Every stored tick, icon and profile stays until retention (#35): a Companion with the token, or anyone with the token switched off, can grow the database file, the profile list and a profile's island list (up to 256 new islands per tick) without a bound, and the server holds the profile list and the island lists of the held profiles in memory."
- `docs/areas/server-data.md`: *Scope* (written by the ingest), *Start* (restore after opening), "what the ingest then
  answers is #30's" → link to `ingest.md`, the file's growth per tick (about 77 KB per real tick every two minutes,
  22.1 MiB at the bounds) until #35, *Not here*.
- `SECURITY.md`, the `RequireToken` `false` sentence: "anyone who reaches the server can then send data, which the
  server stores, and fill its database file".
- `docs/ARCHITECTURE.md`: line 23 ("later the live state" → the current state per profile), line 62 ("stores nothing
  until #30" → stores before it acknowledges, record 0068), line 63 ("nothing writes to it until #30"), and one sentence
  on the state held in memory and restored at start, linking `areas/server-state.md` and record 0069.
- `README.md` line 24 and its German counterpart: stores per profile, restores at start with the start line, the file
  grows until #35, still nothing sends or shows data.
- `docs/USER_GUIDE.md`: line 99 and its German counterpart ("stores and shows nothing it receives"), line 365 ("Nothing
  the server receives is stored yet (#30)") and line 743 ("Noch wird nichts gespeichert … (#30)"): rewritten to the
  new behavior, with the new log lines (start line, E3, E4, E13, E10), English and German.
- `.squad/project.md`, *Security areas*, line "Server database": the Dev reports the text, the orchestrator writes it:
  "- Server database: the file, its side files and folders, owner-only (`ServerDatabase`, `DatabaseSetup`), and the in-memory state of the stored data (`ServerState`, `ServerStateSetup`) — [server-data](../docs/areas/server-data.md), [server-state](../docs/areas/server-state.md)."

## Architecture check

- "Stores data only with the shared ingest token": every write follows an accepted hello; tested by the refused cases of
  AC3 and AC5.
- "Deletable per profile" (record 0021): ticks stay keyed by profile; #35 must also drop the held state.
- "Gaps, never zeros" (record 0031): spec E8.
- Plain HTTP and `/health` untouched; no new setting.

## Security considerations

- [x] Limits: no new limit on parsed input; the store takes what the contract reader accepted (limits after decoding,
  `contract.md`). Memory: two latest ticks plus each held profile's full island list, the profile list and the
  missing-icon set (at most 16,384 references); the island lists and the profile list are unbounded in count, an
  accepted risk written into `ingest.md` *Accepted risks* and `SECURITY.md`. Store-path peak: fact 14.
- [x] Exceptions reaching a user-visible text: none. `SqliteException` reaches only log entries; close reasons stay empty.
- Restore re-reads the stored catalog through the catalog reader (its bound peak once at start); a refused document is unusable.
- A follower that blocks holds a thread-pool thread per notice; #32's pages must not block (record 0069).
- Reads on demand are unbounded in number; #32 bounds its callers (record 0069).

## Decision records

- `docs/decisions/0068-ingest-acknowledges-a-message-only-after-it-is-stored.md` (Proposed)
- `docs/decisions/0069-current-state-of-the-newest-profiles-held-in-memory-and-restored-at-start.md` (Proposed; its
  `Area:` `server-state` needs the index row the Dev adds)
- `docs/decisions/0070-hello-names-the-stored-catalog-only-when-all-its-icons-are-stored.md` (Proposed)

## Out of scope / follow-ups

- Pages, the state's registration for pages, the profile switch, the banner: #31, #32.
- Session events stored, retention, deleting a profile and its held state: #35.
- The Companion's sending, buffer and catalog upload: #40, #41, #42.

## Challenge

Devil's Advocate, plan round: no major objection; seven minor ones, all accepted.

1. The AC3 session-event case contradicted AC5 (the hello stores its profile). Accepted: the case asserts only the
   hello's profile, unchanged, no tick for the events' profile, no follower.
2. The "stored first" test would pass on a wrong build. Accepted: a second connection holds `BEGIN IMMEDIATE`; no
   acknowledgement while held. Tests needing a second connection use a file database.
3. Re-reading a whole state before the acknowledgement costs up to 1.3 s and a fourth tick. Accepted: only
   `ListIslandsAsync` is read on the tick path; fact 14 measures the swap peak.
4. Restore sequences ran the wrong way for ties. Accepted: ascending from the oldest held; a restore tie case in AC10.
5. `Profiles` order could differ after a restart. Accepted: ordered by `Id`; all reads are immutable snapshots.
6. A failed missing-icon read after a stored catalog left memory stale. Accepted: the catalog is marked unusable and
   `Catalog` notified; test in AC6.
7. Documentation missed sentences that become false. Accepted: `USER_GUIDE.md` lines 99, 365, 743 and the German
   twin of 99, `docs/areas/README.md` line 46 and `ingest.md` line 114 are in *Documentation updates*.

## Security review

Security plan review, round 1: `CHANGES_REQUIRED`.

- B1, the memory bound omitted island lists and the accepted growth was unwritten. Resolved: *Security
  considerations*, record 0069 *Consequences*, `server-state.md` *Memory*, the verbatim `ingest.md` *Accepted risks*
  entry and the `SECURITY.md` sentence. No cap (*Options considered*).
- B2, no test that refused input stores nothing. Resolved: `IngestSessionRunAsyncRefusedHelloStoresNothing`,
  `IngestSessionRunAsyncCompanionConnectedHelloRenamesNothing` (AC5), `IngestSessionRunAsyncRefusedMessagesStoreNothing` (AC3).
- Note 1, place leak on a non-SQLite exception at the hello store: fixed (a place flag released in `EndAsync`; test
  `IngestSessionRunAsyncHelloStoreCancelledFreesPlace`, fact 16).
- Note 2, fact 14 as the store-path peak with the worst-shape tick: taken.
- Note 3, reads on demand unbounded: documented (record 0069, `server-state.md`); bounding them belongs to #32, the
  first caller, since serializing here would make one slow page delay every other.
- Note 4, AC7 needs a file database: taken (cases list).
- Note 5, `TryRestoreAsync` catches only `SqliteException`: left, no issue. A value that fails conversion needs write
  access to the owner-only file (out of scope in `SECURITY.md`), and the start still ends, with an unhandled exception
  instead of the E4 line.
- Note 6, Parsing of external input: added to *Security areas touched*.
- Measurement run: fact 16 of round 1 (the trigger) is now fact 13, confirmed by that run.
