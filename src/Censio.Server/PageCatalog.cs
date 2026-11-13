using Censio.Contracts;

namespace Censio.Server;

/// <summary>
/// A lookup over the catalog document by asset GUID.
/// </summary>
public sealed class PageCatalog
{
    #region Methods

    /// <summary>
    /// Creates the lookup of a catalog document.
    /// </summary>
    /// <param name="document">The catalog document</param>
    /// <returns>The lookup</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static PageCatalog From(CatalogDocument document)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Finds the entry of an asset.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <returns>The entry, or <see langword="null"/> when the catalog has none</returns>
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public PageCatalogEntry? Find(int assetGuid)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    #endregion // Methods
}