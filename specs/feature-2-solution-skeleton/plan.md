# Plan: Solution skeleton with all projects

Source: Issue #2 | [spec.md](spec.md)
Status: Draft
Tier: security — the change opens the server's first network listener with routes reachable without login (`/`, `/health`, the Blazor hub and static assets) and changes the CI workflow, both security controls in the sense of `.squad/routing.md` (*Tiers*: network exposure, trust of the build).
Security areas touched: network exposure (new Kestrel listener of `Censio.Server`, unauthenticated routes `/`, `/health`, `/_blazor`, `/_framework/*`; no HTTPS redirection, no HSTS, record 0006); CI workflow `.github/workflows/ci.yml` (step *Detect solution* removed, `sonar.coverage.exclusions` extended); build configuration (`global.json`, `Directory.Build.props`, `Directory.Packages.props`, all `.csproj`, `.gitignore`); dependencies (new NuGet packages: Reihitsu.Analyzer, SonarAnalyzer.CSharp, MSTest.TestFramework, MSTest.TestAdapter, MSTest.Analyzers, Microsoft.NET.Test.Sdk, coverlet.collector, Microsoft.AspNetCore.Mvc.Testing); `.squad/project.md` *Security areas* (one entry added). Not touched: ingest token, authentication, cache/recording paths, parsing of external input, outbound calls, logging of external data.

## Problem / root cause

Feature: see [spec.md](spec.md). There is no solution yet; this change creates it with seven production projects,
six test projects, a runnable server and a runnable Companion shell.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | A `net10.0-windows` project with `UseWPF` and `UseWindowsForms` builds on Linux with `EnableWindowsTargeting`, SDK 10.0.403 | Scratch project in the session scratchpad (`dotnet new wpf`, both flags, `dotnet build -c Release`): 0 warnings, 0 errors | confirmed |
| 2 | With `UseWPF` and `UseWindowsForms` together, implicit usings make `Application` ambiguous (CS0104); removing the `System.Windows.Forms` and `System.Drawing` global usings fixes it | Same scratch project: CS0104 in `App.xaml.cs` first; with `<Using Remove="System.Windows.Forms" />` and `<Using Remove="System.Drawing" />` the build passes | confirmed |
| 3 | The Windows shell's runtime config requires `Microsoft.WindowsDesktop.App`, so no test project referencing it can start on Linux | Scratch `Probe.runtimeconfig.json` lists `Microsoft.WindowsDesktop.App` 10.0.0; the session has only `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` (DA, spec challenge) | confirmed |
| 4 | The *Coverage gate* counts no line of a file that appears in no coverage report | `.squad/tools/coverage-check.py` `overall_coverage` (sums only `hits` entries) and `new_code_coverage` (`relevant` empty → `n/a`, adds 0) | confirmed |
| 5 | A product PR may not edit `.squad/tools/squad_settings.py` (`COVERAGE_EXCLUDES`) | `.squad/tools/scope-check.py` `SQUAD_FILES_ALLOWED = {".squad/stack.md", ".squad/project.md"}` | confirmed |
| 6 | `ci.yml` runs restore, build and test of `Censio.slnx` on `ubuntu-latest` only when the file exists, and its comment asks to remove *Detect solution* once it does | `.github/workflows/ci.yml`, step *Detect solution* and the `steps.solution.outputs.exists` conditions | confirmed |
| 7 | CodeQL analyses C# with `build-mode: none`, so the WPF shell needs no build there | `.github/workflows/codeql.yml`, matrix `csharp` / `none` | confirmed |
| 8 | The repository has no `.gitignore`; build output would show as untracked | `git ls-files`, `ls -a` at the root | confirmed |
| 9 | No `v*` tag exists, so records 0001–0006 are unreleased | `git ls-remote --tags origin`: empty | confirmed |
| 10 | The installed formatter is `reihitsu.cli` 1.1.0, so Reihitsu.Analyzer is pinned on the 1.1 line | `dotnet tool list -g` | confirmed |
| 11 | Coverlet's collector does not instrument the test assembly by default (`IncludeTestAssembly` false), so linked sources compiled into a test project are not measured | to verify: coverlet `Documentation/VSTestIntegration.md`, option `IncludeTestAssembly`; Tester, step 5 (only relevant to the rejected option) | unverified |
| 12 | Without URL configuration Kestrel listens on `http://localhost:5000` (loopback IPv4 and IPv6) and logs `Now listening on: http://localhost:5000` | to verify: Dev starts `dotnet src/Censio.Server/bin/Release/net10.0/Censio.Server.dll` in step 6 and reads the log | unverified |
| 13 | The SDK appends `+<commit>` to the informational version unless `IncludeSourceRevisionInInformationalVersion` is `false`, and a global `-p:Version=` overrides `Version` set in `Directory.Build.props` | to verify: Dev, step 6, builds with and without `-p:Version=1.2.3` and reads the start page and the AC6 tests | unverified |
| 14 | `MapHealthChecks` with no registered check answers `Healthy`, content type `text/plain` | to verify: Tester's AC5 test in step 6 (deferred: fails on the skeleton) | unverified |
| 15 | The page of a component in the interactive server render mode contains the prerender marker `<!--Blazor:{"type":"server"` and references the fingerprinted `_framework/blazor.web.<hash>.js`, which `MapStaticAssets` serves in a `WebApplicationFactory` host in `Production` | to verify: Tester, step 5, on the first green run in step 6; if the test host does not serve the script, the Tester reports it and the Lead decides (no silent switch to `Development`) | unverified |
| 16 | .NET 10 makes the top-level `Program` of a web project public, so `WebApplicationFactory<Program>` compiles from the test project | to verify: Dev, step 4 skeleton build; fallback `<InternalsVisibleTo Include="Censio.Server.Tests" />` in `Censio.Server.csproj` | unverified |
| 17 | A named `Mutex` with the prefix `Local\` works on Linux, and a second `new Mutex(true, name, out createdNew)` in the same process returns `createdNew == false` | to verify: Tester, step 5, `SingleInstanceGuardTests` on Linux | unverified |
| 18 | `Local\` scopes a named mutex to the Windows terminal-services session, so each signed-in user has their own | to verify: Windows documentation *Kernel Object Namespaces*; Product Manager's manual run (E4, if a second account is at hand) | unverified |
| 19 | Windows Forms `NotifyIcon` re-adds its icon on the `TaskbarCreated` message (Explorer restart) | to verify: Dev, step 6, reads `NotifyIcon.cs` in a shallow clone of `dotnet/winforms` (release/10.0); Product Manager's manual run (E5) | unverified |
| 20 | The issue's claim "Depends on #1" is met | Issue #1 closed; template files present | confirmed |
| 21 | The issue's claim that the release workflow can ship artifacts from M0 on | No release workflow exists yet (#5 open); not a precondition of this change | refuted |

## Acceptance criteria

The criteria are AC1–AC14 of [spec.md](spec.md).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `PipeAssemblyReferenceCarriesBuildVersion`, `GameDataAssemblyReferenceCarriesBuildVersion`, `ServerDataAssemblyReferenceCarriesBuildVersion`, `ProductVersionReadReturnsInformationalVersion`, the `ProgramTests` and `TrayMenuTests` below (one or more per test project) | `tests/Censio.Pipe.Tests/PipeAssemblyTests.cs`, `tests/Censio.GameData.Tests/GameDataAssemblyTests.cs`, `tests/Censio.Server.Data.Tests/ServerDataAssemblyTests.cs`, `tests/Censio.Contracts.Tests/ProductVersionTests.cs` |
| AC4 | `ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion`, `ProgramGetRootReferencesServedBlazorScript`, `ProgramGetRootRendersInteractiveServerMarker` | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC5 | `ProgramGetHealthReturnsHealthyPlainText` (`[DataRow("Production")]`, `[DataRow("Development")]`) | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC6 | `ProductVersionReadReturnsInformationalVersion`, `ProductVersionReadWithoutAttributeReturnsFallback`, `ProductVersionReadInformationalVersionEqualsBuildVersion`, the three `…AssemblyReferenceCarriesBuildVersion` tests, `TrayMenuCreateVersionEntryShowsVersion`, `TrayMenuCoreAssemblyCarriesBuildVersion`, `ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion` | `ProductVersionTests.cs`, the three `…AssemblyTests.cs`, `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `ProgramTests.cs` |
| AC7 | `ProgramGetOverHttpWithHttpsPortDoesNotRedirect` (`[DataRow("/")]`, `[DataRow("/health")]`), `ProgramGetOverHttpsSendsNoHstsHeader` (same rows) | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC8 | `ProgramGetUnknownPathReturnsNotFound` | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC9 | `ProgramProductionConfigurationSetsNoListenAddress` | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC10 | `TrayMenuCreateReturnsVersionSeparatorExit`, `TrayMenuCreateVersionEntryIsDisabled`, `TrayMenuCreateExitEntryIsEnabled`, `TrayMenuToolTipIsCensioCompanion` | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` |
| AC11 | `SingleInstanceGuardFirstTryAcquireReturnsTrue`, `SingleInstanceGuardSecondGuardWithSameNameReturnsFalse`, `SingleInstanceGuardAfterDisposeNameCanBeAcquiredAgain`, `SingleInstanceGuardDefaultNameIsSessionLocal` | `tests/Censio.Companion.Core.Tests/SingleInstanceGuardTests.cs` |

What each test asserts:

- `…AssemblyReferenceCarriesBuildVersion`, `TrayMenuCoreAssemblyCarriesBuildVersion`,
  `ProductVersionReadInformationalVersionEqualsBuildVersion`: `ProductVersion.Read(<assembly>)` equals the value of
  the assembly's `AssemblyMetadata("CensioBuildVersion")` (the build's `$(Version)`). This fails when the build
  appends `+<commit>` or when an assembly is not stamped from `Directory.Build.props`.
- `ProductVersionReadWithoutAttributeReturnsFallback`: an assembly from `AssemblyBuilder.DefineDynamicAssembly`
  (no attribute) gives `0.0.0`.
- `ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion`: `Production`; status 200; body contains
  the title element with the text `Censio`, the `h1` element with the text `Censio`, and `Version ` followed by
  `ProductVersion.Read(typeof(Program).Assembly)`
  (the AC6 part on the server).
- `ProgramGetRootReferencesServedBlazorScript`: the `src` of the `<script>` matching `_framework/blazor.web`
  (regex `_framework/blazor\.web(\.[A-Za-z0-9]+)?\.js`) is requested and answers 200.
- `ProgramGetRootRendersInteractiveServerMarker`: body contains `<!--Blazor:{"type":"server"` (fact 15).
- `ProgramGetOverHttpWithHttpsPortDoesNotRedirect`: host built with the setting `https_port=443` (so a present
  `UseHttpsRedirection` would redirect), base address `http://censio.example`; status is not 3xx and no
  `Location` header.
- `ProgramGetOverHttpsSendsNoHstsHeader`: `Production`, base address `https://censio.example` (not a localhost
  host, which HSTS skips); no `Strict-Transport-Security` header.
- `ProgramProductionConfigurationSetsNoListenAddress`: `IConfiguration` of the `Production` host has no value for
  `urls`, `http_ports`, `https_ports` and no section `Kestrel:Endpoints`.
- `TrayMenuCreateReturnsVersionSeparatorExit`: `TrayMenu.Create("1.2.3")` gives exactly three entries, kinds
  `Version`, `Separator`, `Exit`, texts `Version 1.2.3`, empty, `Exit`.
- `SingleInstanceGuardSecondGuardWithSameNameReturnsFalse`: two guards on one unique name (a GUID suffix per
  test, so runs never collide); the first `TryAcquire` is true, the second false, the first stays acquired.
- `SingleInstanceGuardDefaultNameIsSessionLocal`: `DefaultName` starts with `Local\` (in C#: `"Local\\"`; the
  test asserts the string contains the backslash character, so a lost escape fails).

The server tests share a helper `CensioServerFactory` (`WebApplicationFactory<Program>` with an environment and
optional settings), in `tests/Censio.Server.Tests/CensioServerFactory.cs`.

## Verification without tests

Steps 4–6 apply in full; this section only names the criteria, or the parts of them, that no unit test can prove.

- AC1 (structure), AC2: the orchestrator's step-7 gates (*Build*, *Test*, *Analyzer gate*, *Test with coverage*,
  *Coverage gate*) and the CI run on the draft PR; the Reviewer reads `Censio.slnx` for the seven production and
  six test projects and their target frameworks.
- AC3: the Reviewer reads `global.json` (`"version": "10.0.100"`, `"rollForward": "latestFeature"`,
  `"allowPrerelease": false`; `latestFeature` stays within 10.0 and refuses an older SDK, per the `global.json`
  documentation); the Dev's build with the session's 10.0.403 shows acceptance.
- AC4 (image): the Dev runs the *Image build check* reproduction of `.squad/stack.md` (no Dockerfile yet, #4) and
  confirms `publish/wwwroot/_framework/` holds `blazor.web.<hash>.js` (or `blazor.web.js`).
- AC6 (default and override): the Dev builds once without and once with `-p:Version=1.2.3`, starts the server
  each time and reports the start page's `Version 0.0.0` and `Version 1.2.3`, and runs *Test* on the second build.
- AC9 (log): the Dev starts `dotnet src/Censio.Server/bin/Release/net10.0/Censio.Server.dll` with no environment
  variables set, reports the `Now listening on:` line, `curl`s `/` and `/health`, and stops it.
- AC10, AC11 (process), AC12: a manual run of the Product Manager on Windows (x64, .NET 10 Desktop Runtime) of
  the built `Censio.Companion.exe`, before the review (step 8): one icon with tooltip `Censio Companion`, no
  window or taskbar button; menu `Version 0.0.0` (greyed), separator, `Exit`; `Exit` removes the icon
  (`echo %ERRORLEVEL%` after `start /wait` shows 0); a second start adds no icon and the first keeps running;
  after `taskkill /f /im explorer.exe` and `start explorer.exe` the icon is back; sign-out shows no dialog. E4
  (other user session) only if a second account is at hand, else accepted on fact 18. The orchestrator asks the
  Product Manager for the run and logs the answer.
- AC13: the Reviewer reads `docs/ARCHITECTURE.md` and record 0004.
- AC14: the orchestrator's step-7 *Coverage gate* output lists every changed file of `src/Censio.Companion.Core/`
  with a rate (not `n/a`); the Reviewer reads the `sonar.coverage.exclusions` line of `ci.yml`.

## Approach

**Build configuration (root).**
- `global.json`: `{"sdk": {"version": "10.0.100", "rollForward": "latestFeature", "allowPrerelease": false}}`,
  no `test` section (VSTest stays, spec *Challenge* 5).
- `Directory.Build.props`: `Nullable` and `ImplicitUsings` enabled, `GenerateDocumentationFile` true,
  `Product` `Censio`, `Version` `0.0.0`, `IncludeSourceRevisionInInformationalVersion` false,
  `AssemblyMetadata` item `CensioBuildVersion` = `$(Version)`; `PackageReference` (`PrivateAssets="all"`) to
  Reihitsu.Analyzer, SonarAnalyzer.CSharp and MSTest.Analyzers for every project. Test projects (name ends in
  `.Tests`) also get `IsPackable` false and the references `Microsoft.NET.Test.Sdk`, `MSTest.TestFramework`,
  `MSTest.TestAdapter`, `coverlet.collector` (or the same list in each test `.csproj`; Dev's choice, no version
  outside `Directory.Packages.props`).
- `Directory.Packages.props`: `ManagePackageVersionsCentrally` true; current stable versions of the packages under
  *Security areas touched*, Reihitsu.Analyzer on 1.1.x (fact 10), Microsoft.AspNetCore.Mvc.Testing on 10.0.x.
- `.gitignore`: `bin/`, `obj/`, `TestResults/`, `.sonarqube/`, `.vs/`, `*.user`, `publish/`.
- `Censio.slnx`: folders `src` and `tests` with all thirteen projects.

**Project graph** (no other references):

| Project | TFM | SDK | References |
| ------- | --- | --- | ---------- |
| `Censio.Contracts` | `net10.0` | `Microsoft.NET.Sdk` | — |
| `Censio.Pipe` | `net10.0` | `Microsoft.NET.Sdk` | — |
| `Censio.GameData` | `net10.0` | `Microsoft.NET.Sdk` | — |
| `Censio.Server.Data` | `net10.0` | `Microsoft.NET.Sdk` | — |
| `Censio.Server` | `net10.0` | `Microsoft.NET.Sdk.Web` | Contracts, Server.Data |
| `Censio.Companion.Core` | `net10.0` | `Microsoft.NET.Sdk` | Contracts, Pipe, GameData |
| `Censio.Companion` | `net10.0-windows` | `Microsoft.NET.Sdk` | Companion.Core |

Each `tests/<Project>.Tests` references its project and `Censio.Contracts` (for `ProductVersion`).

**Server.** `Program.cs` (top-level): `AddRazorComponents().AddInteractiveServerComponents()`, `AddHealthChecks()`;
pipeline `UseAntiforgery()`, `MapStaticAssets()`, `MapHealthChecks("/health")`,
`MapRazorComponents<App>().AddInteractiveServerRenderMode()`, `Run()`. Deliberately absent: `UseHttpsRedirection`,
`UseHsts`, `UseExceptionHandler`, `UseStatusCodePages*` (record 0006; unknown paths get the routing 404 with an
empty body; `Production` never shows exception details). `appsettings.json`: `Logging` defaults and
`"AllowedHosts": "*"`, no `Urls`/`Kestrel` keys. No `Properties/launchSettings.json` (spec *Challenge* 4).
Components under `src/Censio.Server/Components/`: `App.razor` (HTML shell, `<HeadOutlet @rendermode="InteractiveServer" />`,
`<Routes @rendermode="InteractiveServer" />`, script `@Assets["_framework/blazor.web.js"]`), `Routes.razor`,
`_Imports.razor`, `Layout/MainLayout.razor` (`@Body` only), `Pages/Home.razor` (`@page "/"`).

Start page layout (fixed, spec S3): `<PageTitle>Censio</PageTitle>`, then `<h1>Censio</h1>`, then
`<p>Version {version}</p>` with `version = ProductVersion.Read(typeof(Home).Assembly)`. Nothing else; theming is #31.

**Contracts.** `ProductVersion.Read` returns the `AssemblyInformationalVersionAttribute` value, `0.0.0` when
absent; `ArgumentNullException` for `null`.

**Library markers.** `Censio.Pipe`, `Censio.GameData` and `Censio.Server.Data` hold no feature code; each gets one
static class whose `Reference` property returns its assembly, so its test project tests the version stamp (AC1,
AC6). Feature issues may delete the marker once real code and tests exist.

**Companion.Core.** `TrayMenu` builds the entries (layout fixed by spec S7: `Version <v>` disabled, separator,
`Exit` enabled) and holds the texts `Censio Companion` and `Exit`. `SingleInstanceGuard` wraps a named `Mutex`
created with `initiallyOwned: true`; `TryAcquire` returns `createdNew` (never `WaitOne`, which is reentrant on the
owning thread and would let a second guard in the same process pass); `Dispose` releases the mutex when owned and
disposes it. `DefaultName` = `Local\Censio.Companion.SingleInstance`.

**Companion shell** (`src/Censio.Companion/`, `OutputType` `WinExe`, `UseWPF`, `UseWindowsForms`,
`EnableWindowsTargeting`, `<Using Remove>` for `System.Windows.Forms` and `System.Drawing` — fact 2).
`App.xaml`: no `StartupUri`, `ShutdownMode="OnExplicitShutdown"`. `App.xaml.cs` `OnStartup`: create the guard with
`DefaultName`; if `TryAcquire()` is false, `Shutdown(0)` and return (no icon, no message); else create `TrayIcon`.
`OnExit`: dispose `TrayIcon` (removes the icon), then the guard (same UI thread, as `ReleaseMutex` requires).
`TrayIcon` (`IDisposable`): a Windows Forms `NotifyIcon` with `SystemIcons.Application` as placeholder, `Text`
`TrayMenu.ToolTip`, `Visible` true, and a `ContextMenuStrip` mapped one to one from `TrayMenu.Create(version)`
(`ToolStripMenuItem` with `Enabled`, `ToolStripSeparator`); the `Exit` item calls `Application.Current.Shutdown(0)`.
Version: `ProductVersion.Read(typeof(App).Assembly)`. Session end needs no code: WPF shuts down on
`SessionEnding` and runs `OnExit`.

**CI** (`.github/workflows/ci.yml`): delete step *Detect solution* with its comment and every
`steps.solution.outputs.exists == 'true'` term in the `if:` conditions (spec *Challenge* 2); extend the begin
argument to `/d:sonar.coverage.exclusions=".squad/**,.claude/**,src/Censio.Companion/**"`. Nothing else
(permissions, triggers, action pins unchanged; format check and Windows runner stay with #3).

## Options considered

1. **Companion logic testable on Linux:** (a) platform-neutral `Censio.Companion.Core` — chosen; (b) linked
   sources in a `net10.0` test project — lost: not measured by coverlet (fact 11) and the logic cannot use the
   project's own types; (c) a Windows CI job running a `net10.0-windows` test project — lost: the session and the
   existing job stay blind, second job to maintain. Record 0004.
2. **Coverage exclusion of the shell:** (a) by construction locally (no test loads the shell, fact 4) plus
   `sonar.coverage.exclusions` in `ci.yml` — chosen; (b) `COVERAGE_EXCLUDES` — impossible in a product PR (fact 5);
   (c) `[ExcludeFromCodeCoverage]` on the shell types — lost: redundant locally (the assembly is never
   instrumented) and Sonar computes coverable lines from the analysis, not from the attribute. A squad issue on
   (b) is proposed in the result. Record 0004.
3. **Windows build:** `EnableWindowsTargeting` (chosen) vs a Windows job — part of record 0004.
4. **Tray icon:** Windows Forms `NotifyIcon` (chosen: in the SDK, no new dependency, handles Explorer restarts,
   fact 19) vs a WPF tray package (H.NotifyIcon) — lost: a new third-party dependency for three menu items.
5. **Version:** one MSBuild property (chosen) vs MinVer/Nerdbank.GitVersioning — record 0005.
6. **HTTPS:** template's redirection and HSTS vs plain HTTP with TLS at the proxy (chosen) — record 0006.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| root | `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `Censio.slnx`, `.gitignore` | new |
| `Censio.Contracts` | `Censio.Contracts.csproj`, `ProductVersion.cs` | new |
| `Censio.Pipe` | `Censio.Pipe.csproj`, `PipeAssembly.cs` | new |
| `Censio.GameData` | `Censio.GameData.csproj`, `GameDataAssembly.cs` | new |
| `Censio.Server.Data` | `Censio.Server.Data.csproj`, `ServerDataAssembly.cs` | new |
| `Censio.Server` | `Censio.Server.csproj`, `Program.cs`, `appsettings.json`, `Components/App.razor`, `Components/Routes.razor`, `Components/_Imports.razor`, `Components/Layout/MainLayout.razor`, `Components/Pages/Home.razor` | new |
| `Censio.Companion.Core` | `Censio.Companion.Core.csproj`, `TrayMenu.cs`, `TrayMenuEntry.cs`, `TrayMenuEntryKind.cs`, `SingleInstanceGuard.cs` | new |
| `Censio.Companion` | `Censio.Companion.csproj`, `App.xaml`, `App.xaml.cs`, `TrayIcon.cs` | new |
| CI | `.github/workflows/ci.yml` | *Detect solution* removed, coverage exclusion added |

No existing file is rewritten by the skeleton; every file above is new except `ci.yml`, which the Dev edits in step 6.

## Signatures (for the Dev's skeleton)

Namespace per project root (`Censio.Contracts`, …); all public unless stated. Each line gives the type, then its
members as name with C# type in prose.

- static class `ProductVersion`: constant `Fallback` (string, value 0.0.0); method
  `public static string Read(Assembly assembly)`.
- static classes `PipeAssembly`, `GameDataAssembly`, `ServerDataAssembly`: static read-only property `Reference`
  (type Assembly), returning the own assembly.
- enum `TrayMenuEntryKind`: members `Version`, `Separator`, `Exit`.
- sealed class `TrayMenuEntry`: constructor `TrayMenuEntry(TrayMenuEntryKind kind, string text, bool isEnabled)`;
  read-only properties `Kind` (TrayMenuEntryKind), `Text` (string), `IsEnabled` (bool).
- static class `TrayMenu`: constants `ToolTip` (string, value Censio Companion) and `ExitText` (string, value
  Exit); methods `public static string FormatVersion(string version)` (returns Version plus a space plus the
  version) and `public static IReadOnlyList<TrayMenuEntry> Create(string version)`.
- sealed class `SingleInstanceGuard`, implementing IDisposable: constant `DefaultName` (string, value
  Local\Censio.Companion.SingleInstance with one backslash, which the C# literal writes doubled);
  constructor `SingleInstanceGuard(string name)`; methods `public bool TryAcquire()` and `public void Dispose()`.
- Shell (not tested): partial class `App` deriving from System.Windows.Application, overriding
  `OnStartup(StartupEventArgs e)` and `OnExit(ExitEventArgs e)`; internal sealed class `TrayIcon`, implementing
  IDisposable, constructor `TrayIcon(string version, Action exit)`.
- Server: top-level Program (fact 16); components have no code-behind API.

Skeleton note: the constructor of TrayMenuEntry stores its arguments; the constructor of SingleInstanceGuard stores
the name and creates no mutex (TryAcquire does), so no constructor throws. The server skeleton may build and run an
empty app, so the server tests fail.

## Test files

`tests/Censio.Contracts.Tests/ProductVersionTests.cs`, `tests/Censio.Pipe.Tests/PipeAssemblyTests.cs`,
`tests/Censio.GameData.Tests/GameDataAssemblyTests.cs`, `tests/Censio.Server.Data.Tests/ServerDataAssemblyTests.cs`,
`tests/Censio.Server.Tests/ProgramTests.cs` (helper `tests/Censio.Server.Tests/CensioServerFactory.cs`),
`tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `tests/Censio.Companion.Core.Tests/SingleInstanceGuardTests.cs`;
each test project `tests/<Project>.Tests/<Project>.Tests.csproj` (created by the Dev in step 4 with the skeleton,
so *Build* passes before the Tester starts).

Existing test call sites of a changed signature: none (no tests exist).

Files no unit test can cover: `src/Censio.Companion/App.xaml`, `App.xaml.cs`, `TrayIcon.cs` — they need the
Windows Desktop runtime and a desktop session (fact 3). Excluded by construction locally and by
`sonar.coverage.exclusions` (`src/Censio.Companion/**`) in CI, record 0004. `src/Censio.Server/Program.cs` and the
`.razor` files are covered through `WebApplicationFactory`.

## Areas

Two new areas (Lead decision), index rows in `docs/areas/README.md` written by the Dev:

- `server` — `docs/areas/server.md`. Scope: the server's HTTP surface: listen address and default, `/`, `/health`,
  unknown paths, plain HTTP, the version shown. Not here: ingest (later), Docker (#4).
- `companion` — `docs/areas/companion.md`. Scope: the Companion process on Windows: tray icon, tooltip, menu,
  `Exit`, single instance per user session, session end, platform. Not here: pipe reading (later), release ZIP (#5).

## Documentation updates

All by the Dev after the code is green, unless noted; found by a sweep of `docs/`, `AGENTS.md`, `.squad/stack.md`,
`.squad/project.md` and `SECURITY.md` for enumerations the change extends.

- `docs/areas/server.md`, `docs/areas/companion.md` (new, from `docs/areas/_template.md`) and their index rows.
- `docs/ARCHITECTURE.md` (project block): *Components* lists the seven projects with one line of responsibility
  each (Pipe: reading and parsing the statistics pipe; GameData: reading the game files and the catalog;
  Contracts: the types shared by Companion and server, the product version; Server: web host, UI, later ingest;
  Server.Data: persistence; Companion.Core: the Companion's platform-neutral logic; Companion: the Windows tray
  shell) and the reference table of *Approach*, linking record 0004; *Security model* adds plain HTTP with TLS at
  the proxy and the unauthenticated `/` and `/health`, linking record 0006.
- `AGENTS.md` and `.github/copilot-instructions.md` project block *architecture*, identically: the project list
  replaces "listed here as they are created"; the Companion line names the shell/Core split.
- `.squad/stack.md`: *Layout* lists the seven projects and the shell without test project; *Coverage exclusions*
  adds that `src/Censio.Companion/**` is excluded in `sonar.coverage.exclusions` and, locally, by construction
  (record 0004); *Known pitfalls* adds fact 2 (CS0104 with `UseWPF` + `UseWindowsForms`).
- `.squad/project.md` *Security areas*: a new entry "Network exposure: the server's listener and its routes
  reachable without login (`/`, `/health`, the Blazor hub and static assets); plain HTTP, TLS at the reverse
  proxy (record 0006)".
- `SECURITY.md` *Deployment Security Considerations*: the server speaks plain HTTP only; TLS is the reverse
  proxy's; `/health` and the start page need no login. *Scope*: add "the server's HTTP endpoints".
- `docs/CONTRIBUTING.md`: *Getting started* — the Companion builds on Linux but runs only on Windows; start the
  server with `dotnet run --project src/Censio.Server` (Production, `http://localhost:5000`; set
  `ASPNETCORE_ENVIRONMENT=Development` for development); *Versioning and releases* — every assembly gets the
  version from the build property `Version` (`-p:Version=<v>`, default `0.0.0`), record 0005.
- `docs/UNIT_TESTS.md` (Tester): *Test stack* adds that server endpoint tests use `WebApplicationFactory`
  (Microsoft.AspNetCore.Mvc.Testing), which is a test host, not a mocking library.

## Architecture check

`.squad/project.md` lists no guarantee yet; none is weakened. The components of `docs/ARCHITECTURE.md` stay: the
Companion still reads the pipe (in Core, later) and the server still renders the UI. The ingest token and
WebSocket are not built yet, so the statement "the server accepts data only from a client that presents the ingest
token" stays true: there is no ingest route. The new unauthenticated routes expose no data and match the
trusted-network deployment of `SECURITY.md`; *Security model* and `SECURITY.md` are updated together (record 0006).

## Security considerations

- [ ] No limits: the change parses no external input (no request body is read; `/health` and `/` take no input).
- [ ] Exceptions that reach a user: none by design. In `Production` without an exception handler an unhandled
  error answers `500` with an empty body (no developer exception page outside `Development`); a failure to bind
  the port ends the process with the ASP.NET Core startup error in the console (operator only). The Companion shows
  no message at all.
- [ ] The Blazor hub `/_blazor` accepts circuits without login, as every Blazor Server app does; antiforgery is on
  (`UseAntiforgery`). The start page reveals the version (accepted, record 0006); `/health` reveals only `Healthy`.
- [ ] Default listen address loopback only (fact 12); exposure beyond it needs explicit configuration (#4 sets it
  in the image).
- [ ] CI: no permission, trigger, secret or action pin changes; the removed step only produced a flag.

## Decision records

- `docs/decisions/0004-companion-shell-and-linux-build.md` (Proposed)
- `docs/decisions/0005-product-version-from-one-build-property.md` (Proposed)
- `docs/decisions/0006-server-plain-http-and-open-health.md` (Proposed)

## Out of scope / follow-ups

As in the spec. Follow-ups for the orchestrator to open:

- Template repository (`squad`): "Product PRs cannot set coverage exclusions" — routing *Code a unit test cannot
  cover* and `stack.md` *Coverage exclusions* tell a product change to edit `COVERAGE_EXCLUDES` in
  `.squad/tools/squad_settings.py`, which `scope-check.py` refuses in a product PR.
- Product repository: "[Docs] Align the PR areas in CONTRIBUTING.md with the issue areas" — the list (`Companion`,
  `Server`, `Tests`, `Docker`, `CI`, `Docs`) lacks `Repo`, `Pipe`, `GameData`, `Contracts`, `Release`, `UI` and the
  other areas the issue titles use.
