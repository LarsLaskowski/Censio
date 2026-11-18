namespace Censio.Server.Data;

/// <summary>
/// What an hourly trend read has gathered of one UTC hour so far.
/// </summary>
internal sealed class HourTotals
{
    #region Fields

    /// <summary>
    /// The smallest game time over all rows of the hour.
    /// </summary>
    private long? _allFirst;

    /// <summary>
    /// The largest game time over all rows of the hour.
    /// </summary>
    private long? _allLast;

    /// <summary>
    /// The smallest game time over the rows that hold an entry.
    /// </summary>
    private long? _entryFirst;

    /// <summary>
    /// The largest game time over the rows that hold an entry.
    /// </summary>
    private long? _entryLast;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HourTotals"/> class.
    /// </summary>
    /// <param name="hourTicks">The UTC ticks of the start of the hour</param>
    public HourTotals(long hourTicks)
    {
        HourTicks = hourTicks;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the UTC ticks of the start of the hour.
    /// </summary>
    public long HourTicks { get; }

    /// <summary>
    /// Gets or sets the receive time of the last tick read, or <see langword="null"/> when none was read.
    /// </summary>
    public long? LastReceiveTicks { get; set; }

    /// <summary>
    /// Gets or sets the row number of the last tick read.
    /// </summary>
    public long LastNumber { get; set; }

    /// <summary>
    /// Gets a value indicating whether a row of the hour holds an entry.
    /// </summary>
    public bool HasEntries { get; private set; }

    /// <summary>
    /// Gets the smallest game time of the rows that hold an entry, of all rows when none does.
    /// </summary>
    public long? FirstGameTime => HasEntries
                                      ? _entryFirst
                                      : _allFirst;

    /// <summary>
    /// Gets the largest game time of the rows that hold an entry, of all rows when none does.
    /// </summary>
    public long? LastGameTime => HasEntries
                                     ? _entryLast
                                     : _allLast;

    /// <summary>
    /// Gets the sums of the islands by island key, in island order.
    /// </summary>
    public SortedDictionary<long, HourIslandSums> Islands { get; } = [];

    /// <summary>
    /// Gets the sums of the goods by product GUID, in GUID order.
    /// </summary>
    public SortedDictionary<int, HourGoodSums> Goods { get; } = [];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds the game times of a tick or an hourly point.
    /// </summary>
    /// <param name="first">The smallest game time</param>
    /// <param name="last">The largest game time</param>
    /// <param name="hasEntries">Whether an island of the row holds an entry</param>
    public void AddGameTimes(long first, long last, bool hasEntries)
    {
        _allFirst = _allFirst is { } allFirst ? Math.Min(allFirst, first) : first;
        _allLast = _allLast is { } allLast ? Math.Max(allLast, last) : last;

        if (hasEntries == false)
        {
            return;
        }

        HasEntries = true;
        _entryFirst = _entryFirst is { } entryFirst ? Math.Min(entryFirst, first) : first;
        _entryLast = _entryLast is { } entryLast ? Math.Max(entryLast, last) : last;
    }

    #endregion // Methods
}