namespace Censio.Companion.Core;

/// <summary>
/// Where the game installation was found.
/// </summary>
public enum GameInstallationSource
{
    /// <summary>
    /// The folder is named in the settings file.
    /// </summary>
    SettingsFile,

    /// <summary>
    /// The folder comes from Ubisoft Connect.
    /// </summary>
    UbisoftConnect
}