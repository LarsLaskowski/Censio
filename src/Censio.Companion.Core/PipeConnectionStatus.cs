using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// The status of the connection to the statistics pipe.
/// </summary>
public readonly record struct PipeConnectionStatus
{
    #region Properties

    /// <summary>
    /// Gets the state of the connection.
    /// </summary>
    public required PipeConnectionState State { get; init; }

    /// <summary>
    /// Gets the kind of the pipe error that ended the last connection, if any.
    /// </summary>
    public PipeErrorKind? ErrorKind { get; init; }

    /// <summary>
    /// Gets the Windows error code of the last failed attempt, if any.
    /// </summary>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// Gets the protocol version the pipe sent when the state is a protocol mismatch, if any.
    /// </summary>
    public int? ReceivedVersion { get; init; }

    #endregion // Properties
}