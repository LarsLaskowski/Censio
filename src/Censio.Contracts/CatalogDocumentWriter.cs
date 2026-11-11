using System.Text;
using System.Text.Json;

namespace Censio.Contracts;

/// <summary>
/// Writes a catalog document as compact UTF-8 JSON.
/// </summary>
public static class CatalogDocumentWriter
{
    #region Methods

    /// <summary>
    /// Writes a catalog document.
    /// </summary>
    /// <param name="document">The document to write</param>
    /// <returns>The UTF-8 JSON, or the error that refused the document</returns>
    public static CatalogDocumentWriteResult Write(CatalogDocument document)
    {
        var error = Validate(document);

        if (error is not null)
        {
            return Refuse(error);
        }

        var normalized = Normalize(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized, CatalogDocumentJsonContext.Default.CatalogDocument);

        if (bytes.Length > CatalogDocumentReader.MaxDocumentBytes)
        {
            return Refuse(CatalogDocumentError.CreateTooLarge());
        }

        return new CatalogDocumentWriteResult(bytes, null);
    }

    /// <summary>
    /// Checks a document against the version, the bounds and the value rules, in that order.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The first error, or <c>null</c></returns>
    private static CatalogDocumentError? Validate(CatalogDocument document)
    {
        if (document.SchemaVersion != CatalogDocument.CurrentSchemaVersion)
        {
            return CatalogDocumentError.CreateUnsupportedVersion(document.SchemaVersion);
        }

        if (HasCountAboveBound(document))
        {
            return CatalogDocumentError.CreateTooLarge();
        }

        if (CatalogDocumentRules.ContainsNull(document))
        {
            return CatalogDocumentError.CreateInvalid();
        }

        return CatalogDocumentRules.Check(document) ?? CheckStrings(document);
    }

    /// <summary>
    /// Checks whether a count of the document is above its bound.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns><c>true</c> when one is</returns>
    private static bool HasCountAboveBound(CatalogDocument document)
    {
        if (document.Entries is null)
        {
            return false;
        }

        return document.Entries.Count > CatalogDocumentReader.MaxEntries
               || document.Entries.Any(entry => entry is not null
                                                && (entry.Names?.Count > CatalogDocumentReader.MaxNamesPerEntry
                                                    || entry.RegionTypes?.Count > CatalogDocumentReader.MaxRegionTypesPerEntry));
    }

    /// <summary>
    /// Checks that every string is well-formed Unicode and within the string bound, and that all of them together
    /// fit the document bound.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The first error, or <c>null</c></returns>
    private static CatalogDocumentError? CheckStrings(CatalogDocument document)
    {
        var encoding = new UTF8Encoding(false, true);
        long total = 0;

        foreach (var text in EnumerateStrings(document))
        {
            int length;

            try
            {
                length = encoding.GetByteCount(text);
            }
            catch (EncoderFallbackException)
            {
                return CatalogDocumentError.CreateInvalid();
            }

            total += length;

            if (length > CatalogDocumentReader.MaxStringBytes || total > CatalogDocumentReader.MaxDocumentBytes)
            {
                return CatalogDocumentError.CreateTooLarge();
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates every string of a document.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The strings</returns>
    private static IEnumerable<string> EnumerateStrings(CatalogDocument document)
    {
        if (document.GameVersion is not null)
        {
            yield return document.GameVersion;
        }

        foreach (var entry in document.Entries)
        {
            foreach (var name in entry.Names)
            {
                yield return name.Key;
                yield return name.Value;
            }

            foreach (var regionType in entry.RegionTypes ?? [])
            {
                yield return regionType;
            }

            if (entry.IconReference is not null)
            {
                yield return entry.IconReference;
            }
        }
    }

    /// <summary>
    /// Copies a document with the names of every entry in ordinal order of their language tag.
    /// </summary>
    /// <param name="document">The document</param>
    /// <returns>The copy</returns>
    private static CatalogDocument Normalize(CatalogDocument document)
    {
        return new CatalogDocument
               {
                   SchemaVersion = document.SchemaVersion,
                   GameVersion = document.GameVersion,
                   Entries = document.Entries.Select(entry => new CatalogDocumentEntry
                                                              {
                                                                  AssetGuid = entry.AssetGuid,
                                                                  Kind = entry.Kind,
                                                                  Names = SortNames(entry.Names),
                                                                  RegionGuid = entry.RegionGuid,
                                                                  RegionTypes = entry.RegionTypes,
                                                                  IconReference = entry.IconReference
                                                              })
                                             .ToList()
               };
    }

    /// <summary>
    /// Sorts names by the ordinal order of their language tag.
    /// </summary>
    /// <param name="names">The names</param>
    /// <returns>The sorted names</returns>
    private static SortedDictionary<string, string> SortNames(IReadOnlyDictionary<string, string> names)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            sorted[name.Key] = name.Value;
        }

        return sorted;
    }

    /// <summary>
    /// Builds the result of a refused document.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    private static CatalogDocumentWriteResult Refuse(CatalogDocumentError error)
    {
        return new CatalogDocumentWriteResult(ReadOnlyMemory<byte>.Empty, error);
    }

    #endregion // Methods
}