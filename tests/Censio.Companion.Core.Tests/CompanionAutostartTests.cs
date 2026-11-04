using System.Security;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionAutostart" />.
/// </summary>
[TestClass]
public sealed class CompanionAutostartTests
{
    #region Constants

    /// <summary>
    /// The name of the autostart entry, written out so a change of <see cref="CompanionAutostart.EntryName" /> fails.
    /// </summary>
    private const string EntryName = "Censio Companion";

    /// <summary>
    /// The text of the warning of a failed read.
    /// </summary>
    private const string ReadFailedMessage = "Could not read the autostart setting";

    /// <summary>
    /// The text of the warning of a failed change.
    /// </summary>
    private const string ChangeFailedMessage = "Could not change the autostart setting";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The full path of the executable under test, fully qualified on every platform.
    /// </summary>
    private static readonly string _exePath = Path.Combine(Path.GetTempPath(), "Censio", "Censio.Companion.exe");

    /// <summary>
    /// The full path of another program.
    /// </summary>
    private static readonly string _otherPath = Path.Combine(Path.GetTempPath(), "Other", "Other.exe");

    /// <summary>
    /// The command the Companion writes for <see cref="_exePath" />: the path in double quotes and nothing else.
    /// </summary>
    private static readonly string _command = $""""
                                               "{_exePath}"
                                               """";

    /// <summary>
    /// The store of the autostart entry.
    /// </summary>
    private readonly FakeStartupEntryStore _store = new();

    /// <summary>
    /// The logger the autostart writes to.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Methods

    /// <summary>
    /// An entry that holds exactly the quoted path of this executable is the Companion's own.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshMatchingEntryReturnsTrue()
    {
        // Arrange
        _store.SetCommand(EntryName, _command);

        var autostart = CreateAutostart(_exePath);

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsTrue(isEnabled, "An entry with the quoted path of this executable should be on");
        Assert.IsTrue(autostart.IsEnabled, "IsEnabled should keep the state of the refresh");
        Assert.AreSequenceEqual(new[] { EntryName }, _store.Reads.ToArray(), "The refresh should read the entry Censio Companion once");
    }

    /// <summary>
    /// Without an entry the autostart is off.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshAbsentEntryReturnsFalse()
    {
        // Arrange
        var autostart = CreateAutostart(_exePath);

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "Without an entry the autostart should be off");
        Assert.IsFalse(autostart.IsEnabled, "IsEnabled should keep the state of the refresh");
        Assert.IsEmpty(_logger.Entries, "An absent entry should log nothing");
    }

    /// <summary>
    /// An entry that starts another program is not the Companion's own.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshOtherPathReturnsFalse()
    {
        // Arrange
        _store.SetCommand(EntryName, $"\"{_otherPath}\"");

        var autostart = CreateAutostart(_exePath);

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "An entry of another program should show off");
        Assert.IsFalse(autostart.IsEnabled, "IsEnabled should keep the state of the refresh");
    }

    /// <summary>
    /// An entry that starts this executable with arguments is not the Companion's own.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshEntryWithArgumentsReturnsFalse()
    {
        // Arrange
        _store.SetCommand(EntryName, _command + " --record");

        var autostart = CreateAutostart(_exePath);

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "An entry with arguments should show off");
        Assert.IsFalse(autostart.IsEnabled, "IsEnabled should keep the state of the refresh");
    }

    /// <summary>
    /// Without a usable executable path the refresh returns off without reading the entry or logging, even when a read would
    /// fail.
    /// </summary>
    /// <param name="exePath">The executable path the Companion got</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Censio.Companion.exe")]
    public void CompanionAutostartRefreshNoExePathReturnsFalse(string? exePath)
    {
        // Arrange
        _store.SetCommand(EntryName, $"\"{exePath}\"");
        _store.ReadException = new IOException("The read should not happen");

        var autostart = CreateAutostart(exePath);

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "Without a usable executable path the autostart should be off");
        Assert.IsEmpty(_store.Reads, "Without a usable executable path the entry should not be read");
        Assert.IsEmpty(_logger.Entries, "Without a usable executable path nothing should be logged");
    }

    /// <summary>
    /// Only the exact form the Companion writes, in any case, matches; every other stored value does not (the guard table of
    /// the plan).
    /// </summary>
    /// <param name="template">
    /// The stored value, with &lt;exe&gt; for the executable path, &lt;EXE&gt; for it in upper case, &lt;other&gt; for
    /// another program's path, or <see langword="null" /> for an absent entry
    /// </param>
    /// <param name="expected">Whether the value matches</param>
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow(@"""<exe>""", true)]
    [DataRow(@"""<EXE>""", true)]
    [DataRow(@"""<exe>"" --record", false)]
    [DataRow(@"""<exe>""x", false)]
    [DataRow(@" ""<exe>""", false)]
    [DataRow(@"""<exe>"" ", false)]
    [DataRow("<exe>", false)]
    [DataRow("<exe> ", false)]
    [DataRow(" <exe>", false)]
    [DataRow(@"""""<exe>""""", false)]
    [DataRow(@"""<exe>", false)]
    [DataRow(@"<exe>""", false)]
    [DataRow(@"""%LOCALAPPDATA%\Censio\Censio.Companion.exe""", false)]
    [DataRow(@"""<other>""", false)]
    public void CompanionAutostartMatchesCases(string? template, bool expected)
    {
        // Arrange
        var command = Expand(template);

        // Act
        var matches = CompanionAutostart.Matches(command, _exePath);

        // Assert
        Assert.AreEqual(expected, matches, $"The stored value [{command}] should match only in the exact quoted form");
    }

    /// <summary>
    /// Clicking while off writes the quoted path of this executable, and nothing else, under the entry's name.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleWhenOffWritesQuotedCommand()
    {
        // Arrange
        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.AreSequenceEqual(new[] { (EntryName, _command) }, _store.Writes.ToArray(), "The click should write the quoted path once under Censio Companion");
        Assert.AreEqual('"', _command[0], "The command should start with a double quote");
        Assert.AreEqual('"', _command[^1], "The command should end with a double quote");
        Assert.AreEqual(_exePath.Length + 2, _command.Length, "The command should hold the path and the two quotes only");
        Assert.AreEqual(_command, _store.GetCommand(EntryName), "The store should hold the quoted path");
        Assert.IsEmpty(_store.Removals, "Turning on should remove nothing");
    }

    /// <summary>
    /// Clicking while an entry of another program holds the name replaces it with this executable's command.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleWhenOffReplacesOtherCommand()
    {
        // Arrange
        _store.SetCommand(EntryName, $"\"{_otherPath}\" --start");

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.AreEqual(_command, _store.GetCommand(EntryName), "The other command should be replaced with the quoted path of this executable");
    }

    /// <summary>
    /// Clicking while on removes the entry and writes nothing.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleWhenOnRemovesEntry()
    {
        // Arrange
        _store.SetCommand(EntryName, _command);

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.AreSequenceEqual(new[] { EntryName }, _store.Removals.ToArray(), "The click should remove Censio Companion once");
        Assert.IsNull(_store.GetCommand(EntryName), "The entry should be gone");
        Assert.IsEmpty(_store.Writes, "Turning off should write nothing");
    }

    /// <summary>
    /// The click acts on the state of the last refresh, not on a fresh read: an entry written by someone else after the
    /// refresh does not stop the click from writing.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleUsesStateOfLastRefresh()
    {
        // Arrange
        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();
        _store.SetCommand(EntryName, _command);

        // Act
        autostart.Toggle();

        // Assert
        Assert.HasCount(1, _store.Writes, "The click after an off refresh should write");
        Assert.IsEmpty(_store.Removals, "The click after an off refresh should not remove");
        Assert.HasCount(1, _store.Reads, "The click should not read the entry again");
    }

    /// <summary>
    /// A fully qualified path with spaces, a comma, an ampersand or non-ASCII letters is written in double quotes; one with a
    /// double quote or a percent sign cannot be used.
    /// </summary>
    /// <param name="folder">The folder of the executable, under the temporary folder</param>
    /// <param name="expected">The expected command, with &lt;path&gt; for the path, or <see langword="null" /></param>
    [TestMethod]
    [DataRow("Censio Companion", @"""<path>""")]
    [DataRow("Censio, 1", @"""<path>""")]
    [DataRow("Censio & Co", @"""<path>""")]
    [DataRow("Zentrale Übersicht ÄÖ", @"""<path>""")]
    [DataRow(@"Censio ""Co""", null)]
    [DataRow("Censio %USERNAME%", null)]
    [DataRow("Censio %", null)]
    public void CompanionAutostartFormatCommandCases(string folder, string? expected)
    {
        // Arrange
        var exePath = Path.Combine(Path.GetTempPath(), folder, "Censio.Companion.exe");

        // Act
        var command = CompanionAutostart.FormatCommand(exePath);

        // Assert
        Assert.AreEqual(expected?.Replace("<path>", exePath, StringComparison.Ordinal), command, $"The path [{exePath}] should be quoted, or refused when it holds a quote or a percent sign");
    }

    /// <summary>
    /// A path that is missing, empty or not fully qualified cannot be used.
    /// </summary>
    /// <param name="exePath">The executable path</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Censio.Companion.exe")]
    [DataRow("Censio/Censio.Companion.exe")]
    public void CompanionAutostartFormatCommandUnqualifiedPathReturnsNull(string? exePath)
    {
        // Arrange
        // (exePath comes from the data row)

        // Act
        var command = CompanionAutostart.FormatCommand(exePath);

        // Assert
        Assert.IsNull(command, $"The path [{exePath}] should be refused");
    }

    /// <summary>
    /// A path of exactly 200 characters is still written.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartFormatCommandPathOf200CharactersIsQuoted()
    {
        // Arrange
        var exePath = CreatePathOfLength(200);

        // Act
        var command = CompanionAutostart.FormatCommand(exePath);

        // Assert
        Assert.AreEqual(200, exePath.Length, "The test path should have 200 characters");
        Assert.AreEqual($"\"{exePath}\"", command, "A path of 200 characters should be quoted");
    }

    /// <summary>
    /// A path of 201 characters cannot be used.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartFormatCommandPathOf201CharactersReturnsNull()
    {
        // Arrange
        var exePath = CreatePathOfLength(201);

        // Act
        var command = CompanionAutostart.FormatCommand(exePath);

        // Assert
        Assert.AreEqual(201, exePath.Length, "The test path should have 201 characters");
        Assert.IsNull(command, "A path longer than 200 characters should be refused");
    }

    /// <summary>
    /// A click with a path that holds a percent sign writes nothing and logs a warning without an exception.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleRefusedPathLogsWarningAndWritesNothing()
    {
        // Arrange
        var exePath = Path.Combine(Path.GetTempPath(), "Censio %USERNAME%", "Censio.Companion.exe");
        var autostart = CreateAutostart(exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.IsEmpty(_store.Writes, "A refused path should write nothing");
        Assert.IsEmpty(_store.Removals, "A refused path should remove nothing");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ChangeFailedMessage, (Exception?)null) }, _logger.Entries.ToArray(), "A refused path should log the change warning without an exception");
    }

    /// <summary>
    /// Constructing the autostart and refreshing it, whatever the entry holds, never writes or removes the entry.
    /// </summary>
    /// <param name="template">The stored value, as in <see cref="CompanionAutostartMatchesCases" /></param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(@"""<exe>""")]
    [DataRow(@"""<other>""")]
    [DataRow(@"""<exe>"" --record")]
    public void CompanionAutostartConstructAndRefreshWriteNothing(string? template)
    {
        // Arrange
        var command = Expand(template);

        _store.SetCommand(EntryName, command);

        // Act
        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();
        autostart.Refresh();

        // Assert
        Assert.IsEmpty(_store.Writes, "Construction and refreshes should write nothing");
        Assert.IsEmpty(_store.Removals, "Construction and refreshes should remove nothing");
        Assert.AreEqual(command, _store.GetCommand(EntryName), "The entry should stay as it was");
    }

    /// <summary>
    /// A read that fails shows off, even after an on refresh, and logs the read warning with the exception.
    /// </summary>
    /// <param name="kind">The kind of the exception: security, unauthorized, invalid-data or io</param>
    [TestMethod]
    [DataRow("security")]
    [DataRow("unauthorized")]
    [DataRow("invalid-data")]
    [DataRow("io")]
    public void CompanionAutostartRefreshReadFailsReturnsFalseAndLogsWarning(string kind)
    {
        // Arrange
        var exception = CreateException(kind);

        _store.SetCommand(EntryName, _command);

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();
        _store.ReadException = exception;

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "A failed read should show off");
        Assert.IsFalse(autostart.IsEnabled, "IsEnabled should be off after a failed read");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ReadFailedMessage, (Exception?)exception) }, _logger.Entries.ToArray(), "A failed read should log the read warning with the exception");
    }

    /// <summary>
    /// A read that keeps failing logs the warning once.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshRepeatedReadFailureLogsOnce()
    {
        // Arrange
        var exception = new SecurityException("Requested registry access is not allowed.");

        _store.ReadException = exception;

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        var isEnabled = autostart.Refresh();

        // Assert
        Assert.IsFalse(isEnabled, "A failed read should show off");
        Assert.HasCount(2, _store.Reads, "Every refresh should try to read");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ReadFailedMessage, (Exception?)exception) }, _logger.Entries.ToArray(), "A repeated failure should log the warning once");
    }

    /// <summary>
    /// A read that fails again after a successful read logs the warning again.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartRefreshFailureAfterSuccessLogsAgain()
    {
        // Arrange
        var first = new IOException("first");
        var second = new IOException("second");
        var autostart = CreateAutostart(_exePath);

        _store.ReadException = first;
        autostart.Refresh();
        _store.ReadException = null;
        autostart.Refresh();
        _store.ReadException = second;

        // Act
        autostart.Refresh();

        // Assert
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ReadFailedMessage, (Exception?)first), (LogLevel.Warning, ReadFailedMessage, (Exception?)second) }, _logger.Entries.ToArray(), "A failure after a successful read should log again");
    }

    /// <summary>
    /// A write that fails does not throw and logs the change warning with the exception, and no enabled line.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleWriteFailsLogsWarning()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access to the registry key is denied.");

        _store.WriteException = exception;

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.HasCount(1, _store.Writes, "The click should have tried to write once");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ChangeFailedMessage, (Exception?)exception) }, _logger.Entries.ToArray(), "A failed write should log the change warning with the exception only");
    }

    /// <summary>
    /// A removal that fails does not throw and logs the change warning with the exception, and no disabled line.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleRemoveFailsLogsWarning()
    {
        // Arrange
        var exception = new SecurityException("Requested registry access is not allowed.");

        _store.SetCommand(EntryName, _command);
        _store.RemoveException = exception;

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.HasCount(1, _store.Removals, "The click should have tried to remove once");
        Assert.AreEqual(_command, _store.GetCommand(EntryName), "The entry should stay after a failed removal");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ChangeFailedMessage, (Exception?)exception) }, _logger.Entries.ToArray(), "A failed removal should log the change warning with the exception only");
    }

    /// <summary>
    /// A click without an executable path writes nothing and logs the change warning without an exception.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleNoExePathLogsWarningAndWritesNothing()
    {
        // Arrange
        var autostart = CreateAutostart(null);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.IsEmpty(_store.Writes, "Without an executable path nothing should be written");
        Assert.IsEmpty(_store.Removals, "Without an executable path nothing should be removed");
        Assert.AreSequenceEqual(new[] { (LogLevel.Warning, ChangeFailedMessage, (Exception?)null) }, _logger.Entries.ToArray(), "Without an executable path the change warning should be logged without an exception");
    }

    /// <summary>
    /// A successful click while off logs Autostart enabled, exactly and without the path.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleLogsEnabledWithoutPath()
    {
        // Arrange
        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.AreSequenceEqual(new[] { (LogLevel.Information, "Autostart enabled", (Exception?)null) }, _logger.Entries.ToArray(), "Turning on should log Autostart enabled only");
        Assert.DoesNotContain(_exePath, _logger.Entries[0].Message, "The line should not hold the executable path");
    }

    /// <summary>
    /// A successful click while on logs Autostart disabled, exactly and without the path.
    /// </summary>
    [TestMethod]
    public void CompanionAutostartToggleLogsDisabledWithoutPath()
    {
        // Arrange
        _store.SetCommand(EntryName, _command);

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        Assert.AreSequenceEqual(new[] { (LogLevel.Information, "Autostart disabled", (Exception?)null) }, _logger.Entries.ToArray(), "Turning off should log Autostart disabled only");
        Assert.DoesNotContain(_exePath, _logger.Entries[0].Message, "The line should not hold the executable path");
    }

    /// <summary>
    /// A click writes or removes only the entry Censio Companion and leaves other programs' entries as they were.
    /// </summary>
    /// <param name="template">The Companion's own entry before the click, as in <see cref="CompanionAutostartMatchesCases" /></param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(@"""<exe>""")]
    public void CompanionAutostartToggleTouchesOnlyItsEntryName(string? template)
    {
        // Arrange
        _store.SetCommand("OneDrive", $"\"{_otherPath}\" /background");
        _store.SetCommand("Censio Companion (old)", _command);
        _store.SetCommand(EntryName, Expand(template));

        var autostart = CreateAutostart(_exePath);

        autostart.Refresh();

        // Act
        autostart.Toggle();

        // Assert
        var touched = _store.Writes.Select(write => write.Name).Concat(_store.Removals).ToArray();

        Assert.AreSequenceEqual(new[] { EntryName }, touched, "The click should write or remove the entry Censio Companion only");
        Assert.AreEqual($"\"{_otherPath}\" /background", _store.GetCommand("OneDrive"), "Another program's entry should stay");
        Assert.AreEqual(_command, _store.GetCommand("Censio Companion (old)"), "An entry of another name should stay");
    }

    /// <summary>
    /// Replaces the placeholders of a stored-value template.
    /// </summary>
    /// <param name="template">The template, or <see langword="null" /></param>
    /// <returns>The stored value, or <see langword="null" /></returns>
    private static string? Expand(string? template)
    {
        return template?.Replace("<exe>", _exePath, StringComparison.Ordinal)
                       .Replace("<EXE>", _exePath.ToUpperInvariant(), StringComparison.Ordinal)
                       .Replace("<other>", _otherPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a fully qualified executable path of an exact length.
    /// </summary>
    /// <param name="length">The length of the path</param>
    /// <returns>The path</returns>
    private static string CreatePathOfLength(int length)
    {
        var root = Path.GetTempPath();
        var fileName = Path.DirectorySeparatorChar + "Censio.Companion.exe";

        return $"{root}{new string('c', length - root.Length - fileName.Length)}{fileName}";
    }

    /// <summary>
    /// Creates an exception a store throws when a read fails.
    /// </summary>
    /// <param name="kind">The kind: security, unauthorized, invalid-data or io</param>
    /// <returns>The exception</returns>
    private static Exception CreateException(string kind)
    {
        return kind switch
               {
                   "security" => new SecurityException("Requested registry access is not allowed."),
                   "unauthorized" => new UnauthorizedAccessException("Access to the registry key is denied."),
                   "invalid-data" => new InvalidDataException("The autostart value is not a string."),
                   _ => new IOException("The autostart value could not be read.")
               };
    }

    /// <summary>
    /// Creates the autostart over the test's store and logger.
    /// </summary>
    /// <param name="exePath">The executable path</param>
    /// <returns>The autostart</returns>
    private CompanionAutostart CreateAutostart(string? exePath)
    {
        return new CompanionAutostart(_store, exePath, _logger);
    }

    #endregion // Methods
}