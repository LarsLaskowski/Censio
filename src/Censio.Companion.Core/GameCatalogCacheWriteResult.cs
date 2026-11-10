namespace Censio.Companion.Core;

/// <summary>
/// Holds the outcome of writing the catalog cache.
/// </summary>
public sealed class GameCatalogCacheWriteResult
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether the cache was written.
    /// </summary>
    public required bool IsWritten { get; init; }

    /// <summary>
    /// Gets the exception that stopped the write, if any.
    /// </summary>
    public Exception? Failure { get; init; }

    #endregion // Properties
}