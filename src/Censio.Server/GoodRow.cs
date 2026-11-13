namespace Censio.Server;

/// <summary>
/// A row of the goods table of an island.
/// </summary>
public sealed class GoodRow
{
    #region Properties

    /// <summary>
    /// Gets the product GUID.
    /// </summary>
    public required int ProductGuid { get; init; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public required ShownName Name { get; init; }

    /// <summary>
    /// Gets the icon reference, or <see langword="null"/> when there is none or it is not stored.
    /// </summary>
    public required string? IconReference { get; init; }

    /// <summary>
    /// Gets the production.
    /// </summary>
    public required float? Production { get; init; }

    /// <summary>
    /// Gets the consumption.
    /// </summary>
    public required float? Consumption { get; init; }

    /// <summary>
    /// Gets the delta.
    /// </summary>
    public required float? Delta { get; init; }

    /// <summary>
    /// Gets the productivity.
    /// </summary>
    public required float? Productivity { get; init; }

    /// <summary>
    /// Gets the number of buildings.
    /// </summary>
    public required int Buildings { get; init; }

    #endregion // Properties
}