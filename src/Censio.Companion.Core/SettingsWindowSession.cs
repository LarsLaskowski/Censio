namespace Censio.Companion.Core;

/// <summary>
/// Keeps the settings window and saves what the user enters.
/// </summary>
public sealed class SettingsWindowSession
{
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
    private volatile ISettingsWindow? _window;

    /// <summary>
    /// The settings file as the open window read it, or <c>null</c> until it was read.
    /// </summary>
    private CompanionSettingsDocument? _document;

    /// <summary>
    /// The problems of the file that block saving until their field is edited.
    /// </summary>
    private IReadOnlyList<SettingsProblem> _blocking = [];

    /// <summary>
    /// The language the settings file holds, <c>null</c> if it holds none.
    /// </summary>
    private CompanionLanguage? _fileLanguage;

    /// <summary>
    /// The view shown last, or <c>null</c> until the first one is shown.
    /// </summary>
    private SettingsView? _lastView;

    /// <summary>
    /// Whether a save is running.
    /// </summary>
    private bool _isSaving;

    #endregion // Fields

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

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the window is open.
    /// </summary>
    public bool IsOpen => _window is not null;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the window, or brings the open one to the front.
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
        window.SaveRequested += OnSaveRequested;
        window.DeleteRequested += OnDeleteRequested;
        _context.Profiles.DeletionsChanged += OnDeletionsChanged;
        _window = window;
        _document = null;
        _lastView = null;
        _isSaving = false;

        _ = Task.Run(() => LoadView(window));
    }

    /// <summary>
    /// Shows the open window again with the texts of the current language.
    /// </summary>
    public void RefreshTexts()
    {
        _post(ShowAgain);
    }

    /// <summary>
    /// Creates the warnings of the run state.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="state">The run state</param>
    /// <returns>The warnings, one line each</returns>
    private static List<string> CreateWarnings(CompanionTexts texts, SettingsRunState state)
    {
        var warnings = new List<string>();

        if (state.IsReplay)
        {
            warnings.Add(texts.WarningReplayRunning);
        }

        if (state.IsRecording)
        {
            warnings.Add(texts.WarningRecordingRunning);
        }

        if (state.IsBufferMemoryOnly)
        {
            warnings.Add(texts.WarningBufferMemoryOnly);
        }

        return warnings;
    }

    /// <summary>
    /// Creates a problem.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="kind">The kind</param>
    /// <returns>The problem</returns>
    private static SettingsProblem CreateProblem(SettingsField field, SettingsProblemKind kind)
    {
        return new SettingsProblem
               {
                   Field = field,
                   Kind = kind
               };
    }

    /// <summary>
    /// Creates an outcome without problems.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <returns>The outcome</returns>
    private static SettingsSaveOutcome CreateOutcome(SettingsSaveKind kind)
    {
        return new SettingsSaveOutcome
               {
                   Kind = kind
               };
    }

    /// <summary>
    /// Creates a copy of values with another language.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="language">The language, <c>null</c> for none</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues WithLanguage(CompanionSettingsValues values, CompanionLanguage? language)
    {
        return new CompanionSettingsValues
               {
                   Mode = values.Mode,
                   LocalPort = values.LocalPort,
                   ServerAddress = values.ServerAddress,
                   ServerToken = values.ServerToken,
                   BufferHours = values.BufferHours,
                   BufferMegabytes = values.BufferMegabytes,
                   GameFolder = values.GameFolder,
                   Language = language
               };
    }

    /// <summary>
    /// Reads the settings file and checks its game folder, off the thread of the window, then shows the first view.
    /// </summary>
    /// <param name="window">The window</param>
    private void LoadView(ISettingsWindow window)
    {
        var document = CompanionSettingsDocument.Load(_context.SettingsPath);
        var problems = document.FieldProblems.ToList();

        if (problems.Any(problem => problem.Field == SettingsField.GameFolder) == false)
        {
            var folderProblems = CompanionSettingsValidator.Validate(document.Values, _context.DirectoryExists, _context.IsInstallation)
                                                           .Where(problem => problem.Field == SettingsField.GameFolder);

            problems.AddRange(folderProblems);
        }

        _post(() => ShowFirst(window, document, problems));
    }

    /// <summary>
    /// Shows the first view of a window, unless it was closed meanwhile.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="document">The settings file as read</param>
    /// <param name="problems">The problems of the fields</param>
    private void ShowFirst(ISettingsWindow window, CompanionSettingsDocument document, IReadOnlyList<SettingsProblem> problems)
    {
        if (ReferenceEquals(_window, window) == false)
        {
            return;
        }

        _document = document;
        _blocking = document.FieldProblems;
        _fileLanguage = document.Values.Language;

        _ = _context.Autostart.Refresh();

        Show(window, CreateView(document.Values, document.FileProblem, problems));
    }

    /// <summary>
    /// Shows the last view again with the texts of the current language.
    /// </summary>
    private void ShowAgain()
    {
        if (_window is { } window && _lastView is { } view)
        {
            Show(window, CreateView(view.Values, view.FileProblem, view.Problems));
        }
    }

    /// <summary>
    /// Shows a view and keeps it as the last one.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="view">The view</param>
    private void Show(ISettingsWindow window, SettingsView view)
    {
        _lastView = view;

        window.ShowView(view);
    }

    /// <summary>
    /// Creates a view of the current state of the Companion.
    /// </summary>
    /// <param name="values">The values of the fields</param>
    /// <param name="fileProblem">The problem of the settings file, <c>null</c> if none</param>
    /// <param name="problems">The problems of the fields</param>
    /// <returns>The view</returns>
    private SettingsView CreateView(CompanionSettingsValues values, string? fileProblem, IReadOnlyList<SettingsProblem> problems)
    {
        var texts = _context.Texts();
        var profiles = _context.Profiles;

        return new SettingsView
               {
                   Texts = texts,
                   Values = values,
                   FileProblem = fileProblem,
                   Problems = problems,
                   ProfileList = profiles.Profiles,
                   ActiveProfile = profiles.Active.Id,
                   IsProfilesFileInvalid = profiles.IsFileInvalid,
                   WaitingDeletions = profiles.WaitingDeletions.Count,
                   IsDeletionQueueFull = profiles.IsDeletionQueueFull,
                   IsAutostartEnabled = _context.Autostart.IsEnabled,
                   Warnings = CreateWarnings(texts, _context.RunState())
               };
    }

    /// <summary>
    /// Forgets a window that was closed.
    /// </summary>
    /// <param name="sender">The window</param>
    /// <param name="eventArgs">The event data</param>
    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is not ISettingsWindow window)
        {
            return;
        }

        window.WindowClosed -= OnWindowClosed;
        window.SaveRequested -= OnSaveRequested;
        window.DeleteRequested -= OnDeleteRequested;
        _context.Profiles.DeletionsChanged -= OnDeletionsChanged;

        if (ReferenceEquals(_window, window))
        {
            _window = null;
            _document = null;
            _lastView = null;
        }
    }

    /// <summary>
    /// Shows the profiles again on the window's thread when the deleted identities changed.
    /// </summary>
    /// <param name="sender">The profiles</param>
    /// <param name="eventArgs">The event data</param>
    private void OnDeletionsChanged(object? sender, EventArgs eventArgs)
    {
        _post(ShowProfilesAgain);
    }

    /// <summary>
    /// Shows the profiles, the count of waiting deletions and the button state again, while the window is open.
    /// </summary>
    private void ShowProfilesAgain()
    {
        if (_window is { } window && _lastView is { } view)
        {
            var profiles = CreateView(view.Values, view.FileProblem, view.Problems);

            _lastView = profiles;

            window.ShowProfiles(profiles);
        }
    }

    /// <summary>
    /// Deletes a profile the window asked for and shows the profiles again; ignored while a save runs or when the profile cannot be deleted.
    /// </summary>
    /// <param name="sender">The window</param>
    /// <param name="deletion">The deletion</param>
    private void OnDeleteRequested(object? sender, SettingsDeletion deletion)
    {
        if (sender is not ISettingsWindow window || ReferenceEquals(_window, window) == false || _document is null || _isSaving)
        {
            return;
        }

        var profiles = _context.Profiles;

        if (CompanionProfiles.IsDeletable(deletion.Profile, profiles.Active.Id, profiles.Profiles) == false || (deletion.IsHistoryDeleted && profiles.IsDeletionQueueFull))
        {
            return;
        }

        profiles.Delete(deletion.Profile, deletion.IsHistoryDeleted);
        ShowProfilesAgain();
    }

    /// <summary>
    /// Starts a save, unless one is running.
    /// </summary>
    /// <param name="sender">The window</param>
    /// <param name="input">What the user entered</param>
    private void OnSaveRequested(object? sender, SettingsInput input)
    {
        if (sender is not ISettingsWindow window || ReferenceEquals(_window, window) == false || _document is not { } document || _isSaving)
        {
            return;
        }

        _isSaving = true;

        var blocking = _blocking;
        var language = input.EditedFields.Contains(SettingsField.Language) ? input.Values.Language : _fileLanguage;
        var values = WithLanguage(input.Values, language);

        _ = Task.Run(() => Save(window, document, blocking, values, input));
    }

    /// <summary>
    /// Validates the input and writes the file, off the thread of the window, then posts the result back.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="document">The settings file as read</param>
    /// <param name="blocking">The problems of the file that block saving until their field is edited</param>
    /// <param name="values">The values to save</param>
    /// <param name="input">What the user entered</param>
    private void Save(ISettingsWindow window, CompanionSettingsDocument document, IReadOnlyList<SettingsProblem> blocking, CompanionSettingsValues values, SettingsInput input)
    {
        var problems = CompanionSettingsValidator.Validate(values, _context.DirectoryExists, _context.IsInstallation).ToList();

        if (input.NewProfileName is { } name && _context.Profiles.CheckNewName(name) is { } nameProblem)
        {
            problems.Add(CreateProblem(SettingsField.ProfileName, nameProblem));
        }

        problems.AddRange(blocking.Where(problem => input.EditedFields.Contains(problem.Field) == false && problems.All(other => other.Field != problem.Field)));

        if (problems.Count > 0)
        {
            Finish(window,
                   new SettingsSaveOutcome
                   {
                       Kind = SettingsSaveKind.Refused,
                       Problems = problems.OrderBy(problem => problem.Field).ToList()
                   });

            return;
        }

        var write = document.Save(values);

        switch (write.Kind)
        {
            case CompanionSettingsWriteKind.ChangedOutside:
                {
                    Finish(window, CreateOutcome(SettingsSaveKind.ChangedOutside));
                }
                break;

            case CompanionSettingsWriteKind.Saved:
                {
                    _post(() => Apply(window, values, input));
                }
                break;

            default:
                {
                    CompanionLog.SettingsNotSaved(_context.Logger, write.Exception?.GetType().Name ?? string.Empty);
                    Finish(window, CreateOutcome(SettingsSaveKind.WriteFailed));
                }
                break;
        }
    }

    /// <summary>
    /// Posts the end of a save that changed nothing: the outcome is shown and the next save can start.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="outcome">The outcome</param>
    private void Finish(ISettingsWindow window, SettingsSaveOutcome outcome)
    {
        _post(() =>
              {
                  _isSaving = false;

                  ShowOutcome(window, outcome);
              });
    }

    /// <summary>
    /// Shows an outcome unless the window was closed meanwhile.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="outcome">The outcome</param>
    private void ShowOutcome(ISettingsWindow window, SettingsSaveOutcome outcome)
    {
        if (ReferenceEquals(_window, window))
        {
            window.ShowOutcome(outcome);
        }
    }

    /// <summary>
    /// Applies a saved file: the profile, the start with Windows, the language and the restart.
    /// </summary>
    /// <param name="window">The window</param>
    /// <param name="values">The saved values</param>
    /// <param name="input">What the user entered</param>
    private void Apply(ISettingsWindow window, CompanionSettingsValues values, SettingsInput input)
    {
        CompanionLog.SettingsSaved(_context.Logger);

        _blocking = [];
        _fileLanguage = values.Language;

        ApplyProfile(input);

        var isAutostartFailed = ApplyAutostart(input.IsAutostartEnabled);

        if (values.Language is { } language && language != _context.Texts().Language)
        {
            _context.ApplyLanguage(language);
        }

        _isSaving = false;

        if (CompanionSettingsDocument.NeedsRestart(_context.StartupValues, values))
        {
            var isStarted = _context.Restart.TryStart();
            var outcome = new SettingsSaveOutcome
                          {
                              Kind = isStarted ? SettingsSaveKind.Restarting : SettingsSaveKind.RestartFailed,
                              IsAutostartFailed = isAutostartFailed
                          };

            ShowOutcome(window, outcome);

            if (isStarted)
            {
                _context.Exit();
            }

            return;
        }

        if (ReferenceEquals(_window, window))
        {
            Show(window, CreateView(values, null, []));
            window.ShowOutcome(new SettingsSaveOutcome
                               {
                                   Kind = SettingsSaveKind.Saved,
                                   IsAutostartFailed = isAutostartFailed
                               });
        }
    }

    /// <summary>
    /// Creates the profile of the input or selects its profile.
    /// </summary>
    /// <param name="input">What the user entered</param>
    private void ApplyProfile(SettingsInput input)
    {
        var profiles = _context.Profiles;
        var before = profiles.Active.Id;

        if (input.NewProfileName is { } name)
        {
            if (profiles.CheckNewName(name) is null)
            {
                _ = profiles.Create(name);
            }
        }
        else if (input.SelectedProfile != before && profiles.NameOf(input.SelectedProfile) is not null)
        {
            profiles.Select(input.SelectedProfile);
        }

        if (profiles.Active.Id != before)
        {
            CompanionLog.ProfileSwitched(_context.Logger);
        }
    }

    /// <summary>
    /// Turns the start with Windows on or off when the box differs from the entry.
    /// </summary>
    /// <param name="isTicked">Whether the box is ticked</param>
    /// <returns><c>true</c> if the entry could not be changed</returns>
    private bool ApplyAutostart(bool isTicked)
    {
        var autostart = _context.Autostart;

        if (autostart.Refresh() == isTicked)
        {
            return false;
        }

        autostart.Toggle();

        return autostart.Refresh() != isTicked;
    }

    #endregion // Methods
}