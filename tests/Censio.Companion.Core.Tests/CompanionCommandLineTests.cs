using System.Globalization;

using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionCommandLine" />.
/// </summary>
[TestClass]
public sealed class CompanionCommandLineTests
{
    #region Fields

    /// <summary>
    /// The culture the test started with.
    /// </summary>
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Restores the culture of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        CultureInfo.CurrentCulture = _culture;
    }

    /// <summary>
    /// Without arguments the Companion runs as before.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseNoArgumentsReturnsNormal()
    {
        // Arrange
        var args = Args(string.Empty);

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Normal, commandLine.Mode, "No arguments should mean the normal mode");
        Assert.IsNull(commandLine.ReplayPath, "No replay path should be set");
    }

    /// <summary>
    /// The switch --record selects recording, in any case.
    /// </summary>
    /// <param name="text">The arguments</param>
    [TestMethod]
    [DataRow("--record")]
    [DataRow("--RECORD")]
    [DataRow("--Record")]
    public void CompanionCommandLineParseRecordReturnsRecord(string text)
    {
        // Arrange
        var args = Args(text);

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Record, commandLine.Mode, "The switch --record should select recording");
        Assert.IsNull(commandLine.ReplayPath, "Recording should set no replay path");
    }

    /// <summary>
    /// The switch --replay selects replay with the file and the real speed.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseReplayReturnsReplayAtRealSpeed()
    {
        // Arrange
        var args = Args("--replay file.jsonl");

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Replay, commandLine.Mode, "The switch --replay should select replay");
        Assert.AreEqual("file.jsonl", commandLine.ReplayPath, "The path should be the argument after --replay");
        Assert.AreEqual(PipeReplaySpeed.RealTime, commandLine.Speed, "Without --speed the speed should be real time");
    }

    /// <summary>
    /// A path with spaces is one argument.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseReplayPathWithSpacesIsKept()
    {
        // Arrange
        var args = new[] { "--replay", "C:\\My Recordings\\a b.jsonl" };

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Replay, commandLine.Mode, "The switch --replay should select replay");
        Assert.AreEqual("C:\\My Recordings\\a b.jsonl", commandLine.ReplayPath, "The path should be kept whole");
    }

    /// <summary>
    /// A numeric speed divides the recorded pauses.
    /// </summary>
    /// <param name="text">The speed text</param>
    /// <param name="factor">The expected factor</param>
    [TestMethod]
    [DataRow("10", 10d)]
    [DataRow("0.1", 0.1d)]
    [DataRow("1000", 1000d)]
    [DataRow("2.5", 2.5d)]
    public void CompanionCommandLineParseReplayWithSpeedFactorReturnsFactor(string text, double factor)
    {
        // Arrange
        var args = Args("--replay f --speed " + text);

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Replay, commandLine.Mode, "The switch --replay should select replay");
        Assert.AreEqual(factor, commandLine.Speed.Factor, "The speed should be the given factor");
    }

    /// <summary>
    /// The speed max replays without pauses, whatever the order and the case of the options.
    /// </summary>
    /// <param name="text">The arguments</param>
    [TestMethod]
    [DataRow("--speed max --replay f")]
    [DataRow("--replay f --speed max")]
    [DataRow("--REPLAY f --Speed MAX")]
    [DataRow("--replay f --speed Max")]
    public void CompanionCommandLineParseSpeedMaxReturnsMaximum(string text)
    {
        // Arrange
        var args = Args(text);

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Replay, commandLine.Mode, "The switch --replay should select replay");
        Assert.AreEqual("f", commandLine.ReplayPath, "The path should be the argument after --replay");
        Assert.AreEqual(PipeReplaySpeed.Maximum, commandLine.Speed, "The speed max should select the maximum speed");
    }

    /// <summary>
    /// A command line the Companion does not know is invalid.
    /// </summary>
    /// <param name="text">The arguments</param>
    [TestMethod]
    [DataRow("--unknown")]
    [DataRow("record")]
    [DataRow("--record extra")]
    [DataRow("--speed 10")]
    [DataRow("--speed max")]
    [DataRow("--replay f --speed fast")]
    [DataRow("--replay f --speed 0.09")]
    [DataRow("--replay f --speed 1000.1")]
    [DataRow("--replay f --speed 1,5")]
    [DataRow("--replay f --speed 0")]
    [DataRow("--replay f --speed -1")]
    [DataRow("--replay f --speed NaN")]
    [DataRow("--replay f --speed")]
    [DataRow("--replay")]
    [DataRow("--replay --speed 10")]
    [DataRow("--record --replay f")]
    [DataRow("--replay f --record")]
    [DataRow("--record --record")]
    [DataRow("--replay a --replay b")]
    [DataRow("--replay f --speed 10 --speed 20")]
    public void CompanionCommandLineParseInvalidReturnsInvalid(string text)
    {
        // Arrange
        var args = Args(text);

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Invalid, commandLine.Mode, $"The command line '{text}' should be invalid");
    }

    /// <summary>
    /// An empty replay path is invalid.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseEmptyReplayPathReturnsInvalid()
    {
        // Arrange
        var args = new[] { "--replay", string.Empty };

        // Act
        var commandLine = CompanionCommandLine.Parse(args);

        // Assert
        Assert.AreEqual(CompanionMode.Invalid, commandLine.Mode, "An empty replay path should make the command line invalid");
    }

    /// <summary>
    /// A speed is written with a decimal point, also under a culture that writes a decimal comma.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseSpeedWithPointUnderGermanCultureReturnsFactor()
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        // Act
        var commandLine = CompanionCommandLine.Parse(["--replay", "f", "--speed", "2.5"]);

        // Assert
        Assert.AreEqual(CompanionMode.Replay, commandLine.Mode, "A decimal point should be valid on a German Windows");
        Assert.AreEqual(2.5d, commandLine.Speed.Factor, "The speed should be read with the invariant culture");
    }

    /// <summary>
    /// A speed with a decimal comma is invalid, also under a culture that writes it that way.
    /// </summary>
    [TestMethod]
    public void CompanionCommandLineParseSpeedWithCommaUnderGermanCultureReturnsInvalid()
    {
        // Arrange
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        // Act
        var commandLine = CompanionCommandLine.Parse(["--replay", "f", "--speed", "1,5"]);

        // Assert
        Assert.AreEqual(CompanionMode.Invalid, commandLine.Mode, "A decimal comma should be invalid on a German Windows too");
    }

    /// <summary>
    /// Splits a command line text into its arguments.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The arguments</returns>
    private static string[] Args(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    #endregion // Methods
}