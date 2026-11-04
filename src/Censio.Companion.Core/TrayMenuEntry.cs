namespace Censio.Companion.Core;

/// <summary>
/// An entry of the tray menu.
/// </summary>
public sealed class TrayMenuEntry
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayMenuEntry" /> class.
    /// </summary>
    /// <param name="kind">The kind of the entry</param>
    /// <param name="text">The text of the entry</param>
    /// <param name="isEnabled">Whether the entry can be chosen</param>
    public TrayMenuEntry(TrayMenuEntryKind kind, string text, bool isEnabled)
        : this(kind, text, isEnabled, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayMenuEntry" /> class.
    /// </summary>
    /// <param name="kind">The kind of the entry</param>
    /// <param name="text">The text of the entry</param>
    /// <param name="isEnabled">Whether the entry can be chosen</param>
    /// <param name="isChecked">Whether the entry shows a check mark</param>
    public TrayMenuEntry(TrayMenuEntryKind kind, string text, bool isEnabled, bool isChecked)
    {
        Kind = kind;
        Text = text;
        IsEnabled = isEnabled;
        IsChecked = isChecked;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the kind of the entry.
    /// </summary>
    public TrayMenuEntryKind Kind { get; }

    /// <summary>
    /// Gets the text of the entry.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets a value indicating whether the entry can be chosen.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether the entry shows a check mark.
    /// </summary>
    public bool IsChecked { get; }

    #endregion // Properties
}