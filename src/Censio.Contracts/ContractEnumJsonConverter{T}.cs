using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes an enumeration of the contract as exactly one of its camelCase member names, never as a number.
/// </summary>
/// <typeparam name="T">The enumeration</typeparam>
internal sealed class ContractEnumJsonConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    #region Fields

    /// <summary>
    /// The members.
    /// </summary>
    private readonly T[] _values = Enum.GetValues<T>();

    /// <summary>
    /// The UTF-8 wire names of the members, in the order of <see cref="_values" />.
    /// </summary>
    private readonly byte[][] _names = ContractEnumNames.Create<T>();

    #endregion // Fields

    #region JsonConverter

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        for (var index = 0; index < _names.Length; index++)
        {
            if (reader.ValueTextEquals(_names[index]))
            {
                return _values[index];
            }
        }

        throw new JsonException();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var index = Array.IndexOf(_values, value);

        if (index < 0)
        {
            throw new JsonException();
        }

        writer.WriteStringValue(_names[index]);
    }

    #endregion // JsonConverter
}