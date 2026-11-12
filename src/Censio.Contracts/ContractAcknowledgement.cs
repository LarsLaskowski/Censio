using System.Text.Json.Serialization;

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
    public string? GetRefusalText()
    {
        var version = RefusedVersion.GetValueOrDefault();

        return Refusal switch
               {
                   null => null,
                   ContractAcknowledgementRefusal.Malformed => ContractError.MalformedText,
                   ContractAcknowledgementRefusal.UnsupportedVersion => ContractError.GetUnsupportedVersionText(version),
                   ContractAcknowledgementRefusal.TooLarge => ContractError.TooLargeText,
                   ContractAcknowledgementRefusal.CatalogMalformed => CatalogDocumentError.CreateMalformed().Text,
                   ContractAcknowledgementRefusal.CatalogUnsupportedVersion => CatalogDocumentError.CreateUnsupportedVersion(version).Text,
                   ContractAcknowledgementRefusal.CatalogInvalid => CatalogDocumentError.CreateInvalid().Text,
                   ContractAcknowledgementRefusal.CatalogTooLarge => CatalogDocumentError.CreateTooLarge().Text,
                   _ => ContractError.InvalidText
               };
    }

    #endregion // Methods

    #region ContractMessage

    /// <inheritdoc />
    [JsonPropertyOrder(-2)]
    public override ContractMessageType Type => ContractMessageType.Acknowledgement;

    #endregion // ContractMessage
}