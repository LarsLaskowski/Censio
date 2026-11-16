namespace Censio.Server.Data;

/// <summary>
/// One product entry of a folded hour, in the <c>HourlyEntries</c> table.
/// </summary>
internal sealed class HourlyEntryRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number of the hourly island.
    /// </summary>
    public long HourlyIslandId { get; set; }

    /// <summary>
    /// Gets or sets the product GUID.
    /// </summary>
    public int ProductGuid { get; set; }

    /// <summary>
    /// Gets or sets the number of folded entries.
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Generation values.
    /// </summary>
    public double GenerationSum { get; set; }

    /// <summary>
    /// Gets or sets the number of Generation values summed.
    /// </summary>
    public int GenerationCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Consumption values.
    /// </summary>
    public double ConsumptionSum { get; set; }

    /// <summary>
    /// Gets or sets the number of Consumption values summed.
    /// </summary>
    public int ConsumptionCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Delta values.
    /// </summary>
    public double DeltaSum { get; set; }

    /// <summary>
    /// Gets or sets the number of Delta values summed.
    /// </summary>
    public int DeltaCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the PerfectGeneration values.
    /// </summary>
    public double PerfectGenerationSum { get; set; }

    /// <summary>
    /// Gets or sets the number of PerfectGeneration values summed.
    /// </summary>
    public int PerfectGenerationCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the PerfectConsumption values.
    /// </summary>
    public double PerfectConsumptionSum { get; set; }

    /// <summary>
    /// Gets or sets the number of PerfectConsumption values summed.
    /// </summary>
    public int PerfectConsumptionCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Income values.
    /// </summary>
    public double IncomeSum { get; set; }

    /// <summary>
    /// Gets or sets the number of Income values summed.
    /// </summary>
    public int IncomeCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the SummedProductivity values.
    /// </summary>
    public double SummedProductivitySum { get; set; }

    /// <summary>
    /// Gets or sets the number of SummedProductivity values summed.
    /// </summary>
    public int SummedProductivityCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the AverageProductivity values.
    /// </summary>
    public double AverageProductivitySum { get; set; }

    /// <summary>
    /// Gets or sets the number of AverageProductivity values summed.
    /// </summary>
    public int AverageProductivityCount { get; set; }

    /// <summary>
    /// Gets or sets the sum of the BuildingCount values.
    /// </summary>
    public long BuildingCountSum { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Maintenance values.
    /// </summary>
    public long MaintenanceSum { get; set; }

    /// <summary>
    /// Gets or sets the sum of the Profit values.
    /// </summary>
    public long ProfitSum { get; set; }

    #endregion // Properties
}