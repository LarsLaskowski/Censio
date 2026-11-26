using System.Windows;

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
    public ProfileDeletionDialog()
    {
        InitializeComponent();
    }

    #endregion // Constructors
}