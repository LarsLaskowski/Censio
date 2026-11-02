namespace Censio.Companion.Core;

/// <summary>
/// Hands a status over to the tray with at most one pending update.
/// </summary>
/// <typeparam name="TStatus">The type of the status</typeparam>
public sealed class TrayStatusUpdater<TStatus>
{
    #region Fields

    /// <summary>
    /// Reads the current status.
    /// </summary>
    private readonly Func<TStatus> _readStatus;

    /// <summary>
    /// Posts a callback to the thread of the tray.
    /// </summary>
    private readonly Action<Action> _post;

    /// <summary>
    /// Shows a status in the tray.
    /// </summary>
    private readonly Action<TStatus> _show;

    /// <summary>
    /// Whether a post is waiting to run; 1 if so, otherwise 0.
    /// </summary>
    private int _pending;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayStatusUpdater{TStatus}" /> class.
    /// </summary>
    /// <param name="readStatus">Reads the current status</param>
    /// <param name="post">Posts a callback to the thread of the tray</param>
    /// <param name="show">Shows a status in the tray</param>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c></exception>
    public TrayStatusUpdater(Func<TStatus> readStatus, Action<Action> post, Action<TStatus> show)
    {
        ArgumentNullException.ThrowIfNull(readStatus);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(show);

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
        if (Interlocked.Exchange(ref _pending, 1) == 0)
        {
            _post(ShowCurrent);
        }
    }

    /// <summary>
    /// Clears the pending flag, then shows the current status.
    /// </summary>
    private void ShowCurrent()
    {
        Volatile.Write(ref _pending, 0);

        _show(_readStatus());
    }

    #endregion // Methods
}