# Plan: WebSocket ingest endpoint with shared token

Source: Issue #28 | [spec.md](spec.md)
Tier: security — the change creates the ingest token, the server's first authentication control, and a new route reachable without login.
Security areas touched: Ingest token and Companion authentication (new control); Network exposure (new route `/ingest` in `Program.cs`); Parsing of external input (`ContractReader`, `CatalogDocumentError` in `src/Censio.Contracts`); Logging and showing external data (the server's ingest log lines); a new secret file in the data directory beside the key ring

## Problem / root cause

Feature: see [spec.md](spec.md).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The server maps no ingest route today | `src/Censio.Server/Program.cs`: pages, `/health`, static assets only | confirmed |
| 2 | A refused read returns no message, so no `id` | `ContractReader.Read` returns `Refuse(error)` with `message` `null` on every refusal (`src/Censio.Contracts/ContractReader.cs`, `Read`, `Refuse`) | confirmed |
| 3 | A non-canonical UUID is read as the all-zero `Guid` and refused as *invalid* | `ContractGuidJsonConverter.Read` returns `Guid.Empty`; `ContractRules.CheckIdentity` requires `!= Guid.Empty` | confirmed |
| 4 | The reader builds the catalog document of an upload and drops it | `ContractReader.CheckCatalog` calls `CatalogDocumentReader.Read` and keeps only `Error` | confirmed |
| 5 | `CatalogDocumentError` carries no version, but an acknowledgement `catalogUnsupportedVersion` needs `refusedVersion` | `CatalogDocumentError` members `Kind`, `Text`; `ContractRules.CheckAcknowledgement` requires `RefusedVersion` set exactly for the two unsupported kinds | confirmed |
| 6 | The refused hello reply needs `receivedVersion` and 1 to 16 ascending `supportedVersions` | `ContractHelloRefused` (`required int ReceivedVersion`), `ContractRules.CheckHelloRefused` | confirmed |
| 7 | A refused hello reply of another version in the frozen form is returned as a message, not as *unsupported version* | `ContractReader.ReadOtherVersion` | confirmed |
| 8 | .NET keep-alive with a timeout: a ping is sent after the interval without received data; received payload bytes only postpone the next ping; only the matching pong clears an outstanding ping; a missed pong faults and aborts the socket; heartbeat every `min(interval, timeout) / 4` | `dotnet/runtime` `release/11.0` (3b16722) `ManagedWebSocket.KeepAlive.cs` (`KeepAlivePingHeartBeat`, `OnDataReceived`, `OnPongResponseReceived`), `ManagedWebSocket.cs` lines 948, 1628; read in a scratch sparse clone | confirmed |
| 9 | With 15 s and 40 s a vanished peer is closed within 15 + 40 + 3.75 s < 60 s, an idle peer that keeps a receive pending stays, a message streamed for more than 60 s stays when no ping was outstanding at its start | to verify: arithmetic from fact 8 checked by a scratch measurement on Kestrel with a `ClientWebSocket` and the three cases, Dev, step 6, result and command in `ingest.md` | unverified |
| 10 | A client answers a ping only inside its own receive | `ManagedWebSocket.HandleReceivedPingPongAsync` runs in the receive path | confirmed |
| 11 | Cancelling a pending `ReceiveAsync` aborts the socket, so no close frame can follow; `CloseOutputAsync` may run while a receive is pending | `ManagedWebSocket.cs` line 790 (`Register(... Abort())`), class remarks line 25 | confirmed |
| 12 | `WebSocketAcceptContext` has `KeepAliveInterval` and `KeepAliveTimeout` in ASP.NET Core 11 | to verify: the skeleton build, Dev, step 4 | unverified |
| 13 | `TestServer`'s WebSocket client is an in-memory socket without ping/pong or frame-level bounds | to verify: read `Microsoft.AspNetCore.TestHost` `TestWebSocket` source, Tester, step 5; hence session tests use real `ManagedWebSocket` pairs | unverified |
| 14 | Distinct icon references of one catalog fit `missingIcons` (16,384) | `CatalogDocumentReader.MaxEntries` 16,384 = `ContractReader.MaxMissingIcons` | confirmed |
| 15 | Keeping the catalog the reader builds adds no second read; the peak stays the measured read peak plus the retained model until the reply is written | to verify: `contract.md`, *Memory of a read*, is extended by one catalog-upload session run at the bound with peak managed memory sampled, Dev, step 6, recorded in `ingest.md` | unverified |
| 16 | Record 0019 is released, so it is not edited | `git tag --contains 443ecdc`: `v0.1.0`, `v0.2.0` | confirmed |
| 17 | The issue's "shown once in the logs" conflicts with its "never logged" | issue #28 body | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `IngestTokenSetupTryCreateAccessWithoutFileGeneratesTokenFile`, `IngestTokenSetupTryCreateAccessWithFileReadsSameToken`, `IngestTokenSetupTryCreateAccessAfterFileDeletedGeneratesDifferentToken`, `IngestTokenSetupTryCreateAccessLogsNoTokenText`, `IngestTokenSetupTryCreateAccessCreatesOwnerOnlyFile`, `IngestTokenSetupGenerateTokenReturnsBase64UrlOf43Characters` | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs` |
| AC2 | `IngestSessionRunAsyncWithStoredTokenRepliesHelloAccepted`, `IngestEndpointConnectWithStoredTokenReceivesHelloAccepted` | `IngestSessionTests.cs`, `IngestEndpointTests.cs` |
| AC3 | `IngestSessionRunAsyncWithWrongTokenRepliesTokenRefused` (DataRows: wrong, last character changed, `null`), `IngestSessionRunAsyncAfterTokenRefusedReadsNoFurtherMessage`, `IngestAccessIsTokenAcceptedRefusingAllRefusesEveryToken` | `IngestSessionTests.cs`, `IngestAccessTests.cs` |
| AC4 | `IngestAccessIsTokenAcceptedMatchingTokenReturnsTrue`, `IngestAccessIsTokenAcceptedOtherLengthReturnsFalse`, `IngestSessionRunAsyncLogsNoTokenOrMessageContent`; manual: Security, diff review step 8, reads `IngestAccess` (hash and fixed-time comparison, no token text kept) | `IngestAccessTests.cs`, `IngestSessionTests.cs` |
| AC5 | `IngestSessionRunAsyncWithVersionTwoHelloRepliesUnsupportedVersion` (DataRows: right token, wrong token) | `IngestSessionTests.cs` |
| AC6 | `IngestSessionRunAsyncWhileCompanionConnectedRepliesCompanionConnected`, `IngestSessionRunAsyncAfterFirstClosesAcceptsNextHello`, `IngestSessionRunAsyncAfterConnectionLostReleasesCompanion`, `IngestConnectionsTryTakeCompanionWhenTakenReturnsFalse`, `IngestEndpointCreateAcceptContextSetsKeepAlive`; manual: Dev, scratch measurement of fact 9 on Kestrel, step 6 | `IngestSessionTests.cs`, `IngestConnectionsTests.cs`, `IngestEndpointTests.cs` |
| AC7 | `IngestTokenSetupTryCreateAccessRequireTokenFalseReturnsOpenAccess` (DataRows `false`, `FALSE`), `IngestTokenSetupTryCreateAccessRequireTokenFalseTouchesNoFile`, `IngestTokenSetupTryCreateAccessRequireTokenTrueOrUnsetRequiresToken`, `IngestTokenSetupTryCreateAccessInvalidRequireTokenStops` (DataRows `""`, `no`, `0`, ` true`), `IngestAccessIsTokenAcceptedOpenAcceptsAnyToken` | `IngestTokenSetupTests.cs`, `IngestAccessTests.cs` |
| AC8 | `IngestTokenSetupTryCreateAccessInvalidFileStops` (DataRows: empty, white space only, 1,025 characters, a space inside, a byte above `~`), `IngestTokenSetupTryCreateAccessUnwritableDirectoryStops`, `IngestTokenSetupTryCreateAccessWithoutDataDirectoryRefusesAll`, `ProgramStartWithInvalidTokenFileExitsWithCodeOne` | `IngestTokenSetupTests.cs`, `ProgramTests.cs` |
| AC9 | `IngestSessionRunAsyncAcceptedMessagesAcknowledgedInOrder`, `IngestSessionRunAsyncCatalogUploadListsSortedDistinctIcons`, `IngestSessionRunAsyncStatusGetsNoReply`, `IngestSessionRunAsyncOnCloseLogsCounts` | `IngestSessionTests.cs` |
| AC10 | `IngestSessionRunAsyncInvalidMessageWithIdAcknowledgesRefusal`, `IngestSessionRunAsyncCatalogRefusalAcknowledgesCatalogKind` (DataRows: the four catalog kinds), `IngestSessionRunAsyncUnreadableMessageClosesConnection` (DataRows: malformed, version 2, invalid `id`, `helloAccepted`, `acknowledgement` with a valid `id`, second hello), `IngestSessionRunAsyncBinaryFrameCloses1003`, `IngestSessionRunAsyncOnStoppingCloses1001`, `IngestEndpointAfterRefusedMessageHealthStillAnswers`, `ContractReaderReadInvalidMessageReturnsRefusedId`, `ContractReaderReadMalformedMessageReturnsNoRefusedId`, `ContractReaderReadCatalogUploadReturnsCatalogDocument`, `CatalogDocumentReaderReadUnsupportedVersionReturnsVersion` | `IngestSessionTests.cs`, `IngestEndpointTests.cs`, `tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `CatalogDocumentReaderTests.cs` |
| AC11 | `IngestSessionRunAsyncOversizedHelloCloses1009`, `IngestSessionRunAsyncOversizedMessageReadsNoFurther`, `IngestSessionRunAsyncHelloAfterTenSecondsCloses1008`, `IngestSessionRunAsyncTrickledHelloPastDeadlineCloses1008`, `IngestEndpointSeventeenthPendingConnectionReturns503`, `IngestEndpointPlainRequestReturns400`, `IngestEndpointUpgradeWithOriginReturns403`, `IngestConnectionsTryEnterPendingHelloSeventeenthReturnsFalse` | `IngestSessionTests.cs`, `IngestEndpointTests.cs`, `IngestConnectionsTests.cs` |
| AC12 | manual: Reviewer, step 8, against the documentation updates below (English and German sections, the configuration table row) | — |

## Approach

**Contracts.** `ContractReadResult` gains `RefusedId`: set when the reader built a tick, session start, session end,
catalog upload or icon image before refusing it (rules or catalog step) and its `id` is not all zero; `null` for every
other refusal and for server-to-Companion types. It gains `CatalogDocument`, set for an accepted catalog upload from the
read `CheckCatalog` already does (no second read, record 0063). `CatalogDocumentError` gains `Version`, set for
*unsupported version*. Nothing else in the reader changes.

**Token** (`IngestTokenSetup.TryCreateAccess`, called in `Program.cs` after `TryCreateKeyRing`, `null` → exit 1):

1. `Censio:Ingest:RequireToken`: unset → required; `true`/`false` compared ordinal ignoring case, no trimming; anything
   else → `Critical` `The setting Censio:Ingest:RequireToken must be true or false; the server stops.` (the value is not
   logged).
2. `false` → warning (spec S6) and `IngestAccess.CreateOpen()`; the file is not touched.
3. No data directory → warning (spec E11) and `CreateRefusingAll()`.
4. `<full data directory>/ingest-token` exists → read at most 65,536 bytes (larger is invalid), trim space, tab, CR, LF
   at both ends, require 1 to 1,024 bytes each `0x21`–`0x7E`; log `The ingest token is read from '<file>'.` Otherwise
   generate 32 bytes of `RandomNumberGenerator`, `Base64Url` without padding, write with `FileMode.CreateNew` and on
   Unix `UnixCreateMode` user read/write, log the S1 line. `CreateForToken(token)`.
5. `Critical` texts, exception attached where one exists:
   `The ingest token file '<file>' cannot be read; the server stops.`,
   `The ingest token file '<file>' holds no valid token (1 to 1,024 visible ASCII characters); the server stops.`,
   `The ingest token file '<file>' cannot be created; the server stops. Make the data directory writable for the user the server runs as.`
   Handled exceptions: `IOException`, `UnauthorizedAccessException`; their texts name paths, never content.

`IngestAccess` keeps only `SHA256(token)`; `IsTokenAccepted` hashes the presented UTF-8 token (`null` → refused when
required) and compares with `CryptographicOperations.FixedTimeEquals`. No `ToString` override, no token field.

**Endpoint** (`MapCensioIngest`, after `app.UseWebSockets()`): not a WebSocket request → `400`; an `Origin` header →
`403`; `IngestConnections.TryEnterPendingHello()` false → `503`; all with an empty body. Then accept with
`CreateAcceptContext()` (keep-alive 15 s / 40 s, no subprotocol) and run `IngestSession.RunAsync` with
`IHostApplicationLifetime.ApplicationStopping`. The session owns the pending place from then on.

**Session**, one per connection, logger category `Censio.Server.Ingest`:

- All sends (replies and close frames) go through one send lock; a close frame from the deadline or the stopping token
  is sent concurrently with the pending receive (fact 11), never by cancelling the receive; then the socket is aborted.
- Reading a message: chunks into a buffer until the end of message, never more than bound + 1 payload bytes (the
  receive buffer is sliced to what remains); over → close 1009; binary → 1003; a close from the peer ends the session.
- Hello phase: a `TimeProvider` timer of 10 s from the start of `RunAsync`; it acts only while the phase is open (one
  interlocked flag decides between the timer and the decision) and closes with 1008. Bound 32,768 bytes. Then:
  reader *unsupported version* → `helloRefused` `unsupportedVersion` with the error's version; any other error, or a
  message other than `ContractHello` → 1008 without reply; token refused → `tokenRefused`; `TryTakeCompanion` false →
  `companionConnected`; each refusal with `receivedVersion` 1 (or the found version), `supportedVersions` `[1]`, then
  close 1000. Accepted → `helloAccepted` (`ProductVersion.Read` of the server assembly, `catalogIdentity` `null`). The
  pending place is left exactly once when the phase ends.
- Message phase (bound 25,165,824): order of decisions — (1) a message of type hello, helloAccepted, helloRefused or
  acknowledgement → 1008; (2) `status` → no reply; (3) the five id types → acknowledgement, `refusal` `null`,
  `missingIcons` the distinct non-null `IconReference`s of `CatalogDocument` sorted ordinal (catalog upload) or empty;
  (4) error *invalid* or *catalog refused* with `RefusedId` → acknowledgement with the mapped kind (catalog kinds by
  `CatalogError.Kind`, `refusedVersion` from `CatalogError.Version` for *unsupported version*); (5) error *too large* →
  1009; every other error → 1008. Counts: (2) and (3) accepted, (4) and a closing message refused.
- Stopping token → close 1001. A receive that throws (`WebSocketException`, `OperationCanceledException` from the abort,
  `IOException`) ends the session without a log line of its own. A write refusal of `ContractWriter` (not reachable with
  checked values) aborts the socket.
- `finally`: release the Companion place if taken, log the S5 line with the counts (only after an accepted hello).

Log lines (layout fixed by the spec's texts): S1, S2, S6, E11 from the setup; `Companion connected.`, the three
`Companion refused: …` warnings (E1–E3) and the S5 line from the session. No E4 line. No argument of any line comes
from a message except the found version number (an `int`).

**Data seen by tests.** The session runs on a `WebSocket` and a `TimeProvider` given to it, so `IngestSessionTests`
drive a real `ManagedWebSocket` pair (`WebSocket.CreateFromStream`, server and client) over the test double
`InMemoryDuplexStream` with `FakeTimeProvider`; `IngestEndpointTests` use `CensioServerFactory` for the HTTP statuses and
one end-to-end hello.

## Options considered

- Second Companion: replace against refuse — record 0062.
- Liveness: heartbeat message, own watchdog, .NET ping keep-alive — record 0062.
- Token: log once, settings page, configured token, in-memory, file — record 0061; no data directory: stop or refuse — 0061.
- Refused messages: close always, lenient second parser, reader exposes the `id` and catalog, acknowledge only once
  stored — record 0063 (DA item 7 settled here: no second catalog read).
- Browser access and pending connections: origin allow-list, refuse every `Origin`, no bound, fixed bound, bound per
  address — record 0064.
- Session timing on a fake clock against the real keep-alive: the hello deadline uses `TimeProvider`; the keep-alive
  cannot (fact 8) and is measured instead.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Contracts | `ContractReadResult`, `ContractReader`, `CatalogDocumentError`, `CatalogDocumentReader` (version passed) | refused `id`, catalog document, catalog error version |
| Censio.Server | `IngestTokenSetup`, `IngestAccess`, `IngestConnections`, `IngestSession`, `IngestEndpoint` (new, flat in the project root like `DataProtectionSetup`) | new |
| Censio.Server | `Program.cs` | `AddCensioIngest`, token setup after the key ring, `UseWebSockets`, `MapCensioIngest` |
| Censio.Server.Tests | `InMemoryDuplexStream` (new test double) | a duplex stream over two pipes that counts bytes read |

## Signatures (for the Dev's skeleton)

Censio.Contracts:

- `public Guid? RefusedId { get; }` (`ContractReadResult`)
- `public CatalogDocument? CatalogDocument { get; }` (`ContractReadResult`)
- `public int? Version { get; }` (`CatalogDocumentError`)

Censio.Server, namespace `Censio.Server`:

- `public static partial class IngestTokenSetup`
- `public const string RequireTokenKey = "Censio:Ingest:RequireToken";`
- `public const string TokenFileName = "ingest-token";`
- `public const int MaxTokenLength = 1024;`
- `public static IngestAccess? TryCreateAccess(IConfiguration configuration, ILogger logger)`
- `public static string GenerateToken()`
- `public sealed class IngestAccess`
- `public static IngestAccess CreateOpen()`
- `public static IngestAccess CreateRefusingAll()`
- `public static IngestAccess CreateForToken(string token)`
- `public bool IsTokenRequired { get; }`
- `public bool IsTokenAccepted(string? presentedToken)`
- `public sealed class IngestConnections`
- `public const int MaxPendingHellos = 16;`
- `public bool TryEnterPendingHello()`
- `public void LeavePendingHello()`
- `public bool TryTakeCompanion()`
- `public void ReleaseCompanion()`
- `public sealed partial class IngestSession`
- `public const int MaxHelloBytes = 32768;`
- `public static TimeSpan HelloTimeout { get; }` (10 seconds)
- `public IngestSession(WebSocket webSocket, IngestAccess access, IngestConnections connections, TimeProvider timeProvider, ILogger logger, string serverVersion)`
- `public Task RunAsync(CancellationToken stoppingToken)`
- `public static class IngestEndpoint`
- `public const string Path = "/ingest";`
- `public static TimeSpan KeepAliveInterval { get; }` (15 s)
- `public static TimeSpan KeepAliveTimeout { get; }` (40 s)
- `public static WebSocketAcceptContext CreateAcceptContext()`
- `public static IServiceCollection AddCensioIngest(this IServiceCollection services)` (registers the connections singleton and the system clock if none is registered)
- `public static IEndpointConventionBuilder MapCensioIngest(this IEndpointRouteBuilder endpoints, IngestAccess access)`

Test double: `public sealed class InMemoryDuplexStream : Stream` with `public static (InMemoryDuplexStream Server, InMemoryDuplexStream Client) CreatePair()` and `public long BytesRead { get; }`.

Names checked against *Writing code*: no nested type, no enum, no suffix S2344 refuses; no parameter or member name that
*Logger arguments* lists as sensitive.

## Test files

New: `tests/Censio.Server.Tests/IngestTokenSetupTests.cs`, `IngestAccessTests.cs`, `IngestConnectionsTests.cs`,
`IngestSessionTests.cs`, `IngestEndpointTests.cs`, the double `InMemoryDuplexStream.cs`. Extended:
`tests/Censio.Server.Tests/ProgramTests.cs`, `tests/Censio.Contracts.Tests/ContractReaderTests.cs`,
`CatalogDocumentReaderTests.cs`.

Existing test code that calls a changed signature: none (the changed constructors are internal; `ProgramTests` starts
`Program` with a data directory, which now also creates `ingest-token` there — its key-ring assertions are unaffected;
Tester checks in step 5 and adapts if not).

Files no unit test can cover: none (the `Program.cs` lines run under `CensioServerFactory`).

## Areas

- New area **ingest** (`docs/areas/ingest.md`), scope for the index: "The Companion's WebSocket ingest on the server:
  the endpoint and its HTTP refusals, the hello and its checks in order, the token (file, generation, rotation, switch,
  comparison), one Companion and silent connections, acknowledgements and closes, bounds and times, log lines, privacy";
  not here: the contract's messages and reader (see contract), storing and showing data (#30, #32). Content: spec S1–S7,
  E1–E13, N1–N6 as built, the accepted risk of E8, the keep-alive limit, and the measurements of facts 9 and 15.
- `server.md`: `/ingest` among the routes reachable without login; the data directory also holds `ingest-token`;
  *Not here* points to ingest.
- `contract.md`: *Scope* ("nothing sends or receives these messages yet"), *Not here* and *Security considerations for
  the endpoint (#28)* point to ingest; the reader returns the refused `id` and the catalog document (*The reader's
  checks*).

## Documentation updates

All by the Dev, after the code is green:

- `docs/areas/ingest.md` and its row in `docs/areas/README.md` (before the step-7 stamp: records 0061–0064 name the area,
  `decision-check.py` fails without the row); `server.md`, `contract.md` as above; `game-catalog-format.md` only if it
  states that a catalog error carries no version (Dev checks).
- `README.md`, English and German: *Status* (the server takes a Companion's data on `/ingest` with a token; still nothing
  sends), *Server (Docker)* (reading the token: `docker exec censio cat /config/ingest-token`; rotation), the
  configuration table row `Censio:Ingest:RequireToken` / `CENSIO__INGEST__REQUIRETOKEN` / `true` / meaning with the risk;
  the `Censio:DataDirectory` row also names `ingest-token`.
- `docs/USER_GUIDE.md`, English and German: a section on the ingest endpoint and token under *Server (Docker)* (address
  `ws://<host>:8080/ingest`, the token file, rotation, the switch and its risk, the start failures E10–E12 with their
  texts, the one-Companion rule, the 16-connection lock-out); *Data directory and NAS hosts* names the file;
  *Reporting problems* keeps "redact tokens".
- `docs/ARCHITECTURE.md`: server row "Web host, UI, the ingest", *Data flow* (the endpoint, nothing stored until #30),
  *Security model* and *Configuration* (token required by default, the switch and its risk), linking `ingest.md` and
  records 0061–0064.
- `SECURITY.md`: *Deployment Security Considerations* — the generated token in the data directory instead of an
  environment variable, the switch and its risk, browser upgrades refused, the lock-out risk; *Scope* line of the
  WebSocket ingest names `/ingest` and the token file.
- `.squad/project.md` (Dev reports the exact text, the orchestrator writes it): *Security areas* ingest line (types
  above, link `ingest.md`), *Network exposure* adds `/ingest`, *Data-protection key ring* line or a new line for
  `ingest-token`; *Guarantees* token line links `ingest.md`; *Integration surface* config option list adds
  `Censio:Ingest:RequireToken` in `IngestTokenSetup` and `IngestTokenSetupTests`. *Test doubles*: the Tester reports a
  line for `InMemoryDuplexStream`.

## Architecture check

Guarantee "the server stores data only with the shared token, which only an explicit setting switches off" (record 0019):
kept — required by default, only `false` switches it off, no token means refusal (E11). Plain HTTP and open `/health`
(record 0006): unchanged; `/ingest` is a new open route that authenticates by the hello. Local mode (record 0015): the
code assumes no container path; the data directory is configuration.

## Security considerations

- [x] Limits: the hello bound 32,768 and the message bound 25,165,824 apply to the raw WebSocket payload before any
  decoding (the reader's own bounds follow, `contract.md`); the token file limit 65,536 bytes raw, the token 1,024 bytes
  after trimming, ASCII only so bytes equal characters.
- [x] Exceptions reaching a user-visible text: `IOException` and `UnauthorizedAccessException` of the token file, logged
  as the attached exception of the `Critical` lines (path only, never content); WebSocket exceptions end a session
  silently; nothing reaches the Companion but contract texts.

## Decision records

- `docs/decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md` (Proposed)
- `docs/decisions/0062-one-companion-second-refused-silence-found-by-websocket-pings.md` (Proposed)
- `docs/decisions/0063-refused-contract-messages-acknowledged-by-id-or-the-connection-closed.md` (Proposed)
- `docs/decisions/0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md` (Proposed)

## Out of scope / follow-ups

- Proposed issue "[Server] Ingest token set by configuration": a token from `Censio:Ingest:Token` for operators who
  manage secrets themselves (record 0061, option 3).
- Proposed issue "[Server] Rate limit ingest connections per host": closes the lock-out of record 0064.
