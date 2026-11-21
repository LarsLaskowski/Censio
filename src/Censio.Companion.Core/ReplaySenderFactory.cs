using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Chooses the sender of a replay.
/// </summary>
public static class ReplaySenderFactory
{
    #region Methods

    /// <summary>
    /// Creates the sender of a replay.
    /// </summary>
    /// <param name="commandLine">The command line</param>
    /// <param name="localServer">The local server session, or <see langword="null" /> when none is hosted</param>
    /// <param name="companionVersion">The version of the Companion</param>
    /// <param name="connector">The connector</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    /// <returns>The sender, or <see langword="null" /> when the replay is not sent</returns>
    public static ReplaySender? Create(CompanionCommandLine commandLine, LocalServerSession? localServer, string companionVersion, IReplayServerConnector connector, TimeProvider timeProvider, ILogger logger)
    {
        if (commandLine.ServerAddress is { } address)
        {
            return new ReplaySender(address, commandLine.TokenFilePath, companionVersion, connector, timeProvider, logger);
        }

        return localServer is null
                   ? null
                   : ReplaySender.ForLocalServer(localServer.Status.Port, localServer.WaitForTokenAsync, companionVersion, connector, timeProvider, logger);
    }

    #endregion // Methods
}