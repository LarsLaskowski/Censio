namespace Censio.Companion.Core;

/// <summary>
/// The request of the user to rename a profile.
/// </summary>
public sealed class SettingsRename
{
    #region Properties

    /// <summary>
    /// Gets the identity of the profile to rename.
    /// </summary>
    public required Guid Profile { get; init; }

    /// <summary>
    /// Gets the new name of the profile.
    /// </summary>
    public required string Name { get; init; }

    #endregion // Properties
}