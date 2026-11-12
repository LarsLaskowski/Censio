namespace Censio.Contracts;

/// <summary>
/// The state of the Companion's pipe connection.
/// </summary>
public sealed class ContractStatus : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the pipe state.
    /// </summary>
    public required ContractPipeState PipeState { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.Status;

    #endregion // ContractMessage
}