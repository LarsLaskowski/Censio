namespace Censio.Server;

/// <summary>
/// The entry of one host in <see cref="IngestHosts"/>: its places and its failed hellos.
/// </summary>
public sealed class IngestHost
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestHost"/> class.
    /// </summary>
    /// <param name="key">The key of the host's address, or <c>null</c> for the entry shared by the hosts beyond the tracked ones</param>
    internal IngestHost(string? key)
    {
        Key = key;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the key of the host's address; <c>null</c> for the shared entry.
    /// </summary>
    internal string? Key { get; }

    /// <summary>
    /// Gets a value indicating whether this is the entry shared by the hosts beyond the tracked ones.
    /// </summary>
    internal bool IsShared => Key is null;

    /// <summary>
    /// Gets or sets the number of connections that wait for their hello.
    /// </summary>
    internal int Waiting { get; set; }

    /// <summary>
    /// Gets the times of the failed hellos within the window.
    /// </summary>
    internal List<DateTimeOffset> FailedHellos { get; } = [];

    /// <summary>
    /// Gets or sets the time of the last failed hello.
    /// </summary>
    internal DateTimeOffset? LastFailedHello { get; set; }

    /// <summary>
    /// Gets or sets the end of the block, or <c>null</c> when the host was never blocked.
    /// </summary>
    internal DateTimeOffset? BlockEnd { get; set; }

    /// <summary>
    /// Gets or sets the number of connections refused during the block.
    /// </summary>
    internal int Refused { get; set; }

    /// <summary>
    /// Gets or sets the number of the current block, which tells a timer of an earlier block from the current one.
    /// </summary>
    internal int BlockNumber { get; set; }

    /// <summary>
    /// Gets or sets the timer that ends the block, or <c>null</c> when no block is running.
    /// </summary>
    internal ITimer? BlockTimer { get; set; }

    #endregion // Properties
}