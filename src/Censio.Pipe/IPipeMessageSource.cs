namespace Censio.Pipe;

/// <summary>
/// A source of typed pipe messages: the reader of a pipe stream or a replay.
/// </summary>
public interface IPipeMessageSource
{
    #region Methods

    /// <summary>
    /// Reads the next message.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The message, the error of the frame, or the end of the stream</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    ValueTask<PipeReadResult> ReadAsync(CancellationToken cancellationToken);

    #endregion // Methods
}