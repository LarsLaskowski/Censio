namespace Censio.Server;

/// <summary>
/// The sparklines of a set of keys read from the history.
/// </summary>
/// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
public sealed class SparklineRead<TKey>
    where TKey : notnull
{
    #region Properties

    /// <summary>
    /// Gets the line per key, or <see langword="null"/> for a key with fewer than two valued points.
    /// </summary>
    public required IReadOnlyDictionary<TKey, SparklineView?> Lines { get; init; }

    /// <summary>
    /// Gets the samples per key.
    /// </summary>
    public required IReadOnlyDictionary<TKey, IReadOnlyList<SparklineSample>> Samples { get; init; }

    /// <summary>
    /// Gets a value indicating whether the history could not be read.
    /// </summary>
    public required bool IsFailed { get; init; }

    #endregion // Properties
}