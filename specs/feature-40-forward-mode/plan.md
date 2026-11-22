# Plan: Forward mode — live sending to a server with token and reconnect

Source: Issue #40 | [spec.md](spec.md)
Tier: security — the Companion gets a new outbound connection to a configured server and stores and presents a credential (`serverToken` in the settings file).
Security areas touched: Outbound calls (live connection to `serverAddress` and, in local mode, to `127.0.0.1`); Ingest token and Companion authentication (the Companion's `serverToken` in the hello); Parsing of external input (`CompanionSettings` keys `mode` `forward`, `serverAddress`, `serverToken`); Logging and showing external data (`CompanionLog` lines of live sending)

## Problem / root cause

See [spec.md](spec.md), *Problem*: the live Companion has no sender, and `forward` is an invalid `mode`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The live pipe connection feeds only tray, diagnostics and catalog; nothing sends live | `src/Censio.Companion/App.xaml.cs` `StartConnection` (lines ~281–302) | confirmed |
| 2 | `PipeConnection` raises only `StatusChanged` and `TickReceived`; trade routes and session events are not surfaced | `PipeConnection.cs` lines 105–110, read loop 318–348 (only `item.Tick` published) | confirmed |
| 3 | A repeated session end is its own result kind, so a `SessionEnd` result is always the first | `PipeTickResultKind.RepeatedSessionEnd`, `PipeTickResult.RepeatedSessionEnd` | confirmed |
| 4 | `ReplaySender` holds connect, hello, receive loop, one message in flight, status sends and close in one class (987 lines); `ReplaySenderTests` pin its behavior (75 tests and rows) and use `ReplaySender.ReplyTimeout` and `MaxReplyBytes` | `ReplaySender.cs`; `ReplaySenderTests.cs` lines 312, 764, 1009 | confirmed |
| 5 | `ReplaySender` gives each send a new identity (`ContractMessageConverter.To…` per call), so it cannot resend one message with its identity | `ReplaySender.SendTickAsync` → `ToTick` | confirmed |
| 6 | `ClientWebSocketConnector` connects without proxy and redirect and sets no keep-alive | `ClientWebSocketConnector.cs` lines 44–60 | confirmed |
| 7 | .NET's `ClientWebSocketOptions.KeepAliveTimeout` with `KeepAliveInterval` makes the client send pings and abort the connection, failing a pending receive, when no pong arrives within the timeout | to verify: .NET 9+ API, measured by `ClientWebSocketConnectorConnectWithKeepAliveAbortsSilentConnection` against a loopback endpoint that never reads (Tester, step 5; a failing measurement goes to the Lead before step 6) | unverified |
| 8 | The server answers pings only inside a receive; it keeps one pending between messages and stores a message in about 3 seconds, below the 30-second pong timeout | `docs/areas/ingest.md`, *One Companion and silent connections* | confirmed |
| 9 | `companionConnected` refusals do not count as failed hellos; `tokenRefused` and `unsupportedVersion` do (10 within 60 s block the host) | `docs/areas/ingest.md`, *Limits per host* | confirmed |
| 10 | Settings: `mode` other than `local` is refused with `mode is not local`; `IsHosted` is `Kind != Off`, so a new `Forward` kind would start a server unless changed | `CompanionSettings.cs` 174–240; `LocalServerSession.cs` 138–141 | confirmed |
| 11 | `LocalServerSetting` is a `record`: its `ToString` prints every property | `LocalServerSetting.cs` line 6 | confirmed |
| 12 | `LocalServerSession.WaitForTokenAsync` stays completed with the token after the server stopped | `LocalServerSession.cs` 160–176 (`_token` set once) | confirmed |
| 13 | `TrayMenu.Create` already has seven parameters (S107 forbids an eighth) | `TrayMenu.cs` line 282 | confirmed |
| 14 | Local mode, records 0099–0101 and the line `Local server settings invalid` are unreleased (commit 466fdcb is in no tag) | `git tag --contains 466fdcb` empty; tags up to `v0.4.0` | confirmed |
| 15 | The generated token is 43 Base64url characters; the token rule is 32–1,024 visible ASCII after trimming | `docs/areas/ingest-token.md`, *Token*; `ReplayTokenFile.cs` | confirmed |
| 16 | A UUID in the contract is lowercase hyphenated, never all zero | `docs/areas/contract.md` line 21 | confirmed |
| 17 | Ticks arrive about every two minutes | `docs/areas/pipe.md` line 217 | confirmed |
| 18 | The Product Manager built the server image from a branch on Windows for #34 | `docs/areas/replay-sending.md`, *Measurements* | confirmed |

## Tests per criterion

All in `tests/Censio.Companion.Core.Tests/`, each with `[TestCategory("ACn")]`. Timing on `RecordingTimeProvider`; the
server side is `ScriptedIngestServer`; reconnects use the new `QueuedIngestConnector`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionSettingsReadLocalServerWithModeForwardReturnsForward` (`forward`, `FORWARD`; `ws://`, `wss://`, with query; token trimmed; no token), `CompanionSettingsReadLocalServerWithInvalidServerAddressReturnsInvalid` (missing, `1`, `null`, `http://h/`, relative, user info, fragment, space — each with its problem text), `CompanionSettingsReadLocalServerWithInvalidServerTokenReturnsInvalid` (31 characters, a non-ASCII character, `1`, `null`), `CompanionSettingsReadLocalServerWithDuplicateServerKeysReturnsInvalid` (`serverAddress`/`ServerAddress`, `serverToken` twice), `CompanionSettingsReadLocalServerWithModeLocalIgnoresServerKeys`, `CompanionSettingsReadLocalServerWithModeForwardIgnoresLocalPort`, `CompanionSettingsReadWithForwardKeysKeepsGameFolder`; adapted: `CompanionSettingsReadLocalServerWithInvalidModeReturnsInvalid` (`forward` removed, problem `mode is not local or forward`), `LocalServerSessionRunWithInvalidSettingStartsNoServerAndLogsWarning` (`Server settings invalid: `), `LocalServerSessionIsHostedPerModeAndSetting` (`Forward` → false) | `CompanionSettingsTests.cs`, `LocalServerSessionTests.cs` |
| AC2 | `LiveSenderRunSendsHelloWithFixedProfileAndToken`, `LiveSenderProfileIdIsFixed` (pins the text), `LiveSenderRunConnectsBeforeAnyPipeItem`, `LiveSenderStatusIsConnectingThenConnected`, `TrayMenuFormatLiveSendFormatsEveryState`, `TrayMenuCreateWithLiveSendShowsServerLine` | `LiveSenderTests.cs`, `TrayMenuTests.cs` |
| AC3 | `LiveSenderEnqueueSendsItemsInOrderOneInFlight`, `LiveSenderEnqueueSkipsTickWithoutIslands`, `LiveSenderNotifyStateSendsStatusAfterAcceptAndOnChange`, `LiveSenderEnqueueWithWriterRefusalLogsAndSkips`, `LiveSenderAcknowledgementWithRefusalIsNotResentAndLogged`, `PipeConnectionRunRaisesItemReceivedInReadOrder` (tick, trade routes, session start and end with their read stamps; no item for a repeated session end) | `LiveSenderTests.cs`, `PipeConnectionTests.cs` |
| AC4 | `LiveSenderRunRetriesWithGrowingDelays` (1, 2, 4, 8, 16, 30, 30 s, each with its status), `LiveSenderRunResetsDelaysAfterSixtySecondsConnected`, `LiveSenderRunReconnectsAfterConnectionLost`, `LiveSenderRunRetriesWhileAnotherCompanionIsConnected`, `LiveSenderRunReconnectsAfterIdleConnectionFails` (receive fails with nothing in flight → `ConnectionLost`, retrying, then connected) | `LiveSenderTests.cs` |
| AC5 | `LiveSenderRunResendsUnacknowledgedMessageFirstWithSameId`, `LiveSenderEnqueueBeyondCountBoundDropsOldest`, `LiveSenderEnqueueBeyondByteBoundDropsOldest`, `LiveSenderRunLogsDroppedCountOnce` | `LiveSenderTests.cs` |
| AC6 | `LiveSenderRunStopsOnTokenRefused`, `LiveSenderRunStopsOnUnsupportedVersion` (no attempt after 60 s advanced) | `LiveSenderTests.cs` |
| AC7 | `ClientWebSocketConnectorConnectWithKeepAliveAbortsSilentConnection`, `ClientWebSocketConnectorConnectWithKeepAliveKeepsAnsweringConnection` (interval and timeout 1 s each, a 30-second hang guard only); the existing connector tests (proxy, redirect, `Origin`, TLS) unchanged; `LiveSenderForServerConnectsToGivenAddress` | `ClientWebSocketConnectorTests.cs`, `LiveSenderTests.cs` |
| AC8 | `LiveSenderForLocalServerConnectsToLoopbackWithHeldToken`, `LiveSenderForLocalServerStopsWhenServerNotRunning`, `LocalServerSessionWaitForLiveTokenAsyncReturnsTokenWhileRunning`, `LocalServerSessionWaitForLiveTokenAsyncReturnsNullAfterStop` (failed, port in use, stopped unexpectedly), `TrayMenuCreateInLocalModeKeepsLocalServerLine` | `LiveSenderTests.cs`, `LocalServerSessionTests.cs`, `TrayMenuTests.cs` |
| AC9 | `LiveSenderIsSendingPerModeAndSetting` (normal, record → true with forward or local; replay, invalid → false; off, invalid setting → false), `LiveSenderDisposeAsyncAbortsHeldConnection`; manual: the Product Manager on the branch build (*Approach*, *Manual test*) | `LiveSenderTests.cs` |
| AC10 | `LiveSenderLogNeverHoldsTokenAddressOrExceptionText` (every log entry of a run through each failure kind), `LiveSenderRunLogsFailureOncePerReason`, `CompanionSettingsReadLocalServerToStringHoldsNoToken` | `LiveSenderTests.cs`, `CompanionSettingsTests.cs` |
| AC11 | check: the Reviewer reads the new sections of `README.md` and `docs/USER_GUIDE.md` (English and German) against the spec; manual: the Product Manager follows them in the manual test | — |

## Approach

**Settings.** `CompanionSettings.ReadLocalServer` reads `mode` `forward` (ignoring case) into `LocalServerSettingKind.Forward`
with a `ForwardServerSetting`: `serverAddress` by `CompanionCommandLine.TryParseServerAddress` (made public, rule
unchanged), `serverToken` by `ReplayTokenFile.ReadToken` (the trim and character rule `ReadFile` applies after
decoding, extracted). Problem texts exactly as spec E1. A `mode` that is not a text: `mode is not local or forward`.
`ForwardServerSetting` is a class, so the record's `ToString` prints its type name, never the token. `IsHosted` becomes
`Kind is Local or Invalid`, so an invalid forward setting shows `Server: settings invalid` through the existing session
path and logs `Server settings invalid: <problem>` (renamed `CompanionLog.ServerSettingsInvalid`).

**Shared connection.** The per-connection logic of `ReplaySender` moves unchanged into `IngestClient` (connect, hello,
reply bound, receive loop with a receive always pending, one message in flight, statuses without waiting, close,
abort, every exception mapped to a `ReplaySendProblemKind`). It logs nothing; it raises `Failed` once, synchronously,
at its first failure. `ReplaySender` keeps its profile, token file, local token wait, counts and log lines and uses one
`IngestClient`; its public members stay (`MaxReplyBytes` and `ReplyTimeout` forward to `IngestClient`), so
`ReplaySenderTests` pass unchanged — the regression net of the move.

**Pipe items.** `PipeConnection` raises `ItemReceived` from its read loop for every `Tick`, `SessionStart`,
`SessionEnd` and `TradeRoutes` result, in read order, with the read stamp of the frame (kept from the assembler's
observer), guarded like `TickReceived`.

**Live sender.** `LiveSender.RunAsync` loops: read the token (forward: the setting's, possibly none; local:
`LocalServerSession.WaitForLiveTokenAsync`, `null` → status `Stopped`, return), open an `IngestClient` with the hello
(contract version 2, product version, token, `ProfileId`, `Live`), then:

- refused `TokenRefused` or `VersionNotSupported` → `Stopped` with the problem, `LiveSendRefused` logged, return;
- any other problem → `Retrying` with it, `LiveSendFailed` logged only when the reason differs from the last one
  logged since the last accepted hello, wait `RetryDelays[n]` on the `TimeProvider` (n grows to the last entry);
- accepted → `Connected`, `LiveSendConnected` logged, the current pipe state sent as `status`, then the send loop:
  the oldest waiting message is sent and stays first until its acknowledgement; an acknowledgement removes it
  (with a refusal: `LiveMessageRefused`); `Failed` or a failed send ends the loop with the problem. A connection
  accepted for at least `SuccessfulConnectionTime` resets n.

`Enqueue` runs on the pipe read loop and never waits: it converts the item (`ContractMessageConverter`, a new
identity; a tick without islands is skipped), writes it with `ContractWriter` (refused: `LiveMessageNotWritten`), and
appends `(id, bytes)` to the queue under a lock. While the queue then exceeds `MaxWaitingMessages` or
`MaxWaitingBytes` of written JSON, the oldest message not being sent is removed and counted; at least the newest
stays. `LiveMessagesDropped` is logged with the count at the next acknowledgement after a drop. `NotifyState` stores
the state and forwards it to an accepted client. The bound is observed through `Enqueue` and the messages the
scripted server receives; nothing touches the disk. `DisposeAsync` cancels the run and aborts the client at once.

**Keep-alive.** `ClientWebSocketConnector`'s new constructor sets `KeepAliveInterval` and `KeepAliveTimeout` on the
socket; the live sender's connector uses 15 and 30 seconds, so a silent connection fails its pending receive within
45 seconds (spec N3: 60). The replay's connector keeps the old constructor and behavior.

**Tray.** `TrayMenu.Create` takes a `TrayServerStatus?` instead of `LocalServerStatus?`: with `Local` the lines of
today; with `Live` (forward mode) one greyed entry of the new kind `ServerConnection` at the place of the local line
(below `Catalog:`), text `FormatLiveSend`: `Server: connecting`, `Server: connected`, `Server: unreachable, retrying`,
`Server: not answering, retrying`, `Server: connection lost, retrying`, `Server: invalid reply, retrying`,
`Server: another Companion is connected, retrying`, `Server: token refused`, `Server: does not support contract
version 2`, any other `Server: not connected`. No `Open Censio` in forward mode. Tooltip and icon unchanged.

**Log texts** (`CompanionLog`, no token, address, name or exception): `Server settings invalid: {Problem}` (Warning),
`Live sending connected to the server` (Information), `Live sending failed: {Reason}; retrying` (Warning),
`Live sending refused by the server: {Reason}; not retried` (Warning), `Server refused a live message: {Refusal}`
(Warning, the enum name), `Live message not sent: the contract writer refused it` (Warning), `Live messages dropped:
{Count}` (Warning). `{Reason}` is `TrayMenu.FormatReplaySendReason`.

**Wiring** (`Censio.Companion`, no test project). `App` reads the setting once and passes it to `StartLocalServer`
and `StartConnection`. With `LiveSender.IsSending` true it builds `ForServer` or `ForLocalServer(port,
session.WaitForLiveTokenAsync, …)` with `new ClientWebSocketConnector(TimeProvider.System, LiveSender.KeepAliveInterval,
LiveSender.KeepAliveTimeout)`, subscribes `ItemReceived` → `Enqueue`, `StatusChanged` → `NotifyState`, its own
`StatusChanged` → the tray updater, and runs it on its own cancellation source. `TrayIconSources.LocalServer` becomes
`Server` (`Func<TrayServerStatus?>`). `Dispose` cancels the live sender first and waits at most 250 ms for it, before
the local server stops, so `Exit` stays within 2 (5) seconds and the sender never retries a stopping server.

**Manual test** (before step 9, logged `7 Manual test`): the Product Manager builds the branch head on Windows with
*Build the server image* and *Build the Companion* of the user guide (as for #34; fallback: the Lead adds an early
draft PR for CI's artifacts) and checks with the real game: S1, S2 (pages update live), S3 (`docker restart`, network
cable, sleep and wake), S4, E6 (a wrong token), E9 (`Exit` time), S5 (local mode live), and the guide's new steps.

Early draft PR: no

## Options considered

- **Share the connection by extracting `IngestClient`** (chosen) against **a new `ReplaySender` per attempt** (lost:
  a new identity per send rules out the resend of AC5, replay log lines, no keep-alive) and **a copy in `LiveSender`**
  (lost: the quality gate's duplication condition).
- **Extend `LocalServerSetting` with a `Forward` kind** (chosen) against **a separate reader for forward keys** (lost:
  `mode` parsed twice, two answers for one key) and **renaming to `ServerSetting`** (lost: 23 call sites renamed for
  a name).
- Records 0102–0104 hold the product options (profile, token place, retry, queue, keep-alive).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `IngestClient` (new), `ReplaySender` | per-connection logic moved, replay behavior unchanged |
| Censio.Companion.Core | `LiveSender`, `LiveSendStatus`, `LiveSendState`, `PipeConnectionItem`, `ForwardServerSetting`, `TrayServerStatus` (new) | live sending, status, items, setting, tray input |
| Censio.Companion.Core | `PipeConnection`, `CompanionSettings`, `LocalServerSetting`, `LocalServerSettingKind`, `LocalServerSession`, `ReplayTokenFile`, `CompanionCommandLine`, `ClientWebSocketConnector`, `TrayMenu`, `TrayMenuEntryKind`, `CompanionLog` | as *Signatures* |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` | wiring, `Server` source |

## Signatures (for the Dev's skeleton)

New, in `Censio.Companion.Core`:

- `public sealed class IngestClient : IAsyncDisposable` with `public const int MaxReplyBytes = 65536;`,
  `public static TimeSpan ReplyTimeout { get; }`, `public IngestClient(IReplayServerConnector connector, TimeProvider timeProvider)`,
  `public event EventHandler<ReplaySendProblemKind>? Failed;`, `public ReplaySendProblemKind? Failure { get; }`,
  `public Task<ReplaySendProblemKind?> OpenAsync(Uri address, ContractHello hello, CancellationToken cancellationToken)`,
  `public Task<ContractAcknowledgement?> SendAsync(ReadOnlyMemory<byte> utf8Json, Guid id, CancellationToken cancellationToken)`,
  `public void NotifyState(PipeConnectionState state)`, `public Task<ReplaySendProblemKind?> CloseAsync(CancellationToken cancellationToken)`,
  `public ValueTask DisposeAsync()`.
- `public sealed class LiveSender : IAsyncDisposable` with `public const string ProfileName = "Live";`,
  `public const int MaxWaitingMessages = 64;`, `public const int MaxWaitingBytes = 16777216;`,
  `public static Guid ProfileId { get; }` (value b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94),
  `public static IReadOnlyList<TimeSpan> RetryDelays { get; }`, `public static TimeSpan SuccessfulConnectionTime { get; }`,
  `public static TimeSpan KeepAliveInterval { get; }`, `public static TimeSpan KeepAliveTimeout { get; }`,
  `public event EventHandler<LiveSendStatus>? StatusChanged;`, `public LiveSendStatus Status { get; }`,
  `public static bool IsSending(CompanionMode mode, LocalServerSetting setting)`,
  `public static LiveSender ForServer(ForwardServerSetting setting, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`,
  `public static LiveSender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`,
  `public void Enqueue(PipeConnectionItem item)`, `public void NotifyState(PipeConnectionState state)`,
  `public Task RunAsync(CancellationToken cancellationToken)`, `public ValueTask DisposeAsync()`; private constructor
  (address, token reader, whether local, version, connector, time provider, logger: seven).
- `public readonly record struct LiveSendStatus` with `public required LiveSendState State { get; init; }`,
  `public ReplaySendProblemKind? Problem { get; init; }`.
- `public enum LiveSendState { Connecting, Connected, Retrying, Stopped }`.
- `public sealed record PipeConnectionItem` with `public required PipeTickResult Result { get; init; }`,
  `public required DateTimeOffset ReadStamp { get; init; }`.
- `public sealed class ForwardServerSetting` with `public ForwardServerSetting(Uri address, string? token)`,
  `public Uri Address { get; }`, `public string? Token { get; }`.
- `public readonly record struct TrayServerStatus` with `public LocalServerStatus? Local { get; init; }`,
  `public LiveSendStatus? Live { get; init; }`.

Changed:

- `PipeConnection`: `public event EventHandler<PipeConnectionItem>? ItemReceived;`
- `LocalServerSettingKind`: `Forward` appended; `LocalServerSetting`: `public ForwardServerSetting? Forward { get; init; }`.
- `CompanionSettings`: `public const string ServerAddressKey = "serverAddress";`, `public const string ServerTokenKey = "serverToken";`
- `CompanionCommandLine`: `public static bool TryParseServerAddress(string argument, [NotNullWhen(true)] out Uri? address)` (was private).
- `ReplayTokenFile`: `public static string? ReadToken(string text)`.
- `LocalServerSession`: `public Task<string?> WaitForLiveTokenAsync(CancellationToken cancellationToken)`; `IsHosted` body only.
- `ClientWebSocketConnector`: `public ClientWebSocketConnector(TimeProvider timeProvider, TimeSpan keepAliveInterval, TimeSpan keepAliveTimeout)`.
- `TrayMenu`: `public static string FormatLiveSend(LiveSendStatus status)`;
  `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, GameInstallationStatus game, GameCatalogStatus catalog, TrayServerStatus? server, bool isAutostartEnabled, TimeProvider clock)` replaces the overload with `LocalServerStatus? localServer`.
- `TrayMenuEntryKind`: `ServerConnection` appended.
- `CompanionLog`: `LocalServerSettingsInvalid` is removed; new `public static partial void ServerSettingsInvalid(ILogger logger, string problem);`,
  `public static partial void LiveSendConnected(ILogger logger);`, `public static partial void LiveSendFailed(ILogger logger, string reason);`,
  `public static partial void LiveSendRefused(ILogger logger, string reason);`,
  `public static partial void LiveMessageRefused(ILogger logger, ContractAcknowledgementRefusal refusal);`,
  `public static partial void LiveMessageNotWritten(ILogger logger);`, `public static partial void LiveMessagesDropped(ILogger logger, int count);`

The skeleton holds for real: the `IngestClient` move (behavior-preserving, `ReplaySenderTests` green), the renamed log
method, the `Create` signature with its adapted call sites and `TrayIconSources.Server`.

## Test files

New: `tests/Censio.Companion.Core.Tests/LiveSenderTests.cs`; test double `QueuedIngestConnector.cs` (an
`IReplayServerConnector` handing out a scripted sequence of sockets or `null`, recording each address). Changed:
`CompanionSettingsTests.cs`, `LocalServerSessionTests.cs`, `TrayMenuTests.cs`, `PipeConnectionTests.cs`,
`ClientWebSocketConnectorTests.cs`, `LoopbackWebSocketHost.cs` (a path `/silent` that accepts and never reads).

Existing call sites: the 18 `TrayMenu.Create(…)` calls in `TrayMenuTests.cs` — the Dev, step 4 (old signature goes),
mechanically (`localServer` → `new TrayServerStatus { Local = … }` or `null`). `CompanionLog.LocalServerSettingsInvalid`
has no test call site. The assertions changed by AC1 (`forward` row, the warning text, `IsHosted`) — the Tester.

Files no unit test can cover: `src/Censio.Companion/App.xaml.cs`, `TrayIcon.cs`, `TrayIconSources.cs` — the Windows
shell, excluded by construction (record 0004, `sonar.coverage.exclusions`); no new exclusion.

## Areas

- New area `live-sending` — `docs/areas/live-sending.md` (+1,400 words). Index scope: "Live sending from the Companion
  to a server: forward mode's keys `serverAddress` and `serverToken`, the fixed profile `Live`, start and hello, what is
  sent, the waiting messages and their bounds, retry delays and final refusals, the dead-connection bound, the
  `Server:` texts of forward mode, local mode's live sending, log lines, privacy, accepted risks"; not here: "the
  connection rules shared with the replay (see replay-sending), the messages (see contract), the endpoint (see
  ingest), the persistent buffer (#41), the catalog (#42)".
- `docs/areas/companion.md` (3,660 words, +150): settings keys, the `Server:` entry in forward mode, *Not here*.
- `docs/areas/local-mode.md` (1,609, +70): `forward` valid, renamed warning and problems, live data now sent.
- `docs/areas/companion-log.md` (1,380, +120): the new lines, the renamed one.
- `docs/areas/replay-sending.md` (2,975, +10), `docs/areas/contract.md` (3,446, +5), `docs/areas/ingest.md` (3,947, +5):
  the "#40" pointers become links to live-sending.

## Documentation updates

All by the Dev after the code is green, against it.

- `docs/areas/live-sending.md` (new, from `_template.md`) and its index row in `docs/areas/README.md`; the *Not here*
  cells of the `replay-sending` and `contract` rows point to live-sending.
- The area documents of *Areas*; in `local-mode.md` lines 22 and 69 and *Log*; `companion-log.md` line 55.
- `docs/ARCHITECTURE.md`: lines 61 and 65 ("live sending is #40", "live data into it is #40") describe live sending
  and link records 0102–0104 and `areas/live-sending.md`.
- `README.md` (English and German): *Status* lines 18 and 23 (236, 241), the local-mode steps 156 (374), the settings
  lines 158 (376) and the *Configuration* table (`mode` `forward`, `serverAddress`, `serverToken`), a short *Forward
  mode* part under *Companion*, the menu list with the `Server:` line.
- `docs/USER_GUIDE.md` (English and German): a section *Forward mode* (*Weiterleitungsmodus*) after *Local mode*:
  settings example, where the token comes from (`docker exec censio cat /config/ingest-token`), `ws://<host>:8080/ingest`,
  the `Server:` texts, profile `Live` and its warning (one playthrough per server or delete `Live` between them, until
  #43), what is lost (an outage beyond the bound, `Exit`), not sharing `settings.json`; lines 132, 336, 389 (and their
  German twins) lose "#40"; *Data and logs* (519, 1049): the new log lines and keys.
- `SECURITY.md`: *Deployment* (line 36): live sending puts the token in `settings.json` in the private folder (do not
  share the file), `wss://` where the path is not trusted; *Scope* line 48 adds the live connection.
- `.squad/project.md` — the Dev reports the exact text, the orchestrator writes it:
  - *Outbound calls* becomes: "Outbound calls: the replay's WebSocket to `--server` (`ReplaySender`, `IngestClient`,
    `ClientWebSocketConnector`, token from `ReplayTokenFile`), live sending to the settings file's `serverAddress` with
    its `serverToken` (`LiveSender.ForServer`, `ForwardServerSetting`) and, in local mode, both to `127.0.0.1`
    (`ReplaySender.ForLocalServer`, `LiveSender.ForLocalServer`); any other call to a server is a new security area."
    followed by the unchanged UNC sentence and links, plus `[live-sending](../docs/areas/live-sending.md)`.
  - *Parsing of external input*: `ForwardServerSetting` after `CompanionSettings`, link live-sending.
  - *Integration surface*: "`gameFolder`, `mode`, `localPort`, `serverAddress` and `serverToken` in `CompanionSettings`",
    `docs/areas/live-sending.md` beside `companion.md`; contract messages: `LiveSender` and `IngestClient` beside
    `ReplaySender`, `live-sending.md` in the documents; tray: `TrayServerStatus`.
  - *Test doubles* (the Tester's text): `QueuedIngestConnector` joins the replay sender's doubles line, "of the replay
    and live senders' tests".

## Architecture check

Record 0019 (WebSocket with shared token): kept — the token is sent in the hello only. Record 0018: only contract
messages leave. Record 0006: `ws://` stays plain; `wss://` uses system trust. Guarantee "unsent ticks buffered within
a bound, delivered in order, gaps never zeros" (record 0031): held in memory within the bound until #41. 0020/0041 are
amended by 0102 for the time before #43 (Product Manager). No new listener.

## Security considerations

- [x] Limits: settings file 1 MiB on raw bytes (unchanged); `serverToken` 32–1,024 characters after JSON unescaping
  and trimming; `serverAddress` checked after unescaping by the `--server` rule; replies 65,536 bytes raw, before
  decoding (unchanged); the waiting bound counts written UTF-8 bytes.
- [x] Exceptions: every connect, send and receive exception becomes a `ReplaySendProblemKind`; the menu shows only
  the fixed texts of *Tray* and the log only `{Reason}` texts and enum names. No exception text reaches either.
- The token lives in `ForwardServerSetting` (no `ToString` override) and the hello; `AC10` tests the log and
  `ToString`.

## Decision records

- `docs/decisions/0102-live-data-goes-to-one-fixed-profile-live-until-profiles-are-chosen.md` (Proposed)
- `docs/decisions/0103-forward-server-address-and-token-are-keys-of-the-settings-file.md` (Proposed)
- `docs/decisions/0104-live-sending-reconnects-holds-messages-in-memory-and-pings.md` (Proposed)
- `0099` (unreleased) edited in place: `forward` added by 0103. All three new records carry `Area: companion` until the
  Dev adds the `live-sending` area; then the Lead sets `Area: live-sending` (tasks.md, group PM).

## Out of scope / follow-ups

As spec *Out of scope*: the persistent buffer (#41), the catalog (#42), profile choice and the settings window (#43),
a replay to the forward server without `--server`, the server state in tooltip and icon.
