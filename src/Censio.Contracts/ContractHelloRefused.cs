using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The server's refusal of a hello.
/// </summary>
public sealed class ContractHelloRefused : ContractMessage
{
    #region Properties

    /// <summary>
    /// Gets the reason of the refusal.
    /// </summary>
    public required ContractHelloRefusal Reason { get; init; }

    /// <summary>
    /// Gets the contract version the hello carried.
    /// </summary>
    public required int ReceivedVersion { get; init; }

    /// <summary>
    /// Gets the contract versions the server supports.
    /// </summary>
    public required IReadOnlyList<int> SupportedVersions { get; init; }

    #endregion // Properties

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.HelloRefused;

    #endregion // ContractMessage
}