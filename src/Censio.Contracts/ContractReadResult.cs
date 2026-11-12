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
    internal ContractReadResult(ContractMessage? message, ContractError? error)
    {
        Message = message;
        Error = error;
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

    #endregion // Properties
}