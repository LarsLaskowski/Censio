namespace Censio.Server.Data;

/// <summary>
/// The tombstone of a deleted profile, in the <c>DeletedProfiles</c> table.
/// </summary>
internal sealed class DeletedProfileRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the tombstone identity.
    /// </summary>
    public Guid TombstoneId { get; set; }

    #endregion // Properties
}