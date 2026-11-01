using System.Text;

using Censio.Pipe;
using Censio.Pipe.Tests;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Builds the frames and the recording files the tests of the recording and the replay feed into the pipeline.
/// </summary>
public static class PipeTestFrames
{
    #region Methods

    /// <summary>
    /// Creates a version frame without its length prefix.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The frame</returns>
    public static byte[] Version(int version)
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.Version).Int32(version).Build();
    }

    /// <summary>
    /// Creates a session-start frame without its length prefix.
    /// </summary>
    /// <param name="headline">The headline</param>
    /// <returns>The frame</returns>
    public static byte[] SessionStart(string headline)
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.SessionStart).String(headline).Build();
    }

    /// <summary>
    /// Creates a session-end frame without its length prefix.
    /// </summary>
    /// <returns>The frame</returns>
    public static byte[] SessionEnd()
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.SessionEnd).Build();
    }

    /// <summary>
    /// Creates an island frame (area production statistics) with one production entry, without its length prefix.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the island</param>
    /// <param name="timestamp">The game time</param>
    /// <returns>The frame</returns>
    public static byte[] Island(int sessionGuid, long timestamp)
    {
        return new PipeFrameBuilder().Byte((byte)PipeMessageType.AreaProductionStatistics)
                                     .Byte(7)
                                     .Byte(1)
                                     .Byte(0)
                                     .Int32(sessionGuid)
                                     .String("Ostia")
                                     .Int64(timestamp)
                                     .Int32(1)
                                     .Entry(sessionGuid + 10, [new(1, 2)], [new(3, 4)])
                                     .Build();
    }

    /// <summary>
    /// Creates a frame of a type the reader does not know, without its length prefix.
    /// </summary>
    /// <returns>The frame</returns>
    public static byte[] UnknownType()
    {
        return [99, 1, 2];
    }

    /// <summary>
    /// Creates the bytes of a stream from frames, each with its length prefix.
    /// </summary>
    /// <param name="frames">The frames without their length prefix</param>
    /// <returns>The bytes</returns>
    public static byte[] Wire(params byte[][] frames)
    {
        return [.. frames.SelectMany(PipeFrameBuilder.WithPrefix)];
    }

    /// <summary>
    /// Creates a recording file: the header and one line per frame.
    /// </summary>
    /// <param name="started">The time of the header</param>
    /// <param name="frames">The receive times and the frames without their length prefix</param>
    /// <returns>The bytes of the file</returns>
    public static byte[] Recording(DateTimeOffset started, params (DateTimeOffset Time, byte[] Frame)[] frames)
    {
        var text = PipeRecordingFormat.FormatHeader(started) + string.Concat(frames.Select(frame => PipeRecordingFormat.FormatFrameLine(frame.Time, frame.Frame)));

        return Encoding.UTF8.GetBytes(text);
    }

    #endregion // Methods
}