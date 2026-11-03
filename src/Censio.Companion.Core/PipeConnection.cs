using Censio.Pipe;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// The recording session, or <c>null</c> if nothing is recorded.
    /// </summary>
    private readonly PipeRecordingSession? _recording;

    /// <summary>
    /// Guards <see cref="_status" /> and <see cref="_lastTick" />.
    /// </summary>
    private readonly Lock _statusLock = new();

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The current status.
    /// </summary>
    private PipeConnectionStatus _status = new()
                                           {
                                               State = PipeConnectionState.WaitingForGame
                                           };

    /// <summary>
    /// The summary of the last tick, kept across connections.
    /// </summary>
    private PipeTickSummary? _lastTick;

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
        : this(connector, timeProvider, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnection" /> class.
    /// </summary>
    /// <param name="connector">The connector that makes the attempts</param>
    /// <param name="timeProvider">The clock</param>
    /// <param name="recording">The recording session, or <c>null</c> if nothing is recorded</param>
    public PipeConnection(IPipeConnector connector, TimeProvider timeProvider, PipeRecordingSession? recording)
        : this(connector, timeProvider, recording, NullLogger.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnection" /> class.
    /// </summary>
    /// <param name="connector">The connector that makes the attempts</param>
    /// <param name="timeProvider">The clock</param>
    /// <param name="recording">The recording session, or <c>null</c> if nothing is recorded</param>
    /// <param name="logger">The logger</param>
    public PipeConnection(IPipeConnector connector, TimeProvider timeProvider, PipeRecordingSession? recording, ILogger logger)
    {
        _connector = connector;
        _timeProvider = timeProvider;
        _recording = recording;
        _logger = logger;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<PipeConnectionStatus>? StatusChanged;

    /// <summary>
    /// Occurs when a tick has been closed.
    /// </summary>
    public event EventHandler<PipeTick>? TickReceived;

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

    /// <summary>
    /// Gets the summary of the last tick, or <c>null</c> if none was received.
    /// </summary>
    public PipeTickSummary? LastTick
    {
        get
        {
            lock (_statusLock)
            {
                return _lastTick;
            }
        }
    }

    /// <summary>
    /// Gets the latest closed tick whole, or <c>null</c> if none was received.
    /// </summary>
    public PipeTick? LatestTick => throw new NotImplementedException();

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
    /// Checks whether a read result ends the session.
    /// </summary>
    /// <param name="read">The read result</param>
    /// <returns><c>true</c> if the stream cannot be read on</returns>
    private static bool IsSessionEnding(PipeReadResult read)
    {
        return read.Kind == PipeReadResultKind.EndOfStream
               || read.Error?.Kind is PipeErrorKind.TruncatedFrame or PipeErrorKind.OversizedFrame or PipeErrorKind.ReadFailed;
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
        var recorder = _recording?.BeginConnection();

        try
        {
            await using (stream.ConfigureAwait(false))
            {
                SetStatus(new PipeConnectionStatus
                          {
                              State = PipeConnectionState.Connected
                          },
                          cancellationToken);

                // The observer runs on the read, before the assembler hands the read on; it alone writes the end
                // status and the receiving time, and the loop reads them only after the end of the stream.
                var endStatus = default(PipeConnectionStatus);

                using var assembler = new PipeTickAssembler(new PipeMessageReader(stream),
                                                            _timeProvider,
                                                            (read, stamp) => endStatus = Observe(recorder, read, stamp, endStatus, cancellationToken));

                while (true)
                {
                    PipeTickResult item;

                    try
                    {
                        item = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);
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

                    if (item.Kind == PipeTickResultKind.EndOfStream)
                    {
                        break;
                    }

                    if (item.Tick is { } tick)
                    {
                        PublishTick(tick);
                    }
                }

                var wasLong = _receivingSince is { } since && _timeProvider.GetUtcNow() - since >= PipeRetryDelay.SuccessfulConnectionTime;

                _receivingSince = null;

                return (endStatus, wasLong);
            }
        }
        finally
        {
            if (recorder is not null)
            {
                _recording!.EndConnection(recorder);
            }
        }
    }

    /// <summary>
    /// Handles a read as it arrives: records it, applies the status rule and keeps the receiving time.
    /// </summary>
    /// <param name="recorder">The recording of the connection, or <c>null</c></param>
    /// <param name="read">The read</param>
    /// <param name="stamp">The time of the read</param>
    /// <param name="endStatus">The end status so far</param>
    /// <param name="cancellationToken">The token that stops the run</param>
    /// <returns>The end status after the read</returns>
    private PipeConnectionStatus Observe(PipeRecordingConnection? recorder, PipeReadResult read, DateTimeOffset stamp, PipeConnectionStatus endStatus, CancellationToken cancellationToken)
    {
        recorder?.Record(read, stamp);

        var status = PipeStatusRule.ForRead(read);

        if (IsSessionEnding(read))
        {
            return status!.Value;
        }

        if (status is not { } next)
        {
            return endStatus;
        }

        if (next.State == PipeConnectionState.Receiving)
        {
            EnterReceiving(stamp, cancellationToken);
        }
        else
        {
            LeaveReceiving();
            SetStatus(next, cancellationToken);
        }

        return endStatus;
    }

    /// <summary>
    /// Keeps a closed tick as the last one and raises <see cref="TickReceived" />.
    /// </summary>
    /// <param name="tick">The tick</param>
    private void PublishTick(PipeTick tick)
    {
        lock (_statusLock)
        {
            _lastTick = PipeTickSummary.From(tick);
        }

        var handlers = TickReceived;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<PipeTick>>())
        {
            try
            {
                handler(this, tick);
            }
            catch (Exception)
            {
                // A failing subscriber must not stop the connection
            }
        }
    }

    /// <summary>
    /// Enters the receiving state and stamps the time if the connection was not in it.
    /// </summary>
    /// <param name="stamp">The time of the read that entered the receiving state</param>
    /// <param name="cancellationToken">The token that stops the run</param>
    private void EnterReceiving(DateTimeOffset stamp, CancellationToken cancellationToken)
    {
        _receivingSince ??= stamp;

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

        CompanionLog.ConnectionStateChanged(_logger, status.State, status.ErrorKind, status.ErrorCode, status.ReceivedVersion);

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