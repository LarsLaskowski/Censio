namespace Censio.Pipe;

/// <summary>
/// One good of a trade route station.
/// </summary>
public sealed class TradeRouteGood
{
    #region Properties

    /// <summary>
    /// Gets the product GUID.
    /// </summary>
    public required int ProductGuid { get; init; }

    /// <summary>
    /// Gets the amount.
    /// </summary>
    public required int Amount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the good is loaded.
    /// </summary>
    public required bool Loading { get; init; }

    #endregion // Properties
}