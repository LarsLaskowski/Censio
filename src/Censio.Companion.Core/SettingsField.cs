namespace Censio.Companion.Core;

/// <summary>
/// A field of the settings window.
/// </summary>
public enum SettingsField
{
    /// <summary>
    /// The mode.
    /// </summary>
    Mode,

    /// <summary>
    /// The port of the local server.
    /// </summary>
    LocalPort,

    /// <summary>
    /// The address of the server.
    /// </summary>
    ServerAddress,

    /// <summary>
    /// The token of the server.
    /// </summary>
    ServerToken,

    /// <summary>
    /// The hours the live buffer keeps.
    /// </summary>
    BufferHours,

    /// <summary>
    /// The megabytes the live buffer keeps.
    /// </summary>
    BufferMegabytes,

    /// <summary>
    /// The game folder.
    /// </summary>
    GameFolder,

    /// <summary>
    /// The language.
    /// </summary>
    Language,

    /// <summary>
    /// The name of a new profile.
    /// </summary>
    ProfileName
}