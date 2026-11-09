namespace Censio.Companion.Core;

/// <summary>
/// The outcome kind of the game installation search.
/// </summary>
public enum GameInstallationStatusKind
{
    /// <summary>
    /// The search has not finished.
    /// </summary>
    Searching,

    /// <summary>
    /// An installation was found.
    /// </summary>
    Found,

    /// <summary>
    /// No installation was found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The folder of the settings file does not exist.
    /// </summary>
    SettingsFolderNotFound,

    /// <summary>
    /// The folder of the settings file is not a full path.
    /// </summary>
    SettingsFolderNotFullPath,

    /// <summary>
    /// The folder of the settings file is not a game installation.
    /// </summary>
    SettingsFolderNotInstallation,

    /// <summary>
    /// The settings file cannot be used.
    /// </summary>
    SettingsFileInvalid
}