using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes a float as a JSON number in the range of a 32-bit float, never as infinity or NaN.
/// </summary>
internal sealed class ContractFloatJsonConverter : JsonConverter<float>
{
    #region JsonConverter

    /// <inheritdoc />
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || reader.TryGetSingle(out var value) == false || float.IsFinite(value) == false)
        {
            throw new JsonException();
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
    {
        if (float.IsFinite(value) == false)
        {
            throw new JsonException();
        }

        writer.WriteNumberValue(value);
    }

    #endregion // JsonConverter
}