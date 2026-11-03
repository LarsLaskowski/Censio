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
    /// Runs an action on the thread of the window.
    /// </summary>
    private readonly Action<Action> _post;

    /// <summary>
    /// Creates the window.
    /// </summary>
    private readonly Func<IDiagnosticsWindow> _createWindow;

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
        _post = post;
        _createWindow = createWindow;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the window is open.
    /// </summary>
    public bool IsOpen => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the window, or brings the open one to the front.
    /// </summary>
    public void Open()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells the session that the content may have changed.
    /// </summary>
    public void Notify()
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}