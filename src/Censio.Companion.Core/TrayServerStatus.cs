namespace Censio.Companion.Core;

/// <summary>
/// The server status the tray menu shows: that of the local server or that of live sending, never both.
/// </summary>
public readonly record struct TrayServerStatus
{
    #region Properties

    /// <summary>
    /// Gets the status of the local server, or <see langword="null" /> when none is hosted.
    /// </summary>
    public LocalServerStatus? Local { get; init; }

    /// <summary>
    /// Gets the status of live sending in forward mode, or <see langword="null" /> in any other mode.
    /// </summary>
    public LiveSendStatus? Live { get; init; }

    /// <summary>
    /// Gets the status of the live buffer, or <see langword="null" /> when there is none.
    /// </summary>
    public LiveBufferStatus? Buffer { get; init; }

    #endregion // Properties
}