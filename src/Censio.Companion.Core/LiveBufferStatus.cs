namespace Censio.Companion.Core;

/// <summary>
/// The status of the live buffer.
/// </summary>
public readonly record struct LiveBufferStatus
{
    #region Properties

    /// <summary>
    /// Gets the number of waiting messages.
    /// </summary>
    public int Waiting { get; init; }

    /// <summary>
    /// Gets the number of messages dropped in this run.
    /// </summary>
    public int Dropped { get; init; }

    /// <summary>
    /// Gets a value indicating whether the buffer keeps messages in memory only.
    /// </summary>
    public bool IsMemoryOnly { get; init; }

    #endregion // Properties
}