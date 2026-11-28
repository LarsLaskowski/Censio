using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The merge of a profile's history into another profile's on the server.
/// </summary>
public sealed class ContractProfileMerge : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the identity of the profile whose history is merged (the source).
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the identity of the profile that receives the history.
    /// </summary>
    public required Guid TargetProfileId { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.ProfileMerge;

    #endregion // ContractMessage
}