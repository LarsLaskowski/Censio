namespace Censio.Server.Data;

/// <summary>
/// A stored profile.
/// </summary>
public sealed class StoredProfile
{
    #region Properties

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the profile name, or <see langword="null"/> when none is known.
    /// </summary>
    public required string? Name { get; init; }

    #endregion // Properties
}