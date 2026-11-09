namespace Censio.Companion.Core;

/// <summary>
/// The kind of an entry of the tray menu.
/// </summary>
public enum TrayMenuEntryKind
{
    /// <summary>
    /// The entry that shows the product version.
    /// </summary>
    Version,

    /// <summary>
    /// The entry that shows the status.
    /// </summary>
    Status,

    /// <summary>
    /// The entry that shows the last tick.
    /// </summary>
    LastTick,

    /// <summary>
    /// The entry that starts or stops the recording.
    /// </summary>
    ToggleRecording,

    /// <summary>
    /// The entry that opens the recordings folder.
    /// </summary>
    OpenRecordingsFolder,

    /// <summary>
    /// The entry that opens the log folder.
    /// </summary>
    OpenLogFolder,

    /// <summary>
    /// The entry that opens the diagnostics window.
    /// </summary>
    OpenDiagnostics,

    /// <summary>
    /// A separator line.
    /// </summary>
    Separator,

    /// <summary>
    /// The entry that exits the application.
    /// </summary>
    Exit,

    /// <summary>
    /// The entry that turns the start with Windows on or off.
    /// </summary>
    ToggleAutostart,

    /// <summary>
    /// The entry that shows the game installation.
    /// </summary>
    Game,

    /// <summary>
    /// The entry that shows the game catalog.
    /// </summary>
    Catalog
}