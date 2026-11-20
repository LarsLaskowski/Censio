namespace Censio.Pipe;

/// <summary>
/// The item offer of one trader.
/// </summary>
public sealed class ItemOffer
{
    #region Properties

    /// <summary>
    /// Gets the trader ID.
    /// </summary>
    public required int TraderId { get; init; }

    /// <summary>
    /// Gets the item GUIDs in frame order.
    /// </summary>
    public required IReadOnlyList<int> ItemGuids { get; init; }

    #endregion // Properties
}