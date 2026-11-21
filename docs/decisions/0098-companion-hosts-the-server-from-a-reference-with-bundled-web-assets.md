# 0098: The Companion hosts the server through a project reference, with the server's web assets bundled into its exe

- **Status:** Proposed
- **Date:** 2026-11-21
- **Area:** local-mode
- **Source:** Issue #39
- **Supersedes:** —

## Context

Record [0015](0015-two-deliverables-and-local-mode.md) lets the Companion run `Censio.Server` in its own process. The
release ZIP must stay one self-contained exe that needs nothing installed (record
[0027](0027-companion-zip-self-contained-single-file-unsigned.md), [companion](../areas/companion.md), *Platform*).
The server finds its pages' scripts and styles through static web assets, which the Web SDK publishes
(record [0007](0007-server-web-assets-independent-of-restore-inputs.md)). A probe on this branch's base published the
WPF Companion with a reference to `Censio.Server`. The ASP.NET Core runtime and the server's assemblies were bundled.
The web assets (`_framework/blazor.web.js`, MudBlazor's files, the server's own `wwwroot`) and the endpoints manifest
were missing, and the server's `appsettings.json` was left beside the exe. A second probe added the server's publish
output to the Companion's publish list. That bundled all of them into the one exe. The server also reads environment
variables and `appsettings.json` by default, and on a gaming PC those may belong to another program.

## Options considered

1. **Project reference, with the server's published web assets added to the Companion's publish list; the host is
   built in code from the Companion's settings alone** (chosen). One process, one exe and one server code base. Con:
   a build target in the Companion's project file. The assets are checked by the release check (local-mode AC10).
2. **Start `Censio.Server.exe` as a child process, extracted from the ZIP** — keeps the two hosts apart, but it is a
   second process the issue does not ask for. The ZIP would no longer be one file, and the Companion would have to
   supervise and stop the child.
3. **Serve the assets from embedded resources** — no files on disk, but `MapStaticAssets` and its endpoints manifest
   expect files. The server would need a second way of serving assets that only local mode uses.
4. **Turn the Companion into a Web SDK project** — the assets would flow by themselves, but a WPF application on the
   Web SDK is not a combination the SDK supports. It would change the build of the tray shell for one feature.
5. **Let the hosted server read environment variables and `appsettings.json` like the standalone server** — no extra
   code, but `ASPNETCORE_URLS`, `CENSIO__INGEST__REQUIRETOKEN` or `ASPNETCORE_ENVIRONMENT` set for another program
   could widen the listener or switch off the token.

## Decision

`Censio.Companion.Core` references `Censio.Server`. The Companion's publish adds the server's published `wwwroot` and
endpoints manifest and leaves out its `appsettings.json`. In local mode the host is built only from the values the
Companion passes, with no environment variables and no configuration files; the rules are in
[local-mode](../areas/local-mode.md).

## Consequences

- The standalone server keeps its entry point. `Program.cs` calls the same set-up that local mode uses, so the
  server's start checks, failures and log lines are identical in both.
- The Companion ZIP grows by the ASP.NET Core runtime, the server and its assets (the probe's exe was 232 MB, its web
  assets about 5 MB of it), and the release check fails when the bundled assets are missing.
- `docs/ARCHITECTURE.md` lists Server among the references of `Companion.Core`.
- `Exit` may take up to 5 seconds in local mode, against 2 seconds otherwise, so the server can stop cleanly
  ([companion](../areas/companion.md)).
