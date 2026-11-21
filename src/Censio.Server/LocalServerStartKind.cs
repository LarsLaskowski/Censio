namespace Censio.Server;

/// <summary>
/// The outcome of starting a local server.
/// </summary>
public enum LocalServerStartKind
{
    /// <summary>
    /// The server runs.
    /// </summary>
    Running,

    /// <summary>
    /// The port is taken by another program.
    /// </summary>
    PortInUse,

    /// <summary>
    /// The start failed.
    /// </summary>
    Failed
}