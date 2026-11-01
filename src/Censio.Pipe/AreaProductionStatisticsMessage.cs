namespace Censio.Pipe;

/// <summary>
/// The production statistics of one area.
/// </summary>
public sealed class AreaProductionStatisticsMessage : PipeMessage
{
    #region Properties

    /// <summary>
    /// Gets the session ID.
    /// </summary>
    public required byte SessionId { get; init; }

    /// <summary>
    /// Gets the island ID.
    /// </summary>
    public required byte IslandId { get; init; }

    /// <summary>
    /// Gets the area index.
    /// </summary>
    public required byte AreaIndex { get; init; }

    /// <summary>
    /// Gets the session GUID.
    /// </summary>
    public required int SessionGuid { get; init; }

    /// <summary>
    /// Gets the area name.
    /// </summary>
    public required string AreaName { get; init; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public required long Timestamp { get; init; }

    /// <summary>
    /// Gets the production entries.
    /// </summary>
    public required IReadOnlyList<ProductionEntry> Entries { get; init; }

    #endregion // Properties

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.AreaProductionStatistics;

    #endregion // PipeMessage
}