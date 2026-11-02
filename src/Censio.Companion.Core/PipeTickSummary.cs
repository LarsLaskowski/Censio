using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// What the tray shows of the last tick.
/// </summary>
public readonly record struct PipeTickSummary
{
    #region Properties

    /// <summary>
    /// Gets the receive time of the tick.
    /// </summary>
    public required DateTimeOffset ReceiveTime { get; init; }

    /// <summary>
    /// Gets the number of islands of the tick.
    /// </summary>
    public required int IslandCount { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Summarizes a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The summary</returns>
    public static PipeTickSummary From(PipeTick tick)
    {
        return new PipeTickSummary
               {
                   ReceiveTime = tick.ReceiveTime,
                   IslandCount = tick.Islands.Count
               };
    }

    #endregion // Methods
}