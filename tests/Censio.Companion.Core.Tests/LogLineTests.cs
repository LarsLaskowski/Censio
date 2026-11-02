using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LogLine" />.
/// </summary>
[TestClass]
public sealed class LogLineTests
{
    #region Fields

    /// <summary>
    /// The local time of the lines of the tests.
    /// </summary>
    private static readonly DateTimeOffset _time = new(2026, 11, 2, 18, 26, 3, 45, TimeSpan.FromHours(1));

    #endregion // Fields

    #region Methods

    /// <summary>
    /// A line holds the local time with milliseconds and offset, the level in three letters and the message.
    /// </summary>
    /// <param name="level">The level</param>
    /// <param name="expectedLevel">The expected level text</param>
    [TestMethod]
    [DataRow(LogLevel.Information, "INF")]
    [DataRow(LogLevel.Warning, "WRN")]
    [DataRow(LogLevel.Error, "ERR")]
    [DataRow(LogLevel.Critical, "CRT")]
    public void LogLineFormatLayout(LogLevel level, string expectedLevel)
    {
        // Act
        var line = LogLine.Format(_time, level, "Recording started", null);

        // Assert
        Assert.AreEqual($"2026-11-02T18:26:03.045+01:00 {expectedLevel} Recording started\n", line, "The line should be time, level and message, ended by a line break");
    }

    /// <summary>
    /// Control and format characters, line and paragraph separators and unpaired surrogates become one question
    /// mark each, a supplementary-plane format character one question mark for its pair; other characters stay.
    /// Each case is written with its escape, which the helper turns into the UTF-16 units.
    /// </summary>
    /// <param name="escapedInput">The character of the case as escape</param>
    /// <param name="escapedExpected">The expected replacement as escape or text</param>
    [TestMethod]
    [DataRow("\\u0000", "?")]
    [DataRow("\\u0009", "?")]
    [DataRow("\\u000A", "?")]
    [DataRow("\\u000D", "?")]
    [DataRow("\\u001B", "?")]
    [DataRow("\\u007F", "?")]
    [DataRow("\\u0085", "?")]
    [DataRow("\\u200B", "?")]
    [DataRow("\\u200E", "?")]
    [DataRow("\\u202E", "?")]
    [DataRow("\\u2066", "?")]
    [DataRow("\\u2028", "?")]
    [DataRow("\\u2029", "?")]
    [DataRow("\\uFEFF", "?")]
    [DataRow("\\uD800", "?")]
    [DataRow("\\uDC00", "?")]
    [DataRow("\\uDB40\\uDC01", "?")]
    [DataRow("\\uDB40\\uDC41", "?")]
    [DataRow("\\uD834\\uDD73", "?")]
    [DataRow("\\uD83D\\uDE00", "\\uD83D\\uDE00")]
    [DataRow("\\u00E4", "\\u00E4")]
    public void LogLineSanitizeReplacesControlAndFormatCharacters(string escapedInput, string escapedExpected)
    {
        // Arrange
        Assert.Contains("\\u", escapedInput, "The data row should still hold its escape");

        var text = $"a{Unescape(escapedInput)}b";

        // Act
        var sanitized = LogLine.Sanitize(text, LogLine.MaxMessageLength);

        // Assert
        Assert.AreEqual($"a{Unescape(escapedExpected)}b", sanitized, $"The character {escapedInput} should become {escapedExpected}");
    }

    /// <summary>
    /// A message longer than the limit is cut to the limit and gets an ellipsis.
    /// </summary>
    [TestMethod]
    public void LogLineSanitizeCutsLongMessage()
    {
        // Arrange
        var text = new string('x', LogLine.MaxMessageLength + 1);

        // Act
        var sanitized = LogLine.Sanitize(text, LogLine.MaxMessageLength);

        // Assert
        Assert.AreEqual(new string('x', LogLine.MaxMessageLength) + "…", sanitized, "The message should be cut to the limit and end with an ellipsis");
    }

    /// <summary>
    /// A message of exactly the limit is kept as it is.
    /// </summary>
    [TestMethod]
    public void LogLineSanitizeKeepsMessageAtLimit()
    {
        // Arrange
        var text = new string('x', LogLine.MaxMessageLength);

        // Act
        var sanitized = LogLine.Sanitize(text, LogLine.MaxMessageLength);

        // Assert
        Assert.AreEqual(text, sanitized, "A message of exactly the limit should not be cut");
    }

    /// <summary>
    /// A cut that would fall between the two units of a surrogate pair is made before the pair.
    /// </summary>
    [TestMethod]
    public void LogLineSanitizeCutNeverSplitsPair()
    {
        // Arrange
        var text = $"xxx{char.ConvertFromUtf32(0x1F600)}y";

        // Act
        var sanitized = LogLine.Sanitize(text, 4);

        // Assert
        Assert.AreEqual("xxx…", sanitized, "The cut should be made before the surrogate pair");
    }

    /// <summary>
    /// An exception is written on the lines after the message, each line indented by two spaces; carriage returns
    /// are dropped and other control characters replaced.
    /// </summary>
    [TestMethod]
    public void LogLineFormatExceptionKeepsLineBreaksOnly()
    {
        // Arrange
        var exception = new InvalidOperationException($"first\r\nsecond{char.ConvertFromUtf32(0x1B)}x\nthird");

        // Act
        var line = LogLine.Format(_time, LogLevel.Warning, "Could not open the log folder", exception);

        // Assert
        const string expected = "2026-11-02T18:26:03.045+01:00 WRN Could not open the log folder\n"
                                + "  System.InvalidOperationException: first\n"
                                + "  second?x\n"
                                + "  third\n";

        Assert.AreEqual(expected, line, "Each line of the exception should be its own indented line");
    }

    /// <summary>
    /// An exception text longer than its limit is cut to the limit.
    /// </summary>
    [TestMethod]
    public void LogLineFormatCutsLongException()
    {
        // Arrange
        var exception = new InvalidOperationException(new string('x', LogLine.MaxExceptionLength + 4000));
        var text = exception.ToString();

        // Act
        var line = LogLine.Format(_time, LogLevel.Warning, "A background task failed", exception);

        // Assert
        var lines = line.Split('\n');

        Assert.HasCount(3, lines, "The entry should be the message line and one exception line, ended by a line break");
        Assert.StartsWith("  " + text[..LogLine.MaxExceptionLength], lines[1], "The exception line should keep the first characters up to the limit");
        Assert.IsLessThanOrEqualTo(2 + LogLine.MaxExceptionLength + 1, lines[1].Length, "The exception line should hold at most the limit, its indent and an ellipsis");
    }

    /// <summary>
    /// A cut of an exception that would fall between the two units of a surrogate pair is made before the pair.
    /// </summary>
    [TestMethod]
    public void LogLineFormatExceptionCutNeverSplitsPair()
    {
        // Arrange
        var prefixLength = $"{typeof(InvalidOperationException).FullName}: ".Length;
        var exception = new InvalidOperationException($"{new string('x', LogLine.MaxExceptionLength - prefixLength - 1)}{char.ConvertFromUtf32(0x1F600)}tail");
        var text = exception.ToString();

        // Act
        var line = LogLine.Format(_time, LogLevel.Warning, "A background task failed", exception);

        // Assert
        Assert.IsTrue(char.IsHighSurrogate(text[LogLine.MaxExceptionLength - 1]), "The limit should fall inside the surrogate pair");
        Assert.AreEqual($"  {text[..(LogLine.MaxExceptionLength - 1)]}…", line.Split('\n')[1], "The exception should be cut before the surrogate pair");
    }

    /// <summary>
    /// Replaces every escape of a UTF-16 unit in the text by the unit it names.
    /// </summary>
    /// <param name="text">The text with escapes</param>
    /// <returns>The text with the units</returns>
    private static string Unescape(string text)
    {
        return Regex.Replace(text, @"\\u([0-9A-Fa-f]{4})", match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(), RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    #endregion // Methods
}