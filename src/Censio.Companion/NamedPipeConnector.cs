using System.IO;
using System.IO.Pipes;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Connects to the statistics pipe of the game through a named pipe client.
/// </summary>
internal sealed class NamedPipeConnector : IPipeConnector
{
    #region Methods

    /// <summary>
    /// Checks whether the pipe is listed; a listing that fails counts as an existing pipe.
    /// </summary>
    /// <returns><c>true</c> if the pipe is listed or the listing failed, otherwise <c>false</c></returns>
    private static bool PipeExists()
    {
        try
        {
            return PipeEndpoint.IsListed(Directory.EnumerateFiles(@"\\.\pipe\"));
        }
        catch (Exception)
        {
            return true;
        }
    }

    #endregion // Methods

    #region IPipeConnector

    /// <inheritdoc />
    public async ValueTask<PipeConnectResult> ConnectAsync(CancellationToken cancellationToken)
    {
        if (PipeExists() == false)
        {
            return PipeConnectResult.NotFound;
        }

        var stream = new NamedPipeClientStream(PipeEndpoint.ServerName,
                                               PipeEndpoint.PipeName,
                                               PipeEndpoint.Direction,
                                               PipeEndpoint.Options,
                                               PipeEndpoint.ImpersonationLevel);

        try
        {
            await stream.ConnectAsync((int)PipeEndpoint.AttemptTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

            return PipeConnectResult.FromStream(stream);
        }
        catch (OperationCanceledException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);

            throw;
        }
        catch (Exception exception)
        {
            await stream.DisposeAsync().ConfigureAwait(false);

            return PipeConnectResult.FromFailure(exception, PipeExists());
        }
    }

    #endregion // IPipeConnector
}