namespace Censio.Server;

/// <summary>
/// Loads the sparklines of a page off its load loop and holds the read that belongs to the page's address.
/// </summary>
/// <typeparam name="TKey">The type of the key of a sparkline</typeparam>
public sealed class SparklineLoader<TKey> : IDisposable
    where TKey : notnull
{
    #region Methods

    /// <summary>
    /// Requests the read for an address, replacing a pending one.
    /// </summary>
    /// <param name="addressKey">The key of the address the read belongs to</param>
    /// <param name="asOf">The time of the latest tick the read is made for</param>
    /// <param name="read">Reads the sparklines, given the held read and a cancellation token</param>
    /// <param name="changed">Called when the held read or the failure changed</param>
    public void Request(object addressKey, DateTimeOffset asOf, Func<SparklineRead<TKey>?, CancellationToken, Task<SparklineRead<TKey>>> read, Func<Task> changed)
    {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Gives the held read when it belongs to an address.
    /// </summary>
    /// <param name="addressKey">The key of the address</param>
    /// <returns>The held read, or <see langword="null"/> when none belongs to the address</returns>
    public SparklineRead<TKey>? HeldFor(object addressKey)
    {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Tells whether the read of an address failed.
    /// </summary>
    /// <param name="addressKey">The key of the address</param>
    /// <returns><see langword="true"/> when the read of the address failed</returns>
    public bool IsFailedFor(object addressKey)
    {
#pragma warning disable RH2003 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        // Skeleton: nothing to release yet
    }

    #endregion // IDisposable
}