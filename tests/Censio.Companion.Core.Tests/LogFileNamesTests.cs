using System.Globalization;
using System.Text.RegularExpressions;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LogFileNames" />.
/// </summary>
[TestClass]
public sealed class LogFileNamesTests
{
    #region Fields

    /// <summary>
    /// The local date of the names of the tests.
    /// </summary>
    private static readonly DateOnly _date = new(2026, 11, 2);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// The first file of a day has no number, a continuation the number after a dash.
    /// </summary>
    /// <param name="sequence">The number of the file of the day</param>
    /// <param name="expected">The expected name</param>
    [TestMethod]
    [DataRow(1, "companion-20261102.log")]
    [DataRow(2, "companion-20261102-2.log")]
    [DataRow(9999, "companion-20261102-9999.log")]
    public void LogFileNamesFormatFirstAndContinuation(int sequence, string expected)
    {
        // Act
        var name = LogFileNames.Format(_date, sequence);

        // Assert
        Assert.AreEqual(expected, name, "The name should be companion-<yyyyMMdd>.log, a continuation with -<n>");
    }

    /// <summary>
    /// Only the exact forms of a log file name are accepted, ignoring case.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="expected">Whether the name is accepted</param>
    [TestMethod]
    [DataRow("companion-20261102.log", true)]
    [DataRow("companion-20261102-2.log", true)]
    [DataRow("companion-20261102-9999.log", true)]
    [DataRow("COMPANION-20261102.LOG", true)]
    [DataRow("companion-20261102-1.log", false)]
    [DataRow("companion-20261102-02.log", false)]
    [DataRow("companion-20261102-10000.log", false)]
    [DataRow("companion-20261332.log", false)]
    [DataRow("companion-2026110.log", false)]
    [DataRow("companion-20261102.log.bak", false)]
    [DataRow("companion-20261102.logx", false)]
    [DataRow("companion-20261102_001.log", false)]
    [DataRow("xcompanion-20261102.log", false)]
    [DataRow("companion-20261102 .log", false)]
    [DataRow("recording-20261102-182603Z.jsonl", false)]
    [DataRow("companion-20261102.log:stream", false)]
    [DataRow("..\\companion-20261102.log", false)]
    [DataRow("companion-20261102-+2.log", false)]
    [DataRow("companion-20261102- 2.log", false)]
    [DataRow("companion-20261102-2 .log", false)]
    [DataRow("companion-20261102-.log", false)]
    public void LogFileNamesTryParseAcceptsOnlyExactForms(string fileName, bool expected)
    {
        // Act
        var accepted = LogFileNames.TryParse(fileName, out _, out _);

        // Assert
        Assert.AreEqual(expected, accepted, $"The name '{fileName}' should be accepted only in its exact form");
    }

    /// <summary>
    /// A name with a character outside ASCII, or a digit that is not an ASCII digit, is refused. Each name is
    /// written with its escape, which the helper turns into the character.
    /// </summary>
    /// <param name="escapedFileName">The file name with the escape of its character</param>
    [TestMethod]
    [DataRow("companion-20261102-2\\u0000.log")]
    [DataRow("companion-20261102-\\u0662.log")]
    [DataRow("companion-\\uFF12\\uFF10\\uFF12\\uFF161102.log")]
    [DataRow("compan\\u0131on-20261102.log")]
    public void LogFileNamesTryParseRefusesNonAsciiForms(string escapedFileName)
    {
        // Arrange
        Assert.Contains("\\u", escapedFileName, "The data row should still hold its escape");

        var fileName = Unescape(escapedFileName);

        // Act
        var accepted = LogFileNames.TryParse(fileName, out _, out _);

        // Assert
        Assert.IsFalse(accepted, $"The name '{escapedFileName}' should be refused");
    }

    /// <summary>
    /// An accepted name gives its date and its number, 1 for the first file of a day.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="expectedSequence">The expected number</param>
    [TestMethod]
    [DataRow("companion-20261102.log", 1)]
    [DataRow("companion-20261102-2.log", 2)]
    [DataRow("companion-20261102-9999.log", 9999)]
    [DataRow("COMPANION-20261102.LOG", 1)]
    public void LogFileNamesTryParseAcceptedNameGivesDateAndNumber(string fileName, int expectedSequence)
    {
        // Act
        _ = LogFileNames.TryParse(fileName, out var date, out var sequence);

        // Assert
        Assert.AreEqual(_date, date, "The date should be read from the name");
        Assert.AreEqual(expectedSequence, sequence, "The number should be read from the name");
    }

    /// <summary>
    /// The newest log files by date and then by number are kept, the older ones selected; other names never.
    /// </summary>
    [TestMethod]
    public void LogFileNamesSelectForDeletionKeepsNewestByDateThenNumber()
    {
        // Arrange
        string[] names = [
                             "companion-20261101-2.log",
                             "notes.txt",
                             "companion-20261030.log",
                             "companion-20261102.log",
                             "companion-20261102.log.bak",
                             "companion-20261101-10.log",
                             "COMPANION-20261029.LOG",
                             "companion-20261031-3.log",
                             "recording-20261102-182603Z.jsonl",
                             "companion-20261101.log",
                         ];

        // Act
        var selected = LogFileNames.SelectForDeletion(names, 3);

        // Assert
        string[] expected = ["COMPANION-20261029.LOG", "companion-20261030.log", "companion-20261031-3.log", "companion-20261101.log"];

        Assert.AreSequenceEqual(expected, selected.Order(StringComparer.Ordinal).ToArray(), "All but the 3 newest log files by date and then number should be selected, and no other file");
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