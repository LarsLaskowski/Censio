namespace Censio.Companion.Core.Tests;

/// <summary>
/// A diagnostics window that records each shown view and each request to come to the front, and raises
/// <see cref="WindowClosed" /> when the test closes it.
/// </summary>
public sealed class FakeDiagnosticsWindow : IDiagnosticsWindow
{
    #region Fields

    /// <summary>
    /// Guards the recorded views and the count.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The views shown, in order.
    /// </summary>
    private readonly List<DiagnosticsView> _views = [];

    /// <summary>
    /// The number of requests to come to the front.
    /// </summary>
    private int _bringToFrontCount;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the views shown, in order.
    /// </summary>
    public IReadOnlyList<DiagnosticsView> Views
    {
        get
        {
            lock (_lock)
            {
                return [.. _views];
            }
        }
    }

    /// <summary>
    /// Gets the number of requests to come to the front.
    /// </summary>
    public int BringToFrontCount
    {
        get
        {
            lock (_lock)
            {
                return _bringToFrontCount;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Closes the window as its close button would, raising <see cref="WindowClosed" />.
    /// </summary>
    public void Close()
    {
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Methods

    #region IDiagnosticsWindow

    /// <inheritdoc />
    public event EventHandler? WindowClosed;

    /// <inheritdoc />
    public void ShowView(DiagnosticsView view)
    {
        lock (_lock)
        {
            _views.Add(view);
        }
    }

    /// <inheritdoc />
    public void BringToFront()
    {
        lock (_lock)
        {
            _bringToFrontCount++;
        }
    }

    #endregion // IDiagnosticsWindow
}