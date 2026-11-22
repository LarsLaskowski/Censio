using System.Globalization;

using Censio.Contracts;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Sends the live data of the pipe connection to a server and reconnects when the connection fails.
/// </summary>
public sealed class LiveSender : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The name of the profile live data goes to.
    /// </summary>
    public const string ProfileName = "Live";

    /// <summary>
    /// The largest number of messages that wait for their acknowledgement.
    /// </summary>
    public const int MaxWaitingMessages = 64;

    /// <summary>
    /// The largest size of the waiting messages in bytes of written JSON.
    /// </summary>
    public const int MaxWaitingBytes = 16777216;

    #endregion // Constants

    #region Fields

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The address of the server.
    /// </summary>
    private readonly Uri _address;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The reader of the token, or <see langword="null" /> when no token is presented.
    /// </summary>
    private readonly Func<CancellationToken, Task<string?>>? _readToken;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// Whether the server is the local one.
    /// </summary>
    private readonly bool _isLocal;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private readonly string _companionVersion;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The connector.
    /// </summary>
    private readonly IReplayServerConnector _connector;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;
#pragma warning restore S4487

    // Skeleton: removed with the implementation
#pragma warning disable S4487
    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveSender" /> class.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="readToken">The reader of the token, or <see langword="null" /> when no token is presented</param>
    /// <param name="isLocal">Whether the server is the local one</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    private LiveSender(Uri address, Func<CancellationToken, Task<string?>>? readToken, bool isLocal, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        _address = address;
        _readToken = readToken;
        _isLocal = isLocal;
        _companionVersion = companionVersion;
        _connector = connector;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    // Skeleton: removed with the implementation
#pragma warning disable CS0067, S3264
    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<LiveSendStatus>? StatusChanged;
#pragma warning restore CS0067, S3264

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the identity of the profile live data goes to.
    /// </summary>
    public static Guid ProfileId { get; } = new("b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94");

    /// <summary>
    /// Gets the delays before the attempts to reconnect.
    /// </summary>
    public static IReadOnlyList<TimeSpan> RetryDelays { get; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)];

    /// <summary>
    /// Gets the time a connection must stay accepted to reset the delays.
    /// </summary>
    public static TimeSpan SuccessfulConnectionTime { get; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the interval of the keep-alive pings.
    /// </summary>
    public static TimeSpan KeepAliveInterval { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets the time the connection waits for the answer to a ping.
    /// </summary>
    public static TimeSpan KeepAliveTimeout { get; } = TimeSpan.FromSeconds(30);

    // Skeleton: removed with the implementation
#pragma warning disable CA1822, S2325
    /// <summary>
    /// Gets the current status.
    /// </summary>
    public LiveSendStatus Status => new LiveSendStatus
                                    {
                                        State = LiveSendState.Connecting
                                    };

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003
    /// <summary>
    /// Determines whether the Companion sends live data.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The setting</param>
    /// <returns><see langword="true" /> when live data is sent</returns>
    public static bool IsSending(CompanionMode mode, LocalServerSetting setting)
    {
        throw new NotImplementedException();
    }
#pragma warning restore CA1822, S2325
#pragma warning restore RH2003

    /// <summary>
    /// Creates a sender that sends to the server of the forward setting.
    /// </summary>
    /// <param name="setting">The forward setting</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <returns>The sender</returns>
    public static LiveSender ForServer(ForwardServerSetting setting, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        return new LiveSender(setting.Address, _ => Task.FromResult(setting.Token), false, companionVersion, connector, timeProvider, logger);
    }

    /// <summary>
    /// Creates a sender that sends to the local server.
    /// </summary>
    /// <param name="port">The port of the local server</param>
    /// <param name="waitForToken">The delegate that waits for the token of the local server</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <returns>The sender</returns>
    public static LiveSender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        return new LiveSender(new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{port}/ingest")), waitForToken, true, companionVersion, connector, timeProvider, logger);
    }

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Queues an item of the pipe connection for sending; it never waits.
    /// </summary>
    /// <param name="item">The item</param>
    public void Enqueue(PipeConnectionItem item)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Passes a change of the pipe connection state on to the server.
    /// </summary>
    /// <param name="state">The state</param>
    public void NotifyState(PipeConnectionState state)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    // Skeleton: removed with the implementation
#pragma warning disable S2325, RH2003
    /// <summary>
    /// Connects, sends and reconnects until the token is cancelled or the server refuses for good.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>A task that completes when the run ends</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325, RH2003

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion // IAsyncDisposable
}