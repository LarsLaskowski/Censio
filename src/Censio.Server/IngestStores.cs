namespace Censio.Server;

/// <summary>
/// The stores an ingest session writes to.
/// </summary>
public sealed class IngestStores
{
    #region Properties

    /// <summary>
    /// Gets the server state the messages are stored through.
    /// </summary>
    public required ServerState State { get; init; }

    /// <summary>
    /// Gets the history retention that deletes the history of a profile.
    /// </summary>
    public required HistoryRetention History { get; init; }

    #endregion // Properties
}