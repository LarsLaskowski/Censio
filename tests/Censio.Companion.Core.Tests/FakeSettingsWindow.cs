namespace Censio.Companion.Core.Tests;

/// <summary>
/// A settings window that records each shown view, each shown view of the profiles, each shown outcome and each request
/// to come to the front, raises <see cref="SaveRequested" /> when the test asks to save, <see cref="DeleteRequested" />
/// when the test asks to delete a profile and <see cref="WindowClosed" /> when the test closes it.
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
    /// The views shown with <see cref="ShowProfiles" />, in order.
    /// </summary>
    private readonly List<SettingsView> _profileViews = [];

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
    /// Gets the views shown with <see cref="ShowProfiles" />, in order.
    /// </summary>
    public IReadOnlyList<SettingsView> ProfileViews
    {
        get
        {
            lock (_lock)
            {
                return [.. _profileViews];
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
    /// Asks to delete a profile as the window's dialog would after <c>Delete</c>, raising <see cref="DeleteRequested" />.
    /// </summary>
    /// <param name="deletion">The profile and the box of the dialog</param>
    public void RequestDelete(SettingsDeletion deletion)
    {
        DeleteRequested?.Invoke(this, deletion);
    }

    /// <summary>
    /// Closes the window as its close button or <c>Cancel</c> would, raising <see cref="WindowClosed" />.
    /// </summary>
    public void Close()
    {
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="WindowClosed" /> with another sender than this window.
    /// </summary>
    /// <param name="sender">The sender</param>
    public void RaiseWindowClosed(object? sender)
    {
        WindowClosed?.Invoke(sender, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="SaveRequested" /> with another sender than this window.
    /// </summary>
    /// <param name="sender">The sender</param>
    /// <param name="input">What the user entered</param>
    public void RaiseSaveRequested(object? sender, SettingsInput input)
    {
        SaveRequested?.Invoke(sender, input);
    }

    #endregion // Methods

    #region ISettingsWindow

    /// <inheritdoc />
    public event EventHandler? WindowClosed;

    /// <inheritdoc />
    public event EventHandler<SettingsInput>? SaveRequested;

    /// <inheritdoc />
    public event EventHandler<SettingsDeletion>? DeleteRequested;

    /// <inheritdoc />
    public void ShowView(SettingsView view)
    {
        lock (_lock)
        {
            _views.Add(view);
        }
    }

    /// <inheritdoc />
    public void ShowProfiles(SettingsView view)
    {
        lock (_lock)
        {
            _profileViews.Add(view);
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