namespace Censio.Server.Data;

/// <summary>
/// The sums and counts of the generation, consumption and delta of one island over the ticks of one hour.
/// </summary>
internal sealed class HourIslandSums
{
    #region Fields

    /// <summary>
    /// The sums of the generation, consumption and delta.
    /// </summary>
    private readonly double[] _sums = new double[3];

    /// <summary>
    /// The numbers of ticks that carried the generation, consumption and delta.
    /// </summary>
    private readonly int[] _counts = new int[3];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Adds a sum and its count to one of the three values.
    /// </summary>
    /// <param name="index">The value: 0 generation, 1 consumption, 2 delta</param>
    /// <param name="sum">The sum to add</param>
    /// <param name="count">The count to add</param>
    public void Add(int index, double sum, int count)
    {
        _sums[index] += sum;
        _counts[index] += count;
    }

    /// <summary>
    /// Gets the mean of one of the three values.
    /// </summary>
    /// <param name="index">The value: 0 generation, 1 consumption, 2 delta</param>
    /// <returns>The sum divided by its count, or <see langword="null"/> when no tick carried the value</returns>
    public double? Mean(int index)
    {
        return _counts[index] == 0
                   ? null
                   : _sums[index] / _counts[index];
    }

    #endregion // Methods
}