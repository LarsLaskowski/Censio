namespace Censio.Companion.Core;

/// <summary>
/// The diagnostics window as the session sees it.
/// </summary>
public interface IDiagnosticsWindow
{
    #region Events

    /// <summary>
    /// Occurs when the window was closed.
    /// </summary>
    event EventHandler? WindowClosed;

    #endregion // Events

    #region Methods

    /// <summary>
    /// Shows a view.
    /// </summary>
    /// <param name="view">The view</param>
    void ShowView(DiagnosticsView view);

    /// <summary>
    /// Brings the window to the front.
    /// </summary>
    void BringToFront();

    #endregion // Methods
}