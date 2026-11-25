namespace Censio.Companion.Core;

/// <summary>
/// The mode of the Companion's server connection.
/// </summary>
public enum CompanionSettingsMode
{
    /// <summary>
    /// No server.
    /// </summary>
    Off,

    /// <summary>
    /// The Companion hosts the server.
    /// </summary>
    Local,

    /// <summary>
    /// The Companion forwards to a server.
    /// </summary>
    Forward
}