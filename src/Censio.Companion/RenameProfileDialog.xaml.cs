using System.Globalization;
using System.Windows;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The dialog that asks for the new name of a profile.
/// </summary>
internal sealed partial class RenameProfileDialog : Window
{
    #region Fields

    /// <summary>
    /// The view that checks the name.
    /// </summary>
    private readonly SettingsView _view;

    /// <summary>
    /// The profile to rename.
    /// </summary>
    private readonly CompanionProfile _profile;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameProfileDialog" /> class.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="view">The view that checks the name</param>
    /// <param name="profile">The profile to rename</param>
    public RenameProfileDialog(CompanionTexts texts, SettingsView view, CompanionProfile profile)
    {
        InitializeComponent();

        _view = view;
        _profile = profile;

        Title = texts.RenameProfileTitle;
        MessageText.Text = string.Format(CultureInfo.InvariantCulture, texts.RenameProfileTextFormat, profile.Name);
        NameBox.Text = profile.Name;
        RenameButton.Content = texts.RenameProfileConfirmText;
        CancelButton.Content = texts.CancelText;
        RenameButton.Click += OnRenameClick;
        Loaded += OnLoaded;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the name the user entered.
    /// </summary>
    public string NewName => NameBox.Text;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Focuses the name and selects all of it.
    /// </summary>
    /// <param name="sender">The dialog</param>
    /// <param name="eventArgs">The event data</param>
    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        _ = NameBox.Focus();
        NameBox.SelectAll();
    }

    /// <summary>
    /// Closes the dialog with the answer to rename, or shows the problem of the name and stays open.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnRenameClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_view.CheckRename(_profile.Id, NameBox.Text) is { } problem)
        {
            ProblemText.Text = _view.Texts.FormatProblem(problem);
            ProblemText.Visibility = Visibility.Visible;

            return;
        }

        DialogResult = true;
    }

    #endregion // Methods
}