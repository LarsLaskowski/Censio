namespace Censio.Companion.Core;

/// <summary>
/// The request of the user to merge a profile into another.
/// </summary>
public sealed class SettingsMerge
{
    #region Properties

    /// <summary>
    /// Gets the identity of the profile that is merged and removed.
    /// </summary>
    public required Guid Profile { get; init; }

    /// <summary>
    /// Gets the identity of the profile that receives the figures and the history.
    /// </summary>
    public required Guid Target { get; init; }

    #endregion // Properties
}