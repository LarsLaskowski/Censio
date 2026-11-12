namespace Censio.Contracts.Tests;

/// <summary>
/// A message derived from <see cref="ContractMessage" /> outside the contract: no type of version 1 is built from it.
/// </summary>
internal sealed class ForeignContractMessage : ContractMessage
{
    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.Status;

    #endregion // ContractMessage
}