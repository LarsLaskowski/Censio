namespace Censio.Companion.Core;

/// <summary>
/// Keeps the settings window and saves what the user enters.
/// </summary>
public sealed class SettingsWindowSession
{
#pragma warning disable S4487, CS0169, IDE0044, S1144 // Skeleton: removed with the implementation

    #region Fields

    /// <summary>
    /// What the session needs from the running Companion.
    /// </summary>
    private readonly SettingsWindowContext _context;

    /// <summary>
    /// Runs an action on the thread of the window.
    /// </summary>
    private readonly Action<Action> _post;

    /// <summary>
    /// Creates the window.
    /// </summary>
    private readonly Func<ISettingsWindow> _createWindow;

    /// <summary>
    /// The open window, or <c>null</c> if none.
    /// </summary>
    private ISettingsWindow? _window;

    #endregion // Fields

#pragma warning restore S4487, CS0169, IDE0044, S1144

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindowSession" /> class.
    /// </summary>
    /// <param name="context">What the session needs from the running Companion</param>
    /// <param name="post">Runs an action on the thread of the window</param>
    /// <param name="createWindow">Creates the window</param>
    public SettingsWindowSession(SettingsWindowContext context, Action<Action> post, Func<ISettingsWindow> createWindow)
    {
        _context = context;
        _post = post;
        _createWindow = createWindow;
    }

    #endregion // Constructor

#pragma warning disable S2325, CA1065, RH2003 // Skeleton: removed with the implementation

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
    /// Shows the open window again with the texts of the current language.
    /// </summary>
    public void RefreshTexts()
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

#pragma warning restore S2325, CA1065, RH2003
}