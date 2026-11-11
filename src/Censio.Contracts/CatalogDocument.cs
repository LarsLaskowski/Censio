using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// The versioned catalog document the Companion sends to the server.
/// </summary>
public sealed class CatalogDocument
{
    #region Constants

    /// <summary>
    /// The only schema version this build reads and writes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the schema version of the document.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the game build the catalog was read from, or <c>null</c> when unknown.
    /// </summary>
    [JsonPropertyName("gameVersion")]
    public required string? GameVersion { get; init; }

    /// <summary>
    /// Gets the entries of the catalog.
    /// </summary>
    [JsonPropertyName("entries")]
    public required IReadOnlyList<CatalogDocumentEntry> Entries { get; init; }

    #endregion // Properties
}