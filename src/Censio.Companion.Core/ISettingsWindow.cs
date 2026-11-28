namespace Censio.Companion.Core;

/// <summary>
/// The settings window as the session sees it.
/// </summary>
public interface ISettingsWindow
{
    #region Events

    /// <summary>
    /// Occurs when the window was closed.
    /// </summary>
    event EventHandler? WindowClosed;

    /// <summary>
    /// Occurs when the user asks to save the settings.
    /// </summary>
    event EventHandler<SettingsInput>? SaveRequested;

    /// <summary>
    /// Occurs when the user asks to delete a profile.
    /// </summary>
    event EventHandler<SettingsDeletion>? DeleteRequested;

    /// <summary>
    /// Occurs when the user asks to rename a profile.
    /// </summary>
    event EventHandler<SettingsRename>? RenameRequested;

    /// <summary>
    /// Occurs when the user asks to merge a profile into another.
    /// </summary>
    event EventHandler<SettingsMerge>? MergeRequested;

    #endregion // Events

    #region Methods

    /// <summary>
    /// Shows a view.
    /// </summary>
    /// <param name="view">The view</param>
    void ShowView(SettingsView view);

    /// <summary>
    /// Shows the profiles: the drop-down with the chosen profile kept selected while it is still listed and the active one otherwise, the count line and the state of the delete button.
    /// </summary>
    /// <param name="view">The view</param>
    void ShowProfiles(SettingsView view);

    /// <summary>
    /// Shows the outcome of a save.
    /// </summary>
    /// <param name="outcome">The outcome</param>
    void ShowOutcome(SettingsSaveOutcome outcome);

    /// <summary>
    /// Brings the window to the front.
    /// </summary>
    void BringToFront();

    #endregion // Methods
}