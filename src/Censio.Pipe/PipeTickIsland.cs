namespace Censio.Pipe;

/// <summary>
/// One island frame of a tick.
/// </summary>
public sealed class PipeTickIsland
{
    #region Properties

    /// <summary>
    /// Gets the identity of the island frame.
    /// </summary>
    public required PipeIslandKey Key { get; init; }

    /// <summary>
    /// Gets the session ID of the frame.
    /// </summary>
    public required byte SessionId { get; init; }

    /// <summary>
    /// Gets the area name, unchanged.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the production entries.
    /// </summary>
    public required IReadOnlyList<ProductionEntry> Entries { get; init; }

    /// <summary>
    /// Gets a value indicating whether the island has no entries.
    /// </summary>
    public bool HasNoEntries => Entries.Count == 0;

    #endregion // Properties
}