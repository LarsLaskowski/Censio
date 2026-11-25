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
    /// The problem of a mode other than <c>local</c> and <c>forward</c>.
    /// </summary>
    private const string ModeNotLocalOrForward = "mode is not local or forward";

    /// <summary>
    /// The problem of a missing <c>serverAddress</c> in forward mode.
    /// </summary>
    private const string ServerAddressMissing = "serverAddress is missing";

    /// <summary>
    /// The problem of a <c>serverAddress</c> that is not a <c>ws://</c> or <c>wss://</c> address.
    /// </summary>
    private const string ServerAddressInvalid = "serverAddress is not a ws:// or wss:// address";

    /// <summary>
    /// The problem of a <c>serverToken</c> that is not a valid token.
    /// </summary>
    private const string ServerTokenInvalid = "serverToken is not a valid token";

    /// <summary>
    /// A valid server address of forward mode.
    /// </summary>
    private const string ServerAddress = "ws://nas:8080/ingest";

    /// <summary>
    /// A valid token of 39 visible ASCII characters.
    /// </summary>
    private const string ServerToken = "ForwardToken-0123456789abcdefghijKLMNOP";

    /// <summary>
    /// A token of 32 characters, the shortest valid length.
    /// </summary>
    private const string ShortestToken = "abcdefghijklmnopqrstuvwxyz012345";

    /// <summary>
    /// A token of 31 characters, one below the shortest valid length.
    /// </summary>
    private const string TooShortToken = "abcdefghijklmnopqrstuvwxyz01234";

    /// <summary>
    /// The problem of a port that is not an integer from 1 to 65535.
    /// </summary>
    private const string LocalPortInvalid = "localPort is not a port from 1 to 65535";

    /// <summary>
    /// A settings text that switches local mode on.
    /// </summary>
    private const string LocalSettings = """{"mode": "local"}""";

    /// <summary>
    /// The problem of a <c>bufferHours</c> that is not an integer from 1 to 168.
    /// </summary>
    private const string BufferHoursInvalid = "bufferHours is not a whole number from 1 to 168";

    /// <summary>
    /// The problem of a <c>bufferHours</c> that occurs more than once.
    /// </summary>
    private const string BufferHoursTwice = "bufferHours occurs more than once";

    /// <summary>
    /// The problem of a <c>bufferMegabytes</c> that is not an integer from 1 to 10240.
    /// </summary>
    private const string BufferMegabytesInvalid = "bufferMegabytes is not a whole number from 1 to 10240";

    /// <summary>
    /// The problem of a <c>bufferMegabytes</c> that occurs more than once.
    /// </summary>
    private const string BufferMegabytesTwice = "bufferMegabytes occurs more than once";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The settings that switch live sending on, before the buffer keys: forward mode, local mode with <c>localPort</c>
    /// and local mode without it, each with the kind it reads as.
    /// </summary>
    private static readonly (string Settings, LocalServerSettingKind Kind)[] _bufferModes = [
                                                                                                ($"\"mode\": \"forward\", \"serverAddress\": \"{ServerAddress}\"", LocalServerSettingKind.Forward),
                                                                                                ("\"mode\": \"local\", \"localPort\": 5117", LocalServerSettingKind.Local),
                                                                                                ("\"mode\": \"local\"", LocalServerSettingKind.Local)
                                                                                            ];

    /// <summary>
    /// Valid buffer keys as written after the mode, with the limits they read as.
    /// </summary>
    private static readonly (string Keys, int Hours, int Megabytes)[] _validBufferKeys = [
                                                                                             (string.Empty, 24, 100),
                                                                                             (", \"bufferHours\": 48, \"bufferMegabytes\": 500", 48, 500),
                                                                                             (", \"bufferHours\": 1, \"bufferMegabytes\": 1", 1, 1),
                                                                                             (", \"bufferHours\": 168, \"bufferMegabytes\": 10240", 168, 10240),
                                                                                             (", \"BufferHours\": 48", 48, 100),
                                                                                             (", \"BUFFERMEGABYTES\": 500", 24, 500)
                                                                                         ];

    /// <summary>
    /// Invalid buffer keys as written after the mode, with the fixed problem text each reads as.
    /// </summary>
    private static readonly (string Keys, string Problem)[] _invalidBufferKeys = [
                                                                                     (", \"bufferHours\": 24.0", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 2.4e1", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 24e0", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": \"24\"", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": null", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": true", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 0", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": -0", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": -1", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 169", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 99999999999999999999", BufferHoursInvalid),
                                                                                     (", \"bufferHours\": 24, \"BufferHours\": 24", BufferHoursTwice),
                                                                                     (", \"bufferMegabytes\": 100.0", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": 1e2", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": \"100\"", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": null", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": 0", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": -1", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": 10241", BufferMegabytesInvalid),
                                                                                     (", \"bufferMegabytes\": 100, \"BUFFERMEGABYTES\": 100", BufferMegabytesTwice)
                                                                                 ];

    /// <summary>
    /// The temporary folder of the test, which holds the settings file.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-settings-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets every mode that switches live sending on combined with every valid buffer key: the settings before the keys,
    /// the kind they read as, the keys, and the expected hours and megabytes.
    /// </summary>
    private static IEnumerable<object[]> ValidBufferLimitCases => _bufferModes.SelectMany(_ => _validBufferKeys, (mode, keys) => new object[] { mode.Settings, mode.Kind, keys.Keys, keys.Hours, keys.Megabytes });

    /// <summary>
    /// Gets every mode that switches live sending on combined with every invalid buffer key: the settings before the
    /// keys, the keys, and the expected problem text.
    /// </summary>
    private static IEnumerable<object[]> InvalidBufferLimitCases => _bufferModes.SelectMany(_ => _invalidBufferKeys, (mode, keys) => new object[] { mode.Settings, keys.Keys, keys.Problem });

    #endregion // Properties

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
    /// A mode that is not the text <c>local</c> or <c>forward</c> is invalid with its fixed problem text (E1).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
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
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, "A mode other than local or forward should be invalid");
        Assert.AreEqual(ModeNotLocalOrForward, setting.Problem, "The problem should be the fixed text of a mode other than local or forward");
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
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/", "mode": "remote", "localPort": 0}""", DisplayName = "Invalid mode and port")]
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
    /// The mode <c>forward</c>, matched ignoring case, with a valid <c>serverAddress</c> returns the forward setting with
    /// the address and the trimmed token, or no token without <c>serverToken</c> (S1, E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedAddress">The expected address</param>
    /// <param name="expectedToken">The expected token</param>
    /// <param name="backslashes">The number of backslashes the settings text holds for its JSON escapes</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}"}""", ServerAddress, ServerToken, 0, DisplayName = "forward")]
    [DataRow($$"""{"mode": "FORWARD", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}"}""", ServerAddress, ServerToken, 0, DisplayName = "FORWARD")]
    [DataRow($$"""{"Mode": "Forward", "ServerAddress": "{{ServerAddress}}", "SERVERTOKEN": "{{ServerToken}}"}""", ServerAddress, ServerToken, 0, DisplayName = "Keys in other case")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "wss://censio.example.org/ingest", "serverToken": "{{ServerToken}}"}""", "wss://censio.example.org/ingest", ServerToken, 0, DisplayName = "wss")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "ws://nas:8080/ingest?profile=1", "serverToken": "{{ServerToken}}"}""", "ws://nas:8080/ingest?profile=1", ServerToken, 0, DisplayName = "With query")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ShortestToken}}"}""", ServerAddress, ShortestToken, 0, DisplayName = "Shortest token")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": " \t\r\n{{ServerToken}} \t\r\n"}""", ServerAddress, ServerToken, 6, DisplayName = "Token trimmed")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}"}""", ServerAddress, null, 0, DisplayName = "No token")]
    public void CompanionSettingsReadLocalServerWithModeForwardReturnsForward(string text, string expectedAddress, string? expectedToken, int backslashes)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(backslashes, text.Count(character => character == '\\'), "The settings text should keep the backslashes of its JSON escapes");
        Assert.AreEqual(LocalServerSettingKind.Forward, setting.Kind, "The mode forward with a valid address should be forward");
        Assert.IsNull(setting.Problem, "A valid forward setting should carry no problem");
        Assert.IsNotNull(setting.Forward, "A forward setting should carry the server setting");
        Assert.AreEqual(new Uri(expectedAddress), setting.Forward.Address, "The address should be the value of serverAddress");
        Assert.AreEqual(expectedAddress, setting.Forward.Address.OriginalString, "The address should be the text of serverAddress");
        Assert.AreEqual(expectedToken, setting.Forward.Token, "The token should be the trimmed value of serverToken, or none without it");
    }

    /// <summary>
    /// With the mode <c>forward</c>, a <c>serverAddress</c> that is missing or breaks the <c>--server</c> rules is invalid
    /// with its fixed problem text, which never quotes the value (E1).
    /// </summary>
    /// <param name="value">The value of serverAddress as written, or <see langword="null" /> when the key is missing</param>
    /// <param name="expectedProblem">The expected problem text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(null, ServerAddressMissing, DisplayName = "Missing")]
    [DataRow("1", ServerAddressInvalid, DisplayName = "Number")]
    [DataRow("null", ServerAddressInvalid, DisplayName = "Null")]
    [DataRow("\"http://nas:8080/ingest\"", ServerAddressInvalid, DisplayName = "http")]
    [DataRow("\"/ingest\"", ServerAddressInvalid, DisplayName = "Relative")]
    [DataRow("\"ws://user:secret@nas:8080/ingest\"", ServerAddressInvalid, DisplayName = "User information")]
    [DataRow("\"ws://nas:8080/ingest#top\"", ServerAddressInvalid, DisplayName = "Fragment")]
    [DataRow("\"ws://nas:8080/my ingest\"", ServerAddressInvalid, DisplayName = "Space")]
    [DataRow("\"\"", ServerAddressInvalid, DisplayName = "Empty")]
    [DataRow("\"   \"", ServerAddressInvalid, DisplayName = "Blank")]
    public void CompanionSettingsReadLocalServerWithInvalidServerAddressReturnsInvalid(string? value, string expectedProblem)
    {
        // Arrange
        var path = WriteSettings(ForwardSettings(value, $"\"{ServerToken}\""));

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, $"The serverAddress {value} should be invalid");
        Assert.AreEqual(expectedProblem, setting.Problem, "The problem should be the fixed text of the invalid serverAddress");
        Assert.IsNull(setting.Forward, "An invalid setting should carry no server setting");
    }

    /// <summary>
    /// With the mode <c>forward</c>, a <c>serverToken</c> that is not a text or not 32 to 1,024 visible ASCII characters
    /// after trimming is invalid with its fixed problem text (E2).
    /// </summary>
    /// <param name="value">The value of serverToken as written</param>
    /// <param name="backslashes">The number of backslashes the value holds for its JSON escapes</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow($"\"{TooShortToken}\"", 0, DisplayName = "31 characters")]
    [DataRow($"\" {TooShortToken} \"", 0, DisplayName = "31 characters after trimming")]
    [DataRow($"\"{TooShortToken}\\u00e9\"", 1, DisplayName = "A non-ASCII character")]
    [DataRow("1", 0, DisplayName = "Number")]
    [DataRow("null", 0, DisplayName = "Null")]
    [DataRow("\"\"", 0, DisplayName = "Empty")]
    [DataRow("\"   \"", 0, DisplayName = "Blank")]
    public void CompanionSettingsReadLocalServerWithInvalidServerTokenReturnsInvalid(string value, int backslashes)
    {
        // Arrange
        var path = WriteSettings(ForwardSettings($"\"{ServerAddress}\"", value));

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(backslashes, value.Count(character => character == '\\'), "The value should keep the backslashes of its JSON escapes");
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, $"The serverToken {value} should be invalid");
        Assert.AreEqual(ServerTokenInvalid, setting.Problem, "The problem should be the fixed text of an invalid serverToken");
        Assert.IsNull(setting.Forward, "An invalid setting should carry no server setting");
    }

    /// <summary>
    /// With the mode <c>forward</c>, <c>serverAddress</c> or <c>serverToken</c> occurring twice, also in other case, is
    /// invalid with its fixed problem text (E1, E2).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedProblem">The expected problem text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "ServerAddress": "{{ServerAddress}}"}""", "serverAddress occurs more than once", DisplayName = "serverAddress and ServerAddress")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}", "serverToken": "{{ServerToken}}"}""", "serverToken occurs more than once", DisplayName = "serverToken twice")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}", "SERVERTOKEN": "{{ShortestToken}}"}""", "serverToken occurs more than once", DisplayName = "serverToken and SERVERTOKEN")]
    public void CompanionSettingsReadLocalServerWithDuplicateServerKeysReturnsInvalid(string text, string expectedProblem)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, "A server key that occurs twice should be invalid");
        Assert.AreEqual(expectedProblem, setting.Problem, "The problem should name the server key that occurs twice");
        Assert.IsNull(setting.Forward, "An invalid setting should carry no server setting");
    }

    /// <summary>
    /// With the mode <c>forward</c> the key <c>localPort</c> is not read (N1).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "localPort": 0}""", DisplayName = "Invalid port")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "localPort": 5117, "LOCALPORT": 5118}""", DisplayName = "Port twice")]
    public void CompanionSettingsReadLocalServerWithModeForwardIgnoresLocalPort(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Forward, setting.Kind, "With the mode forward localPort should not be read");
        Assert.IsNull(setting.Problem, "A localPort that is not read should be no problem");
        Assert.IsNotNull(setting.Forward, "A forward setting should carry the server setting");
        Assert.AreEqual(new Uri(ServerAddress), setting.Forward.Address, "The address should be read past localPort");
    }

    /// <summary>
    /// A <c>serverToken</c> of 1,024 characters, the longest valid length, is read whole (E2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithLongestServerTokenReturnsForward()
    {
        // Arrange
        var token = new string('t', 1024);
        var path = WriteSettings(ForwardSettings($"\"{ServerAddress}\"", $"\"{token}\""));

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Forward, setting.Kind, "A serverToken of 1,024 characters should be valid");
        Assert.IsNotNull(setting.Forward, "A forward setting should carry the server setting");
        Assert.AreEqual(token, setting.Forward.Token, "The token of 1,024 characters should be read whole");
    }

    /// <summary>
    /// A <c>serverToken</c> of 1,025 characters, one above the longest valid length, is invalid with its fixed problem
    /// text (E2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsReadLocalServerWithTooLongServerTokenReturnsInvalid()
    {
        // Arrange
        var path = WriteSettings(ForwardSettings($"\"{ServerAddress}\"", $"\"{new string('t', 1025)}\""));

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, "A serverToken of 1,025 characters should be invalid");
        Assert.AreEqual(ServerTokenInvalid, setting.Problem, "The problem should be the fixed text of an invalid serverToken");
        Assert.IsNull(setting.Forward, "An invalid setting should carry no server setting");
    }

    /// <summary>
    /// With the mode <c>local</c> the keys <c>serverAddress</c> and <c>serverToken</c> are not read, also when they are
    /// invalid or occur twice (N1).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expectedPort">The expected port</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow("""{"mode": "local", "serverAddress": "http://nas:8080/ingest", "serverToken": 1}""", CompanionSettings.DefaultLocalPort, DisplayName = "Invalid address and token")]
    [DataRow($$"""{"mode": "local", "localPort": 5117, "serverAddress": "{{ServerAddress}}", "ServerAddress": "{{ServerAddress}}", "serverToken": "{{TooShortToken}}", "SERVERTOKEN": "{{ServerToken}}"}""", 5117, DisplayName = "Keys twice")]
    [DataRow($$"""{"mode": "LOCAL", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}"}""", CompanionSettings.DefaultLocalPort, DisplayName = "Valid forward keys")]
    public void CompanionSettingsReadLocalServerWithModeLocalIgnoresServerKeys(string text, int expectedPort)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "With the mode local the server keys should not be read");
        Assert.AreEqual(expectedPort, setting.Port, "The port should be read past the server keys");
        Assert.IsNull(setting.Problem, "Server keys that are not read should be no problem");
        Assert.IsNull(setting.Forward, "A local setting should carry no server setting of forward mode");
    }

    /// <summary>
    /// The keys of forward mode, valid or not, do not change the game folder of the file (E1).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow($$"""{"gameFolder": "{{ValidFolder}}", "mode": "forward", "serverAddress": "{{ServerAddress}}", "serverToken": "{{ServerToken}}"}""", DisplayName = "Valid forward keys")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "http://nas:8080/ingest", "serverToken": 1, "gameFolder": "{{ValidFolder}}"}""", DisplayName = "Invalid address and token")]
    [DataRow($$"""{"mode": "forward", "gameFolder": "{{ValidFolder}}"}""", DisplayName = "Address missing")]
    [DataRow($$"""{"mode": "forward", "serverAddress": "{{ServerAddress}}", "SERVERADDRESS": "{{ServerAddress}}", "serverToken": "{{ServerToken}}", "serverToken": "{{ServerToken}}", "gameFolder": "{{ValidFolder}}"}""", DisplayName = "Keys twice")]
    public void CompanionSettingsReadWithForwardKeysKeepsGameFolder(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var result = CompanionSettings.Read(path);

        // Assert
        Assert.AreEqual(CompanionSettingsResultKind.GameFolder, result.Kind, "The keys of forward mode should not make the settings invalid");
        Assert.AreEqual(ValidFolder, result.GameFolder, "The game folder should be read past the keys of forward mode");
        Assert.IsNull(result.Problem, "The keys of forward mode should be no problem of the game folder");
    }

    /// <summary>
    /// The text of a forward setting names the server setting by its type only and never holds the token (N5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC10")]
    public void CompanionSettingsReadLocalServerToStringHoldsNoToken()
    {
        // Arrange
        var path = WriteSettings(ForwardSettings($"\"{ServerAddress}\"", $"\"{ServerToken}\""));
        var setting = CompanionSettings.ReadLocalServer(path);

        // Act
        var text = setting.ToString();

        // Assert
        Assert.Contains("Kind = Forward", text, "The text should name the forward kind");
        Assert.Contains("Forward = " + typeof(ForwardServerSetting).FullName, text, "The text should name the server setting by its type");
        Assert.DoesNotContain(ServerToken, text, "The text of the setting should never hold the token");
    }

    /// <summary>
    /// In forward mode and in local mode with and without <c>localPort</c>, <c>bufferHours</c> and
    /// <c>bufferMegabytes</c> from 1 to 168 and 1 to 10240, matched ignoring case, are the buffer limits, and a key left
    /// out is 24 hours or 100 megabytes (S5, E1).
    /// </summary>
    /// <param name="mode">The settings before the buffer keys</param>
    /// <param name="expectedKind">The expected kind of the setting</param>
    /// <param name="keys">The buffer keys as written after the mode</param>
    /// <param name="expectedHours">The expected age limit in hours</param>
    /// <param name="expectedMegabytes">The expected size limit in megabytes</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(ValidBufferLimitCases))]
    public void CompanionSettingsReadLocalServerBufferLimits(string mode, LocalServerSettingKind expectedKind, string keys, int expectedHours, int expectedMegabytes)
    {
        // Arrange
        var path = WriteSettings($"{{{mode}{keys}}}");
        var expected = new LiveBufferLimits
                       {
                           Hours = expectedHours,
                           Megabytes = expectedMegabytes
                       };

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(expectedKind, setting.Kind, $"The settings {mode}{keys} should switch live sending on");
        Assert.IsNull(setting.Problem, "Valid buffer keys should carry no problem");
        Assert.AreEqual(expected, setting.BufferLimits, $"The buffer limits of {keys} should be {expectedHours} hours and {expectedMegabytes} megabytes");
    }

    /// <summary>
    /// In forward mode and in local mode with and without <c>localPort</c>, a <c>bufferHours</c> or
    /// <c>bufferMegabytes</c> that is not an integer literal in its range, or that occurs twice in any case, makes the
    /// server setting invalid with its fixed problem text (E1).
    /// </summary>
    /// <param name="mode">The settings before the buffer keys</param>
    /// <param name="keys">The buffer keys as written after the mode</param>
    /// <param name="expectedProblem">The expected problem text</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(InvalidBufferLimitCases))]
    public void CompanionSettingsReadLocalServerInvalidBufferLimitIsInvalid(string mode, string keys, string expectedProblem)
    {
        // Arrange
        var path = WriteSettings($"{{{mode}{keys}}}");

        // Act
        var setting = CompanionSettings.ReadLocalServer(path);

        // Assert
        Assert.AreEqual(LocalServerSettingKind.Invalid, setting.Kind, $"The settings {mode}{keys} should be an invalid server setting");
        Assert.AreEqual(expectedProblem, setting.Problem, "The problem should be the fixed text of the invalid buffer key");
        Assert.IsNull(setting.Forward, "An invalid setting should carry no server setting");
    }

    /// <summary>
    /// The key <c>language</c> reads <c>en</c> and <c>de</c> ignoring case; without the key, with another value, a value
    /// that is not a text or a key written twice it gives no language, so the Windows display language decides (S9).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="expected">The expected language, or <see langword="null" /> for none</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("""{"language": "en"}""", CompanionLanguage.English, DisplayName = "en")]
    [DataRow("""{"language": "DE"}""", CompanionLanguage.German, DisplayName = "DE")]
    [DataRow("""{"Language": "de"}""", CompanionLanguage.German, DisplayName = "Key in other case")]
    [DataRow("""{"gameFolder": "E:/Anno 117 - Pax Romana/"}""", null, DisplayName = "Missing")]
    [DataRow("""{"language": "fr"}""", null, DisplayName = "fr")]
    [DataRow("""{"language": 5}""", null, DisplayName = "Number 5")]
    [DataRow("""{"language": "de", "language": "de"}""", null, DisplayName = "Twice")]
    public void CompanionSettingsReadLanguageGivesEnOrDeOnly(string text, CompanionLanguage? expected)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var language = CompanionSettings.ReadLanguage(path);

        // Assert
        Assert.AreEqual(expected, language, $"The settings {text} should give the language {expected}");
    }

    /// <summary>
    /// Turns a test text into the bytes of the file:<see cref="ByteOrderMarkMarker" /> becomes the UTF-8 byte order
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
    /// Creates a settings text with the mode <c>forward</c>.
    /// </summary>
    /// <param name="address">The value of serverAddress as written, or <see langword="null" /> to leave the key out</param>
    /// <param name="token">The value of serverToken as written</param>
    /// <returns>The settings text</returns>
    private static string ForwardSettings(string? address, string token)
    {
        var addressProperty = address is null ? string.Empty : $"\"serverAddress\": {address}, ";

        return $$"""{"mode": "forward", {{addressProperty}}"serverToken": {{token}}}""";
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