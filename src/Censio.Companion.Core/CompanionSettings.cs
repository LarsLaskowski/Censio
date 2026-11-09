namespace Censio.Companion.Core;

/// <summary>
/// Reads the settings file of the Companion.
/// </summary>
public static class CompanionSettings
{
    #region Constants

    /// <summary>
    /// The maximum length of the settings file in bytes.
    /// </summary>
    public const int MaxFileLength = 1048576;

    /// <summary>
    /// The key of the game folder in the settings file.
    /// </summary>
    public const string GameFolderKey = "gameFolder";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The result</returns>
    public static CompanionSettingsResult Read(string path)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}