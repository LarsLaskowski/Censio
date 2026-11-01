namespace Censio.Companion.Core;

/// <summary>
/// Computes the delay between two attempts to connect to the statistics pipe.
/// </summary>
public static class PipeRetryDelay
{
    #region Properties

    /// <summary>
    /// Gets the maximum delay.
    /// </summary>
    public static TimeSpan Max => throw new NotImplementedException();

    /// <summary>
    /// Gets the time a connection must last to count as successful.
    /// </summary>
    public static TimeSpan SuccessfulConnectionTime => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the delay after a number of consecutive failures.
    /// </summary>
    /// <param name="consecutiveFailures">The number of consecutive failures, at least 1</param>
    /// <returns>The delay</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="consecutiveFailures" /> is below 1</exception>
    public static TimeSpan For(int consecutiveFailures)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}