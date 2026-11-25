namespace Censio.Companion.Core;

/// <summary>
/// The kind of the outcome of saving the settings.
/// </summary>
public enum SettingsSaveKind
{
    /// <summary>
    /// The settings were saved.
    /// </summary>
    Saved,

    /// <summary>
    /// The settings were saved and the Companion restarts.
    /// </summary>
    Restarting,

    /// <summary>
    /// The values were refused.
    /// </summary>
    Refused,

    /// <summary>
    /// The settings file could not be written.
    /// </summary>
    WriteFailed,

    /// <summary>
    /// The settings file was changed outside the window.
    /// </summary>
    ChangedOutside,

    /// <summary>
    /// The settings were saved but the restart failed.
    /// </summary>
    RestartFailed
}