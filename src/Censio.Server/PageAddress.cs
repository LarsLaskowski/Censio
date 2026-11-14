using System.Globalization;

namespace Censio.Server;

/// <summary>
/// Parses the segments of a data page address and builds the links to the data pages.
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

    /// <summary>
    /// The start of the address of a profile's pages.
    /// </summary>
    private const string ProfilesPrefix = "profiles/";

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
    /// Gives the address of a profile's region overview.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <returns>The address</returns>
    public static string RegionsLink(Guid profileId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"/profiles/{profileId}/regions");
    }

    /// <summary>
    /// Gives the address the Regions navigation entry leads to: the region overview of the profile the address shows, or the one of the newest profile.
    /// </summary>
    /// <param name="relativePath">The base-relative address, query and fragment included</param>
    /// <returns>The address</returns>
    public static string RegionsEntryLink(string? relativePath)
    {
        var path = relativePath ?? string.Empty;
        var end = path.IndexOfAny(['?', '#']);

        if (end >= 0)
        {
            path = path[..end];
        }

        if (path.StartsWith(ProfilesPrefix, StringComparison.Ordinal))
        {
            var rest = path[ProfilesPrefix.Length..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            var identity = slash < 0 ? rest : rest[..slash];

            if (TryParseProfile(identity, out var profileId))
            {
                return RegionsLink(profileId);
            }
        }

        return "/regions";
    }

    /// <summary>
    /// Gives the address of the island list of the region that holds a session.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="sessionGuid">The session GUID</param>
    /// <returns>The address</returns>
    public static string RegionLink(Guid profileId, int sessionGuid)
    {
        return string.Create(CultureInfo.InvariantCulture, $"/profiles/{profileId}/sessions/{sessionGuid}");
    }

    /// <summary>
    /// Gives the address of an island's detail.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="island">The island</param>
    /// <returns>The address</returns>
    public static string IslandLink(Guid profileId, IslandKey island)
    {
        return string.Create(CultureInfo.InvariantCulture, $"/profiles/{profileId}/islands/{island.SessionGuid}/{island.IslandId}/{island.AreaIndex}");
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