namespace Censio.Companion.Core;

/// <summary>
/// Hands the connection status over to the tray with at most one pending update.
/// </summary>
public sealed class TrayStatusUpdater
{
    #region Fields

    /// <summary>
    /// Reads the current status.
    /// </summary>
    private readonly Func<PipeConnectionStatus> _readStatus;

    /// <summary>
    /// Posts a callback to the thread of the tray.
    /// </summary>
    private readonly Action<Action> _post;

    /// <summary>
    /// Shows a status in the tray.
    /// </summary>
    private readonly Action<PipeConnectionStatus> _show;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayStatusUpdater" /> class.
    /// </summary>
    /// <param name="readStatus">Reads the current status</param>
    /// <param name="post">Posts a callback to the thread of the tray</param>
    /// <param name="show">Shows a status in the tray</param>
    public TrayStatusUpdater(Func<PipeConnectionStatus> readStatus, Action<Action> post, Action<PipeConnectionStatus> show)
    {
        _readStatus = readStatus;
        _post = post;
        _show = show;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Announces a status change; callable from any thread, with at most one post pending.
    /// </summary>
    public void Notify()
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}