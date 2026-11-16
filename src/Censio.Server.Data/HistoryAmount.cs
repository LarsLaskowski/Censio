namespace Censio.Server.Data;

/// <summary>
/// One workforce or building amount of a history point.
/// </summary>
public sealed class HistoryAmount
{
    #region Properties

    /// <summary>
    /// Gets the GUID of the item.
    /// </summary>
    public required int ItemGuid { get; init; }

    /// <summary>
    /// Gets the amount.
    /// </summary>
    public required double Amount { get; init; }

    #endregion // Properties
}