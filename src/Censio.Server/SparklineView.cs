namespace Censio.Server;

/// <summary>
/// The drawn line of one sparkline and the direction of its trend.
/// </summary>
public sealed class SparklineView
{
    #region Properties

    /// <summary>
    /// Gets the path data of the line.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the direction of the trend.
    /// </summary>
    public required TrendDirection Direction { get; init; }

    #endregion // Properties
}