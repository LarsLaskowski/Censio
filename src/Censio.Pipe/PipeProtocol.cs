namespace Censio.Pipe;

/// <summary>
/// Holds the constants of the statistics pipe protocol.
/// </summary>
public static class PipeProtocol
{
    #region Constants

    /// <summary>
    /// The oldest protocol version this reader decodes.
    /// </summary>
    public const int OldestSupportedVersion = 2;

    /// <summary>
    /// The newest protocol version this reader decodes.
    /// </summary>
    public const int NewestSupportedVersion = 3;

    /// <summary>
    /// The largest accepted frame length in bytes (1 MiB), counting the type byte and the payload.
    /// </summary>
    public const int MaxFrameLength = 1048576;

    /// <summary>
    /// The largest accepted number of pairs in one workforce or building map.
    /// </summary>
    public const int MaxMapPairs = 1024;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Determines whether this reader decodes the given protocol version.
    /// </summary>
    /// <param name="version">The announced protocol version</param>
    /// <returns><see langword="true"/> when the version lies within the supported range</returns>
    public static bool IsSupported(int version)
    {
        return version is >= OldestSupportedVersion and <= NewestSupportedVersion;
    }

    #endregion // Methods
}