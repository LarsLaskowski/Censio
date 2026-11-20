namespace Censio.Pipe;

/// <summary>
/// The trade routes of the game.
/// </summary>
public sealed class TradeRoutesMessage : PipeMessage
{
    #region Properties

    /// <summary>
    /// Gets the routes in frame order.
    /// </summary>
    public required IReadOnlyList<TradeRoute> Routes { get; init; }

    #endregion // Properties

    #region PipeMessage

    /// <inheritdoc />
    public override PipeMessageType MessageType => PipeMessageType.TradeRoutes;

    #endregion // PipeMessage
}