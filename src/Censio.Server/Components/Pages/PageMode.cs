namespace Censio.Server.Components.Pages;

/// <summary>
/// What a data page shows.
/// </summary>
public enum PageMode
{
    #region Values

    /// <summary>
    /// The page waits for its first data. As the answer of an address, it means that the address names a profile to read.
    /// </summary>
    Loading,

    /// <summary>
    /// The page shows its figures.
    /// </summary>
    Ready,

    /// <summary>
    /// The page has no data to show.
    /// </summary>
    NoData,

    /// <summary>
    /// The address names nothing known.
    /// </summary>
    NotFound,

    /// <summary>
    /// The read failed.
    /// </summary>
    Failed

    #endregion // Values
}