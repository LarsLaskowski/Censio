namespace Censio.Companion.Core;

/// <summary>
/// A notice about the active profile.
/// </summary>
public sealed class ProfileNotice
{
    #region Properties

    /// <summary>
    /// Gets the kind of the notice.
    /// </summary>
    public required ProfileNoticeKind Kind { get; init; }

    /// <summary>
    /// Gets the name of the profile the notice is about.
    /// </summary>
    public required string ProfileName { get; init; }

    #endregion // Properties
}