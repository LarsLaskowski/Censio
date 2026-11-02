using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Forms the lines of the log file.
/// </summary>
public static class LogLine
{
    #region Constants

    /// <summary>
    /// The maximum length of a message in UTF-16 characters.
    /// </summary>
    public const int MaxMessageLength = 2048;

    /// <summary>
    /// The maximum length of an exception text in UTF-16 characters.
    /// </summary>
    public const int MaxExceptionLength = 16384;

    /// <summary>
    /// The character that marks a cut text.
    /// </summary>
    private const char Ellipsis = '…';

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Forms the lines of a log entry.
    /// </summary>
    /// <param name="localTime">The local time</param>
    /// <param name="level">The level</param>
    /// <param name="message">The message</param>
    /// <param name="exception">The exception, or <see langword="null" /> if there is none</param>
    /// <returns>The lines, each ended by a line break</returns>
    public static string Format(DateTimeOffset localTime, LogLevel level, string message, Exception? exception)
    {
        var builder = new StringBuilder();

        builder.Append(localTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture))
               .Append(' ')
               .Append(GetLevelText(level))
               .Append(' ')
               .Append(Sanitize(message, MaxMessageLength))
               .Append('\n');

        if (exception is not null)
        {
            foreach (var line in Cut(exception.ToString(), MaxExceptionLength).Split('\n'))
            {
                builder.Append("  ")
                       .Append(Sanitize(line.Replace("\r", string.Empty, StringComparison.Ordinal), int.MaxValue))
                       .Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Replaces control and format characters and cuts the text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="maxLength">The maximum length in UTF-16 characters</param>
    /// <returns>The sanitized text</returns>
    public static string Sanitize(string text, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(text.Length, maxLength) + 1);
        var index = 0;

        while (index < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var consumed);

            string replacement;

            if (status != System.Buffers.OperationStatus.Done || IsReplaced(rune))
            {
                replacement = "?";
            }
            else
            {
                replacement = text.Substring(index, consumed);
            }

            if (builder.Length + replacement.Length > maxLength)
            {
                builder.Append(Ellipsis);

                break;
            }

            builder.Append(replacement);
            index += consumed;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Cuts a text to a length without splitting a surrogate pair.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="maxLength">The maximum length in UTF-16 characters</param>
    /// <returns>The text, with an ellipsis when it was cut</returns>
    private static string Cut(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var length = maxLength;

        if (char.IsLowSurrogate(text[length]) && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length] + Ellipsis;
    }

    /// <summary>
    /// Determines whether a code point is replaced by a question mark.
    /// </summary>
    /// <param name="rune">The code point</param>
    /// <returns><see langword="true" /> if the code point is a control, format or separator character</returns>
    private static bool IsReplaced(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                                             or UnicodeCategory.Format
                                             or UnicodeCategory.LineSeparator
                                             or UnicodeCategory.ParagraphSeparator;
    }

    /// <summary>
    /// Gets the three-letter text of a level.
    /// </summary>
    /// <param name="level">The level</param>
    /// <returns>The text</returns>
    private static string GetLevelText(LogLevel level)
    {
        return level switch
               {
                   LogLevel.Warning => "WRN",
                   LogLevel.Error => "ERR",
                   LogLevel.Critical => "CRT",
                   _ => "INF"
               };
    }

    #endregion // Methods
}