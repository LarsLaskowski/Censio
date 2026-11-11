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
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}