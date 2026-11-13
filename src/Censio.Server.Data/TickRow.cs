namespace Censio.Server.Data;

/// <summary>
/// One tick of the <c>Ticks</c> table.
/// </summary>
internal sealed class TickRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Gets or sets the tick identity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the profile the tick belongs to.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the receive time as UTC ticks.
    /// </summary>
    public long ReceiveTicks { get; set; }

    /// <summary>
    /// Gets or sets the game time.
    /// </summary>
    public long GameTime { get; set; }

    #endregion // Properties
}