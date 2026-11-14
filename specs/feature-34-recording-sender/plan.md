# Plan: Send recordings to a server for testing

Source: Issue #34 | [spec.md](spec.md)
Tier: security — the Companion makes its first outbound network connection and reads, holds and presents the ingest token, a credential (network exposure, handling of secrets).
Security areas touched: Outbound calls (new: the replay's connection to a server); Ingest token and Companion authentication (the token on the client side); Parsing of external input (server replies through `ContractReader`, the recording header check, the `--server` address); File paths the Companion opens (the token file); Logging and showing external data (new `CompanionLog` lines, tray texts); build configuration of `tests/Censio.Companion.Core.Tests` (a framework reference)

## Problem / root cause

See [spec.md](spec.md), *Problem*: no Companion sends contract messages; the replay stays local.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | No Companion code opens a network connection today | `grep -rn "ClientWebSocket\|HttpClient" src` finds nothing; `.squad/project.md`, *Outbound calls* | confirmed |
| 2 | The replay runner raises ticks only through a synchronous event and has no way to wait for a consumer | `src/Censio.Companion.Core/PipeReplayRunner.cs:90`, `PublishTick` at 205-230 | confirmed |
| 3 | The read observer receives every read with its stamp, and all items of one read are dequeued before the next read starts, so the stamp of the last observed read is the stamp of a session item when it is returned | `src/Censio.Pipe/PipeTickAssembler.cs:97-118` (queue first), `175-188` (`Feed`), `207-215` (observer per read) | confirmed |
| 4 | A repeated session end is a result kind of its own | `src/Censio.Pipe/PipeTickBuilder.cs:160-163` | confirmed |
| 5 | A contract tick needs 1 to 256 islands; a tick whose islands have no entries is valid | `src/Censio.Contracts/ContractRules.cs:392-393`; `PipeTick.IsNoDataYet` is "every island has no entries" (`src/Censio.Pipe/PipeTick.cs:33`) | confirmed |
| 6 | A replayed quiet tick closes when the next frame is read, not by a timer | `PipeTickAssembler.cs:152-168` with the replay clock; record 0036, option 6 | confirmed |
| 7 | `PipeRecordingReader.ReadAsync` checks the header on its first call and reports `NotARecording`, `UnsupportedVersion`, `ReadFailed` as a problem line | `src/Censio.Pipe/PipeRecordingReader.cs:95-115` | confirmed |
| 8 | The server answers a ping only-with-receive-pending client late and aborts it after about 56 s | `docs/areas/ingest.md`, *One Companion and silent connections*, *Measurements* | confirmed |
| 9 | `Censio.Server.Tests` already references `Censio.Companion.Core`, links `long-session.jsonl`, and connects WebSockets to the real endpoint through `factory.Server.CreateWebSocketClient()` | `tests/Censio.Server.Tests/Censio.Server.Tests.csproj:16,21`; `IngestEndpointTests.cs:137,227` | confirmed |
| 10 | The TestServer WebSocket allows a send while a receive is pending on the same socket | to verify: Tester, step 5, the first `IngestEndpointTests` replay test | unverified |
| 11 | A `ClientWebSocket` connected through an `HttpMessageInvoker` over `SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }` uses no proxy, follows no redirect and sends no `Origin` | to verify: Tester, step 5, `ClientWebSocketConnectorTests` (each test must fail on a connector that uses `ClientWebSocket.ConnectAsync(uri, ct)` with default options, checked once in a scratch worktree) | unverified |
| 12 | Windows PowerShell 5.1 `>` writes UTF-16 LE with a byte order mark; `docker cp` copies the `0600` token file out of the container byte for byte | to verify: Product Manager, AC12 dry run | unverified |
| 13 | `dotnet run --project src/Censio.Server` uses the launch profile (`http://localhost:51849`, Development, a browser); with `--no-launch-profile` the server takes `--Censio:DataDirectory=<folder>` from its arguments and listens on `http://localhost:5000` | `src/Censio.Server/Properties/launchSettings.json`; `src/Censio.Server/Program.cs:8` (`CreateBuilder(args)`); README, *Configuration*; the run is shown by AC12 | confirmed |
| 16 | The Windows shell logs a faulted background task with its full exception | `src/Censio.Companion/App.xaml.cs:411`; `docs/areas/companion-log.md` | confirmed |
| 14 | The fixture's 33 ticks: `<n>` of AC1 is the count with at least one island | `tests/Censio.Pipe.Tests/Fixtures/README.md`; the Tester computes `<n>` from the replay in the test, never a typed number | confirmed |
| 15 | The released tags are `v0.1.0` and `v0.2.0`; records 0079 and 0080 are new | `git ls-remote --tags origin` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `IngestEndpointReplaySentFixtureFillsPages`, `IngestEndpointReplaySentFixtureCountsTicksWithIslands`; `TrayMenuReplaySendFinishedGivesCounts` (DataRows 0, 1, 31 ticks and 1 refused); `ReplaySenderTickWithoutIslandsIsNotSent` (E10: the assembler emits no tick without islands, so the rule is tested at the sender) | `tests/Censio.Server.Tests/IngestEndpointTests.cs`; `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`; `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs` |
| AC2 | `IngestEndpointReplaySentFixtureNotifiesFollowersPerTick`, `IngestEndpointReplaySentShowsCompanionConnectedThenNot` | `tests/Censio.Server.Tests/IngestEndpointTests.cs` |
| AC3 | `ReplaySenderHelloCarriesNewProfileNameAndToken`, `ReplaySenderWithoutTokenFileSendsNoToken`; `IngestEndpointSecondReplayCreatesSecondProfile` | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs`; `tests/Censio.Server.Tests/IngestEndpointTests.cs` |
| AC4 | `CompanionCommandLineServerSwitchesGiveInvalid` (one DataRow per E1 case), `CompanionCommandLineReplayWithServerGivesAddressAndTokenFile`, `CompanionCommandLineReplayWithoutServerHasNoAddress` | `tests/Censio.Companion.Core.Tests/CompanionCommandLineTests.cs` |
| AC5 | `ReplayTokenFileUnreadableGivesCannotBeRead` (missing, folder), `ReplayTokenFileInvalidContentGivesNoValidToken` (empty, 31 and 1,025 characters, a space inside, a character above `~`, 65,537 bytes, invalid UTF-8, UTF-16 without mark), `ReplayTokenFileSurroundingWhiteSpaceIsTrimmed`, `ReplayTokenFileByteOrderMarksGiveSameToken` (UTF-8, UTF-16 LE, UTF-16 BE); `PipeReplayRunnerTokenFileProblemConnectsNever` | `tests/Censio.Companion.Core.Tests/ReplayTokenFileTests.cs`; `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC6 | `PipeReplayRunnerHeaderProblemWithSenderConnectsNever` (missing file, not a recording, unsupported version) | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC7 | `ReplaySenderUnreachableServerGivesServerUnreachable`, `ReplaySenderHelloRefusedGivesReason` (three reasons), `ReplaySenderNoHelloReplyGivesNoAnswer`, `ReplaySenderNoAcknowledgementGivesNoAnswer`, `ReplaySenderSendNeverCompletesGivesNoAnswer` (a socket whose send never completes, before and after a pending status send holds the send lock), `ReplaySenderCloseAfterHelloGivesConnectionLost`, `ReplaySenderInvalidReplyGivesInvalidServerReply` (not JSON, binary, `helloAccepted` after the hello, acknowledgement of another id, acknowledgement while nothing is awaited); `ClientWebSocketConnectorHttpAnswerGivesNoSocket`, `ClientWebSocketConnectorRedirectGivesNoSocket`, `ClientWebSocketConnectorUntrustedCertificateGivesNoSocket`, `ClientWebSocketConnectorSendsNoOrigin`, `ClientWebSocketConnectorBypassesSystemProxy`, `ClientWebSocketConnectorNoUpgradeWithinTimeoutGivesNoSocket`; `TrayMenuReplaySendProblemGivesReasonText` (one DataRow per kind) | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs`; `tests/Censio.Companion.Core.Tests/ClientWebSocketConnectorTests.cs`; `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`; manual: Dev, the successful `wss://` connection (S4) in a disposable container of the .NET SDK image whose own store trusts a scratch CA, a scratch program with `ClientWebSocketConnector` against a Kestrel `wss://` endpoint, result in `docs/areas/replay-sending.md`, *Measurements* |
| AC8 | `ReplaySenderRefusedAcknowledgementCountsRefused`, `ReplaySenderWriterRefusalCountsRefusedAndGoesOn` | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs` |
| AC9 | `ReplaySenderSendsNextMessageOnlyAfterAcknowledgement`, `ReplaySenderStateChangeSendsStatusWithoutWaiting`, `ReplaySenderReplyOverBoundReadsNoFurther`; `PipeReplayRunnerRepeatedSessionEndIsNotSent`, `PipeReplayRunnerSessionEventCarriesRecordedStamp` | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs`; `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC10 | `PipeReplayRunnerInvalidLineAfterTicksClosesConnection`, `PipeReplayRunnerCancelledAbortsConnection`, `PipeReplayRunnerSecondOpenFailureClosesConnection` (an `openRecording` that succeeds once, then throws) | `tests/Censio.Companion.Core.Tests/PipeReplayRunnerTests.cs` |
| AC11 | `ReplaySenderLogsConnectionAndEnd`, `ReplaySenderLogsNeitherTokenNorAddressNorNames` (every failure path), `ReplaySenderUnlistedExceptionGivesConnectionLostWithoutAddress` (a test `WebSocket` whose send and receive throw an exception type of no catch list, its message holding the address) | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs` |
| AC12 | manual: Product Manager on Windows, on the branch head: the Companion built from source as the README says, the server image built from the checkout (*Build from source*) and the server started from source; runs the documented steps of both languages at `--speed 10`; confirms facts 12 and 13 | — |
| AC13 | `ReplaySenderFrameBetweenMessagesIsReadAtOnce` (a receive is pending between messages); manual: Dev, a scratch measurement on the published server (`dotnet publish src/Censio.Server -c Release`), a replay whose next tick follows 70 s after an acknowledgement, recorded in `docs/areas/replay-sending.md`, *Measurements* | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs` |

## Approach

**Command line.** `CompanionCommandLine` takes `--server <address>` and `--token-file <file>`, case-insensitive like
the other switches. Valid only with `--replay`; `--token-file` only with `--server`. The address must parse as an
absolute URI with scheme `ws` or `wss`, a non-empty host and empty user info; its text must hold no `#`, no
white space and no control character (checked on the raw argument, since `Uri` drops an empty fragment and trims).
A query is allowed. The token file path follows the replay path rule (`IsPath`). Every other combination of the
spec's E1 is `CompanionMode.Invalid`.

**Token file.** `ReplayTokenFile.ReadFile` opens the file read-only (`FileShare.Read`), reads at most 65,537 bytes
(more than 65,536 is *no valid token*), decodes by the byte order mark (EF BB BF UTF-8, FF FE UTF-16 LE, FE FF
UTF-16 BE, none: UTF-8) with throwing decoders (an invalid sequence is *no valid token*), trims space, tab, CR and
LF, and accepts 32 to 1,024 characters `!` to `~`. `IOException`, `UnauthorizedAccessException`, `ArgumentException`,
`NotSupportedException` and `SecurityException` give *cannot be read*. The token string is kept only by the sender.

**Connector.** `IReplayServerConnector.ConnectAsync` returns the open `WebSocket` or `null`. `ClientWebSocketConnector`
creates a `ClientWebSocket` without subprotocol, `Origin`, client certificate or certificate callback and connects
through an `HttpMessageInvoker` over `SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }` (default
TLS validation of the operating system), under a cancellation of `ConnectTimeout` (30 s) on the injected
`TimeProvider`, linked to the caller's token. `WebSocketException`, `HttpRequestException`, `AuthenticationException`,
`IOException`, `InvalidOperationException`, the timeout's `OperationCanceledException` and every other exception
give `null` (the socket disposed); only the caller's cancellation is rethrown. The production constructor sets no
certificate chain policy and no validation callback; there is no test-only trust seam, and no test writes to a
certificate store.

**Sender.** `ReplaySender` holds the connection of one run:

- Constructor: profile identity `Guid.NewGuid()`, name `Replay <yyyy-MM-dd HH:mm:ss>` of the `TimeProvider`'s local
  time (invariant culture).
- `Prepare` reads the token file when one is named (problem kinds `TokenFileCannotBeRead`,
  `TokenFileHoldsNoValidToken`).
- `ConnectServerAsync`: connector `null` gives `ServerUnreachable`. It sends the hello (`ContractWriter`), then receives one
  message within `ReplyTimeout` (30 s): `helloAccepted` starts the receive loop; `helloRefused` maps `tokenRefused`,
  `companionConnected`, `unsupportedVersion` to their kinds; no reply in time `NoAnswer`; a close or a broken
  connection `ConnectionLost`; anything else `InvalidReply`. Logs `ReplaySendConnected` or `ReplaySendRefused`.
- Receive loop (from `helloAccepted` to the close, always one receive pending, answering pings): reads each message
  into one buffer of `MaxReplyBytes + 1` bytes; a message longer than `MaxReplyBytes` (65,536), a binary frame, a
  message `ContractReader` refuses, any type but `acknowledgement`, or an acknowledgement whose id is not the one
  awaited (or none awaited) ends the run as `InvalidReply`; a close frame or any receive exception as `ConnectionLost`.
  The awaited acknowledgement completes the one pending send.
- `SendTickAsync`: a tick without islands returns at once (not counted). Otherwise `ContractMessageConverter.ToTick`,
  then `ContractWriter.Write`; a writer refusal counts as refused and returns no problem. It sends, then waits for the
  acknowledgement within `ReplyTimeout` (`NoAnswer`): `refusal` `null` counts a sent tick, any refusal a refused
  message. `SendSessionStartAsync` and `SendSessionEndAsync` do the same with the converter's session messages (an
  accepted session event is not counted, a refused one is).
- `NotifyState` is synchronous and safe to call from any thread, also while a send runs (the observer runs inside the
  assembler's read, which on the quiet-close path keeps running while the runner awaits a send): under a lock it
  records the state and, when it differs from the last state sent, starts a send of `ContractMessageConverter.ToStatus`
  without waiting for any reply. All sends go through one send lock (`SemaphoreSlim`), so a status never interleaves
  with a message; the status send task is kept and always observed (also after `DisposeAsync`), and its failure ends
  the run as `ConnectionLost` at the next awaited step.
- `CloseAsync` waits for a running status send, closes with `NormalClosure` and waits at most 2 s on the injected
  `TimeProvider` (never a real delay) for the server's close, then aborts; logs `ReplaySendFinished`. `DisposeAsync`
  aborts the socket and cancels the loop (Exit).
- Every exception of a send or receive but the caller's cancellation gives `ConnectionLost`; none leaves the sender.
- Every failure logs `ReplaySendFailed` with the reason text of the tray; no exception object, address, token or
  name is passed to a log call (exception texts can carry the address).

**Runner.** A new `PipeReplayRunner` constructor takes the sender. With it, `RunAsync` runs in this order:

1. `Prepare`; a problem finishes with `SendProblem`.
2. Header check: open the recording and call `PipeRecordingReader.ReadAsync` once; an open failure finishes with
   `FileNotFound`, a problem line of kind other than `InvalidLine` with that problem; then the stream is closed.
3. Publish `IsConnecting`; `ConnectServerAsync`; a problem finishes with `SendProblem`.
4. Publish `Connected`, `NotifyState(Connected)`; replay as today. The observer also stores the stamp of every read
   (behind a lock, since it runs on the read's thread) and calls `NotifyState` for `Receiving` and `ProtocolMismatch`. After each item: a tick goes to `SendTickAsync`
   (after `PublishTick`), a session start or end to its send with the stored stamp, a repeated session end nowhere.
   A send problem stops the replay and finishes with `SendProblem` after `DisposeAsync`.
5. At the end: `CloseAsync`; then a recording problem finishes with that problem (today's text), a close problem with
   `SendProblem`, otherwise with `Sent` = the counts. The recording is opened a second time for the replay; a failure
   of that open, after the connection is open, goes through `CloseAsync` before it finishes with `FileNotFound`, like
   every other end of the run.
6. Cancellation: `DisposeAsync`, no further status.

Without a sender nothing changes.

**Status and tray (layout fixed).** `PipeReplayStatus` gains `IsConnecting`, `SendProblem` and `Sent`.
`TrayMenu.FormatReplayToolTip` decides in this order: `SendProblem` → `Censio Companion: Replay error: <reason>` with
the reasons `token file cannot be read`, `token file holds no valid token`, `server unreachable`, `token refused`,
`another Companion is connected`, `server does not support contract version 1`, `server does not answer`, `server
connection lost`, `invalid server reply`; `Problem` → today's text; `IsConnecting` → `Censio Companion: Replay:
Connecting to server`; `IsFinished` with `Sent` → `Censio Companion: Replay finished: <n> ticks sent, <r> refused`
(`1 tick sent` for one; `<r>` without singular form); `IsFinished` → today's; otherwise today's states.
`TraySnapshot.ForReplay` gives the *problem* look for `SendProblem` and the *waiting* look for `IsConnecting`.

**Log lines** (`CompanionLog`): `ReplaySendConnected` Information `Replay connected to the server`;
`ReplaySendRefused` Warning `Replay refused by the server: {Reason}`; `ReplaySendFailed` Warning
`Replay sending failed: {Reason}`; `ReplaySendFinished` Information `Replay sent {Ticks} ticks, {Refused} refused`.

**Shell.** `App.xaml.cs` `StartReplay` builds a `ReplaySender` with a `ClientWebSocketConnector`, the version the
Companion shows and `TimeProvider.System` when `ServerAddress` is set, and passes it to the new runner constructor.

**Resource bounds and their seams.** One message in flight and one reply buffer of 65,537 bytes: observed through
`InMemoryDuplexStream` (linked into `Censio.Companion.Core.Tests`), whose count of bytes read shows that a reply over
the bound is not read past 65,537 bytes plus frame headers, and through a scripted server that holds an
acknowledgement and sees no second message. Timeouts run on `FakeTimeProvider`.

## Options considered

- **Companion switches** (chosen) vs **a separate console tool** vs **a server-side import**: record 0079.
- **Token from a file with byte order marks** (chosen) vs **command line**, **environment variable**, **settings key**,
  **the server's exact rule**: record 0080.
- **Sender as its own type the runner calls** (chosen) vs **sending inside the runner's tick event**: the event is
  synchronous (fact 2) and cannot wait for an acknowledgement, so one message in flight and stopping on a server
  failure need an awaited call.
- **One token rule shared in `Censio.Contracts`** vs **an own reader in the Companion** (chosen): sharing changes the
  server's token validation, a security control outside this feature, and the Companion's rule differs (byte order
  marks).
- **Proxy and redirect left to `ClientWebSocket`'s defaults** vs **an own `SocketsHttpHandler`** (chosen): the
  defaults use the system proxy and are not documented for redirects (fact 11); the handler states both.
- **Reconnect and resend**: rejected, #40 (record 0079).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `CompanionCommandLine` | two properties, parsing |
| `Censio.Companion.Core` | `ReplayTokenFile`, `ReplayTokenFileResult`, `ReplayTokenFileResultKind` | new |
| `Censio.Companion.Core` | `IReplayServerConnector`, `ClientWebSocketConnector` | new |
| `Censio.Companion.Core` | `ReplaySender`, `ReplaySendProblemKind`, `ReplaySendCounts` | new |
| `Censio.Companion.Core` | `PipeReplayRunner`, `PipeReplayStatus` | constructor, flow; three properties |
| `Censio.Companion.Core` | `TrayMenu`, `TraySnapshot`, `CompanionLog` | texts, look, four log methods |
| `Censio.Companion` | `App.xaml.cs` | wiring |
| tests | `Censio.Companion.Core.Tests.csproj` | `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (Kestrel loopback host), link of `InMemoryDuplexStream.cs` (the end-to-end tests read the `long-session.jsonl` linked into `Censio.Server.Tests`) |

## Signatures (for the Dev's skeleton)

Properties and constants as Type.Member with their declaration in prose; methods and constructors in full.

`CompanionCommandLine`:
- `CompanionCommandLine.ServerAddress` — public Uri? with get and init
- `CompanionCommandLine.TokenFilePath` — public string? with get and init

`PipeReplayStatus`:
- `PipeReplayStatus.IsConnecting` — public bool with get and init
- `PipeReplayStatus.SendProblem` — public ReplaySendProblemKind? with get and init
- `PipeReplayStatus.Sent` — public ReplaySendCounts? with get and init

New types:
- `public enum ReplaySendProblemKind { TokenFileCannotBeRead, TokenFileHoldsNoValidToken, ServerUnreachable, TokenRefused, CompanionConnected, VersionNotSupported, NoAnswer, ConnectionLost, InvalidReply }`
- `public readonly record struct ReplaySendCounts` with `ReplaySendCounts.Ticks` and `ReplaySendCounts.Refused` — each public required int with get and init
- `public enum ReplayTokenFileResultKind { Read, CannotBeRead, NoValidToken }`
- `ReplayTokenFileResult` (public sealed class) with `ReplayTokenFileResult.Kind` (public required ReplayTokenFileResultKind, get and init) and `ReplayTokenFileResult.Token` (public string?, get and init)
- `public static class ReplayTokenFile` with `ReplayTokenFile.MaxFileBytes` (public const int, 65536) and `public static ReplayTokenFileResult ReadFile(string path)`
- `public interface IReplayServerConnector` with `Task<WebSocket?> ConnectAsync(Uri address, CancellationToken cancellationToken);`
- `ClientWebSocketConnector` (public sealed class, implements IReplayServerConnector) with `public ClientWebSocketConnector(TimeProvider timeProvider)`, `ClientWebSocketConnector.ConnectTimeout` (public static TimeSpan, 30 seconds) and its `ConnectAsync` above
- `ReplaySender` (public sealed class, implements IAsyncDisposable) with:
  - `ReplaySender.MaxReplyBytes` — public const int, 65536
  - `ReplaySender.ReplyTimeout` — public static TimeSpan, 30 seconds
  - `public ReplaySender(Uri address, string? tokenFilePath, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`
  - `ReplaySender.ProfileId` — public Guid, get only
  - `ReplaySender.ProfileName` — public string, get only
  - `ReplaySender.Counts` — public ReplaySendCounts, get only
  - `public ReplaySendProblemKind? Prepare()`
  - `public Task<ReplaySendProblemKind?> ConnectServerAsync(CancellationToken cancellationToken)`
  - `public void NotifyState(PipeConnectionState state)`
  - `public Task<ReplaySendProblemKind?> SendTickAsync(PipeTick tick, CancellationToken cancellationToken)`
  - `public Task<ReplaySendProblemKind?> SendSessionStartAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)`
  - `public Task<ReplaySendProblemKind?> SendSessionEndAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)`
  - `public Task<ReplaySendProblemKind?> CloseAsync(CancellationToken cancellationToken)`
  - `public ValueTask DisposeAsync()`

`PipeReplayRunner`: `public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider, ILogger logger, ReplaySender sender)`

`TrayMenu`: `public static string? FormatReplaySendReason(ReplaySendProblemKind kind)`

`CompanionLog`, as its other logger-message methods:
- `public static partial void ReplaySendConnected(ILogger logger);`
- `public static partial void ReplaySendRefused(ILogger logger, string reason);`
- `public static partial void ReplaySendFailed(ILogger logger, string reason);`
- `public static partial void ReplaySendFinished(ILogger logger, int ticks, int refused);`

Names checked against *Writing code*: no enum name ends in the two refused suffixes, no nested type, and no name of
the CodeQL sensitive list (*Logger arguments*).

## Test files

- `tests/Censio.Companion.Core.Tests/CompanionCommandLineTests.cs`, `PipeReplayRunnerTests.cs`, `TrayMenuTests.cs`
  (existing), `ReplayTokenFileTests.cs`, `ReplaySenderTests.cs`, `ClientWebSocketConnectorTests.cs` (new).
- `tests/Censio.Server.Tests/IngestEndpointTests.cs` (existing).
- New test doubles (Tester): `ScriptedIngestServer` (`tests/Censio.Companion.Core.Tests/`, the server side of an
  `InMemoryDuplexStream` pair that reads messages with `ContractReader` and answers by script),
  `DuplexReplayConnector` (an `IReplayServerConnector` that returns the client side of that pair, or `null`),
  `LoopbackWebSocketHost` (a Kestrel host on `127.0.0.1` port 0: WebSocket, HTTP 404, 302, a self-signed TLS
  certificate, a held upgrade, a recording proxy), `TestServerReplayConnector` (`tests/Censio.Server.Tests/`, over
  `factory.Server.CreateWebSocketClient()`).

Existing test call sites of a changed signature: none; the runner's existing constructors stay, and the new
`PipeReplayStatus` properties default to today's values.

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs` (the Windows shell, excluded by construction,
record 0004).

## Areas

- New area `replay-sending` (Lead decision): "Sending a replayed recording to a server: the switches `--server` and
  `--token-file`, the token file, the connection (proxy, redirects, TLS, `Origin`), hello and profile, what is sent
  and in which order, one message in flight, the receive kept pending, timeouts, the status texts, counts, the log
  lines, privacy, the keep-alive measurement"; *Not here*: the replay itself (pipe-recording), the switches' table
  (companion), the server's side (ingest).
- `companion.md`, `pipe-recording.md`, `contract.md`, `ingest.md`, `companion-log.md`: see below.

## Documentation updates

All by the Dev after the code is green, unless named otherwise:

- `docs/areas/replay-sending.md` and its row in `docs/areas/README.md`; records 0079 and 0080 name it, so
  `decision-check.py` fails until it exists.
- `docs/areas/companion.md`: *Command line* table rows for `--server` and `--token-file` and the invalid
  combinations; *Replay* the new status texts and looks; scope sentence and *Not here* (server connection now in
  replay-sending for a replay).
- `docs/areas/pipe-recording.md`, *Privacy*: "never sent anywhere" becomes: sent only as contract messages, when the
  user replays it with `--server`, to the server named.
- `docs/areas/contract.md` (*Scope*, *Not here*) and `docs/areas/ingest.md` (*Scope*, *Not here*, the "#34" pointer):
  a replay with `--server` sends the messages.
- `docs/areas/replay-sending.md`, *Accepted risks*: a UNC path given for `--replay` or `--token-file` is read over SMB
  with the user's credentials (the user's own path, accepted); a FIFO or device as token file blocks the start.
- `docs/areas/companion-log.md`: the four lines and that they hold no token, address or name.
- `docs/ARCHITECTURE.md`: *Data flow* (contract messages are sent by a replay with `--server`, live sending #40;
  links record 0079), *Security model* (the outbound connection, token file, TLS, no proxy; record 0080).
- `SECURITY.md`: the security-relevant list gets the replay's outbound connection and the token file.
- `README.md` (English and German): *Status* bullet; a short *Sending a recording to a server* under *Companion*
  with the command and a link to the guide; correct "Nothing is sent from the Companion to the server.", "The
  Companion has no settings, such as a server address or a token." and "No Companion sends anything yet" (and their
  German lines) for the replay, and "enter it in the Companion" (README *Server* section, both languages) to say that
  for a replay the token goes into the `--token-file` file.
- `docs/USER_GUIDE.md` (English and German): the procedure of the spec's N11 (three server placements, the server
  from source with exactly `dotnet run --project src/Censio.Server -c Release --no-launch-profile --
  --Censio:DataDirectory=<full path>` at `ws://localhost:5000/ingest`, the token file outside the source checkout
  (for example `%USERPROFILE%\censio-token.txt`), kept private and deleted when testing ends (`SECURITY.md`), the token file by `docker cp` or Notepad, the fixture's raw download
  `https://raw.githubusercontent.com/LarsLaskowski/Censio/main/tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl`,
  full paths, `--speed 10`), the status texts and errors, the warnings (`ws://` unencrypted, raw recordings only into
  one's own server, a profile per run, E13); *Ingest endpoint and token*: "No Companion sends data yet" corrected, and
  every "enter it in the Companion" (the token sections, both languages) says that for a replay the token goes into
  the `--token-file` file.
  Statements AC12 has not shown are marked "Not yet tried" until it ran.
- `tests/Censio.Pipe.Tests/Fixtures/README.md` (Tester): the fixture is also linked into `Censio.Companion.Core.Tests`
  and used by the replay sender tests.
- `.squad/project.md` (orchestrator writes; Dev reports this text for *Security areas*): replace the *Outbound calls*
  line with "- Outbound calls: only the replay's WebSocket to the server named by `--server` (`ReplaySender`,
  `ClientWebSocketConnector`, `IReplayServerConnector`), with the token from `ReplayTokenFile`, no proxy, no redirect;
  any other call to a server is a new security area. A UNC path given for `--replay` or `--token-file` is read over SMB
  as the user's own choice. A UNC installation folder from the settings file is probed and
  read over SMB (`GameInstallation`, `GameArchiveStateReader`, `GameCatalogReader.Load`, `GameIconReader.Load`) —
  [replay-sending](../docs/areas/replay-sending.md), [game-installation](../docs/areas/game-installation.md),
  *Folders from outside*, [game-catalog](../docs/areas/game-catalog.md), *Limits and errors*."; and add `ReplayTokenFile`
  after `AnonymiserCommand (output and temporary file)` in *File paths the Companion writes, prunes or opens*. The
  Tester reports the *Test doubles* lines of its four new doubles.

## Architecture check

- Record 0019 (token by default) holds: the sender presents the token; the server is unchanged.
- Record 0015 holds: no new deliverable. Record 0004: the logic is in `Censio.Companion.Core`, Windows only wires it.
- Record 0036 holds: receive times stay the recorded ones. Record 0041: the figure name never crosses.
- Guarantee "unsent ticks buffered" (record 0031) is not touched: the replay is no live Companion and buffers
  nothing; documented as such.
- The privacy statement of `pipe-recording.md` changes by the user's explicit request only (*Documentation updates*).

## Security considerations

- [x] Limits: the reply bound of 65,536 bytes is on the raw received bytes, before `ContractReader` decodes; the
  token file bound of 65,536 bytes is on the raw file, before decoding; the 32 to 1,024 characters are counted after
  decoding and trimming (ASCII only, so equal to bytes); the contract's own bounds apply inside `ContractReader`.
- [x] Exceptions reaching a user-visible reason: none. Connector, receive and send exceptions map to the kinds of
  `ReplaySendProblemKind` with fixed texts; file exceptions to the token file kinds and today's replay reasons. No
  exception text reaches the tray or the log (it can carry the address).
- The token is held as a string by the sender for the run's hello only; it is not in `ToString` of any type, not in a
  status, menu, diagnostics or log line (AC11).
- `wss://` uses the operating system's certificate validation; there is no switch to skip it.
- A FIFO or device named as token file blocks the replay start, and a UNC path for `--replay` or `--token-file` is
  read over SMB with the user's credentials; both are the user's own path, accepted like the server's configured file
  (`ingest.md`, *Accepted risks*) and stated in replay-sending.
- Every exception but the caller's cancellation is caught inside the connector (`null`) and the sender
  (`ConnectionLost`), so no exception reaches the shell's background-task log line (fact 16), which logs it whole.
- No test writes to a certificate store; the test certificate is created in memory and its key never persisted.
- A hostile server can only end the run (every unexpected frame is `InvalidReply`) or hold it until the 30 s timeouts.

## Decision records

- `docs/decisions/0079-recordings-sent-to-a-server-by-companion-replay-switches.md` (Proposed)
- `docs/decisions/0080-replay-token-read-from-a-file-named-on-the-command-line.md` (Proposed)

## Challenge

Plan challenge (2 major, 5 minor), all accepted:

- M1 (launch profile): accepted. Fact 13 corrected; the guide gives `dotnet run ... --no-launch-profile` with the data
  directory, and spec N11 names that command.
- M2 (trusted certificate on Linux CI): accepted, together with Security's blocking finding: no test-only trust seam;
  `ClientWebSocketConnectorTrustedCertificateConnects` dropped; S4's success is a manual check by the Dev in a
  disposable container (AC7 row); the untrusted-certificate test stays.
- m1 (README and guide lines): accepted, *Documentation updates*.
- m2 (close wait): accepted; it runs on the injected `TimeProvider`, tested on `FakeTimeProvider`.
- m3 (observer thread): accepted; stamp behind a lock, `NotifyState` thread-safe, the status task always observed.
- m4 (proxy test): accepted; the Tester sets `HttpClient.DefaultProxy` to a `WebProxy` at the recording proxy with
  `BypassProxyOnLocal = false`, restores it in `finally`, and confirms in a scratch worktree that the test fails on a
  default `ClientWebSocket` (plan fact 11).
- m5 (second open): accepted; routed through `CloseAsync`, test `PipeReplayRunnerSecondOpenFailureClosesConnection`.

## Security review

- Blocking (certificate trust in tests): option (b) taken: the trusted-certificate test is dropped (M2); the
  production constructor sets no chain policy or callback; "no test writes to a certificate store" is in *Security
  considerations*. Option (a) lost: a trust seam in production code would be a second way to configure validation.
- Non-blocking 1 (unlisted exceptions): fixed; every exception but the caller's cancellation stays inside the
  connector and the sender; AC11 test `ReplaySenderUnlistedExceptionGivesConnectionLostWithoutAddress`.
- Non-blocking 2 (UNC paths): fixed; *Security considerations*, replay-sending *Accepted risks*, and the
  `.squad/project.md` text.
- Non-blocking 3 (token file location): fixed; the guide names a place outside the checkout, kept private and deleted
  after testing, in both languages.

## Out of scope / follow-ups

The spec's *Out of scope*; no follow-up issue: #40, #41, #42 and #43 exist.
