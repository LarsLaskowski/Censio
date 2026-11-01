using Censio.Pipe;

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
    /// Guards <see cref="_status" />.
    /// </summary>
    private readonly Lock _statusLock = new();

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
    {
        _openRecording = openRecording;
        _speed = speed;
        _waitProvider = waitProvider;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the status changes.
    /// </summary>
    public event EventHandler<PipeReplayStatus>? StatusChanged;

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

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Replays the recording until it ends or the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>A task that completes when the replay has stopped</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
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
            using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock, (read, _) => Observe(read, cancellationToken));

            try
            {
                PipeTickResult item;

                do
                {
                    item = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);
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
    /// Applies the status rule to a read result.
    /// </summary>
    /// <param name="read">The read result</param>
    /// <param name="cancellationToken">The token that stops the replay</param>
    private void Observe(PipeReadResult read, CancellationToken cancellationToken)
    {
        if (PipeStatusRule.ForRead(read) is not { State: PipeConnectionState.Receiving or PipeConnectionState.ProtocolMismatch } status)
        {
            return;
        }

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
        PipeConnectionState state;

        lock (_statusLock)
        {
            state = _status.State;
        }

        Publish(new PipeReplayStatus
                {
                    State = state,
                    IsFinished = true,
                    Problem = problem
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