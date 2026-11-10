using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// Reads the key of the catalog cache from an installation.
/// </summary>
public static class GameCatalogKeyReader
{
    #region Methods

    /// <summary>
    /// Reads the key of an installation.
    /// </summary>
    /// <param name="installation">The status of the game installation search</param>
    /// <param name="identity">The identity of the Companion build</param>
    /// <returns>The key, or <c>null</c> when the installation cannot be read</returns>
    public static GameCatalogKey? Read(GameInstallationStatus installation, string identity)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}