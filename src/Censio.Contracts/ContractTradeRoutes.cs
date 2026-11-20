using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The trade routes of the game.
/// </summary>
public sealed class ContractTradeRoutes : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the receive time of the trade routes.
    /// </summary>
    public required DateTimeOffset ReceiveTime { get; init; }

    /// <summary>
    /// Gets the routes.
    /// </summary>
    public required IReadOnlyList<ContractTradeRoute> Routes { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.TradeRoutes;

    #endregion // ContractMessage
}