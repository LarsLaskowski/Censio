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
    {
        Index = index;
        Cells = cells;
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

    #endregion // Properties
}