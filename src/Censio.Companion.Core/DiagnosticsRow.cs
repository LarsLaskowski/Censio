using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// A row of the diagnostics window.
/// </summary>
public sealed class DiagnosticsRow
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsRow" /> class.
    /// </summary>
    /// <param name="index">The index of the row</param>
    /// <param name="cells">The texts of the cells</param>
    public DiagnosticsRow(int index, IReadOnlyList<string> cells)
        : this(index, cells, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsRow" /> class.
    /// </summary>
    /// <param name="index">The index of the row</param>
    /// <param name="cells">The texts of the cells</param>
    /// <param name="regionIcon">The icon of the region, or <c>null</c> if none</param>
    /// <param name="goodIcon">The icon of the good, or <c>null</c> if none</param>
    public DiagnosticsRow(int index, IReadOnlyList<string> cells, GameIcon? regionIcon, GameIcon? goodIcon)
    {
        Index = index;
        Cells = cells;
        RegionIcon = regionIcon;
        GoodIcon = goodIcon;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the index of the row.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the texts of the cells.
    /// </summary>
    public IReadOnlyList<string> Cells { get; }

    /// <summary>
    /// Gets the icon of the region, or <c>null</c> if none.
    /// </summary>
    public GameIcon? RegionIcon { get; }

    /// <summary>
    /// Gets the icon of the good, or <c>null</c> if none.
    /// </summary>
    public GameIcon? GoodIcon { get; }

    #endregion // Properties
}