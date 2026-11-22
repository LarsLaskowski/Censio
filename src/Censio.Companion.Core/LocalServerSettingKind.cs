namespace Censio.Companion.Core;

/// <summary>
/// The kind of the local server setting.
/// </summary>
public enum LocalServerSettingKind
{
    /// <summary>
    /// No local server is hosted.
    /// </summary>
    Off,

    /// <summary>
    /// The Companion hosts the server.
    /// </summary>
    Local,

    /// <summary>
    /// The setting is not valid.
    /// </summary>
    Invalid,

    /// <summary>
    /// The Companion sends live data to the configured server.
    /// </summary>
    Forward
}