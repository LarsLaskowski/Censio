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
    public PipeRecordingState State => (PipeRecordingState)Volatile.Read(ref _state);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Begins the recording of a connection.
    /// </summary>
    /// <returns>The recorder of the connection, or <c>null</c> if nothing is recorded</returns>
    public PipeRecorder? BeginConnection()
    {
        if (State == PipeRecordingState.Failed)
        {
            return null;
        }

        var recorder = new PipeRecorder(_openFile);

        recorder.Failed += OnRecorderFailed;

        lock (_lock)
        {
            _recorders.Add(recorder);
        }

        return recorder;
    }

    /// <summary>
    /// Ends the recording of a connection.
    /// </summary>
    /// <param name="recorder">The recorder of the connection</param>
    public void EndConnection(PipeRecorder recorder)
    {
        _ = FinishAsync(recorder);
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
    /// Completes a recorder and forgets it afterwards.
    /// </summary>
    /// <param name="recorder">The recorder</param>
    /// <returns>A task that completes when the recorder has written its lines</returns>
    private async Task FinishAsync(PipeRecorder recorder)
    {
        try
        {
            await recorder.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_lock)
            {
                _recorders.Remove(recorder);
            }
        }
    }

    /// <summary>
    /// Handles the failure of a recorder.
    /// </summary>
    /// <param name="sender">The recorder</param>
    /// <param name="eventArgs">The event data</param>
    private void OnRecorderFailed(object? sender, EventArgs eventArgs)
    {
        SetFailed();
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