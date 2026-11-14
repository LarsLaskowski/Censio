using Censio.Pipe;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Sends a replayed recording to a server over one connection.
/// </summary>
public sealed class ReplaySender : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The largest size of a reply of the server in bytes.
    /// </summary>
    public const int MaxReplyBytes = 65536;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The address of the server.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly Uri _address;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The path of the token file, if any.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly string? _tokenFilePath;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The version of the Companion.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly string _companionVersion;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The connector.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly IReplayServerConnector _connector;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The time provider.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly TimeProvider _timeProvider;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The logger.
    /// </summary>
#pragma warning disable S4487 // Skeleton: removed with the implementation
    private readonly ILogger _logger;
#pragma warning restore S4487 // Skeleton: removed with the implementation

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySender" /> class.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="tokenFilePath">The path of the token file, if any</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public ReplaySender(Uri address, string? tokenFilePath, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        _address = address;
        _tokenFilePath = tokenFilePath;
        _companionVersion = companionVersion;
        _connector = connector;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the time the sender waits for a reply of the server.
    /// </summary>
    public static TimeSpan ReplyTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the identity of the profile the replay is sent to.
    /// </summary>
    public Guid ProfileId { get; }

    /// <summary>
    /// Gets the name of the profile the replay is sent to.
    /// </summary>
    public string ProfileName { get; } = string.Empty;

    /// <summary>
    /// Gets the counts of what was sent.
    /// </summary>
    public ReplaySendCounts Counts { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Reads the token file, if one is named.
    /// </summary>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public ReplaySendProblemKind? Prepare()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Connects to the server and sends the hello.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<ReplaySendProblemKind?> ConnectServerAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Passes a change of the connection state on to the server.
    /// </summary>
    /// <param name="state">The state</param>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public void NotifyState(PipeConnectionState state)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Sends a tick and waits for the acknowledgement.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<ReplaySendProblemKind?> SendTickAsync(PipeTick tick, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Sends the start of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the start was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<ReplaySendProblemKind?> SendSessionStartAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Sends the end of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the end was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<ReplaySendProblemKind?> SendSessionEndAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public Task<ReplaySendProblemKind?> CloseAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
#pragma warning disable RH2003 // Skeleton: removed with the implementation
    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // IAsyncDisposable
}