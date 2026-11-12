namespace Censio.Contracts;

/// <summary>
/// The server's answer to a message.
/// </summary>
public sealed class ContractAcknowledgement : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message answered.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the refusal, or <c>null</c> when the message was accepted.
    /// </summary>
    public required ContractAcknowledgementRefusal? Refusal { get; init; }

    /// <summary>
    /// Gets the refused contract version, or <c>null</c>.
    /// </summary>
    public required int? RefusedVersion { get; init; }

    /// <summary>
    /// Gets the references of the icons the server misses.
    /// </summary>
    public required IReadOnlyList<string> MissingIcons { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the fixed text of the refusal.
    /// </summary>
    /// <returns>The text, or <c>null</c> when the message was accepted</returns>
#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    public string? GetRefusalText()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    #endregion // Methods

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.Acknowledgement;

    #endregion // ContractMessage
}