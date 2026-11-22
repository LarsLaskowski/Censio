using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// An item the pipe connection read, with the time it was read.
/// </summary>
public sealed record PipeConnectionItem
{
    #region Properties

    /// <summary>
    /// Gets the result of the item.
    /// </summary>
    public required PipeTickResult Result { get; init; }

    /// <summary>
    /// Gets the time the frame of the item was read.
    /// </summary>
    public required DateTimeOffset ReadStamp { get; init; }

    #endregion // Properties
}