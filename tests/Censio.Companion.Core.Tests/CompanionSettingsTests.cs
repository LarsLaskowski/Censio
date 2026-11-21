using System.Text;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionSettings" />.
/// </summary>
[TestClass]
public sealed class CompanionSettingsTests
{
    #region Constants

    /// <summary>
    /// The marker in a test text that stands for the invalid UTF-8 byte <c>0xFF</c>.
    /// </summary>
    private const string InvalidByteMarker = "<FF>";

    /// <summary>
    /// The marker in a test text that stands for the UTF-8 byte order mark <c>EF BB BF</c>.
    /// </summary>
    private const string ByteOrderMarkMarker = "<BOM>";

    /// <summary>
    /// A settings text that names a game folder.
    /// </summary>
    private const string ValidSettings = """{"gameFolder": "E:/Anno 117 - Pax Romana/"}""";

    /// <summary>
    /// The game folder <see cref="ValidSettings" /> names.
    /// </summary>
    private const string ValidFolder = "E:/Anno 117 - Pax Romana/";

    /// <summary>
    /// The problem of a mode other than <c>local</c>.
    /// </summary>
    private const string ModeNotLocal = "mode is not local";

    /// <summary>
    /// The problem of a port that is not an integer from 1 to 65535.
    /// </summary>
    private const string LocalPortInvalid = "localPort is not a port from 1 to 65535";

    /// <summary>
    /// A settings text that switches local mode on.
    /// </summary>
    private const string LocalSettings = """{"mode": "local"}""";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary folder of the test, which holds the settings file.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-settings-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// A <c>gameFolder</c> text is returned as written, without expanding a variable.
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expected">The expected game folder</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/"}""", "E:/Anno 117 - Pax Romana/", DisplayName = "Ubisoft Connect form")]
    [DataRow("""{"gameFolder": "C:\\Games\\Anno 117"}""", @"C:\Games\Anno 117", DisplayName = "Doubled backslashes")]
    [DataRow("""{"gameFolder": "C:\\Games\\%USERNAME%\\Anno"}""", @"C:\Games\%USERNAME%\Anno", DisplayName = "Variable not expanded")]
    public void CompanionSettingsReadReturnsGameFolder(string text, string expected)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "A gameFolder text should give a game folder");
        Assert.AreEqual(expected, result.GameFolder, "The game folder should be the text as written");
        Assert.IsNull(result.Problem, "A game folder should carry no problem");
    }

    /// <summary>
    /// A leading byte order mark, comments of both kinds and trailing commas are accepted.
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("""<BOM>{"gameFolder": "E:/Anno 117 - Pax Romana/"}""", DisplayName = "Byte order mark")]
    [DataRow("// The game folder\n{\"gameFolder\": \"E:/Anno 117 - Pax Romana/\"}", DisplayName = "Line comment")]
    [DataRow("""{/* The game folder */ "gameFolder": "E:/Anno 117 - Pax Romana/" /* end */}""", DisplayName = "Block comment")]
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/",}""", DisplayName = "Trailing comma in object")]
    [DataRow("""{"other": [1, 2,], "gameFolder": "E:/Anno 117 - Pax Romana/",}""", DisplayName = "Trailing comma in array and object")]
    [DataRow("<BOM>// Censio\n{\n  // folder\n  \"gameFolder\": \"E:/Anno 117 - Pax Romana/\", /* last */\n}\n", DisplayName = "All together")]
    public void CompanionSettingsReadAcceptsBomCommentsAndTrailingCommas(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "A byte order mark, comments and trailing commas should be accepted");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be read past the tolerated forms");
    }

    /// <summary>
    /// The key matches <c>gameFolder</c> ignoring case.
    /// </summary>
    /// <param name="key">The key as written</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("GameFolder")]
    [DataRow("GAMEFOLDER")]
    [DataRow("gamefolder")]
    public void CompanionSettingsReadKeyIgnoresCase(string key)
    {
        // Arrange
        var path = WriteSettings($$"""{"{{key}}": "{{ValidFolder}}"}""");

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "A key that differs from gameFolder only in case should count as gameFolder");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be the value of the key in other case");
    }

    /// <summary>
    /// Other keys are ignored, a nested <c>gameFolder</c> included, and another spelling is an unknown key.
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedKind">The expected kind</param>
    /// <param name="expectedFolder">The expected game folder</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("""{"theme": "dark", "gameFolder": "E:/Anno 117 - Pax Romana/", "window": {"gameFolder": 5, "width": 800}, "list": [null]}""", CompanionSettingsResultKind.GameFolder, ValidFolder, DisplayName = "Other keys around gameFolder")]
    [DataRow("""{"game_folder": "E:/Anno 117 - Pax Romana/", "window": {"gameFolder": "E:/x/"}}""", CompanionSettingsResultKind.NoGameFolder, null, DisplayName = "Other spelling and nested key only")]
    public void CompanionSettingsReadIgnoresOtherKeys(string text, CompanionSettingsResultKind expectedKind, string? expectedFolder)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(expectedKind, result.Kind, "Only a top-level gameFolder key should count");
        Assert.AreEqual(expectedFolder, result.GameFolder, "Other keys should not change the game folder");
        Assert.IsNull(result.Problem, "Other keys should not make the file invalid");
    }

    /// <summary>
    /// A file that cannot be used gives an invalid result with the fixed problem text.
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedProblem">The expected problem text</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("""{"gameFolder": "E:/x/" """, "not valid JSON", DisplayName = "Broken JSON")]
    [DataRow("""["E:/Anno 117 - Pax Romana/"]""", "not a JSON object", DisplayName = "Root array")]
    [DataRow("42", "not a JSON object", DisplayName = "Root number")]
    [DataRow("null", "not a JSON object", DisplayName = "Root null")]
    [DataRow("""{"gameFolder": 5}""", "gameFolder is not a text", DisplayName = "Number value")]
    [DataRow("""{"gameFolder": null}""", "gameFolder is not a text", DisplayName = "Null value")]
    [DataRow("""{"gameFolder": true}""", "gameFolder is not a text", DisplayName = "Boolean value")]
    [DataRow("""{"gameFolder": {"path": "E:/x/"}}""", "gameFolder is not a text", DisplayName = "Object value")]
    [DataRow("""{"gameFolder": "E:/a/", "gameFolder": "E:/b/"}""", "gameFolder occurs more than once", DisplayName = "Key twice")]
    [DataRow("""{"gameFolder": "E:/a/", "GameFolder": "E:/b/"}""", "gameFolder occurs more than once", DisplayName = "Key twice in other case")]
    [DataRow("""{"gameFolder": "E:/Anno<FF>/"}""", "not valid JSON", DisplayName = "Byte 0xFF in the gameFolder value")]
    [DataRow("""{"game<FF>Folder": "E:/Anno 117 - Pax Romana/"}""", "not valid JSON", DisplayName = "Byte 0xFF in the gameFolder key")]
    [DataRow("""{"oth<FF>er": 1, "gameFolder": "E:/Anno 117 - Pax Romana/"}""", "not valid JSON", DisplayName = "Byte 0xFF in another key")]
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/", "oth<FF>er": 1}""", "not valid JSON", DisplayName = "Byte 0xFF in another key after gameFolder")]
    [DataRow("""{"gameFolder": "\uD800"}""", "not valid JSON", DisplayName = "Lone surrogate as the value")]
    public void CompanionSettingsReadInvalidReturnsInvalid(string text, string expectedProblem)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.Invalid, result.Kind, "A file that cannot be used should be invalid");
        Assert.AreEqual(expectedProblem, result.Problem, "The problem should be the fixed text of the case");
        Assert.IsNull(result.GameFolder, "An invalid file should give no game folder");
    }

    /// <summary>
    /// Invalid bytes in a comment or in another key's value are accepted.
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("""{/* note <FF> */ "gameFolder": "E:/Anno 117 - Pax Romana/"}""", DisplayName = "Byte 0xFF in a block comment")]
    [DataRow("// note <FF>\n{\"gameFolder\": \"E:/Anno 117 - Pax Romana/\"}", DisplayName = "Byte 0xFF in a line comment")]
    [DataRow("""{"other": "x<FF>y", "gameFolder": "E:/Anno 117 - Pax Romana/"}""", DisplayName = "Byte 0xFF in another key's value")]
    public void CompanionSettingsReadAcceptsInvalidBytesOutsideNames(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "An invalid byte outside every name and the gameFolder value should be accepted");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be read past the invalid byte");
    }

    /// <summary>
    /// A file larger than 1 MiB is invalid, judged by its raw bytes, also a sparse file of 3 GiB.
    /// </summary>
    /// <param name="length">The file length in bytes</param>
    /// <param name="sparse">Whether the file is extended with <see cref="FileStream.SetLength" /> instead of written</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(1048577L, false, DisplayName = "One byte over the bound")]
    [DataRow(3221225472L, true, DisplayName = "Sparse file of 3 GiB")]
    public void CompanionSettingsReadTooLargeReturnsInvalid(long length, bool sparse)
    {
        // Arrange
        var path = WriteSettingsOfLength(length, sparse);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.Invalid, result.Kind, "A file larger than 1 MiB should be invalid");
        Assert.AreEqual("larger than 1 MiB", result.Problem, "The problem should name the bound");
        Assert.IsNull(result.GameFolder, "A file over the bound should give no game folder");
    }

    /// <summary>
    /// A file of exactly 1 MiB is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void CompanionSettingsReadMaxLengthReturnsGameFolder()
    {
        // Arrange
        var path = WriteSettingsOfLength(1048576L, false);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "A file of exactly 1 MiB should be read");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be read from a file at the bound");
    }

    /// <summary>
    /// A settings path that cannot be read as a file is invalid with its exception.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void CompanionSettingsReadUnreadableReturnsInvalid()
    {
        // Arrange
        var path = Path.Combine(_root, "settings.json");

        Directory.CreateDirectory(path);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.Invalid, result.Kind, "A folder named like the settings file should be invalid");
        Assert.AreEqual("cannot be read", result.Problem, "The problem should say the file cannot be read");
        Assert.IsNotNull(result.Exception, "The read failure should carry its exception");
    }

    /// <summary>
    /// A missing file, a missing folder and a path that is not fully qualified give no game folder.
    /// </summary>
    /// <param name="kind">Which path is passed</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("missing file")]
    [DataRow("missing folder")]
    [DataRow("relative path to an existing file")]
    public void CompanionSettingsReadMissingFileReturnsNoGameFolder(string kind)
    {
        // Arrange
        var path = MissingSettingsPath(kind);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.NoGameFolder, result.Kind, "A settings file that is not there should name no game folder");
        Assert.IsNull(result.GameFolder, "No game folder should be returned");
        Assert.IsNull(result.Problem, "A missing settings file is no problem");
    }

    /// <summary>
    /// Without a settings file no local server is hosted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithoutFileReturnsOff()
    {
        // Arrange
        var path = MissingSettingsPath("missing file");

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Off, setting.Kind, "Without a settings file no local server should be hosted");
        Assert.IsNull(setting.Problem, "A missing settings file is no problem");
    }

    /// <summary>
    /// The mode <c>local</c> without a port hosts the local server at port 5000 (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithModeLocalReturnsDefaultPort()
    {
        // Arrange
        var path = WriteSettings(LocalSettings);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "The mode local should host the local server");
        Assert.AreEqual(5000, setting.Port, "Without localPort the port should be 5000");
        Assert.AreEqual(CompanionSettings.DefaultLocalPort, setting.Port, "Without localPort the port should be the default port");
        Assert.IsNull(setting.Problem, "A local setting should carry no problem");
    }

    /// <summary>
    /// The value <c>local</c> and the key <c>mode</c> are matched ignoring case.
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": "local"}""", DisplayName = "local")]
    [DataRow("""{"mode": "LOCAL"}""", DisplayName = "LOCAL")]
    [DataRow("""{"mode": "Local"}""", DisplayName = "Local")]
    [DataRow("""{"Mode": "local"}""", DisplayName = "Key in other case")]
    public void CompanionSettingsReadLocalServerWithModeInAnyCaseReturnsLocal(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "The mode local should be matched ignoring case");
        Assert.AreEqual(CompanionSettings.DefaultLocalPort, setting.Port, "Without localPort the port should be the default port");
    }

    /// <summary>
    /// A blank mode counts as no mode (S7, E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": ""}""", DisplayName = "Empty")]
    [DataRow("""{"mode": "  "}""", DisplayName = "Spaces")]
    public void CompanionSettingsReadLocalServerWithBlankModeReturnsOff(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Off, setting.Kind, "A blank mode should count as no mode");
        Assert.IsNull(setting.Problem, "A blank mode is no problem");
    }

    /// <summary>
    /// A <c>localPort</c> from 1 to 65535 is the port of the local server, its key matched ignoring case (S3).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expected">The expected port</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": "local", "localPort": 1}""", 1, DisplayName = "Lowest port")]
    [DataRow("""{"mode": "local", "localPort": 5117}""", 5117, DisplayName = "Port 5117")]
    [DataRow("""{"mode": "local", "localPort": 65535}""", 65535, DisplayName = "Highest port")]
    [DataRow("""{"localPort": 5117, "MODE": "local"}""", 5117, DisplayName = "Port before the mode")]
    [DataRow("""{"mode": "local", "LocalPort": 5117}""", 5117, DisplayName = "Key in other case")]
    public void CompanionSettingsReadLocalServerWithLocalPortReturnsThatPort(string text, int expected)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "A valid port should host the local server");
        Assert.AreEqual(expected, setting.Port, "The port should be the value of localPort");
        Assert.IsNull(setting.Problem, "A valid port should carry no problem");
    }

    /// <summary>
    /// A mode that is not the text <c>local</c> is invalid with its fixed problem text (E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": "forward"}""", DisplayName = "forward")]
    [DataRow("""{"mode": "remote"}""", DisplayName = "remote")]
    [DataRow("""{"mode": " local"}""", DisplayName = "local with a leading space")]
    [DataRow("""{"mode": 1}""", DisplayName = "Number")]
    [DataRow("""{"mode": null}""", DisplayName = "Null")]
    [DataRow("""{"mode": true}""", DisplayName = "Boolean")]
    [DataRow("""{"mode": {}}""", DisplayName = "Object")]
    [DataRow("""{"mode": []}""", DisplayName = "Array")]
    public void CompanionSettingsReadLocalServerWithInvalidModeReturnsInvalid(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, "A mode other than local should be invalid");
        Assert.AreEqual(ModeNotLocal, setting.Problem, "The problem should be the fixed text of a mode other than local");
    }

    /// <summary>
    /// With the mode <c>local</c>, a <c>localPort</c> that is not an integer literal from 1 to 65535 is invalid with its
    /// fixed problem text (E2).
    /// </summary>
    /// <param name="value">The value of localPort as written</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("5117.0", DisplayName = "Decimal point")]
    [DataRow("5.117e3", DisplayName = "Exponent")]
    [DataRow("5117e0", DisplayName = "Exponent of an integer")]
    [DataRow("\"5117\"", DisplayName = "Text")]
    [DataRow("null", DisplayName = "Null")]
    [DataRow("0", DisplayName = "Zero")]
    [DataRow("-0", DisplayName = "Negative zero")]
    [DataRow("65536", DisplayName = "One above the highest port")]
    [DataRow("-1", DisplayName = "Negative")]
    [DataRow("99999999999999999999", DisplayName = "Beyond a 64-bit integer")]
    [DataRow("true", DisplayName = "Boolean")]
    [DataRow("[5117]", DisplayName = "Array")]
    public void CompanionSettingsReadLocalServerWithInvalidLocalPortReturnsInvalid(string value)
    {
        // Arrange
        var path = WriteSettings($$"""{"mode": "local", "localPort": {{value}}}""");

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, $"The localPort {value} should be invalid");
        Assert.AreEqual(LocalPortInvalid, setting.Problem, "The problem should be the fixed text of an invalid port");
    }

    /// <summary>
    /// A key that occurs twice, also in different case, is invalid with its fixed problem text (E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedProblem">The expected problem text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": "local", "mode": "local"}""", "mode occurs more than once", DisplayName = "mode twice")]
    [DataRow("""{"mode": "local", "Mode": "local"}""", "mode occurs more than once", DisplayName = "mode and Mode")]
    [DataRow("""{"mode": "local", "localPort": 5117, "LOCALPORT": 5117}""", "localPort occurs more than once", DisplayName = "localPort and LOCALPORT")]
    public void CompanionSettingsReadLocalServerWithDuplicateKeyReturnsInvalid(string text, string expectedProblem)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, "A key that occurs twice should be invalid");
        Assert.AreEqual(expectedProblem, setting.Problem, "The problem should name the key that occurs twice");
    }

    /// <summary>
    /// Without the mode <c>local</c> the key <c>localPort</c> is not read (E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"localPort": "abc"}""", DisplayName = "Invalid port without mode")]
    [DataRow("""{"localPort": 5117, "localPort": 0}""", DisplayName = "Port twice without mode")]
    [DataRow("""{"mode": " ", "localPort": 0}""", DisplayName = "Invalid port with a blank mode")]
    public void CompanionSettingsReadLocalServerWithoutModeIgnoresInvalidLocalPort(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Off, setting.Kind, "Without the mode local localPort should not be read");
        Assert.IsNull(setting.Problem, "A localPort that is not read should be no problem");
    }

    /// <summary>
    /// A settings file that is invalid as a whole starts no server and shows no server line (E3).
    /// </summary>
    /// <param name="kind">How the file is invalid</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("not JSON")]
    [DataRow("array root")]
    [DataRow("over 1 MiB")]
    [DataRow("cannot be read")]
    public void CompanionSettingsReadLocalServerWithInvalidFileReturnsOff(string kind)
    {
        // Arrange
        var path = WriteInvalidLocalSettings(kind);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Off, setting.Kind, $"A settings file that is {kind} should host no local server");
    }

    /// <summary>
    /// An invalid <c>gameFolder</c> does not change the local setting (E2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithInvalidGameFolderKeepsLocal()
    {
        // Arrange
        var path = WriteSettings("""{"gameFolder": 5, "gameFolder": null, "mode": "local", "localPort": 5117}""");

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "An invalid gameFolder should not change the local setting");
        Assert.AreEqual(5117, setting.Port, "The port should be read past an invalid gameFolder");
    }

    /// <summary>
    /// A relative path to a settings file with the mode <c>local</c> is not read, so no local server is hosted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithRelativePathReturnsOff()
    {
        // Arrange
        var path = Path.GetRelativePath(Environment.CurrentDirectory, WriteSettings("""{"mode": "local", "localPort": 5117}"""));

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.IsFalse(Path.IsPathFullyQualified(path), "The path of the case should be relative");
        Assert.AreEqual(LocalServerSettingKind.Off, setting.Kind, "A relative settings path should host no local server");
        Assert.IsNull(setting.Problem, "A relative settings path is no problem of the setting");
    }

    /// <summary>
    /// Invalid <c>mode</c> and <c>localPort</c> keys do not change the game folder of the file (E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/", "mode": "forward", "localPort": 0}""", DisplayName = "Invalid mode and port")]
    [DataRow("""{"mode": 1, "mode": null, "gameFolder": "E:/Anno 117 - Pax Romana/"}""", DisplayName = "Mode twice, not a text")]
    [DataRow("""{"mode": "local", "localPort": "abc", "LOCALPORT": 5117, "gameFolder": "E:/Anno 117 - Pax Romana/"}""", DisplayName = "Port invalid and twice")]
    public void CompanionSettingsReadWithInvalidLocalServerKeysKeepsGameFolder(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "Invalid local server keys should not make the settings invalid");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be read past invalid local server keys");
        Assert.IsNull(result.Problem, "Invalid local server keys should be no problem of the game folder");
    }

    /// <summary>
    /// Turns a test text into the bytes of the file: <see cref="ByteOrderMarkMarker" /> becomes the UTF-8 byte order
    /// mark and <see cref="InvalidByteMarker" /> the byte <c>0xFF</c>, the rest is encoded as UTF-8.
    /// </summary>
    /// <param name="text">The test text</param>
    /// <returns>The bytes</returns>
    private static byte[] ToBytes(string text)
    {
        var withMark = text.Replace(ByteOrderMarkMarker, ((char)0xFEFF).ToString(), StringComparison.Ordinal);

        return withMark.Split(InvalidByteMarker)
                       .Select(Encoding.UTF8.GetBytes)
                       .Aggregate((left, right) => [.. left, 0xFF, .. right]);
    }

    /// <summary>
    /// Writes a settings file into the temporary folder.
    /// </summary>
    /// <param name="text">The test text, see <see cref="ToBytes" /></param>
    /// <returns>The path of the file</returns>
    private string WriteSettings(string text)
    {
        Directory.CreateDirectory(_root);

        var path = Path.Combine(_root, "settings.json");

        File.WriteAllBytes(path, ToBytes(text));

        return path;
    }

    /// <summary>
    /// Writes a settings file of the given length that starts with <see cref="ValidSettings" />, padded with spaces or,
    /// for a sparse file, extended with <see cref="FileStream.SetLength" />.
    /// </summary>
    /// <param name="length">The file length in bytes</param>
    /// <param name="sparse">Whether the file is extended instead of written</param>
    /// <returns>The path of the file</returns>
    private string WriteSettingsOfLength(long length, bool sparse)
    {
        var path = WriteSettings(ValidSettings);

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write))
        {
            if (sparse)
            {
                stream.SetLength(length);
            }
            else
            {
                stream.Seek(0, SeekOrigin.End);

                var padding = new byte[length - stream.Length];

                Array.Fill(padding, (byte)' ');

                stream.Write(padding);
            }
        }

        return path;
    }

    /// <summary>
    /// Writes a settings file with the mode <c>local</c> that is invalid as a whole, or a folder in its place.
    /// </summary>
    /// <param name="kind">How the file is invalid: not JSON, array root, over 1 MiB or cannot be read</param>
    /// <returns>The path of the file</returns>
    private string WriteInvalidLocalSettings(string kind)
    {
        return kind switch
               {
                   "not JSON" => WriteSettings("""{"mode": "local" """),
                   "array root" => WriteSettings("""[{"mode": "local"}]"""),
                   "over 1 MiB" => WriteSettings(LocalSettings + new string(' ', CompanionSettings.MaxFileLength + 1 - LocalSettings.Length)),
                   _ => CreateFolderInPlaceOfSettings()
               };
    }

    /// <summary>
    /// Creates a folder where the settings file should be, so it cannot be read as a file.
    /// </summary>
    /// <returns>The path of the folder</returns>
    private string CreateFolderInPlaceOfSettings()
    {
        var path = Path.Combine(_root, "settings.json");

        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Gets a settings path that names no readable settings file.
    /// </summary>
    /// <param name="kind">Which path: a missing file, a missing folder, or a relative path to an existing file</param>
    /// <returns>The path</returns>
    private string MissingSettingsPath(string kind)
    {
        Directory.CreateDirectory(_root);

        return kind switch
               {
                   "missing file" => Path.Combine(_root, "settings.json"),
                   "missing folder" => Path.Combine(_root, "Censio", "settings.json"),
                   _ => Path.GetRelativePath(Environment.CurrentDirectory, WriteSettings(ValidSettings)),
               };
    }

    #endregion // Methods
}