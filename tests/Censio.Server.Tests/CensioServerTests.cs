using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;

using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Data;
using Censio.Server.Data.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="CensioServer" />: the server the Companion hosts in its own process on real loopback ports.
/// </summary>
[TestClass]
public sealed class CensioServerTests
{
    #region Constants

    /// <summary>
    /// The end of a line of an HTTP request.
    /// </summary>
    private const string LineEnd = "\r\n";

    /// <summary>
    /// A valid token of 44 characters for a token file written before the start.
    /// </summary>
    private const string StoredToken = "MARKER-localserver-0123456789abcdefghijABCDE";

    /// <summary>
    /// The content of the Blazor script in a web root named by the environment, which the server must not serve.
    /// </summary>
    private const string ForeignScript = "MARKER-WEBROOT-SCRIPT";

    /// <summary>
    /// The placeholder of an environment value that stands for a free port the server must not listen on.
    /// </summary>
    private const string OtherPortPlaceholder = "{otherPort}";

    /// <summary>
    /// The placeholder of an environment value that stands for a web root holding a foreign Blazor script.
    /// </summary>
    private const string WebRootPlaceholder = "{webRoot}";

    /// <summary>
    /// The environment variable that names the assemblies of hosting startups.
    /// </summary>
    private const string HostingStartupAssembliesVariable = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";

    /// <summary>
    /// The environment variable that prevents hosting startups.
    /// </summary>
    private const string PreventHostingStartupVariable = "ASPNETCORE_PREVENTHOSTINGSTARTUP";

    /// <summary>
    /// The identity of a schema step no build of Censio knows, as a newer version would record it.
    /// </summary>
    private const string NewerMigrationId = "29991231000000_FromNewerVersion";

    /// <summary>
    /// The text of the warning written through the log provider after the server stopped.
    /// </summary>
    private const string AfterStopText = "MARKER-after-stop: the provider still writes";

    /// <summary>
    /// The anonymised recording the local replay sends.
    /// </summary>
    private const string LongSessionFixture = "long-session.jsonl";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The pattern of the Blazor script element on the start page.
    /// </summary>
    private static readonly Regex _blazorScript = new("<script[^>]*\\ssrc=\"([^\"]*_framework/blazor\\.web(\\.[A-Za-z0-9]+)?\\.js)\"", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The pattern of the version paragraph on the start page.
    /// </summary>
    private static readonly Regex _version = new("<p>Version ([^<]*)</p>", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The real-time limit of a local replay of the fixture and of a stop, which keeps a broken implementation from
    /// hanging a test.
    /// </summary>
    private static readonly TimeSpan _replayBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary directories of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryDirectories = [];

    /// <summary>
    /// The servers the running test started, disposed afterwards.
    /// </summary>
    private readonly List<ILocalServer> _servers = [];

    /// <summary>
    /// The sessions the running test started, stopped afterwards.
    /// </summary>
    private readonly List<LocalServerSession> _sessions = [];

    /// <summary>
    /// The replay senders of the running test, disposed afterwards.
    /// </summary>
    private readonly List<ReplaySender> _senders = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CensioServerTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public CensioServerTests(TestContext testContext)
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
    /// Disposes the started servers and deletes the temporary directories of the test.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var sender in _senders)
        {
            await sender.DisposeAsync();
        }

        foreach (var session in _sessions)
        {
            await session.StopAsync(_replayBound).WaitAsync(_replayBound, TestContext.CancellationToken);
            await session.DisposeAsync();
        }

        foreach (var server in _servers)
        {
            await server.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();

        foreach (var directory in _temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// With IPv6 the server listens on the IPv4 and the IPv6 loopback address of the port.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CensioServerLocalEndpointsReturnsBothLoopbackAddresses()
    {
        // Act
        var endpoints = CensioServer.LocalEndpoints(5117, true);

        // Assert
        List<string> expected = ["127.0.0.1:5117", "[::1]:5117"];

        Assert.AreSequenceEqual(expected, endpoints.Select(endpoint => endpoint.ToString()).Order(StringComparer.Ordinal).ToList(), "The endpoints should be 127.0.0.1 and [::1] at the port");
    }

    /// <summary>
    /// Without IPv6 the server listens on the IPv4 loopback address of the port alone (E11).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CensioServerLocalEndpointsWithoutIPv6ReturnsIPv4Only()
    {
        // Act
        var endpoints = CensioServer.LocalEndpoints(5117, false);

        // Assert
        List<string> expected = ["127.0.0.1:5117"];

        Assert.AreSequenceEqual(expected, endpoints.Select(endpoint => endpoint.ToString()).ToList(), "The endpoints should be 127.0.0.1 at the port alone");
    }

    /// <summary>
    /// A started local server answers its start page with the version of the server assembly.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task CensioServerStartLocalServesStartPageWithVersion()
    {
        // Arrange
        var expectedVersion = ProductVersion.Read(typeof(CensioServer).Assembly);

        // Act
        var server = await StartRunningAsync(CreateTemporaryDirectory(), NullLoggerProvider.Instance);

        // Assert
        using var client = CreateHttpClient(server.Port);
        using var response = await client.GetAsync("/", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var match = _version.Match(body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "The start page should answer 200");
        Assert.IsTrue(match.Success, "The start page should hold a version paragraph");
        Assert.AreEqual(expectedVersion, match.Groups[1].Value, "The start page should show the version of the server assembly");
    }

    /// <summary>
    /// The Blazor script the start page of a local server references is served from the build output (fact 6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task CensioServerStartLocalServesBlazorScript()
    {
        // Act
        var server = await StartRunningAsync(CreateTemporaryDirectory(), NullLoggerProvider.Instance);

        // Assert
        var (referenced, status, body) = await FetchBlazorScriptAsync(server.Port);

        Assert.IsTrue(referenced, "The start page should reference _framework/blazor.web.js, fingerprinted or not");
        Assert.AreEqual(HttpStatusCode.OK, status, "The referenced Blazor script should answer 200");
        Assert.IsNotEmpty(body, "The Blazor script should have content");
    }

    /// <summary>
    /// The first start creates the data directory, which does not exist yet, with the database, the key ring and the
    /// token file (S4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task CensioServerStartLocalCreatesDataDirectoryWithDatabaseKeyRingAndToken()
    {
        // Arrange
        var dataDirectory = Path.Combine(CreateTemporaryDirectory(), "Censio", "Server");

        // Act
        await StartRunningAsync(dataDirectory, NullLoggerProvider.Instance);

        // Assert
        var keyDirectory = Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName);

        Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName)), "The start should create censio.db in the data directory");
        Assert.IsTrue(Directory.Exists(keyDirectory), "The start should create the key ring folder in the data directory");
        Assert.HasCount(1, Directory.GetFiles(keyDirectory, "key-*.xml"), "The start should create exactly one key file");
        Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName)), "The start should create the ingest token file in the data directory");
    }

    /// <summary>
    /// A started local server exposes the token of the token file in its data directory, without the line end, and leaves
    /// the file as it was.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task CensioServerStartLocalExposesTokenOfTokenFile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        await File.WriteAllTextAsync(tokenFile, StoredToken + "\n", TestContext.CancellationToken);

        // Act
        var server = await StartRunningAsync(dataDirectory, NullLoggerProvider.Instance);

        // Assert
        Assert.AreEqual(StoredToken, server.Token, "The server should expose the token of the token file");
        Assert.AreEqual(StoredToken + "\n", await File.ReadAllTextAsync(tokenFile, TestContext.CancellationToken), "The token file should stay as it was");
    }

    /// <summary>
    /// A setting of the environment takes no effect on a local server: each case sets one variable (and allows hosting
    /// startups), and every case checks that the token is required, no other port is listened on, the Blazor script of
    /// the web root named by the environment is not served and the test assembly's hosting startup does not run. Every
    /// variable is restored. The web-root case checks the build-output layout only, whose development manifest maps every
    /// asset to its source, so it cannot fail there; the published layout rests on fact 15 of the plan.
    /// </summary>
    /// <param name="variable">The environment variable of the case</param>
    /// <param name="value">Its value, with a placeholder for the other port or the foreign web root</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("CENSIO__INGEST__REQUIRETOKEN", "false")]
    [DataRow("ASPNETCORE_URLS", "http://127.0.0.1:" + OtherPortPlaceholder)]
    [DataRow("ASPNETCORE_WEBROOT", WebRootPlaceholder)]
    [DataRow(HostingStartupAssembliesVariable, "Censio.Server.Tests")]
    public async Task CensioServerStartLocalIgnoresEnvironmentSettings(string variable, string value)
    {
        // Arrange
        var otherPort = FindFreePort();
        var webRoot = CreateForeignWebRoot();
        var settings = new Dictionary<string, string?>
                       {
                           [PreventHostingStartupVariable] = "false",
                           [variable] = value.Replace(OtherPortPlaceholder, otherPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                                             .Replace(WebRootPlaceholder, webRoot, StringComparison.Ordinal)
                       };

        RecordingHostingStartup.Reset();

        var previous = SetEnvironment(settings);

        try
        {
            // Act
            var server = await StartRunningAsync(CreateTemporaryDirectory(), NullLoggerProvider.Instance);

            // Assert
            var reply = await SendHelloWithoutTokenAsync(server.Port);
            var (_, scriptStatus, scriptBody) = await FetchBlazorScriptAsync(server.Port);

            Assert.AreEqual(0, RecordingHostingStartup.Runs, $"The hosting startup of the test assembly should not run: {variable}");
            Assert.AreEqual(ContractHelloRefusal.TokenRefused, Assert.IsInstanceOfType<ContractHelloRefused>(reply, $"A hello without token should be refused: {variable}").Reason, $"The hello should be refused for its token: {variable}");
            Assert.IsFalse(IsListening(otherPort), $"The server should not listen on the port of the environment: {variable}");
            Assert.AreEqual(HttpStatusCode.OK, scriptStatus, $"The Blazor script should be served: {variable}");
            Assert.AreNotEqual(ForeignScript, scriptBody, $"The Blazor script of the web root named by the environment should not be served: {variable}");
        }
        finally
        {
            RestoreEnvironment(previous);
        }
    }

    /// <summary>
    /// A port another program holds on <c>127.0.0.1</c> makes the start report the port in use, with no server (E1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task CensioServerStartLocalWithPortTakenOnIPv4ReturnsPortInUse()
    {
        // Arrange
        using var blocker = new TcpListener(IPAddress.Loopback, 0);

        blocker.Start();

        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var options = CreateOptions(port, CreateTemporaryDirectory(), NullLoggerProvider.Instance, Socket.OSSupportsIPv6);

        // Act
        var start = await CensioServer.StartLocalAsync(options, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LocalServerStartKind.PortInUse, start.Kind, "A port taken on 127.0.0.1 should be reported in use");
        Assert.IsNull(start.Server, "No server should be handed out");
    }

    /// <summary>
    /// A port another program holds on <c>[::1]</c> makes the start report the port in use, with no server, and the
    /// IPv4 address the start had bound is released again (E1). Needs a system with IPv6.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task CensioServerStartLocalWithPortTakenOnIPv6ReturnsPortInUse()
    {
        // Arrange
        using var blocker = StartIPv6BlockerOrInconclusive();
        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;
        var options = CreateOptions(port, CreateTemporaryDirectory(), NullLoggerProvider.Instance, true);

        // Act
        var start = await CensioServer.StartLocalAsync(options, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LocalServerStartKind.PortInUse, start.Kind, "A port taken on [::1] should be reported in use");
        Assert.IsNull(start.Server, "No server should be handed out");
        Assert.IsTrue(CanBind(IPAddress.Loopback, port), "127.0.0.1 at the port should be released after the failed start");
    }

    /// <summary>
    /// A file in place of the data directory fails the start with no server, and the file stays as it was (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task CensioServerStartLocalWithUnwritableDataDirectoryReturnsFailed()
    {
        // Arrange
        var dataDirectory = Path.Combine(CreateTemporaryDirectory(), "Server");

        await File.WriteAllTextAsync(dataDirectory, "MARKER-file-in-place-of-the-folder", TestContext.CancellationToken);

        var options = CreateOptions(FindFreePort(), dataDirectory, NullLoggerProvider.Instance, Socket.OSSupportsIPv6);

        // Act
        var start = await CensioServer.StartLocalAsync(options, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LocalServerStartKind.Failed, start.Kind, "A data directory that cannot be created should fail the start");
        Assert.IsNull(start.Server, "No server should be handed out");
        Assert.AreEqual("MARKER-file-in-place-of-the-folder", await File.ReadAllTextAsync(dataDirectory, TestContext.CancellationToken), "The file in place of the folder should stay as it was");
    }

    /// <summary>
    /// A database written by a newer Censio version fails the start with no server and the server's critical line; the
    /// database file and the port are released afterwards (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task CensioServerStartLocalWithNewerDatabaseReturnsFailed()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = await CreateNewerDatabaseAsync(dataDirectory);
        var provider = new RecordingLoggerProvider();
        var port = FindFreePort();
        var options = CreateOptions(port, dataDirectory, provider, Socket.OSSupportsIPv6);

        // Act
        var start = await CensioServer.StartLocalAsync(options, TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(LocalServerStartKind.Failed, start.Kind, "A database of a newer version should fail the start");
        Assert.IsNull(start.Server, "No server should be handed out");
        Assert.Contains($"The database '{databaseFile}' was written by a newer Censio version; the server stops. Start that version or a newer one.", provider.Logger.Entries.Select(entry => entry.Message).ToList(), "The server's critical line should reach the log provider");

        File.Delete(databaseFile);

        Assert.IsFalse(File.Exists(databaseFile), "The database file should be released and deletable");
        Assert.IsTrue(CanBind(IPAddress.Loopback, port), "127.0.0.1 at the port should be free after the failed start");
    }

    /// <summary>
    /// The log provider gets the server's lines of level warning and above only: a start that generates the token (an
    /// information line) and then fails on a newer database passes the critical line and no line below warning (N7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task CensioServerStartLocalPassesOnlyWarningsAndAboveToLogProvider()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var databaseFile = await CreateNewerDatabaseAsync(dataDirectory);
        var provider = new RecordingLoggerProvider();
        var options = CreateOptions(FindFreePort(), dataDirectory, provider, Socket.OSSupportsIPv6);

        // Act
        await CensioServer.StartLocalAsync(options, TestContext.CancellationToken);

        // Assert
        var entries = provider.Logger.Entries.ToList();

        Assert.IsTrue(File.Exists(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName)), "The start should generate the token, whose information line the server writes");
        Assert.Contains((LogLevel.Critical, $"The database '{databaseFile}' was written by a newer Censio version; the server stops. Start that version or a newer one."), entries.Select(entry => (entry.Level, entry.Message)).ToList(), "The critical start line should reach the log provider");
        Assert.IsEmpty(entries.Where(entry => entry.Level < LogLevel.Warning).Select(entry => entry.Message), "No line below warning should reach the log provider");
    }

    /// <summary>
    /// Stopping a local server leaves the log provider it was given usable: a warning written through the Companion's log
    /// provider after the stop still reaches the log file (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task CensioServerStopLocalLeavesLogProviderUsable()
    {
        // Arrange
        var logFolder = CreateTemporaryDirectory();
        using var provider = new LogFileLoggerProvider(logFolder, new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero)));
        var server = await StartRunningAsync(CreateTemporaryDirectory(), provider);

        // Act
        await server.StopAsync(TestContext.CancellationToken);

        // Assert
        await server.Stopped.WaitAsync(_replayBound, TestContext.CancellationToken);

        Assert.IsTrue(server.Stopped.IsCompletedSuccessfully, "Stopped should complete once the server was stopped");

        provider.CreateLogger("Censio.Server.Tests").Log(LogLevel.Warning, new EventId(1), AfterStopText, null, (state, _) => state);

        await provider.FlushAsync(TimeSpan.FromSeconds(5));

        var written = string.Concat(Directory.GetFiles(logFolder).Order().Select(File.ReadAllText));

        Assert.Contains(AfterStopText, written, "A warning written after the stop should reach the log file");
    }

    /// <summary>
    /// A start whose token is already cancelled fails with the cancellation as its exception, hands out no server and
    /// leaves <c>127.0.0.1</c> at the port free (E9).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task CensioServerStartLocalWhenCancelledReturnsFailedAndReleasesPort()
    {
        // Arrange
        var port = FindFreePort();
        var options = CreateOptions(port, CreateTemporaryDirectory(), NullLoggerProvider.Instance, Socket.OSSupportsIPv6);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        await cancellation.CancelAsync();

        // Act
        var start = await CensioServer.StartLocalAsync(options, cancellation.Token);

        // Assert
        Assert.AreEqual(LocalServerStartKind.Failed, start.Kind, "A cancelled start should fail");
        Assert.IsNull(start.Server, "No server should be handed out");
        Assert.IsInstanceOfType<OperationCanceledException>(start.Exception, "The cancellation should be the exception of the failed start");
        Assert.IsTrue(CanBind(IPAddress.Loopback, port), "127.0.0.1 at the port should be free after the cancelled start");
    }

    /// <summary>
    /// A request whose host is not <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c>, or an HTTP/1.0 request without a
    /// host, is answered 400 (E7, fact 16).
    /// </summary>
    /// <param name="version">The HTTP version of the request</param>
    /// <param name="host">The host header, or <c>null</c> for none</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("HTTP/1.1", "example.com")]
    [DataRow("HTTP/1.1", "localhost.")]
    [DataRow("HTTP/1.1", "foo.localhost")]
    [DataRow("HTTP/1.1", "127.0.0.2")]
    [DataRow("HTTP/1.1", "[::2]")]
    [DataRow("HTTP/1.1", "[0:0:0:0:0:0:0:1]")]
    [DataRow("HTTP/1.1", "127.1")]
    [DataRow("HTTP/1.1", "2130706433")]
    [DataRow("HTTP/1.0", null)]
    public async Task CensioServerStartLocalRefusesForeignHostWith400(string version, string? host)
    {
        // Act
        var server = await StartRunningAsync(CreateTemporaryDirectory(), NullLoggerProvider.Instance);

        // Assert
        var status = await SendRawRequestAsync(server.Port, version, host);

        Assert.AreEqual(400, status, $"The request should be answered 400: {version}, host {host ?? "none"}");
    }

    /// <summary>
    /// A request whose host is <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c>, in any case and with any port, is
    /// answered (fact 10).
    /// </summary>
    /// <param name="host">The host header</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("localhost")]
    [DataRow("LOCALHOST:5000")]
    [DataRow("127.0.0.1")]
    [DataRow("[::1]")]
    public async Task CensioServerStartLocalAcceptsLoopbackHosts(string host)
    {
        // Act
        var server = await StartRunningAsync(CreateTemporaryDirectory(), NullLoggerProvider.Instance);

        // Assert
        var status = await SendRawRequestAsync(server.Port, "HTTP/1.1", host);

        Assert.AreEqual(200, status, $"The request should be answered 200: host {host}");
    }

    /// <summary>
    /// A replay without <c>--server</c> in local mode, started before the hosted server runs, waits for it, sends the
    /// fixture with the token taken in the process, and the pages of the hosted server show the profile (S5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task CensioServerStartLocalFillsProfileFromLocalReplay()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));

        // Act
        var (session, (sender, runner, replay)) = await RunSessionOnFreePortAsync(CreateTemporaryDirectory(), timeProvider, started => StartLocalReplay(started, timeProvider));

        await replay.WaitAsync(_replayBound, TestContext.CancellationToken);

        // Assert
        Assert.IsNull(runner.Status.SendProblem, "The replay should end without a send problem");
        Assert.IsNull(runner.Status.Problem, "The replay should end without a problem of the recording");
        Assert.AreEqual(LocalServerState.Running, session.Status.State, "The hosted server should run: " + session.Status);
        Assert.IsGreaterThan(0, sender.Counts.Ticks, "The replay should send ticks to the hosted server");
        Assert.AreEqual(0, sender.Counts.Refused, "The hosted server should refuse no tick");
        Assert.Contains(sender.ProfileName, await ReadRegionsPageAsync(session.Status.Port, sender.ProfileId), "The pages of the hosted server should show the replayed profile");
    }

    /// <summary>
    /// A profile the local replay stored is shown again after the hosted server was stopped and started anew on the same
    /// data directory (S6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC5")]
    public async Task CensioServerStartLocalKeepsStoredProfileAfterRestart()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        var first = await RunSessionOnFreePortAsync(dataDirectory, timeProvider);

        await using var sender = CreateLocalReplaySender(first, timeProvider);

        await CreateFixtureRunner(sender).RunAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);
        await first.StopAsync(_replayBound).WaitAsync(_replayBound, TestContext.CancellationToken);

        // Act
        var second = await RunSessionOnFreePortAsync(dataDirectory, timeProvider);

        // Assert
        Assert.AreEqual(LocalServerState.Running, second.Status.State, "The hosted server should start again on the same data directory: " + second.Status);
        Assert.IsGreaterThan(0, sender.Counts.Ticks, "The replay should have stored ticks before the restart");
        Assert.Contains(sender.ProfileName, await ReadRegionsPageAsync(second.Status.Port, sender.ProfileId), "The pages should show the stored profile after the restart");
    }

    /// <summary>
    /// Creates the options of a local server whose content root is the test's base folder.
    /// </summary>
    /// <param name="port">The port</param>
    /// <param name="dataDirectory">The data directory</param>
    /// <param name="logProvider">The log provider</param>
    /// <param name="listenOnIPv6">Whether the IPv6 loopback address is listened on as well</param>
    /// <returns>The options</returns>
    private static LocalServerOptions CreateOptions(int port, string dataDirectory, ILoggerProvider logProvider, bool listenOnIPv6)
    {
        return new LocalServerOptions
               {
                   Port = port,
                   DataDirectory = dataDirectory,
                   ContentRoot = AppContext.BaseDirectory,
                   LogProvider = logProvider,
                   ListenOnIPv6 = listenOnIPv6
               };
    }

    /// <summary>
    /// Finds a port that is free on <c>127.0.0.1</c> at the moment of the call.
    /// </summary>
    /// <returns>The port</returns>
    private static int FindFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);

        probe.Start();

        var port = ((IPEndPoint)probe.LocalEndpoint).Port;

        probe.Stop();

        return port;
    }

    /// <summary>
    /// Starts a listener on <c>[::1]</c> at a free port, or reports the test inconclusive on a system without IPv6.
    /// </summary>
    /// <returns>The started listener</returns>
    private static TcpListener StartIPv6BlockerOrInconclusive()
    {
        if (Socket.OSSupportsIPv6 == false)
        {
            Assert.Inconclusive("The system has no IPv6; the IPv4-only endpoints are asserted by CensioServerLocalEndpointsWithoutIPv6ReturnsIPv4Only");
        }

        var blocker = new TcpListener(IPAddress.IPv6Loopback, 0);

        blocker.Start();

        return blocker;
    }

    /// <summary>
    /// Checks whether an address and port can be bound.
    /// </summary>
    /// <param name="address">The address</param>
    /// <param name="port">The port</param>
    /// <returns>Whether a listener could be started there</returns>
    private static bool CanBind(IPAddress address, int port)
    {
        using var listener = new TcpListener(address, port);

        try
        {
            listener.Start();
            listener.Stop();

            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether anything listens on <c>127.0.0.1</c> at a port.
    /// </summary>
    /// <param name="port">The port</param>
    /// <returns>Whether a connection could be opened</returns>
    private static bool IsListening(int port)
    {
        using var client = new TcpClient();

        try
        {
            client.Connect(IPAddress.Loopback, port);

            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an HTTP client of the local server at a port, without a proxy.
    /// </summary>
    /// <param name="port">The port</param>
    /// <returns>The client</returns>
    private static HttpClient CreateHttpClient(int port)
    {
        return new HttpClient(new SocketsHttpHandler
                              {
                                  UseProxy = false
                              })
               {
                   BaseAddress = new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/")
               };
    }

    /// <summary>
    /// Sets environment variables.
    /// </summary>
    /// <param name="settings">The variables and their values</param>
    /// <returns>The values of the variables before</returns>
    private static Dictionary<string, string?> SetEnvironment(Dictionary<string, string?> settings)
    {
        var previous = settings.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        foreach (var (name, value) in settings)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        return previous;
    }

    /// <summary>
    /// Restores environment variables.
    /// </summary>
    /// <param name="previous">The variables and their values before</param>
    private static void RestoreEnvironment(Dictionary<string, string?> previous)
    {
        foreach (var (name, value) in previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>
    /// Builds the head of a GET request of the health endpoint.
    /// </summary>
    /// <param name="version">The HTTP version</param>
    /// <param name="host">The host header, or <c>null</c> for none</param>
    /// <returns>The request head, ending with the empty line</returns>
    private static string BuildRequest(string version, string? host)
    {
        var hostLine = host is null ? string.Empty : $"Host: {host}{LineEnd}";

        return $"GET /health {version}{LineEnd}{hostLine}Connection: close{LineEnd}{LineEnd}";
    }

    /// <summary>
    /// Creates a runner that replays the fixture at maximum speed into a sender.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <returns>The runner</returns>
    private static PipeReplayRunner CreateFixtureRunner(ReplaySender sender)
    {
        return new PipeReplayRunner(() => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", LongSessionFixture)), PipeReplaySpeed.Maximum, TimeProvider.System, NullLogger.Instance, sender);
    }

    /// <summary>
    /// Creates the sender of a replay without <c>--server</c> in local mode, as the Companion chooses it.
    /// </summary>
    /// <param name="session">The session of the hosted server</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The sender</returns>
    private static ReplaySender CreateLocalReplaySender(LocalServerSession session, TimeProvider timeProvider)
    {
        var commandLine = new CompanionCommandLine
                          {
                              Mode = CompanionMode.Replay,
                              ReplayPath = LongSessionFixture,
                              Speed = PipeReplaySpeed.Maximum
                          };

        return ReplaySenderFactory.Create(commandLine, session, "1.0.0", new ClientWebSocketConnector(timeProvider), timeProvider, NullLogger.Instance)
                   ?? throw new AssertFailedException("A replay without --server in local mode should be sent");
    }

    /// <summary>
    /// Creates the session of a server hosted in local mode at a port, which starts the server on the data directory.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <param name="port">The port</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The session</returns>
    private static LocalServerSession CreateLocalSession(string dataDirectory, int port, TimeProvider timeProvider)
    {
        var setting = new LocalServerSetting
                      {
                          Kind = LocalServerSettingKind.Local,
                          Port = port
                      };

        return new LocalServerSession(setting,
                                      (startPort, cancellationToken) => CensioServer.StartLocalAsync(CreateOptions(startPort, dataDirectory, NullLoggerProvider.Instance, Socket.OSSupportsIPv6), cancellationToken),
                                      timeProvider,
                                      NullLogger.Instance);
    }

    /// <summary>
    /// Starts a local server at a free port, retrying once on a port taken in the meantime, and asserts that it runs.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <param name="logProvider">The log provider</param>
    /// <returns>The running server, disposed after the test</returns>
    private async Task<ILocalServer> StartRunningAsync(string dataDirectory, ILoggerProvider logProvider)
    {
        var start = await CensioServer.StartLocalAsync(CreateOptions(FindFreePort(), dataDirectory, logProvider, Socket.OSSupportsIPv6), TestContext.CancellationToken);

        if (start.Kind == LocalServerStartKind.PortInUse)
        {
            start = await CensioServer.StartLocalAsync(CreateOptions(FindFreePort(), dataDirectory, logProvider, Socket.OSSupportsIPv6), TestContext.CancellationToken);
        }

        Assert.AreEqual(LocalServerStartKind.Running, start.Kind, "The local server should start: " + start.Exception);
        Assert.IsNotNull(start.Server, "A running start should hand out the server");

        _servers.Add(start.Server);

        return start.Server;
    }

    /// <summary>
    /// Creates the sender of a local replay into a session, which is disposed after the test, and starts the replay of the
    /// fixture.
    /// </summary>
    /// <param name="session">The session of the hosted server</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The sender, its runner and the running replay</returns>
    private (ReplaySender Sender, PipeReplayRunner Runner, Task Replay) StartLocalReplay(LocalServerSession session, TimeProvider timeProvider)
    {
        var sender = CreateLocalReplaySender(session, timeProvider);
        var runner = CreateFixtureRunner(sender);

        _senders.Add(sender);

        return (sender, runner, runner.RunAsync(TestContext.CancellationToken));
    }

    /// <summary>
    /// Runs the session of a server hosted at a free port until its server runs, retrying once with a new session at a new
    /// free port when the port was taken in the meantime.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <param name="timeProvider">The time provider</param>
    /// <returns>The session</returns>
    private async Task<LocalServerSession> RunSessionOnFreePortAsync(string dataDirectory, TimeProvider timeProvider)
    {
        var (session, _) = await RunSessionOnFreePortAsync(dataDirectory, timeProvider, static _ => 0);

        return session;
    }

    /// <summary>
    /// Runs the session of a server hosted at a free port until its server runs, after a step that is given the session
    /// before it runs, retrying once with a new session at a new free port when the port was taken in the meantime.
    /// </summary>
    /// <typeparam name="T">The result of the step</typeparam>
    /// <param name="dataDirectory">The data directory</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="beforeRun">The step before the session runs</param>
    /// <returns>The session and the result of the step of the session that ran</returns>
    private async Task<(LocalServerSession Session, T Result)> RunSessionOnFreePortAsync<T>(string dataDirectory, TimeProvider timeProvider, Func<LocalServerSession, T> beforeRun)
    {
        var session = CreateLocalSession(dataDirectory, FindFreePort(), timeProvider);
        var result = beforeRun(session);

        await RunSessionAsync(session);

        if (session.Status.State == LocalServerState.PortInUse)
        {
            session = CreateLocalSession(dataDirectory, FindFreePort(), timeProvider);
            result = beforeRun(session);

            await RunSessionAsync(session);
        }

        return (session, result);
    }

    /// <summary>
    /// Runs a session, which is stopped after the test, until its server runs or its start ended.
    /// </summary>
    /// <param name="session">The session</param>
    /// <returns>A task that completes when the start ended</returns>
    private async Task RunSessionAsync(LocalServerSession session)
    {
        _ = session.RunAsync(TestContext.CancellationToken);
        _sessions.Add(session);

        await session.WaitForTokenAsync(TestContext.CancellationToken).WaitAsync(_replayBound, TestContext.CancellationToken);
    }

    /// <summary>
    /// Reads the regions page of a profile from the hosted server.
    /// </summary>
    /// <param name="port">The port of the server</param>
    /// <param name="profileId">The identity of the profile</param>
    /// <returns>The page</returns>
    private async Task<string> ReadRegionsPageAsync(int port, Guid profileId)
    {
        using var client = CreateHttpClient(port);

        return await client.GetStringAsync($"/profiles/{profileId:D}/regions", TestContext.CancellationToken);
    }

    /// <summary>
    /// Requests the start page and the Blazor script it references.
    /// </summary>
    /// <param name="port">The port of the server</param>
    /// <returns>Whether the page references the script, and the status and body of the script</returns>
    private async Task<(bool Referenced, HttpStatusCode Status, string Body)> FetchBlazorScriptAsync(int port)
    {
        using var client = CreateHttpClient(port);
        var page = await client.GetStringAsync("/", TestContext.CancellationToken);
        var match = _blazorScript.Match(page);

        using var script = await client.GetAsync(match.Groups[1].Value, TestContext.CancellationToken);
        var body = await script.Content.ReadAsStringAsync(TestContext.CancellationToken);

        return (match.Success, script.StatusCode, body);
    }

    /// <summary>
    /// Opens an ingest connection to the local server, sends a hello without token and receives the reply.
    /// </summary>
    /// <param name="port">The port of the server</param>
    /// <returns>The reply, or <c>null</c> when the server closed the connection</returns>
    private async Task<ContractMessage?> SendHelloWithoutTokenAsync(int port)
    {
        using var socket = new ClientWebSocket();

        socket.Options.Proxy = null;

        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}{IngestEndpoint.Path}"), TestContext.CancellationToken);
        await IngestClient.SendAsync(socket, IngestClient.Hello(null));

        var (reply, _, _) = await IngestClient.ReceiveAsync(socket);

        return reply;
    }

    /// <summary>
    /// Sends a raw GET request of the health endpoint to <c>127.0.0.1</c> and reads the status of the answer.
    /// </summary>
    /// <param name="port">The port of the server</param>
    /// <param name="version">The HTTP version</param>
    /// <param name="host">The host header, or <c>null</c> for none</param>
    /// <returns>The status code</returns>
    private async Task<int> SendRawRequestAsync(int port, string version, string? host)
    {
        using var client = new TcpClient();

        await client.ConnectAsync(IPAddress.Loopback, port, TestContext.CancellationToken);

        var stream = client.GetStream();

        await stream.WriteAsync(Encoding.ASCII.GetBytes(BuildRequest(version, host)), TestContext.CancellationToken);

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync(TestContext.CancellationToken) ?? string.Empty;

        return int.Parse(statusLine.Split(' ')[1], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Creates a database in the data directory that records a schema step of a newer Censio version.
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <returns>The full path of the database file</returns>
    private async Task<string> CreateNewerDatabaseAsync(string dataDirectory)
    {
        var databaseFile = Path.Combine(dataDirectory, DatabaseSetup.DatabaseFileName);
        var opened = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, TestContext.CancellationToken);

        opened.Database?.Dispose();
        SqliteConnection.ClearAllPools();

        var builder = new SqliteConnectionStringBuilder
                      {
                          DataSource = databaseFile,
                          Mode = SqliteOpenMode.ReadWrite,
                          Pooling = false
                      };

        using var connection = new SqliteConnection(builder.ToString());

        await connection.OpenAsync(TestContext.CancellationToken);

        using var command = connection.CreateCommand();

        command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('" + NewerMigrationId + "', '99.0.0');";
        await command.ExecuteNonQueryAsync(TestContext.CancellationToken);

        return databaseFile;
    }

    /// <summary>
    /// Creates a web root that holds a foreign Blazor script, deleted after the test.
    /// </summary>
    /// <returns>The full path of the web root</returns>
    private string CreateForeignWebRoot()
    {
        var webRoot = CreateTemporaryDirectory();
        var framework = Path.Combine(webRoot, "_framework");

        Directory.CreateDirectory(framework);
        File.WriteAllText(Path.Combine(framework, "blazor.web.js"), ForeignScript);

        return webRoot;
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);

        return path;
    }

    #endregion // Methods
}