namespace Censio.Contracts;

/// <summary>
/// An island of a tick.
/// </summary>
public sealed class ContractTickIsland
{
    #region Properties

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the island id.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public required byte AreaIndex { get; init; }

    /// <summary>
    /// Gets the session id.
    /// </summary>
    public required byte SessionId { get; init; }

    /// <summary>
    /// Gets the island name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the entries of the island.
    /// </summary>
    public required IReadOnlyList<ContractTickEntry> Entries { get; init; }

    #endregion // Properties
}