using System.Text;
using System.Text.Json;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionSettingsDocument" />.
/// </summary>
[TestClass]
public sealed class CompanionSettingsDocumentTests
{
    #region Constants

    /// <summary>
    /// The marker in a test text that stands for the invalid UTF-8 byte <c>0xFF</c>.
    /// </summary>
    private const string InvalidByteMarker = "<FF>";

    /// <summary>
    /// The marker in a test text that stands for spaces of the length of <see cref="CompanionSettings.MaxFileLength" />.
    /// </summary>
    private const string PaddingMarker = "<1 MiB>";

    /// <summary>
    /// The E7 reason of a file that holds a text that cannot be decoded.
    /// </summary>
    private const string NotValidUtf8 = "holds text that is not valid UTF-8";

    /// <summary>
    /// A valid server address of forward mode.
    /// </summary>
    private const string ServerAddress = "ws://nas:8080/ingest";

    /// <summary>
    /// A valid token of 39 visible ASCII characters.
    /// </summary>
    private const string ServerToken = "ForwardToken-0123456789abcdefghijKLMNOP";

    /// <summary>
    /// A game folder.
    /// </summary>
    private const string GameFolder = "E:/Anno 117/";

    /// <summary>
    /// A settings text of local mode with every other known key but the server keys.
    /// </summary>
    private const string LocalSettings = """{"mode": "local", "localPort": 5117, "bufferHours": 48, "bufferMegabytes": 200, "gameFolder": "E:/Anno 117/"}""";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-document-" + Guid.NewGuid().ToString("N"));

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the path of the settings file in the temporary folder.
    /// </summary>
    private string SettingsPath => Path.Combine(_root, "settings.json");

    /// <summary>
    /// Gets the path of the temporary file of a save.
    /// </summary>
    private string TmpPath => Path.Combine(_root, "settings.json.tmp");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the temporary folder.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        Directory.CreateDirectory(_root);
    }

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
    /// Without a file the document holds the defaults: mode Off, port 5000, 24 hours, 100 megabytes, no address, token,
    /// game folder or language, and no problem (S1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsDocumentLoadMissingFileGivesDefaults()
    {
        // Arrange
        var path = SettingsPath;

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.IsEmpty(Differences(CreateDefaults(), document.Values), "A missing file should give the default of every field");
        Assert.IsNull(document.FileProblem, "A missing file should be no file problem");
        Assert.IsEmpty(document.FieldProblems, "A missing file should give no field problem");
    }

    /// <summary>
    /// The document shows the text of every known key as the file holds it, a key in other case or escaped included.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsDocumentLoadShowsFileValues()
    {
        // Arrange
        var path = WriteSettings("""{"MODE": "Forward", "localPort": 5117, "serverAddress": "ws://nas:8080/ingest", "serverToken": "ForwardToken-0123456789abcdefghijKLMNOP", "bufferHours": 48, "bufferMegabytes": 200, "g\u0061meFolder": "E:/Anno 117/", "language": "de"}""");
        var expected = CreateValues(CompanionSettingsMode.Forward,
                                    CompanionLanguage.German,
                                    (SettingsField.LocalPort, "5117"),
                                    (SettingsField.ServerAddress, ServerAddress),
                                    (SettingsField.ServerToken, ServerToken),
                                    (SettingsField.BufferHours, "48"),
                                    (SettingsField.BufferMegabytes, "200"),
                                    (SettingsField.GameFolder, GameFolder));

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.IsEmpty(Differences(expected, document.Values), "Every field should show the text of its key");
        Assert.IsNull(document.FileProblem, "A valid file should be no file problem");
        Assert.IsEmpty(document.FieldProblems, "A valid file should give no field problem");
    }

    /// <summary>
    /// A known key with a wrong value or written twice, of any mode, empties its field and marks it with its problem; the
    /// other fields show the file's values; a wrong or repeated <c>language</c> only loads as not set (E8).
    /// </summary>
    /// <param name="keys">The keys added to a local-mode file with 200 megabytes</param>
    /// <param name="field">The field of the problem</param>
    /// <param name="kind">The kind of the problem</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("""
             "localPort": "abc"
             """,
             SettingsField.LocalPort,
             SettingsProblemKind.PortRange,
             DisplayName = "Port as text")]
    [DataRow("""
             "localPort": 65536
             """,
             SettingsField.LocalPort,
             SettingsProblemKind.PortRange,
             DisplayName = "Port too large")]
    [DataRow("""
             "localPort": 5.0
             """,
             SettingsField.LocalPort,
             SettingsProblemKind.PortRange,
             DisplayName = "Port 5.0")]
    [DataRow("""
             "bufferHours": 169
             """,
             SettingsField.BufferHours,
             SettingsProblemKind.HoursRange,
             DisplayName = "Hours 169")]
    [DataRow("""
             "serverAddress": "http://nas/"
             """,
             SettingsField.ServerAddress,
             SettingsProblemKind.AddressInvalid,
             DisplayName = "Address of another mode")]
    [DataRow("""
             "serverToken": "short"
             """,
             SettingsField.ServerToken,
             SettingsProblemKind.TokenInvalid,
             DisplayName = "Token of another mode")]
    [DataRow("""
             "localPort": 5117, "LocalPort": 5118
             """,
             SettingsField.LocalPort,
             SettingsProblemKind.WrittenTwice,
             DisplayName = "Port twice")]
    [DataRow("""
             "gameFolder": "E:/a/", "gameFolder": "E:/b/"
             """,
             SettingsField.GameFolder,
             SettingsProblemKind.WrittenTwice,
             DisplayName = "Game folder twice")]
    [DataRow("""
             "mode": "local"
             """,
             SettingsField.Mode,
             SettingsProblemKind.WrittenTwice,
             DisplayName = "Mode twice")]
    [DataRow("""
             "gameFolder": 5
             """,
             SettingsField.GameFolder,
             SettingsProblemKind.FolderNotFullPath,
             DisplayName = "Game folder as number")]
    public void CompanionSettingsDocumentLoadMarksInvalidAndDuplicatedKeys(string keys, SettingsField field, SettingsProblemKind kind)
    {
        // Arrange
        var path = WriteSettings($$"""{"mode": "local", "bufferMegabytes": 200, {{keys}}}""");
        var expected = new SettingsProblem
                       {
                           Field = field,
                           Kind = kind
                       };

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.AreSequenceEqual(new[] { expected }, document.FieldProblems.ToArray(), $"The key {keys} should mark its field with its problem");
        Assert.AreEqual(string.Empty, TextOf(document.Values, field), $"The field of {keys} should be empty");
        Assert.AreEqual("200", document.Values.BufferMegabytes, "The other fields should show the file's values");
        Assert.IsNull(document.FileProblem, "A wrong key should be no file problem");
    }

    /// <summary>
    /// A wrong or repeated <c>language</c> loads as not set, without a field problem.
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("""{"language": "fr"}""", DisplayName = "fr")]
    [DataRow("""{"language": 5}""", DisplayName = "Number")]
    [DataRow("""{"language": "de", "Language": "de"}""", DisplayName = "Twice")]
    public void CompanionSettingsDocumentLoadWrongLanguageIsNotSet(string text)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.IsNull(document.Values.Language, $"The language of {text} should load as not set");
        Assert.IsEmpty(document.FieldProblems, $"The language of {text} should be no field problem");
        Assert.IsNull(document.FileProblem, $"The language of {text} should be no file problem");
    }

    /// <summary>
    /// A file that cannot be parsed, or that holds a text that cannot be decoded at any depth, gives the defaults and the
    /// reason (E7).
    /// </summary>
    /// <param name="text">The settings text</param>
    /// <param name="reason">The expected reason</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("""{"mode": "local"<1 MiB>}""", "larger than 1 MiB", DisplayName = "Over 1 MiB")]
    [DataRow("""{"mode": "local" """, "not valid JSON", DisplayName = "Not JSON")]
    [DataRow("""[{"mode": "local"}]""", "not a JSON object", DisplayName = "Not an object")]
    [DataRow("""{"mode": "local", "theme": "da<FF>rk"}""", NotValidUtf8, DisplayName = "Invalid UTF-8 in an unknown value")]
    [DataRow("""{"mode": "local", "theme": "\uD800"}""", NotValidUtf8, DisplayName = "Lone surrogate in an unknown key's value")]
    [DataRow("""{"mode": "local", "gameFolder": "\uD800"}""", NotValidUtf8, DisplayName = "Lone surrogate in a known key")]
    [DataRow("""{"mode": "local", "window": {"wi\uD800dth": 800}}""", NotValidUtf8, DisplayName = "Lone surrogate in a nested name")]
    [DataRow("""{"mode": "local", "list": [1, ["\uD800"]]}""", NotValidUtf8, DisplayName = "Lone surrogate in a nested array")]
    public void CompanionSettingsDocumentLoadUnparseableGivesDefaultsAndReason(string text, string reason)
    {
        // Arrange
        var path = WriteSettings(text);

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.AreEqual(reason, document.FileProblem, "The file problem should be the reason the file cannot be used");
        Assert.IsEmpty(Differences(CreateDefaults(), document.Values), "A file that cannot be used should give the defaults");
        Assert.IsEmpty(document.FieldProblems, "A file that cannot be used should mark no field");
    }

    /// <summary>
    /// A switch from local to forward mode writes the new mode, address and token and keeps the port, the buffer limits and
    /// the game folder, in the fixed order and spelling (S2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionSettingsDocumentSaveModeSwitchKeepsOtherKeys()
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(LocalSettings));
        var values = Change(document.Values, CompanionSettingsMode.Forward, null, (SettingsField.ServerAddress, ServerAddress), (SettingsField.ServerToken, ServerToken));
        const string expected = """
                                {
                                  "mode": "forward",
                                  "localPort": 5117,
                                  "serverAddress": "ws://nas:8080/ingest",
                                  "serverToken": "ForwardToken-0123456789abcdefghijKLMNOP",
                                  "bufferHours": 48,
                                  "bufferMegabytes": 200,
                                  "gameFolder": "E:/Anno 117/"
                                }
                                """;

        // Act
        var write = document.Save(values);

        // Assert
        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.IsNull(write.Exception, "A save should carry no exception");
        Assert.AreEqual(expected.ReplaceLineEndings("\n"), ReadSavedText(), "The file should hold the new mode, address and token and keep the other keys");
    }

    /// <summary>
    /// Unknown keys stay with their values, after the known keys (E11).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionSettingsDocumentSaveKeepsUnknownKeysAndValues()
    {
        // Arrange
        const string text = """{"theme": "dark", "mode": "local", "window": {"width": 800, "gameFolder": 5, "title": "Größe"}, "list": [1, null, "x", true], "note": null}""";
        var document = CompanionSettingsDocument.Load(WriteSettings(text));
        using var original = JsonDocument.Parse(text);

        // Act
        var write = document.Save(document.Values);

        // Assert
        using var saved = ReadSaved();
        var names = saved.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        var expectedNames = new[] { "mode", "localPort", "bufferHours", "bufferMegabytes", "theme", "window", "list", "note" };

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreSequenceEqual(expectedNames, names, "The known keys should come first, then the unknown keys in the file's order");
        Assert.IsEmpty(UnequalUnknownKeys(original, saved, "theme", "window", "list", "note"), "Every unknown key should keep its value");
    }

    /// <summary>
    /// Saving mode Off removes the key <c>mode</c> and keeps the other keys (S2, N1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionSettingsDocumentSaveOffRemovesMode()
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(LocalSettings));
        var values = Change(document.Values, CompanionSettingsMode.Off, null);

        // Act
        var write = document.Save(values);

        // Assert
        using var saved = ReadSaved();
        var names = saved.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        var expectedNames = new[] { "localPort", "bufferHours", "bufferMegabytes", "gameFolder" };

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreSequenceEqual(expectedNames, names, "Off should write no mode and keep the other keys");
        Assert.AreEqual(LocalServerSettingKind.Off, CompanionSettings.ReadLocalServer(SettingsPath).Kind, "The file should read back as Off");
    }

    /// <summary>
    /// A known key written in other case or escaped is written once, in its own spelling (N1).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("""{"MODE": "local", "LocalPort": 5117}""", DisplayName = "MODE")]
    [DataRow("""{"\u006Dode": "local", "local\u0050ort": 5117}""", DisplayName = "Escaped mode")]
    public void CompanionSettingsDocumentSaveWritesEachKnownKeyOnceRemovingOtherCase(string text)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(text));
        var values = Change(document.Values, CompanionSettingsMode.Forward, null, (SettingsField.ServerAddress, ServerAddress));

        // Act
        var write = document.Save(values);

        // Assert
        using var saved = ReadSaved();
        var names = saved.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        var expectedNames = new[] { "mode", "localPort", "serverAddress", "bufferHours", "bufferMegabytes" };

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreSequenceEqual(expectedNames, names, $"Each known key of {text} should be written once in its own spelling");
        Assert.AreEqual("forward", saved.RootElement.GetProperty("mode").GetString(), "The mode should be the saved one");
        Assert.AreEqual(5117, saved.RootElement.GetProperty("localPort").GetInt32(), "The port should be the file's");
    }

    /// <summary>
    /// The saved file reads back, through the Companion's own readers and a new document, as the values saved (AC2).
    /// </summary>
    /// <param name="mode">The saved mode</param>
    /// <param name="expectedKind">The kind the server setting reads back as</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(CompanionSettingsMode.Local, LocalServerSettingKind.Local)]
    [DataRow(CompanionSettingsMode.Forward, LocalServerSettingKind.Forward)]
    public void CompanionSettingsDocumentSavedFileReadsBackAsSaved(CompanionSettingsMode mode, LocalServerSettingKind expectedKind)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(SettingsPath);
        var values = CreateValues(mode,
                                  CompanionLanguage.German,
                                  (SettingsField.LocalPort, "5117"),
                                  (SettingsField.ServerAddress, ServerAddress),
                                  (SettingsField.ServerToken, ServerToken),
                                  (SettingsField.BufferHours, "48"),
                                  (SettingsField.BufferMegabytes, "200"),
                                  (SettingsField.GameFolder, GameFolder));

        // Act
        var write = document.Save(values);

        // Assert
        var setting = CompanionSettings.ReadLocalServer(SettingsPath);

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreEqual(expectedKind, setting.Kind, "The server setting should read back as the saved mode");
        Assert.AreEqual(48, setting.BufferLimits.Hours, "The buffer hours should read back as saved");
        Assert.AreEqual(200, setting.BufferLimits.Megabytes, "The buffer megabytes should read back as saved");
        Assert.AreEqual(GameFolder, CompanionSettings.Read(SettingsPath).GameFolder, "The game folder should read back as saved");
        Assert.AreEqual(CompanionLanguage.German, CompanionSettings.ReadLanguage(SettingsPath), "The language should read back as saved");
        Assert.IsEmpty(Differences(values, CompanionSettingsDocument.Load(SettingsPath).Values), "A new document should show the saved values");
    }

    /// <summary>
    /// Switching to forward mode and back to local mode keeps the port, the address and the token, so neither needs to be
    /// entered again (AC2).
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CompanionSettingsDocumentSwitchBackRestoresEarlierValues()
    {
        // Arrange
        var first = CompanionSettingsDocument.Load(WriteSettings(LocalSettings));

        first.Save(Change(first.Values, CompanionSettingsMode.Forward, null, (SettingsField.ServerAddress, ServerAddress), (SettingsField.ServerToken, ServerToken)));

        var second = CompanionSettingsDocument.Load(SettingsPath);

        // Act
        var write = second.Save(Change(second.Values, CompanionSettingsMode.Local, null));

        // Assert
        var setting = CompanionSettings.ReadLocalServer(SettingsPath);
        var back = CompanionSettingsDocument.Load(SettingsPath).Values;

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "The file should read back as local mode");
        Assert.AreEqual(5117, setting.Port, "The port should be the one from before the switch");
        Assert.AreEqual(ServerAddress, back.ServerAddress, "The address should stay for the next switch");
        Assert.AreEqual(ServerToken, back.ServerToken, "The token should stay for the next switch");
    }

    /// <summary>
    /// A number is written as the parsed number, without its leading zeros (N5).
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="text">The text of the field</param>
    /// <param name="key">The key of the field</param>
    /// <param name="expected">The expected raw text of the number</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(SettingsField.LocalPort, "05", "localPort", "5")]
    [DataRow(SettingsField.BufferHours, "0024", "bufferHours", "24")]
    [DataRow(SettingsField.BufferMegabytes, "0100", "bufferMegabytes", "100")]
    public void CompanionSettingsDocumentSaveWritesParsedNumber(SettingsField field, string text, string key, string expected)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(SettingsPath);
        var values = Change(CreateDefaults(), CompanionSettingsMode.Local, null, (field, text));

        // Act
        var write = document.Save(values);

        // Assert
        using var saved = ReadSaved();
        var number = saved.RootElement.GetProperty(key);

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreEqual(JsonValueKind.Number, number.ValueKind, $"{key} should be written as a number");
        Assert.AreEqual(expected, number.GetRawText(), $"{key} should be written as the parsed number");
    }

    /// <summary>
    /// Without a language in the values the key <c>language</c> is absent, a wrong one in the file removed (S9).
    /// </summary>
    /// <param name="text">The settings text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("""{"mode": "local"}""", DisplayName = "No language")]
    [DataRow("""{"mode": "local", "language": "fr"}""", DisplayName = "Wrong language")]
    public void CompanionSettingsDocumentSaveWithoutLanguageLeavesKeyAbsent(string text)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(text));

        // Act
        var write = document.Save(document.Values);

        // Assert
        using var saved = ReadSaved();
        var languages = saved.RootElement.EnumerateObject().Where(property => string.Equals(property.Name, "language", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.IsEmpty(languages, $"Without a language the file of {text} should hold no language key");
    }

    /// <summary>
    /// Saving over a file that cannot be parsed replaces it with the values shown (E7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionSettingsDocumentSaveReplacesUnparseableFile()
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings("""{"mode": "local" """));
        var values = Change(document.Values, CompanionSettingsMode.Local, null, (SettingsField.LocalPort, "5117"));

        // Act
        var write = document.Save(values);

        // Assert
        var setting = CompanionSettings.ReadLocalServer(SettingsPath);

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should replace the file");
        Assert.AreEqual(LocalServerSettingKind.Local, setting.Kind, "The file should read back as local mode");
        Assert.AreEqual(5117, setting.Port, "The file should read back with the saved port");
    }

    /// <summary>
    /// A file changed, deleted or created after the document was loaded is not written (E10).
    /// </summary>
    /// <param name="change">How the file changed: changed, deleted or created</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("changed")]
    [DataRow("deleted")]
    [DataRow("created")]
    public void CompanionSettingsDocumentSaveChangedOutsideWritesNothing(string change)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(PathBefore(change));
        var outside = ChangeOutside(change);

        // Act
        var write = document.Save(Change(document.Values, CompanionSettingsMode.Forward, null, (SettingsField.ServerAddress, ServerAddress)));

        // Assert
        Assert.AreEqual(CompanionSettingsWriteKind.ChangedOutside, write.Kind, $"A file {change} outside should not be saved");
        Assert.AreSequenceEqual(outside, ReadBytesOrNone(SettingsPath), $"The file {change} outside should stay as it is");
        Assert.IsFalse(File.Exists(TmpPath), "No temporary file should be left");
    }

    /// <summary>
    /// A write that fails leaves the old file whole and returns the exception.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionSettingsDocumentSaveFailureKeepsOldFile()
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(LocalSettings));
        var old = File.ReadAllBytes(SettingsPath);

        Directory.CreateDirectory(TmpPath);

        // Act
        var write = document.Save(Change(document.Values, CompanionSettingsMode.Forward, null, (SettingsField.ServerAddress, ServerAddress)));

        // Assert
        Assert.AreEqual(CompanionSettingsWriteKind.Failed, write.Kind, "A temporary file that cannot be written should fail the save");
        Assert.IsInstanceOfType<UnauthorizedAccessException>(write.Exception, "The failure should carry the exception of the write");
        Assert.AreSequenceEqual(old, File.ReadAllBytes(SettingsPath), "The old file should stay whole");
    }

    /// <summary>
    /// A save leaves no temporary file, also over one left by an earlier crash.
    /// </summary>
    /// <param name="leftover">The name of a file left in the folder from before</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("settings.json.tmp", DisplayName = "Temporary file left over")]
    [DataRow("other.txt", DisplayName = "No temporary file")]
    public void CompanionSettingsDocumentSaveLeavesNoTmpFile(string leftover)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(WriteSettings(LocalSettings));

        File.WriteAllText(Path.Combine(_root, leftover), "left over");

        // Act
        var write = document.Save(document.Values);

        // Assert
        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.IsFalse(File.Exists(TmpPath), "No temporary file should be left");
        Assert.AreEqual(LocalServerSettingKind.Local, CompanionSettings.ReadLocalServer(SettingsPath).Kind, "The saved file should be the new one");
    }

    /// <summary>
    /// A save over a settings file that cannot be read any more fails with the exception, writes nothing and leaves no
    /// temporary file.
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionSettingsDocumentSaveOverUnreadableFileFails()
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(SettingsPath);

        Directory.CreateDirectory(SettingsPath);

        // Act
        var write = document.Save(CreateValues(CompanionSettingsMode.Local, null));

        // Assert
        Assert.AreEqual(CompanionSettingsWriteKind.Failed, write.Kind, "A settings file that cannot be read should fail the save");
        Assert.IsInstanceOfType<UnauthorizedAccessException>(write.Exception, "The failure should carry the exception of the read");
        Assert.IsTrue(Directory.Exists(SettingsPath), "The folder in place of the file should stay");
        Assert.IsFalse(File.Exists(TmpPath), "No temporary file should be left");
    }

    /// <summary>
    /// A folder in place of the settings file cannot be read: the document holds the defaults and the reason (E7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionSettingsDocumentLoadUnreadableGivesDefaultsAndReason()
    {
        // Arrange
        Directory.CreateDirectory(SettingsPath);

        // Act
        var document = CompanionSettingsDocument.Load(SettingsPath);

        // Assert
        Assert.AreEqual("cannot be read", document.FileProblem, "A file that cannot be read should give its reason");
        Assert.IsEmpty(Differences(CreateDefaults(), document.Values), "A file that cannot be read should give the default of every field");
        Assert.IsEmpty(document.FieldProblems, "A file that cannot be read should give no field problem");
    }

    /// <summary>
    /// Empty texts of the address, the token and the game folder load as empty fields without a problem.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void CompanionSettingsDocumentLoadEmptyTextsAreNoProblem()
    {
        // Arrange
        var path = WriteSettings("""{"mode": "local", "serverAddress": "", "serverToken": "", "gameFolder": ""}""");

        // Act
        var document = CompanionSettingsDocument.Load(path);

        // Assert
        Assert.IsEmpty(Differences(CreateValues(CompanionSettingsMode.Local, null), document.Values), "Empty texts should load as empty fields");
        Assert.IsEmpty(document.FieldProblems, "Empty texts should give no field problem");
    }

    /// <summary>
    /// An empty number field leaves its key out of the file, so the reader takes its default.
    /// </summary>
    /// <param name="field">The empty field</param>
    /// <param name="key">The key of the field</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(SettingsField.LocalPort, "localPort")]
    [DataRow(SettingsField.BufferHours, "bufferHours")]
    [DataRow(SettingsField.BufferMegabytes, "bufferMegabytes")]
    public void CompanionSettingsDocumentSaveEmptyNumberLeavesKeyAbsent(SettingsField field, string key)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(SettingsPath);
        var values = Change(CreateDefaults(), CompanionSettingsMode.Local, null, (field, string.Empty));

        // Act
        var write = document.Save(values);

        // Assert
        using var saved = ReadSaved();

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.IsFalse(saved.RootElement.TryGetProperty(key, out _), $"An empty {field} should leave {key} out of the file");
    }

    /// <summary>
    /// The language is written as <c>en</c> or <c>de</c> (S9).
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected value of the key</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(CompanionLanguage.English, "en")]
    [DataRow(CompanionLanguage.German, "de")]
    public void CompanionSettingsDocumentSaveWritesLanguage(CompanionLanguage language, string expected)
    {
        // Arrange
        var document = CompanionSettingsDocument.Load(SettingsPath);

        // Act
        var write = document.Save(CreateValues(CompanionSettingsMode.Local, language));

        // Assert
        using var saved = ReadSaved();

        Assert.AreEqual(CompanionSettingsWriteKind.Saved, write.Kind, "The save should succeed");
        Assert.AreEqual(expected, saved.RootElement.GetProperty("language").GetString(), $"The language {language} should be written as {expected}");
    }

    /// <summary>
    /// A changed mode, port, address, token, buffer limit or game folder needs a restart; a number is compared as the
    /// parsed number and an invalid one as its text (S3).
    /// </summary>
    /// <param name="field">The changed field</param>
    /// <param name="running">The text the running Companion started with</param>
    /// <param name="saved">The saved text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(SettingsField.Mode, "Off", "Local", DisplayName = "Mode")]
    [DataRow(SettingsField.LocalPort, "5000", "5001", DisplayName = "Port")]
    [DataRow(SettingsField.LocalPort, "", "5001", DisplayName = "Empty port against another port")]
    [DataRow(SettingsField.LocalPort, "abc", "abd", DisplayName = "Two invalid ports")]
    [DataRow(SettingsField.ServerAddress, "", ServerAddress, DisplayName = "Address")]
    [DataRow(SettingsField.ServerToken, "", ServerToken, DisplayName = "Token")]
    [DataRow(SettingsField.BufferHours, "24", "48", DisplayName = "Hours")]
    [DataRow(SettingsField.BufferMegabytes, "100", "200", DisplayName = "Megabytes")]
    [DataRow(SettingsField.GameFolder, "", GameFolder, DisplayName = "Game folder")]
    public void CompanionSettingsDocumentNeedsRestartWhenSettingDiffers(SettingsField field, string running, string saved)
    {
        // Arrange
        var runningValues = CreateValuesWith(field, running);
        var savedValues = CreateValuesWith(field, saved);

        // Act
        var needsRestart = CompanionSettingsDocument.NeedsRestart(runningValues, savedValues);

        // Assert
        Assert.IsTrue(needsRestart, $"{field} changed from '{running}' to '{saved}' should need a restart");
    }

    /// <summary>
    /// The language, another spelling of the same number, an empty number against its default, a token with spaces
    /// around it and the same invalid text need no restart (S3, S4).
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="running">The text the running Companion started with</param>
    /// <param name="saved">The saved text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(SettingsField.Language, "", "German", DisplayName = "Language")]
    [DataRow(SettingsField.Language, "English", "German", DisplayName = "Language switched")]
    [DataRow(SettingsField.LocalPort, "5", "05", DisplayName = "Port 05")]
    [DataRow(SettingsField.LocalPort, "", "5000", DisplayName = "Empty port against the default")]
    [DataRow(SettingsField.LocalPort, "abc", "abc", DisplayName = "Same invalid port")]
    [DataRow(SettingsField.BufferHours, "24", "0024", DisplayName = "Hours 0024")]
    [DataRow(SettingsField.BufferHours, "", "24", DisplayName = "Empty hours against the default")]
    [DataRow(SettingsField.BufferMegabytes, "100", "0100", DisplayName = "Megabytes 0100")]
    [DataRow(SettingsField.BufferMegabytes, "", "100", DisplayName = "Empty megabytes against the default")]
    [DataRow(SettingsField.ServerToken, ServerToken, " \t" + ServerToken + "\r\n", DisplayName = "Token with spaces around")]
    public void CompanionSettingsDocumentNeedsRestartSameSettingsNeedNoRestart(SettingsField field, string running, string saved)
    {
        // Arrange
        var runningValues = CreateValuesWith(field, running);
        var savedValues = CreateValuesWith(field, saved);

        // Act
        var needsRestart = CompanionSettingsDocument.NeedsRestart(runningValues, savedValues);

        // Assert
        Assert.IsFalse(needsRestart, $"{field} from '{running}' to '{saved}' should need no restart");
    }

    /// <summary>
    /// Creates the values of the defaults.
    /// </summary>
    /// <returns>The values</returns>
    private static CompanionSettingsValues CreateDefaults()
    {
        return CreateValues(CompanionSettingsMode.Off, null);
    }

    /// <summary>
    /// Creates values from the defaults with some field texts replaced.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="language">The language</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The values</returns>
    private static CompanionSettingsValues CreateValues(CompanionSettingsMode mode, CompanionLanguage? language, params (SettingsField Field, string Text)[] texts)
    {
        var map = texts.ToDictionary(text => text.Field, text => text.Text);

        return new CompanionSettingsValues
               {
                   Mode = mode,
                   LocalPort = map.GetValueOrDefault(SettingsField.LocalPort, "5000"),
                   ServerAddress = map.GetValueOrDefault(SettingsField.ServerAddress, string.Empty),
                   ServerToken = map.GetValueOrDefault(SettingsField.ServerToken, string.Empty),
                   BufferHours = map.GetValueOrDefault(SettingsField.BufferHours, "24"),
                   BufferMegabytes = map.GetValueOrDefault(SettingsField.BufferMegabytes, "100"),
                   GameFolder = map.GetValueOrDefault(SettingsField.GameFolder, string.Empty),
                   Language = language
               };
    }

    /// <summary>
    /// Creates values of local mode with one field set: the mode or the language by its name (empty for no language), any
    /// other field by its text.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="text">The text of the field</param>
    /// <returns>The values</returns>
    private static CompanionSettingsValues CreateValuesWith(SettingsField field, string text)
    {
        return field switch
               {
                   SettingsField.Mode => CreateValues(Enum.Parse<CompanionSettingsMode>(text), null),
                   SettingsField.Language => CreateValues(CompanionSettingsMode.Local, text.Length == 0 ? null : Enum.Parse<CompanionLanguage>(text)),
                   _ => CreateValues(CompanionSettingsMode.Local, null, (field, text))
               };
    }

    /// <summary>
    /// Creates a copy of values with another mode, the language or, when given, another language, and some field texts
    /// replaced.
    /// </summary>
    /// <param name="source">The values</param>
    /// <param name="mode">The mode</param>
    /// <param name="language">The language, or <see langword="null" /> to keep the one of the values</param>
    /// <param name="texts">The replaced field texts</param>
    /// <returns>The copy</returns>
    private static CompanionSettingsValues Change(CompanionSettingsValues source, CompanionSettingsMode mode, CompanionLanguage? language, params (SettingsField Field, string Text)[] texts)
    {
        var map = texts.ToDictionary(text => text.Field, text => text.Text);

        return new CompanionSettingsValues
               {
                   Mode = mode,
                   LocalPort = map.GetValueOrDefault(SettingsField.LocalPort, source.LocalPort),
                   ServerAddress = map.GetValueOrDefault(SettingsField.ServerAddress, source.ServerAddress),
                   ServerToken = map.GetValueOrDefault(SettingsField.ServerToken, source.ServerToken),
                   BufferHours = map.GetValueOrDefault(SettingsField.BufferHours, source.BufferHours),
                   BufferMegabytes = map.GetValueOrDefault(SettingsField.BufferMegabytes, source.BufferMegabytes),
                   GameFolder = map.GetValueOrDefault(SettingsField.GameFolder, source.GameFolder),
                   Language = language ?? source.Language
               };
    }

    /// <summary>
    /// Gets the text of a field of values.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="field">The field</param>
    /// <returns>The text, or <see langword="null" /> for a field without a text</returns>
    private static string? TextOf(CompanionSettingsValues values, SettingsField field)
    {
        return field switch
               {
                   SettingsField.LocalPort => values.LocalPort,
                   SettingsField.ServerAddress => values.ServerAddress,
                   SettingsField.ServerToken => values.ServerToken,
                   SettingsField.BufferHours => values.BufferHours,
                   SettingsField.BufferMegabytes => values.BufferMegabytes,
                   SettingsField.GameFolder => values.GameFolder,
                   SettingsField.Mode => values.Mode == CompanionSettingsMode.Off ? string.Empty : values.Mode.ToString(),
                   _ => null
               };
    }

    /// <summary>
    /// Gets the names of the fields in which two values differ.
    /// </summary>
    /// <param name="expected">The expected values</param>
    /// <param name="actual">The actual values</param>
    /// <returns>The names with both values</returns>
    private static List<string> Differences(CompanionSettingsValues expected, CompanionSettingsValues actual)
    {
        var pairs = new (string Name, object? Expected, object? Actual)[]
                    {
                        (nameof(CompanionSettingsValues.Mode), expected.Mode, actual.Mode),
                        (nameof(CompanionSettingsValues.LocalPort), expected.LocalPort, actual.LocalPort),
                        (nameof(CompanionSettingsValues.ServerAddress), expected.ServerAddress, actual.ServerAddress),
                        (nameof(CompanionSettingsValues.ServerToken), expected.ServerToken, actual.ServerToken),
                        (nameof(CompanionSettingsValues.BufferHours), expected.BufferHours, actual.BufferHours),
                        (nameof(CompanionSettingsValues.BufferMegabytes), expected.BufferMegabytes, actual.BufferMegabytes),
                        (nameof(CompanionSettingsValues.GameFolder), expected.GameFolder, actual.GameFolder),
                        (nameof(CompanionSettingsValues.Language), expected.Language, actual.Language)
                    };

        return pairs.Where(pair => Equals(pair.Expected, pair.Actual) == false)
                    .Select(pair => $"{pair.Name}: expected {pair.Expected}, got {pair.Actual}")
                    .ToList();
    }

    /// <summary>
    /// Gets the unknown keys whose value differs between two documents.
    /// </summary>
    /// <param name="original">The original document</param>
    /// <param name="saved">The saved document</param>
    /// <param name="keys">The unknown keys</param>
    /// <returns>The keys whose value differs</returns>
    private static List<string> UnequalUnknownKeys(JsonDocument original, JsonDocument saved, params string[] keys)
    {
        return keys.Where(key => saved.RootElement.TryGetProperty(key, out var value) == false || JsonElement.DeepEquals(original.RootElement.GetProperty(key), value) == false)
                   .ToList();
    }

    /// <summary>
    /// Turns a test text into the bytes of the file: <see cref="InvalidByteMarker" /> becomes the byte <c>0xFF</c> and
    /// <see cref="PaddingMarker" /> spaces of 1 MiB, the rest is encoded as UTF-8.
    /// </summary>
    /// <param name="text">The test text</param>
    /// <returns>The bytes</returns>
    private static byte[] ToBytes(string text)
    {
        var padded = text.Replace(PaddingMarker, new string(' ', CompanionSettings.MaxFileLength), StringComparison.Ordinal);

        return padded.Split(InvalidByteMarker)
                     .Select(Encoding.UTF8.GetBytes)
                     .Aggregate((left, right) => [.. left, 0xFF, .. right]);
    }

    /// <summary>
    /// Reads the bytes of a file, or none when it is missing.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The bytes, empty for a missing file</returns>
    private static byte[] ReadBytesOrNone(string path)
    {
        return File.Exists(path) ? File.ReadAllBytes(path) : [];
    }

    /// <summary>
    /// Writes the settings file.
    /// </summary>
    /// <param name="text">The test text, see <see cref="ToBytes" /></param>
    /// <returns>The path of the file</returns>
    private string WriteSettings(string text)
    {
        File.WriteAllBytes(SettingsPath, ToBytes(text));

        return SettingsPath;
    }

    /// <summary>
    /// Gets the path of the settings file before it changes outside: missing for a file created later, otherwise written
    /// with <see cref="LocalSettings" />.
    /// </summary>
    /// <param name="change">How the file changes: changed, deleted or created</param>
    /// <returns>The path</returns>
    private string PathBefore(string change)
    {
        return change == "created" ? SettingsPath : WriteSettings(LocalSettings);
    }

    /// <summary>
    /// Changes the settings file outside the document.
    /// </summary>
    /// <param name="change">How: changed, deleted or created</param>
    /// <returns>The bytes the file holds afterwards, empty when deleted</returns>
    private byte[] ChangeOutside(string change)
    {
        if (change == "deleted")
        {
            File.Delete(SettingsPath);

            return [];
        }

        WriteSettings("""{"mode": "local", "localPort": 6000}""");

        return File.ReadAllBytes(SettingsPath);
    }

    /// <summary>
    /// Reads the saved settings file as a JSON document.
    /// </summary>
    /// <returns>The document</returns>
    private JsonDocument ReadSaved()
    {
        return JsonDocument.Parse(File.ReadAllBytes(SettingsPath));
    }

    /// <summary>
    /// Reads the saved settings file as text with line feeds only and without a final line break.
    /// </summary>
    /// <returns>The text</returns>
    private string ReadSavedText()
    {
        return File.ReadAllText(SettingsPath).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    #endregion // Methods
}