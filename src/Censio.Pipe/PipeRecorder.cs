namespace Censio.Pipe;

/// <summary>
/// Writes the frames of the pipe to a recording file on a background writer without ever waiting.
/// </summary>
public class PipeRecorder : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The most bytes that may be queued for the writer.
    /// </summary>
    public const long MaxQueuedBytes = 16777216;

    /// <summary>
    /// The bytes counted for each queued entry on top of its encoded frame.
    /// </summary>
    public const int EntryOverhead = 128;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The factory that opens the recording file for a start time.
    /// </summary>
    private readonly Func<DateTimeOffset, Stream> _openFile;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecorder" /> class.
    /// </summary>
    /// <param name="openFile">The factory that opens the recording file for a start time</param>
    public PipeRecorder(Func<DateTimeOffset, Stream> openFile)
    {
        _openFile = openFile;
    }

    #endregion // Constructors

    #region Events

    /// <summary>
    /// Raised once when the recorder has failed.
    /// </summary>
    public event EventHandler? Failed;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the recorder has failed.
    /// </summary>
    public bool HasFailed { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Calculates the queue cost of a frame.
    /// </summary>
    /// <param name="frameLength">The frame length in bytes</param>
    /// <returns>The cost in bytes</returns>
    public static long EntryCost(int frameLength)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Queues the frame of a read result for writing.
    /// </summary>
    /// <param name="result">The read result</param>
    /// <param name="receiveTime">The time the result was received</param>
    public void Record(PipeReadResult result, DateTimeOffset receiveTime)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Closes the queue and waits for the writer.
    /// </summary>
    /// <param name="cancellationToken">The token that stops the wait</param>
    /// <returns>The task</returns>
    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    #endregion // IAsyncDisposable
}