namespace Censio.Pipe;

/// <summary>
/// The line format of a pipe recording.
/// </summary>
public static class PipeRecordingFormat
{
    #region Constants

    /// <summary>
    /// The format name in the header line.
    /// </summary>
    public const string FormatName = "censio-pipe-recording";

    /// <summary>
    /// The format version in the header line.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// The longest line in bytes, the line terminator excluded.
    /// </summary>
    public const int MaxLineLength = 1402200;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats the header line, including its terminator.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <returns>The header line</returns>
    public static string FormatHeader(DateTimeOffset started)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats a frame line, including its terminator.
    /// </summary>
    /// <param name="receiveTime">The time the frame was received</param>
    /// <param name="frame">The whole frame</param>
    /// <returns>The frame line</returns>
    public static string FormatFrameLine(DateTimeOffset receiveTime, ReadOnlySpan<byte> frame)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the name of a recording file.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <param name="sequence">The sequence number, 1 for the first candidate</param>
    /// <returns>The file name</returns>
    public static string FormatFileName(DateTimeOffset started, int sequence)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks a header line.
    /// </summary>
    /// <param name="line">The line without its terminator</param>
    /// <returns>The problem, or <see langword="null" /> when the header is valid</returns>
    public static PipeReplayProblemKind? CheckHeader(ReadOnlySpan<byte> line)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Parses a frame line.
    /// </summary>
    /// <param name="line">The line without its terminator</param>
    /// <param name="receiveTime">The receive time in UTC</param>
    /// <param name="frame">The decoded frame</param>
    /// <returns><see langword="true" /> when the line is a valid frame line</returns>
    public static bool TryParseFrameLine(ReadOnlySpan<byte> line, out DateTimeOffset receiveTime, out byte[] frame)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}