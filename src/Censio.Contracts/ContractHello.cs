namespace Censio.Contracts;

/// <summary>
/// The hello the Companion sends first.
/// </summary>
public sealed class ContractHello : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the ingest token, or <c>null</c> when none is configured.
    /// </summary>
    public required string? Token { get; init; }

    /// <summary>
    /// Gets the Companion's version.
    /// </summary>
    public required string CompanionVersion { get; init; }

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public required string ProfileName { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    public override ContractMessageType Type => ContractMessageType.Hello;

    #endregion // ContractMessage
}