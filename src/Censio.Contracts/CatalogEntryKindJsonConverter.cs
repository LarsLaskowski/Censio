using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes the kind of an entry as exactly one of four lowercase words.
/// </summary>
internal sealed class CatalogEntryKindJsonConverter : JsonConverter<CatalogEntryKind>
{
    #region JsonConverter

    /// <inheritdoc />
    public override CatalogEntryKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        if (reader.ValueTextEquals("region"u8))
        {
            return CatalogEntryKind.Region;
        }

        if (reader.ValueTextEquals("good"u8))
        {
            return CatalogEntryKind.Good;
        }

        if (reader.ValueTextEquals("workforce"u8))
        {
            return CatalogEntryKind.Workforce;
        }

        if (reader.ValueTextEquals("building"u8))
        {
            return CatalogEntryKind.Building;
        }

        throw new JsonException();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CatalogEntryKind value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case CatalogEntryKind.Region:
                writer.WriteStringValue("region"u8);
                break;

            case CatalogEntryKind.Good:
                writer.WriteStringValue("good"u8);
                break;

            case CatalogEntryKind.Workforce:
                writer.WriteStringValue("workforce"u8);
                break;

            case CatalogEntryKind.Building:
                writer.WriteStringValue("building"u8);
                break;

            default:
                throw new JsonException();
        }
    }

    #endregion // JsonConverter
}