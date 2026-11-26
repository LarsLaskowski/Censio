using System.Globalization;

using Censio.Contracts;
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
    public const int MaxReplyBytes = IngestClient.MaxReplyBytes;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The address of the server.
    /// </summary>
    private readonly Uri _address;

    /// <summary>
    /// The path of the token file, if any.
    /// </summary>
    private readonly string? _tokenFilePath;

    /// <summary>
    /// The delegate that waits for the token of the local server, or <see langword="null" /> for a remote server.
    /// </summary>
    private readonly Func<CancellationToken, Task<string?>>? _waitForToken;

    /// <summary>
    /// The version of the Companion.
    /// </summary>
    private readonly string _companionVersion;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The connection to the server.
    /// </summary>
    private readonly IngestClient _client;

    /// <summary>
    /// The lock of the counts.
    /// </summary>
    private readonly Lock _countLock = new();

    /// <summary>
    /// The token read from the token file, if any.
    /// </summary>
    private string? _token;

    /// <summary>
    /// The number of ticks sent.
    /// </summary>
    private int _ticks;

    /// <summary>
    /// The number of messages refused.
    /// </summary>
    private int _refused;

    /// <summary>
    /// Whether the server accepted the hello.
    /// </summary>
    private bool _isConnected;

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
        : this(address, tokenFilePath, null, companionVersion, connector, timeProvider, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySender" /> class.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="tokenFilePath">The path of the token file, if any</param>
    /// <param name="waitForToken">The delegate that waits for the token of the local server, if any</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    private ReplaySender(Uri address, string? tokenFilePath, Func<CancellationToken, Task<string?>>? waitForToken, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        _waitForToken = waitForToken;
        _address = address;
        _tokenFilePath = tokenFilePath;
        _companionVersion = companionVersion;
        _logger = logger;
        _client = new IngestClient(connector, timeProvider);
        _client.Failed += OnClientFailed;

        ProfileId = Guid.NewGuid();
        ProfileName = "Replay " + timeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the time the sender waits for a reply of the server.
    /// </summary>
    public static TimeSpan ReplyTimeout => IngestClient.ReplyTimeout;

    /// <summary>
    /// Gets the identity of the profile the replay is sent to.
    /// </summary>
    public Guid ProfileId { get; }

    /// <summary>
    /// Gets the name of the profile the replay is sent to.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Gets the counts of what was sent.
    /// </summary>
    public ReplaySendCounts Counts
    {
        get
        {
            lock (_countLock)
            {
                return new ReplaySendCounts
                       {
                           Ticks = _ticks,
                           Refused = _refused
                       };
            }
        }
    }

    #endregion // Properties

    #region Methods

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
    public static ReplaySender ForLocalServer(int port, Func<CancellationToken, Task<string?>> waitForToken, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        return new ReplaySender(new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{port}/ingest")), null, waitForToken, companionVersion, connector, timeProvider, logger);
    }

    /// <summary>
    /// Reads the token file, if one is named.
    /// </summary>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public ReplaySendProblemKind? Prepare()
    {
        if (_tokenFilePath is null)
        {
            return null;
        }

        var result = ReplayTokenFile.ReadFile(_tokenFilePath);

        if (result.Kind == ReplayTokenFileResultKind.Read)
        {
            _token = result.Token;

            return null;
        }

        var problem = result.Kind == ReplayTokenFileResultKind.CannotBeRead
                          ? ReplaySendProblemKind.TokenFileCannotBeRead
                          : ReplaySendProblemKind.TokenFileHoldsNoValidToken;

        CompanionLog.ReplaySendFailed(_logger, Reason(problem));

        return problem;
    }

    /// <summary>
    /// Connects to the server and sends the hello.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> ConnectServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_waitForToken is not null)
            {
                _token = await _waitForToken(cancellationToken).ConfigureAwait(false);

                if (_token is null)
                {
                    return _client.Fail(ReplaySendProblemKind.LocalServerNotRunning);
                }
            }

            var hello = new ContractHello
                        {
                            Token = _token,
                            CompanionVersion = _companionVersion,
                            ProfileId = ProfileId,
                            ProfileName = ProfileName
                        };
            var problem = await _client.OpenAsync(_address, hello, cancellationToken).ConfigureAwait(false);

            if (problem is null)
            {
                _isConnected = true;

                CompanionLog.ReplaySendConnected(_logger);
            }

            return problem;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return _client.Fail(ReplaySendProblemKind.ConnectionLost);
        }
    }

    /// <summary>
    /// Passes a change of the connection state on to the server.
    /// </summary>
    /// <param name="state">The state</param>
    public void NotifyState(PipeConnectionState state)
    {
        _client.NotifyState(state);
    }

    /// <summary>
    /// Sends a tick and waits for the acknowledgement.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendTickAsync(PipeTick tick, CancellationToken cancellationToken)
    {
        if (tick.Islands.Count == 0)
        {
            return null;
        }

        var message = ContractMessageConverter.ToTick(tick, ProfileId);

        return await SendMessageAsync(message, message.Id, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the start of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the start was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendSessionStartAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        var message = ContractMessageConverter.ToSessionStart(ProfileId, receiveTime);

        return await SendMessageAsync(message, message.Id, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the end of a session and waits for the acknowledgement.
    /// </summary>
    /// <param name="receiveTime">The time the end was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendSessionEndAsync(DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        var message = ContractMessageConverter.ToSessionEnd(ProfileId, receiveTime);

        return await SendMessageAsync(message, message.Id, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the trade routes and waits for the acknowledgement.
    /// </summary>
    /// <param name="message">The trade-routes message</param>
    /// <param name="receiveTime">The time the message was received</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> SendTradeRoutesAsync(TradeRoutesMessage message, DateTimeOffset receiveTime, CancellationToken cancellationToken)
    {
        var routes = ContractMessageConverter.ToTradeRoutes(message, ProfileId, receiveTime);

        return await SendMessageAsync(routes, routes.Id, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    public async Task<ReplaySendProblemKind?> CloseAsync(CancellationToken cancellationToken)
    {
        var problem = await _client.CloseAsync(cancellationToken).ConfigureAwait(false);

        if (problem is null && _isConnected)
        {
            var counts = Counts;

            CompanionLog.ReplaySendFinished(_logger, counts.Ticks, counts.Refused);
        }

        return problem;
    }

    /// <summary>
    /// Gives the reason of a problem as the tray shows it.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The reason</returns>
    private static string Reason(ReplaySendProblemKind problem)
    {
        return TrayMenu.FormatReplaySendReason(CompanionTexts.English, problem) ?? problem.ToString();
    }

    /// <summary>
    /// Sends a message and waits for its acknowledgement.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="id">The identity of the message</param>
    /// <param name="isTick">Whether the message is a tick, which is counted when it is accepted</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The problem, or <c>null</c> if there is none</returns>
    private async Task<ReplaySendProblemKind?> SendMessageAsync(ContractMessage message, Guid id, bool isTick, CancellationToken cancellationToken)
    {
        var written = ContractWriter.Write(message);

        if (written.Error is not null)
        {
            CountRefused();

            return null;
        }

        var acknowledgement = await _client.SendAsync(written.Utf8Json, id, cancellationToken).ConfigureAwait(false);

        if (acknowledgement is null)
        {
            return _client.Failure;
        }

        Count(acknowledgement.Refusal is not null, isTick);

        return null;
    }

    /// <summary>
    /// Counts the result of a message.
    /// </summary>
    /// <param name="isRefused">Whether the server refused the message</param>
    /// <param name="isTick">Whether the message is a tick</param>
    private void Count(bool isRefused, bool isTick)
    {
        lock (_countLock)
        {
            if (isRefused)
            {
                _refused++;
            }
            else if (isTick)
            {
                _ticks++;
            }
        }
    }

    /// <summary>
    /// Counts a message that was refused before it was sent.
    /// </summary>
    private void CountRefused()
    {
        Count(true, false);
    }

    /// <summary>
    /// Logs the first problem of the connection.
    /// </summary>
    /// <param name="sender">The client</param>
    /// <param name="problem">The problem</param>
    private void OnClientFailed(object? sender, ReplaySendProblemKind problem)
    {
        LogFailure(problem);
    }

    /// <summary>
    /// Logs a problem with its reason.
    /// </summary>
    /// <param name="problem">The problem</param>
    private void LogFailure(ReplaySendProblemKind problem)
    {
        if (problem is ReplaySendProblemKind.TokenRefused or ReplaySendProblemKind.CompanionConnected or ReplaySendProblemKind.VersionNotSupported)
        {
            CompanionLog.ReplaySendRefused(_logger, Reason(problem));
        }
        else
        {
            CompanionLog.ReplaySendFailed(_logger, Reason(problem));
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    #endregion // IAsyncDisposable
}