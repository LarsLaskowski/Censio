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
    public PipeReplayStatus Status => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Replays the recording until it ends or the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the replay</param>
    /// <returns>A task that completes when the replay has stopped</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}