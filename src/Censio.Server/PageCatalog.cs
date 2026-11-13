using Censio.Contracts;

namespace Censio.Server;

/// <summary>
/// A lookup over the catalog document by asset GUID.
/// </summary>
public sealed class PageCatalog
{
    #region Fields

    /// <summary>
    /// The entries by asset GUID.
    /// </summary>
    private readonly Dictionary<int, PageCatalogEntry> _entries;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PageCatalog"/> class.
    /// </summary>
    /// <param name="entries">The entries by asset GUID</param>
    private PageCatalog(Dictionary<int, PageCatalogEntry> entries)
    {
        _entries = entries;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Creates the lookup of a catalog document.
    /// </summary>
    /// <param name="document">The catalog document</param>
    /// <returns>The lookup</returns>
    public static PageCatalog From(CatalogDocument document)
    {
        var entries = new Dictionary<int, PageCatalogEntry>();

        foreach (var entry in document.Entries)
        {
            entries[entry.AssetGuid] = new PageCatalogEntry
                                       {
                                           Kind = entry.Kind,
                                           English = entry.Names.GetValueOrDefault("en"),
                                           German = entry.Names.GetValueOrDefault("de"),
                                           RegionGuid = entry.RegionGuid,
                                           IconReference = entry.IconReference
                                       };
        }

        return new PageCatalog(entries);
    }

    /// <summary>
    /// Finds the entry of an asset.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The entry, or <see langword="null"/> when the catalog has none</returns>
    public PageCatalogEntry? Find(int assetGuid)
    {
        return _entries.GetValueOrDefault(assetGuid);
    }

    #endregion // Methods
}