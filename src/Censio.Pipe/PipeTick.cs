namespace Censio.Pipe;

/// <summary>
/// The island frames that share one game time.
/// </summary>
public sealed class PipeTick
{
    #region Properties

    /// <summary>
    /// Gets the game time shared by the islands.
    /// </summary>
    public required long GameTime { get; init; }

    /// <summary>
    /// Gets the receive time of the last island frame.
    /// </summary>
    public required DateTimeOffset ReceiveTime { get; init; }

    /// <summary>
    /// Gets the islands in stream order.
    /// </summary>
    public required IReadOnlyList<PipeTickIsland> Islands { get; init; }

    /// <summary>
    /// Gets why the tick was closed.
    /// </summary>
    public required PipeTickCloseReason CloseReason { get; init; }

    /// <summary>
    /// Gets a value indicating whether every island has no entries.
    /// </summary>
    public bool IsNoDataYet => throw new NotImplementedException();

    #endregion // Properties
}