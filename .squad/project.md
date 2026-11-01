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
- Local pipe connection (Companion): `NamedPipeConnector` opens `\\.\pipe\anno117` read-only, asynchronously and at impersonation level `Identification` (`PipeEndpoint`), so the pipe's server cannot act with the user's Windows identity; nothing is written to it (record 0035).
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
- `ChunkedStream` (`tests/Censio.Pipe.Tests/ChunkedStream.cs`): a read-only `Stream` that returns its bytes in chunks of a given size, can throw an `IOException` at an offset or cancel a `CancellationTokenSource` at an offset, can report `CanRead` as false, and throws `ObjectDisposedException` once disposed, for tests of the frame reader's partial reads, I/O errors and cancellation. It models no real component.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`): a fluent builder that writes the pipe's frame fields in little-endian order (type byte, payload, optional length prefix), for tests that need synthetic and corrupt frames.
- `ScriptedPipeMessageSource` (`tests/Censio.Pipe.Tests/ScriptedPipeMessageSource.cs`, with its handle `HeldPipeRead`): an `IPipeMessageSource` that returns queued results, either completed at once (optionally running an action when the read is requested, for example to set a clock) or held until the test releases them, counts its reads, can wait until a number of reads was requested, and records whether the token of a read that had not completed was cancelled; a read without a script entry stays pending until cancelled. For tests of `PipeTickAssembler`. It models no real component.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, referenced by `tests/Censio.Pipe.Tests`): the clock of timing tests, passed as the `TimeProvider` of the type under test and moved with `Advance` or `SetUtcNow`; its timers (`Task.Delay(delay, timeProvider, token)`) fire inside `Advance` exactly when the clock reaches their due time. It is a test library, not a mocking library.
