using System.Globalization;
using System.Reflection;

using Censio.Contracts;
using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="TrayMenu" />.
/// </summary>
[TestClass]
public sealed class TrayMenuTests
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
    /// The menu consists of the version entry, a separator and the exit entry.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateReturnsVersionSeparatorExit()
    {
        // Arrange
        var expectedKinds = new[] { TrayMenuEntryKind.Version, TrayMenuEntryKind.Separator, TrayMenuEntryKind.Exit };
        var expectedTexts = new[] { "Version 1.2.3", string.Empty, "Exit" };

        // Act
        var entries = TrayMenu.Create("1.2.3");

        // Assert
        Assert.HasCount(3, entries, "The menu should have exactly three entries");
        Assert.AreSequenceEqual(expectedKinds, entries.Select(entry => entry.Kind).ToArray(), "The entries should be version, separator, exit in this order");
        Assert.AreSequenceEqual(expectedTexts, entries.Select(entry => entry.Text).ToArray(), "The entry texts should be the version text, empty, Exit");
    }

    /// <summary>
    /// The version entry cannot be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryIsDisabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.IsFalse(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).IsEnabled, "The version entry should be greyed out");
    }

    /// <summary>
    /// The exit entry can be chosen.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateExitEntryIsEnabled()
    {
        // Arrange
        const string version = "1.2.3";

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.IsTrue(entries.Single(entry => entry.Kind == TrayMenuEntryKind.Exit).IsEnabled, "The exit entry should be enabled");
    }

    /// <summary>
    /// The version entry shows the version of the Core assembly.
    /// </summary>
    [TestMethod]
    public void TrayMenuCreateVersionEntryShowsVersion()
    {
        // Arrange
        var version = ProductVersion.Read(typeof(TrayMenu).Assembly);

        // Act
        var entries = TrayMenu.Create(version);

        // Assert
        Assert.AreEqual("Version " + version, entries.Single(entry => entry.Kind == TrayMenuEntryKind.Version).Text, "The version entry should read Version followed by the product version");
    }

    /// <summary>
    /// The tooltip names the application.
    /// </summary>
    [TestMethod]
    public void TrayMenuToolTipIsCensioCompanion()
    {
        // Arrange
        const string expected = "Censio Companion";

        // Act
        var toolTip = TrayMenu.ToolTip;

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip of the tray icon should be Censio Companion");
    }

    /// <summary>
    /// The tooltip shows the application name and the text of the state.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, "Censio Companion: Waiting for game")]
    [DataRow(PipeConnectionState.Connected, "Censio Companion: Connected")]
    [DataRow(PipeConnectionState.Receiving, "Censio Companion: Receiving")]
    [DataRow(PipeConnectionState.PipeBusy, "Censio Companion: Pipe busy")]
    [DataRow(PipeConnectionState.ProtocolMismatch, "Censio Companion: Protocol mismatch")]
    [DataRow(PipeConnectionState.Error, "Censio Companion: Error")]
    public void TrayMenuFormatToolTipReturnsTextPerState(PipeConnectionState state, string expected)
    {
        // Arrange
        // (state and expected come from the data row)

        // Act
        var toolTip = TrayMenu.FormatToolTip(state);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should be the application name, a colon and the state text");
    }

    /// <summary>
    /// An undefined state is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipUndefinedStateThrows()
    {
        // Arrange
        var state = (PipeConnectionState)99;
        var call = () => TrayMenu.FormatToolTip(state);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined state should be rejected");

        // Assert
        Assert.AreEqual("state", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The tooltip appends the recording suffix to the state text of the connection.
    /// </summary>
    /// <param name="state">The connection state</param>
    /// <param name="recording">The recording state</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Off, "Censio Companion: Waiting for game")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Off, "Censio Companion: Receiving")]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Recording, "Censio Companion: Waiting for game (recording)")]
    [DataRow(PipeConnectionState.Connected, PipeRecordingState.Recording, "Censio Companion: Connected (recording)")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Recording, "Censio Companion: Receiving (recording)")]
    [DataRow(PipeConnectionState.PipeBusy, PipeRecordingState.Recording, "Censio Companion: Pipe busy (recording)")]
    [DataRow(PipeConnectionState.ProtocolMismatch, PipeRecordingState.Recording, "Censio Companion: Protocol mismatch (recording)")]
    [DataRow(PipeConnectionState.Error, PipeRecordingState.Recording, "Censio Companion: Error (recording)")]
    [DataRow(PipeConnectionState.WaitingForGame, PipeRecordingState.Failed, "Censio Companion: Waiting for game (recording failed)")]
    [DataRow(PipeConnectionState.Receiving, PipeRecordingState.Failed, "Censio Companion: Receiving (recording failed)")]
    [DataRow(PipeConnectionState.Error, PipeRecordingState.Failed, "Censio Companion: Error (recording failed)")]
    public void TrayMenuFormatToolTipWithRecordingStateAppendsSuffix(PipeConnectionState state, PipeRecordingState recording, string expected)
    {
        // Arrange
        // (state, recording and expected come from the data row)

        // Act
        var toolTip = TrayMenu.FormatToolTip(state, recording);

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should carry the suffix of the recording state");
    }

    /// <summary>
    /// The replay tooltip shows the state of the replayed connection until the replay finished.
    /// </summary>
    /// <param name="state">The state of the replayed connection</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeConnectionState.Connected, false, "Censio Companion: Replay: Connected")]
    [DataRow(PipeConnectionState.Receiving, false, "Censio Companion: Replay: Receiving")]
    [DataRow(PipeConnectionState.ProtocolMismatch, false, "Censio Companion: Replay: Protocol mismatch")]
    [DataRow(PipeConnectionState.Connected, true, "Censio Companion: Replay finished")]
    [DataRow(PipeConnectionState.Receiving, true, "Censio Companion: Replay finished")]
    [DataRow(PipeConnectionState.ProtocolMismatch, true, "Censio Companion: Replay finished")]
    public void TrayMenuFormatReplayToolTipReturnsTextPerStatus(PipeConnectionState state, bool isFinished, string expected)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = state,
                         IsFinished = isFinished
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(status);

        // Assert
        Assert.AreEqual(expected, toolTip, "The replay tooltip should name the state, or the end of the replay");
    }

    /// <summary>
    /// The replay tooltip names the problem that ended the replay, which wins over the end of the replay.
    /// </summary>
    /// <param name="kind">The kind of the problem</param>
    /// <param name="lineNumber">The line number of the problem, if any</param>
    /// <param name="isFinished">Whether the replay finished</param>
    /// <param name="expected">The expected tooltip</param>
    [TestMethod]
    [DataRow(PipeReplayProblemKind.FileNotFound, null, true, "Censio Companion: Replay error: file not found")]
    [DataRow(PipeReplayProblemKind.NotARecording, null, true, "Censio Companion: Replay error: not a recording")]
    [DataRow(PipeReplayProblemKind.UnsupportedVersion, null, true, "Censio Companion: Replay error: unsupported version")]
    [DataRow(PipeReplayProblemKind.ReadFailed, null, true, "Censio Companion: Replay error: read failed")]
    [DataRow(PipeReplayProblemKind.InvalidLine, 7L, true, "Censio Companion: Replay error: invalid line 7")]
    [DataRow(PipeReplayProblemKind.InvalidLine, 1234567L, true, "Censio Companion: Replay error: invalid line 1234567")]
    [DataRow(PipeReplayProblemKind.InvalidLine, long.MaxValue, true, "Censio Companion: Replay error: invalid line 9223372036854775807")]
    [DataRow(PipeReplayProblemKind.FileNotFound, null, false, "Censio Companion: Replay error: file not found")]
    public void TrayMenuFormatReplayToolTipProblemReturnsErrorText(PipeReplayProblemKind kind, long? lineNumber, bool isFinished, string expected)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = isFinished,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = kind,
                                       LineNumber = lineNumber
                                   }
                     };

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(status);

        // Assert
        Assert.AreEqual(expected, toolTip, "The replay tooltip should name the reason of the problem");
    }

    /// <summary>
    /// The line number in the tooltip has no digit grouping under any culture.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatReplayToolTipInvalidLineIgnoresCulture()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Receiving,
                         IsFinished = true,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = PipeReplayProblemKind.InvalidLine,
                                       LineNumber = 1234567
                                   }
                     };

        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        // Act
        var toolTip = TrayMenu.FormatReplayToolTip(status);

        // Assert
        Assert.AreEqual("Censio Companion: Replay error: invalid line 1234567", toolTip, "The line number should be written without grouping");
    }

    /// <summary>
    /// The tooltip of an invalid command line has the exact text.
    /// </summary>
    [TestMethod]
    public void TrayMenuInvalidCommandLineToolTipHasExactText()
    {
        // Arrange
        const string expected = "Censio Companion: Invalid command line";

        // Act
        var toolTip = TrayMenu.InvalidCommandLineToolTip;

        // Assert
        Assert.AreEqual(expected, toolTip, "The tooltip should name the application and the invalid command line");
    }

    /// <summary>
    /// An undefined recording state is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatToolTipUndefinedRecordingStateThrows()
    {
        // Arrange
        var call = () => TrayMenu.FormatToolTip(PipeConnectionState.Connected, (PipeRecordingState)99);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined recording state should be rejected");

        // Assert
        Assert.AreEqual("recording", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// An undefined problem kind is rejected.
    /// </summary>
    [TestMethod]
    public void TrayMenuFormatReplayToolTipUndefinedProblemKindThrows()
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = PipeConnectionState.Connected,
                         Problem = new PipeReplayProblem
                                   {
                                       Kind = (PipeReplayProblemKind)99
                                   }
                     };
        var call = () => TrayMenu.FormatReplayToolTip(status);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "An undefined problem kind should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// A state a replay cannot be in is rejected.
    /// </summary>
    /// <param name="state">The state</param>
    [TestMethod]
    [DataRow(PipeConnectionState.WaitingForGame)]
    [DataRow(PipeConnectionState.Error)]
    [DataRow((PipeConnectionState)99)]
    public void TrayMenuFormatReplayToolTipUnreachableStateThrows(PipeConnectionState state)
    {
        // Arrange
        var status = new PipeReplayStatus
                     {
                         State = state
                     };
        var call = () => TrayMenu.FormatReplayToolTip(status);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "A state a replay cannot reach should be rejected");

        // Assert
        Assert.AreEqual("status", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The Core assembly carries the build version of the solution.
    /// </summary>
    [TestMethod]
    public void TrayMenuCoreAssemblyCarriesBuildVersion()
    {
        // Arrange
        var assembly = typeof(TrayMenu).Assembly;
        var expected = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                               .Single(attribute => attribute.Key == "CensioBuildVersion")
                               .Value;

        // Act
        var actual = ProductVersion.Read(assembly);

        // Assert
        Assert.AreEqual(expected, actual, "The Core assembly should carry the build version as its informational version");
    }

    #endregion // Methods
}