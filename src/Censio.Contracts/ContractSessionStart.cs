namespace Censio.Contracts;

/// <summary>
/// The start of a game session.
/// </summary>
public sealed class ContractSessionStart : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the identity of the message.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the receive time of the pipe message.
    /// </summary>
    public required DateTimeOffset ReceiveTime { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.SessionStart;

    #endregion // ContractMessage
}