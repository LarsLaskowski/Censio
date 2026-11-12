using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes a list of texts as an array of strings, none of them <c>null</c>. It is written in ordinal order, so
/// the same list gives the same bytes.
/// </summary>
internal sealed class ContractTextListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    #region JsonConverter

    /// <inheritdoc />
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }

        var list = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return list;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            list.Add(reader.GetString() ?? throw new JsonException());
        }

        throw new JsonException();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        var sorted = value.ToArray();

        Array.Sort(sorted, StringComparer.Ordinal);
        writer.WriteStartArray();

        foreach (var text in sorted)
        {
            writer.WriteStringValue(text);
        }

        writer.WriteEndArray();
    }

    #endregion // JsonConverter
}