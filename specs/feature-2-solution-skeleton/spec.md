# Spec: Solution skeleton with all projects

Source: Issue #2
Status: Draft

## Problem

The repository holds the squad and the CI workflow but no code: there is no `Censio.slnx`, so the CI job skips
restore, build and test (`.github/workflows/ci.yml`, step *Detect solution*), and every later issue (#3–#54)
needs a project to put its code in. The issue asks for the agreed project split as a solution that already
builds, tests and runs: a server that answers a start page and `/health` (what the Docker image of #4 and the
release of #5 check), and a Companion that starts as a tray icon on Windows. Until it exists, no gate of
`.squad/stack.md` can run and no artifact can be shipped.

The users here are the developer (and the squad), who builds and tests the solution, and the operator or tester,
who starts the server or the Companion and needs to see which version runs.

## Scenarios

- S1: Given a clone of the repository on Linux with a .NET 10 SDK, when the developer runs *Restore*, *Build*
  and *Test* of `.squad/stack.md` from the repository root, then each command succeeds, *Build* reports no
  `RH####` warning or error, and *Test* runs one test project per production project, all passing. The
  solution holds the production projects `Censio.Pipe`, `Censio.GameData`, `Censio.Contracts`,
  `Censio.Server`, `Censio.Server.Data` (each `net10.0`) and `Censio.Companion` (`net10.0-windows`) under
  `src/`, and `tests/<Project>.Tests/` for each.
- S2: Given the same clone, when the developer runs *Test with coverage*, the *Coverage gate* and the
  *Analyzer gate*, then both gates pass.
- S3: Given a built server, when it is started with no configuration, then it logs the address it listens on,
  which is a loopback address, and a browser at that address shows a page titled `Censio` with the heading
  `Censio` and the line `Version 0.0.0`; the page loads the Blazor script `_framework/blazor.web.js`, which the
  server serves.
- S4: Given a running server, when a client sends `GET /health`, then the server answers `200 OK` with the
  content type `text/plain` and the body `Healthy`, nothing else.
- S5: Given a build that is passed a version (`-p:Version=1.2.3`, the way the release and edge workflows of #5
  and #6 will pass theirs), when the server and the Companion run, then the start page shows `Version 1.2.3`,
  the tray menu shows `Version 1.2.3`, and every assembly of the solution carries `1.2.3` as its informational
  version. A version with build metadata, such as `0.0.0-edge+abc1234`, is shown exactly as passed.
- S6: Given Windows, when the user starts `Censio.Companion.exe`, then one tray icon with the tooltip
  `Censio Companion` appears in the notification area; no window and no taskbar button appear.
- S7: Given a running Companion, when the user right-clicks its tray icon, then a context menu shows the item
  `Version <version>` (greyed out, nothing happens on click), a separator, and the item `Exit`.
- S8: Given the context menu, when the user chooses `Exit`, then the tray icon disappears and the process ends
  with exit code 0.
- S9: Given a running Companion, when the same Windows user starts `Censio.Companion.exe` again, then the
  second process ends at once with exit code 0, without a message and without a second tray icon; the first
  keeps running.

## Errors and edge cases

- E1: The SDK does not match `global.json`: with a .NET SDK older than 10.0.100, or of another major version
  only, `dotnet build` fails with the SDK's own message naming the required version, before any project
  builds.
- E2: A request for a path the server does not know (`GET /does-not-exist`) is answered with `404 Not Found`.
- E3: A request over plain HTTP, in the `Production` environment (the default when no environment is set), is
  served as such: `/` and `/health` answer without a redirect to HTTPS and without a
  `Strict-Transport-Security` header (TLS is the reverse proxy's job, `SECURITY.md`).
- E4: A second Windows user signed in on the same machine (fast user switching) starts the Companion while the
  first user's instance runs: it starts normally with its own tray icon, because a single instance holds per
  user session.
- E5: Windows Explorer restarts while the Companion runs: the tray icon appears again once the notification
  area is back.
- E6: The user signs out or shuts Windows down while the Companion runs: it ends without a dialog and does not
  hold up the sign-out.

## Non-functional requirements

- Compatibility: the solution builds with any .NET 10 SDK from 10.0.100 on (`global.json`), on Linux and
  Windows; *Restore*, *Build*, *Test*, the *Analyzer gate* and the *Coverage gate* run on Linux, because the
  session and the CI job (`ubuntu-latest`) are Linux. The Companion runs on Windows x64 with the .NET 10
  Desktop Runtime installed; the server anywhere the ASP.NET Core 10 runtime runs.
- Packages: every package version in `Directory.Packages.props`; Reihitsu.Analyzer, SonarAnalyzer.CSharp and
  the MSTest analyzers in every project through `Directory.Build.props`; nullable reference types enabled
  everywhere; the version set in one place for all assemblies.
- Security: the server opens its first listener with two routes reachable without login (`/`, `/health`), as
  the trusted-network deployment of `SECURITY.md` intends. Without address configuration it listens on
  loopback only. `/health` reveals nothing but its status (no version, no checks, no exception text, in any
  environment). No secret, token or outbound call is added.
- Configuration: no new setting. The listen address uses the standard ASP.NET Core settings (`--urls`,
  `ASPNETCORE_URLS`, `ASPNETCORE_HTTP_PORTS`); the version is the build property `Version`, default `0.0.0`.
- Logs: the server logs to the console as ASP.NET Core does by default, including the listen address. The
  Companion writes no log yet (#13).
- Time: `/health` answers without touching any dependency, so it stays fast while the server runs.

## Acceptance criteria

- [ ] AC1: On Linux with a .NET 10 SDK, *Restore*, *Build* and *Test* succeed for `Censio.slnx` with no
  `RH####` warning or error; the solution contains exactly the six production projects with the target
  frameworks of S1 and one test project `tests/<Project>.Tests/` per production project, each running at least
  one passing test (S1).
- [ ] AC2: The *Analyzer gate* and, after *Test with coverage*, the *Coverage gate* pass on the change, and the
  CI job on the pull request builds and tests the whole solution, the Companion included, and passes (S2).
- [ ] AC3: `global.json` accepts every .NET 10 SDK from 10.0.100 on and refuses an older SDK and another major
  version (E1).
- [ ] AC4: In the `Production` environment, `GET /` answers `200` with an HTML page whose title is `Censio`,
  whose heading is `Censio` and which contains `Version <version>`; the page references
  `_framework/blazor.web.js`, and `GET /_framework/blazor.web.js` answers `200`; the app uses the interactive
  server render mode (S3).
- [ ] AC5: `GET /health` answers `200`, content type `text/plain`, body exactly `Healthy`, in `Production` and
  `Development` alike (S4).
- [ ] AC6: Built without a version, every assembly of the solution carries the informational version `0.0.0`
  and the start page and the tray menu show `Version 0.0.0`; built with `-p:Version=<v>`, all carry and show
  exactly `<v>`, with no source-revision suffix added by the build (S3, S5).
- [ ] AC7: In `Production` over plain HTTP, `GET /` and `GET /health` are not redirected (no `3xx`) and carry no
  `Strict-Transport-Security` header (E3).
- [ ] AC8: `GET /does-not-exist` answers `404` (E2).
- [ ] AC9: Started without address configuration, the server listens on loopback addresses only and logs the
  address (S3).
- [ ] AC10: On Windows, the started Companion shows one tray icon with the tooltip `Censio Companion`, no window
  and no taskbar button; its context menu holds exactly `Version <version>` (disabled), a separator and
  `Exit`; `Exit` removes the icon and ends the process with exit code 0 (S6, S7, S8).
- [ ] AC11: A second start in the same Windows user session ends with exit code 0 without a message or a second
  icon while the first keeps running; a start in another user session is not blocked (S9, E4).
- [ ] AC12: After an Explorer restart the Companion's tray icon is shown again, and sign-out or shutdown ends
  the Companion without a dialog (E5, E6).
- [ ] AC13: `docs/ARCHITECTURE.md` lists every project with its responsibility and the project references
  between them, and the way the Windows-only projects build on Linux CI is recorded in a decision record
  (S1, S2).

## Out of scope

- The Dockerfile and the image (#4), the release and edge workflows (#5, #6), README and user guide (#7), the
  rest of the CI issue (#3: format check, CodeQL, a Windows runner unless the plan chooses one for AC2).
- Any pipe, game-data, contract, persistence or ingest code: the four libraries hold no feature code yet.
- Companion features: connection status, logging, settings, autostart, local mode (#13, #15, #39, #43); a
  localized (German) Companion UI; a final product icon (a placeholder icon is enough).
- Web layout and theming (#31), web login (none planned, #8), error pages beyond the `404` of E2.
- A self-contained Companion that runs without an installed .NET (#5).
- Two servers on the same port: the second fails with the ASP.NET Core default error; no own handling.

## Open questions

- `EnableWindowsTargeting` or a dedicated Windows job? Answered: the plan decides and records it (AC13). The
  spec fixes the constraint: the stack's commands and the CI job run on Linux and must pass for the whole
  solution (AC1, AC2), so the Companion must at least compile on Linux; how its tests run there (a test
  project for `net10.0-windows` cannot start without the Windows Desktop runtime) is the plan's to settle.
- Which version does a build show without one? Answered: `0.0.0` (AC6). #5 passes the tag's version and #6
  names `0.0.0-edge+<short sha>`, which S5 shows unchanged; #8 lists 0.1.0 as the first release, which is a
  tag, not a default.
- What does the start page show? Answered: the product name and the version (issue scope), with the exact text
  of S3; its layout is #31.
- Single instance per machine or per user, and does the second start say anything? Answered: per Windows user
  session, silently (S9, E4). The issue asks for a single instance only; who reads the pipe when two users run
  the game is #11's single-reader handling, and a message is asked for nowhere.
- Companion UI language? Answered: English. #8 limits DE/EN to the names extracted from the game; the
  bilingual rule of `AGENTS.md` covers `README.md` and the user guides, not the UI.
- Does this feature change CI? Answered: `ci.yml` already restores, builds and tests `Censio.slnx` on
  `ubuntu-latest` once the file exists (*Detect solution*), so this pull request is the first whose CI builds
  the solution; an edit to `ci.yml` is in scope only as far as AC2 needs it.
- How are AC10–AC12 verified, when the session and CI are Linux? Answered: the menu's items, texts and states
  and the single-instance rule by unit tests as far as they run on Linux; the tray behavior itself by a manual
  run of the Product Manager on Windows, which the plan schedules before the review — the issue's own criterion
  "The Companion starts on Windows and shows the tray icon; exit works".

## Area documents

No area exists yet (`docs/areas/README.md`); the plan decides the areas. In outline:

- A server area (`docs/areas/server.md`): the listen address and its default, the routes `/` and `/health` with
  their responses, the plain-HTTP behavior, the version shown, `404` for unknown paths.
- A Companion area (`docs/areas/companion.md`): the tray icon, its tooltip and menu, `Exit`, single instance
  per user session, the platform it runs on.
- Not an area: `docs/ARCHITECTURE.md` gets the project list, responsibilities and references (AC13);
  `docs/CONTRIBUTING.md` (*Versioning and releases*) how the version is passed to a build; `.squad/stack.md`
  (*Layout*) and the architecture block of `AGENTS.md` and `.github/copilot-instructions.md`, which say the
  projects are added "with the issues that need them", list the six projects.
