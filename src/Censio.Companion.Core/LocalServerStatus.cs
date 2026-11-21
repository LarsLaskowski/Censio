namespace Censio.Companion.Core;

/// <summary>
/// The status of the local server.
/// </summary>
public readonly record struct LocalServerStatus
{
    #region Properties

    /// <summary>
    /// Gets the state.
    /// </summary>
    public required LocalServerState State { get; init; }

    /// <summary>
    /// Gets the port.
    /// </summary>
    public required int Port { get; init; }

    #endregion // Properties
}