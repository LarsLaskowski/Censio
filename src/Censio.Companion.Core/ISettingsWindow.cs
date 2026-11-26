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

    #endregion // Events

    #region Methods

    /// <summary>
    /// Shows a view.
    /// </summary>
    /// <param name="view">The view</param>
    void ShowView(SettingsView view);

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