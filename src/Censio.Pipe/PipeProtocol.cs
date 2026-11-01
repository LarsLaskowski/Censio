namespace Censio.Pipe;

/// <summary>
/// Holds the constants of the statistics pipe protocol.
/// </summary>
public static class PipeProtocol
{
    #region Constants

    /// <summary>
    /// The protocol version this reader decodes.
    /// </summary>
    public const int SupportedVersion = 2;

    /// <summary>
    /// The largest accepted frame length in bytes (1 MiB), counting the type byte and the payload.
    /// </summary>
    public const int MaxFrameLength = 1048576;

    /// <summary>
    /// The largest accepted number of pairs in one workforce or building map.
    /// </summary>
    public const int MaxMapPairs = 1024;

    #endregion // Constants
}