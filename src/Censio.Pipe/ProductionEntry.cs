namespace Censio.Pipe;

/// <summary>
/// The production figures of one product within an area.
/// </summary>
public sealed class ProductionEntry
{
    #region Properties

    /// <summary>
    /// Gets the product GUID.
    /// </summary>
    public required int ProductGuid { get; init; }

    /// <summary>
    /// Gets the generation.
    /// </summary>
    public required float Generation { get; init; }

    /// <summary>
    /// Gets the consumption.
    /// </summary>
    public required float Consumption { get; init; }

    /// <summary>
    /// Gets the delta.
    /// </summary>
    public required float Delta { get; init; }

    /// <summary>
    /// Gets the perfect generation.
    /// </summary>
    public required float PerfectGeneration { get; init; }

    /// <summary>
    /// Gets the perfect consumption.
    /// </summary>
    public required float PerfectConsumption { get; init; }

    /// <summary>
    /// Gets the building count.
    /// </summary>
    public required int BuildingCount { get; init; }

    /// <summary>
    /// Gets the maintenance.
    /// </summary>
    public required int Maintenance { get; init; }

    /// <summary>
    /// Gets the income.
    /// </summary>
    public required float Income { get; init; }

    /// <summary>
    /// Gets the profit.
    /// </summary>
    public required int Profit { get; init; }

    /// <summary>
    /// Gets the summed productivity.
    /// </summary>
    public required float SummedProductivity { get; init; }

    /// <summary>
    /// Gets the average productivity.
    /// </summary>
    public required float AverageProductivity { get; init; }

    /// <summary>
    /// Gets the workforce amounts by workforce GUID.
    /// </summary>
    public required IReadOnlyDictionary<int, int> WorkforceAmounts { get; init; }

    /// <summary>
    /// Gets the building amounts by building GUID.
    /// </summary>
    public required IReadOnlyDictionary<int, int> BuildingAmounts { get; init; }

    #endregion // Properties
}