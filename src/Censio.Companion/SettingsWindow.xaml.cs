using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Censio.Companion.Core;

using Microsoft.Win32;

namespace Censio.Companion;

/// <summary>
/// The window of the Companion's settings.
/// </summary>
internal sealed partial class SettingsWindow : Window, ISettingsWindow
{
    #region Fields

    /// <summary>
    /// The fields the user edited since the last view was shown
    /// </summary>
    private readonly HashSet<SettingsField> _edited = [];

    /// <summary>
    /// The view shown last, <c>null</c> until the first one is shown
    /// </summary>
    private SettingsView? _view;

    /// <summary>
    /// The problems of the last refused save
    /// </summary>
    private IReadOnlyList<SettingsProblem> _outcomeProblems = [];

    /// <summary>
    /// Whether the window fills its controls, which are no edits of the user
    /// </summary>
    private bool _isPopulating;

    /// <summary>
    /// Whether the window was shown
    /// </summary>
    private bool _shown;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow" /> class.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();

        MaxHeight = SystemParameters.WorkArea.Height;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Gets the mode of the radio buttons.
    /// </summary>
    /// <returns>The mode; <see cref="CompanionSettingsMode.Off" /> when none is checked</returns>
    private CompanionSettingsMode GetMode()
    {
        if (ForwardRadio.IsChecked == true)
        {
            return CompanionSettingsMode.Forward;
        }

        return LocalRadio.IsChecked == true ? CompanionSettingsMode.Local : CompanionSettingsMode.Off;
    }

    /// <summary>
    /// Gets the language of the language box.
    /// </summary>
    /// <returns>The language</returns>
    private CompanionLanguage GetLanguage()
    {
        return LanguageBox.SelectedIndex == 1 ? CompanionLanguage.German : CompanionLanguage.English;
    }

    /// <summary>
    /// Records an edit of a field, unless the window fills its controls.
    /// </summary>
    /// <param name="field">The field</param>
    private void MarkEdited(SettingsField field)
    {
        if (_isPopulating == false)
        {
            _ = _edited.Add(field);
        }
    }

    /// <summary>
    /// Fills the labels with the texts.
    /// </summary>
    /// <param name="texts">The texts</param>
    private void ShowTexts(CompanionTexts texts)
    {
        Title = texts.SettingsWindowTitle;
        ModeLabel.Text = texts.ModeLabel;
        OffRadio.Content = texts.ModeOffText;
        LocalRadio.Content = texts.ModeLocalText;
        ForwardRadio.Content = texts.ModeForwardText;
        PortLabel.Text = texts.LocalPortLabel;
        AddressLabel.Text = texts.ServerAddressLabel;
        TokenLabel.Text = texts.ServerTokenLabel;
        HoursLabel.Text = texts.BufferHoursLabel;
        MegabytesLabel.Text = texts.BufferMegabytesLabel;
        FolderLabel.Text = texts.GameFolderLabel;
        BrowseButton.Content = texts.BrowseText;
        FolderHint.Text = texts.GameFolderHint;
        ProfileLabel.Text = texts.ProfileLabel;
        NewProfileButton.Content = texts.NewProfileText;
        DeleteProfileButton.Content = texts.DeleteProfileText;
        NewProfileLabel.Text = texts.NewProfileNameLabel;
        AutostartBox.Content = texts.StartWithWindowsText;
        LanguageLabel.Text = texts.LanguageLabel;
        SaveButton.Content = texts.SaveText;
        CancelButton.Content = texts.CancelText;
    }

    /// <summary>
    /// Fills the list of profiles with the active one selected, the count of waiting deletions and the state of the delete button.
    /// </summary>
    /// <param name="view">The view</param>
    private void ShowProfileControls(SettingsView view)
    {
        var wasPopulating = _isPopulating;

        _isPopulating = true;

        try
        {
            var chosen = ProfileBox.SelectedValue;

            ProfileBox.ItemsSource = view.ProfileList;
            ProfileBox.SelectedValue = chosen is not null && view.ProfileList.Any(profile => Equals(profile.Id, chosen)) ? chosen : view.ActiveProfile;
        }
        finally
        {
            _isPopulating = wasPopulating;
        }

        var culture = CultureInfo.GetCultureInfo(view.Texts.Language == CompanionLanguage.German ? "de-DE" : "en-US");

        DeleteProfileButton.Content = view.Texts.DeleteProfileText;
        WaitingDeletionsText.Text = string.Format(CultureInfo.InvariantCulture, view.Texts.WaitingDeletionsFormat, view.WaitingDeletions.ToString("N0", culture));
        WaitingDeletionsText.Visibility = view.WaitingDeletions > 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateDeleteButton();
    }

    /// <summary>
    /// Enables the delete button for a profile that can be deleted and gives the reason as its tooltip otherwise.
    /// </summary>
    private void UpdateDeleteButton()
    {
        if (_view is not { } view)
        {
            return;
        }

        var selected = ProfileBox.SelectedValue is Guid id ? id : view.ActiveProfile;
        var canDelete = view.CanDelete(selected);

        DeleteProfileButton.IsEnabled = canDelete;
        DeleteProfileButton.ToolTip = canDelete ? null : view.Texts.DeleteProfileDisabledText;
    }

    /// <summary>
    /// Fills the banner with the problems of the files and the warnings.
    /// </summary>
    /// <param name="view">The view</param>
    private void ShowBanner(SettingsView view)
    {
        BannerPanel.Children.Clear();

        var lines = new List<string>();

        if (view.FileProblem is { } fileProblem)
        {
            lines.Add(view.Texts.FormatFileProblem(fileProblem));
        }

        if (view.IsProfilesFileInvalid)
        {
            lines.Add(view.Texts.ProfilesFileInvalidText);
        }

        lines.AddRange(view.Warnings);

        foreach (var line in lines)
        {
            BannerPanel.Children.Add(new TextBlock
                                     {
                                         Text = line,
                                         Style = (Style)FindResource("BannerStyle")
                                     });
        }

        BannerPanel.Margin = new Thickness(0, 0, 0, lines.Count > 0 ? 6 : 0);
    }

    /// <summary>
    /// Fills the controls with the values of a view.
    /// </summary>
    /// <param name="view">The view</param>
    private void ShowValues(SettingsView view)
    {
        var values = view.Values;
        var hasModeProblem = view.Problems.Any(problem => problem.Field == SettingsField.Mode);

        OffRadio.IsChecked = hasModeProblem == false && values.Mode == CompanionSettingsMode.Off;
        LocalRadio.IsChecked = hasModeProblem == false && values.Mode == CompanionSettingsMode.Local;
        ForwardRadio.IsChecked = hasModeProblem == false && values.Mode == CompanionSettingsMode.Forward;
        PortBox.Text = values.LocalPort;
        AddressBox.Text = values.ServerAddress;
        TokenBox.Password = values.ServerToken;
        HoursBox.Text = values.BufferHours;
        MegabytesBox.Text = values.BufferMegabytes;
        FolderBox.Text = values.GameFolder;
        ShowProfileControls(view);
        NewProfileBox.Text = string.Empty;
        NewProfileMessage.Text = string.Empty;
        NewProfilePanel.Visibility = Visibility.Collapsed;
        AutostartBox.IsChecked = view.IsAutostartEnabled;
        LanguageBox.SelectedIndex = (values.Language ?? view.Texts.Language) == CompanionLanguage.German ? 1 : 0;
    }

    /// <summary>
    /// Shows the problems below their fields.
    /// </summary>
    /// <param name="problems">The problems</param>
    /// <param name="texts">The texts</param>
    private void ShowProblems(IReadOnlyList<SettingsProblem> problems, CompanionTexts texts)
    {
        _outcomeProblems = problems;

        foreach (var (field, message) in GetMessages())
        {
            var problem = problems.Where(candidate => candidate.Field == field).Select(candidate => (SettingsProblem?)candidate).FirstOrDefault();

            message.Text = problem is { } found ? texts.FormatProblem(found.Kind) : string.Empty;
            message.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        }

        UpdateEnabled();
    }

    /// <summary>
    /// Gets the message below each field that has one.
    /// </summary>
    /// <returns>The fields with their messages</returns>
    private IEnumerable<(SettingsField Field, TextBlock Message)> GetMessages()
    {
        return
        [
            (SettingsField.Mode, ModeMessage),
            (SettingsField.LocalPort, PortMessage),
            (SettingsField.ServerAddress, AddressMessage),
            (SettingsField.ServerToken, TokenMessage),
            (SettingsField.BufferHours, HoursMessage),
            (SettingsField.BufferMegabytes, MegabytesMessage),
            (SettingsField.GameFolder, FolderMessage),
            (SettingsField.ProfileName, NewProfileMessage)
        ];
    }

    /// <summary>
    /// Enables the fields the mode uses and those a problem names.
    /// </summary>
    private void UpdateEnabled()
    {
        if (_view is not { } view)
        {
            return;
        }

        var mode = GetMode();

        bool IsEnabled(SettingsField field)
        {
            return view.IsFieldEnabled(field, mode) || _outcomeProblems.Any(problem => problem.Field == field);
        }

        PortBox.IsEnabled = IsEnabled(SettingsField.LocalPort);
        AddressBox.IsEnabled = IsEnabled(SettingsField.ServerAddress);
        TokenBox.IsEnabled = IsEnabled(SettingsField.ServerToken);
        HoursBox.IsEnabled = IsEnabled(SettingsField.BufferHours);
        MegabytesBox.IsEnabled = IsEnabled(SettingsField.BufferMegabytes);
    }

    /// <summary>
    /// Creates what the user entered.
    /// </summary>
    /// <returns>The input</returns>
    private SettingsInput CreateInput()
    {
        var values = new CompanionSettingsValues
                     {
                         Mode = GetMode(),
                         LocalPort = PortBox.Text,
                         ServerAddress = AddressBox.Text,
                         ServerToken = TokenBox.Password,
                         BufferHours = HoursBox.Text,
                         BufferMegabytes = MegabytesBox.Text,
                         GameFolder = FolderBox.Text,
                         Language = GetLanguage()
                     };
        var selected = ProfileBox.SelectedValue is Guid id ? id : _view?.ActiveProfile ?? CompanionProfile.Live.Id;
        var isNewProfile = NewProfilePanel.Visibility == Visibility.Visible;

        return new SettingsInput
               {
                   Values = values,
                   SelectedProfile = selected,
                   NewProfileName = isNewProfile ? NewProfileBox.Text : null,
                   IsAutostartEnabled = AutostartBox.IsChecked == true,
                   EditedFields = [.. _edited]
               };
    }

    /// <summary>
    /// Records the mode as edited and updates which fields are enabled.
    /// </summary>
    /// <param name="sender">The radio button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnModeChecked(object sender, RoutedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.Mode);
        UpdateEnabled();
    }

    /// <summary>
    /// Records the port as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnPortChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.LocalPort);
    }

    /// <summary>
    /// Records the address as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnAddressChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.ServerAddress);
    }

    /// <summary>
    /// Records the token as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnTokenChanged(object sender, RoutedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.ServerToken);
    }

    /// <summary>
    /// Records the buffer hours as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnHoursChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.BufferHours);
    }

    /// <summary>
    /// Records the buffer megabytes as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnMegabytesChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.BufferMegabytes);
    }

    /// <summary>
    /// Records the game folder as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnFolderChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.GameFolder);
    }

    /// <summary>
    /// Records the name of a new profile as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnNewProfileChanged(object sender, TextChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.ProfileName);
    }

    /// <summary>
    /// Records the language as edited.
    /// </summary>
    /// <param name="sender">The box</param>
    /// <param name="eventArgs">The event data</param>
    private void OnLanguageChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        MarkEdited(SettingsField.Language);
    }

    /// <summary>
    /// Lets the user pick the game folder and puts its full path into the field.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnBrowseClick(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFolderDialog
                     {
                         Title = _view?.Texts.GameFolderLabel ?? string.Empty
                     };

        if (System.IO.Path.IsPathFullyQualified(FolderBox.Text))
        {
            dialog.InitialDirectory = FolderBox.Text;
        }

        if (dialog.ShowDialog(this) == true && System.IO.Path.IsPathFullyQualified(dialog.FolderName))
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    /// <summary>
    /// Shows or hides the box for the name of a new profile.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnNewProfileClick(object sender, RoutedEventArgs eventArgs)
    {
        var isShown = NewProfilePanel.Visibility == Visibility.Visible;

        NewProfilePanel.Visibility = isShown ? Visibility.Collapsed : Visibility.Visible;

        if (isShown)
        {
            NewProfileBox.Text = string.Empty;
            NewProfileMessage.Visibility = Visibility.Collapsed;
            _ = _edited.Remove(SettingsField.ProfileName);
        }
        else
        {
            _ = NewProfileBox.Focus();
        }
    }

    /// <summary>
    /// Updates the delete button when another profile is selected.
    /// </summary>
    /// <param name="sender">The drop-down</param>
    /// <param name="eventArgs">The event data</param>
    private void OnProfileChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        UpdateDeleteButton();
    }

    /// <summary>
    /// Asks whether the selected profile is deleted and reports the answer to the session.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnDeleteProfileClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_view is not { } view || ProfileBox.SelectedItem is not CompanionProfile profile || view.CanDelete(profile.Id) == false)
        {
            return;
        }

        var dialog = new ProfileDeletionDialog(view.Texts, profile.Name, view.IsDeletionQueueFull)
                     {
                         Owner = this
                     };

        if (dialog.ShowDialog() == true)
        {
            DeleteRequested?.Invoke(this,
                                    new SettingsDeletion
                                    {
                                        Profile = profile.Id,
                                        IsHistoryDeleted = dialog.IsHistoryDeleted
                                    });
        }
    }

    /// <summary>
    /// Asks the session to save what the user entered.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnSaveClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_view is null)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        OutcomeText.Text = string.Empty;

        SaveRequested?.Invoke(this, CreateInput());
    }

    /// <summary>
    /// Closes the window without saving.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnCancelClick(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    #endregion // Methods

    #region Window

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        TokenBox.Clear();

        base.OnClosed(e);

        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Window

    #region ISettingsWindow

    /// <inheritdoc />
    public event EventHandler? WindowClosed;

    /// <inheritdoc />
    public event EventHandler<SettingsInput>? SaveRequested;

    /// <inheritdoc />
    public event EventHandler<SettingsDeletion>? DeleteRequested;

    /// <inheritdoc />
    public void ShowProfiles(SettingsView view)
    {
        _view = view;

        ShowProfileControls(view);
    }

    /// <inheritdoc />
    public void ShowView(SettingsView view)
    {
        _isPopulating = true;

        try
        {
            _view = view;

            ShowTexts(view.Texts);
            ShowBanner(view);
            ShowValues(view);
            ShowProblems(view.Problems, view.Texts);
        }
        finally
        {
            _isPopulating = false;
        }

        _edited.Clear();
        SaveButton.IsEnabled = true;

        if (_shown == false)
        {
            _shown = true;
            Show();
        }
    }

    /// <inheritdoc />
    public void ShowOutcome(SettingsSaveOutcome outcome)
    {
        if (_view is not { } view)
        {
            return;
        }

        var text = view.Texts.FormatOutcome(outcome);

        if (outcome.Kind == SettingsSaveKind.Saved && text.Length == 0)
        {
            Close();

            return;
        }

        var isFailure = outcome.Kind is SettingsSaveKind.WriteFailed or SettingsSaveKind.ChangedOutside or SettingsSaveKind.RestartFailed || outcome.IsAutostartFailed;

        OutcomeText.Text = text;
        OutcomeText.Foreground = isFailure ? Brushes.Firebrick : SystemColors.ControlTextBrush;

        if (outcome.Kind == SettingsSaveKind.Refused)
        {
            ShowProblems(outcome.Problems, view.Texts);
        }

        SaveButton.IsEnabled = outcome.Kind != SettingsSaveKind.Restarting;
    }

    /// <inheritdoc />
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        _ = Activate();
    }

    #endregion // ISettingsWindow
}