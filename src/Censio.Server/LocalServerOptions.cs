namespace Censio.Server;

/// <summary>
/// The options of a server hosted in the Companion's process.
/// </summary>
public sealed class LocalServerOptions
{
    #region Properties

    /// <summary>
    /// Gets the port the server listens on.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets the data directory of the server.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// Gets the content root of the server.
    /// </summary>
    public required string ContentRoot { get; init; }

    /// <summary>
    /// Gets the logger provider the server logs through; the server never disposes it.
    /// </summary>
    public required ILoggerProvider LogProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether the IPv6 loopback address is listened on as well.
    /// </summary>
    public bool ListenOnIPv6 { get; init; }

    #endregion // Properties
}