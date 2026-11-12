namespace Censio.Contracts;

/// <summary>
/// The server's acceptance of a hello.
/// </summary>
public sealed class ContractHelloAccepted : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the server's version.
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Gets the identity of the catalog the server holds, or <c>null</c>.
    /// </summary>
    public required string? CatalogIdentity { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.HelloAccepted;

    #endregion // ContractMessage
}