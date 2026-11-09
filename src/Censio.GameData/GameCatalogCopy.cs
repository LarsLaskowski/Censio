namespace Censio.GameData;

/// <summary>
/// Describes a catalog file that another archive lists besides the configuration archive.
/// </summary>
public sealed class GameCatalogCopy
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogCopy" /> class.
    /// </summary>
    /// <param name="archiveName">The name of the archive that lists the copy</param>
    /// <param name="path">The path of the copy</param>
    public GameCatalogCopy(string archiveName, string path)
    {
        ArchiveName = archiveName;
        Path = path;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the name of the archive that lists the copy.
    /// </summary>
    public string ArchiveName { get; }

    /// <summary>
    /// Gets the path of the copy.
    /// </summary>
    public string Path { get; }

    #endregion // Properties
}