namespace Censio.GameData;

/// <summary>
/// Reads the icons of the catalog entries from the game's UI archives.
/// </summary>
public static class GameIconReader
{
    #region Constants

    /// <summary>
    /// The longest side in pixels an icon may have.
    /// </summary>
    public const int MaxIconSide = 2048;

    /// <summary>
    /// The most bytes an icon's archive entry may take.
    /// </summary>
    public const int MaxIconEntryBytes = 8388608;

    /// <summary>
    /// The longest icon entry path in characters.
    /// </summary>
    public const int MaxIconPathLength = 1024;

    /// <summary>
    /// The most bytes of PNG data the kept icons may take together.
    /// </summary>
    public const int MaxIconSetBytes = 268435456;

    /// <summary>
    /// The name of the base UI archive.
    /// </summary>
    public const string UiArchiveName = "ui.rda";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Loads the icons of the catalog's entries.
    /// </summary>
    /// <param name="installationFolder">The game's installation folder</param>
    /// <param name="catalog">The catalog whose entries name the icons</param>
    /// <returns>The icons or the reason they could not be loaded</returns>
    public static GameIconResult Load(string installationFolder, GameCatalog catalog)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Derives the archive entry path of an icon file name.
    /// </summary>
    /// <param name="iconFilename">The icon file name an asset states</param>
    /// <returns>The entry path, or <c>null</c> when the name yields none</returns>
    public static string? EntryPathOf(string? iconFilename)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the patch number of a UI archive file name.
    /// </summary>
    /// <param name="fileName">The archive file name</param>
    /// <returns>The patch number, or <c>null</c> when the name is not a patch archive</returns>
    public static uint? PatchNumberOf(string fileName)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}