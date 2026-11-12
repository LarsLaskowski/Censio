using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The upload of the game catalog document.
/// </summary>
public sealed class ContractCatalogUpload : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the bytes of the catalog document.
    /// </summary>
    public required ReadOnlyMemory<byte> Document { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Computes the identity of the document.
    /// </summary>
    /// <returns>The SHA-256 of the document as 64 lowercase hexadecimal characters</returns>
    public string ComputeIdentity()
    {
        return Convert.ToHexStringLower(SHA256.HashData(Document.Span));
    }

    #endregion // Methods

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.CatalogUpload;

    #endregion // ContractMessage
}