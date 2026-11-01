using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Connects to the statistics pipe of the game through a named pipe client.
/// </summary>
internal sealed class NamedPipeConnector : IPipeConnector
{
    #region IPipeConnector

    /// <inheritdoc />
    public ValueTask<PipeConnectResult> ConnectAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #endregion // IPipeConnector
}