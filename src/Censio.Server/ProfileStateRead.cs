using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The result of reading the state of a profile.
/// </summary>
public sealed class ProfileStateRead
{
    #region Properties

    /// <summary>
    /// Gets the state, or <see langword="null"/> when the profile is unknown or the read failed.
    /// </summary>
    public required StoredProfileState? State { get; init; }

    /// <summary>
    /// Gets the failure of the read, or <see langword="null"/> when it did not fail.
    /// </summary>
    public required Exception? Failure { get; init; }

    #endregion // Properties
}