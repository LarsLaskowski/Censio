using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes binary data as Base64 text. The raw token must hold no escape, only the Base64 alphabet and the
/// canonical padding, before anything is decoded.
/// </summary>
internal sealed class ContractBinaryJsonConverter : JsonConverter<ReadOnlyMemory<byte>>
{
    #region Fields

    /// <summary>
    /// The characters of the standard Base64 alphabet, without the padding.
    /// </summary>
    private static readonly SearchValues<byte> _alphabet = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"u8);

    #endregion // Fields

    #region Static methods

    /// <summary>
    /// Checks that a token is a multiple of four characters of the alphabet followed by at most two padding characters.
    /// </summary>
    /// <param name="text">The raw bytes of the token</param>
    /// <returns><c>true</c> when the token has the form</returns>
    private static bool HasForm(ReadOnlySpan<byte> text)
    {
        if (text.Length % 4 != 0)
        {
            return false;
        }

        var padding = 0;

        while (padding < 2 && padding < text.Length && text[text.Length - 1 - padding] == (byte)'=')
        {
            padding++;
        }

        return text[..^padding].IndexOfAnyExcept(_alphabet) < 0;
    }

    #endregion // Static methods

    #region JsonConverter

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped || reader.HasValueSequence || HasForm(reader.ValueSpan) == false || reader.TryGetBytesFromBase64(out var bytes) == false)
        {
            throw new JsonException();
        }

        return bytes;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.Span);
    }

    #endregion // JsonConverter
}