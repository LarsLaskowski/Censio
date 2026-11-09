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