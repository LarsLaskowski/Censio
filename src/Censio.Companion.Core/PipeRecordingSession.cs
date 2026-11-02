using Censio.Pipe;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Guards <see cref="_recorders" />.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The recorders that have not finished writing yet.
    /// </summary>
    private readonly List<PipeRecorder> _recorders = [];

    /// <summary>
    /// The state; the value of a <see cref="PipeRecordingState" />.
    /// </summary>
    private int _state = (int)PipeRecordingState.Recording;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingSession" /> class.
    /// </summary>
    /// <param name="openFile">Opens the file of a recording</param>
    public PipeRecordingSession(Func<DateTimeOffset, Stream> openFile)
        : this(openFile, NullLogger.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingSession" /> class.
    /// </summary>
    /// <param name="openFile">Opens the file of a recording</param>
    /// <param name="logger">The logger</param>
    public PipeRecordingSession(Func<DateTimeOffset, Stream> openFile, ILogger logger)
    {
        _openFile = openFile;
        _logger = logger;
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
    public PipeRecordingState State => (PipeRecordingState)Volatile.Read(ref _state);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Starts the recording.
    /// </summary>
    public void Start()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stops the recording.
    /// </summary>
    public void Stop()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stops the recording if it is running, otherwise starts it.
    /// </summary>
    public void Toggle()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Begins the recording of a connection.
    /// </summary>
    /// <returns>The recording of the connection</returns>
    public PipeRecordingConnection BeginConnection()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Ends the recording of a connection.
    /// </summary>
    /// <param name="connection">The recording of the connection</param>
    public void EndConnection(PipeRecordingConnection connection)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Stops the recording and waits for the pending lines.
    /// </summary>
    /// <param name="timeout">The longest time to wait</param>
    /// <returns>A task that completes when the recording has stopped</returns>
    public async Task StopAsync(TimeSpan timeout)
    {
        PipeRecorder[] recorders;

        lock (_lock)
        {
            recorders = [.. _recorders];
        }

        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            await Task.WhenAll(recorders.Select(recorder => recorder.CompleteAsync(cancellation.Token))).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The disk did not finish in time: the last lines may be missing
            SetFailed();
        }
    }

    /// <summary>
    /// Sets the failed state and raises <see cref="StateChanged" /> if the state was not failed.
    /// </summary>
    private void SetFailed()
    {
        if (Interlocked.Exchange(ref _state, (int)PipeRecordingState.Failed) == (int)PipeRecordingState.Failed)
        {
            return;
        }

        var handlers = StateChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // A failing subscriber must not stop the recording
            }
        }
    }

    #endregion // Methods
}