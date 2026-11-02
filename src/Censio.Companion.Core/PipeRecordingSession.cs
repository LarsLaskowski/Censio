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
    /// Guards <see cref="_recorders" />, <see cref="_connections" />, the state changes and the recorder of every connection.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The recorders that have not finished writing yet.
    /// </summary>
    private readonly List<PipeRecorder> _recorders = [];

    /// <summary>
    /// The connections that have begun and not ended.
    /// </summary>
    private readonly List<PipeRecordingConnection> _connections = [];

    /// <summary>
    /// The state; the value of a <see cref="PipeRecordingState" />.
    /// </summary>
    private int _state = (int)PipeRecordingState.Off;

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
    /// Starts the recording; nothing happens while it is recording.
    /// </summary>
    public void Start()
    {
        Transition(true);
    }

    /// <summary>
    /// Stops the recording; nothing happens unless it is recording.
    /// </summary>
    public void Stop()
    {
        Transition(false);
    }

    /// <summary>
    /// Stops the recording if it is running, otherwise starts it.
    /// </summary>
    public void Toggle()
    {
        Transition(null);
    }

    /// <summary>
    /// Begins the recording of a connection.
    /// </summary>
    /// <returns>The recording of the connection</returns>
    public PipeRecordingConnection BeginConnection()
    {
        var connection = new PipeRecordingConnection(this);

        lock (_lock)
        {
            _connections.Add(connection);
        }

        return connection;
    }

    /// <summary>
    /// Ends the recording of a connection.
    /// </summary>
    /// <param name="connection">The recording of the connection</param>
    public void EndConnection(PipeRecordingConnection connection)
    {
        PipeRecorder? recorder;

        lock (_lock)
        {
            _connections.Remove(connection);

            recorder = connection.Recorder;
            connection.Recorder = null;
        }

        if (recorder is not null)
        {
            _ = FinishAsync(recorder);
        }
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
            var recording = false;

            lock (_lock)
            {
                if (State == PipeRecordingState.Recording)
                {
                    recording = true;
                    Volatile.Write(ref _state, (int)PipeRecordingState.Failed);
                }
            }

            if (recording)
            {
                CompanionLog.RecordingFailed(_logger);
                RaiseStateChanged();
            }
            else
            {
                CompanionLog.StoppedRecordingFailed(_logger);
            }
        }
    }

    /// <summary>
    /// Records a read of a connection if the session is recording.
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="read">The read</param>
    /// <param name="receiveTime">The time the read was received</param>
    internal void Record(PipeRecordingConnection connection, PipeReadResult read, DateTimeOffset receiveTime)
    {
        var isVersion = read.Frame is { Length: > 0 } frame && frame.Span[0] == 0;
        PipeRecorder recorder;
        PipeReadResult? version = null;

        lock (_lock)
        {
            if (isVersion)
            {
                connection.LastVersion = read;
            }

            if (State != PipeRecordingState.Recording || read.Frame is null)
            {
                return;
            }

            if (connection.Recorder is null)
            {
                var created = new PipeRecorder(_openFile);

                created.Failed += (_, _) => OnRecorderFailed(connection, created);
                _recorders.Add(created);
                connection.Recorder = created;

                if (isVersion == false)
                {
                    version = connection.LastVersion;
                }
            }

            recorder = connection.Recorder;
        }

        if (version is not null)
        {
            recorder.Record(version, receiveTime);
        }

        recorder.Record(read, receiveTime);
    }

    /// <summary>
    /// Changes the state of the recording.
    /// </summary>
    /// <param name="start"><see langword="true" /> to start, <see langword="false" /> to stop, <see langword="null" /> to toggle</param>
    private void Transition(bool? start)
    {
        List<PipeRecorder> detached = [];

        lock (_lock)
        {
            var recording = State == PipeRecordingState.Recording;

            if ((start ?? recording == false) == recording)
            {
                return;
            }

            foreach (var connection in _connections)
            {
                if (connection.Recorder is { } recorder)
                {
                    detached.Add(recorder);
                    connection.Recorder = null;
                }
            }

            if (recording)
            {
                Volatile.Write(ref _state, (int)PipeRecordingState.Off);
                CompanionLog.RecordingStopped(_logger);
            }
            else
            {
                Volatile.Write(ref _state, (int)PipeRecordingState.Recording);
                CompanionLog.RecordingStarted(_logger);
            }
        }

        foreach (var recorder in detached)
        {
            _ = FinishAsync(recorder);
        }

        RaiseStateChanged();
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
    /// Handles the failure of a recorder: the active one of a connection fails the recording, a stopped one is only logged.
    /// </summary>
    /// <param name="connection">The connection the recorder was created for</param>
    /// <param name="recorder">The recorder</param>
    private void OnRecorderFailed(PipeRecordingConnection connection, PipeRecorder recorder)
    {
        bool active;
        var changed = false;

        lock (_lock)
        {
            active = ReferenceEquals(connection.Recorder, recorder);

            if (active)
            {
                connection.Recorder = null;
                changed = State != PipeRecordingState.Failed;
                Volatile.Write(ref _state, (int)PipeRecordingState.Failed);
            }
        }

        if (active)
        {
            if (changed)
            {
                CompanionLog.RecordingFailed(_logger);
                RaiseStateChanged();
            }
        }
        else
        {
            CompanionLog.StoppedRecordingFailed(_logger);
        }
    }

    /// <summary>
    /// Raises <see cref="StateChanged" />; a failing subscriber does not stop the others.
    /// </summary>
    private void RaiseStateChanged()
    {
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