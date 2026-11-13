namespace Censio.Server.Data;

/// <summary>
/// One profile of the <c>Profiles</c> table.
/// </summary>
internal sealed class ProfileRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the profile identity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the profile name, or <see langword="null"/> when none is known.
    /// </summary>
    public string? Name { get; set; }

    #endregion // Properties
}