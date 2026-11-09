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
    /// Gets the path of the settings file.
    /// </summary>
    /// <param name="localApplicationData">The local application data folder</param>
    /// <returns>The path of the settings file</returns>
    public static string Settings(string localApplicationData)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}