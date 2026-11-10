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
        if (installation.Kind != GameInstallationStatusKind.Found || installation.Folder is not { } folder)
        {
            return null;
        }

        try
        {
            if (File.Exists(GameInstallation.ProgramFilePath(folder)) == false)
            {
                return null;
            }

            return new GameCatalogKey(identity, folder, installation.Build, GameArchiveStateReader.Read(folder));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    #endregion // Methods
}