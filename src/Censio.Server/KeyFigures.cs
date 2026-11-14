namespace Censio.Server;

/// <summary>
/// The key figures of an island or a region.
/// </summary>
public sealed class KeyFigures
{
    #region Properties

    /// <summary>
    /// Gets the number of buildings.
    /// </summary>
    public required long Buildings { get; init; }

    /// <summary>
    /// Gets the number of produced goods.
    /// </summary>
    public required int ProducedGoods { get; init; }

    /// <summary>
    /// Gets the number of goods in deficit.
    /// </summary>
    public required int GoodsInDeficit { get; init; }

    #endregion // Properties
}