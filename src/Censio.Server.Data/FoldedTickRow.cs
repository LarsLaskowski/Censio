namespace Censio.Server.Data;

/// <summary>
/// The identity of a tick folded into an hourly point, in the <c>FoldedTicks</c> table.
/// </summary>
internal sealed class FoldedTickRow
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
    /// Gets or sets the number of the hourly point that holds the tick.
    /// </summary>
    public long PointNumber { get; set; }

    #endregion // Properties
}