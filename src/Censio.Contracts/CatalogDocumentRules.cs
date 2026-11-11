namespace Censio.Contracts;

/// <summary>
/// The value rules of the catalog document, shared by reader and writer.
/// </summary>
internal static class CatalogDocumentRules
{
    #region Constants

    /// <summary>
    /// The longest language tag in characters.
    /// </summary>
    private const int MaxLanguageTagLength = 35;

    /// <summary>
    /// The number of parts of a game version.
    /// </summary>
    private const int GameVersionParts = 4;

    /// <summary>
    /// The largest number of a game version part.
    /// </summary>
    private const int MaxGameVersionPart = 65535;

    /// <summary>
    /// The length of an icon reference in characters.
    /// </summary>
    private const int IconReferenceLength = 64;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Checks a document for <c>null</c> in a place the model forbids it.
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns><c>true</c> when the document holds a <c>null</c> where none is allowed</returns>
    internal static bool ContainsNull(CatalogDocument? document)
    {
        if (document?.Entries is null)
        {
            return true;
        }

        return document.Entries.Any(IsNullIn);
    }

    /// <summary>
    /// Checks the value rules of a document that holds no <c>null</c> where none is allowed.
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns>The first violation, or <c>null</c> when the document is valid</returns>
    internal static CatalogDocumentError? Check(CatalogDocument document)
    {
        if (document.GameVersion is not null && IsGameVersion(document.GameVersion) == false)
        {
            return CatalogDocumentError.CreateInvalid();
        }

        var guids = new HashSet<int>();

        return document.Entries.Any(entry => guids.Add(entry.AssetGuid) == false || IsEntryValid(entry) == false) ? CatalogDocumentError.CreateInvalid() : null;
    }

    /// <summary>
    /// Checks whether an entry holds a <c>null</c> where none is allowed.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns><c>true</c> when the entry holds one</returns>
    private static bool IsNullIn(CatalogDocumentEntry? entry)
    {
        if (entry?.Names is null)
        {
            return true;
        }

        return entry.Names.Any(name => name.Key is null || name.Value is null) || entry.RegionTypes?.Any(type => type is null) == true;
    }

    /// <summary>
    /// Checks the value rules of one entry, apart from its GUID being repeated.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns><c>true</c> when the entry is valid</returns>
    private static bool IsEntryValid(CatalogDocumentEntry entry)
    {
        if (entry.AssetGuid == 0 || Enum.IsDefined(entry.Kind) == false)
        {
            return false;
        }

        if (entry.Kind == CatalogEntryKind.Region ? entry.RegionGuid is null or 0 : entry.RegionGuid is not null)
        {
            return false;
        }

        if (entry.Names.Any(name => IsLanguageTag(name.Key) == false || name.Value.Length == 0))
        {
            return false;
        }

        if (entry.RegionTypes?.Any(type => type.Length == 0) == true)
        {
            return false;
        }

        return entry.IconReference is null || IsIconReference(entry.IconReference);
    }

    /// <summary>
    /// Checks a lowercase BCP 47 language tag of at most 35 characters.
    /// </summary>
    /// <param name="tag">The tag</param>
    /// <returns><c>true</c> when the tag is valid</returns>
    private static bool IsLanguageTag(string tag)
    {
        if (tag.Length is 0 or > MaxLanguageTagLength)
        {
            return false;
        }

        var subtags = tag.Split('-');

        if (subtags[0].Length is < 2 or > 3 || subtags[0].All(char.IsAsciiLetterLower) == false)
        {
            return false;
        }

        return subtags.Skip(1).All(subtag => subtag.Length is >= 1 and <= 8 && subtag.All(IsLowerAlphanumeric));
    }

    /// <summary>
    /// Checks a lowercase ASCII letter or an ASCII digit.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns><c>true</c> when it is one</returns>
    private static bool IsLowerAlphanumeric(char character)
    {
        return char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character);
    }

    /// <summary>
    /// Checks a game version of four numbers of ASCII digits, not all zero.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns><c>true</c> when the version is valid</returns>
    private static bool IsGameVersion(string version)
    {
        var parts = version.Split('.');

        if (parts.Length != GameVersionParts)
        {
            return false;
        }

        var allZero = true;

        foreach (var part in parts)
        {
            if (IsVersionPart(part) == false)
            {
                return false;
            }

            allZero &= part == "0";
        }

        return allZero == false;
    }

    /// <summary>
    /// Checks one part of a game version: ASCII digits, 0 to 65,535, no leading zero.
    /// </summary>
    /// <param name="part">The part</param>
    /// <returns><c>true</c> when the part is valid</returns>
    private static bool IsVersionPart(string part)
    {
        if (part.Length is 0 or > 5 || part.All(char.IsAsciiDigit) == false || (part.Length > 1 && part[0] == '0'))
        {
            return false;
        }

        return int.Parse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture) <= MaxGameVersionPart;
    }

    /// <summary>
    /// Checks an icon reference of 64 lowercase hex digits.
    /// </summary>
    /// <param name="reference">The reference</param>
    /// <returns><c>true</c> when the reference is valid</returns>
    private static bool IsIconReference(string reference)
    {
        return reference.Length == IconReferenceLength && reference.All(char.IsAsciiHexDigitLower);
    }

    #endregion // Methods
}