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
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CatalogEntryKind value, JsonSerializerOptions options)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // JsonConverter
}