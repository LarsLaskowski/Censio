using System.Collections;

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

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The lines of the header.
    /// </summary>
    private readonly IReadOnlyList<string> _headerLines;

    /// <summary>
    /// The tick the view shows.
    /// </summary>
    private readonly PipeTick? _tick;

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
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the column headers.
    /// </summary>
    public static IReadOnlyList<string> Columns => throw new NotImplementedException();

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
        throw new NotImplementedException();
    }

    #endregion // Methods

    #region IReadOnlyCollection

    /// <inheritdoc />
    public int Count => throw new NotImplementedException();

    #endregion // IReadOnlyCollection

    #region IReadOnlyList

    /// <inheritdoc />
    public DiagnosticsRow this[int index] => throw new NotImplementedException();

    #endregion // IReadOnlyList

    #region IEnumerable

    /// <inheritdoc />
    public IEnumerator<DiagnosticsRow> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }

    #endregion // IEnumerable

    #region ICollection

    /// <inheritdoc />
    bool ICollection.IsSynchronized => throw new NotImplementedException();

    /// <inheritdoc />
    object ICollection.SyncRoot => throw new NotImplementedException();

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index)
    {
        throw new NotImplementedException();
    }

    #endregion // ICollection

    #region IList

    /// <inheritdoc />
    bool IList.IsFixedSize => throw new NotImplementedException();

    /// <inheritdoc />
    bool IList.IsReadOnly => throw new NotImplementedException();

    /// <inheritdoc />
    object? IList.this[int index]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <inheritdoc />
    bool IList.Contains(object? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    int IList.IndexOf(object? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    int IList.Add(object? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    void IList.Clear()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    void IList.Insert(int index, object? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    void IList.Remove(object? value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    void IList.RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    #endregion // IList
}