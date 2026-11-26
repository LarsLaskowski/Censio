namespace Censio.Companion.Core;

/// <summary>
/// The request of the user to delete a profile.
/// </summary>
public sealed class SettingsDeletion
{
    #region Properties

    /// <summary>
    /// Gets the identity of the profile to delete.
    /// </summary>
    public required Guid Profile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the history of the profile on the server is deleted too.
    /// </summary>
    public required bool IsHistoryDeleted { get; init; }

    #endregion // Properties
}