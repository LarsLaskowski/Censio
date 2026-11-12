namespace Censio.Contracts;

/// <summary>
/// A production entry of an island.
/// </summary>
public sealed class ContractTickEntry
{
    #region Properties

    /// <summary>
    /// Gets the product GUID.
    /// </summary>
    public required int ProductGuid { get; init; }

    /// <summary>
    /// Gets the generation, or <c>null</c> for no value.
    /// </summary>
    public required float? Generation { get; init; }

    /// <summary>
    /// Gets the consumption, or <c>null</c> for no value.
    /// </summary>
    public required float? Consumption { get; init; }

    /// <summary>
    /// Gets the delta, or <c>null</c> for no value.
    /// </summary>
    public required float? Delta { get; init; }

    /// <summary>
    /// Gets the perfect generation, or <c>null</c> for no value.
    /// </summary>
    public required float? PerfectGeneration { get; init; }

    /// <summary>
    /// Gets the perfect consumption, or <c>null</c> for no value.
    /// </summary>
    public required float? PerfectConsumption { get; init; }

    /// <summary>
    /// Gets the building count.
    /// </summary>
    public required int BuildingCount { get; init; }

    /// <summary>
    /// Gets the maintenance.
    /// </summary>
    public required int Maintenance { get; init; }

    /// <summary>
    /// Gets the income, or <c>null</c> for no value.
    /// </summary>
    public required float? Income { get; init; }

    /// <summary>
    /// Gets the profit.
    /// </summary>
    public required int Profit { get; init; }

    /// <summary>
    /// Gets the summed productivity, or <c>null</c> for no value.
    /// </summary>
    public required float? SummedProductivity { get; init; }

    /// <summary>
    /// Gets the average productivity, or <c>null</c> for no value.
    /// </summary>
    public required float? AverageProductivity { get; init; }

    /// <summary>
    /// Gets the workforce amounts by GUID.
    /// </summary>
    public required IReadOnlyDictionary<int, int> WorkforceAmounts { get; init; }

    /// <summary>
    /// Gets the building amounts by GUID.
    /// </summary>
    public required IReadOnlyDictionary<int, int> BuildingAmounts { get; init; }

    #endregion // Properties
}