using System.Net.WebSockets;

namespace Censio.Server;

/// <summary>
/// One WebSocket connection of the ingest endpoint, from the hello to the close.
/// </summary>
public sealed partial class IngestSession
{
    #region Constants

    /// <summary>
    /// The maximum size of the hello message in bytes.
    /// </summary>
    public const int MaxHelloBytes = 32768;

    #endregion // Constants

    #region Fields

    // Skeleton: removed with the implementation
#pragma warning disable S4487

    /// <summary>
    /// The WebSocket of the connection.
    /// </summary>
    private readonly WebSocket _webSocket;

    /// <summary>
    /// The access that checks the token.
    /// </summary>
    private readonly IngestAccess _access;

    /// <summary>
    /// The connection counters.
    /// </summary>
    private readonly IngestConnections _connections;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The version of the server.
    /// </summary>
    private readonly string _serverVersion;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestSession" /> class.
    /// </summary>
    /// <param name="webSocket">The WebSocket of the connection</param>
    /// <param name="access">The access that checks the token</param>
    /// <param name="connections">The connection counters</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <param name="serverVersion">The version of the server</param>
    public IngestSession(WebSocket webSocket, IngestAccess access, IngestConnections connections, TimeProvider timeProvider, ILogger logger, string serverVersion)
    {
        _webSocket = webSocket;
        _access = access;
        _connections = connections;
        _timeProvider = timeProvider;
        _logger = logger;
        _serverVersion = serverVersion;
    }

    #endregion // Constructors

    #region Properties

    // Skeleton: removed with the implementation
#pragma warning disable RH2003

    /// <summary>
    /// Gets the time the Companion has to send its hello (10 seconds).
    /// </summary>
    public static TimeSpan HelloTimeout => throw new NotImplementedException();

    /// <summary>
    /// Gets the time a close waits for the reply of the peer (2 seconds).
    /// </summary>
    public static TimeSpan CloseWait => throw new NotImplementedException();
#pragma warning restore RH2003

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325

    /// <summary>
    /// Runs the session until the connection ends.
    /// </summary>
    /// <param name="stoppingToken">The token that signals the stop of the server</param>
    /// <returns>The task</returns>
    public Task RunAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325

    #endregion // Methods
}