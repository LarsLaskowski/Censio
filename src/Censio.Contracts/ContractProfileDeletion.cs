using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The deletion of a profile's history on the server.
/// </summary>
public sealed class ContractProfileDeletion : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the identity of the profile whose history is deleted.
    /// </summary>
    public required Guid ProfileId { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.ProfileDeletion;

    #endregion // ContractMessage
}