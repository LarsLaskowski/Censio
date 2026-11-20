namespace Censio.Server.Data;

/// <summary>
/// One set of trade routes of the <c>TradeRouteSets</c> table: the latest message of a profile.
/// </summary>
internal sealed class TradeRouteSetRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the profile identity.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the identity of the stored message.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the receive time as UTC ticks.
    /// </summary>
    public long ReceiveTicks { get; set; }

    #endregion // Properties
}