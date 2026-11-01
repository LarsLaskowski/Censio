using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Keeps the connection to the statistics pipe: connects, reads, reconnects and publishes its status.
/// </summary>
public sealed class PipeConnection
{
    #region Fields

    /// <summary>
    /// The connector that makes the attempts.
    /// </summary>
    private readonly IPipeConnector _connector;

    /// <summary>
    /// The clock of the retry delays and the connection time.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Guards <see cref="_status" />.
    /// </summary>
    private readonly Lock _statusLock = new();

    /// <summary>
    /// The current status.
    /// </summary>
    private PipeConnectionStatus _status = new()
                                           {
                                               State = PipeConnectionState.WaitingForGame
                                           };

    /// <summary>
    /// Whether a run is in progress; 1 if so, otherwise 0.
    /// </summary>
    private int _running;

    /// <summary>
    /// The time the connection entered the receiving state, if it is in it.
    /// </summary>
    private DateTimeOffset? _receivingSince;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnection" /> class.
    /// </summary>
    /// <param name="connector">The connector that makes the attempts</param>
    /// <param name="timeProvider">The clock</param>
    public PipeConnection(IPipeConnector connector, TimeProvider timeProvider)
    {
        _connector = connector;
        _timeProvider = timeProvider;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<PipeConnectionStatus>? StatusChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public PipeConnectionStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Connects and reconnects until the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>A task that completes when the run has stopped</returns>
    /// <exception cref="InvalidOperationException">A run is already in progress</exception>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            throw new InvalidOperationException("The connection is already running.");
        }

        return RunLoopAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether an error of the pipe ends the session.
    /// </summary>
    /// <param name="kind">The kind of the error</param>
    /// <returns><c>true</c> if the stream cannot be read on</returns>
    private static bool IsStreamEnding(PipeErrorKind kind)
    {
        return kind is PipeErrorKind.TruncatedFrame or PipeErrorKind.OversizedFrame or PipeErrorKind.ReadFailed;
    }

    /// <summary>
    /// Creates the status of a pipe error.
    /// </summary>
    /// <param name="kind">The kind of the error</param>
    /// <returns>The status</returns>
    private static PipeConnectionStatus ErrorStatus(PipeErrorKind kind)
    {
        return new PipeConnectionStatus
               {
                   State = PipeConnectionState.Error,
                   ErrorKind = kind
               };
    }

    /// <summary>
    /// Runs the attempts, sessions and delays until the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>A task that completes when the run has stopped</returns>
    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var failures = 0;

            _receivingSince = null;

            while (cancellationToken.IsCancellationRequested == false)
            {
                var (endStatus, connectionWasLong) = await AttemptAsync(cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                failures = connectionWasLong ? 1 : failures + 1;

                SetStatus(endStatus, cancellationToken);

                await Task.Delay(PipeRetryDelay.For(failures), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The stop ends the run normally
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>
    /// Makes one attempt and, when it connects, runs the session.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>The status the attempt or the session ended with, and whether a receiving connection lasted long enough to reset the failures</returns>
    private async Task<(PipeConnectionStatus Status, bool WasLong)> AttemptAsync(CancellationToken cancellationToken)
    {
        PipeConnectResult result;

        try
        {
            result = await _connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested == false || exception is not OperationCanceledException)
        {
            result = PipeConnectResult.FromFailure(exception, true);
        }
        catch (OperationCanceledException)
        {
            return (default, false);
        }

        switch (result.Kind)
        {
            case PipeConnectResultKind.Connected:
                return await RunSessionAsync(result.Stream!, cancellationToken).ConfigureAwait(false);

            case PipeConnectResultKind.Busy:
                return (new PipeConnectionStatus
                        {
                            State = PipeConnectionState.PipeBusy
                        }, false);

            case PipeConnectResultKind.Failed:
                return (new PipeConnectionStatus
                        {
                            State = PipeConnectionState.Error,
                            ErrorCode = result.ErrorCode
                        }, false);

            default:
                return (new PipeConnectionStatus
                        {
                            State = PipeConnectionState.WaitingForGame
                        }, false);
        }
    }

    /// <summary>
    /// Reads the messages of a connected stream until the session ends, and disposes the stream.
    /// </summary>
    /// <param name="stream">The connected stream</param>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>The status the session ended with, and whether a receiving connection lasted long enough to reset the failures</returns>
    private async Task<(PipeConnectionStatus Status, bool WasLong)> RunSessionAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using (stream.ConfigureAwait(false))
        {
            SetStatus(new PipeConnectionStatus
                      {
                          State = PipeConnectionState.Connected
                      },
                      cancellationToken);

            var reader = new PipeMessageReader(stream);
            PipeConnectionStatus endStatus;

            while (true)
            {
                PipeReadResult read;

                try
                {
                    read = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return (default, false);
                }
                catch (Exception)
                {
                    endStatus = ErrorStatus(PipeErrorKind.ReadFailed);

                    break;
                }

                if (read.Kind == PipeReadResultKind.Message)
                {
                    EnterReceiving(cancellationToken);
                }
                else if (read.Kind == PipeReadResultKind.EndOfStream)
                {
                    endStatus = new PipeConnectionStatus
                                {
                                    State = PipeConnectionState.WaitingForGame
                                };

                    break;
                }
                else if (read.Error!.Kind == PipeErrorKind.ProtocolMismatch)
                {
                    LeaveReceiving();
                    SetStatus(new PipeConnectionStatus
                              {
                                  State = PipeConnectionState.ProtocolMismatch,
                                  ReceivedVersion = read.Error.ReceivedVersion
                              },
                              cancellationToken);
                }
                else if (IsStreamEnding(read.Error.Kind))
                {
                    endStatus = ErrorStatus(read.Error.Kind);

                    break;
                }
            }

            var wasLong = _receivingSince is { } since && _timeProvider.GetUtcNow() - since >= PipeRetryDelay.SuccessfulConnectionTime;

            _receivingSince = null;

            return (endStatus, wasLong);
        }
    }

    /// <summary>
    /// Enters the receiving state and stamps the time if the connection was not in it.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the run</param>
    private void EnterReceiving(CancellationToken cancellationToken)
    {
        _receivingSince ??= _timeProvider.GetUtcNow();

        SetStatus(new PipeConnectionStatus
                  {
                      State = PipeConnectionState.Receiving
                  },
                  cancellationToken);
    }

    /// <summary>
    /// Clears the receiving time stamp.
    /// </summary>
    private void LeaveReceiving()
    {
        _receivingSince = null;
    }

    /// <summary>
    /// Stores a new status and raises <see cref="StatusChanged" /> unless the token is cancelled or the status is unchanged.
    /// </summary>
    /// <param name="status">The new status</param>
    /// <param name="cancellationToken">The token that stops the run</param>
    private void SetStatus(PipeConnectionStatus status, CancellationToken cancellationToken)
    {
        lock (_statusLock)
        {
            if (cancellationToken.IsCancellationRequested || _status == status)
            {
                return;
            }

            _status = status;
        }

        var handlers = StatusChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<PipeConnectionStatus>>())
        {
            try
            {
                handler(this, status);
            }
            catch (Exception)
            {
                // A failing subscriber must not stop the connection
            }
        }
    }

    #endregion // Methods
}