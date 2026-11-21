namespace Censio.Companion.Core;

/// <summary>
/// Names the folders of the Companion.
/// </summary>
public static class CompanionFolders
{
    #region Methods

    /// <summary>
    /// Gets the log folder.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The log folder</returns>
    public static string Logs(string localApplicationData)
    {
        return Path.Combine(localApplicationData, "Censio", "Logs");
    }

    /// <summary>
    /// Gets the folder of the catalog cache.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The cache folder</returns>
    public static string Cache(string localApplicationData)
    {
        return Path.Combine(localApplicationData, "Censio", "Cache");
    }

    /// <summary>
    /// Gets the path of the settings file.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The path of the settings file</returns>
    public static string Settings(string localApplicationData)
    {
        return Path.Combine(localApplicationData, "Censio", "settings.json");
    }

    /// <summary>
    /// Gets the data folder of the local server.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The server folder</returns>
    public static string Server(string localApplicationData)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }

    #endregion // Methods
}