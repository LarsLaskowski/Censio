using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The parts of a server the start checks create and the services resolve afterwards.
/// </summary>
internal sealed class ServerParts : IDisposable
{
    #region Properties

    /// <summary>
    /// Gets or sets the server state.
    /// </summary>
    public ServerState? State { get; set; }

    /// <summary>
    /// Gets or sets the history retention.
    /// </summary>
    public HistoryRetention? Retention { get; set; }

    /// <summary>
    /// Gets or sets the history store.
    /// </summary>
    public HistoryStore? HistoryStore { get; set; }

    /// <summary>
    /// Gets or sets the server database.
    /// </summary>
    public ServerDatabase? Database { get; set; }

    #endregion // Properties

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Retention?.Dispose();
        Database?.Dispose();
    }

    #endregion // IDisposable
}