namespace Censio.GameData;

/// <summary>
/// Writes and reads the binary catalog cache file.
/// </summary>
public static class GameCatalogCacheFile
{
    #region Constants

    /// <summary>
    /// The version of the file format.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// The largest file size in bytes that is read.
    /// </summary>
    public const long MaxFileBytes = 335544320;

    /// <summary>
    /// The largest size in bytes of everything but the PNG data.
    /// </summary>
    public const long MaxCatalogBytes = 67108864;

    /// <summary>
    /// The largest size in bytes of one string.
    /// </summary>
    public const int MaxStringBytes = 65536;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Writes the cache file.
    /// </summary>
    /// <param name="destination">The stream to write to</param>
    /// <param name="key">The key the content belongs to</param>
    /// <param name="catalog">The catalog</param>
    /// <param name="icons">The icons</param>
    /// <returns><c>false</c> when a bound would be exceeded and nothing more was written</returns>
    public static bool Write(Stream destination, GameCatalogKey key, GameCatalog catalog, GameIconSet icons)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads and validates the cache file as a whole.
    /// </summary>
    /// <param name="source">The stream to read from</param>
    /// <param name="length">The length of the file in bytes</param>
    /// <param name="expected">The key the content must belong to</param>
    /// <returns>The content, or the reason there is none</returns>
    public static GameCatalogCacheResult Read(Stream source, long length, GameCatalogKey expected)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}