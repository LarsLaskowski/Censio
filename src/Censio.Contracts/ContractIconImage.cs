using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The image of a game icon.
/// </summary>
public sealed class ContractIconImage : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the reference of the icon: the SHA-256 of the image as 64 lowercase hexadecimal characters.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Gets the PNG bytes of the image.
    /// </summary>
    public required ReadOnlyMemory<byte> Png { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.IconImage;

    #endregion // ContractMessage
}