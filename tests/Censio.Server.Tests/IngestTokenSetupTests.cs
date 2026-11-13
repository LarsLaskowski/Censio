using System.Buffers.Text;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestTokenSetup" />.
/// </summary>
[TestClass]
public sealed class IngestTokenSetupTests
{
    #region Constants

    /// <summary>
    /// The name of the temporary file the token is written to before it is moved into place.
    /// </summary>
    private const string TemporaryFileName = "ingest-token.tmp";

    /// <summary>
    /// The warning of a switched-off token (spec S6).
    /// </summary>
    private const string SwitchedOffText = "The ingest token is switched off: anyone who reaches the server can send data.";

    /// <summary>
    /// The warning without a data directory (spec E11).
    /// </summary>
    private const string NoDataDirectoryText = "No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.";

    /// <summary>
    /// The critical line of an invalid switch (spec E12).
    /// </summary>
    private const string InvalidSwitchText = "The setting Censio:Ingest:RequireToken must be true or false; the server stops.";

    /// <summary>
    /// The information line of a token read from the configured file (spec S1 of #176).
    /// </summary>
    private const string TokenFileReadText = "The ingest token is read from the file named by Censio:Ingest:TokenFile.";

    /// <summary>
    /// The critical line of a configured file that cannot be read (spec E1 of #176).
    /// </summary>
    private const string TokenFileNotReadableText = "The ingest token file named by Censio:Ingest:TokenFile cannot be read; the server stops.";

    /// <summary>
    /// The critical line of a configured file that holds no valid token (spec E2 of #176).
    /// </summary>
    private const string TokenFileInvalidText = "The ingest token file named by Censio:Ingest:TokenFile holds no valid token (32 to 1,024 visible ASCII characters); the server stops.";

    /// <summary>
    /// The critical line of a setting that is not an absolute path (spec E3 of #176).
    /// </summary>
    private const string TokenFileNotAbsoluteText = "The setting Censio:Ingest:TokenFile must be an absolute path; the server stops.";

    /// <summary>
    /// The critical line of the setting combined with the switch set to false (spec E4 of #176).
    /// </summary>
    private const string TokenFileWithSwitchOffText = "The settings Censio:Ingest:TokenFile and Censio:Ingest:RequireToken false exclude each other; the server stops.";

    /// <summary>
    /// The text every token of the configured-file tests contains, which no log entry may hold.
    /// </summary>
    private const string TokenMarker = "MARKER";

    /// <summary>
    /// The text the directory of every configured file contains, which no log entry may hold.
    /// </summary>
    private const string PathMarker = "PATHMARK";

    /// <summary>
    /// A valid token of 44 characters for the configured file.
    /// </summary>
    private const string ConfiguredToken = "MARKER-configured-0123456789abcdefghijABCDEF";

    /// <summary>
    /// A valid token of 44 characters for the file in the data directory.
    /// </summary>
    private const string DataDirectoryToken = "MARKER-datadirectory-0123456789abcdefghijABC";

    /// <summary>
    /// The case of a configured file that does not exist.
    /// </summary>
    private const string MissingFileCase = "missing file";

    /// <summary>
    /// The case of a configured path that is a directory.
    /// </summary>
    private const string DirectoryCase = "a directory";

    /// <summary>
    /// The case of a configured path that is a symbolic link to a missing file.
    /// </summary>
    private const string LinkToMissingFileCase = "a symbolic link to a missing file";

    /// <summary>
    /// The case of a configured file another stream holds with <see cref="FileShare.None" />.
    /// </summary>
    private const string HeldFileCase = "a file held exclusively";

    /// <summary>
    /// The case of a fully qualified configured path that contains a NUL character.
    /// </summary>
    private const string NulCharacterCase = "a path containing a NUL character";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The pattern of a generated token: 43 characters of the Base64Url alphabet.
    /// </summary>
    private static readonly Regex _generatedToken = new("^[A-Za-z0-9_-]{43}$", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The temporary paths of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryPaths = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary paths of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in _temporaryPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// The first start with a data directory generates a token of 43 characters into the token file, logs the S1 line and
    /// accepts that token.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void IngestTokenSetupTryCreateAccessWithoutFileGeneratesTokenFile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(File.Exists(tokenFile), "The token file should be created");

        var token = File.ReadAllText(tokenFile);

        Assert.IsTrue(_generatedToken.IsMatch(token), "The file should hold exactly a 43-character Base64Url token");
        Assert.IsTrue(access.IsTokenRequired, "The access should require a token");
        Assert.IsTrue(access.IsTokenAccepted(token), "The access should accept the generated token");
        Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, TemporaryFileName)), "No temporary file should be left");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Information, logger.Entries[0].Level, "The entry should be information");
        Assert.AreEqual($"An ingest token was generated and stored in '{tokenFile}'.", logger.Entries[0].Message, "The entry should carry the exact S1 text");
    }

    /// <summary>
    /// A later start reads the token of the file unchanged, white space around it trimmed, and logs the S2 line.
    /// </summary>
    /// <param name="before">The white space written before the token</param>
    /// <param name="after">The white space written after the token</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC6")]
    [DataRow("", "")]
    [DataRow(" \t", "\r\n")]
    [DataRow("\n", " \t\r")]
    public void IngestTokenSetupTryCreateAccessWithFileReadsSameToken(string before, string after)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var configuration = CreateConfiguration(dataDirectory, null);

        IngestTokenSetup.TryCreateAccess(configuration, new RecordingLogger());

        var token = File.ReadAllText(tokenFile);

        File.WriteAllText(tokenFile, $"{before}{token}{after}");

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(access.IsTokenAccepted(token), "The access should accept the token of the file");
        Assert.AreEqual($"{before}{token}{after}", File.ReadAllText(tokenFile), "The token file should stay unchanged");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Information, logger.Entries[0].Level, "The entry should be information");
        Assert.AreEqual($"The ingest token is read from '{tokenFile}'.", logger.Entries[0].Message, "The entry should carry the exact S2 text");
    }

    /// <summary>
    /// After the token file is deleted the next start generates a new, different token, and the old one is refused.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void IngestTokenSetupTryCreateAccessAfterFileDeletedGeneratesDifferentToken()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var configuration = CreateConfiguration(dataDirectory, null);

        IngestTokenSetup.TryCreateAccess(configuration, new RecordingLogger());

        var oldToken = File.ReadAllText(tokenFile);

        File.Delete(tokenFile);

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(File.Exists(tokenFile), "A new token file should be created");

        var newToken = File.ReadAllText(tokenFile);

        Assert.IsTrue(_generatedToken.IsMatch(newToken), "The new file should hold a 43-character Base64Url token");
        Assert.AreNotEqual(oldToken, newToken, "The new token should differ from the old one");
        Assert.IsTrue(access.IsTokenAccepted(newToken), "The new token should be accepted");
        Assert.IsFalse(access.IsTokenAccepted(oldToken), "The old token should be refused");
        Assert.AreEqual($"An ingest token was generated and stored in '{tokenFile}'.", logger.Entries[0].Message, "The S1 line should be logged again");
    }

    /// <summary>
    /// Neither the generating nor the reading start writes the token into a log message or an attached exception.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC4")]
    public void IngestTokenSetupTryCreateAccessLogsNoTokenText()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var configuration = CreateConfiguration(dataDirectory, null);
        var logger = new RecordingLogger();

        IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Act
        IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        var token = File.ReadAllText(tokenFile);
        var texts = logger.Entries.Select(entry => $"{entry.Message} {entry.Exception}").ToList();

        Assert.HasCount(2, logger.Entries, "Both starts should log one line each");
        Assert.DoesNotContain(text => text.Contains(token, StringComparison.Ordinal), texts, "No log line should hold the token");
    }

    /// <summary>
    /// The generated token file is readable and writable by its owner only on Linux.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public void IngestTokenSetupTryCreateAccessCreatesOwnerOnlyFile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        // Act
        IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), new RecordingLogger());

        // Assert
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(tokenFile), "The token file should be readable and writable by its owner only");
    }

    /// <summary>
    /// A generated token is 43 characters of Base64Url without padding, decodes to 32 bytes and differs per call.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void IngestTokenSetupGenerateTokenReturnsBase64UrlOf43Characters()
    {
        // Arrange
        var first = IngestTokenSetup.GenerateToken();

        // Act
        var second = IngestTokenSetup.GenerateToken();

        // Assert
        Assert.IsTrue(_generatedToken.IsMatch(second), "The token should be 43 characters of the Base64Url alphabet");
        Assert.HasCount(32, Base64Url.DecodeFromChars(second), "The token should decode to 32 bytes");
        Assert.AreNotEqual(first, second, "Two tokens should differ");
    }

    /// <summary>
    /// The switch set to false, in any case, logs the S6 warning and returns the open access.
    /// </summary>
    /// <param name="value">The value of the switch</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("false")]
    [DataRow("FALSE")]
    [DataRow("False")]
    public void IngestTokenSetupTryCreateAccessRequireTokenFalseReturnsOpenAccess(string value)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, value), logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsFalse(access.IsTokenRequired, "The access should require no token");
        Assert.IsTrue(access.IsTokenAccepted("any token"), "The access should accept any token");
        Assert.IsTrue(access.IsTokenAccepted(null), "The access should accept no token");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual(SwitchedOffText, logger.Entries[0].Message, "The entry should carry the exact S6 text");
    }

    /// <summary>
    /// The switch set to false neither creates a token file nor reads an existing one, an invalid one included.
    /// </summary>
    /// <param name="content">The content of a token file written before the start, or <c>null</c> for no file</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a valid token")]
    public void IngestTokenSetupTryCreateAccessRequireTokenFalseTouchesNoFile(string? content)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        WriteTokenFileIfAny(tokenFile, content);

        var before = Directory.GetFileSystemEntries(dataDirectory).Order().ToList();
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, "false"), logger);

        // Assert
        var after = Directory.GetFileSystemEntries(dataDirectory).Order().ToList();

        Assert.IsNotNull(access, "The start should go on, whatever the file holds");
        Assert.AreSequenceEqual(before, after, "The data directory should stay as it was");
        Assert.DoesNotContain(entry => entry.Level == LogLevel.Critical, logger.Entries, "No critical line should be logged");
    }

    /// <summary>
    /// The switch unset or true, in any case, requires a token: the token file is generated and other tokens are refused.
    /// </summary>
    /// <param name="value">The value of the switch, or <c>null</c> for unset</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(null)]
    [DataRow("true")]
    [DataRow("TRUE")]
    [DataRow("True")]
    public void IngestTokenSetupTryCreateAccessRequireTokenTrueOrUnsetRequiresToken(string? value)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, value), new RecordingLogger());

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(access.IsTokenRequired, "The access should require a token");
        Assert.IsTrue(File.Exists(tokenFile), "The token file should be created");
        Assert.IsFalse(access.IsTokenAccepted("wrong token"), "Another token should be refused");
        Assert.IsFalse(access.IsTokenAccepted(null), "No token should be refused");
    }

    /// <summary>
    /// A switch value other than true or false, in any case, stops the start with one critical line naming the setting.
    /// </summary>
    /// <param name="value">The value of the switch</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow("")]
    [DataRow("no")]
    [DataRow("0")]
    [DataRow(" true")]
    [DataRow("false ")]
    public void IngestTokenSetupTryCreateAccessInvalidRequireTokenStops(string value)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, value), logger);

        // Assert
        Assert.IsNull(access, $"The start should stop: '{value}'");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual(InvalidSwitchText, logger.Entries[0].Message, "The entry should carry the exact E12 text");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), "No token file should be created");
    }

    /// <summary>
    /// A token file that holds no valid token stops the start with one critical line without its content, and stays as it was.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="content">The content of the file, each character written as one byte</param>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC6")]
    [DataRow("empty", "")]
    [DataRow("white space only", " \t\r\n ")]
    [DataRow("31 characters", "MARKER0123456789abcdefghijABCDE")]
    [DataRow("1,025 characters", null)]
    [DataRow("a space inside", "MARKER MARKER")]
    [DataRow("a byte above tilde", "MARKER\u007FMARKER")]
    [DataRow("a byte above ASCII", "MARKERäMARKER")]
    public void IngestTokenSetupTryCreateAccessInvalidFileStops(string label, string? content)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var bytes = Encoding.Latin1.GetBytes(content ?? new string('M', IngestTokenSetup.MaxTokenLength + 1));

        File.WriteAllBytes(tokenFile, bytes);

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNull(access, $"The start should stop: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, $"The entry should be critical: {label}");
        Assert.AreEqual($"The ingest token file '{tokenFile}' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.", logger.Entries[0].Message, $"The entry should carry the exact E10 text: {label}");
        Assert.DoesNotContain("MARKER", $"{logger.Entries[0].Message} {logger.Entries[0].Exception}", $"The entry should hold no file content: {label}");
        Assert.AreSequenceEqual(bytes, File.ReadAllBytes(tokenFile), $"The token file should stay as it was: {label}");
    }

    /// <summary>
    /// A token at the bounds of E10 is read: 1,024 characters, a file of 65,536 bytes, and 32 characters starting with the
    /// first and ending with the last visible ASCII character.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="token">The token, or <c>null</c> for 1,024 characters</param>
    /// <param name="fileLength">The length of the file in bytes, the token padded with trailing spaces</param>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC6")]
    [DataRow("1,024 characters", null, 1024)]
    [DataRow("1,024 characters in 65,536 bytes", null, 65536)]
    [DataRow("32 characters from the first to the last visible character", "!0123456789abcdefghijABCDEFGHIJ~", 32)]
    public void IngestTokenSetupTryCreateAccessFileAtBoundsReadsToken(string label, string? token, int fileLength)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var expectedToken = token ?? new string('M', IngestTokenSetup.MaxTokenLength);

        File.WriteAllBytes(tokenFile, Encoding.ASCII.GetBytes(expectedToken.PadRight(fileLength)));

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.AreEqual(fileLength, new FileInfo(tokenFile).Length, $"The file should have the length of its row: {label}");
        Assert.IsNotNull(access, $"The start should go on: {label}");
        Assert.IsTrue(access.IsTokenRequired, $"The access should require a token: {label}");
        Assert.IsTrue(access.IsTokenAccepted(expectedToken), $"The access should accept the token of the file: {label}");
        Assert.IsFalse(access.IsTokenAccepted(expectedToken[..^1]), $"The access should refuse a shorter token: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Information, logger.Entries[0].Level, $"The entry should be information: {label}");
        Assert.AreEqual($"The ingest token is read from '{tokenFile}'.", logger.Entries[0].Message, $"The entry should carry the exact S2 text: {label}");
    }

    /// <summary>
    /// A token file of 65,537 bytes stops the start, even when its content trims to a valid token.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC6")]
    public void IngestTokenSetupTryCreateAccessFileAboveSizeBoundStops()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var bytes = Encoding.ASCII.GetBytes("MARKER".PadRight(65537));

        File.WriteAllBytes(tokenFile, bytes);

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNull(access, "The start should stop");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The ingest token file '{tokenFile}' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.", logger.Entries[0].Message, "The entry should carry the exact E10 text");
        Assert.DoesNotContain("MARKER", $"{logger.Entries[0].Message} {logger.Entries[0].Exception}", "The entry should hold no file content");
        Assert.AreSequenceEqual(bytes, File.ReadAllBytes(tokenFile), "The token file should stay as it was");
    }

    /// <summary>
    /// A token file that cannot be read (another process holds it exclusively) stops the start with one critical line that
    /// carries the exception and no token text.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void IngestTokenSetupTryCreateAccessUnreadableFileStops()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        File.WriteAllText(tokenFile, "MARKERTOKEN");

        var logger = new RecordingLogger();
        IngestAccess? access;

        // Act
        using (new FileStream(tokenFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);
        }

        // Assert
        Assert.IsNull(access, "The start should stop");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The ingest token file '{tokenFile}' cannot be read; the server stops.", logger.Entries[0].Message, "The entry should carry the exact E10 text");
        Assert.IsNotNull(logger.Entries[0].Exception, "The entry should carry the exception");
        Assert.DoesNotContain("MARKER", $"{logger.Entries[0].Message} {logger.Entries[0].Exception}", "The entry should hold no file content");
        Assert.AreEqual("MARKERTOKEN", File.ReadAllText(tokenFile), "The token file should stay as it was");
    }

    /// <summary>
    /// A temporary file that cannot be written or deleted (its path is taken by a directory) stops the start with one
    /// critical line that carries the exception, and no token file is created.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void IngestTokenSetupTryCreateAccessTemporaryPathTakenStops()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        Directory.CreateDirectory(Path.Combine(dataDirectory, TemporaryFileName));

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNull(access, "The start should stop");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The ingest token file '{tokenFile}' cannot be created; the server stops. Make the data directory writable for the user the server runs as.", logger.Entries[0].Message, "The entry should carry the exact E10 text");
        Assert.IsNotNull(logger.Entries[0].Exception, "The entry should carry the exception");
        Assert.IsFalse(File.Exists(tokenFile), "No token file should be created");
    }

    /// <summary>
    /// A token file that cannot be created stops the start with one critical line carrying the exception, and leaves
    /// neither a token file nor its temporary file behind.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void IngestTokenSetupTryCreateAccessUnwritableDirectoryStops()
    {
        // Arrange
        var blocker = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        File.WriteAllText(blocker, "not a directory");
        _temporaryPaths.Add(blocker);

        var dataDirectory = Path.Combine(blocker, "data");
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNull(access, "The start should stop");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The ingest token file '{tokenFile}' cannot be created; the server stops. Make the data directory writable for the user the server runs as.", logger.Entries[0].Message, "The entry should carry the exact E10 text");
        Assert.IsNotNull(logger.Entries[0].Exception, "The entry should carry the exception");
        Assert.IsFalse(File.Exists(tokenFile), "No token file should be left");
        Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, TemporaryFileName)), "No temporary file should be left");
    }

    /// <summary>
    /// A temporary file that cannot be moved into place (the token path is taken by a directory) stops the start with one
    /// critical line, and the temporary file is deleted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void IngestTokenSetupTryCreateAccessFailedMoveLeavesNoTemporaryFile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName);

        Directory.CreateDirectory(tokenFile);

        var logger = new RecordingLogger();
        string[] expectedTexts = [
                                     $"The ingest token file '{tokenFile}' cannot be created; the server stops. Make the data directory writable for the user the server runs as.",
                                     $"The ingest token file '{tokenFile}' cannot be read; the server stops."
                                 ];

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null), logger);

        // Assert
        Assert.IsNull(access, "The start should stop");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.Contains(logger.Entries[0].Message, expectedTexts, "The entry should carry the exact E10 text of a file that cannot be created or read");
        Assert.IsFalse(File.Exists(Path.Combine(dataDirectory, TemporaryFileName)), "No temporary file should be left");
        Assert.IsEmpty(Directory.GetFileSystemEntries(tokenFile), "Nothing should be written into the directory in the token's place");
    }

    /// <summary>
    /// Without a data directory a required token cannot exist: every token is refused and the E11 warning is logged.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void IngestTokenSetupTryCreateAccessWithoutDataDirectoryRefusesAll()
    {
        // Arrange
        var configuration = CreateConfiguration(null, null);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(access.IsTokenRequired, "The access should require a token");
        Assert.IsFalse(access.IsTokenAccepted("any token"), "Any token should be refused");
        Assert.IsFalse(access.IsTokenAccepted(null), "No token should be refused");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level, "The entry should be a warning");
        Assert.AreEqual(NoDataDirectoryText, logger.Entries[0].Message, "The entry should carry the exact E11 text");
    }

    /// <summary>
    /// With the token file setting the configured token is read and accepted, any other token is refused, and the file in
    /// the data directory is neither read, created, changed nor deleted, with and without a data directory.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="passDataDirectory">Whether the data directory is configured</param>
    /// <param name="dataDirectoryContent">The content of the token file in the data directory, or <c>null</c> for no file</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC7")]
    [DataRow("data directory with a different ingest-token", true, DataDirectoryToken)]
    [DataRow("data directory without the file", true, null)]
    [DataRow("no data directory", false, null)]
    public void IngestTokenSetupTryCreateAccessWithTokenFileReadsConfiguredToken(string label, bool passDataDirectory, string? dataDirectoryContent)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = CreateConfiguredTokenFile(Encoding.ASCII.GetBytes(ConfiguredToken));

        WriteTokenFileIfAny(Path.Combine(dataDirectory, IngestTokenSetup.TokenFileName), dataDirectoryContent);

        var before = ReadDirectoryState(dataDirectory);
        var configuration = CreateConfiguration(passDataDirectory ? dataDirectory : null, null, tokenFile);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        Assert.IsNotNull(access, $"The start should go on: {label}");
        Assert.IsTrue(access.IsTokenRequired, $"The access should require a token: {label}");
        Assert.IsTrue(access.IsTokenAccepted(ConfiguredToken), $"The access should accept the configured token: {label}");
        Assert.IsFalse(access.IsTokenAccepted(DataDirectoryToken), $"The access should refuse the token of the data directory: {label}");
        Assert.IsFalse(access.IsTokenAccepted(ConfiguredToken[..^1]), $"The access should refuse another token: {label}");
        Assert.IsFalse(access.IsTokenAccepted(null), $"The access should refuse no token: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Information, logger.Entries[0].Level, $"The entry should be information: {label}");
        Assert.AreEqual(TokenFileReadText, logger.Entries[0].Message, $"The entry should carry the exact S1 text: {label}");
        Assert.AreSequenceEqual(before, ReadDirectoryState(dataDirectory), $"The data directory should stay as it was: {label}");
        Assert.AreEqual(ConfiguredToken, File.ReadAllText(tokenFile), $"The configured file should stay as it was: {label}");
        AssertNoMarker(logger, label);
    }

    /// <summary>
    /// The configured file is read at start only: after the file changed, the next start accepts the new token only.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC7")]
    public void IngestTokenSetupTryCreateAccessWithTokenFileChangedReadsNewToken()
    {
        // Arrange
        const string newToken = "MARKER-new-token-0123456789abcdefghijABCDEF";
        var tokenFile = CreateConfiguredTokenFile(Encoding.ASCII.GetBytes(ConfiguredToken));
        var configuration = CreateConfiguration(null, null, tokenFile);

        IngestTokenSetup.TryCreateAccess(configuration, new RecordingLogger());
        File.WriteAllText(tokenFile, newToken);

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(configuration, logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(access.IsTokenAccepted(newToken), "The access should accept the new token");
        Assert.IsFalse(access.IsTokenAccepted(ConfiguredToken), "The access should refuse the old token");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(TokenFileReadText, logger.Entries[0].Message, "The entry should carry the exact S1 text");
        AssertNoMarker(logger, "changed file");
    }

    /// <summary>
    /// A configured file that does not exist or cannot be read stops the start with the E1 line, which carries no
    /// exception, and nothing is created at the configured path.
    /// </summary>
    /// <param name="label">The case, which names how the configured path is arranged</param>
    [TestMethod]
    [TestCategory("AC2")]
    [TestCategory("AC7")]
    [DataRow(MissingFileCase)]
    [DataRow(DirectoryCase)]
    [DataRow(LinkToMissingFileCase)]
    [DataRow(HeldFileCase)]
    [DataRow(NulCharacterCase)]
    public void IngestTokenSetupTryCreateAccessTokenFileNotReadableStops(string label)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var (tokenFile, holder) = ArrangeUnreadableTokenFile(label);

        var tokenDirectory = Path.GetDirectoryName(tokenFile)!;
        var before = Directory.GetFileSystemEntries(tokenDirectory).Order().ToList();
        var logger = new RecordingLogger();
        IngestAccess? access;

        // Act
        using (holder)
        {
            access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null, tokenFile), logger);
        }

        // Assert
        Assert.IsNull(access, $"The start should stop: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, $"The entry should be critical: {label}");
        Assert.AreEqual(TokenFileNotReadableText, logger.Entries[0].Message, $"The entry should carry the exact E1 text: {label}");
        Assert.IsNull(logger.Entries[0].Exception, $"The entry should carry no exception: {label}");
        Assert.AreSequenceEqual(before, Directory.GetFileSystemEntries(tokenDirectory).Order().ToList(), $"Nothing should be created at the configured path: {label}");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), $"No token file should be created in the data directory: {label}");
        AssertNoMarker(logger, label);
    }

    /// <summary>
    /// A configured file whose content breaks the rule of E2 stops the start with the E2 line and stays as it was.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="content">The start of the content, each character written as one byte</param>
    /// <param name="padding">The character the content is padded with</param>
    /// <param name="fileLength">The length of the file in bytes</param>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC7")]
    [DataRow("empty", "", ' ', 0)]
    [DataRow("white space only", " \t\r\n ", ' ', 5)]
    [DataRow("31 characters", "MARKER", 'M', 31)]
    [DataRow("1,025 characters", "MARKER", 'M', 1025)]
    [DataRow("a space inside", "MARKER MARKER", 'M', 40)]
    [DataRow("a byte above tilde", "MARKER\u007FMARKER", 'M', 40)]
    [DataRow("a byte above ASCII", "MARKERäMARKER", 'M', 40)]
    [DataRow("65,537 bytes", "MARKER012345678901234567890123456789abcd", ' ', 65537)]
    public void IngestTokenSetupTryCreateAccessTokenFileInvalidStops(string label, string content, char padding, int fileLength)
    {
        // Arrange
        var bytes = Encoding.Latin1.GetBytes(content.PadRight(fileLength, padding));
        var tokenFile = CreateConfiguredTokenFile(bytes);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(null, null, tokenFile), logger);

        // Assert
        Assert.HasCount(fileLength, bytes, $"The file should have the length of its row: {label}");
        Assert.IsNull(access, $"The start should stop: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, $"The entry should be critical: {label}");
        Assert.AreEqual(TokenFileInvalidText, logger.Entries[0].Message, $"The entry should carry the exact E2 text: {label}");
        Assert.IsNull(logger.Entries[0].Exception, $"The entry should carry no exception: {label}");
        Assert.AreSequenceEqual(bytes, File.ReadAllBytes(tokenFile), $"The configured file should stay as it was: {label}");
        AssertNoMarker(logger, label);
    }

    /// <summary>
    /// A configured token at the bounds of E2 is read: 32 characters from the first to the last visible ASCII character,
    /// 1,024 characters, a token and a line feed, and 1,024 characters in a file of 65,536 bytes.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="head">The start of the token</param>
    /// <param name="tail">The end of the token</param>
    /// <param name="tokenLength">The length of the token, padded with <c>M</c> between head and tail</param>
    /// <param name="suffix">The white space written after the token</param>
    /// <param name="fileLength">The length of the file in bytes, padded with trailing spaces</param>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC7")]
    [DataRow("32 characters from the first to the last visible character", "!MARKER", "~", 32, "", 32)]
    [DataRow("1,024 characters", "MARKER", "", 1024, "", 1024)]
    [DataRow("a token and a line feed", "MARKER", "", 44, "\n", 45)]
    [DataRow("1,024 characters in 65,536 bytes", "MARKER", "", 1024, "", 65536)]
    public void IngestTokenSetupTryCreateAccessTokenFileAtBoundsReadsToken(string label, string head, string tail, int tokenLength, string suffix, int fileLength)
    {
        // Arrange
        var token = head.PadRight(tokenLength - tail.Length, 'M') + tail;
        var bytes = Encoding.ASCII.GetBytes((token + suffix).PadRight(fileLength));
        var tokenFile = CreateConfiguredTokenFile(bytes);
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(null, null, tokenFile), logger);

        // Assert
        Assert.HasCount(tokenLength, token, $"The token should have the length of its row: {label}");
        Assert.HasCount(fileLength, bytes, $"The file should have the length of its row: {label}");
        Assert.IsNotNull(access, $"The start should go on: {label}");
        Assert.IsTrue(access.IsTokenRequired, $"The access should require a token: {label}");
        Assert.IsTrue(access.IsTokenAccepted(token), $"The access should accept the configured token: {label}");
        Assert.IsFalse(access.IsTokenAccepted(token[..^1]), $"The access should refuse a shorter token: {label}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {label}");
        Assert.AreEqual(LogLevel.Information, logger.Entries[0].Level, $"The entry should be information: {label}");
        Assert.AreEqual(TokenFileReadText, logger.Entries[0].Message, $"The entry should carry the exact S1 text: {label}");
        Assert.AreSequenceEqual(bytes, File.ReadAllBytes(tokenFile), $"The configured file should stay as it was: {label}");
        AssertNoMarker(logger, label);
    }

    /// <summary>
    /// A configured path that is a symbolic link to a valid file is followed, and the token of the file is accepted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    [TestCategory("AC7")]
    public void IngestTokenSetupTryCreateAccessTokenFileSymbolicLinkReadsToken()
    {
        // Arrange
        var target = CreateConfiguredTokenFile(Encoding.ASCII.GetBytes(ConfiguredToken));
        var link = Path.Combine(Path.GetDirectoryName(target)!, "token-link");

        CreateSymbolicLinkOrInconclusive(link, target);

        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(null, null, link), logger);

        // Assert
        Assert.IsNotNull(access, "The start should go on");
        Assert.IsTrue(access.IsTokenAccepted(ConfiguredToken), "The access should accept the token of the file the link points to");
        Assert.IsFalse(access.IsTokenAccepted(ConfiguredToken[..^1]), "The access should refuse another token");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(TokenFileReadText, logger.Entries[0].Message, "The entry should carry the exact S1 text");
        AssertNoMarker(logger, "symbolic link");
    }

    /// <summary>
    /// A value of the setting that is not an absolute path, the empty string included, stops the start with the E3 line,
    /// which holds no part of the value, and the data directory stays untouched.
    /// </summary>
    /// <param name="value">The value of the setting</param>
    [TestMethod]
    [TestCategory("AC4")]
    [TestCategory("AC7")]
    [DataRow("")]
    [DataRow("token")]
    [DataRow("relative/token")]
    [DataRow("MARKER-a-token-by-mistake-0123456789abcdefg")]
    public void IngestTokenSetupTryCreateAccessTokenFileNotAbsoluteStops(string value)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, null, value), logger);

        // Assert
        Assert.IsNull(access, $"The start should stop: '{value}'");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: '{value}'");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, $"The entry should be critical: '{value}'");
        Assert.AreEqual(TokenFileNotAbsoluteText, logger.Entries[0].Message, $"The entry should carry the exact E3 text and nothing of the value: '{value}'");
        Assert.IsNull(logger.Entries[0].Exception, $"The entry should carry no exception: '{value}'");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), $"No token file should be created in the data directory: '{value}'");
        AssertNoMarker(logger, value);
    }

    /// <summary>
    /// The setting together with the switch set to false stops the start with the E4 line instead of running open.
    /// </summary>
    /// <param name="requireToken">The value of the switch</param>
    /// <param name="configureValidFile">Whether the setting names a valid file; otherwise it holds the empty string</param>
    [TestMethod]
    [TestCategory("AC5")]
    [TestCategory("AC7")]
    [DataRow("false", true)]
    [DataRow("FALSE", true)]
    [DataRow("false", false)]
    public void IngestTokenSetupTryCreateAccessTokenFileWithRequireTokenFalseStops(string requireToken, bool configureValidFile)
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var tokenFile = configureValidFile ? CreateConfiguredTokenFile(Encoding.ASCII.GetBytes(ConfiguredToken)) : string.Empty;
        var logger = new RecordingLogger();

        // Act
        var access = IngestTokenSetup.TryCreateAccess(CreateConfiguration(dataDirectory, requireToken, tokenFile), logger);

        // Assert
        Assert.IsNull(access, $"The start should stop: {requireToken}, valid file {configureValidFile}");
        Assert.HasCount(1, logger.Entries, $"Exactly one entry should be logged: {requireToken}, valid file {configureValidFile}");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, $"The entry should be critical: {requireToken}, valid file {configureValidFile}");
        Assert.AreEqual(TokenFileWithSwitchOffText, logger.Entries[0].Message, $"The entry should carry the exact E4 text: {requireToken}, valid file {configureValidFile}");
        Assert.IsNull(logger.Entries[0].Exception, $"The entry should carry no exception: {requireToken}, valid file {configureValidFile}");
        Assert.IsEmpty(Directory.GetFileSystemEntries(dataDirectory), $"No token file should be created in the data directory: {requireToken}, valid file {configureValidFile}");
        AssertNoMarker(logger, requireToken);
    }

    /// <summary>
    /// Creates a configuration with the data directory and the switch.
    /// </summary>
    /// <param name="dataDirectory">The data directory, or <c>null</c> for none</param>
    /// <param name="requireToken">The value of the switch, or <c>null</c> for unset</param>
    /// <returns>The configuration</returns>
    private static IConfiguration CreateConfiguration(string? dataDirectory, string? requireToken)
    {
        return CreateConfiguration(dataDirectory, requireToken, null);
    }

    /// <summary>
    /// Creates a configuration with the data directory, the switch and the token file setting.
    /// </summary>
    /// <param name="dataDirectory">The data directory, or <c>null</c> for none</param>
    /// <param name="requireToken">The value of the switch, or <c>null</c> for unset</param>
    /// <param name="tokenFile">The value of the token file setting, or <c>null</c> for unset</param>
    /// <returns>The configuration</returns>
    private static IConfiguration CreateConfiguration(string? dataDirectory, string? requireToken, string? tokenFile)
    {
        var settings = new Dictionary<string, string?>();

        if (dataDirectory is not null)
        {
            settings[DataProtectionSetup.DataDirectoryKey] = dataDirectory;
        }

        if (requireToken is not null)
        {
            settings[IngestTokenSetup.RequireTokenKey] = requireToken;
        }

        if (tokenFile is not null)
        {
            settings[IngestTokenSetup.TokenFileKey] = tokenFile;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    /// <summary>
    /// Asserts that no log entry holds a token or a configured path, neither in its message nor in its exception.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="label">The name of the case</param>
    private static void AssertNoMarker(RecordingLogger logger, string label)
    {
        var texts = logger.Entries.Select(entry => $"{entry.Message} {entry.Exception}").ToList();

        Assert.DoesNotContain(text => text.Contains(TokenMarker, StringComparison.Ordinal), texts, $"No log entry should hold the token: {label}");
        Assert.DoesNotContain(text => text.Contains(PathMarker, StringComparison.Ordinal), texts, $"No log entry should hold the configured path: {label}");
    }

    /// <summary>
    /// Reads the names and contents of the files in a directory.
    /// </summary>
    /// <param name="directory">The directory</param>
    /// <returns>One text per file: its name and its content in Base64</returns>
    private static List<string> ReadDirectoryState(string directory)
    {
        return Directory.GetFiles(directory)
                        .Order()
                        .Select(file => $"{Path.GetFileName(file)}={Convert.ToBase64String(File.ReadAllBytes(file))}")
                        .ToList();
    }

    /// <summary>
    /// Creates a symbolic link, or reports the test inconclusive when the run cannot create one.
    /// </summary>
    /// <param name="link">The path of the link</param>
    /// <param name="target">The path the link points to</param>
    private static void CreateSymbolicLinkOrInconclusive(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive("The run cannot create a symbolic link: " + exception.Message);
        }
    }

    /// <summary>
    /// Writes a token file when content is given.
    /// </summary>
    /// <param name="tokenFile">The path of the token file</param>
    /// <param name="content">The content, or <c>null</c> for no file</param>
    private static void WriteTokenFileIfAny(string tokenFile, string? content)
    {
        if (content is not null)
        {
            File.WriteAllText(tokenFile, content);
        }
    }

    /// <summary>
    /// Creates a configured token file with the content in a temporary directory whose name holds the path marker.
    /// </summary>
    /// <param name="content">The content of the file</param>
    /// <returns>The full path of the file</returns>
    private string CreateConfiguredTokenFile(byte[] content)
    {
        var tokenFile = Path.Combine(CreateMarkedDirectory(), "token");

        File.WriteAllBytes(tokenFile, content);

        return tokenFile;
    }

    /// <summary>
    /// Arranges a configured path that cannot be read, as the case names it.
    /// </summary>
    /// <param name="label">The case</param>
    /// <returns>The full configured path and a stream that holds the file until the act, or <c>null</c></returns>
    private (string TokenFile, FileStream? Holder) ArrangeUnreadableTokenFile(string label)
    {
        var directory = CreateMarkedDirectory();
        var tokenFile = Path.Combine(directory, "token");

        switch (label)
        {
            case DirectoryCase:
                {
                    Directory.CreateDirectory(tokenFile);

                    return (tokenFile, null);
                }

            case LinkToMissingFileCase:
                {
                    CreateSymbolicLinkOrInconclusive(tokenFile, Path.Combine(directory, "missing"));

                    return (tokenFile, null);
                }

            case HeldFileCase:
                {
                    File.WriteAllText(tokenFile, ConfiguredToken);

                    return (tokenFile, new FileStream(tokenFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
                }

            case NulCharacterCase:
                {
                    return (Path.Combine(directory, "token\0name"), null);
                }

            default:
                {
                    return (tokenFile, null);
                }
        }
    }

    /// <summary>
    /// Creates an empty temporary directory whose name holds the path marker, deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateMarkedDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"censio-test-{PathMarker}-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryPaths.Add(path);

        return path;
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryPaths.Add(path);

        return path;
    }

    #endregion // Methods
}