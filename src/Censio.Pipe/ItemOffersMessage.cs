namespace Censio.Pipe;

/// <summary>
/// The item offers of the game.
/// </summary>
public sealed class ItemOffersMessage : PipeMessage
{
    #region Properties

    /// <summary>
    /// Gets the offers in frame order.
    /// </summary>
    public required IReadOnlyList<ItemOffer> Offers { get; init; }

    #endregion // Properties

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.ItemOffers;

    #endregion // PipeMessage
}