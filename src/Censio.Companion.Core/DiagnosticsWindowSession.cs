using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// Keeps the diagnostics window and updates its content.
/// </summary>
public sealed class DiagnosticsWindowSession
{
    #region Fields

    /// <summary>
    /// Reads the status text.
    /// </summary>
    private readonly Func<string> _readStatusText;

    /// <summary>
    /// Reads the latest closed tick.
    /// </summary>
    private readonly Func<PipeTick?> _readTick;

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates the window.
    /// </summary>
    private readonly Func<IDiagnosticsWindow> _createWindow;

    /// <summary>
    /// Hands the content over to the window with at most one pending update.
    /// </summary>
    private readonly TrayStatusUpdater<DiagnosticsView?> _updater;

    /// <summary>
    /// The open window, or <c>null</c> if none.
    /// </summary>
    private IDiagnosticsWindow? _window;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsWindowSession" /> class.
    /// </summary>
    /// <param name="readStatusText">Reads the status text</param>
    /// <param name="readTick">Reads the latest closed tick</param>
    /// <param name="clock">The clock</param>
    /// <param name="post">Runs an action on the thread of the window</param>
    /// <param name="createWindow">Creates the window</param>
    public DiagnosticsWindowSession(Func<string> readStatusText, Func<PipeTick?> readTick, TimeProvider clock, Action<Action> post, Func<IDiagnosticsWindow> createWindow)
    {
        _readStatusText = readStatusText;
        _readTick = readTick;
        _clock = clock;
        _createWindow = createWindow;
        _updater = new TrayStatusUpdater<DiagnosticsView?>(ReadView, post, ShowUpdate);
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the window is open.
    /// </summary>
    public bool IsOpen => _window is not null;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the window, or brings the open one to the front; callable on the thread of the window only.
    /// </summary>
    public void Open()
    {
        if (_window is { } open)
        {
            open.BringToFront();

            return;
        }

        var window = _createWindow();

        window.WindowClosed += OnWindowClosed;
        _window = window;
        window.ShowView(CreateView());
    }

    /// <summary>
    /// Tells the session that the content may have changed; callable from any thread.
    /// </summary>
    public void Notify()
    {
        _updater.Notify();
    }

    /// <summary>
    /// Creates a view of the current state.
    /// </summary>
    /// <returns>The view</returns>
    private DiagnosticsView CreateView()
    {
        return DiagnosticsView.Create(_readStatusText(), _readTick(), _clock);
    }

    /// <summary>
    /// Creates the view for an update, or nothing while no window is open.
    /// </summary>
    /// <returns>The view, or <c>null</c> if no window is open</returns>
    private DiagnosticsView? ReadView()
    {
        return _window is null ? null : CreateView();
    }

    /// <summary>
    /// Shows a view in the open window.
    /// </summary>
    /// <param name="view">The view, or <c>null</c> if none</param>
    private void ShowUpdate(DiagnosticsView? view)
    {
        if (view is not null)
        {
            _window?.ShowView(view);
        }
    }

    /// <summary>
    /// Drops the window and its view.
    /// </summary>
    /// <param name="sender">The window</param>
    /// <param name="eventArgs">The event data</param>
    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is IDiagnosticsWindow window)
        {
            window.WindowClosed -= OnWindowClosed;
        }

        _window = null;
    }

    #endregion // Methods
}