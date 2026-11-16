namespace Censio.Server.Data;

/// <summary>
/// One point of a history series: a tick or a folded hour.
/// </summary>
public sealed class HistoryPoint
{
    #region Properties

    /// <summary>
    /// Gets the time of the point.
    /// </summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>
    /// Gets the flag whether the point is a folded hour.
    /// </summary>
    public required bool IsHourly { get; init; }

    /// <summary>
    /// Gets the number of ticks the point stands for.
    /// </summary>
    public required int TickCount { get; init; }

    /// <summary>
    /// Gets the smallest game time.
    /// </summary>
    public required long FirstGameTime { get; init; }

    /// <summary>
    /// Gets the largest game time.
    /// </summary>
    public required long LastGameTime { get; init; }

    /// <summary>
    /// Gets the island name, or <see langword="null"/> when the island is missing.
    /// </summary>
    public required string? IslandName { get; init; }

    /// <summary>
    /// Gets the Generation value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Generation { get; init; }

    /// <summary>
    /// Gets the Consumption value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Consumption { get; init; }

    /// <summary>
    /// Gets the Delta value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Delta { get; init; }

    /// <summary>
    /// Gets the PerfectGeneration value, or <see langword="null"/> for none.
    /// </summary>
    public required double? PerfectGeneration { get; init; }

    /// <summary>
    /// Gets the PerfectConsumption value, or <see langword="null"/> for none.
    /// </summary>
    public required double? PerfectConsumption { get; init; }

    /// <summary>
    /// Gets the Income value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Income { get; init; }

    /// <summary>
    /// Gets the SummedProductivity value, or <see langword="null"/> for none.
    /// </summary>
    public required double? SummedProductivity { get; init; }

    /// <summary>
    /// Gets the AverageProductivity value, or <see langword="null"/> for none.
    /// </summary>
    public required double? AverageProductivity { get; init; }

    /// <summary>
    /// Gets the BuildingCount value, or <see langword="null"/> for none.
    /// </summary>
    public required double? BuildingCount { get; init; }

    /// <summary>
    /// Gets the Maintenance value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Maintenance { get; init; }

    /// <summary>
    /// Gets the Profit value, or <see langword="null"/> for none.
    /// </summary>
    public required double? Profit { get; init; }

    /// <summary>
    /// Gets the workforce amounts.
    /// </summary>
    public required IReadOnlyList<HistoryAmount> WorkforceAmounts { get; init; }

    /// <summary>
    /// Gets the building amounts.
    /// </summary>
    public required IReadOnlyList<HistoryAmount> BuildingAmounts { get; init; }

    #endregion // Properties
}