using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The base of every message of the contract between Companion and server.
/// </summary>
public abstract class ContractMessage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractMessage" /> class.
    /// </summary>
    protected ContractMessage()
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    public abstract ContractMessageType Type { get; }

    /// <summary>
    /// Gets the contract version written to the wire, the second property of every message.
    /// </summary>
    [JsonInclude]
    [JsonPropertyOrder(-1)]
    internal int ContractVersion { get; } = ContractReader.CurrentVersion;

    #endregion // Properties
}