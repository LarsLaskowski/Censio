namespace Censio.Server.Data;

/// <summary>
/// One folded hour of a profile, in the <c>HourlyPoints</c> table.
/// </summary>
internal sealed class HourlyPointRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Gets or sets the profile, or the tombstone of a deleted profile.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the hour start as UTC ticks.
    /// </summary>
    public long HourTicks { get; set; }

    /// <summary>
    /// Gets or sets the number of ticks folded into the hour.
    /// </summary>
    public int TickCount { get; set; }

    /// <summary>
    /// Gets or sets the smallest game time of the folded ticks.
    /// </summary>
    public long FirstGameTime { get; set; }

    /// <summary>
    /// Gets or sets the largest game time of the folded ticks.
    /// </summary>
    public long LastGameTime { get; set; }

    #endregion // Properties
}