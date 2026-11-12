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
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public string ComputeIdentity()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    #endregion // Methods

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.CatalogUpload;

    #endregion // ContractMessage
}