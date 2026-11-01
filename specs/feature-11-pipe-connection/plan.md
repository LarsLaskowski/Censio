# Plan: Pipe connection: discovery, reconnect and single-reader handling

Source: Issue #11 | [spec.md](spec.md)
Status: Revised (1)
Tier: security — the connection creates a security control: the impersonation level that keeps the pipe's server (the game, or whoever created the name first) from acting with the player's Windows identity (spec AC13).
Security areas touched: Parsing of external input (the pipe stream, now read live through `PipeMessageReader` by `PipeConnection`); a new local connection to an external process (`NamedPipeConnector`, opening `\\.\pipe\anno117`), proposed as a new entry in `.squad/project.md` *Security areas*; build configuration of a test project (`tests/Censio.Companion.Core.Tests/Censio.Companion.Core.Tests.csproj`: an existing package and a linked source file)

## Problem / root cause

Feature: see [spec.md](spec.md), status `Approved by Lead`, criteria AC1–AC18.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The pipe is `\\.\pipe\anno117`, the game is its server, the reference opens it `GENERIC_READ`, share mode `0`, and never writes | `UbisoftMainzAnno/anno117_pipe_example` `src/pipe.h:19`, `src/pipe.cpp:186`, read 2026-11-01 | confirmed |
| 2 | `NamedPipeClientStream.ConnectAsync(timeout, token)` treats `ERROR_FILE_NOT_FOUND` as "try again" and throws `TimeoutException` at the timeout, the same as for a busy pipe (`ERROR_PIPE_BUSY`, then `WaitNamedPipe` gives `ERROR_SEM_TIMEOUT`) | `dotnet/runtime` `release/10.0`, `src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeClientStream.cs` `ConnectInternal`; `NamedPipeClientStream.Windows.cs` `TryConnect` | confirmed |
| 3 | Any other Windows error of the open is thrown through `Win32Marshal.GetExceptionForWin32Error`: `UnauthorizedAccessException` for access denied, otherwise mostly an `IOException` whose `HResult` is `0x8007xxxx` with the Win32 code in the low 16 bits | `src/libraries/Common/src/System/IO/Win32Marshal.cs` `GetExceptionForWin32Error`, `MakeHRFromErrorCode`; `UnauthorizedAccessException.HResult` is `COR_E_UNAUTHORIZEDACCESS` = `0x80070005` | confirmed |
| 4 | With a cancellable token, `ConnectAsync` waits in slices of 50 ms and checks the token between them, so cancellation ends a pending connect within about 50 ms | `NamedPipeClientStream.cs` `CancellationCheckInterval = 50`, `ConnectInternal` | confirmed |
| 5 | Impersonation level `None` (the default of every constructor without it) sets no `SECURITY_SQOS_PRESENT`; any other level sets it with level − 1 shifted by 16, so `Identification` gives `SECURITY_IDENTIFICATION` | `NamedPipeClientStream.Windows.cs` `TryConnect`; `TokenImpersonationLevel` None 0, Anonymous 1, Identification 2 | confirmed |
| 6 | Without `SECURITY_SQOS_PRESENT` the pipe server may impersonate the client at `SecurityImpersonation`; `SECURITY_SQOS_PRESENT` with `SECURITY_IDENTIFICATION` limits it to identification; the flags hold for a local connection only (server name `.`) | Microsoft documentation `impersonating-a-named-pipe-client.md` line 15 and `impersonation-levels.md` line 30, read by Security in step 3 | confirmed |
| 7 | `PipeDirection.In` opens with `GENERIC_READ` only; `PipeOptions.Asynchronous` makes reads overlapped | `NamedPipeClientStream.Windows.cs` `AccessRightsFromDirection`; `TryConnect` → `InitializeHandle(..., isAsync)` | confirmed |
| 8 | On an asynchronous pipe stream a read that fails with `ERROR_BROKEN_PIPE` or `ERROR_PIPE_NOT_CONNECTED` returns 0 (end of stream), and cancelling the token calls `CancelIoEx`, so the read ends with `OperationCanceledException` | `PipeStream.ValueTaskSource.cs` `CompleteCore`, `RegisterForCancellation`; `Win32Marshal` maps `ERROR_OPERATION_ABORTED` to `OperationCanceledException` | confirmed |
| 9 | A read that returns 0 makes `PipeFrameReader` report the end of the stream between frames and a truncated frame inside one; an `IOException` is read failed; `OperationCanceledException` passes through | `docs/areas/pipe.md` *Limits and errors*; `src/Censio.Pipe/PipeFrameReader.cs:87` | confirmed |
| 10 | `PipeMessageReader` returns a message only after a `Version 2` (the version gate), so any message result means the connection speaks protocol 2 | `src/Censio.Pipe/PipeMessageReader.cs` `ReadAsync`, `ApplyVersion` | confirmed |
| 11 | Enumerating `\\.\pipe\` with `Directory.EnumerateFiles` lists each existing pipe as `\\.\pipe\` plus its full name (a name with a backslash, such as `x\anno117`, as `\\.\pipe\x\anno117`) and opens no pipe instance | to verify: Product Manager, manual test AC17 in step 9 (game off: `Waiting for game` at once; game on: `Receiving`); widely used, not measured | unverified |
| 12 | Querying the attributes of a pipe path (`File.Exists`) opens the pipe | to verify: not needed, only the rejected option 4 of 0035 rests on it, recorded there as not measured | unverified |
| 13 | The game accepts a new client after the previous one closed, and admits one client at a time or more | to verify: Product Manager, manual test AC17 in step 9; the reference reconnects after a broken pipe (`src/pipe.cpp:160–190`), Tabularium117 `docs/protocol.md` marks multi-client `[open]` | unverified |
| 14 | The game starts every connection with `Version`; a save load and the main menu keep the connection | to verify: Product Manager, manual test AC17 in step 9; Tabularium117 `docs/protocol.md` (`[live]`) | unverified |
| 15 | The Windows shell is excluded from coverage and has no test project | `.squad/stack.md` *Coverage exclusions*; record 0004 | confirmed |
| 16 | `Microsoft.Extensions.TimeProvider.Testing` is already centrally versioned | `Directory.Packages.props:13` | confirmed |
| 17 | `App.OnExit` runs on the UI thread after `Shutdown(0)` | `src/Censio.Companion/App.xaml.cs:39`, `:49` | confirmed |
| 18 | `PipeFrameReader` catches only `IOException` and `ObjectDisposedException`; a stream can also throw `UnauthorizedAccessException`, or `OperationCanceledException` for `ERROR_OPERATION_ABORTED` without the Companion's cancel | `src/Censio.Pipe/PipeFrameReader.cs:87`; `Win32Marshal.GetExceptionForWin32Error` | confirmed |

## Acceptance criteria

Feature: the criteria are AC1–AC18 of [spec.md](spec.md).

## Tests per criterion

All in `tests/Censio.Companion.Core.Tests/`. The connection tests use `FakeTimeProvider`, `ScriptedPipeConnector`
and `ControlledPipeStream` (*Test files*); frames are built with the linked `PipeFrameBuilder`. AC15 is met on
Windows by the listing (not found at once) and a timeout while the pipe is still listed (busy), not by a Windows
error code; the `FileNotFoundException` mapping is a defensive branch `ConnectAsync` does not reach (fact 2).

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeConnectionStatusBeforeRunIsWaitingForGame`, `PipeConnectionRunFirstAttemptWithoutDelay`; `TrayMenuFormatToolTipReturnsTextPerState` (data rows for all six states, exact texts), `TrayMenuFormatToolTipUndefinedStateThrows` | `PipeConnectionTests.cs`, `TrayMenuTests.cs` |
| AC2 | `PipeConnectionPipeNotFoundRetriesWithGrowingDelays` (attempt times 0, 1, 3, 7, 15, 25, 35 s), `PipeConnectionPipeNotFoundRaisesNoEvent`; `PipeRetryDelayForReturnsDoublingUpToTenSeconds` (1→1, 2→2, 3→4, 4→8, 5→10, 50→10), `PipeRetryDelayForBelowOneThrows` | `PipeConnectionTests.cs`, `PipeRetryDelayTests.cs` |
| AC3 | `PipeConnectionConnectedRaisesConnected`, `PipeConnectionVersionTwoRaisesReceiving` | `PipeConnectionTests.cs` |
| AC4 | `PipeConnectionEndAtFrameBoundaryRaisesWaitingForGame`, `PipeConnectionEndInsideFrameRaisesErrorTruncatedFrame`, `PipeConnectionEndedConnectionRetriesAfterDelay`, `PipeConnectionReappearingPipeIsConnected` | `PipeConnectionTests.cs` |
| AC5 | `PipeConnectionSessionAndIslandMessagesKeepReceiving` (session start, session end, two island frames: one event in all, no second attempt) | `PipeConnectionTests.cs` |
| AC6 | `PipeConnectionBusyRaisesPipeBusy`, `PipeConnectionBusyRetriesWithDelay`, `PipeConnectionBusyThenConnectedRaisesConnected`; `PipeConnectResultFromFailureTimeoutWithPipeIsBusy` | `PipeConnectionTests.cs`, `PipeConnectResultTests.cs` |
| AC7 | `PipeConnectionWrongVersionRaisesProtocolMismatchWithVersion`, `PipeConnectionFrameBeforeVersionRaisesProtocolMismatchWithoutVersion`, `PipeConnectionRepeatedMismatchRaisesNoEvent`, `PipeConnectionMismatchKeepsReading`, `PipeConnectionVersionTwoAfterMismatchRaisesReceiving` | `PipeConnectionTests.cs` |
| AC8 | `PipeConnectionSkippedFrameKeepsState` (data rows: empty frame, unknown type, malformed message) | `PipeConnectionTests.cs` |
| AC9 | `PipeConnectionStreamEndingErrorRaisesError` (data rows: truncated frame, oversized frame, read failed by `IOException`), `PipeConnectionStreamEndingErrorDisposesStream`, `PipeConnectionErrorReplacedByNextAttempt`, `PipeConnectionUnexpectedReadExceptionRaisesReadFailed` (data rows: `UnauthorizedAccessException`, `OperationCanceledException` without a cancel; then a new attempt after the delay) | `PipeConnectionTests.cs` |
| AC10 | `PipeConnectionFailedAttemptRaisesErrorWithCode`, `PipeConnectionFailedAttemptReplacedByNextAttempt`, `PipeConnectionConnectorExceptionRaisesErrorWithoutCode`; `PipeConnectResultFromFailureUnauthorizedAccessIsFailedFive`, `PipeConnectResultFromFailureIOExceptionCarriesWin32Code`, `PipeConnectResultFromFailureOtherExceptionHasNoCode` | `PipeConnectionTests.cs`, `PipeConnectResultTests.cs` |
| AC11 | `PipeConnectionCancelDuringAttemptCompletes`, `PipeConnectionCancelDuringDelayCompletes`, `PipeConnectionCancelDuringReadCompletes` (each: the run task completes without `Advance`, no event after the cancel), `PipeConnectionCancelDuringReadDisposesStream`, `PipeConnectionRunWhileRunningThrows` | `PipeConnectionTests.cs` |
| AC12 | `PipeConnectionRaisesEventsInOrder` (Waiting → Connected → Receiving → Waiting, status values and causes), `PipeConnectionCauseChangeRaisesEvent` (error code 5 then read failed), `PipeConnectionThrowingHandlerDoesNotStopConnection`, `PipeConnectionThrowingHandlerOtherHandlersReceive`; `TrayStatusUpdaterManyChangesWhilePendingPostOnce` (1,000 notifications before the post runs: one post, showing the latest status), `TrayStatusUpdaterNotifyAfterPostRanPostsAgain`, `TrayStatusUpdaterChangeDuringShowPostsAgain` | `PipeConnectionTests.cs`, `TrayStatusUpdaterTests.cs` |
| AC13 | `PipeEndpointNameIsAnno117OnLocalServer`, `PipeEndpointOpensReadOnlyAsynchronous`, `PipeEndpointImpersonationLevelIsIdentification`; `PipeConnectionNeverWritesToStream` (the stream records every write call) | `PipeEndpointTests.cs`, `PipeConnectionTests.cs` |
| AC14 | `PipeConnectionReadsAllFramesWithoutClockAdvance` (twenty frames queued at once are all requested without `Advance`) | `PipeConnectionTests.cs` |
| AC15 | `PipeConnectionNotFoundRaisesNoBusyAndNoWait` (a not-found attempt completes at once; the next attempt is due 1 s later, not 3 s); `PipeConnectResultFromFailureTimeoutWithoutPipeIsNotFound`, `PipeConnectResultFromFailureFileNotFoundIsNotFound`; `PipeEndpointIsListedMatchesWholePath` (data rows: `\\.\pipe\anno117` true, `\\.\PIPE\ANNO117` true, `\\.\pipe\x\anno117` false, `\\.\pipe\anno1170` false, empty list false) | `PipeConnectionTests.cs`, `PipeConnectResultTests.cs`, `PipeEndpointTests.cs` |
| AC16 | `PipeConnectionShortReceivingDoesNotResetDelay` (Version 2 then oversized frame, three times: delays 1, 2, 4 s), `PipeConnectionReceivingThirtySecondsResetsDelay` (after 29.999 s no reset, after 30 s reset), `PipeConnectionInterruptedReceivingDoesNotResetDelay` (20 s `Receiving`, mismatch, 20 s `Receiving`, end: no reset), `PipeConnectionOneCounterSpansFailureKinds` (not found, busy, failed: delays 1, 2, 4 s) | `PipeConnectionTests.cs` |

## Verification without tests

Only two criteria; the rest are tested above.

- AC17: the manual test on Windows, run by the Product Manager on the PR head before PR approval (step 9) with the
  release-style publish of `README.md`: Companion before and after the game, quit and restart, another save, the main
  menu, a second client if at hand (the reference example works as one), and `Exit` in `Waiting for game`,
  `Receiving` and `Pipe busy` (under 2 s), and, if it can be set up, a pipe named `x\anno117` (a small PowerShell
  `NamedPipeServerStream`) while the game is off: `Waiting for game` at once. It also settles facts 11, 13 and 14. The orchestrator asks for it as soon
  as step 7 is green.
- AC18: the Reviewer reads `README.md` and `docs/USER_GUIDE.md` against the code (step 8), both languages.

## Approach

**Layout of the user-visible text.** The tooltip is one line, `Censio Companion: ` followed by the state text:
`Waiting for game`, `Connected`, `Receiving`, `Pipe busy`, `Protocol mismatch`, `Error` (spec *Scenarios*). No
cause is shown. The menu does not change.

**Connector (Windows shell, `NamedPipeConnector`).** Per attempt:

1. List `Directory.EnumerateFiles(@"\\.\pipe\")`; if `PipeEndpoint.IsListed(entries)` is false (it compares each
   whole entry with `PipeEndpoint.PipePath`, ordinal, case-insensitive, so `x\anno117` does not count), return
   `PipeConnectResult.NotFound` at once. If the listing throws, the pipe is treated as existing and step 2 runs.
2. Create `new NamedPipeClientStream(PipeEndpoint.ServerName, PipeEndpoint.PipeName, PipeEndpoint.Direction,
   PipeEndpoint.Options, PipeEndpoint.ImpersonationLevel)` and `await ConnectAsync(PipeEndpoint.AttemptTimeout,
   token)`; on success return `PipeConnectResult.FromStream(stream)`.
3. On `OperationCanceledException`: dispose the stream and rethrow. On any other exception: dispose the stream and
   return `PipeConnectResult.FromFailure(exception, pipeExists)`, with `pipeExists` the listing checked again
   (true when that listing throws).

**Classification (`PipeConnectResult.FromFailure`, Core, tested on Linux).** `TimeoutException`: `Busy` when
`pipeExists`, else `NotFound`. `FileNotFoundException`: `NotFound` (defensive; `ConnectAsync` returns no such
exception for a missing pipe, fact 2). Any other exception: `Failed` with the error
code `HResult & 0xFFFF` when `HResult & 0xFFFF0000` is `0x80070000`, else without code (fact 3).

**State machine (`PipeConnection.RunAsync`, Core).** One counter `failures`, starting at 0; status
`Waiting for game`. Loop until the token is cancelled:

1. Attempt (`connector.ConnectAsync(token)`; any exception other than the stop's own cancellation — an
   `OperationCanceledException` while `token.IsCancellationRequested` — is `Failed` without code). Not
   found → `Waiting for game`; busy → `Pipe busy`; failed → `Error` with the code; each adds 1 to `failures`.
2. Connected → `Connected`, then the session on a new `PipeMessageReader(stream)`, reading again as soon as a
   result is handled (AC14):
   - a message → `Receiving`; entering `Receiving` stamps `receivingSince` from the injected clock, leaving it
     clears the stamp (fact 10);
   - protocol mismatch → `Protocol mismatch` with `ReceivedVersion` (an event only when the version differs);
   - empty frame, unknown type, malformed message → no change;
   - end of stream → the session ends with `Waiting for game`;
   - truncated frame, oversized frame, read failed → the session ends with `Error` and that kind;
   - any exception of the read other than the stop's own cancellation (fact 18) → the session ends as read failed.

   The stream is disposed when the session ends. If `receivingSince` is set (the connection is in `Receiving`
   when it ends) and the clock shows at least 30 s since, `failures` is set to 0; then 1 is added, and the session's end status is set.
3. Wait `PipeRetryDelay.For(failures)` with `Task.Delay(delay, timeProvider, token)`.

Status changes go through one method: it does nothing when the token is cancelled or the new status equals the
current one (value equality of state and cause), else stores it and calls each handler of the invocation list
separately, catching and dropping any exception (E10; no log until #13). Cancellation ends `RunAsync` normally (no
exception for its own token), with the stream disposed. A second `RunAsync` while one runs throws
`InvalidOperationException`.

**Shell.** `App.OnStartup`, after the guard: the tray icon with `TrayMenu.FormatToolTip(PipeConnectionState.WaitingForGame)`,
a `PipeConnection(new NamedPipeConnector(), TimeProvider.System)`, a `TrayStatusUpdater(() => connection.Status,
action => Dispatcher.BeginInvoke(DispatcherPriority.Background, action), status =>
trayIcon.SetToolTip(TrayMenu.FormatToolTip(status.State)))` whose `Notify` handles `StatusChanged`, and `RunAsync` started
with `Task.Run` on a `CancellationTokenSource`. `App.OnExit`: dispose the tray icon (icon gone at once), cancel the
source, dispose the guard; it does **not** wait for the run task, so the UI thread never waits on a pipe call; the
process exit closes the handle, and a cancelled connect or read ends within about 50 ms anyway (facts 4, 8).
`TrayIcon.SetToolTip` does nothing once disposed.

**Hand-off to the tray (`TrayStatusUpdater`, Core).** `Notify` (any thread) posts one callback only when none is
pending (an interlocked flag); the callback clears the flag first, then reads the current status and shows it. So
however fast the status changes, at most one update waits in the dispatcher queue and it shows the latest status;
input such as the `Exit` click runs ahead of it at `Background` priority.

## Options considered

The options for discovery, classification, the impersonation level and the retry reset are in record
[0035](../../docs/decisions/0035-pipe-connection-states-retry-and-identification-level.md) (*Options considered*):
listing check plus `NamedPipeClientStream` (chosen) against P/Invoke `CreateFileW`/`WaitNamedPipeW`, `ConnectAsync`
alone, `File.Exists`; `Identification` against `Anonymous` and `CurrentUserOnly`; reset after 30 s in `Receiving`
against reset on any `Receiving`. Further options of this plan:

- **Waiting on `Exit` for the run task (bounded, 1 s)** — the pipe closed before the process ends. Lost: it blocks
  the UI thread, which the spec rules out; process exit closes the handle anyway.
- **Reading through `PipeTickAssembler`** — ticks ready for #13. Lost: no consumer, and a 30-second timer that
  decides nothing here (spec challenge m1).
- **Throttling tooltip updates by time** instead of coalescing — also bounded. Lost: needs a timer in the shell
  and still lags; coalescing bounds the queue to one without one.
- **`RunAsync` throwing `OperationCanceledException` on stop** — the usual .NET pattern. Lost: the shell starts it
  fire-and-forget; a normal completion leaves no unobserved exception.

The plan weighs options: the plan challenge runs.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `PipeConnectionState.cs` | new enum |
| Censio.Companion.Core | `PipeConnectionStatus.cs` | new record struct: state and cause |
| Censio.Companion.Core | `PipeConnectResultKind.cs`, `PipeConnectResult.cs` | new: an attempt's result and its classification |
| Censio.Companion.Core | `IPipeConnector.cs` | new interface |
| Censio.Companion.Core | `PipeEndpoint.cs` | new: pipe name and path, the listing match, direction, options, impersonation level, attempt timeout |
| Censio.Companion.Core | `PipeRetryDelay.cs` | new: delays and the 30-second rule |
| Censio.Companion.Core | `PipeConnection.cs` | new: the state machine |
| Censio.Companion.Core | `TrayStatusUpdater.cs` | new: the coalescing hand-off to the tray |
| Censio.Companion.Core | `TrayMenu.cs` | new `FormatToolTip`; `ToolTip` stays `Censio Companion` (its prefix) |
| Censio.Companion | `NamedPipeConnector.cs` | new, Windows connector |
| Censio.Companion | `TrayIcon.cs` | new `SetToolTip`; the constructor keeps its signature |
| Censio.Companion | `App.xaml.cs` | starts and stops the connection, marshals the tooltip |

## Signatures (for the Dev's skeleton)

New types `PipeConnectionState`, `PipeConnectionStatus`, `PipeConnectResultKind`, `PipeConnectResult`,
`IPipeConnector`, `PipeEndpoint`, `PipeRetryDelay`, `PipeConnection`, `TrayStatusUpdater`, `NamedPipeConnector`; new members
`PipeEndpoint.PipePath`, `PipeEndpoint.IsListed`,
`TrayMenu.FormatToolTip`, `TrayIcon.SetToolTip`. Exceptions per member are noted in the comments.

```csharp
// Censio.Companion.Core; usings System.IO.Pipes, System.Security.Principal, Censio.Pipe
public enum PipeConnectionState { WaitingForGame, Connected, Receiving, PipeBusy, ProtocolMismatch, Error }

public readonly record struct PipeConnectionStatus
{
    public required PipeConnectionState State { get; init; }
    public PipeErrorKind? ErrorKind { get; init; }
    public int? ErrorCode { get; init; }
    public int? ReceivedVersion { get; init; }
}

public enum PipeConnectResultKind { Connected, NotFound, Busy, Failed }

public sealed class PipeConnectResult           // private constructor
{
    public PipeConnectResultKind Kind { get; }
    public Stream? Stream { get; }
    public int? ErrorCode { get; }
    public static PipeConnectResult NotFound { get; }
    public static PipeConnectResult Busy { get; }
    public static PipeConnectResult FromStream(Stream stream);                         // ArgumentNullException
    public static PipeConnectResult FromFailure(Exception exception, bool pipeExists); // ArgumentNullException
}

public interface IPipeConnector
{
    // Throws only OperationCanceledException; the caller owns and disposes the stream.
    ValueTask<PipeConnectResult> ConnectAsync(CancellationToken cancellationToken);
}

public static class PipeEndpoint
{
    public const string ServerName = ".";
    public const string PipeName = "anno117";
    public const PipeDirection Direction = PipeDirection.In;
    public const PipeOptions Options = PipeOptions.Asynchronous;
    public const TokenImpersonationLevel ImpersonationLevel = TokenImpersonationLevel.Identification;
    public const string PipePath = @"\\.\pipe\anno117";
    public static TimeSpan AttemptTimeout { get; }              // 2 s
    public static bool IsListed(IEnumerable<string> entries);   // whole entry equals PipePath, ordinal, ignore case; ArgumentNullException
}

public static class PipeRetryDelay
{
    public static TimeSpan Max { get; }                         // 10 s
    public static TimeSpan SuccessfulConnectionTime { get; }    // 30 s
    public static TimeSpan For(int consecutiveFailures);        // ArgumentOutOfRangeException below 1
}

public sealed class PipeConnection
{
    public PipeConnection(IPipeConnector connector, TimeProvider timeProvider);
    public PipeConnectionStatus Status { get; }
    public event EventHandler<PipeConnectionStatus>? StatusChanged;
    public Task RunAsync(CancellationToken cancellationToken);  // InvalidOperationException while running
}

public sealed class TrayStatusUpdater
{
    // Arguments stored; ArgumentNullException for null.
    public TrayStatusUpdater(Func<PipeConnectionStatus> readStatus, Action<Action> post, Action<PipeConnectionStatus> show);
    public void Notify();                                       // any thread; at most one pending post
}

// TrayMenu (existing)
public static string FormatToolTip(PipeConnectionState state); // ArgumentOutOfRangeException for an undefined value

// Censio.Companion
internal sealed class NamedPipeConnector : IPipeConnector { }  // parameterless constructor
// TrayIcon (existing)
public void SetToolTip(string text);
```

Existing files the skeleton rewrites: `src/Censio.Companion.Core/TrayMenu.cs` (one member added; the rest final),
`src/Censio.Companion/TrayIcon.cs`, `src/Censio.Companion/App.xaml.cs`.

## Test files

- `tests/Censio.Companion.Core.Tests/PipeConnectionTests.cs`, `PipeConnectResultTests.cs`, `PipeEndpointTests.cs`,
  `PipeRetryDelayTests.cs`, `TrayStatusUpdaterTests.cs` (new); `TrayMenuTests.cs` (extended).
- Test doubles (new, Tester): `ScriptedPipeConnector.cs` — an `IPipeConnector` that returns queued results, or holds
  an attempt until released, counts attempts and stamps each with the fake clock's time; an attempt without a script
  entry stays pending until cancelled. `ControlledPipeStream.cs` — a read-only `Stream` the test feeds with bytes,
  ends (read returns 0, fact 8) or fails with a given exception (`IOException`,
  `UnauthorizedAccessException`, `OperationCanceledException`, fact 18); a pending read ends with `OperationCanceledException` on
  cancel; it records disposal and every write call (`CanWrite` false). It models the end and the cancellation of an
  asynchronous pipe read as facts 8 and 9 state.
- `Censio.Companion.Core.Tests.csproj` (Tester): `PackageReference Microsoft.Extensions.TimeProvider.Testing`
  (no version) and `<Compile Include="..\Censio.Pipe.Tests\PipeFrameBuilder.cs" Link="PipeFrameBuilder.cs" />`.
- Existing call sites of changed signatures: none (`TrayMenu.ToolTip` and `TrayIcon`'s constructor keep theirs;
  `TrayMenuToolTipIsCensioCompanion` stays valid).
- Files no unit test can cover: `src/Censio.Companion/NamedPipeConnector.cs`, `TrayIcon.cs`, `App.xaml.cs` — Windows
  shell, outside every coverage measure by construction (record 0004, fact 15); verified by AC17.

## Areas

- `pipe`: new section *Connection*; scope and *Not here* updated.
- `companion`: tooltip with the connection state; `Exit` within 2 seconds.

## Documentation updates

All by the Dev once the code is green, against that code:

- `docs/areas/pipe.md`: *Scope* adds the connection; new section *Connection* (the pipe name; read-only,
  asynchronous, impersonation level identification; the existence check; attempt classification; the six states and
  their transitions; delays 1, 2, 4, 8, 10 s, one counter, reset when the connection was in `Receiving` without
  interruption for at least 30 s up to its end; an unexpected read exception ends the connection as read failed; 2-second attempt; no
  read timeout; a quit inside a frame shows `Error` briefly; stop); *Deliberate differences*, "The caller that opens
  the pipe (#11) must reconnect" becomes the statement that the connection reconnects; *Order and end*, "the
  decoupling of #11" becomes "the hand-off built with the first consumers (#12, #13)"; *Residual risks* adds facts
  11, 13, 14 and a foreign pipe server (E11) until AC17 confirms them; *Related decisions* adds 0035; *Not here*
  drops the connection.
- `docs/areas/companion.md`: tooltip `Censio Companion: <state>`, the six texts, link to `pipe.md`; at most one
  pending tooltip update, showing the latest state; `Exit` ends
  the process within 2 seconds; *Limits and errors*: no error dialog, nothing logged; *Not here* adjusted.
- `docs/areas/README.md` index: `pipe` row scope adds "the connection: discovery, states, reconnect", *Not here*
  drops "Opening the pipe and reconnecting"; `companion` row scope adds "connection state in the tooltip".
- `docs/ARCHITECTURE.md`: the `Censio.Companion.Core` row adds "the pipe connection"; *Data flow* one sentence that
  the Companion connects to the pipe on its own and reconnects, linking 0035; *Security model*: the pipe is opened
  read-only at identification level.
- `README.md` (English, then German): *Status* — the Companion connects to the game's pipe and shows the state in
  its tooltip; "No game data is read" becomes "Game data is read but not yet shown or stored"; *Companion* — the game
  needs the launch argument `/pipe` (details later, #16), the six states and their meaning.
- `docs/USER_GUIDE.md` (English, then German): *First start* — the tooltip states with a one-line meaning each, the
  brief `Error` when the game quits, reconnect without restart, `Pipe busy` when another tool holds the pipe.
- `SECURITY.md` *Scope*: add "Connecting to the statistics pipe (Companion)".
- `.squad/project.md` *Security areas* (Dev): "Pipe connection: `NamedPipeConnector` opens `\\.\pipe\anno117`
  read-only at impersonation level `Identification`; the pipe's server is untrusted (record 0035)". *Test doubles*
  (Tester): `ScriptedPipeConnector`, `ControlledPipeStream`, `FakeTimeProvider` in `tests/Censio.Companion.Core.Tests`
  and the linked `PipeFrameBuilder`.

## Architecture check

The logic stays in `Censio.Companion.Core` and the shell stays thin (record 0004); the pipe format stays confined to
`Censio.Pipe` (record 0018), used only through `PipeMessageReader`. The strict reader (record 0033) is not changed;
its "stream ends" errors now lead to a reconnect, as the record expects. No guarantee of `.squad/project.md` is
touched (none recorded).

## Security considerations

- [ ] Limits: no new byte or length limit; the reader's 1 MiB frame limit applies before decoding, as today.
  The connection holds one frame and its message.
- [ ] Exceptions reaching the user: none as text. A connector exception becomes `Error` with at most a Win32 code in
  the event; the tooltip shows `Error` only. A handler exception is dropped.
- [ ] The impersonation level is a constant tested in `PipeEndpointTests`; the shell must use `PipeEndpoint` for
  every argument of the stream (Security diff review).

## Decision records

- [`docs/decisions/0035-pipe-connection-states-retry-and-identification-level.md`](../../docs/decisions/0035-pipe-connection-states-retry-and-identification-level.md) (Proposed, indexed)

## Challenge

- Major 1 (an unexpected exception kills the connection silently): accepted. Any exception of an attempt or a read
  other than the stop's own cancellation (`OperationCanceledException` with the token cancelled) is handled: an
  attempt as `Error` without code, a read as read failed, stream disposed, failure counted, retry after the delay
  (*Approach*, fact 18). New test `PipeConnectionUnexpectedReadExceptionRaisesReadFailed` (AC9 row);
  `ControlledPipeStream` throws a given exception.
- minor 1 (AC15 wording against the listing): accepted. The note under *Tests per criterion* says how AC15 is met;
  `FileNotFoundException` is kept as a defensive branch and marked so. AC15's intent is unchanged, so the spec stays.
- minor 2 (a throwing listing never connects): accepted. A throwing listing counts as "pipe exists" and the connect
  runs (*Approach*, connector steps 1 and 3). No search pattern: it matches on the last name part, which is what
  Security finding 2 rules out.
- minor 3 (the 30-second rule counts more than time in `Receiving`): accepted with the spec's wording. The stamp is
  set on entering `Receiving` and cleared on leaving it, so only an uninterrupted stretch up to the end counts; new
  test `PipeConnectionInterruptedReceivingDoesNotResetDelay`; `pipe.md` wording in *Documentation updates*.

## Security review

- Blocking 1 (unbounded tooltip updates): accepted. New Core type `TrayStatusUpdater`: at most one pending post, the
  callback reads the current status (latest wins), posted at `Background` priority so `Exit` runs ahead. Tests in the
  AC12 row (1,000 changes, one post, latest shown). Spec *Memory* and AC12 now cover the hand-off.
- 2 (the listing can match a nested name): accepted. `PipeEndpoint.IsListed` compares the whole entry with
  `\\.\pipe\anno117`; tests with `x\anno117`, case and prefix variants (AC15 row); the case is added to the
  manual test (AC17) where it can be set up.
- 3 (fact 6): accepted. Fact 6 is confirmed with the two passages; record 0035 names `SecurityImpersonation`, cites
  `impersonating-a-named-pipe-client.md`, and says the level holds only for the local server name `.`.
- 4 (events carry the received version): accepted. Spec *Security* now reads "no pipe content beyond the received
  version number".

## Out of scope / follow-ups

As spec *Out of scope*. No follow-up issue: the consumer hand-off belongs to #12 and #13, the `/pipe` how-to to #16.
