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

    #region Fields

    /// <summary>
    /// The number of connections that wait for their hello.
    /// </summary>
    private int _pendingHellos;

    /// <summary>
    /// A value indicating whether a Companion holds its place (0 free, 1 taken).
    /// </summary>
    private int _companionTaken;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Tries to count a connection that waits for its hello.
    /// </summary>
    /// <returns><c>true</c> when the connection was counted</returns>
    public bool TryEnterPendingHello()
    {
        while (true)
        {
            var current = Volatile.Read(ref _pendingHellos);

            if (current >= MaxPendingHellos)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _pendingHellos, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Releases a connection that waited for its hello.
    /// </summary>
    public void LeavePendingHello()
    {
        Interlocked.Decrement(ref _pendingHellos);
    }

    /// <summary>
    /// Tries to take the place of the one Companion.
    /// </summary>
    /// <returns><c>true</c> when the place was taken</returns>
    public bool TryTakeCompanion()
    {
        return Interlocked.CompareExchange(ref _companionTaken, 1, 0) == 0;
    }

    /// <summary>
    /// Releases the place of the Companion.
    /// </summary>
    public void ReleaseCompanion()
    {
        Volatile.Write(ref _companionTaken, 0);
    }

    #endregion // Methods
}