# Plan: Limit ingest connections per host

Source: Issue #177 | [spec.md](spec.md)
Tier: security — the change adds a guard against abuse (a limit per host and a block of hosts whose hellos keep failing, `.squad/routing.md`, *Tiers*: "a rate limit against abuse is a guard") and changes which address the server trusts as a client's.
Security areas touched: Network exposure (`/ingest` refusals, the forwarded-headers options of the whole server); Ingest token and Companion authentication (failed hellos counted per host, the accepted Companion's connection ended on a blocked send)

## Problem / root cause

Feature: see [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The bound on waiting connections is global, 16, refused `503`; no address is read | `IngestConnections.cs` `MaxPendingHellos`; `IngestEndpoint.cs` `HandleAsync` (400, 403, 503 in this order) | confirmed |
| 2 | A place is freed when the hello is decided, before the close wait (the issue's 10 + 2 s is wrong) | `IngestSession.cs` `RunHelloPhaseAsync` (`LeavePendingHello` after the read), `OnHelloTimeout` | confirmed |
| 3 | The server's sends have no timeout | `IngestSession.cs` `SendAsync`: `_webSocket.SendAsync(..., CancellationToken.None)` | confirmed |
| 4 | No code in `src/` reads the remote address or forwarded headers | `grep -rn "RemoteIpAddress\|Forwarded" src/` finds nothing | confirmed |
| 5 | `WebApplication.CreateBuilder` (`Program.cs:8`) adds ASP.NET Core's `ForwardedHeadersStartupFilter` and `ForwardedHeadersOptionsSetup`, which with `FORWARDEDHEADERS_ENABLED` `true` enable `XForwardedFor` and clear the known proxies | both type names in `Microsoft.AspNetCore.dll` 11.0.0 (`strings`); the behavior: ASP.NET Core documentation, *Configure ASP.NET Core to work with proxy servers and load balancers* | confirmed |
| 6 | A `PostConfigure<ForwardedHeadersOptions>` that clears `XForwardedFor` wins over that setup, so the remote address stays the TCP peer's; .NET does not process the RFC 7239 `Forwarded` header | to verify: AC9's endpoint tests (Tester, step 5/6), and once with the `PostConfigure` removed they must fail (Tester, step 6) | unverified |
| 7 | A test sets a connection's address through `WebSocketClient.ConfigureRequest`, an `Action<HttpRequest>` on the server's request | `IngestEndpointTests.cs:152` sets `request.Headers.Origin` there; `request.HttpContext.Connection.RemoteIpAddress` is settable on the same object | confirmed |
| 8 | Cancelling `WebSocket.SendAsync` aborts the socket and ends a send blocked on a full transport, on the in-memory pipe and on Kestrel | to verify: AC8's session test (pipe, Tester) and the measurement *noread-send* on the published server (Dev, step 6) | unverified |
| 9 | Whether .NET's keep-alive aborts while its ping send is blocked behind a full transport | to verify: measurement *noread-then-idle* (Dev, step 6) | unverified |
| 10 | `InMemoryDuplexStream` pipes pause a writer at 65,536 unread bytes (default `Pipe`) | `tests/Censio.Server.Tests/InMemoryDuplexStream.cs:85` `new Pipe()` | confirmed |
| 11 | Records 0062 and 0064 are unreleased | `git ls-tree v0.2.0 docs/decisions/` holds neither; `git ls-remote --tags origin`: `v0.1.0`, `v0.2.0` | confirmed |
| 12 | `IngestSession`'s constructor has 7 parameters, the S107 limit | `IngestSession.cs` constructor | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | IngestHostsTryEnterPendingHelloFifthOfOneHostReturnsNull, IngestHostsTryEnterPendingHelloOtherHostWhileFirstFullReturnsHost, IngestEndpointFifthPendingConnectionOfOneHostReturns429, IngestEndpointSeventeenthPendingConnectionReturns503 (rewritten) | tests/Censio.Server.Tests/IngestHostsTests.cs, tests/Censio.Server.Tests/IngestEndpointTests.cs |
| AC2 | IngestEndpointBlockedHostPlainRequestReturns400, IngestEndpointBlockedHostWithOriginReturns403, IngestEndpointFullHostWhileSixteenPendingReturns429, IngestEndpointRefusedUpgradesWriteNoLogLine | tests/Censio.Server.Tests/IngestEndpointTests.cs |
| AC3 | IngestHostsRecordFailedHelloTenthWithinWindowBlocksHost, IngestHostsRecordFailedHelloTenSpreadOverMoreThanWindowDoesNotBlock, IngestHostsRecordFailedHelloTenthLogsBlockStartOnce, IngestHostsTryEnterPendingHelloDuringBlockDoesNotExtendBlock, IngestHostsTryEnterPendingHelloAfterBlockReturnsHost, IngestHostsRecordFailedHelloDuringBlockIsNotCounted, IngestEndpointTenthTokenRefusedHelloBlocksHost | tests/Censio.Server.Tests/IngestHostsTests.cs, tests/Censio.Server.Tests/IngestEndpointTests.cs |
| AC4 | IngestSessionTokenRefusedHelloCountsAsFailedHello, IngestSessionUnsupportedVersionHelloCountsAsFailedHello, IngestSessionFirstMessageNotHelloCountsAsFailedHello, IngestSessionBinaryFirstMessageCountsAsFailedHello, IngestSessionHelloTimeoutCountsAsFailedHello, IngestSessionPeerClosedBeforeHelloCountsAsFailedHello, IngestSessionCompanionConnectedRefusalIsNotCounted, IngestSessionProfileStoreFailureIsNotCounted, IngestSessionServerStopBeforeHelloIsNotCounted, IngestHostsRecordAcceptedHelloResetsCountKeepsBlock, IngestEndpointAcceptFailureLeavesPendingPlace (adapted) | tests/Censio.Server.Tests/IngestSessionTests.cs, tests/Censio.Server.Tests/IngestHostsTests.cs, tests/Censio.Server.Tests/IngestEndpointTests.cs |
| AC5 | IngestHostsBlockEndLogsRefusedUpgradeCount, IngestHostsBlockEndWithoutRefusedUpgradesLogsZero, IngestHostsBlockLinesNameNoAddress | tests/Censio.Server.Tests/IngestHostsTests.cs |
| AC6 | IngestHostsTryEnterPendingHelloTwoIpv4AddressesAreSeparateHosts, IngestHostsTryEnterPendingHelloTwoIpv6AddressesOfOnePrefixAreSeparateHosts, IngestHostsTryEnterPendingHelloWithoutAddressIsOneHost, IngestHostsTryEnterPendingHelloIpv4MappedAddressIsIpv4Host | tests/Censio.Server.Tests/IngestHostsTests.cs |
| AC7 | IngestHostsTryEnterPendingHelloBeyondTrackedHostsSharesOneEntry, IngestHostsTryEnterPendingHelloLastAcceptedHostKeepsOwnEntry, IngestHostsTryEnterPendingHelloForgottenHostFreesEntry, IngestHostsTryEnterPendingHelloWaitingHostIsNotForgotten | tests/Censio.Server.Tests/IngestHostsTests.cs |
| AC8 | IngestSessionRepliesNotReadEndsConnectionAfterSendTimeout, IngestSessionIdleWithReceivePendingStaysConnected; manual: Dev, step 6, measurements *noread-send*, *noread-then-idle* and *idle* on `dotnet publish src/Censio.Server -c Release` of the branch head (*Approach*) | tests/Censio.Server.Tests/IngestSessionTests.cs |
| AC9 | IngestEndpointForwardedForWithForwardedHeadersEnabledCountsForPeer, IngestEndpointForwardedHeadersCountForPeer | tests/Censio.Server.Tests/IngestEndpointTests.cs |

Each test carries `[TestCategory("ACn")]` of this spec. The rewritten and adapted endpoint tests keep their old
category (`AC11` of #28) and add this one.

## Approach

Three new types in `src/Censio.Server`, a changed endpoint and session. New user-visible output is two log lines
whose text the spec fixes, and the status `429`; there is no other layout.

**`IngestHosts`** (singleton) keeps the state per host under one lock, on the injected `TimeProvider`:

- Key: `RemoteIpAddress`, an IPv4-mapped address mapped to IPv4, the scope id dropped (the key is the address bytes);
  `null` is a key of its own. An entry (`IngestHost`) holds its waiting count, the times of its failed hellos (at most
  10), the end of its block, the upgrades refused during it and the block's timer.
- `TryEnterPendingHello(address)`: finds the entry — the protected entry when its address matches, else the table,
  else a new table entry while fewer than `MaxTrackedHosts` are held (when full, forgettable entries are removed first),
  else the shared overflow entry. Then: blocked (end in the future) → count the refusal, return `null`; 4 waiting →
  return `null`; else count it waiting and return the entry. An entry is forgettable when nothing waits and now is at
  least 60 seconds after its last failed hello and its block end (an entry with neither is forgettable at once).
- `RecordFailedHello(host)`: ignored while the host is blocked (spec E3); drops times with `now - t >= 60 s`, adds now;
  at 10 it clears the times, sets the block end to now + 60 s, resets the refused count, starts a one-shot timer of
  `BlockDuration` and logs the start line. The timer, under the lock, clears the block and logs the end line with the
  count; the line is written outside the lock.
- `RecordAcceptedHello(host)`: clears the host's times (not a block) and makes the host the protected entry: a table
  entry of the same address moves out of the table; the previous protected entry moves into the table when there is
  room, else it is dropped (its waiting connections keep their handle; only a token holder can cause this, and the
  only effect is a fresh count for that address).
- `Dispose` disposes every block timer. Log lines are `[LoggerMessage]` methods of `IngestHosts`: `Warning`
  `A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.` and `Information`
  `A host's block ended; {Refused} connections were refused while it lasted.`, logger category `Censio.Server.Ingest`
  (`AddCensioIngest` passes a logger of the endpoint's `LogCategory`, a constant of the same class).

**`IngestAdmission`** is one connection's places: the global and the host's waiting place, the Companion's place,
and the outcome report. It replaces the `IngestConnections` parameter of `IngestSession`, which keeps the constructor at
7 parameters (fact 12).

**Endpoint** (`HandleAsync`): after `400` and `403`, `hosts.TryEnterPendingHello(context.Connection.RemoteIpAddress)`;
`null` → `429`. Then `connections.TryEnterPendingHello()`; false → `hosts.LeavePendingHello(host)` and `503`. The order is
spec E4. When the session never starts (a failed accept), the `finally` leaves both waiting places and records a failed
hello. `AddCensioIngest` registers `IngestHosts` (factory with the clock and the category logger) and
`services.PostConfigure<ForwardedHeadersOptions>(options => options.ForwardedHeaders &= ~ForwardedHeaders.XForwardedFor)`,
so `X-Forwarded-For` never replaces the remote address, for the whole server; `X-Forwarded-Proto` keeps its effect.

**Session**: reports its hello's outcome once. `RecordAcceptedHello` where it logs `Companion connected.`;
`RecordFailedHello` when the hello is refused `unsupportedVersion` or `tokenRefused`, closed without a reply, timed out,
or the session ends before a decision while the server is not stopping; nothing for `companionConnected`, a failed
profile store, or an end caused by the stop. A once-guard field keeps it to one report.

**Send bound**: `IngestSession.SendTimeout` 30 seconds. `SendAsync` passes a token of
`new CancellationTokenSource(SendTimeout, _timeProvider)`, created after the send lock is taken, to
`_webSocket.SendAsync`; cancellation aborts the socket (fact 8), the exception ends the session through the existing
`catch`, and `EndAsync` releases the place and logs `Companion disconnected; …`. Worst case from the last frame read: the
store (about 3 s, `ingest.md`) plus 30 s, under the 60 s of AC8. A Companion that reads at least 37 KB/s gets the
largest reply (an acknowledgement of about 1.1 MB, `ingest.md`) in time. The idle part of AC8 (answering no ping) stays
the keep-alive's, measured at 56.24 s.

**Measurements** (Dev, step 6, scratch probes outside the repository as in `ingest.md`, *Measurements*; published
Release server of the branch head): *noread-send* — the client sends ticks without ever receiving: the time from the
last frame the server read to the abort, `Companion disconnected; …`, then a hello accepted; *noread-then-idle* — the
client sends 1, 10, 100 and 1,000 ticks without receiving, then nothing: the abort within 60 s of its last frame in every
run (fact 9); *idle* — a client with a receive pending stays open at 110 s, as before. A run over 60 s goes to the Lead
(mode `decide`).

**Seam for the endpoint tests**: `client.ConfigureRequest = request => request.HttpContext.Connection.RemoteIpAddress = …`
(fact 7). AC9 sets `FORWARDEDHEADERS_ENABLED` with `UseSetting` on the factory's builder. Blocks in endpoint tests use
a `FakeTimeProvider` as the 503 test does.

## Options considered

- ASP.NET Core's rate limiter, the per-host-only bound, a global log cap, `/64` hosts, trusted proxies and logging
  the address: record 0082 and the spec's *Challenge*.
- Reaching the TCP peer for AC9 by our own startup filter inserted ahead of ASP.NET Core's — it rests on the order in
  which startup filters are registered, an internal of the host; reading `X-Original-For` — a client sets it itself when
  the middleware does not run. Both lost to the `PostConfigure`, which no other code is affected by (fact 4).
- AC8 by the keep-alive alone — fact 9 is unmeasured and a blocked send has no bound today (fact 3); lost to the send
  bound, which fails closed and runs on the injected clock (record 0062, option 6).
- The block end logged at the host's next contact instead of a timer — a host that goes quiet would never get the line
  AC5 asks for; lost.
- `IngestSession` taking the host and `IngestHosts` as two more parameters — 9 parameters, S107; lost to `IngestAdmission`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `IngestHosts.cs` (new) | the hosts, their limits, blocks and log lines |
| Censio.Server | `IngestHost.cs` (new) | one host's entry, members internal |
| Censio.Server | `IngestAdmission.cs` (new) | one connection's places and outcome report |
| Censio.Server | `IngestEndpoint.cs` | `429`, admission, registration of `IngestHosts`, forwarded-headers post-configuration |
| Censio.Server | `IngestSession.cs` | constructor takes `IngestAdmission`; outcome reports; `SendTimeout` |

`IngestConnections.cs` and `Program.cs` stay unchanged.

## Signatures (for the Dev's skeleton)

Each line: the member in backticks, then its declaration in words.

`IngestHosts.cs` holds the new class `IngestHosts`, public sealed partial, implementing IDisposable, with:

- `IngestHosts.MaxPendingHellosPerHost` — public const int, 4
- `IngestHosts.MaxFailedHellos` — public const int, 10
- `IngestHosts.MaxTrackedHosts` — public const int, 1024
- `IngestHosts.FailureWindow` — public static TimeSpan property, 60 seconds
- `IngestHosts.BlockDuration` — public static TimeSpan property, 60 seconds
- `public IngestHosts(TimeProvider timeProvider, ILogger logger)`
- `public IngestHost? TryEnterPendingHello(IPAddress? address)`
- `public void LeavePendingHello(IngestHost host)`
- `public void RecordFailedHello(IngestHost host)`
- `public void RecordAcceptedHello(IngestHost host)`
- `public void Dispose()`

`IngestHost.cs` holds the new class `IngestHost`, public sealed, its constructor and state internal.

`IngestAdmission.cs` holds the new class `IngestAdmission`, public sealed, with:

- `public IngestAdmission(IngestConnections connections, IngestHosts hosts, IngestHost host)`
- `public void LeavePendingHello()`
- `public bool TryTakeCompanion()`
- `public void ReleaseCompanion()`
- `public void RecordFailedHello()`
- `public void RecordAcceptedHello()`

`IngestSession.cs` changes:

- `public IngestSession(WebSocket webSocket, IngestAccess access, IngestAdmission admission, ServerState state, TimeProvider timeProvider, ILogger logger, string serverVersion)` — replaces the constructor whose third parameter is the connection counters
- `IngestSession.SendTimeout` — public static TimeSpan property, 30 seconds

IngestEndpoint.cs keeps its signatures.

The names were checked against *Writing code* in the stack file: no nested type, at most 7 parameters, no enum, no
CA1716 keyword, and no word CodeQL counts as sensitive (*Logger arguments*).

## Test files

- `tests/Censio.Server.Tests/IngestHostsTests.cs` (new)
- `tests/Censio.Server.Tests/IngestEndpointTests.cs`
- `tests/Censio.Server.Tests/IngestSessionTests.cs`

`IngestAdmission` is delegation only and is covered through the session and endpoint tests; the *Coverage gate* decides
whether the Tester adds `IngestAdmissionTests.cs` in its coverage step.

Existing test call sites of a changed signature:

- `IngestSessionTests.StartSession` (`IngestSessionTests.cs:1844`) constructs `IngestSession` with `_connections`: the
  **Dev** adapts it in step 4 (an `IngestAdmission` over `_connections`, a test-owned `IngestHosts` and the host it
  returns for a `null` address). The Tester's AC4 tests use that `IngestHosts`.
- `IngestEndpointSeventeenthPendingConnectionReturns503`: 16 connections with no address are now one host, refused `429`
  at the 5th; the **Tester** rewrites it in step 5 with 4 hosts of 4 connections (not deleted).
- `IngestEndpointAcceptFailureLeavesPendingPlace`: the **Tester** adds that the host's 4 places are free again.
- The other endpoint and session tests open one accepted connection at a time or fewer than 10 failed hellos per test
  instance; their inputs are handled as before.

Files no unit test can cover: none.

## Areas

- `docs/areas/ingest.md`: the limits per host and the send bound (*Documentation updates*).
- `docs/areas/server.md`: `ASPNETCORE_FORWARDEDHEADERS_ENABLED` affects the scheme only.

## Documentation updates

All by the Dev, once the code is green, against the code as built:

- `docs/areas/ingest.md`:
  - *Scope*: add the limits per host.
  - *Route and HTTP refusals*: the two `429` cases and the order of spec E4.
  - New subsection *Limits per host* after *One Companion and silent connections*: what a host is, failed hellos, the
    sliding window, the block and its lines, the tracked hosts with the shared and the protected entry, and that
    forwarded headers never change the address.
  - *One Companion and silent connections*: the send bound of 30 seconds beside the keep-alive.
  - *Closes*: a send not finished within 30 seconds aborts the connection without a close frame.
  - *Bounds and times*: rows for 4 per host, 10 within 60 s, the 60-second block, 1,024 hosts and the 30-second send.
  - *Log lines*: the two block lines.
  - *Accepted risks*: rewrite *Lock-out through the 16 places* and *Log volume* (many hosts or IPv6 addresses, a proxy
    as one host, a flood refusing a Companion on a new address, rotation with `--log-opt max-size`).
  - *Measurements*: the three measurements of *Approach*.
  - *Related decisions*: 0082. *Not here*: `IngestHosts`, `IngestHost` and `IngestAdmission` beside the code types.
- `docs/areas/server.md`, *HTTP surface*: `ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true` takes the scheme from
  `X-Forwarded-Proto` only; the client address stays the TCP peer's (record 0082).
- `docs/ARCHITECTURE.md` line 91: "the 16 places can lock the Companion out, and there is no rate limit" becomes the
  limits per host and the remaining risks; records "0061 to 0064, 0082".
- `SECURITY.md` line 36: the sentence beginning "Without a rate limit" becomes the limits per host, what remains
  (many hosts, a proxy as one host) and Docker log rotation.
- `docs/USER_GUIDE.md` line 162 (*Limits*, English) and line 625 (*Einschränkungen*, German): the same, in user terms,
  with `--log-opt max-size` as the rotation example.

`README.md` needs no change: no new setting. The PR template's checklist asks for nothing else for this change.

## Architecture check

The guarantees of `.squad/project.md` stay: the token still decides who stores data (the limits only refuse earlier),
plain HTTP and open pages are unchanged. Network exposure narrows (more refusals); the server trusts less of a
client's request than before (no `X-Forwarded-For`). The `docs/ARCHITECTURE.md` update links record 0082.

## Security considerations

- [x] No limits on parsed input: the address is no parsed input; the values are counts and times.
- [x] Exceptions reaching a user-visible text: none; the refusals are bare statuses, and a send cancelled by its bound
  ends in the session's existing `catch`.
- A guard over forms an attacker chooses: the address forms are IPv4, IPv4-mapped IPv6 (mapped), IPv6 with a scope id
  (dropped), IPv6 (full address) and none (`null`); `X-Forwarded-For` (stripped by the post-configuration, AC9) and
  `Forwarded` (not processed by .NET, fact 6) cannot change it.
- Memory: at most 1,024 table entries plus the shared and the protected one, each with at most 10 times and one timer.

## Decision records

- `docs/decisions/0082-ingest-limits-connections-per-host-and-blocks-failing-hosts.md` (Proposed, new, indexed).
- `docs/decisions/0064-ingest-refuses-browser-upgrades-and-bounds-pending-hellos.md` (unreleased, edited in place: its
  open rate limit now points to 0082).
- `docs/decisions/0062-one-companion-second-refused-silence-found-by-websocket-pings.md` (unreleased, edited in place:
  option 6, the send bound, is the same decision of how a connection that stops taking part loses its place).

## Out of scope / follow-ups

The spec's *Out of scope*. No follow-up issue is opened now; trusted proxies become one only on a user's request.
