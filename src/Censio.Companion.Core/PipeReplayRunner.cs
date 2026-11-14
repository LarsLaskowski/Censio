using Censio.Pipe;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Companion.Core;

/// <summary>
/// Replays a recording through the reader and the tick assembler and publishes its status.
/// </summary>
public sealed class PipeReplayRunner
{
    #region Fields

    /// <summary>
    /// Opens the recording.
    /// </summary>
    private readonly Func<Stream> _openRecording;

    /// <summary>
    /// The replay speed.
    /// </summary>
    private readonly PipeReplaySpeed _speed;

    /// <summary>
    /// The clock of the waits between the lines.
    /// </summary>
    private readonly TimeProvider _waitProvider;

    /// <summary>
    /// Guards <see cref="_status" />, <see cref="LastTick" /> and <see cref="LatestTick" />.
    /// </summary>
    private readonly Lock _statusLock = new();

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The sender that sends the replay to a server, if any.
    /// </summary>
    private readonly ReplaySender? _sender;

    /// <summary>
    /// The read stamp of the last read; it is the stamp of a session item when the assembler returns the item.
    /// </summary>
    private DateTimeOffset _lastReadStamp;

    /// <summary>
    /// The current status.
    /// </summary>
    private PipeReplayStatus _status = new()
                                       {
                                           State = PipeConnectionState.Connected
                                       };

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayRunner" /> class.
    /// </summary>
    /// <param name="openRecording">Opens the recording</param>
    /// <param name="speed">The replay speed</param>
    /// <param name="waitProvider">The clock of the waits between the lines</param>
    public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider)
        : this(openRecording, speed, waitProvider, NullLogger.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayRunner" /> class.
    /// </summary>
    /// <param name="openRecording">Opens the recording</param>
    /// <param name="speed">The replay speed</param>
    /// <param name="waitProvider">The clock of the waits between the lines</param>
    /// <param name="logger">The logger</param>
    public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider, ILogger logger)
    {
        _openRecording = openRecording;
        _speed = speed;
        _waitProvider = waitProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeReplayRunner" /> class that sends the replay to a server.
    /// </summary>
    /// <param name="openRecording">Opens the recording</param>
    /// <param name="speed">The replay speed</param>
    /// <param name="waitProvider">The clock of the waits between the lines</param>
    /// <param name="logger">The logger</param>
    /// <param name="sender">The sender that sends the replay to a server</param>
    public PipeReplayRunner(Func<Stream> openRecording, PipeReplaySpeed speed, TimeProvider waitProvider, ILogger logger, ReplaySender sender)
    {
        _openRecording = openRecording;
        _speed = speed;
        _waitProvider = waitProvider;
        _logger = logger;
        _sender = sender;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<PipeReplayStatus>? StatusChanged;

    /// <summary>
    /// Occurs when a tick has been closed.
    /// </summary>
    public event EventHandler<PipeTick>? TickReceived;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public PipeReplayStatus Status
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
                return field;
            }
        }

        private set;
    }

    /// <summary>
    /// Gets the latest closed tick whole, or <c>null</c> if none was received.
    /// </summary>
    public PipeTick? LatestTick
    {
        get
        {
            lock (_statusLock)
            {
                return field;
            }
        }

        private set;
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Replays the recording until it ends or the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>A task that completes when the replay has stopped</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_sender is not null)
        {
            await RunSendingAsync(_sender, cancellationToken).ConfigureAwait(false);

            return;
        }

        Stream recording;

        try
        {
            recording = _openRecording();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Finish(new PipeReplayProblem
                   {
                       Kind = PipeReplayProblemKind.FileNotFound
                   },
                   cancellationToken);

            return;
        }

        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        await using (recording.ConfigureAwait(false))
        {
            using var replay = new PipeReplayStream(recording, _speed, clock, _waitProvider);
            using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock, (read, stamp) => Observe(read, stamp, cancellationToken));

            try
            {
                PipeTickResult item;

                do
                {
                    item = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);

                    if (item.Tick is { } tick)
                    {
                        PublishTick(tick);
                    }
                }
                while (item.Kind != PipeTickResultKind.EndOfStream);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Finish(replay.Problem, cancellationToken);
        }
    }

    /// <summary>
    /// Replays the recording to a server: reads the token file, checks the header, connects, sends and closes.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>A task that completes when the replay has stopped</returns>
    private async Task RunSendingAsync(ReplaySender sender, CancellationToken cancellationToken)
    {
        try
        {
            if (sender.Prepare() is { } tokenProblem)
            {
                Finish(null, tokenProblem, null, cancellationToken);

                return;
            }

            if (await CheckHeaderAsync(cancellationToken).ConfigureAwait(false) is { } headerProblem)
            {
                Finish(headerProblem, cancellationToken);

                return;
            }

            Publish(new PipeReplayStatus
                    {
                        State = PipeConnectionState.Connected,
                        IsConnecting = true
                    },
                    cancellationToken);

            if (await sender.ConnectServerAsync(cancellationToken).ConfigureAwait(false) is { } connectProblem)
            {
                Finish(null, connectProblem, null, cancellationToken);

                return;
            }

            Publish(new PipeReplayStatus
                    {
                        State = PipeConnectionState.Connected
                    },
                    cancellationToken);
            sender.NotifyState(PipeConnectionState.Connected);

            await ReplayAndSendAsync(sender, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The replay is stopped: no further status.
        }
        finally
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens the recording and reads its first line to find a problem of its header.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>The problem, or <c>null</c> if the header is fine</returns>
    private async Task<PipeReplayProblem?> CheckHeaderAsync(CancellationToken cancellationToken)
    {
        Stream recording;

        try
        {
            recording = _openRecording();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new PipeReplayProblem
                   {
                       Kind = PipeReplayProblemKind.FileNotFound
                   };
        }

        await using (recording.ConfigureAwait(false))
        {
            var line = await new PipeRecordingReader(recording).ReadAsync(cancellationToken).ConfigureAwait(false);

            return line is { Kind: PipeRecordingLineKind.Problem, Problem.Kind: not PipeReplayProblemKind.InvalidLine }
                       ? line.Problem
                       : null;
        }
    }

    /// <summary>
    /// Replays the recording and sends every item to the server, then closes the connection and finishes.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>A task that completes when the replay has stopped</returns>
    private async Task ReplayAndSendAsync(ReplaySender sender, CancellationToken cancellationToken)
    {
        Stream recording;

        try
        {
            recording = _openRecording();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var closeProblem = await sender.CloseAsync(cancellationToken).ConfigureAwait(false);

            Finish(new PipeReplayProblem
                   {
                       Kind = PipeReplayProblemKind.FileNotFound
                   },
                   closeProblem,
                   null,
                   cancellationToken);

            return;
        }

        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        await using (recording.ConfigureAwait(false))
        {
            using var replay = new PipeReplayStream(recording, _speed, clock, _waitProvider);
            using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock, (read, stamp) => Observe(read, stamp, cancellationToken));

            var sendProblem = await SendItemsAsync(sender, assembler, cancellationToken).ConfigureAwait(false);

            if (sendProblem is not null)
            {
                Finish(null, sendProblem, null, cancellationToken);

                return;
            }

            var problem = await sender.CloseAsync(cancellationToken).ConfigureAwait(false);

            if (replay.Problem is not null || problem is not null)
            {
                Finish(replay.Problem, problem, null, cancellationToken);
            }
            else
            {
                Finish(null, null, sender.Counts, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Reads the items of the replay and sends them until the end or a problem of sending.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="assembler">The assembler</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>The problem of sending, or <c>null</c> if the replay reached its end</returns>
    private async Task<ReplaySendProblemKind?> SendItemsAsync(ReplaySender sender, PipeTickAssembler assembler, CancellationToken cancellationToken)
    {
        PipeTickResult item;

        do
        {
            item = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);

            var problem = await SendItemAsync(sender, item, cancellationToken).ConfigureAwait(false);

            if (problem is not null)
            {
                return problem;
            }
        }
        while (item.Kind != PipeTickResultKind.EndOfStream);

        return null;
    }

    /// <summary>
    /// Publishes and sends one item of the replay.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="item">The item</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>The problem of sending, or <c>null</c> if there is none</returns>
    private async Task<ReplaySendProblemKind?> SendItemAsync(ReplaySender sender, PipeTickResult item, CancellationToken cancellationToken)
    {
        if (item.Tick is { } tick)
        {
            PublishTick(tick);

            return await sender.SendTickAsync(tick, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset stamp;

        lock (_statusLock)
        {
            stamp = _lastReadStamp;
        }

        return item.Kind switch
               {
                   PipeTickResultKind.SessionStart => await sender.SendSessionStartAsync(stamp, cancellationToken).ConfigureAwait(false),
                   PipeTickResultKind.SessionEnd => await sender.SendSessionEndAsync(stamp, cancellationToken).ConfigureAwait(false),
                   _ => null
               };
    }

    /// <summary>
    /// Keeps a closed tick as the last one and raises <see cref="TickReceived" />.
    /// </summary>
    /// <param name="tick">The tick</param>
    private void PublishTick(PipeTick tick)
    {
        lock (_statusLock)
        {
            LastTick = PipeTickSummary.From(tick);
            LatestTick = tick;
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
                // A failing subscriber must not stop the replay
            }
        }
    }

    /// <summary>
    /// Applies the status rule to a read result.
    /// </summary>
    /// <param name="read">The read result</param>
    /// <param name="stamp">The time the read completed</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    private void Observe(PipeReadResult read, DateTimeOffset stamp, CancellationToken cancellationToken)
    {
        lock (_statusLock)
        {
            _lastReadStamp = stamp;
        }

        if (PipeStatusRule.ForRead(read) is not { State: PipeConnectionState.Receiving or PipeConnectionState.ProtocolMismatch } status)
        {
            return;
        }

        _sender?.NotifyState(status.State);

        Publish(new PipeReplayStatus
                {
                    State = status.State
                },
                cancellationToken);
    }

    /// <summary>
    /// Ends the replay, keeping the state it reached.
    /// </summary>
    /// <param name="problem">The problem that ended the replay, if any</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    private void Finish(PipeReplayProblem? problem, CancellationToken cancellationToken)
    {
        Finish(problem, null, null, cancellationToken);
    }

    /// <summary>
    /// Ends the replay that was sent to a server, keeping the state it reached.
    /// </summary>
    /// <param name="problem">The problem of the recording that ended the replay, if any</param>
    /// <param name="sendProblem">The problem of sending that ended the replay, if any</param>
    /// <param name="sent">The counts of what was sent, if the replay was sent completely</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    private void Finish(PipeReplayProblem? problem, ReplaySendProblemKind? sendProblem, ReplaySendCounts? sent, CancellationToken cancellationToken)
    {
        PipeConnectionState state;

        lock (_statusLock)
        {
            state = _status.State;
        }

        if (cancellationToken.IsCancellationRequested == false)
        {
            if (problem is null && sendProblem is null)
            {
                CompanionLog.ReplayFinished(_logger);
            }
            else if (problem is not null)
            {
                CompanionLog.ReplayProblem(_logger, TrayMenu.FormatReplayReason(problem) ?? problem.Kind.ToString());
            }
        }

        Publish(new PipeReplayStatus
                {
                    State = state,
                    IsFinished = true,
                    Problem = problem,
                    SendProblem = sendProblem,
                    Sent = sent
                },
                cancellationToken);
    }

    /// <summary>
    /// Stores a new status and raises <see cref="StatusChanged" /> unless the token is cancelled or the status is unchanged.
    /// </summary>
    /// <param name="status">The new status</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    private void Publish(PipeReplayStatus status, CancellationToken cancellationToken)
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

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<PipeReplayStatus>>())
        {
            try
            {
                handler(this, status);
            }
            catch (Exception)
            {
                // A failing subscriber must not stop the replay
            }
        }
    }

    #endregion // Methods
}