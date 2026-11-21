namespace Censio.Companion.Core;

/// <summary>
/// The state of the local server.
/// </summary>
public enum LocalServerState
{
    /// <summary>
    /// The server is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// The server runs.
    /// </summary>
    Running,

    /// <summary>
    /// The port is taken by another program.
    /// </summary>
    PortInUse,

    /// <summary>
    /// The server failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The setting is not valid, so no server was started.
    /// </summary>
    SettingsInvalid
}