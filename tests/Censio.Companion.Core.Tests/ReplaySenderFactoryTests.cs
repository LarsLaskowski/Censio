using Censio.Contracts;
using Censio.Pipe;
using Censio.Server;
using Censio.Server.Tests;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ReplaySenderFactory" />.
/// </summary>
[TestClass]
public sealed class ReplaySenderFactoryTests
{
    #region Constants

    /// <summary>
    /// The port of the local server.
    /// </summary>
    private const int LocalPort = 5117;

    /// <summary>
    /// The token of the local server.
    /// </summary>
    private const string Token = "LocalToken-0123456789abcdefghijKLMNOPQRST";

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private const string CompanionVersion = "1.2.3";

    /// <summary>
    /// The path of the recording.
    /// </summary>
    private const string ReplayPath = "recording.jsonl";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The address given with <c>--server</c>.
    /// </summary>
    private static readonly Uri _serverAddress = new("ws://censio.example:8080/ingest");

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The sessions of the test, disposed afterwards.
    /// </summary>
    private readonly List<LocalServerSession> _sessions = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySenderFactoryTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public ReplaySenderFactoryTests(TestContext testContext)
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
    /// Disposes the sessions of the test.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var session in _sessions)
        {
            await session.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }
    }

    /// <summary>
    /// With <c>--server</c> the replay goes to that address as today, also in local mode (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderFactoryCreateWithServerAddressReturnsRemoteSender()
    {
        // Arrange
        var connector = new DuplexReplayConnector(null);
        var commandLine = CreateReplayCommandLine(_serverAddress);
        var session = CreateSession();

        // Act
        await using var sender = ReplaySenderFactory.Create(commandLine, session, CompanionVersion, connector, new FakeTimeProvider(), new RecordingLogger());

        // Assert
        Assert.IsNotNull(sender, "A replay with --server should be sent");

        var problem = await sender.ConnectServerAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.AreEqual(ReplaySendProblemKind.ServerUnreachable, problem, "The connector of the test reaches no server");
        Assert.AreSequenceEqual([_serverAddress], connector.Addresses, "The replay should connect to the address of --server");
    }

    /// <summary>
    /// Without <c>--server</c> in local mode the replay goes to the hosted server with its token (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task ReplaySenderFactoryCreateWithoutServerInLocalModeReturnsLocalSender()
    {
        // Arrange
        await using var server = ScriptedIngestServer.Create();
        var connector = new DuplexReplayConnector(server.ClientSocket);
        var session = CreateSession();

        _ = session.RunAsync(TestContext.CancellationToken);

        // Act
        await using var sender = ReplaySenderFactory.Create(CreateReplayCommandLine(null), session, CompanionVersion, connector, new FakeTimeProvider(), new RecordingLogger());

        // Assert
        Assert.IsNotNull(sender, "A replay in local mode should be sent");

        var connecting = sender.ConnectServerAsync(TestContext.CancellationToken);
        var message = await server.AcceptHelloAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
        var connected = await connecting.WaitAsync(_guard, TestContext.CancellationToken);

        Assert.IsNull(connected, "The accepted hello should connect");
        Assert.AreEqual(Token, Assert.IsInstanceOfType<ContractHello>(message, "The first message should be the hello").Token, "The hello should carry the token of the hosted server");
        Assert.AreSequenceEqual([new Uri("ws://127.0.0.1:5117/ingest")], connector.Addresses, "The replay should connect to the hosted server at its port");
    }

    /// <summary>
    /// Without <c>--server</c> and outside local mode the replay is not sent.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void ReplaySenderFactoryCreateWithoutServerOutsideLocalModeReturnsNull()
    {
        // Arrange
        var connector = new DuplexReplayConnector(null);

        // Act
        var sender = ReplaySenderFactory.Create(CreateReplayCommandLine(null), null, CompanionVersion, connector, new FakeTimeProvider(), new RecordingLogger());

        // Assert
        Assert.IsNull(sender, "A replay without --server outside local mode should not be sent");
        Assert.IsEmpty(connector.Addresses, "No connection should be opened");
    }

    /// <summary>
    /// Creates the command line of a replay.
    /// </summary>
    /// <param name="serverAddress">The address of <c>--server</c>, or <see langword="null" /> for none</param>
    /// <returns>The command line</returns>
    private static CompanionCommandLine CreateReplayCommandLine(Uri? serverAddress)
    {
        return new CompanionCommandLine
               {
                   Mode = CompanionMode.Replay,
                   ReplayPath = ReplayPath,
                   Speed = PipeReplaySpeed.Maximum,
                   ServerAddress = serverAddress
               };
    }

    /// <summary>
    /// Creates a session in local mode whose start runs a fake server at the port with the token of the tests.
    /// </summary>
    /// <returns>The session, disposed after the test</returns>
    private LocalServerSession CreateSession()
    {
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Local,
                          Port = LocalPort
                      };
        var session = new LocalServerSession(setting,
                                             (port, _) => Task.FromResult(new LocalServerStart
                                                                          {
                                                                              Kind = LocalServerStartKind.Running,
                                                                              Server = new FakeLocalServer(port, Token, false)
                                                                          }),
                                             new FakeTimeProvider(),
                                             new RecordingLogger());

        _sessions.Add(session);

        return session;
    }

    #endregion // Methods
}