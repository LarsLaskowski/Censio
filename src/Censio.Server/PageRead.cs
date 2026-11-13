namespace Censio.Server;

/// <summary>
/// The result of a read of a profile for a page.
/// </summary>
public sealed class PageRead
{
    #region Properties

    /// <summary>
    /// Gets the view, or <see langword="null"/> when the profile is unknown or the read failed.
    /// </summary>
    public required ProfileView? View { get; init; }

    /// <summary>
    /// Gets a value indicating whether the read failed.
    /// </summary>
    public required bool IsFailed { get; init; }

    #endregion // Properties
}