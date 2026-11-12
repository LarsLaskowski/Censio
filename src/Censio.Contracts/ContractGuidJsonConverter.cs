using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes a UUID as 36-character lowercase hyphenated text. Text of any other form reads as the all-zero
/// UUID, which the value rules refuse as invalid, not as malformed.
/// </summary>
internal sealed class ContractGuidJsonConverter : JsonConverter<Guid>
{
    #region Constants

    /// <summary>
    /// The length of the text of a UUID.
    /// </summary>
    private const int TextLength = 36;

    #endregion // Constants

    #region Static methods

    /// <summary>
    /// Checks that a text is 36 characters of lowercase hexadecimal digits and hyphens after the 8th, 13th, 18th and 23rd.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><c>true</c> when the text has the canonical form</returns>
    private static bool IsCanonical(string text)
    {
        if (text.Length != TextLength)
        {
            return false;
        }

        for (var index = 0; index < TextLength; index++)
        {
            var isHyphen = index is 8 or 13 or 18 or 23;

            if (isHyphen ? text[index] != '-' : char.IsAsciiHexDigitLower(text[index]) == false)
            {
                return false;
            }
        }

        return true;
    }

    #endregion // Static methods

    #region JsonConverter

    /// <inheritdoc />
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        var text = reader.GetString();

        return text is not null && IsCanonical(text) && Guid.TryParseExact(text, "D", out var value) ? value : Guid.Empty;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    #endregion // JsonConverter
}