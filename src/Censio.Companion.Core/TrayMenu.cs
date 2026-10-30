namespace Censio.Companion.Core;

/// <summary>
/// Builds the entries of the tray menu.
/// </summary>
public static class TrayMenu
{
    #region Constants

    /// <summary>
    /// The tooltip of the tray icon.
    /// </summary>
    public const string ToolTip = "Censio Companion";

    /// <summary>
    /// The text of the exit entry.
    /// </summary>
    public const string ExitText = "Exit";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats the text of the version entry.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <returns>The text of the version entry</returns>
    public static string FormatVersion(string version)
    {
        return "Version " + version;
    }

    /// <summary>
    /// Creates the entries of the tray menu.
    /// </summary>
    /// <param name="version">The product version</param>
    /// <returns>The entries in display order</returns>
    public static IReadOnlyList<TrayMenuEntry> Create(string version)
    {
        return
        [
            new TrayMenuEntry(TrayMenuEntryKind.Version, FormatVersion(version), false),
            new TrayMenuEntry(TrayMenuEntryKind.Separator, string.Empty, false),
            new TrayMenuEntry(TrayMenuEntryKind.Exit, ExitText, true)
        ];
    }

    #endregion // Methods
}