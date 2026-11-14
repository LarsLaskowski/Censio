namespace Censio.Server;

/// <summary>
/// A profile as a page shows it.
/// </summary>
public sealed class ProfileView
{
    #region Constants

    /// <summary>
    /// The maximum number of rows shown.
    /// </summary>
    public const int MaxRows = 500;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the profile identity.
    /// </summary>
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public required string? ProfileName { get; init; }

    /// <summary>
    /// Gets the receive time of the latest tick.
    /// </summary>
    public required DateTimeOffset? AsOf { get; init; }

    /// <summary>
    /// Gets the regions.
    /// </summary>
    public required IReadOnlyList<RegionView> Regions { get; init; }

    /// <summary>
    /// Gets the shown island.
    /// </summary>
    public required IslandKey? Island { get; init; }

    /// <summary>
    /// Gets the shown rows of the shown island.
    /// </summary>
    public required IReadOnlyList<GoodRow> Rows { get; init; }

    /// <summary>
    /// Gets the number of rows of the shown island before the limit.
    /// </summary>
    public required int RowCount { get; init; }

    #endregion // Properties
}