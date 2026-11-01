namespace Censio.Pipe;

/// <summary>
/// Reads messages from a source and delivers them as ticks and session events on an injected clock.
/// </summary>
public sealed class PipeTickAssembler : IDisposable
{
    #region Fields

    /// <summary>
    /// The source of the messages.
    /// </summary>
    private readonly IPipeMessageSource _source;

    /// <summary>
    /// The clock that stamps the receive times and times the quiet period.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeTickAssembler" /> class.
    /// </summary>
    /// <param name="source">The source of the messages</param>
    /// <param name="timeProvider">The clock</param>
    public PipeTickAssembler(IPipeMessageSource source, TimeProvider timeProvider)
    {
        _source = source;
        _timeProvider = timeProvider;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Reads the next tick or session event.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The next result, or the end of the stream</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The assembler was disposed.</exception>
    public ValueTask<PipeTickResult> ReadAsync(CancellationToken cancellationToken)
    {
        _ = _source;
        _ = _timeProvider;

        throw new NotImplementedException();
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion // IDisposable
}