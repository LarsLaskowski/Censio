using System.Globalization;
using System.Text.Json;

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

    /// <summary>
    /// The most bytes a decoded frame may have.
    /// </summary>
    private const int MaxFrameBytes = 1048576;

    /// <summary>
    /// The most characters the base64 text of a frame may have.
    /// </summary>
    private const int MaxBase64Length = 1398104;

    /// <summary>
    /// The margin before the largest date, so that adding the quiet period to a receive time cannot overflow.
    /// </summary>
    private const long TimeMarginTicks = 30 * TimeSpan.TicksPerSecond;

    /// <summary>
    /// The format of a time in a recording.
    /// </summary>
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>
    /// The format of the start in a file name.
    /// </summary>
    private const string FileNameTimeFormat = "yyyyMMdd-HHmmss'Z'";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Formats the header line, including its terminator.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <returns>The header line</returns>
    public static string FormatHeader(DateTimeOffset started)
    {
        return string.Concat("{\"format\":\"", FormatName, "\",\"version\":", FormatVersion.ToString(CultureInfo.InvariantCulture), ",\"started\":\"", FormatTime(started), "\"}\n");
    }

    /// <summary>
    /// Formats a frame line, including its terminator.
    /// </summary>
    /// <param name="receiveTime">The time the frame was received</param>
    /// <param name="frame">The whole frame</param>
    /// <returns>The frame line</returns>
    public static string FormatFrameLine(DateTimeOffset receiveTime, ReadOnlySpan<byte> frame)
    {
        return string.Concat("{\"time\":\"", FormatTime(receiveTime), "\",\"frame\":\"", Convert.ToBase64String(frame), "\"}\n");
    }

    /// <summary>
    /// Formats the name of a recording file.
    /// </summary>
    /// <param name="started">The time the recording started</param>
    /// <param name="sequence">The sequence number, 1 for the first candidate</param>
    /// <returns>The file name</returns>
    public static string FormatFileName(DateTimeOffset started, int sequence)
    {
        var stamp = started.ToUniversalTime().ToString(FileNameTimeFormat, CultureInfo.InvariantCulture);

        return sequence <= 1
                   ? $"recording-{stamp}.jsonl"
                   : $"recording-{stamp}-{sequence.ToString(CultureInfo.InvariantCulture)}.jsonl";
    }

    /// <summary>
    /// Checks a header line.
    /// </summary>
    /// <param name="line">The line without its terminator</param>
    /// <returns>The problem, or <see langword="null" /> when the header is valid</returns>
    public static PipeReplayProblemKind? CheckHeader(ReadOnlySpan<byte> line)
    {
        try
        {
            if (TryReadHeader(line, out var version) == false)
            {
                return PipeReplayProblemKind.NotARecording;
            }

            return version == FormatVersion ? null : PipeReplayProblemKind.UnsupportedVersion;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return PipeReplayProblemKind.NotARecording;
        }
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
        receiveTime = default;
        frame = [];

        try
        {
            return TryReadFrameLine(line, out receiveTime, out frame);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            receiveTime = default;
            frame = [];

            return false;
        }
    }

    /// <summary>
    /// Formats a time in UTC with seven fractional digits.
    /// </summary>
    /// <param name="time">The time</param>
    /// <returns>The text</returns>
    private static string FormatTime(DateTimeOffset time)
    {
        return time.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the header fields with the type of each checked before its value is read.
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="version">The format version</param>
    /// <returns><see langword="true" /> when the line is an object with a valid format name and an integer version</returns>
    private static bool TryReadHeader(ReadOnlySpan<byte> line, out int version)
    {
        version = 0;

        var reader = new Utf8JsonReader(line);
        var hasFormat = false;
        var hasVersion = false;

        if (TryStartObject(ref reader) == false)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (TryReadHeaderField(ref reader, ref hasFormat, ref hasVersion, ref version) == false)
            {
                return false;
            }
        }

        return hasFormat && hasVersion && IsEndOfObject(ref reader);
    }

    /// <summary>
    /// Reads the header field the reader is on; an unknown field is skipped.
    /// </summary>
    /// <param name="reader">The reader on the property name</param>
    /// <param name="hasFormat">Whether the format field was read before</param>
    /// <param name="hasVersion">Whether the version field was read before</param>
    /// <param name="version">The format version</param>
    /// <returns><see langword="true" /> when the field is valid</returns>
    private static bool TryReadHeaderField(ref Utf8JsonReader reader, ref bool hasFormat, ref bool hasVersion, ref int version)
    {
        if (reader.ValueTextEquals("format"u8))
        {
            if (hasFormat || reader.Read() == false || reader.TokenType != JsonTokenType.String || reader.ValueTextEquals("censio-pipe-recording"u8) == false)
            {
                return false;
            }

            hasFormat = true;

            return true;
        }

        if (reader.ValueTextEquals("version"u8))
        {
            if (hasVersion || reader.Read() == false || reader.TokenType != JsonTokenType.Number || reader.TryGetInt32(out version) == false)
            {
                return false;
            }

            hasVersion = true;

            return true;
        }

        return SkipValue(ref reader);
    }

    /// <summary>
    /// Reads the fields of a frame line with the type of each checked before its value is read.
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="receiveTime">The receive time in UTC</param>
    /// <param name="frame">The decoded frame</param>
    /// <returns><see langword="true" /> when the line is a valid frame line</returns>
    private static bool TryReadFrameLine(ReadOnlySpan<byte> line, out DateTimeOffset receiveTime, out byte[] frame)
    {
        receiveTime = default;
        frame = [];

        var reader = new Utf8JsonReader(line);
        var hasTime = false;
        var hasFrame = false;

        if (TryStartObject(ref reader) == false)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (TryReadFrameField(ref reader, ref hasTime, ref hasFrame, ref receiveTime, ref frame) == false)
            {
                return false;
            }
        }

        return hasTime && hasFrame && IsEndOfObject(ref reader);
    }

    /// <summary>
    /// Reads the frame line field the reader is on; an unknown field is skipped.
    /// </summary>
    /// <param name="reader">The reader on the property name</param>
    /// <param name="hasTime">Whether the time field was read before</param>
    /// <param name="hasFrame">Whether the frame field was read before</param>
    /// <param name="receiveTime">The receive time in UTC</param>
    /// <param name="frame">The decoded frame</param>
    /// <returns><see langword="true" /> when the field is valid</returns>
    private static bool TryReadFrameField(ref Utf8JsonReader reader, ref bool hasTime, ref bool hasFrame, ref DateTimeOffset receiveTime, ref byte[] frame)
    {
        if (reader.ValueTextEquals("time"u8))
        {
            if (hasTime || reader.Read() == false || reader.TokenType != JsonTokenType.String || TryReadTime(ref reader, out receiveTime) == false)
            {
                return false;
            }

            hasTime = true;

            return true;
        }

        if (reader.ValueTextEquals("frame"u8))
        {
            if (hasFrame || reader.Read() == false || reader.TokenType != JsonTokenType.String || TryReadFrame(ref reader, out frame) == false)
            {
                return false;
            }

            hasFrame = true;

            return true;
        }

        return SkipValue(ref reader);
    }

    /// <summary>
    /// Reads the start of the object a line must consist of.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns><see langword="true" /> when the first token starts an object</returns>
    private static bool TryStartObject(ref Utf8JsonReader reader)
    {
        return reader.Read() && reader.TokenType == JsonTokenType.StartObject;
    }

    /// <summary>
    /// Checks that the reader is on the end of the object and that no token follows.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns><see langword="true" /> when the object ends the line</returns>
    private static bool IsEndOfObject(ref Utf8JsonReader reader)
    {
        return reader.TokenType == JsonTokenType.EndObject && reader.Read() == false;
    }

    /// <summary>
    /// Skips the value of the property the reader is on.
    /// </summary>
    /// <param name="reader">The reader on the property name</param>
    /// <returns><see langword="true" />, as an unknown field is accepted</returns>
    private static bool SkipValue(ref Utf8JsonReader reader)
    {
        reader.Read();
        reader.Skip();

        return true;
    }

    /// <summary>
    /// Reads the time of a string token and checks that the quiet deadline of a tick at that time cannot overflow.
    /// </summary>
    /// <param name="reader">The reader on the string token</param>
    /// <param name="receiveTime">The time in UTC</param>
    /// <returns><see langword="true" /> when the time is valid</returns>
    private static bool TryReadTime(ref Utf8JsonReader reader, out DateTimeOffset receiveTime)
    {
        receiveTime = default;

        if (reader.TryGetDateTimeOffset(out var time) == false)
        {
            return false;
        }

        var utc = time.ToUniversalTime();

        if (utc.UtcTicks > DateTimeOffset.MaxValue.UtcTicks - TimeMarginTicks)
        {
            return false;
        }

        receiveTime = utc;

        return true;
    }

    /// <summary>
    /// Decodes the base64 text of a string token after its length is checked.
    /// </summary>
    /// <param name="reader">The reader on the string token</param>
    /// <param name="frame">The decoded frame</param>
    /// <returns><see langword="true" /> when the frame is valid base64 within the limits</returns>
    private static bool TryReadFrame(ref Utf8JsonReader reader, out byte[] frame)
    {
        frame = [];

        if (reader.HasValueSequence || reader.ValueSpan.Length > MaxBase64Length || reader.TryGetBytesFromBase64(out var decoded) == false || decoded.Length > MaxFrameBytes)
        {
            return false;
        }

        frame = decoded;

        return true;
    }

    #endregion // Methods
}