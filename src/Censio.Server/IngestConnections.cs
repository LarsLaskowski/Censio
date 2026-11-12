namespace Censio.Server;

/// <summary>
/// Counts the connections of the ingest endpoint: the pending hellos and the one Companion.
/// </summary>
public sealed class IngestConnections
{
    #region Constants

    /// <summary>
    /// The maximum number of connections that wait for their hello at the same time.
    /// </summary>
    public const int MaxPendingHellos = 16;

    #endregion // Constants

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325

    /// <summary>
    /// Tries to count a connection that waits for its hello.
    /// </summary>
    /// <returns><c>true</c> when the connection was counted</returns>
    public bool TryEnterPendingHello()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Releases a connection that waited for its hello.
    /// </summary>
    public void LeavePendingHello()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tries to take the place of the one Companion.
    /// </summary>
    /// <returns><c>true</c> when the place was taken</returns>
    public bool TryTakeCompanion()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Releases the place of the Companion.
    /// </summary>
    public void ReleaseCompanion()
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S2325

    #endregion // Methods
}