using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The dialog that asks into which profile another profile is merged.
/// </summary>
internal sealed partial class ProfileMergeDialog : Window
{
    #region Fields

    /// <summary>
    /// Whether the queue of changes is full, so no merge can be queued
    /// </summary>
    private readonly bool _isQueueFull;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileMergeDialog" /> class.
    /// </summary>
    /// <param name="texts">The texts</param>
    /// <param name="profileName">The name of the profile that is merged</param>
    /// <param name="targets">The profiles it can be merged into</param>
    /// <param name="isQueueFull">Whether the queue of changes is full, so the merge cannot be queued</param>
    public ProfileMergeDialog(CompanionTexts texts, string profileName, IReadOnlyList<CompanionProfile> targets, bool isQueueFull)
    {
        InitializeComponent();

        _isQueueFull = isQueueFull;

        Title = texts.MergeProfileTitle;
        NameText.Text = string.Format(CultureInfo.InvariantCulture, texts.MergeProfileTargetFormat, profileName);
        TargetBox.ItemsSource = targets;
        MessageText.Text = string.Format(CultureInfo.InvariantCulture, texts.MergeProfileTextFormat, profileName);
        QueueFullText.Text = texts.ChangesQueueFullText;
        QueueFullText.Visibility = isQueueFull ? Visibility.Visible : Visibility.Collapsed;
        MergeButton.Content = texts.MergeProfileConfirmText;
        CancelButton.Content = texts.CancelText;
        MergeButton.Click += OnMergeClick;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the profile chosen as the target, <c>null</c> if none.
    /// </summary>
    public CompanionProfile? Target => TargetBox.SelectedItem as CompanionProfile;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Enables the merge button once a target is chosen and the queue has room.
    /// </summary>
    /// <param name="sender">The drop-down</param>
    /// <param name="eventArgs">The event data</param>
    private void OnTargetChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        MergeButton.IsEnabled = Target is not null && _isQueueFull == false;
    }

    /// <summary>
    /// Closes the dialog with the answer to merge.
    /// </summary>
    /// <param name="sender">The button</param>
    /// <param name="eventArgs">The event data</param>
    private void OnMergeClick(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = true;
    }

    #endregion // Methods
}