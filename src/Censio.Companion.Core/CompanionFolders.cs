namespace Censio.Companion.Core;

/// <summary>
/// Names the folders of the Companion.
/// </summary>
public static class CompanionFolders
{
    #region Constants

    /// <summary>
    /// The name of the folder of the Companion below the local application data.
    /// </summary>
    private const string ApplicationFolderName = "Censio";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Gets the log folder.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The log folder</returns>
    public static string Logs(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "Logs");
    }

    /// <summary>
    /// Gets the folder of the catalog cache.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The cache folder</returns>
    public static string Cache(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "Cache");
    }

    /// <summary>
    /// Gets the folder of the live buffer.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The buffer folder</returns>
    public static string Buffer(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "Buffer");
    }

    /// <summary>
    /// Gets the path of the settings file.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The path of the settings file</returns>
    public static string Settings(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "settings.json");
    }

    /// <summary>
    /// Gets the data folder of the local server.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The server folder</returns>
    public static string Server(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "Server");
    }

    /// <summary>
    /// Gets the path of the profiles file.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The path of the profiles file</returns>
    public static string Profiles(string localApplicationData)
    {
        return Path.Combine(localApplicationData, ApplicationFolderName, "profiles.json");
    }

    #endregion // Methods
}