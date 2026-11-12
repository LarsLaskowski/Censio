using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Censio.Contracts;

/// <summary>
/// Reads and writes a receive time as <c>yyyy-MM-ddTHH:mm:ss.fffffffZ</c> exactly (UTC, seven fractional digits). Text of
/// any other form reads as <see cref="NotATime" />, which has an offset other than zero and which the value rules refuse as
/// invalid, not as malformed.
/// </summary>
internal sealed class ContractTimeJsonConverter : JsonConverter<DateTimeOffset>
{
    #region Constants

    /// <summary>
    /// The format of a receive time.
    /// </summary>
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>
    /// The length of the text of a receive time.
    /// </summary>
    private const int TextLength = 28;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The value that stands for text that is no receive time: a time with an offset of one hour.
    /// </summary>
    internal static readonly DateTimeOffset NotATime = new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(1));

    #endregion // Fields

    #region Static methods

    /// <summary>
    /// Checks the fixed characters of the text of a receive time: ASCII digits, the separators and the <c>Z</c>.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><c>true</c> when the text has the form</returns>
    private static bool HasForm(string text)
    {
        if (text.Length != TextLength)
        {
            return false;
        }

        for (var index = 0; index < TextLength; index++)
        {
            var expected = index switch
                           {
                               4 or 7 => '-',
                               10 => 'T',
                               13 or 16 => ':',
                               19 => '.',
                               27 => 'Z',
                               _ => '0'
                           };

            if (expected == '0' ? char.IsAsciiDigit(text[index]) == false : text[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    #endregion // Static methods

    #region JsonConverter

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        var text = reader.GetString();

        if (text is not null && HasForm(text) && DateTimeOffset.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            return value;
        }

        return NotATime;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException();
        }

        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }

    #endregion // JsonConverter
}