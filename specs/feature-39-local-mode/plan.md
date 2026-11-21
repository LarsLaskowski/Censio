# Plan: Local mode — the Companion hosts the server in its own process

Source: Issue #39 | [spec.md](spec.md)
Tier: security — the change adds a listener and a host filter (network exposure), changes the release check (build trust) and hands the ingest token to a new loopback client.
Security areas touched: Network exposure (the Companion's new loopback listener, host filtering, `Program.cs` set-up moved into `CensioServer`); Ingest token (`IngestTokenSetup.ReadTokenFile`, the token handed to the local replay); Server database and data-protection key ring (a data directory under `%LOCALAPPDATA%\Censio\Server`); Parsing of external input (`CompanionSettings` new keys); File paths the Companion writes (`CompanionFolders.Server`); Logging (the server's lines in the Companion log); Outbound calls (the replay's WebSocket to `127.0.0.1`); Release publishing (`.github/scripts/release-check.sh`, `release-check-test.sh`); build configuration (`src/Censio.Companion/Censio.Companion.csproj` publish target, `src/Censio.Companion.Core/Censio.Companion.Core.csproj` reference).

## Problem / root cause

See [spec.md](spec.md) (*Problem*); the Product Manager chose option B: the server is hosted, the pages fill from a
replay, live data is #40.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The live Companion sends nothing; only a replay with `--server` sends. | `src/Censio.Companion/App.xaml.cs` `StartConnection` wires no sender, `StartReplay` does; `README.md` line 21 | confirmed |
| 2 | Referencing `Censio.Server` from the WPF Companion bundles ASP.NET Core and the server's assemblies into the single-file exe, but no web asset and no endpoints manifest, and leaves `appsettings.json` beside the exe. | Lead's probe in a scratch worktree: the release check's `dotnet publish` with the reference; `grep -a` on the exe found `Censio.Server.dll` and Kestrel, not `blazor.web` or `staticwebassets`; the publish folder held `appsettings.json` | confirmed |
| 3 | A target `AfterTargets="ComputeResolvedFilesToPublishList"` that publishes `Censio.Server` (RID `win-x64`, framework-dependent) to an absolute folder under the Companion's `obj/` and adds its `wwwroot/**` (as `wwwroot/<path>`) and `Censio.Server.staticwebassets.endpoints.json` to `ResolvedFileToPublish`, and removes `appsettings.json` from it, gives one exe that holds `wwwroot/_framework/blazor.web.js`, MudBlazor's files, `wwwroot/css`, `wwwroot/js` and the endpoints manifest (4.8 MB of assets). The inner `PublishDir` must be absolute: a relative one resolves against the server's project. | Lead's second probe (same command): exit 0, publish folder holds only `Censio.Companion.exe`, `grep -a` finds each path | confirmed |
| 4 | The server's set-up runs key ring, token, database, state restore, then `RunAsync`. | `src/Censio.Server/Program.cs` lines 31–73 | confirmed |
| 5 | `IngestAccess` keeps only the token's hash, so the local replay needs the token from the file the server wrote. | `src/Censio.Server/IngestAccess.cs`; `IngestTokenSetup.ReadToken` (`ReadBounded`, `ParseToken`) | confirmed |
| 6 | The build output of Server.Tests holds `Censio.Server.staticwebassets.runtime.json` and `.endpoints.json`, so a local host started there with `UseStaticWebAssets()` and the test's base folder as content root serves `_framework/blazor.web.js`. | to verify: the Tester's AC2 test `CensioServerStartLocalServesBlazorScript` in step 5 (basis: Lead's build of `tests/Censio.Server.Tests` in the scratch worktree lists both manifests) | unverified |
| 7 | With `IncludeAllContentForSelfExtract`, `AppContext.BaseDirectory` is the extraction folder, so `MapStaticAssets` finds the bundled endpoints manifest and `wwwroot` there when the host's `ApplicationName` is `Censio.Server` and its content root `AppContext.BaseDirectory`. | to verify: AC7, the Product Manager's run of the CI dry-run ZIP on Windows 11 (pages styled, `_framework/blazor.web.js` 200) (basis: .NET single-file documentation; `MapStaticAssets` reads `<ApplicationName>.staticwebassets.endpoints.json`) | unverified |
| 8 | Kestrel reports a taken address as `AddressInUseException` (an `IOException`) from `StartAsync`, and an explicit endpoint that fails for another reason fails the whole start. | to verify: the Tester's AC4 tests `CensioServerStartLocalWithPortTakenOnIPv4ReturnsPortInUse` and `...OnIPv6...` in step 5 (basis: Kestrel source (`AddressBinder`, socket transport)) | unverified |
| 9 | On Windows a program bound to `0.0.0.0:<port>` makes a bind of `127.0.0.1:<port>` fail rather than share the port. | to verify: AC7 manual case "port taken by a standalone server on all interfaces", Product Manager (basis: not readable here (Windows socket sharing rules)) | unverified |
| 10 | Host filtering accepts `[::1]` in `AllowedHosts` for a request `Host: [::1]:<port>`. | to verify: the Tester's AC6 test `CensioServerStartLocalAcceptsLoopbackHosts` in step 5 (basis: ASP.NET Core `HostString.MatchesAny`) | unverified |
| 11 | WebApplicationFactory of Program keeps working when `Program.cs` calls `CensioServer.RunAsync(args)`: it intercepts the host built during the entry point. | to verify: the existing `Censio.Server.Tests` pass after the skeleton (Dev, step 4) (basis: `Microsoft.AspNetCore.Mvc.Testing` `HostFactoryResolver` listens to host building) | unverified |
| 12 | A loopback bind does not raise the Windows Defender Firewall prompt. | to verify: AC7, Product Manager (basis: Windows documents prompts for listeners on non-loopback addresses) | unverified |
| 13 | The issue's dependencies #13 and #32 are closed; records 0015 and 0066 already decide local mode and the database under local app data. | `gh api` issue reads; the records | confirmed |
| 14 | The Companion log sanitizes and cuts every message, also of foreign categories. | `LogFileLogger.Log` → `LogLine.Format` → `Sanitize` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionSettingsReadLocalServerWithoutFileReturnsOff`, `CompanionSettingsReadLocalServerWithModeLocalReturnsDefaultPort`, `CompanionSettingsReadLocalServerWithModeInAnyCaseReturnsLocal` (`local`, `LOCAL`, `Local`), `CompanionSettingsReadLocalServerWithBlankModeReturnsOff` (`""`, `"  "`), `CompanionSettingsReadLocalServerWithLocalPortReturnsThatPort` (1, 5117, 65535), `CompanionSettingsReadLocalServerWithInvalidModeReturnsInvalid` (`forward`, `remote`, 1, `null`, `true`, `{}`, `[]`), `CompanionSettingsReadLocalServerWithInvalidLocalPortReturnsInvalid` (`5117.0`, `5.117e3`, `"5117"`, `null`, 0, 65536, -1, `true`), `CompanionSettingsReadLocalServerWithDuplicateKeyReturnsInvalid` (`mode` twice, `mode`/`Mode`, `localPort`/`LOCALPORT` with mode local), `CompanionSettingsReadLocalServerWithoutModeIgnoresInvalidLocalPort`, `CompanionSettingsReadLocalServerWithInvalidFileReturnsOff` (not JSON, array root, over 1 MiB), `CompanionSettingsReadWithInvalidLocalServerKeysKeepsGameFolder`, `CompanionSettingsReadLocalServerWithInvalidGameFolderKeepsLocal`; `LocalServerSessionRunWithInvalidSettingStartsNoServerAndLogsWarning`; `LocalServerSessionIsHostedPerModeAndSetting` | `tests/Censio.Companion.Core.Tests/CompanionSettingsTests.cs`, `LocalServerSessionTests.cs` |
| AC2 | `CensioServerLocalEndpointsReturnsBothLoopbackAddresses`, `CensioServerLocalEndpointsWithoutIPv6ReturnsIPv4Only`, `CensioServerStartLocalServesStartPageWithVersion`, `CensioServerStartLocalServesBlazorScript`, `CensioServerStartLocalCreatesDataDirectoryWithDatabaseKeyRingAndToken`, `CensioServerStartLocalExposesTokenOfTokenFile`, `CensioServerStartLocalIgnoresEnvironmentSettings` (`CENSIO__INGEST__REQUIRETOKEN=false`: a hello without token is refused; `ASPNETCORE_URLS` on another port: not listened on); `IngestTokenSetupReadTokenFileReturnsToken`, `IngestTokenSetupReadTokenFileWithInvalidFileReturnsNull`; `LocalServerSessionRunShowsStartingThenRunning`, `LocalServerSessionRunPassesSettingPort`; `TrayMenuFormatLocalServerReturnsTextPerState` (each state, port 5000 and 5117), `TrayMenuCreateInLocalModeShowsServerLineBelowCatalog`, `TrayMenuCreateWithoutLocalModeShowsNoServerEntries`; `CompanionFoldersServerReturnsServerFolderUnderLocalAppData` | `tests/Censio.Server.Tests/CensioServerTests.cs`, `IngestTokenSetupTests.cs`; `tests/Censio.Companion.Core.Tests/LocalServerSessionTests.cs`, `TrayMenuTests.cs`, `CompanionFoldersTests.cs` |
| AC3 | `TrayMenuCreateInLocalModeShowsOpenCensioAboveDiagnostics`, `TrayMenuCreateEnablesOpenCensioOnlyWhileRunning` (each state); `LocalServerSessionAddressIsLocalhostWithPort`; `BrowserOpenerOpenLaunchesAddress`, `BrowserOpenerOpenWhenLaunchFailsLogsWarning` | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `LocalServerSessionTests.cs`, `BrowserOpenerTests.cs` |
| AC4 | `CensioServerStartLocalWithPortTakenOnIPv4ReturnsPortInUse`, `CensioServerStartLocalWithPortTakenOnIPv6ReturnsPortInUse` (runs where the system supports IPv6, else asserts the IPv4-only endpoints), `CensioServerStartLocalWithUnwritableDataDirectoryReturnsFailed` (a file in place of the folder), `CensioServerStartLocalWithNewerDatabaseReturnsFailed`, `CensioServerStartLocalPassesOnlyWarningsAndAboveToLogProvider` (the `Critical` start line arrives, an `Information` line does not); `LocalServerSessionRunWhenPortInUseShowsPortInUseAndLogs`, `LocalServerSessionRunWhenStartFailsShowsFailed`, `LocalServerSessionRunWhenStartThrowsShowsFailedAndLogs`, `LocalServerSessionRunWhenServerStopsOnItsOwnShowsFailed` | `tests/Censio.Server.Tests/CensioServerTests.cs`; `tests/Censio.Companion.Core.Tests/LocalServerSessionTests.cs` |
| AC5 | `LocalServerSessionStopAsyncStopsRunningServer`, `LocalServerSessionStopAsyncReturnsAfterTimeoutWhenServerHangs` (`FakeTimeProvider`), `LocalServerSessionStopAsyncWhileStartingCancelsStartAndStopsLateServer`; `CensioServerStartLocalKeepsStoredProfileAfterRestart`, `CensioServerStopLocalLeavesLogProviderUsable` | `tests/Censio.Companion.Core.Tests/LocalServerSessionTests.cs`; `tests/Censio.Server.Tests/CensioServerTests.cs` |
| AC6 | `CensioServerStartLocalRefusesForeignHostWith400` (`example.com`, `localhost.`, `foo.localhost`, `127.0.0.2`, `[::2]`), `CensioServerStartLocalAcceptsLoopbackHosts` (`localhost`, `LOCALHOST:5000`, `127.0.0.1`, `[::1]`) | `tests/Censio.Server.Tests/CensioServerTests.cs` |
| AC7 | manual: the Product Manager, on Windows 11 x64 without .NET, with the ZIP of the PR's CI artifact `companion-dry-run`: S1, S2, S5 (fixture `long-session.jsonl`), S6 (Exit under 5 s, pages again after restart), E1 (a standalone `Censio.Server` on all interfaces at 5000), no firewall prompt, styled pages | — |
| AC8 | manual: the Reviewer reads `README.md` and `docs/USER_GUIDE.md` (English and German) against AC8 in step 8 | — |
| AC9 | `ReplaySenderForLocalServerSendsHelloWithTokenOfLocalServer`, `ReplaySenderForLocalServerWaitsForTokenBeforeConnecting`, `ReplaySenderForLocalServerWhenServerNotRunningReturnsLocalServerNotRunning`; `ReplaySenderFactoryCreateWithServerAddressReturnsRemoteSender`, `ReplaySenderFactoryCreateWithoutServerInLocalModeReturnsLocalSender`, `ReplaySenderFactoryCreateWithoutServerOutsideLocalModeReturnsNull`; `LocalServerSessionWaitForTokenAsyncReturnsTokenOnceRunning`, `LocalServerSessionWaitForTokenAsyncReturnsNullWhenStartFails`; `TrayMenuFormatReplaySendReasonLocalServerNotRunningReturnsText`; `PipeReplayRunnerRunSendingToLocalServerShowsConnectingUntilServerRuns`; `CensioServerStartLocalFillsProfileFromLocalReplay` | `tests/Censio.Companion.Core.Tests/ReplaySenderTests.cs`, `ReplaySenderFactoryTests.cs`, `LocalServerSessionTests.cs`, `TrayMenuTests.cs`, `PipeReplayRunnerTests.cs`; `tests/Censio.Server.Tests/CensioServerTests.cs` |
| AC10 | CI: job `release-check` (`.github/scripts/release-check-test.sh`): the real dry-run publish passes `check-companion`; an exe without `wwwroot/_framework/blazor.web.js` fails with the text of *Approach* | — |

## Approach

**Server (`Censio.Server`).** Move the body of `Program.cs` into `CensioServer`; `Program.cs` keeps one line,
`return await CensioServer.RunAsync(args).ConfigureAwait(false);`. Both entry points share one private set-up (key
ring, token, database, state, retention, the middleware and maps in today's order), so start checks and log lines
stay identical. `StartLocalAsync`:
- builds the host with `WebApplicationOptions` `ApplicationName` `Censio.Server`, `EnvironmentName` `Production`,
  `ContentRootPath` `options.ContentRoot`, no args; clears `builder.Configuration.Sources` and adds in memory only
  `Censio:DataDirectory` = `options.DataDirectory`, `AllowedHosts` = `localhost;127.0.0.1;[::1]` (record 0100);
- listens on `LocalEndpoints(port, listenOnIPv6)` through `ConfigureKestrel` (explicit `Listen` calls; no URLs);
- replaces the logging providers by a wrapper over `options.LogProvider` that never disposes it, minimum level
  `Warning`;
- sets `HostOptions.ShutdownTimeout` to 3 seconds; calls `UseStaticWebAssets()` as the standalone server does;
- runs the shared set-up with `cancellationToken`; a failed check returns `Failed` (its `Critical` line is already
  logged); `StartAsync` throwing with an `AddressInUseException` in its chain returns `PortInUse`, any other exception
  `Failed` with the exception logged by the session;
- once started, reads the token with `IngestTokenSetup.ReadTokenFile(<data directory>/ingest-token)`; no token →
  stops the host and returns `Failed`.
The returned `ILocalServer` (internal class `LocalServerHost`): `Stopped` completes when the host's lifetime ends;
`StopAsync` stops the host and then disposes the retention, the database and the app, in that order.
`IngestTokenSetup.ReadTokenFile` reuses `ReadBounded` and `ParseToken`; no log line, the token is never logged.

**Companion.Core.** It references `Censio.Server` (record 0098).
- `CompanionSettings.ReadLocalServer` shares `ReadBytes` and one private JSON parse with `Read` (one shared helper,
  not a copy). The `mode` and `localPort` rules are E2 of the spec: a file-level failure → `Off`. Then `mode` is
  missing or blank → `Off`. `mode` is not a string, is duplicated, or is a value other than `local` → `Invalid`. With
  `local`, `localPort` must be a `JsonValueKind.Number` whose raw text is decimal digits only, optionally with a
  leading `-`, in 1–65535, so `5117.0` and `5.117e3` are refused; anything else, or a duplicate, → `Invalid`. Problem texts
  (logged): `mode is not local`, `mode occurs more than once`, `localPort is not a port from 1 to 65535`,
  `localPort occurs more than once`.
- `LocalServerSession` runs the state machine `Starting` → `Running` | `PortInUse` | `Failed`; `SettingsInvalid`
  from the start for an invalid setting (no start, warning logged). `Running` → `Failed` when `Stopped` completes
  without `StopAsync`. `StatusChanged` is raised on every change. `WaitForTokenAsync` completes with the token
  on `Running` and with `null` on any end state. `StopAsync(timeout)` cancels a start in progress, stops a running
  server (also one whose start finishes later) and returns after the timeout at most, on the injected `TimeProvider`.
  `IsHosted(mode, setting)`: `setting.Kind != Off && mode != Invalid`.
- `TrayMenu`: `FormatLocalServer` texts exactly as spec E1, E2, E4, S1 (`Server: starting`,
  `Server: running at http://localhost:<port>/`, `Server: port <port> in use`, `Server: failed`,
  `Server: settings invalid`, port in invariant digits). `Create` inserts, when `localServer` is not null, the greyed
  `LocalServer` line directly below `Catalog:`, and `Open Censio` (enabled only in `Running`) directly above
  `Open diagnostics` after the first separator. `null` gives today's menu. New `TrayMenuEntryKind` members are appended.
  The new reason `local server not running` is appended to `ReplaySendProblemKind` and `FormatReplaySendReason`.
- `ReplaySender.ForLocalServer`: same run as today. `Prepare` does nothing; `ConnectServerAsync` first awaits
  `waitForToken` (inside the `Connecting` status), `null` → `LocalServerNotRunning` with today's log line
  `Replay sending failed: local server not running`; then connects to `ws://127.0.0.1:<port>/ingest` with the token
  in the hello. `ReplaySenderFactory.Create` picks: `--server` given → today's sender; else a session → local; else
  `null` (offline replay).
- `BrowserOpener` mirrors `FolderOpener`: launch the `Uri`, a failure → warning `CompanionLog.BrowserOpenFailed`.
- `CompanionLog` new lines: `Local server settings invalid: <problem>` (warning), `Local server running on port
  <port>` (information), `Local server port <port> in use` (warning), `Local server failed` with the exception
  (error), `Local server stopped unexpectedly` (error), `Could not open the browser` with the exception (warning).

**Shell (`Censio.Companion`, no tests).** `OnStartup` reads `CompanionSettings.ReadLocalServer`. When `IsHosted`, it
creates the session, with the start delegate binding `LocalServerOptions`: `ContentRoot` = `AppContext.BaseDirectory`,
`DataDirectory` = `CompanionFolders.Server`, `LogProvider` = `_logProvider`, `ListenOnIPv6` = `Socket.OSSupportsIPv6`.
It then runs the session with `Run` (never on the UI thread). `StatusChanged` notifies the tray updater.
`StartReplay` uses `ReplaySenderFactory.Create`. `TrayIcon` passes the session's status and dispatches `OpenCensio`
to `BrowserOpener` through `Run`, with launcher `Process.Start(new ProcessStartInfo(uri.AbsoluteUri) {
UseShellExecute = true })`; the URI is built from the port alone. `Dispose` stops the session first,
`Task.Run(() => session.StopAsync(3 s)).Wait(3 s)`, before the recording (1 s) and the log flush (0.5 s): 4.5 s at
most. Without a session nothing changes (2 s).

**Build.** `Censio.Companion.csproj` gets the publish target of fact 3, for every publish of the Companion. The
`companion` check of `release-check.sh` also fails when the exe lacks a required asset path: the required paths are
`Censio.Server.staticwebassets.endpoints.json`, `wwwroot/_framework/blazor.web.js`,
`wwwroot/_content/MudBlazor/MudBlazor.min.css`, `wwwroot/_content/MudBlazor/MudBlazor.min.js`, and `wwwroot/<path>`
for each file of `git ls-files src/Censio.Server/wwwroot`. The failure text is `Censio.Companion.exe does not bundle
<path>.`, in the style of the existing errors.

## Options considered

The hosting, switch, exposure and replay choices with their alternatives are in decision records 0098, 0099, 0100 and 0101
(child process, embedded resources, Web SDK Companion, environment-read configuration; command-line switch, on by
default; `ListenLocalhost`, all interfaces, no host filter; `--server` with a wait, `--local`). Plan-level:
- **Core references Server** (chosen) against **the session in the shell with its own types** (no tests for the state
  machine and the menu, against AGENTS.md) and **the session in `Censio.Server`** (the tray texts and replay sender would
  leave the Companion).
- **Re-reading the token file** (chosen) against **exposing the token on `IngestAccess`** (would keep the plain
  token in every server's memory and change a security type the ingest uses).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `Program.cs`, `CensioServer` (new), `LocalServerOptions`, `LocalServerStart`, `LocalServerStartKind`, `ILocalServer`, `LocalServerHost` (new, internal), `IngestTokenSetup` | as *Approach* |
| Censio.Companion.Core | `Censio.Companion.Core.csproj` (reference), `CompanionSettings`, `LocalServerSetting`, `LocalServerSettingKind`, `LocalServerSession`, `LocalServerState`, `LocalServerStatus`, `BrowserOpener`, `ReplaySender`, `ReplaySenderFactory`, `ReplaySendProblemKind`, `TrayMenu`, `TrayMenuEntryKind`, `CompanionFolders`, `CompanionLog` | as *Approach* |
| Censio.Companion | `App.xaml.cs`, `TrayIcon.cs`, `Censio.Companion.csproj` | wiring, menu, publish target |
| CI | `.github/scripts/release-check.sh`, `.github/scripts/release-check-test.sh` | AC10 |

## Signatures (for the Dev's skeleton)

Censio.Server:
- `public static class CensioServer`
- `public static Task<int> RunAsync(string[] args)`
- `public static Task<LocalServerStart> StartLocalAsync(LocalServerOptions options, CancellationToken cancellationToken)`
- `public static IReadOnlyList<IPEndPoint> LocalEndpoints(int port, bool listenOnIPv6)`
- `public class LocalServerOptions` (sealed)
- `LocalServerOptions.Port` (int, required, init)
- `LocalServerOptions.DataDirectory` (string, required, init)
- `LocalServerOptions.ContentRoot` (string, required, init)
- `LocalServerOptions.LogProvider` (ILoggerProvider, required, init)
- `LocalServerOptions.ListenOnIPv6` (bool, init)
- `public enum LocalServerStartKind { Running, PortInUse, Failed }`
- `public class LocalServerStart` (sealed)
- `LocalServerStart.Kind` (LocalServerStartKind, required, init)
- `LocalServerStart.Server` (ILocalServer?, init)
- `LocalServerStart.Exception` (Exception?, init)
- `public interface ILocalServer : IAsyncDisposable`
- `ILocalServer.Port` (int, get)
- `ILocalServer.Token` (string, get)
- `ILocalServer.Stopped` (Task, get)
- `ILocalServer.StopAsync(CancellationToken cancellationToken)` returning Task
- `public static string? ReadTokenFile(string tokenFile)` (on IngestTokenSetup)

Censio.Companion.Core:
- `CompanionSettings.ModeKey = "mode"` (public const string)
- `CompanionSettings.LocalPortKey = "localPort"` (public const string)
- `CompanionSettings.DefaultLocalPort = 5000` (public const int)
- `public static LocalServerSetting ReadLocalServer(string path)`
- `public enum LocalServerSettingKind { Off, Local, Invalid }`
- `public record LocalServerSetting` (sealed)
- `LocalServerSetting.Kind` (LocalServerSettingKind, required, init)
- `LocalServerSetting.Port` (int, init)
- `LocalServerSetting.Problem` (string?, init)
- `public enum LocalServerState { Starting, Running, PortInUse, Failed, SettingsInvalid }`
- `public readonly record struct LocalServerStatus`
- `LocalServerStatus.State` (LocalServerState, required, init) and `LocalServerStatus.Port` (int, required, init)
- `public class LocalServerSession : IAsyncDisposable` (sealed)
- `public LocalServerSession(LocalServerSetting setting, Func<int, CancellationToken, Task<LocalServerStart>> start, TimeProvider timeProvider, ILogger logger)`
- `LocalServerSession.Status` (LocalServerStatus, get)
- `LocalServerSession.Address` (Uri, get: http://localhost:<port>/)
- `LocalServerSession.StatusChanged` (event EventHandler of LocalServerStatus)
- `public Task RunAsync(CancellationToken cancellationToken)` (on LocalServerSession)
- `public Task<string?> WaitForTokenAsync(CancellationToken cancellationToken)`
- `public Task StopAsync(TimeSpan timeout)` (on LocalServerSession)
- `public static bool IsHosted(CompanionMode mode, LocalServerSetting setting)`
- `public class BrowserOpener` (sealed)
- `public BrowserOpener(Action<Uri> launch, ILogger logger)`
- `public void Open(Uri address)`
- `public static ReplaySender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`
- `public static class ReplaySenderFactory`
- `public static ReplaySender? Create(CompanionCommandLine commandLine, LocalServerSession? localServer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)`
- `ReplaySendProblemKind.LocalServerNotRunning` (appended member)
- `TrayMenuEntryKind.LocalServer` (appended member)
- `TrayMenuEntryKind.OpenCensio` (appended member)
- `TrayMenu.OpenCensioText = "Open Censio"` (public const string)
- `public static string FormatLocalServer(LocalServerStatus status)`
- `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, GameInstallationStatus game, GameCatalogStatus catalog, LocalServerStatus? localServer, bool isAutostartEnabled, TimeProvider clock)` (TrayMenu, changed: seven parameters)
- `public static string Server(string localApplicationData)` (on CompanionFolders)
- `CompanionLog.LocalServerSettingsInvalid(ILogger logger, string problem)` (static partial void)
- `CompanionLog.LocalServerRunning(ILogger logger, int port)` (static partial void)
- `CompanionLog.LocalServerPortInUse(ILogger logger, int port)` (static partial void)
- `CompanionLog.LocalServerFailed(ILogger logger, Exception? exception)` (static partial void)
- `CompanionLog.LocalServerStoppedUnexpectedly(ILogger logger)` (static partial void)
- `CompanionLog.BrowserOpenFailed(ILogger logger, Exception exception)` (static partial void)

The six log methods are LoggerMessage methods of CompanionLog with the texts and levels of *Approach*.
ReplaySender's private constructor gains the wait delegate.

## Test files

New: `tests/Censio.Server.Tests/CensioServerTests.cs`, `tests/Censio.Companion.Core.Tests/LocalServerSessionTests.cs`,
`BrowserOpenerTests.cs`, `ReplaySenderFactoryTests.cs`, the double `FakeLocalServer.cs` (an `ILocalServer` with
held start, held stop and a `Stopped` the test completes). Extended: `CompanionSettingsTests.cs`, `TrayMenuTests.cs`,
`ReplaySenderTests.cs`, `PipeReplayRunnerTests.cs`, `CompanionFoldersTests.cs`, `IngestTokenSetupTests.cs`.
The Server.Tests local-host tests bind real loopback ports (find a free one first, retry once on a taken port) and
use a temporary data directory each; a test that sets an environment variable restores it in `finally`.

Existing call sites of a changed signature: the 15 `TrayMenu.Create` calls in `TrayMenuTests.cs` get `null` for
`localServer` — Tester. `TrayIcon.cs` — Dev. No input of a kept test is handled differently: without local mode
everything stays as it is (S7).

Code a unit test cannot cover: `src/Censio.Companion/**` (wiring, menu dispatch, `Process.Start`), excluded already
(record 0004); `Censio.Companion.csproj` and the release check (CI, AC10).

## Areas

- New `docs/areas/local-mode.md` (+1,200 words) — scope for the index: "Local mode: the Companion's settings `mode` and
  `localPort`, the hosted server's addresses and host rule, data folder, menu line and `Open Censio`, start, stop and
  failures with their texts, what is logged, the accepted risk of other users of the PC, the target host" — not
  here: the server's own behavior (server, server-data, ingest-token), the replay (replay-sending).
- `docs/areas/companion.md` (3,389 words, +150): menu order with the two items, *Settings file* keys pointing to
  local-mode, `Exit` within 5 s in local mode, *Platform* the larger exe.
- `docs/areas/replay-sending.md` (2,655 words, +200): the replay without `--server` in local mode, its wait and reason, the
  log line's new reason.
- `docs/areas/server.md` (2,133 words, +60): *Listen address* and *Data directory*: also hosted by the Companion,
  link local-mode.
- `docs/areas/companion-log.md` (1,232 words, +80): the local-server lines and the server's warning-and-above lines.

## Documentation updates

All by the Dev once the code is green, each English then German where the file has both:
- `README.md`: *What works* (local mode, the pages fill from a replay), the Companion section (the two keys with an
  example file, the menu list with `Server:` and `Open Censio`, the replay without `--server`, Windows 11 only for
  local mode), the accepted risk N9, the Companion's settings in the configuration part.
- `docs/USER_GUIDE.md`: a section "Local mode" with the steps (write `settings.json`, start, `Open Censio`, replay
  `long-session.jsonl`, `Exit`, start again), the menu texts and failures, the data folder, N9; the menu list; the
  tooltip/menu table where it lists entries.
- `SECURITY.md`: the local listener (loopback, host filter, N9) and the local replay connection in the scope list
  and the deployment note.
- `docs/ARCHITECTURE.md`: *Components* and the project table (Companion.Core references Server; the Companion hosts
  the server), *Data flow*, *Configuration* (settings keys), *Security model* (loopback listener, host filter, second
  outbound call), *Deployment* (the ZIP holds the server), linking records 0098–0101 (record 0015, *Consequences*).
- `docs/areas/README.md`: the index row of `local-mode` (text of *Areas*); the five area documents of *Areas*.
- `.squad/project.md` (exact text from the Dev, written by the orchestrator): *Security areas* — Network exposure adds
  `CensioServer.StartLocalAsync`/`LocalServerHost` with [local-mode]; Outbound calls adds the local replay
  (`ReplaySender.ForLocalServer`) to `127.0.0.1`; File paths adds `CompanionFolders.Server`; Ingest token adds
  `IngestTokenSetup.ReadTokenFile`. *Integration surface* — config option: `mode`, `localPort` in `CompanionSettings`;
  service: `LocalServerSession`, `BrowserOpener`; tray: `LocalServer`, `OpenCensio`. *Test doubles* —
  `FakeLocalServer` (text from the Tester).

## Architecture check

Record 0015's consequence that the server assumes neither container nor Companion is kept: `StartLocalAsync`
passes paths, port and logging explicitly. The guarantees of `.squad/project.md` hold: plain HTTP, open pages and
`/health` (now on loopback), the token required with no switch in local mode, history and deletion unchanged, the
buffer guarantee untouched (no live sending). `Windows-only code in Censio.Companion alone` holds: `Process.Start`
and `Socket.OSSupportsIPv6` stay in the shell.

## Security considerations

- [x] Limits: the settings file keeps its 1 MiB bound on the raw bytes, before decoding; `localPort` is checked
  on the number's raw text; the token file keeps `ReadBounded`'s bound on raw bytes.
- [x] Exceptions reaching user text: none — the menu shows only the five fixed texts and the replay only `local
  server not running`; exception texts go to the log, sanitized (fact 14).
- Host filtering is on only for the local server; the standalone server's `AllowedHosts` default stays `*`.
- The token stays in process memory (`ILocalServer.Token`, the sender's hello), never logged or written anew; the
  replay connects to `127.0.0.1` only, the address built from the integer port.
- Environment variables and configuration files are not read by the hosted server (record 0098).
- Key ring unencrypted in the player's private folder, as on Windows today; DPAPI is out of scope.

## Decision records

- `docs/decisions/0098-companion-hosts-the-server-from-a-reference-with-bundled-web-assets.md` (Proposed)
- `docs/decisions/0099-local-mode-switched-on-in-the-settings-file-off-by-default.md` (Proposed)
- `docs/decisions/0100-local-server-on-loopback-only-with-host-filtering.md` (Proposed)
- `docs/decisions/0101-local-replay-sent-to-the-hosted-server-with-the-token-in-memory.md` (Proposed)

Their `Area: local-mode` fails `decision-check.py` until the Dev adds the index row in group G4; the orchestrator
expects that failure on the WIP commits before G4.

## Out of scope / follow-ups

Live data and the catalog into the local server (#40, #42); a settings window (#43); a restart of a failed server
without restarting the Companion; DPAPI for the key ring; LAN access.
