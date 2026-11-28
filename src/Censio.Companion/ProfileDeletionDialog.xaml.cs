using System.Globalization;
using System.Windows;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The dialog that asks whether a profile is deleted and whether its history on the server is deleted too.
/// </summary>
internal sealed partial class ProfileDeletionDialog : Window
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDeletionDialog" /> class.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="profileName">The name of the profile</param>
    /// <param name="isQueueFull">Whether the queue of deletions is full, so the history cannot be deleted</param>
    public ProfileDeletionDialog(CompanionTexts texts, string profileName, bool isQueueFull)
    {
        InitializeComponent();

        Title = texts.DeleteProfileTitle;
        MessageText.Text = string.Format(CultureInfo.InvariantCulture, texts.DeleteProfileTextFormat, profileName);
        HistoryBox.Content = texts.DeleteProfileHistoryText;
        HistoryBox.IsEnabled = isQueueFull == false;
        HistoryFullText.Text = texts.ChangesQueueFullText;
        HistoryFullText.Visibility = isQueueFull ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Content = texts.DeleteProfileConfirmText;
        CancelButton.Content = texts.CancelText;
        DeleteButton.Click += OnDeleteClick;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the box for the history on the server is ticked.
    /// </summary>
    public bool IsHistoryDeleted => HistoryBox.IsChecked == true;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Closes the dialog with the answer to delete.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnDeleteClick(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }

    #endregion // Methods
}