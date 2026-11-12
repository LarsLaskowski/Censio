namespace Censio.Contracts;

/// <summary>
/// The result of writing a contract message: the bytes or an error, never both.
/// </summary>
public sealed class ContractWriteResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractWriteResult" /> class.
    /// </summary>
    /// <param name="utf8Json">The bytes written</param>
    /// <param name="error">The error, or <c>null</c></param>
    internal ContractWriteResult(ReadOnlyMemory<byte> utf8Json, ContractError? error)
    {
        Utf8Json = utf8Json;
        Error = error;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the UTF-8 JSON bytes of the message, empty when it was refused.
    /// </summary>
    public ReadOnlyMemory<byte> Utf8Json { get; }

    /// <summary>
    /// Gets the error, or <c>null</c> when the message was written.
    /// </summary>
    public ContractError? Error { get; }

    #endregion // Properties
}