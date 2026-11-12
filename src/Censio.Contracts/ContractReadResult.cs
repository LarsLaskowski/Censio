namespace Censio.Contracts;

/// <summary>
/// The result of reading a contract message: the message or an error, never both.
/// </summary>
public sealed class ContractReadResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractReadResult" /> class.
    /// </summary>
    /// <param name="message">The message read, or <c>null</c></param>
    /// <param name="error">The error, or <c>null</c></param>
    /// <param name="refusedId">The <c>id</c> of a refused message the reader built, or <c>null</c></param>
    /// <param name="catalogDocument">The catalog document of an accepted catalog upload, or <c>null</c></param>
    internal ContractReadResult(ContractMessage? message, ContractError? error, Guid? refusedId = null, CatalogDocument? catalogDocument = null)
    {
        Message = message;
        Error = error;
        RefusedId = refusedId;
        CatalogDocument = catalogDocument;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the message read, or <c>null</c> when it was refused.
    /// </summary>
    public ContractMessage? Message { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the message was read.
    /// </summary>
    public ContractError? Error { get; }

    /// <summary>
    /// Gets the <c>id</c> of a message the reader built before refusing it, or <c>null</c> when none was built or the <c>id</c> is all zero.
    /// </summary>
    public Guid? RefusedId { get; }

    /// <summary>
    /// Gets the catalog document of an accepted catalog upload, or <c>null</c> for every other result.
    /// </summary>
    public CatalogDocument? CatalogDocument { get; }

    #endregion // Properties
}