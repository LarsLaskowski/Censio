using System.Collections.Concurrent;

using Censio.Pipe.Tests;
using Censio.Server;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LocalServerSession" />.
/// </summary>
[TestClass]
public sealed class LocalServerSessionTests
{
    #region Constants

    /// <summary>
    /// The port of the setting.
    /// </summary>
    private const int Port = 5117;

    /// <summary>
    /// The token of the fake server.
    /// </summary>
    private const string Token = "LocalToken-0123456789abcdefghijKLMNOPQRST";

    /// <summary>
    /// The problem of an invalid setting.
    /// </summary>
    private const string Problem = "serverAddress is missing";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The start time of the time provider.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The time the session may take to stop.
    /// </summary>
    private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The ports the start delegate was called with, in order.
    /// </summary>
    private readonly ConcurrentQueue<int> _startPorts = [];

    /// <summary>
    /// The cancellation tokens the start delegate was called with, in order.
    /// </summary>
    private readonly ConcurrentQueue<CancellationToken> _startTokens = [];

    /// <summary>
    /// The result of the start delegate, which the test completes.
    /// </summary>
    private readonly TaskCompletionSource<LocalServerStart> _startResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes when the start delegate was called.
    /// </summary>
    private readonly TaskCompletionSource _startCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The fake servers of the test, whose held stops are released afterwards.
    /// </summary>
    private readonly List<FakeLocalServer> _servers = [];

    /// <summary>
    /// The sessions of the test, disposed afterwards.
    /// </summary>
    private readonly List<LocalServerSession> _sessions = [];

    /// <summary>
    /// The statuses the session raised, in order.
    /// </summary>
    private readonly ConcurrentQueue<LocalServerStatus> _statuses = [];

    /// <summary>
    /// The logger of the session.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalServerSessionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LocalServerSessionTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Releases the held stops of the fake servers and disposes the sessions.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var server in _servers)
        {
            server.ReleaseStop();
        }

        foreach (var session in _sessions)
        {
            await session.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }
    }

    /// <summary>
    /// A server is hosted for every valid command line when the setting is local or invalid (to show the invalid
    /// setting), and never when the setting is off or forward or the command line is invalid (S7, S8).
    /// </summary>
    /// <param name="mode">The mode of the command line</param>
    /// <param name="kind">The kind of the setting</param>
    /// <param name="expected">Whether a server is hosted</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Local, true)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Local, true)]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Local, true)]
    [DataRow(CompanionMode.Invalid, LocalServerSettingKind.Local, false)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Invalid, true)]
    [DataRow(CompanionMode.Invalid, LocalServerSettingKind.Invalid, false)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Off, false)]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Off, false)]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Forward, false)]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Forward, false)]
    public void LocalServerSessionIsHostedPerModeAndSetting(CompanionMode mode, LocalServerSettingKind kind, bool expected)
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = kind,
                          Port = kind == LocalServerSettingKind.Local ? Port : 0,
                          Problem = kind == LocalServerSettingKind.Invalid ? Problem : null
                      };

        // Act
        var isHosted = LocalServerSession.IsHosted(mode, setting);

        // Assert
        Assert.AreEqual(expected, isHosted, $"A {kind} setting with the mode {mode} should be hosted: {expected}");
    }

    /// <summary>
    /// An invalid setting starts no server, shows that the settings are invalid and logs the problem as a warning (E2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LocalServerSessionRunWithInvalidSettingStartsNoServerAndLogsWarning()
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Invalid,
                          Problem = Problem
                      };
        var session = CreateSession(setting, new RecordingTimeProvider(_start));

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(_startPorts, "An invalid setting should start no server");
        Assert.AreEqual(LocalServerState.SettingsInvalid, session.Status.State, "The status should be settings invalid");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Warning && entry.Message == "Server settings invalid: " + Problem, _logger.Entries, "The problem should be logged once as a warning");
    }

    /// <summary>
    /// An invalid setting without a problem text starts no server and logs the warning with an empty problem (E1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LocalServerSessionRunWithInvalidSettingWithoutProblemLogsEmptyProblem()
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Invalid
                      };
        var session = CreateSession(setting, new RecordingTimeProvider(_start));

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(_startPorts, "An invalid setting should start no server");
        Assert.AreEqual(LocalServerState.SettingsInvalid, session.Status.State, "The status should be settings invalid");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Warning && entry.Message == "Server settings invalid: ", _logger.Entries, "The warning should be logged once with an empty problem");
    }

    /// <summary>
    /// The session shows starting while the server starts, then running with the port, and logs the port (S1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LocalServerSessionRunShowsStartingThenRunning()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var running = WaitForStateAsync(session, LocalServerState.Running);

        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        var whileStarting = session.Status;

        // Act
        _startResult.SetResult(Running(CreateServer(false)));

        var status = await running.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(LocalServerState.Starting), whileStarting, "The session should show starting while the server starts");
        Assert.AreEqual(Status(LocalServerState.Running), status, "The session should raise running with the port");
        Assert.AreEqual(Status(LocalServerState.Running), session.Status, "The session should show running with the port");
        Assert.Contains((LogLevel.Information, "Local server running on port 5117"), _logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList(), "The running server should be logged with its port");
    }

    /// <summary>
    /// The server is started on the port of the setting (S3).
    /// </summary>
    /// <param name="port">The port of the setting</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(5000)]
    [DataRow(5117)]
    public async Task LocalServerSessionRunPassesSettingPort(int port)
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Local,
                          Port = port
                      };
        var session = CreateSession(setting, new RecordingTimeProvider(_start));

        // Act
        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        // Assert
        Assert.AreSequenceEqual(new[] { port }, _startPorts.ToArray(), "The server should be started once on the port of the setting");
        Assert.AreEqual(port, session.Status.Port, "The status should carry the port of the setting");
    }

    /// <summary>
    /// The address of the session is <c>http://localhost:&lt;port&gt;/</c> (S2, S3).
    /// </summary>
    /// <param name="port">The port of the setting</param>
    /// <param name="expected">The expected address</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(5000, "http://localhost:5000/")]
    [DataRow(5117, "http://localhost:5117/")]
    public void LocalServerSessionAddressIsLocalhostWithPort(int port, string expected)
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Local,
                          Port = port
                      };

        // Act
        var session = CreateSession(setting, new RecordingTimeProvider(_start));

        // Assert
        Assert.AreEqual(new Uri(expected), session.Address, "The address should be localhost at the port");
        Assert.AreEqual(expected, session.Address.AbsoluteUri, "The address should be written as http://localhost:<port>/");
    }

    /// <summary>
    /// A port taken by another program shows port in use with the port and logs it as a warning (E1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LocalServerSessionRunWhenPortInUseShowsPortInUseAndLogs()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));

        _startResult.SetResult(new LocalServerStart
                               {
                                   Kind = LocalServerStartKind.PortInUse
                               });

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(LocalServerState.PortInUse), session.Status, "The session should show the port in use");
        Assert.AreEqual(Status(LocalServerState.PortInUse), _statuses.LastOrDefault(), "The session should raise the port in use");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Warning && entry.Message == "Local server port 5117 in use", _logger.Entries, "The taken port should be logged once as a warning");
    }

    /// <summary>
    /// A start that fails shows failed, and the start is not tried again (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LocalServerSessionRunWhenStartFailsShowsFailed()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));

        _startResult.SetResult(new LocalServerStart
                               {
                                   Kind = LocalServerStartKind.Failed
                               });

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(LocalServerState.Failed), session.Status, "The session should show failed");
        Assert.AreEqual(Status(LocalServerState.Failed), _statuses.LastOrDefault(), "The session should raise failed");
        Assert.HasCount(1, _startPorts, "A failed start should not be tried again");
    }

    /// <summary>
    /// A start that returns failed with an exception shows failed and logs the exception once as an error (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LocalServerSessionRunWhenStartFailsWithExceptionShowsFailedAndLogs()
    {
        // Arrange
        var failure = new InvalidOperationException("The host could not be started");
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));

        _startResult.SetResult(new LocalServerStart
                               {
                                   Kind = LocalServerStartKind.Failed,
                                   Exception = failure
                               });

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(LocalServerState.Failed), session.Status, "The session should show failed");
        Assert.AreEqual(Status(LocalServerState.Failed), _statuses.LastOrDefault(), "The session should raise failed");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Error && entry.Message == "Local server failed" && ReferenceEquals(entry.Exception, failure), _logger.Entries, "The failure should be logged once as an error with its exception");
    }

    /// <summary>
    /// A start that throws, as a faulted task or synchronously, shows failed and logs the exception as an error, without
    /// throwing (E4).
    /// </summary>
    /// <param name="isSynchronous">Whether the start delegate throws synchronously instead of returning a faulted task</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(false, DisplayName = "Faulted task")]
    [DataRow(true, DisplayName = "Synchronous throw")]
    public async Task LocalServerSessionRunWhenStartThrowsShowsFailedAndLogs(bool isSynchronous)
    {
        // Arrange
        var failure = new IOException("The data folder cannot be created");
        var session = new LocalServerSession(CreateLocalSetting(), CreateThrowingStart(failure, isSynchronous), new RecordingTimeProvider(_start), _logger);

        _sessions.Add(session);

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(Status(LocalServerState.Failed), session.Status, "The session should show failed");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Error && entry.Message == "Local server failed" && ReferenceEquals(entry.Exception, failure), _logger.Entries, "The failure should be logged once as an error with its exception");
    }

    /// <summary>
    /// A server that stops on its own while it runs shows failed, logs it as an error and is not started again (E5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LocalServerSessionRunWhenServerStopsOnItsOwnShowsFailed()
    {
        // Arrange
        var server = CreateServer(false);
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var running = WaitForStateAsync(session, LocalServerState.Running);
        var failed = WaitForStateAsync(session, LocalServerState.Failed);

        _startResult.SetResult(Running(server));

        var run = session.RunAsync(TestContext.CancellationToken);

        await running.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        server.CompleteStopped();

        var status = await failed.WaitAsync(_guard, TestContext.CancellationToken);

        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(run.IsCompletedSuccessfully, "The run should end once the server stopped on its own");
        Assert.AreEqual(Status(LocalServerState.Failed), status, "A server that stops on its own should show failed");
        Assert.ContainsSingle(entry => entry.Level == LogLevel.Error && entry.Message == "Local server stopped unexpectedly", _logger.Entries, "The unexpected stop should be logged once as an error");
        Assert.HasCount(1, _startPorts, "A stopped server should not be started again");
    }

    /// <summary>
    /// Stopping the session stops the running server, and the stop shows no failure (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LocalServerSessionStopAsyncStopsRunningServer()
    {
        // Arrange
        var server = CreateServer(false);
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var running = WaitForStateAsync(session, LocalServerState.Running);

        _startResult.SetResult(Running(server));

        var run = session.RunAsync(TestContext.CancellationToken);

        await running.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await session.StopAsync(_stopTimeout).WaitAsync(_guard, TestContext.CancellationToken);
        await run.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(run.IsCompletedSuccessfully, "The run should end once the server stopped");
        Assert.IsTrue(server.StopRequested.IsCompleted, "The running server should be stopped");
        Assert.IsTrue(server.Stopped.IsCompleted, "The server should have stopped when the stop returns");
        Assert.IsEmpty(_statuses.Where(status => status.State == LocalServerState.Failed).ToList(), "A stop of the session should not show failed");
        Assert.IsEmpty(_logger.Entries.Where(entry => entry.Level >= LogLevel.Error).ToList(), "A stop of the session should log no error");
    }

    /// <summary>
    /// A server whose stop hangs lets the stop of the session return once the timeout passed on the time provider, and not
    /// before (S6, E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LocalServerSessionStopAsyncReturnsAfterTimeoutWhenServerHangs()
    {
        // Arrange
        var timeProvider = new RecordingTimeProvider(_start);
        var server = CreateServer(true);
        var session = CreateSession(CreateLocalSetting(), timeProvider);
        var running = WaitForStateAsync(session, LocalServerState.Running);

        _startResult.SetResult(Running(server));
        _ = session.RunAsync(TestContext.CancellationToken);

        await running.WaitAsync(_guard, TestContext.CancellationToken);

        var stopping = session.StopAsync(_stopTimeout);

        await server.StopRequested.WaitAsync(_guard, TestContext.CancellationToken);
        await timeProvider.WaitForDelayAsync(_stopTimeout, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var beforeTimeout = stopping.IsCompleted;

        // Act
        timeProvider.Advance(_stopTimeout);

        await stopping.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(beforeTimeout, "The stop should wait for the hanging server until the timeout");
        Assert.IsTrue(stopping.IsCompletedSuccessfully, "The stop should return once the timeout passed");
        Assert.IsFalse(server.Stopped.IsCompleted, "The server should still hang when the stop returns");
    }

    /// <summary>
    /// Stopping the session while the server starts cancels the start, and a server whose start finishes afterwards is
    /// stopped (E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LocalServerSessionStopAsyncWhileStartingCancelsStartAndStopsLateServer()
    {
        // Arrange
        var server = CreateServer(false);
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));

        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        var startToken = _startTokens.Single();

        // Act
        var stopping = session.StopAsync(_stopTimeout);

        await WaitForCancellationAsync(startToken);
        _startResult.SetResult(Running(server));
        await server.StopRequested.WaitAsync(_guard, TestContext.CancellationToken);
        await stopping.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(startToken.IsCancellationRequested, "The stop should cancel the start in progress");
        Assert.IsTrue(server.StopRequested.IsCompleted, "A server whose start finished after the stop should be stopped");
        Assert.IsEmpty(_statuses.Where(status => status.State == LocalServerState.Running).ToList(), "A server started after the stop should not show running");
    }

    /// <summary>
    /// A stop before the run starts no server, raises no status, and the run that follows ends at once (S6, E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LocalServerSessionStopAsyncBeforeRunStartsNoServer()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));

        await session.StopAsync(_stopTimeout).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        var token = await session.WaitForTokenAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsEmpty(_startPorts, "A stopped session should start no server");
        Assert.IsEmpty(_statuses, "A stopped session should raise no status");
        Assert.IsNull(token, "The wait for the token should return no token");
        Assert.IsEmpty(_logger.Entries, "A stopped session should log nothing");
    }

    /// <summary>
    /// A stop that arrives while the server starts, whose start then returns a running server at once, stops that server
    /// and never shows it running (E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task LocalServerSessionStopAsyncDuringStartThatReturnsRunningStopsServer()
    {
        // Arrange
        var server = CreateServer(false);
        var stopping = Task.CompletedTask;
        var isStartCancelled = false;
        LocalServerSession? session = null;

        session = new LocalServerSession(CreateLocalSetting(),
                                         (_, cancellationToken) =>
                                         {
                                             stopping = session?.StopAsync(_stopTimeout) ?? Task.CompletedTask;
                                             isStartCancelled = SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, _guard);

                                             return Task.FromResult(Running(server));
                                         },
                                         new RecordingTimeProvider(_start),
                                         _logger);
        session.StatusChanged += (_, status) => _statuses.Enqueue(status);
        _sessions.Add(session);

        // Act
        await session.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        await stopping.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(isStartCancelled, "The stop should cancel the start in progress");
        Assert.AreEqual(1, server.StopRequests, "The server the start returned should be stopped once");
        Assert.IsTrue(server.Stopped.IsCompleted, "The server should have stopped when the run ends");
        Assert.IsEmpty(_statuses, "A server started during the stop should raise no status");
        Assert.IsEmpty(_logger.Entries, "A stop during the start should log nothing");
    }

    /// <summary>
    /// The wait for the token, begun while the server starts, returns the token of the server once it runs (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task LocalServerSessionWaitForTokenAsyncReturnsTokenOnceRunning()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var waiting = session.WaitForTokenAsync(TestContext.CancellationToken);

        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        var beforeRunning = waiting.IsCompleted;

        // Act
        _startResult.SetResult(Running(CreateServer(false)));

        var token = await waiting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(beforeRunning, "The wait should not end while the server starts");
        Assert.AreEqual(Token, token, "The wait should return the token of the running server");
    }

    /// <summary>
    /// The wait for the token returns <see langword="null" /> when the server does not start (E10).
    /// </summary>
    /// <param name="kind">The kind of the failed start</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(LocalServerStartKind.PortInUse)]
    [DataRow(LocalServerStartKind.Failed)]
    public async Task LocalServerSessionWaitForTokenAsyncReturnsNullWhenStartFails(LocalServerStartKind kind)
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var waiting = session.WaitForTokenAsync(TestContext.CancellationToken);

        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        // Act
        _startResult.SetResult(new LocalServerStart
                               {
                                   Kind = kind
                               });

        var token = await waiting.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(token, $"The wait should return no token after the start ended with {kind}");
    }

    /// <summary>
    /// The wait for the live token, begun while the server starts, returns the token of the server once it runs, and a
    /// wait begun while it runs returns it at once (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LocalServerSessionWaitForLiveTokenAsyncReturnsTokenWhileRunning()
    {
        // Arrange
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var running = WaitForStateAsync(session, LocalServerState.Running);
        var waiting = session.WaitForLiveTokenAsync(TestContext.CancellationToken);

        _ = session.RunAsync(TestContext.CancellationToken);

        await WaitForStartAsync();

        var beforeRunning = waiting.IsCompleted;

        _startResult.SetResult(Running(CreateServer(false)));

        await running.WaitAsync(_guard, TestContext.CancellationToken);

        var firstToken = await waiting.WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        var laterToken = await session.WaitForLiveTokenAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsFalse(beforeRunning, "The wait should not end while the server starts");
        Assert.AreEqual(Token, firstToken, "The wait begun while starting should return the token of the running server");
        Assert.AreEqual(Token, laterToken, "A wait begun while the server runs should return its token");
    }

    /// <summary>
    /// Once the server no longer runs — its start failed, the port was in use, it stopped on its own or the session
    /// stopped it — the wait for the live token returns <see langword="null" />, so live sending does not retry a server
    /// that ended (E10).
    /// </summary>
    /// <param name="ending">How the server ended: port in use, failed, stopped unexpectedly or stopped</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("port in use")]
    [DataRow("failed")]
    [DataRow("stopped unexpectedly")]
    [DataRow("stopped")]
    public async Task LocalServerSessionWaitForLiveTokenAsyncReturnsNullAfterStop(string ending)
    {
        // Arrange
        var session = await CreateEndedSessionAsync(ending);

        // Act
        var token = await session.WaitForLiveTokenAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(token, $"The wait for the live token should return no token once the server ended: {ending}");
    }

    /// <summary>
    /// Creates a local setting at the port of the tests.
    /// </summary>
    /// <returns>The setting</returns>
    private static LocalServerSetting CreateLocalSetting()
    {
        return new LocalServerSetting
               {
                   Kind = LocalServerSettingKind.Local,
                   Port = Port
               };
    }

    /// <summary>
    /// Creates a start delegate that fails with an exception.
    /// </summary>
    /// <param name="failure">The exception</param>
    /// <param name="isSynchronous">Whether the delegate throws instead of returning a faulted task</param>
    /// <returns>The start delegate</returns>
    private static Func<int, CancellationToken, Task<LocalServerStart>> CreateThrowingStart(Exception failure, bool isSynchronous)
    {
        if (isSynchronous)
        {
            return (_, _) => throw failure;
        }

        return (_, _) => Task.FromException<LocalServerStart>(failure);
    }

    /// <summary>
    /// Creates a status at the port of the tests.
    /// </summary>
    /// <param name="state">The state</param>
    /// <returns>The status</returns>
    private static LocalServerStatus Status(LocalServerState state)
    {
        return new LocalServerStatus
               {
                   State = state,
                   Port = Port
               };
    }

    /// <summary>
    /// Creates the result of a start that runs a server.
    /// </summary>
    /// <param name="server">The server</param>
    /// <returns>The result</returns>
    private static LocalServerStart Running(ILocalServer server)
    {
        return new LocalServerStart
               {
                   Kind = LocalServerStartKind.Running,
                   Server = server
               };
    }

    /// <summary>
    /// Returns a task that completes with the first status of a state the session raises.
    /// </summary>
    /// <param name="session">The session</param>
    /// <param name="state">The state</param>
    /// <returns>The task</returns>
    private static Task<LocalServerStatus> WaitForStateAsync(LocalServerSession session, LocalServerState state)
    {
        var source = new TaskCompletionSource<LocalServerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

        session.StatusChanged += (_, status) =>
                                 {
                                     if (status.State == state)
                                     {
                                         source.TrySetResult(status);
                                     }
                                 };

        return source.Task;
    }

    /// <summary>
    /// Creates a session whose start delegate records its calls and returns <see cref="_startResult" />, with a handler
    /// that records its statuses.
    /// </summary>
    /// <param name="setting">The setting</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The session</returns>
    private LocalServerSession CreateSession(LocalServerSetting setting, TimeProvider timeProvider)
    {
        var session = new LocalServerSession(setting, StartAsync, timeProvider, _logger);

        session.StatusChanged += (_, status) => _statuses.Enqueue(status);
        _sessions.Add(session);

        return session;
    }

    /// <summary>
    /// Creates a session whose server ended in the given way and waits until the run ended.
    /// </summary>
    /// <param name="ending">How the server ends: port in use, failed, stopped unexpectedly or stopped</param>
    /// <returns>The session</returns>
    private async Task<LocalServerSession> CreateEndedSessionAsync(string ending)
    {
        var server = CreateServer(false);
        var session = CreateSession(CreateLocalSetting(), new RecordingTimeProvider(_start));
        var running = WaitForStateAsync(session, LocalServerState.Running);

        _startResult.SetResult(ending switch
                               {
                                   "port in use" => new LocalServerStart
                                                    {
                                                        Kind = LocalServerStartKind.PortInUse
                                                    },
                                   "failed" => new LocalServerStart
                                               {
                                                   Kind = LocalServerStartKind.Failed
                                               },
                                   _ => Running(server)
                               });

        var run = session.RunAsync(TestContext.CancellationToken);

        if (ending == "stopped unexpectedly")
        {
            await running.WaitAsync(_guard, TestContext.CancellationToken);
            server.CompleteStopped();
        }
        else if (ending == "stopped")
        {
            await running.WaitAsync(_guard, TestContext.CancellationToken);
            await session.StopAsync(_stopTimeout).WaitAsync(_guard, TestContext.CancellationToken);
        }

        await run.WaitAsync(_guard, TestContext.CancellationToken);

        return session;
    }

    /// <summary>
    /// Creates a fake server at the port of the tests with the token of the tests.
    /// </summary>
    /// <param name="isStopHeld">Whether its stop waits until the cleanup</param>
    /// <returns>The server</returns>
    private FakeLocalServer CreateServer(bool isStopHeld)
    {
        var server = new FakeLocalServer(Port, Token, isStopHeld);

        _servers.Add(server);

        return server;
    }

    /// <summary>
    /// The start delegate of the sessions: records the port and the token and returns the result the test sets.
    /// </summary>
    /// <param name="port">The port</param>
    /// <param name="cancellationToken">The cancellation token of the start</param>
    /// <returns>The result</returns>
    private Task<LocalServerStart> StartAsync(int port, CancellationToken cancellationToken)
    {
        _startTokens.Enqueue(cancellationToken);
        _startPorts.Enqueue(port);
        _startCalled.TrySetResult();

        return _startResult.Task;
    }

    /// <summary>
    /// Waits until the start delegate was called.
    /// </summary>
    /// <returns>A task that completes when the start was called</returns>
    private Task WaitForStartAsync()
    {
        return _startCalled.Task.WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Waits until a token is cancelled.
    /// </summary>
    /// <param name="token">The token</param>
    /// <returns>A task that completes when the token is cancelled</returns>
    private async Task WaitForCancellationAsync(CancellationToken token)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var registration = token.Register(() => source.TrySetResult());

        await source.Task.WaitAsync(_guard, TestContext.CancellationToken);
    }

    #endregion // Methods
}