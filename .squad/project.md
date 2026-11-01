# Project

What the squad needs to know about this project that is not stack-specific. Read by the Lead, the Devil's
Advocate, Security, the Tester and the Reviewer. Not template-managed: `adopt-template` creates it once
and never overwrites it. A product PR updates it when the change makes an entry untrue
(`.squad/routing.md`, *Scope of a product PR*).

## Security areas

A change that touches one of these lists it under *Security areas touched* in its plan, so Security reviews the
plan (`.squad/routing.md`, *Tiers*, which also says when a change is tier `security`). Name the concrete types,
files or endpoints.

- Ingest token: read from configuration or an environment variable, compared in constant time, never logged.
- Authentication: the WebSocket ingest accepts a Companion only with a valid token; the web UI follows the deployment note in `SECURITY.md`.
- File paths for the cache and for recordings: derived from configuration, never from data read from the pipe or a game file; reject traversal.
- Parsing of external input: the Anno 117 statistics pipe stream and game files (Companion), and every WebSocket message (server) — bounded sizes, malformed input is rejected, never trusted.
- Data-protection key ring: `<Censio:DataDirectory>/data-protection-keys` (`DataProtectionSetup`), created owner-only, unencrypted at rest; the server stops when it cannot write it.
- Network exposure: the server's listener and its routes reachable without login (`/`, `/health`, the Blazor hub and static assets); plain HTTP, TLS at the reverse proxy (record 0006).
- Release publishing: `.github/workflows/release.yml` — the Docker Hub credentials and the `contents: write` token only in jobs that run no build code; the Docker Hub credentials and `WEBSITE_DISPATCH_TOKEN` are secrets of the environment `release`, which admits only `v*` tags (record 0012); a run moves Docker Hub `latest` only for a tag that is the highest `v*` version, checked in `check` and `promote` (record 0012); GitHub's *Latest* mark is not protected.
- Outbound calls: none planned; a new one is a security area.
- Logging of external data: pipe, game-file and WebSocket content is logged only truncated and sanitized, never the token.

## Guarantees

Deliberate behavior that must not change without the Product Manager. Each one is described in
`docs/ARCHITECTURE.md` and, where it was a real choice, has a decision record.

- None recorded yet; a guarantee is added here with its decision record when the first one is made.

## Integration surface

What the Reviewer checks when the diff introduces or changes a thing of this kind: every place that must
change with it.

**A new or changed configuration option** touches:
- the options type and its validation
- registration and binding in the Companion or server host
- the default configuration file (`appsettings.json`)
- the configuration table in `README.md` (key, environment variable, default; English first, then German)
- the test that pins the binding

**A new or changed service / module** touches:
- its interface
- registration and lifetime
- the test double in the test project
- the component list in `docs/ARCHITECTURE.md`

**A new external API call or DTO** touches:
- the pipe/game-file reader or ingest message type and its DTOs — the external shape must not leak past it
- the fake/stub in the tests

## Test doubles

The hand-written fakes and stubs the tests reuse (no mocking library unless `docs/UNIT_TESTS.md` says
otherwise):

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`): an `ILogger` that keeps each entry's level, formatted message and exception in `Entries`, for tests that assert what a component logs.
