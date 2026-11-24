# Unit Tests

This document describes how unit tests are written in this repository. It is binding for human
contributors and AI agents alike: **unit tests are mandatory for newly written code**, new tests follow the
conventions below, and existing tests are the reference implementation — when in doubt, look at a
neighboring test file before inventing a new pattern.

## Test stack

- **MSTest** (`[TestClass]`, `[TestMethod]`, `[DataRow]`). No other test framework.
- **No assertion library** (no FluentAssertions) and **no mocking library** — use real objects or the
  hand-written fakes and stubs listed under *Test doubles*.
- Coverage is collected with `coverlet.collector`.
- **Server endpoint tests** use `WebApplicationFactory` (`Microsoft.AspNetCore.Mvc.Testing`), which hosts the
  real server in memory. It is a test host, not a mocking library, so it does not conflict with the rule above.
- **Companion logic** (the tray menu entries, the single-instance guard) lives in the platform-neutral
  `Censio.Companion.Core` and is tested in `Censio.Companion.Core.Tests`; the Windows shell `Censio.Companion`
  only wires it to the tray and has no unit tests of its own.

## Test doubles

The hand-written fakes and stubs the tests reuse (no mocking library unless *Test stack* says otherwise), one line
each: the double, where it lives and what it stands in for; how it behaves is read in its code.

- `RecordingLogger` (`tests/Censio.Server.Tests/RecordingLogger.cs`, linked into the Companion and Server.Data tests) and `ConcurrentRecordingLogger` (`tests/Censio.Server.Tests/ConcurrentRecordingLogger.cs`, linked into the Companion tests) — `ILogger`s keeping every entry, the second from any thread.
- `ExtendedMigrationsAssembly` (`tests/Censio.Server.Data.Tests/`) — an `IMigrationsAssembly` of the real migrations plus test ones.
- `FakeProfileStateSource` (`tests/Censio.Server.Tests/FakeProfileStateSource.cs`) — an `IProfileStateSource`.
- `FakeHistoryTrends` (`tests/Censio.Server.Tests/FakeHistoryTrends.cs`) — an `IHistoryTrends` with held, failing and recorded reads.
- `HeldTimerTimeProvider` (`tests/Censio.Server.Tests/HeldTimerTimeProvider.cs`) — a `FakeTimeProvider` whose timer callbacks run by hand, also after disposal.
- `BlockingClockTimeProvider` (`tests/Censio.Server.Tests/BlockingClockTimeProvider.cs`) — a `FakeTimeProvider` whose first clock read blocks.
- `ChunkedStream` and `ChunkedClockStream` (`tests/Censio.Pipe.Tests/`) — chunked streams for the frame reader and for record-and-replay.
- `PipeFrameBuilder` (`tests/Censio.Pipe.Tests/PipeFrameBuilder.cs`, linked into the Companion and Anonymiser tests) — synthetic and corrupt pipe frames.
- `ScriptedPipeMessageSource` with `HeldPipeRead` (`tests/Censio.Pipe.Tests/`) — an `IPipeMessageSource`.
- `ScriptedPipeConnector` (`tests/Censio.Companion.Core.Tests/ScriptedPipeConnector.cs`) — an `IPipeConnector`.
- `ControlledPipeStream` (`tests/Censio.Companion.Core.Tests/ControlledPipeStream.cs`) — models an asynchronous pipe read's end and cancellation (checked against `dotnet/runtime` `release/11.0` `PipeStream.ValueTaskSource.cs`).
- `ControlledWriteStream` (`tests/Censio.Pipe.Tests/ControlledWriteStream.cs`, linked into the Companion tests) — models a file stream's write and flush failures (checked against `dotnet/runtime` `release/11.0` `Win32Marshal`).
- `SparseStream`, `NonSeekableStream`, `TrackingStream`, `RdaArchiveBuilder`, `DdsFileBuilder` (`tests/Censio.GameData.Tests/`) — sparse, forward-only and disposal-logging streams; builders of RDA archives and BC7 DDS files.
- `ScriptedPipeAnonymiserRandom` (`tests/Censio.Pipe.Tests/ScriptedPipeAnonymiserRandom.cs`, linked into the Anonymiser tests) — an `IPipeAnonymiserRandom`.
- `TwoPassRecordingStream` (`tests/Censio.Pipe.Tests/TwoPassRecordingStream.cs`) — a stream that differs per pass, for the anonymiser.
- `FailingTextWriter` (`tests/Censio.Anonymiser.Tests/FailingTextWriter.cs`) — a `TextWriter` whose writes fail.
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — the clock of every timing test; a test library, not a mocking one.
- `BackwardsFakeTimeProvider` (`tests/Censio.Pipe.Tests/BackwardsFakeTimeProvider.cs`) — models a system clock set back (checked by a scratch run).
- `RecordingTimeProvider` (`tests/Censio.Pipe.Tests/RecordingTimeProvider.cs`, linked into the Companion and Server tests) — a `FakeTimeProvider` that records and awaits its timers.
- `RecordingLoggerProvider` (`tests/Censio.Server.Data.Tests/`, linked into the Server tests) — an `ILoggerProvider` over one `RecordingLogger`.
- `CascadedLanguageProbe` (`tests/Censio.Server.Tests/CascadedLanguageProbe.cs`) — a page, for what `MainLayout` cascades.
- `InMemoryDuplexStream` (`tests/Censio.Server.Tests/InMemoryDuplexStream.cs`, linked into the Companion tests) — a connection end, for real `WebSocket` pairs.
- `FailingWebSocketFeature` (`tests/Censio.Server.Tests/FailingWebSocketFeature.cs`) — an `IHttpWebSocketFeature` failing every accept.
- `FakeStartupEntryStore` (`tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs`) — models `RegistryStartupEntryStore` (checked against the .NET 11 `RegistryKey` source).
- `FakeDiagnosticsWindow`, `FakeUbisoftInstallRecords`, `FakeFileVersionReader` (`tests/Censio.Companion.Core.Tests/`) — an `IDiagnosticsWindow`, `IUbisoftInstallRecords` and `IFileVersionReader`.
- `FakeLocalServer` (`tests/Censio.Companion.Core.Tests/FakeLocalServer.cs`) — an `ILocalServer` with a held stop.
- `RecordingHostingStartup` (`tests/Censio.Server.Tests/RecordingHostingStartup.cs`) — an `IHostingStartup` counting its runs.
- `RegExportUbisoftInstallRecords` (`tests/Censio.Companion.Core.Tests/RegExportUbisoftInstallRecords.cs`) — an `IUbisoftInstallRecords` over the `reg export` sample in `Fixtures/GameInstallation/`.
- `ScriptedIngestServer`, `DuplexReplayConnector`, `ThrowingWebSocket`, `SendFailingWebSocket`, `LoopbackWebSocketHost`, `LoopbackWebProxy`, `QueuedIngestConnector` (`tests/Censio.Companion.Core.Tests/`), `TestServerReplayConnector` (`tests/Censio.Server.Tests/`) — doubles of the replay and live senders' tests.
- `CatalogTestData` (`tests/Censio.Companion.Core.Tests/CatalogTestData.cs`) — test catalogs with icons, their documents, identities and icon references, for the catalog upload tests.
- `ForeignContractMessage` (`tests/Censio.Contracts.Tests/ForeignContractMessage.cs`) — a `ContractMessage` of no contract type.

## Where tests live

<!-- project:begin layout -->
- Test project: `tests/<Project>.Tests/`, test classes placed flat in it (no subfolders).
- One test class per type under test, in `{TypeUnderTest}Tests.cs`.
- Test fixtures: `tests/<Project>.Tests/Fixtures/`, copied to the test output, each described with its facts in the
  folder's `README.md`. Only anonymised recordings, never a raw one (record 0029).
- A fixture another test project reads is linked into it, never copied: `Censio.Server.Tests.csproj` links
  `tests/Censio.Pipe.Tests/Fixtures/long-session.jsonl` as `Fixtures/Pipe/long-session.jsonl` (copied to the test output),
  and the `PageReaderFixture…` tests (`PageReaderTests`) replay it through `PipeReplayStream`, `PipeTickAssembler` and
  `ContractMessageConverter.ToTick`, as the Companion converts it, into a real `ServerState`; the facts they assert are
  listed in the fixture's `README.md`.
- Data pages are rendered with `PageTestHost` (`tests/Censio.Server.Tests/PageTestHost.cs`): a bUnit context with
  MudBlazor's services, loose JavaScript interop and a restored `ServerState` over an in-memory database (or a database
  file the test passes), whose `PageReader` reads that state or a `FakeProfileStateSource`; notices still come from the
  state, so a test that counts reads stores its ticks and catalogs in the state and its read results in the fake. The
  host also registers a `HistoryRetention` over that state by a late-bound factory, as the server registers it, and an
  `IConfiguration` without settings of its own, and cascades `DeletionAllowed` to the page it renders: `true` unless
  the test sets `PageTestHost.DeletionAllowed`, which stands for a page opened by a host the deletion rule does not
  trust.
- `CENSIO_FIXTURE_DENY_LIST`: the path of the local deny list the fixtures are checked against
  (`FixtureDenyListTests`, `tests/Censio.Pipe.Tests/Fixtures/README.md`); unset, that test is reported inconclusive.
- `CENSIO_ANNO_INSTALL`: the Anno 117 installation folder, set on Windows for the survey test
  `RdaInstallationSurveyRealInstallationReadsAssetAndIcon` and for the catalog test
  `GameCatalogReaderInstallationResolvesFixtureGuids` (`tests/Censio.GameData.Tests`); unset, those tests are reported
  inconclusive.
- `CENSIO_CATALOG_SAMPLE`: the folder of the six loose game files `assets.xml`, `datasets.xml`, `texts_english.xml`,
  `texts_german.xml`, `properties.xml` and `templates.xml` (never in the repository), for `GameCatalogReaderSampleResolvesFixtureGuids` and
  `GameCatalogReaderFixtureHoldsNoSampleText` (`tests/Censio.GameData.Tests`); unset, those tests are reported
  inconclusive. They write GUIDs, kinds and counts only, never a name or text. `GameCatalogReaderSampleResolvesFixtureGuids` and
  `GameCatalogReaderInstallationResolvesFixtureGuids` also build the catalog a second time with empty `properties.xml`
  and `templates.xml` (the assets' own values), write the difference to the catalog with the template and property
  defaults as counts and GUIDs (kind changes, region-list transitions, the pipe-fixture GUIDs that changed), and fail
  when an entry of that build is missing from the other; the installation test is the one the Product Manager runs to
  confirm the changes against the real files.
<!-- project:end layout -->

## Naming

- Class: `{TypeUnderTest}Tests`; a type whose tests grow large is split by concern into
  `{TypeUnderTest}{Concern}Tests` (for example `ParserFailureTests`), one file each.
- Method: `{TypeUnderTest}{Scenario}{ExpectedResult}` in PascalCase **without underscores**, e.g.
  `ParserEmptyInputReturnsNull`.

## Criterion ids

A test written for an acceptance criterion of a squad plan or spec carries the criterion's id as a test category,
`[TestCategory("AC3")]` on the test method, so that `.squad/tools/plan-check.py --matrix` finds it by id after a
rename or a split; a test that asserts several criteria carries each id. The method name follows *Naming*
unchanged.

## Structure

- Arrange / Act / Assert, separated by blank lines; **one act per test**.
- No logic in tests (no `if`, loops or `switch`); use `[DataRow]` for several inputs.
- Shared setup goes into a private/static helper method, not the constructor.
- **Every `Assert.*` call has an explanatory message.**
- Use the specific `Assert` member (`Assert.AreEqual`, `Assert.Contains`, `Assert.HasCount`,
  `Assert.AreSame`, `Assert.ThrowsExactly`, …) instead of `Assert.IsTrue(...)` around a boolean or
  `StringAssert` (MSTEST0037 / MSTEST0046).
- Pass `TestContext.CancellationToken` (constructor-injected `TestContext`) to every call that accepts a
  token — `Task.Run`, `Task.Delay`, `*Async` APIs (MSTEST0049).
- Drive timing through an injected `TimeProvider` (`FakeTimeProvider`), never `Thread.Sleep` or the real
  clock; use a temporary directory for file-system tests and delete it afterwards.
- **Time bounds as a ratio.** A time bound that cannot be tested as a count (allocations, iterations) — a
  quadratic check that must stay linear, say — is an exception to the real clock: measure two durations
  in the same test (the base input and a multiple of it) and assert their **ratio**, never an absolute
  duration; set the limit to at least three times the highest ratio measured, with the coverage
  instrumentation of *Test with coverage* included.
- `#region` blocks and XML documentation as in production code.

## Tests that read repository documents

`.gitattributes` may check files out with CRLF (`*.md text eol=crlf`), so on a CI runner a document carries
`\r\n` where the repository blob has `\n`. A test or script that reads a repository document (`*.md`, and the
other files `.gitattributes` checks out with CRLF) normalizes `\r\n` to `\n` before it compares lines or feeds
them to a shell. The first such test also tests the normalizer with CRLF input.

## Code coverage

**Threshold: at least 80 % coverage on new or changed production code, and at least 80 % line coverage overall** —
the same measure as SonarQube's "coverage on new code": covered lines and branch conditions together, so an
untested arm of a `switch` or an `if` counts on a changed line. Both values are set in `.squad/tools/squad_settings.py`
(`COVERAGE_THRESHOLD`, `COVERAGE_OVERALL_THRESHOLD`); the overall one may start lower in a repository adopted
with a coverage debt and is only ever raised. Check it locally before a push with *Test with coverage* and the
*Coverage gate* from [`.squad/stack.md`](../.squad/stack.md). The gate lists every
changed production file with its covered/coverable changed lines, the covered branch conditions on them
(`+ m/n conditions`), the uncovered line numbers and the lines with an uncovered condition (`conditions not
covered`). Lines
that genuinely cannot be covered by a unit test (for example host startup glue) need an explicit,
recorded decision — they are not silently accepted.

## Checklist for new tests

- [ ] New production code has accompanying unit tests — mandatory, not optional.
- [ ] The *Analyzer gate* reports no diagnostic in a changed test file (MSTest analyzer rules are
      info-level and only visible there or in SonarQube Cloud).
- [ ] At least 80 % coverage (lines and branch conditions) on new/changed production code and 80 % line coverage
  overall (*Coverage gate*).
- [ ] File, class and method named as above; a test for a squad acceptance criterion carries its id.
- [ ] Arrange / Act / Assert, one act, an explanatory message on every assertion, the specific `Assert`
      member, `TestContext.CancellationToken` passed on.
- [ ] No mocking library — real objects or a hand-written fake/stub.
- [ ] *Format* from `.squad/stack.md` run before committing.
