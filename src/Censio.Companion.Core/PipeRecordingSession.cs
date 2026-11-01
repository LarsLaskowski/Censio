using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Holds the recording over all connections of a run and publishes its state.
/// </summary>
public sealed class PipeRecordingSession
{
    #region Fields

    /// <summary>
    /// Opens the file of a recording.
    /// </summary>
    private readonly Func<DateTimeOffset, Stream> _openFile;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingSession" /> class.
    /// </summary>
    /// <param name="openFile">Opens the file of a recording</param>
    public PipeRecordingSession(Func<DateTimeOffset, Stream> openFile)
    {
        _openFile = openFile;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the state of the recording.
    /// </summary>
    public PipeRecordingState State => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Begins the recording of a connection.
    /// </summary>
    /// <returns>The recorder of the connection, or <c>null</c> if nothing is recorded</returns>
    public PipeRecorder? BeginConnection()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Ends the recording of a connection.
    /// </summary>
    /// <param name="recorder">The recorder of the connection</param>
    public void EndConnection(PipeRecorder recorder)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stops the recording and waits for the pending lines.
    /// </summary>
    /// <param name="timeout">The longest time to wait</param>
    /// <returns>A task that completes when the recording has stopped</returns>
    public Task StopAsync(TimeSpan timeout)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}