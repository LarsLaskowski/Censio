namespace Censio.Pipe;

/// <summary>
/// Groups read results into ticks and session events; pure, without a clock or I/O.
/// </summary>
public sealed class PipeTickBuilder
{
    #region Fields

    /// <summary>
    /// The islands of the open tick.
    /// </summary>
    private readonly List<PipeTickIsland> _islands = [];

    /// <summary>
    /// The keys of the islands of the open tick.
    /// </summary>
    private readonly HashSet<PipeIslandKey> _keys = [];

    /// <summary>
    /// The game time of the open tick.
    /// </summary>
    private long _gameTime;

    /// <summary>
    /// The number of entries and map pairs of the open tick.
    /// </summary>
    private int _itemCount;

    /// <summary>
    /// The receive time of the last island frame of the open tick.
    /// </summary>
    private DateTimeOffset _lastReceiveTime;

    /// <summary>
    /// Whether an island frame or a session start came since the last session end.
    /// </summary>
    private bool _endable;

    /// <summary>
    /// Whether the stream has ended.
    /// </summary>
    private bool _ended;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeTickBuilder" /> class.
    /// </summary>
    public PipeTickBuilder()
    {
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the time at which the open tick closes as quiet, or <see langword="null" /> without an open tick.
    /// </summary>
    public DateTimeOffset? QuietDeadline => _islands.Count == 0 ? null : _lastReceiveTime + PipeTickLimits.QuietPeriod;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a read result and returns the items it produces, in order.
    /// </summary>
    /// <param name="result">The read result</param>
    /// <param name="receiveTime">The time the result was received</param>
    /// <returns>The produced items</returns>
    public IReadOnlyList<PipeTickResult> Add(PipeReadResult result, DateTimeOffset receiveTime)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = new List<PipeTickResult>();

        if (_ended)
        {
            return items;
        }

        var quiet = CloseIfQuiet(receiveTime);

        if (quiet is not null)
        {
            items.Add(quiet);
        }

        switch (result.Kind)
        {
            case PipeReadResultKind.Message:
                {
                    AddMessage(result.Message!, receiveTime, items);
                }
                break;

            case PipeReadResultKind.Error:
                {
                    AddError(result.Error!, items);
                }
                break;

            default:
                {
                    CloseTick(PipeTickCloseReason.StreamEnded, items);
                    items.Add(PipeTickResult.End);
                    _ended = true;
                }
                break;
        }

        return items;
    }

    /// <summary>
    /// Closes the open tick when the quiet period has passed.
    /// </summary>
    /// <param name="now">The current time</param>
    /// <returns>The closed tick, or <see langword="null" /> when nothing closed</returns>
    public PipeTickResult? CloseIfQuiet(DateTimeOffset now)
    {
        var deadline = QuietDeadline;

        if (deadline is null || now < deadline.Value)
        {
            return null;
        }

        return PipeTickResult.FromTick(TakeTick(PipeTickCloseReason.QuietPeriod));
    }

    /// <summary>
    /// Handles a decoded message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="receiveTime">The time the message was received</param>
    /// <param name="items">The produced items</param>
    private void AddMessage(PipeMessage message, DateTimeOffset receiveTime, List<PipeTickResult> items)
    {
        switch (message)
        {
            case AreaProductionStatisticsMessage island:
                {
                    AddIsland(island, receiveTime, items);
                }
                break;

            case SessionStartMessage start:
                {
                    CloseTick(PipeTickCloseReason.SessionEvent, items);
                    _endable = true;
                    items.Add(PipeTickResult.FromSessionStart(start.Headline));
                }
                break;

            case SessionEndMessage:
                {
                    CloseTick(PipeTickCloseReason.SessionEvent, items);
                    items.Add(_endable ? PipeTickResult.SessionEnd : PipeTickResult.RepeatedSessionEnd);
                    _endable = false;
                }
                break;

            case VersionMessage version:
                {
                    items.Add(PipeTickResult.FromVersion(version.Version));
                }
                break;
        }
    }

    /// <summary>
    /// Handles a reader error.
    /// </summary>
    /// <param name="error">The error</param>
    /// <param name="items">The produced items</param>
    private void AddError(PipeError error, List<PipeTickResult> items)
    {
        if (error.Kind is PipeErrorKind.TruncatedFrame or PipeErrorKind.OversizedFrame or PipeErrorKind.ReadFailed)
        {
            CloseTick(PipeTickCloseReason.StreamEnded, items);
            items.Add(PipeTickResult.FromError(error));
            items.Add(PipeTickResult.End);
            _ended = true;

            return;
        }

        items.Add(PipeTickResult.FromError(error));
    }

    /// <summary>
    /// Handles an island frame.
    /// </summary>
    /// <param name="island">The island frame</param>
    /// <param name="receiveTime">The time the frame was received</param>
    /// <param name="items">The produced items</param>
    private void AddIsland(AreaProductionStatisticsMessage island, DateTimeOffset receiveTime, List<PipeTickResult> items)
    {
        _endable = true;

        var key = new PipeIslandKey(island.SessionGuid, island.IslandId, island.AreaIndex);

        if (_islands.Count > 0)
        {
            if (_gameTime != island.Timestamp)
            {
                CloseTick(PipeTickCloseReason.GameTimeChanged, items);
            }
            else if (_keys.Contains(key))
            {
                CloseTick(PipeTickCloseReason.IslandRepeated, items);
            }
        }

        var frameItems = 0L;

        foreach (var entry in island.Entries)
        {
            frameItems += 1 + entry.WorkforceAmounts.Count + entry.BuildingAmounts.Count;
        }

        if (_islands.Count + 1 > PipeTickLimits.MaxIslands || _itemCount + frameItems > PipeTickLimits.MaxEntriesAndPairs)
        {
            items.Add(PipeTickResult.FromTickLimit(key));

            return;
        }

        _gameTime = island.Timestamp;
        _itemCount += (int)frameItems;
        _lastReceiveTime = receiveTime;
        _keys.Add(key);
        _islands.Add(new PipeTickIsland
                     {
                         Key = key,
                         SessionId = island.SessionId,
                         Name = island.AreaName,
                         Entries = island.Entries
                     });
    }

    /// <summary>
    /// Closes the open tick, when there is one, and appends it to the items.
    /// </summary>
    /// <param name="reason">The close reason</param>
    /// <param name="items">The produced items</param>
    private void CloseTick(PipeTickCloseReason reason, List<PipeTickResult> items)
    {
        if (_islands.Count > 0)
        {
            items.Add(PipeTickResult.FromTick(TakeTick(reason)));
        }
    }

    /// <summary>
    /// Takes the open tick and resets the builder's tick state.
    /// </summary>
    /// <param name="reason">The close reason</param>
    /// <returns>The tick</returns>
    private PipeTick TakeTick(PipeTickCloseReason reason)
    {
        var tick = new PipeTick
                   {
                       GameTime = _gameTime,
                       ReceiveTime = _lastReceiveTime,
                       Islands = _islands.ToArray(),
                       CloseReason = reason
                   };

        _islands.Clear();
        _keys.Clear();
        _itemCount = 0;

        return tick;
    }

    #endregion // Methods
}