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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Replaces control and format characters and cuts the text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="maxLength">The maximum length in UTF-16 characters</param>
    /// <returns>The sanitized text</returns>
    public static string Sanitize(string text, int maxLength)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}