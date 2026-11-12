using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// A closed tick of the pipe.
/// </summary>
public sealed class ContractTick : ContractMessage
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
    /// Gets the receive time of the tick.
    /// </summary>
    public required DateTimeOffset ReceiveTime { get; init; }

    /// <summary>
    /// Gets the game time of the tick.
    /// </summary>
    public required long GameTime { get; init; }

    /// <summary>
    /// Gets the islands of the tick.
    /// </summary>
    public required IReadOnlyList<ContractTickIsland> Islands { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.Tick;

    #endregion // ContractMessage
}