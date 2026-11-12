using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes a map of a tick entry as an array of <c>[guid, amount]</c> pairs in ascending GUID order. Each pair is
/// read straight into the map: exactly two int32 numbers, no array per pair.
/// </summary>
internal sealed class ContractMapJsonConverter : JsonConverter<IReadOnlyDictionary<int, int>>
{
    #region Static methods

    /// <summary>
    /// Reads the next token as an int32 number.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>The number</returns>
    private static int ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.Read() == false || reader.TokenType != JsonTokenType.Number || reader.TryGetInt32(out var value) == false)
        {
            throw new JsonException();
        }

        return value;
    }

    #endregion // Static methods

    #region JsonConverter

    /// <inheritdoc />
    public override IReadOnlyDictionary<int, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }

        var map = new ContractMap();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return map;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }

            var guid = ReadNumber(ref reader);
            var amount = ReadNumber(ref reader);

            if (reader.Read() == false || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException();
            }

            map.Append(guid, amount);
        }

        throw new JsonException();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<int, int> value, JsonSerializerOptions options)
    {
        var guids = new int[value.Count];
        var index = 0;

        foreach (var guid in value.Keys)
        {
            guids[index++] = guid;
        }

        Array.Sort(guids);
        writer.WriteStartArray();

        foreach (var guid in guids)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(guid);
            writer.WriteNumberValue(value[guid]);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    #endregion // JsonConverter
}