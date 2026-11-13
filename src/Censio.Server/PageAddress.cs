using System.Globalization;

namespace Censio.Server;

/// <summary>
/// Parses the segments of a data page address.
/// </summary>
public static class PageAddress
{
    #region Constants

    /// <summary>
    /// The length of an identity in its 36-character form.
    /// </summary>
    private const int GuidLength = 36;

    /// <summary>
    /// The longest digit run a number of an address may have.
    /// </summary>
    private const int MaxDigits = 10;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Tries to parse a profile identity.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="profileId">The identity</param>
    /// <returns><see langword="true"/> when the text is an identity</returns>
    public static bool TryParseProfile(string? text, out Guid profileId)
    {
        profileId = Guid.Empty;

        return text is { Length: GuidLength } && Guid.TryParseExact(text, "D", out profileId);
    }

    /// <summary>
    /// Tries to parse a session GUID.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="sessionGuid">The session GUID</param>
    /// <returns><see langword="true"/> when the text is a session GUID</returns>
    public static bool TryParseSession(string? text, out int sessionGuid)
    {
        sessionGuid = 0;

        return text is { Length: > 0 and <= MaxDigits + 1 }
               && IsDigits(text.StartsWith('-') ? text[1..] : text)
               && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out sessionGuid);
    }

    /// <summary>
    /// Tries to parse a byte.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="value">The value</param>
    /// <returns><see langword="true"/> when the text is a byte</returns>
    public static bool TryParseByte(string? text, out byte value)
    {
        value = 0;

        return text is { Length: > 0 and <= MaxDigits }
               && IsDigits(text)
               && byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Tells whether a text is one or more ASCII digits.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><see langword="true"/> when the text is not empty and holds only the digits 0 to 9</returns>
    private static bool IsDigits(string text)
    {
        return text.Length > 0 && text.All(character => character is >= '0' and <= '9');
    }

    #endregion // Methods
}