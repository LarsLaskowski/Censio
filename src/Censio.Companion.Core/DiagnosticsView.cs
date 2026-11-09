using System.Collections;
using System.Globalization;

using Censio.GameData;
using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// The content of the diagnostics window: a header and a flat table of rows.
/// </summary>
public sealed class DiagnosticsView : IReadOnlyList<DiagnosticsRow>, IList
{
    #region Constants

    /// <summary>
    /// The title of the window.
    /// </summary>
    public const string Title = "Censio Companion: Diagnostics";

    /// <summary>
    /// The text shown when the table has no rows.
    /// </summary>
    public const string NoEntriesText = "(no entries)";

    /// <summary>
    /// The longest catalog name shown in a cell.
    /// </summary>
    public const int MaxCatalogNameLength = 4096;

    /// <summary>
    /// The message of the exception thrown by every mutating <see cref="IList" /> member.
    /// </summary>
    private const string ReadOnlyText = "The view is read-only";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The column headers.
    /// </summary>
    private static readonly string[] _columns = [
                                                    "Region",
                                                    "Island",
                                                    "Area",
                                                    "Session ID",
                                                    "Name",
                                                    "Good",
                                                    "Generation",
                                                    "Consumption",
                                                    "Delta",
                                                    "Perfect generation",
                                                    "Perfect consumption",
                                                    "Buildings",
                                                    "Maintenance",
                                                    "Income",
                                                    "Profit",
                                                    "Productivity (sum)",
                                                    "Productivity (avg)"
                                                ];

    /// <summary>
    /// The lines of the header.
    /// </summary>
    private readonly IReadOnlyList<string> _headerLines;

    /// <summary>
    /// The tick the view shows.
    /// </summary>
    private readonly PipeTick? _tick;

    /// <summary>
    /// The index of the first row of each island; an island without entries has one row.
    /// </summary>
    private readonly int[] _firstRows;

    /// <summary>
    /// The number of rows.
    /// </summary>
    private readonly int _count;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsView" /> class.
    /// </summary>
    /// <param name="headerLines">The lines of the header</param>
    /// <param name="tick">The tick, or <c>null</c> if none</param>
    private DiagnosticsView(IReadOnlyList<string> headerLines, PipeTick? tick)
    {
        _headerLines = headerLines;
        _tick = tick;

        var islands = tick?.Islands ?? [];
        var firstRows = new int[islands.Count];
        var count = 0;

        for (var islandIndex = 0; islandIndex < firstRows.Length; islandIndex++)
        {
            firstRows[islandIndex] = count;
            count += Math.Max(1, islands[islandIndex].Entries.Count);
        }

        _firstRows = firstRows;
        _count = count;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the column headers.
    /// </summary>
    public static IReadOnlyList<string> Columns => _columns;

    /// <summary>
    /// Gets the lines of the header.
    /// </summary>
    public IReadOnlyList<string> HeaderLines => _headerLines;

    /// <summary>
    /// Gets the tick the view shows, or <c>null</c> if none.
    /// </summary>
    public PipeTick? Tick => _tick;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a view.
    /// </summary>
    /// <param name="statusText">The status text</param>
    /// <param name="tick">The tick, or <c>null</c> if none</param>
    /// <param name="clock">The clock</param>
    /// <returns>The view</returns>
    public static DiagnosticsView Create(string statusText, PipeTick? tick, TimeProvider clock)
    {
        return Create(statusText, tick, null, clock);
    }

    /// <summary>
    /// Creates a view that names the GUIDs from a catalog.
    /// </summary>
    /// <param name="statusText">The status text</param>
    /// <param name="tick">The tick, or <c>null</c> if none</param>
    /// <param name="catalog">The catalog, or <c>null</c> if none</param>
    /// <param name="clock">The clock</param>
    /// <returns>The view</returns>
#pragma warning disable S1172 // Skeleton: removed with the implementation
    public static DiagnosticsView Create(string statusText, PipeTick? tick, GameCatalog? catalog, TimeProvider clock)
    {
        var lines = new List<string>(3)
                    {
                        $"Status: {statusText}",
                        TrayMenu.FormatLastTick(tick is null ? null : PipeTickSummary.From(tick), clock)
                    };

        if (tick is not null)
        {
            var gameTime = string.Create(CultureInfo.InvariantCulture, $"Game time: {tick.GameTime}");

            lines.Add(tick.IsNoDataYet ? $"{gameTime} (no data yet)" : gameTime);
        }

        return new DiagnosticsView(lines, tick);
    }
#pragma warning restore S1172

    /// <summary>
    /// Formats an integer culture-independently.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Format(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a float culture-independently.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private static string Format(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Creates the row at an index.
    /// </summary>
    /// <param name="index">The index</param>
    /// <returns>The row</returns>
    private DiagnosticsRow CreateRow(int index)
    {
        var islandIndex = Array.BinarySearch(_firstRows, index);

        if (islandIndex < 0)
        {
            islandIndex = ~islandIndex - 1;
        }

        var island = _tick!.Islands[islandIndex];
        var entryIndex = index - _firstRows[islandIndex];
        var key = island.Key;
        var cells = new string[_columns.Length];

        cells[0] = Format(key.SessionGuid);
        cells[1] = Format(key.IslandId);
        cells[2] = Format(key.AreaIndex);
        cells[3] = Format(island.SessionId);
        cells[4] = LogLine.Sanitize(island.Name, int.MaxValue);

        if (island.Entries.Count == 0)
        {
            cells[5] = NoEntriesText;

            for (var column = 6; column < cells.Length; column++)
            {
                cells[column] = string.Empty;
            }

            return new DiagnosticsRow(index, cells);
        }

        var entry = island.Entries[entryIndex];

        cells[5] = Format(entry.ProductGuid);
        cells[6] = Format(entry.Generation);
        cells[7] = Format(entry.Consumption);
        cells[8] = Format(entry.Delta);
        cells[9] = Format(entry.PerfectGeneration);
        cells[10] = Format(entry.PerfectConsumption);
        cells[11] = Format(entry.BuildingCount);
        cells[12] = Format(entry.Maintenance);
        cells[13] = Format(entry.Income);
        cells[14] = Format(entry.Profit);
        cells[15] = Format(entry.SummedProductivity);
        cells[16] = Format(entry.AverageProductivity);

        return new DiagnosticsRow(index, cells);
    }

    /// <summary>
    /// Finds the index of a row without scanning.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The index of the row, or -1 if it is not a row of this view</returns>
    private int FindIndex(object? value)
    {
        if (value is not DiagnosticsRow row || row.Index < 0 || row.Index >= _count)
        {
            return -1;
        }

        return CreateRow(row.Index).Cells.SequenceEqual(row.Cells) ? row.Index : -1;
    }

    #endregion // Methods

    #region IReadOnlyCollection

    /// <inheritdoc />
    public int Count => _count;

    #endregion // IReadOnlyCollection

    #region IReadOnlyList

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the rows</exception>
    public DiagnosticsRow this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);

            return CreateRow(index);
        }
    }

    #endregion // IReadOnlyList

    #region IEnumerable

    /// <inheritdoc />
    public IEnumerator<DiagnosticsRow> GetEnumerator()
    {
        for (var rowIndex = 0; rowIndex < _count; rowIndex++)
        {
            yield return CreateRow(rowIndex);
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion // IEnumerable

    #region ICollection

    /// <inheritdoc />
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object ICollection.SyncRoot => this;

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        for (var rowIndex = 0; rowIndex < _count; rowIndex++)
        {
            array.SetValue(CreateRow(rowIndex), index + rowIndex);
        }
    }

    #endregion // ICollection

    #region IList

    /// <inheritdoc />
    bool IList.IsFixedSize => true;

    /// <inheritdoc />
    bool IList.IsReadOnly => true;

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException(ReadOnlyText);
    }

    /// <inheritdoc />
    bool IList.Contains(object? value)
    {
        return FindIndex(value) >= 0;
    }

    /// <inheritdoc />
    int IList.IndexOf(object? value)
    {
        return FindIndex(value);
    }

    /// <inheritdoc />
    int IList.Add(object? value)
    {
        throw new NotSupportedException(ReadOnlyText);
    }

    /// <inheritdoc />
    void IList.Clear()
    {
        throw new NotSupportedException(ReadOnlyText);
    }

    /// <inheritdoc />
    void IList.Insert(int index, object? value)
    {
        throw new NotSupportedException(ReadOnlyText);
    }

    /// <inheritdoc />
    void IList.Remove(object? value)
    {
        throw new NotSupportedException(ReadOnlyText);
    }

    /// <inheritdoc />
    void IList.RemoveAt(int index)
    {
        throw new NotSupportedException(ReadOnlyText);
    }

    #endregion // IList
}