namespace Censio.Companion.Core;

/// <summary>
/// How the tray icon looks.
/// </summary>
public enum TrayIconLook
{
    /// <summary>
    /// The Companion waits for the game or for a connection to deliver data.
    /// </summary>
    Waiting,

    /// <summary>
    /// The Companion receives data.
    /// </summary>
    Receiving,

    /// <summary>
    /// The Companion has a problem.
    /// </summary>
    Problem
}