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
    /// A separator line.
    /// </summary>
    Separator,

    /// <summary>
    /// The entry that exits the application.
    /// </summary>
    Exit
}