namespace Censio.Companion.Core.Tests;

/// <summary>
/// A settings window that records each shown view, each shown outcome and each request to come to the front, raises
/// <see cref="SaveRequested" /> when the test asks to save and <see cref="WindowClosed" /> when the test closes it.
/// </summary>
public sealed class FakeSettingsWindow : ISettingsWindow
{
    #region Fields

    /// <summary>
    /// Guards the recorded views, outcomes and the count.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The views shown, in order.
    /// </summary>
    private readonly List<SettingsView> _views = [];

    /// <summary>
    /// The outcomes shown, in order.
    /// </summary>
    private readonly List<SettingsSaveOutcome> _outcomes = [];

    /// <summary>
    /// The number of requests to come to the front.
    /// </summary>
    private int _bringToFrontCount;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the views shown, in order.
    /// </summary>
    public IReadOnlyList<SettingsView> Views
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
    /// Gets the outcomes shown, in order.
    /// </summary>
    public IReadOnlyList<SettingsSaveOutcome> Outcomes
    {
        get
        {
            lock (_lock)
            {
                return [.. _outcomes];
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
    /// Asks to save as the window's <c>Save</c> button would, raising <see cref="SaveRequested" />.
    /// </summary>
    /// <param name="input">What the user entered</param>
    public void RequestSave(SettingsInput input)
    {
        SaveRequested?.Invoke(this, input);
    }

    /// <summary>
    /// Closes the window as its close button or <c>Cancel</c> would, raising <see cref="WindowClosed" />.
    /// </summary>
    public void Close()
    {
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Methods

    #region ISettingsWindow

    /// <inheritdoc />
    public event EventHandler? WindowClosed;

    /// <inheritdoc />
    public event EventHandler<SettingsInput>? SaveRequested;

    /// <inheritdoc />
    public void ShowView(SettingsView view)
    {
        lock (_lock)
        {
            _views.Add(view);
        }
    }

    /// <inheritdoc />
    public void ShowOutcome(SettingsSaveOutcome outcome)
    {
        lock (_lock)
        {
            _outcomes.Add(outcome);
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

    #endregion // ISettingsWindow
}