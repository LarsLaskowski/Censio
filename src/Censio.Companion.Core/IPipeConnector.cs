namespace Censio.Companion.Core;

/// <summary>
/// Makes one attempt to connect to the statistics pipe.
/// </summary>
public interface IPipeConnector
{
    #region Methods

    /// <summary>
    /// Makes one connect attempt. Throws only <see cref="OperationCanceledException" />; the caller owns and
    /// disposes the stream of a connected result.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the attempt</param>
    /// <returns>The result of the attempt</returns>
    ValueTask<PipeConnectResult> ConnectAsync(CancellationToken cancellationToken);

    #endregion // Methods
}