using System.Security.Cryptography;

using Censio.Contracts;
using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// Converts the game catalog into the versioned catalog document.
/// </summary>
public static class GameCatalogDocumentConverter
{
    #region Methods

    /// <summary>
    /// Converts a catalog into a catalog document.
    /// </summary>
    /// <param name="catalog">The catalog to convert</param>
    /// <param name="icons">The icons of the catalog, or <c>null</c> when there are none</param>
    /// <param name="build">The game build, or <c>null</c> when unknown</param>
    /// <returns>The catalog document</returns>
    public static CatalogDocument ToDocument(GameCatalog catalog, GameIconSet? icons, string? build)
    {
        var references = new Dictionary<GameIcon, string>();

        return new CatalogDocument
               {
                   SchemaVersion = CatalogDocument.CurrentSchemaVersion,
                   GameVersion = build,
                   Entries = catalog.Entries
                                    .Where(entry => entry.AssetGuid != 0 && ReferenceEquals(catalog.Find(entry.AssetGuid), entry))
                                    .Select(entry => ToEntry(entry, icons, references))
                                    .ToList()
               };
    }

    /// <summary>
    /// Converts one catalog entry.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="icons">The icons, or <c>null</c></param>
    /// <param name="references">The icon references computed so far, by icon</param>
    /// <returns>The document entry</returns>
    private static CatalogDocumentEntry ToEntry(GameCatalogEntry entry, GameIconSet? icons, Dictionary<GameIcon, string> references)
    {
        var names = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(entry.EnglishName) == false)
        {
            names["en"] = entry.EnglishName;
        }

        if (string.IsNullOrEmpty(entry.GermanName) == false)
        {
            names["de"] = entry.GermanName;
        }

        return new CatalogDocumentEntry
               {
                   AssetGuid = entry.AssetGuid,
                   Kind = ToKind(entry.Kind),
                   Names = names,
                   RegionGuid = entry.RegionGuid,
                   RegionTypes = entry.RegionTypes?.ToList(),
                   IconReference = ToReference(icons?.Find(entry.AssetGuid), references)
               };
    }

    /// <summary>
    /// Maps the kind of a catalog entry.
    /// </summary>
    /// <param name="kind">The catalog kind</param>
    /// <returns>The document kind</returns>
    private static CatalogEntryKind ToKind(GameCatalogKind kind)
    {
        return kind switch
               {
                   GameCatalogKind.Region => CatalogEntryKind.Region,
                   GameCatalogKind.Good => CatalogEntryKind.Good,
                   GameCatalogKind.Workforce => CatalogEntryKind.Workforce,
                   GameCatalogKind.Building => CatalogEntryKind.Building,
                   _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
               };
    }

    /// <summary>
    /// Computes the reference of an icon, once per icon instance.
    /// </summary>
    /// <param name="icon">The icon, or <c>null</c></param>
    /// <param name="references">The references computed so far</param>
    /// <returns>The lowercase SHA-256 hex of the PNG data, or <c>null</c> without an icon</returns>
    private static string? ToReference(GameIcon? icon, Dictionary<GameIcon, string> references)
    {
        if (icon is null)
        {
            return null;
        }

        if (references.TryGetValue(icon, out var reference) == false)
        {
            reference = Convert.ToHexStringLower(SHA256.HashData(icon.Png.Span));
            references[icon] = reference;
        }

        return reference;
    }

    #endregion // Methods
}