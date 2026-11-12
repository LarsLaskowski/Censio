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
    [DataRow("empty", "")]
    [DataRow("white space only", " \t\r\n ")]
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
        Assert.AreEqual($"The ingest token file '{tokenFile}' holds no valid token (1 to 1,024 visible ASCII characters); the server stops.", logger.Entries[0].Message, $"The entry should carry the exact E10 text: {label}");
        Assert.DoesNotContain("MARKER", $"{logger.Entries[0].Message} {logger.Entries[0].Exception}", $"The entry should hold no file content: {label}");
        Assert.AreSequenceEqual(bytes, File.ReadAllBytes(tokenFile), $"The token file should stay as it was: {label}");
    }

    /// <summary>
    /// A token at the bounds of E10 is read: 1,024 characters, a file of 65,536 bytes, and the first and last visible ASCII
    /// characters.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="token">The token, or <c>null</c> for 1,024 characters</param>
    /// <param name="fileLength">The length of the file in bytes, the token padded with trailing spaces</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("1,024 characters", null, 1024)]
    [DataRow("1,024 characters in 65,536 bytes", null, 65536)]
    [DataRow("first and last visible character", "!~", 2)]
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
        Assert.AreEqual($"The ingest token file '{tokenFile}' holds no valid token (1 to 1,024 visible ASCII characters); the server stops.", logger.Entries[0].Message, "The entry should carry the exact E10 text");
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
    /// Creates a configuration with the data directory and the switch.
    /// </summary>
    /// <param name="dataDirectory">The data directory, or <c>null</c> for none</param>
    /// <param name="requireToken">The value of the switch, or <c>null</c> for unset</param>
    /// <returns>The configuration</returns>
    private static IConfiguration CreateConfiguration(string? dataDirectory, string? requireToken)
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

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
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