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
| 7 | .NET's `ClientWebSocketOptions.KeepAliveTimeout` with `KeepAliveInterval` makes the client send pings and abort the connection, failing a pending receive, when no pong arrives within the timeout | to verify: `ClientWebSocketConnectorConnectWithKeepAliveAbortsSilentConnection` against a loopback endpoint that never reads (Tester, step 5; a failing measurement goes to the Lead before step 6); prior evidence: the server's same mechanism, measured worst case 56.24 s for 15 + 35 s (`ingest.md`, *One Companion and silent connections*) | unverified |
| 8 | The server answers pings only inside a receive; it keeps one pending between messages and stores a message in about 3 seconds, below the 30-second pong timeout | `docs/areas/ingest.md`, *One Companion and silent connections* | confirmed |
| 9 | `companionConnected` refusals do not count as failed hellos; `tokenRefused` and `unsupportedVersion` do | `docs/areas/ingest.md`, *Limits per host*; record 0104 | confirmed |
| 10 | Settings: `mode` other than `local` is refused with `mode is not local`; `IsHosted` is `Kind != Off`, so a new `Forward` kind would start a server unless changed | `CompanionSettings.cs` 174–240; `LocalServerSession.cs` 138–141 | confirmed |
| 11 | `LocalServerSetting` is a `record`: its `ToString` prints every property | `LocalServerSetting.cs` line 6 | confirmed |
| 12 | `LocalServerSession.WaitForTokenAsync` stays completed with the token after the server stopped | `LocalServerSession.cs` 160–176 (`_token` set once) | confirmed |
| 13 | `TrayMenu.Create` already has seven parameters (S107 forbids an eighth) | `TrayMenu.cs` line 282 | confirmed |
| 14 | Local mode, records 0099–0101 and the line `Local server settings invalid` are unreleased (commit 466fdcb is in no tag) | `git tag --contains 466fdcb` empty; tags up to `v0.4.0` | confirmed |
| 15 | The token rule is 32–1,024 visible ASCII after trimming; the generated token is 43 Base64url characters (record 0103) | `docs/areas/ingest-token.md`, *Token*; `ReplayTokenFile.cs` | confirmed |
| 16 | `App.StartConnection` subscribes every event before `Run(connection.RunAsync)` | `App.xaml.cs` lines 293–299 | confirmed |

## Tests per criterion

All in `tests/Censio.Companion.Core.Tests/`, each with `[TestCategory("ACn")]`. Timing on `RecordingTimeProvider`; the
server side is `ScriptedIngestServer`; reconnects use the new `QueuedIngestConnector`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionSettingsReadLocalServerWithModeForwardReturnsForward` (`forward`, `FORWARD`; `ws://`, `wss://`, with query; token trimmed; no token), `CompanionSettingsReadLocalServerWithInvalidServerAddressReturnsInvalid` (missing, `1`, `null`, `http://h/`, relative, user info, fragment, space — each with its problem text), `CompanionSettingsReadLocalServerWithInvalidServerTokenReturnsInvalid` (31 characters, a non-ASCII character, `1`, `null`), `CompanionSettingsReadLocalServerWithDuplicateServerKeysReturnsInvalid` (`serverAddress`/`ServerAddress`, `serverToken` twice), `CompanionSettingsReadLocalServerWithModeLocalIgnoresServerKeys`, `CompanionSettingsReadLocalServerWithModeForwardIgnoresLocalPort`, `CompanionSettingsReadWithForwardKeysKeepsGameFolder`; adapted: `CompanionSettingsReadLocalServerWithInvalidModeReturnsInvalid` (`forward` removed, problem `mode is not local or forward`), `LocalServerSessionRunWithInvalidSettingStartsNoServerAndLogsWarning` (`Server settings invalid: `), `LocalServerSessionIsHostedPerModeAndSetting` (`Forward` → false; the invalid-command-line row kept); new `TrayMenuCreateWithInvalidSettingKeepsGreyedOpenCensio` | `CompanionSettingsTests.cs`, `LocalServerSessionTests.cs`, `TrayMenuTests.cs` |
| AC2 | `LiveSenderRunSendsHelloWithFixedProfileAndToken`, `LiveSenderProfileIdIsFixed` (pins the text), `LiveSenderRunConnectsBeforeAnyPipeItem`, `LiveSenderStatusIsConnectingThenConnected`, `TrayMenuFormatLiveSendFormatsEveryState`, `TrayMenuCreateWithLiveSendShowsServerLine` | `LiveSenderTests.cs`, `TrayMenuTests.cs` |
| AC3 | `LiveSenderEnqueueSendsItemsInOrderOneInFlight`, `LiveSenderEnqueueSkipsTickWithoutIslands`, `LiveSenderNotifyStateSendsStatusAfterAcceptAndOnChange`, `LiveSenderEnqueueWithWriterRefusalLogsAndSkips`, `LiveSenderAcknowledgementWithRefusalIsNotResentAndLogged`, `PipeConnectionRunRaisesItemReceivedInReadOrder` (tick, trade routes, session start and end with their read stamps; no item for a repeated session end) | `LiveSenderTests.cs`, `PipeConnectionTests.cs` |
| AC4 | `LiveSenderRunRetriesWithGrowingDelays` (1, 2, 4, 8, 16, 30, 30 s, each with its status), `LiveSenderRunResetsDelaysAfterSixtySecondsConnected`, `LiveSenderRunReconnectsAfterConnectionLost`, `LiveSenderRunRetriesWhileAnotherCompanionIsConnected`, `LiveSenderRunReconnectsAfterIdleConnectionFails` (receive fails with nothing in flight → `ConnectionLost`, retrying, then connected) | `LiveSenderTests.cs` |
| AC5 | `LiveSenderRunResendsUnacknowledgedMessageFirstWithSameId`, `LiveSenderEnqueueBeyondCountBoundDropsOldest`, `LiveSenderEnqueueBeyondByteBoundDropsOldest`, `LiveSenderRunLogsDroppedCountOnce` (at the next acknowledgement), `LiveSenderRunLogsUnloggedDroppedCountWhenStoppedOrDisposed` (token refused; `DisposeAsync`) | `LiveSenderTests.cs` |
| AC6 | `LiveSenderRunStopsOnTokenRefused`, `LiveSenderRunStopsOnUnsupportedVersion` (no attempt after 60 s advanced) | `LiveSenderTests.cs` |
| AC7 | `ClientWebSocketConnectorConnectWithKeepAliveAbortsSilentConnection` (interval and timeout 1 s), `ClientWebSocketConnectorConnectWithKeepAliveKeepsAnsweringConnection` (interval 1 s, timeout 5 s, observed over three intervals): real clock (`ClientWebSocket` takes no `TimeProvider`, the exception noted in the test's documentation), only the outcome asserted (aborted, still open), a 30-second hang guard; adapted to data rows over both constructors (keep-alive 15 s / 30 s): `ClientWebSocketConnectorRedirectGivesNoSocket`, `…UntrustedCertificateGivesNoSocket`, `…SendsNoOrigin`, `…BypassesSystemProxy`; `LiveSenderForServerConnectsToGivenAddress` | `ClientWebSocketConnectorTests.cs`, `LiveSenderTests.cs` |
| AC8 | `LiveSenderForLocalServerConnectsToLoopbackWithHeldToken`, `LiveSenderForLocalServerStopsWhenServerNotRunning`, `LocalServerSessionWaitForLiveTokenAsyncReturnsTokenWhileRunning`, `LocalServerSessionWaitForLiveTokenAsyncReturnsNullAfterStop` (failed, port in use, stopped unexpectedly), `TrayMenuCreateInLocalModeKeepsLocalServerLine` | `LiveSenderTests.cs`, `LocalServerSessionTests.cs`, `TrayMenuTests.cs` |
| AC9 | `LiveSenderIsSendingPerModeAndSetting` (normal, record → true with forward or local; replay, invalid → false; off, invalid setting → false), `LiveSenderDisposeAsyncAbortsHeldConnection`; manual: the Product Manager on the branch build (*Approach*, *Manual test*) | `LiveSenderTests.cs` |
| AC10 | `LiveSenderLogNeverHoldsTokenAddressOrExceptionText` (every log entry of a run through each failure kind), `LiveSenderRunLogsFailureOncePerReason`, `LiveSenderRunWithThrowingTokenReaderLogsNoExceptionAndRetries` (an `InvalidOperationException` with a host name in its text, then a token: `Retrying` with `ConnectionLost`, then `Connected`), `CompanionSettingsReadLocalServerToStringHoldsNoToken` | `LiveSenderTests.cs`, `CompanionSettingsTests.cs` |
| AC11 | check: the Reviewer reads the new sections of `README.md` and `docs/USER_GUIDE.md` (English and German) against the spec; manual: the Product Manager follows them in the manual test | — |

## Approach

**Settings.** `CompanionSettings.ReadLocalServer` reads `mode` `forward` (ignoring case) into `LocalServerSettingKind.Forward`
with a `ForwardServerSetting`: `serverAddress` by `CompanionCommandLine.TryParseServerAddress` (made public, rule
unchanged), `serverToken` by `ReplayTokenFile.ReadToken` (the trim and character rule `ReadFile` applies after
decoding, extracted). Problem texts exactly as spec E1. A `mode` that is not a text: `mode is not local or forward`.
`ForwardServerSetting` is a class, so the record's `ToString` prints its type name, never the token. `IsHosted` becomes
`setting.Kind is Local or Invalid && mode != CompanionMode.Invalid` (the command-line check kept), so an invalid
forward setting shows `Server: settings invalid` through the existing session path and logs `Server settings invalid:
<problem>` (renamed `CompanionLog.ServerSettingsInvalid`). It also keeps, as today (S6), the greyed `Open Censio` and,
with `--replay`, the replay's "local server not running".

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

`RunAsync` lets no exception but the caller's cancellation escape (`App.Run` would log its text through
`BackgroundTaskFailed`): an unexpected one in an attempt (token wait, queue, status) is the problem `ConnectionLost`
and takes the retry path above, its text never logged.

`Enqueue` runs on the pipe read loop and never waits: it converts the item (`ContractMessageConverter`, a new
identity; a tick without islands is skipped), writes it with `ContractWriter` (refused: `LiveMessageNotWritten`), and
appends `(id, bytes)` to the queue under a lock. While the queue then exceeds `MaxWaitingMessages` or
`MaxWaitingBytes` of written JSON, the oldest message not being sent is removed and counted; at least the newest
stays. `LiveMessagesDropped` is logged with the count at the next acknowledgement after a drop, or at `Stopped` or
`DisposeAsync` while a count is still unlogged (at most one line per connection, N6). `NotifyState` stores
the state and forwards it to an accepted client. The bound is observed through `Enqueue` and the messages the
scripted server receives; nothing touches the disk. `DisposeAsync` cancels the run and aborts the client at once.

**Keep-alive.** `ClientWebSocketConnector`'s new constructor sets `KeepAliveInterval` and `KeepAliveTimeout` on the
socket; the live sender's connector uses 15 and 30 seconds, so a silent connection fails its pending receive within
60 seconds (spec N3; 45 s plus the about 6 s the server's same mechanism measured over its own sum, fact 7) — the
documents and record 0104 promise 60, not 45. The replay's connector keeps the old constructor and behavior.

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
LiveSender.KeepAliveTimeout)`, calls `NotifyState(connection.Status.State)` (no event fires while the game stays
closed), then subscribes `ItemReceived` → `Enqueue`, `StatusChanged` → `NotifyState`, its own
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
  `public LiveSendStatus? Live { get; init; }` (no `required` member: one of them is null).

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
has no test call site. The assertions changed by AC1 (`forward` row, the warning text, `IsHosted`) and the four
connector tests made data rows (AC7) — the Tester.

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
  cells of the `replay-sending` and `contract` rows point to live-sending. It states: a dead idle connection found
  within 60 seconds; the dropped count logged after the reconnect, or at a final refusal or `Exit`; an invalid forward
  setting's greyed `Open Censio` and `--replay` outcome (also in `local-mode.md`); and under *Accepted risks*, verbatim:
  "Over `ws://` the Companion sends the ingest token unencrypted at every start and every retry, unattended, to whatever
  host answers `serverAddress`; a name without a domain (such as `nas`) may be answered by any device of the network
  the PC is on, so use `wss://`, or an address on a network you trust, when the PC leaves your home network."
- The area documents of *Areas*; in `local-mode.md` lines 22 and 69 and *Log*; `companion-log.md` line 55.
- `docs/ARCHITECTURE.md`: lines 61 and 65 ("live sending is #40", "live data into it is #40") describe live sending
  and link records 0102–0104 and `areas/live-sending.md`.
- `README.md` (English and German): *Status* lines 18 and 23 (236, 241), the local-mode steps 156 (374), the settings
  lines 158 (376) and the *Configuration* table (`mode` `forward`, `serverAddress`, `serverToken`), a short *Forward
  mode* part under *Companion*, the menu list with the `Server:` line.
- `docs/USER_GUIDE.md` (English and German): a section *Forward mode* (*Weiterleitungsmodus*) after *Local mode*:
  settings example, where the token comes from (`docker exec censio cat /config/ingest-token` for the generated token;
  with `Censio:Ingest:TokenFile` that file's token, `"` and `\` JSON-escaped; no `serverToken` when `RequireToken` is
  `false`), `ws://<host>:8080/ingest`,
  the `Server:` texts, profile `Live` and its warning (one playthrough per server or delete `Live` between them, until
  #43), what is lost (an outage beyond the bound, `Exit`), not sharing `settings.json`; lines 132, 336, 389 (and their
  German twins) lose "#40"; *Data and logs* (519, 1049): the new log lines and keys.
- `SECURITY.md`: *Deployment* (line 36): live sending puts the token in `settings.json` in the private folder (do not
  share the file), and after the replay sentence the *Accepted risks* sentence above, verbatim; *Scope* line 48 adds
  the live connection.
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
- [x] Exceptions: every connect, send and receive exception becomes a `ReplaySendProblemKind`, and `LiveSender.RunAsync`
  lets none but cancellation escape (*Live sender*); the menu shows only
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

## Challenge

0 major, 8 minor objections; each answered, the report kept verbatim below.

1. Invalid forward setting on the local-mode path — accepted, kept as today (S6): *Approach*, *Settings* states the
   greyed `Open Censio` and the `--replay` outcome, the Tester adds `TrayMenuCreateWithInvalidSettingKeepsGreyedOpenCensio`
   (AC1), the Dev states both in `live-sending.md` and `local-mode.md` (*Documentation updates*). Dropping the entry
   would need a forward-specific path for an invalid setting whose `mode` may not even be readable.
2. `IsHosted` keeps its command-line check — accepted: *Approach* gives the full expression; the invalid-command-line
   row stays (AC1).
3. 45 s too tight — accepted: *Approach*, *Keep-alive* and the documents promise 60 s; fact 7 cites the server's
   measurement; record 0104 option 5 now says 60 seconds.
4. Keep-alive tests on the real clock — accepted: AC7 row (exception documented, outcome only, interval 1 s and timeout
   5 s over three intervals for the answering case).
5. `tasks.md` row 1 against *Signatures* — accepted: row 1 corrected, *Signatures* says `TrayServerStatus` has no
   `required` member.
6. Starting pipe state — accepted: *Wiring* calls `NotifyState(connection.Status.State)` before subscribing (fact 16:
   before `RunAsync` starts, so no change is missed).
7. Token source in the user guide — accepted: *Documentation updates* (`TokenFile`, JSON escape of `"` and `\`,
   `RequireToken` `false`); record 0103 narrows its sentence to the generated token.
8. Dropped count never logged — accepted with a third option: logged at the next acknowledgement, or at `Stopped` or
   `DisposeAsync` while unlogged (*Approach*, AC5 test). Logging at the first drop would log a count of one and lose
   the rest; the chosen form keeps N6's one line per connection.

### Devil's Advocate report (verbatim)

#### Plan challenge: feature 40, forward mode (`/home/user/Censio/specs/feature-40-forward-mode/plan.md`)

The plan holds up. I checked its verified facts against the code and found them correct:
- Fact 2: `PipeConnection.cs` publishes only `item.Tick` (lines 346–349).
- Fact 10: `CompanionSettings.cs` 191–209 and `LocalServerSession.cs` 140.
- Fact 12: `_token` is set once, at line 335.
- Fact 13: `TrayMenu.cs` 282.
- Fact 14: `git tag --contains 466fdcb` returns nothing.
- Fact 4: there are 18 `TrayMenu.Create` calls in the tests, and `ReplaySenderTests` counts reply timers by `ReplyTimeout` (line 1551), so the `IngestClient` move really is pinned.

Moving the connection logic into `IngestClient`, rather than starting a new `ReplaySender` per attempt, is justified: `SendTickAsync` creates a new identity on every call (`ReplaySender.cs` 326). The server's handling of resends (`docs/areas/ingest.md` 114–115) supports resending with the same identity. Not changing anything is not an option, because nothing sends live data today (`App.xaml.cs` 276–301). I found no major objection.

##### Minor objections (each written as an instruction)

1. **Minor — an invalid forward setting goes down the local-mode path.** The plan says "No `Open Censio` in forward mode", but an invalid setting creates a `LocalServerSession`.
   - Evidence: `TrayMenu.cs` 299–302 adds a greyed `Open Censio` for every `LocalServerStatus`.
   - Evidence: `ReplaySenderFactory.Create` turns a non-null `_localServer` into `ReplaySender.ForLocalServer`. So `--replay` with an invalid forward setting ends with "local server not running" instead of staying offline. That is today's behavior for `"mode": "forward"`, so S6 "as today" still holds.
   - Instruction: the Dev states both outcomes in `live-sending.md` and `local-mode.md`. The Tester adds a `TrayMenu` row showing that an invalid setting keeps the greyed `Open Censio`. Alternatively, the Lead decides to drop that entry for a forward-mode invalid setting.
   - Why minor: a detail of what the user sees, not a wrong build.
2. **Minor — `IsHosted` must keep its command-line check.** Today it is `setting.Kind != Off && mode != CompanionMode.Invalid` (`LocalServerSession.cs` 140). The plan's "`Kind is Local or Invalid`" drops the mode check if taken literally.
   - Instruction: write `setting.Kind is Local or Invalid && mode != CompanionMode.Invalid`. Keep the invalid-command-line row of `LocalServerSessionIsHostedPerModeAndSetting`.
   - Why minor: the existing test catches the mistake.
3. **Minor — the "within 45 seconds" claim is too tight.**
   - Evidence: the server uses the same .NET keep-alive (`IngestEndpoint.cs` 35–55, interval 15 s, timeout 35 s, 50 s in total), and its measured worst case was 56.24 s (`ingest.md` 59–62), about 6 s over interval plus timeout.
   - Instruction: `live-sending.md` and record 0104 promise "within 60 seconds" (spec N3), not 45. Fact 7 cites the server's measurement as prior evidence that the mechanism works.
   - Why minor: 15 + 30 plus about 6 s still meets N3.
4. **Minor — the keep-alive connector tests run on the real clock.** `ClientWebSocket` takes no `TimeProvider`, and `docs/UNIT_TESTS.md` 81–82 says never to use the real clock.
   - Instruction: the Tester notes the exception in the test's documentation and asserts only the outcome (aborted or still open), never a duration.
   - Instruction: in `…KeepsAnsweringConnection`, use a timeout several times the interval (for example interval 1 s, timeout 5 s, observed over three intervals). A 1 s pong timeout under coverage on CI is likely to fail at random.
   - Why minor: concerns how the test is written, not the plan.
5. **Minor — `tasks.md` row 1 and the plan's *Signatures* disagree.** Row 1 asks for "the `required` properties of … `TrayServerStatus`", but *Signatures* gives `Local` and `Live` as plain `{ get; init; }`, and both must be able to be null.
   - Instruction: the Dev follows *Signatures* (`TrayServerStatus` has no required members).
   - Why minor: a wording conflict.
6. **Minor — set the live sender's starting pipe state from the connection.** `PipeConnection` starts as `WaitingForGame` (line 43) and raises `StatusChanged` only on a change (467–476). With the game closed, no event ever fires.
   - Instruction: in `App`, call `NotifyState(connection.Status.State)` before subscribing, instead of relying on the enum default happening to be right.
   - Why minor: the result is correct today by coincidence.
7. **Minor — the user guide's token source covers only the generated token.** `docker exec censio cat /config/ingest-token` holds only while `Censio:Ingest:TokenFile` is unset (README 188, 193). A token from that file may contain `"` or `\` (any visible ASCII), so record 0103's "the token needs no JSON escape" is true only for the generated token.
   - Instruction: the forward-mode section of `USER_GUIDE.md` names the `TokenFile` case and says to JSON-escape `"` and `\`. It also says to omit `serverToken` when `RequireToken` is `false`. Record 0103 narrows its sentence to the generated token.
   - Why minor: a documentation detail.
8. **Minor — the dropped-messages count may never be logged.** `LiveMessagesDropped` is written only "at the next acknowledgement after a drop", so an `Exit` during an outage, or the `Stopped` state after a token refusal, loses the count that spec E5 and AC5 say is logged.
   - Instruction: `live-sending.md` states that the count is logged after the reconnect and is lost at `Exit` or after a final refusal. The alternative is to log it once at the first drop since the last log line, which keeps N6's bound.
   - Why minor: the line is a diagnostic, not data.

## Security review

Verdict APPROVED, 3 non-blocking findings, each fixed in this revision; the report kept verbatim below.

1. Exception text through `BackgroundTaskFailed` — fixed: *Approach*, *Live sender* (`RunAsync` lets only the caller's
   cancellation escape) and AC10's `LiveSenderRunWithThrowingTokenReaderLogsNoExceptionAndRetries`. Choice left open:
   the unexpected exception takes the retry path as `ConnectionLost` instead of ending in `Stopped`, because
   `LiveSendFailed` says "retrying" and a transient fault (a token wait that threw once) should not end live sending
   until a restart; N6 still bounds the lines and the 30-second delay the attempts.
2. Connector security rules untested on the keep-alive constructor — fixed: AC7 makes the redirect, certificate,
   `Origin` and proxy tests data rows over both constructors (Tester, *Test files*).
3. Cleartext exposure unstated — fixed: the sentence verbatim in `live-sending.md` *Accepted risks* and in
   `SECURITY.md` *Deployment* after the replay sentence (*Documentation updates*).

### Security report (verbatim)

#### Security review: feature #40, forward mode (mode `plan`)

Inputs: `/home/user/Censio/specs/feature-40-forward-mode/spec.md`, `plan.md`, and records `docs/decisions/0102`–`0104`. I read them against `.squad/project.md`, `SECURITY.md`, `docs/areas/replay-sending.md` and the current code: `CompanionSettings.cs`, `ReplayTokenFile.cs`, `CompanionCommandLine.TryParseServerAddress`, `ClientWebSocketConnector.cs`, `LocalServerSession.cs`, `ReplaySender.cs` (its exception mapping), `PipeConnection.cs` (how it guards subscribers), `TrayMenu.FormatReplaySendReason`, `App.xaml.cs` (`Run`, `BackgroundTaskFailed`) and `ClientWebSocketConnectorTests.cs`.

The plan is good to proceed. There are no blocking findings, and three non-blocking ones need a small fix now.

**What I checked and found correct:**
- **Tier and areas:** the tier `security` is right. The four areas listed under *Security areas touched* are complete. The other changed types (`ReplayTokenFile`, `CompanionCommandLine`, `ClientWebSocketConnector`, `PipeConnection`) fall under outbound calls and parsing, or open nothing new. There is no new listener.
- **Settings parsing, bypass classes enumerated:**
  - Key names are matched after JSON unescaping, so an escaped key such as `"serverToken"` is found and counted as a duplicate.
  - Case variants count as duplicates because `FindProperties` uses `OrdinalIgnoreCase`, which ignores culture. A Kelvin-sign or Turkish-i key matches nothing, so no token is sent and the failure stays closed.
  - Comments, the BOM and trailing commas are handled by the existing `ParseDocument`. A UTF-16 file or malformed JSON makes the setting `Off`, so nothing is sent.
  - The token rule (32–1,024 characters from `!` to `~` after trimming) runs on the unescaped value, so `\u0000`, non-ASCII text and surrogates are refused.
  - The address goes through the `--server` rule. Whitespace and control characters (after unescaping), `#` and user info are refused, and only `ws://` or `wss://` is allowed.
  - `serverToken` is read only with `mode` `forward`. No problem text quotes a value.
- **Token handling:**
  - The token is sent only in the hello.
  - `ForwardServerSetting` is a class, so the `LocalServerSetting` record prints only its type name, and the plan has a test for that.
  - `ContractHello` is a class, not a record.
  - `tokenRefused` and `unsupportedVersion` are final, so the server's block after 10 failed hellos is never reached. `companionConnected` does not count toward that block.
  - An untrusted `wss://` certificate fails before the upgrade, so no token is sent.
  - In local mode, `WaitForLiveTokenAsync` returns null once the server has stopped, and `Dispose` cancels the sender before the server stops.
- **Logging:**
  - `{Reason}` comes from `FormatReplaySendReason`, which returns fixed texts only.
  - Refusals are parsed by the strict `ContractEnumJsonConverter` and logged by enum name.
  - The moved `IngestClient` code maps every exception (catch-all) to a `ReplaySendProblemKind`.
  - `PipeConnection` already swallows exceptions thrown by subscribers, so `Enqueue` cannot leak an exception text through it.
- **Memory bound:** the queue keeps only `(id, bytes)`. It is capped at 64 messages and 16 MiB, plus the message in flight and at least the newest. The tests `LiveSenderEnqueueBeyondCountBoundDropsOldest` and `…ByteBoundDropsOldest` pin both caps. Nothing is written to disk.
- **Fact 7 (the keep-alive timeout):** dead-connection detection is a reliability property, not a security one. The Tester's measurement already gates step 6, so I ran no probe.

**Findings:**

1. [NON-BLOCKING] **An unexpected exception in `LiveSender.RunAsync` would put exception text in the log, breaking N5 and AC10.** `App.Run` (`src/Censio.Companion/App.xaml.cs:512–517`) logs a faulted task with `CompanionLog.BackgroundTaskFailed(_logger, task.Exception!)`. `LogLine.Format` then writes `exception.ToString()`. Only `IngestClient` is said to map every exception. `LiveSender`'s own code (the token wait, the queue, status handling, the retry delay) is not covered. A socket or HTTP exception's text can hold the host and port.
   Smallest fix: under *Live sender*, add "`RunAsync` lets no exception other than cancellation escape: an unexpected one ends in `Stopped` with `ConnectionLost` and the `LiveSendFailed` line, never `BackgroundTaskFailed`". Pin it with a `LiveSenderTests` row whose token delegate throws, asserting that no log entry carries an exception.
   Recommendation: fix now

2. [NON-BLOCKING] **The tests for AC7 do not cover the security rules on the live connector.** Every connector test builds `new ClientWebSocketConnector(new FakeTimeProvider())` (`ClientWebSocketConnectorTests.cs` lines 67, 86, 106, 126, 151). That includes the redirect, untrusted-certificate, no-`Origin` and proxy-bypass tests. The live sender uses the new keep-alive constructor, which none of these tests exercise. The plan relies on "the existing connector tests … unchanged".
   Smallest fix: under AC7, make `ClientWebSocketConnectorRedirectGivesNoSocket`, `…UntrustedCertificateGivesNoSocket`, `…SendsNoOrigin` and `…BypassesSystemProxy` data rows over both constructors (keep-alive 15 s / 30 s).
   Recommendation: fix now

3. [NON-BLOCKING] **The new cleartext exposure is not stated.** Today's accepted risk (`SECURITY.md` line 36, `replay-sending.md` line 79) covers a replay the user starts once. Forward mode sends the token over `ws://` with no one watching: at every sign-in and every retry (up to every 30 seconds), to whatever answers the configured name. A single-label name such as the spec's `ws://nas:8080/ingest` may be resolved by LLMNR, NetBIOS or mDNS, which any host on the current network can answer. The plan's `SECURITY.md` item says only "`wss://` where the path is not trusted".
   Smallest fix: add this sentence to the *Accepted risks* of `docs/areas/live-sending.md`, verbatim, and the same sentence to `SECURITY.md` *Deployment* after the replay sentence, verbatim:
   "Over `ws://` the Companion sends the ingest token unencrypted at every start and every retry, unattended, to whatever host answers `serverAddress`; a name without a domain (such as `nas`) may be answered by any device of the network the PC is on, so use `wss://`, or an address on a network you trust, when the PC leaves your home network."
   Recommendation: fix now
