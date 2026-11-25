using System.Security.Cryptography;
using System.Text;

using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionProfiles" />.
/// </summary>
[TestClass]
public sealed class CompanionProfilesTests
{
    #region Constants

    /// <summary>
    /// The identity of the profile <c>Live</c>, as the file writes it.
    /// </summary>
    private const string LiveId = "b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94";

    /// <summary>
    /// The identity of the second profile of the test files.
    /// </summary>
    private const string SecondId = "0f8c2d3e-1111-4a2b-8c3d-00000000a002";

    /// <summary>
    /// An identity no profile of the test files has.
    /// </summary>
    private const string OtherId = "7d0e4c1b-2222-4f3a-9b4c-00000000b003";

    /// <summary>
    /// The name of the second profile of the test files, a marker no log line may hold.
    /// </summary>
    private const string SecondName = "MARKER-Second";

    /// <summary>
    /// The figure the test files link to the second profile, a marker no log line may hold.
    /// </summary>
    private const string FigureA = "MARKER-Figure-A";

    /// <summary>
    /// A figure no test file links.
    /// </summary>
    private const string FigureB = "MARKER-Figure-B";

    /// <summary>
    /// The marker in a test text that stands for the invalid UTF-8 byte <c>0xFF</c>.
    /// </summary>
    private const string InvalidByteMarker = "<FF>";

    /// <summary>
    /// The marker in a test text that stands for the UTF-8 byte order mark.
    /// </summary>
    private const string ByteOrderMarkMarker = "<BOM>";

    /// <summary>
    /// The beginning of the warning of an unusable profiles file.
    /// </summary>
    private const string InvalidLinePrefix = "Profiles file invalid: ";

    /// <summary>
    /// The beginning of the warning of a failed write.
    /// </summary>
    private const string NotSavedLinePrefix = "Could not save the settings";

    /// <summary>
    /// A valid profiles file: <c>Live</c> and the second profile, the second active, figure A linked to it.
    /// </summary>
    private const string ValidFile = """{"version":1,"active":"{second}","profiles":[{"id":"{live}","name":"Live"},{"id":"{second}","name":"MARKER-Second"}],"figures":[{"figure":"{key}","profile":"{second}"}]}""";

    /// <summary>
    /// The file the writer writes for <c>Live</c>, the created profile <c>{created}</c> named <c>Zweité</c> and active, and
    /// figure A linked to it.
    /// </summary>
    private const string PlannedFile = """{"version":1,"active":"{created}","profiles":[{"id":"b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94","name":"Live"},{"id":"{created}","name":"Zweit\u00E9"}],"figures":[{"figure":"{key}","profile":"{created}"}]}""";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-profiles-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The logger of the profiles.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionProfilesTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public CompanionProfilesTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the path of the profiles file in the temporary folder.
    /// </summary>
    private string ProfilesPath => Path.Combine(_root, "profiles.json");

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
    /// Without a file the profiles are <c>Live</c> alone, active and valid, and the load writes and logs nothing (E16).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionProfilesLoadMissingFileGivesLiveAlone()
    {
        // Arrange
        var path = ProfilesPath;

        // Act
        var profiles = CompanionProfiles.Load(path, _logger);

        // Assert
        Assert.AreSequenceEqual([CompanionProfile.Live.Id], profiles.Profiles.Select(profile => profile.Id).ToArray(), "A missing file should give Live alone");
        Assert.AreEqual("Live", profiles.Profiles[0].Name, "The one profile should be named Live");
        Assert.AreEqual(CompanionProfile.Live.Id, profiles.Active.Id, "Live should be active");
        Assert.IsFalse(profiles.IsFileInvalid, "A missing file should be valid");
        Assert.IsFalse(File.Exists(path), "The load should write no file");
        Assert.IsEmpty(_logger.Entries, "A missing file should log nothing");
    }

    /// <summary>
    /// A valid file gives its profiles in file order, its active profile and its figure links (G3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionProfilesLoadValidFileReadsProfilesActiveAndLinks()
    {
        // Arrange
        var path = WriteFile(Expand(ValidFile));

        // Act
        var (profiles, notices) = LoadAndObserveOnLive(path, FigureA);

        // Assert
        Assert.AreSequenceEqual([CompanionProfile.Live.Id, new Guid(SecondId)], profiles.Profiles.Select(profile => profile.Id).ToArray(), "The profiles should be read in file order");
        Assert.AreSequenceEqual(["Live", SecondName], profiles.Profiles.Select(profile => profile.Name).ToArray(), "The names should be read");
        Assert.IsFalse(profiles.IsFileInvalid, "A valid file should not be invalid");
        Assert.AreEqual(SecondName, profiles.NameOf(new Guid(SecondId)), "The name of the second profile should be found by its identity");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe(notices), "Figure A should be read as linked to the second profile");
        Assert.IsEmpty(_logger.Entries, "A valid file should log nothing");
    }

    /// <summary>
    /// The identities are read in either case and compared as values: <c>Live</c>'s entry, the active profile and the
    /// figure's profile in uppercase are the same profiles (G3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionProfilesLoadAcceptsUppercaseIds()
    {
        // Arrange
        var text = Expand(ValidFile.Replace("{live}", "{liveUpper}", StringComparison.Ordinal).Replace("{second}", "{secondUpper}", StringComparison.Ordinal));
        var path = WriteFile(text);

        // Act
        var (profiles, notices) = LoadAndObserveOnLive(path, FigureA);

        // Assert
        Assert.Contains(LiveId.ToUpperInvariant(), text, "The file should hold the identities in uppercase");
        Assert.IsFalse(profiles.IsFileInvalid, "Uppercase identities should be valid");
        Assert.AreSequenceEqual([CompanionProfile.Live.Id, new Guid(SecondId)], profiles.Profiles.Select(profile => profile.Id).ToArray(), "Live's entry and the second profile should be read");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe(notices), "The figure should be linked to the second profile");
    }

    /// <summary>
    /// Whitespace and line breaks between the tokens are allowed (G3).
    /// </summary>
    /// <param name="find">The text of the valid file to replace</param>
    /// <param name="replacement">The replacement</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("""{"version":1,""", "{\r\n  \"version\" : 1 ,\r\n  ", DisplayName = "Whitespace and line breaks")]
    public void CompanionProfilesLoadAcceptsEquivalentForms(string find, string replacement)
    {
        // Arrange
        var path = WriteFile(Mutate(find, replacement));

        // Act
        var (profiles, notices) = LoadAndObserveOnLive(path, FigureA);

        // Assert
        Assert.IsFalse(profiles.IsFileInvalid, "The form should be valid");
        Assert.HasCount(2, profiles.Profiles, "Both profiles should be read");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe(notices), "The figure should be linked to the second profile");
    }

    /// <summary>
    /// A property name is compared on its unescaped text, so an escaped <c>name</c> is the name; the escape is kept in the
    /// file (G3).
    /// </summary>
    /// <param name="find">The text of the valid file to replace</param>
    /// <param name="replacement">The replacement</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("""{"id":"{live}","name":"Live"}""", """{"id":"{live}","n\u0061me":"Live"}""", DisplayName = "Escaped property name")]
    public void CompanionProfilesLoadAcceptsEscapedPropertyName(string find, string replacement)
    {
        // Arrange
        var text = Mutate(find, replacement);
        var path = WriteFile(text);

        // Act
        var (profiles, notices) = LoadAndObserveOnLive(path, FigureA);

        // Assert
        Assert.Contains("\\u0061", text, "The file should still hold the escape");
        Assert.IsFalse(profiles.IsFileInvalid, "An escaped property name should be valid");
        Assert.AreEqual("Live", profiles.Profiles[0].Name, "The escaped property should be read as the name");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe(notices), "The figure should be linked to the second profile");
    }

    /// <summary>
    /// Every form outside the reader's rules fails closed: <c>Live</c> alone and active, the file invalid, one warning that
    /// quotes no value, and no figure linked, so a second figure gives no notice either (E16).
    /// </summary>
    /// <param name="find">The text of the valid file to replace</param>
    /// <param name="replacement">The replacement</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("""{"version":""", """<BOM>{"version":""", DisplayName = "Byte order mark")]
    [DataRow(""","figures":""", """,/* note */"figures":""", DisplayName = "Comment")]
    [DataRow("}]}", "}],}", DisplayName = "Trailing comma")]
    [DataRow(ValidFile, "[" + ValidFile + "]", DisplayName = "Root an array")]
    [DataRow(ValidFile, "1", DisplayName = "Root a number")]
    [DataRow(ValidFile, "not json", DisplayName = "Not JSON")]
    [DataRow(ValidFile, "", DisplayName = "Empty file")]
    [DataRow(ValidFile, "{}", DisplayName = "Empty object")]
    [DataRow("""{"version":1,""", """{"version":"1",""", DisplayName = "Version a text")]
    [DataRow("""{"version":1,""", """{"version":1.0,""", DisplayName = "Version 1.0")]
    [DataRow("""{"version":1,""", """{"version":1e0,""", DisplayName = "Version 1e0")]
    [DataRow("""{"version":1,""", """{"version":2,""", DisplayName = "Version 2")]
    [DataRow("""{"version":1,""", "{", DisplayName = "Version missing")]
    [DataRow("""{"version":1,""", """{"version":1,"version":1,""", DisplayName = "Version twice")]
    [DataRow("""{"version":1,""", """{"version":1,"extra":0,""", DisplayName = "Unknown root property")]
    [DataRow("""{"version":""", """{"Version":""", DisplayName = "Root property in other case")]
    [DataRow(""","active":"{second}",""", ""","active":"second",""", DisplayName = "Active not an identity")]
    [DataRow(""","active":"{second}",""", ""","active":"{secondN}",""", DisplayName = "Active without hyphens")]
    [DataRow(""","active":"{second}",""", ""","active":"({second})",""", DisplayName = "Active in parentheses")]
    [DataRow(""","active":"{second}",""", ""","active":"{other}",""", DisplayName = "Active unknown")]
    [DataRow(""","active":"{second}",""", ""","active":5,""", DisplayName = "Active a number")]
    [DataRow(""","active":"{second}",""", ",", DisplayName = "Active missing")]
    [DataRow(""","profiles":[{"id":"{live}","name":"Live"},{"id":"{second}","name":"MARKER-Second"}],""", ""","profiles":{},""", DisplayName = "Profiles an object")]
    [DataRow(""","profiles":[{"id":"{live}","name":"Live"},{"id":"{second}","name":"MARKER-Second"}],""", ""","profiles":[],""", DisplayName = "No profile")]
    [DataRow("""{"id":"{live}","name":"Live"},""", "5,", DisplayName = "Profile a number")]
    [DataRow(""","name":"Live"}""", ""","name":"Live","extra":0}""", DisplayName = "Unknown profile property")]
    [DataRow("""{"id":"{live}",""", """{"ID":"{live}",""", DisplayName = "Profile property in other case")]
    [DataRow("""{"id":"{live}",""", """{"id":"{live}","id":"{live}",""", DisplayName = "Profile identity twice")]
    [DataRow(""","name":"Live"}""", ""","name":"Live","name":"Live"}""", DisplayName = "Profile name twice")]
    [DataRow(""","name":"Live"}""", "}", DisplayName = "Profile name missing")]
    [DataRow("""{"id":"{live}",""", """{"id":"live",""", DisplayName = "Profile identity not an identity")]
    [DataRow("""{"id":"{live}",""", """{"id":7,""", DisplayName = "Profile identity a number")]
    [DataRow("""{"id":"{live}","name":"Live"}""", """{"id":"{live}","name":"Live"},{"id":"{liveUpper}","name":"Copy"}""", DisplayName = "Identity twice in other case")]
    [DataRow("""{"id":"{live}",""", """{"id":"{other}",""", DisplayName = "Live missing")]
    [DataRow(""","name":"Live"}""", ""","name":""}""", DisplayName = "Name empty")]
    [DataRow(""","name":"Live"}""", ""","name":"   "}""", DisplayName = "Name blank")]
    [DataRow(""","name":"Live"}""", ""","name":"{long}"}""", DisplayName = "Name of 65 characters")]
    [DataRow(""","name":"MARKER-Second"}""", ""","name":"LIVE"}""", DisplayName = "Names equal ignoring case")]
    [DataRow(""","name":"Live"}""", ""","name":5}""", DisplayName = "Name a number")]
    [DataRow(""","name":"Live"}""", ""","name":null}""", DisplayName = "Name null")]
    [DataRow(""","name":"Live"}""", ""","name":"Li<FF>ve"}""", DisplayName = "Name not valid UTF-8")]
    [DataRow(""","figures":[{"figure":"{key}","profile":"{second}"}]""", ""","figures":{}""", DisplayName = "Figures an object")]
    [DataRow(""","figures":[{"figure":"{key}","profile":"{second}"}]""", ""","figures":[5]""", DisplayName = "Figure a number")]
    [DataRow("""{"figure":"{key}",""", """{"figure":"0123abcd",""", DisplayName = "Figure key too short")]
    [DataRow("""{"figure":"{key}",""", """{"figure":"{key}0",""", DisplayName = "Figure key too long")]
    [DataRow("""{"figure":"{key}",""", """{"figure":"{keyUpper}",""", DisplayName = "Figure key in uppercase")]
    [DataRow("""{"figure":"{key}",""", """{"figure":"{nonHex}",""", DisplayName = "Figure key not hexadecimal")]
    [DataRow("""{"figure":"{key}",""", """{"figure":5,""", DisplayName = "Figure key a number")]
    [DataRow("""{"figure":"{key}",""", """{"Figure":"{key}",""", DisplayName = "Figure property in other case")]
    [DataRow("""{"figure":"{key}","profile":"{second}"}""", """{"figure":"{key}","profile":"{second}"},{"figure":"{key}","profile":"{live}"}""", DisplayName = "Figure key twice")]
    [DataRow(""","profile":"{second}"}""", ""","profile":"{other}"}""", DisplayName = "Figure of an unknown profile")]
    [DataRow(""","profile":"{second}"}""", ""","profile":"second"}""", DisplayName = "Figure profile not an identity")]
    [DataRow(""","profile":"{second}"}""", ""","profile":"{second}","extra":0}""", DisplayName = "Unknown figure property")]
    [DataRow(""","profile":"{second}"}""", "}", DisplayName = "Figure profile missing")]
    public void CompanionProfilesInvalidFileUsesLiveAndLinksNothing(string find, string replacement)
    {
        // Arrange
        var path = WriteFile(Mutate(find, replacement));

        // Act
        var (profiles, notices) = LoadAndObserveTwoFigures(path);

        // Assert
        AssertFailedClosed(profiles, notices);
    }

    /// <summary>
    /// A name that holds an escaped lone surrogate or control character fails closed as well; the escape is kept in the
    /// file (E16, record 0033).
    /// </summary>
    /// <param name="find">The text of the valid file to replace</param>
    /// <param name="replacement">The replacement</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(""","name":"Live"}""", ""","name":"Li\uD800ve"}""", DisplayName = "Lone high surrogate")]
    [DataRow(""","name":"Live"}""", ""","name":"Li\uDC00ve"}""", DisplayName = "Lone low surrogate")]
    [DataRow(""","name":"Live"}""", ""","name":"Li\u0007ve"}""", DisplayName = "Control character")]
    [DataRow(""","name":"Live"}""", ""","name":"Li\u0000ve"}""", DisplayName = "Null character")]
    [DataRow(""","name":"Live"}""", ""","n\uD800me":"Live"}""", DisplayName = "Lone surrogate in a property name")]
    public void CompanionProfilesInvalidEscapedNameUsesLiveAndLinksNothing(string find, string replacement)
    {
        // Arrange
        var text = Mutate(find, replacement);
        var path = WriteFile(text);

        // Act
        var (profiles, notices) = LoadAndObserveTwoFigures(path);

        // Assert
        Assert.Contains("\\u", text, "The file should still hold the escape");
        AssertFailedClosed(profiles, notices);
    }

    /// <summary>
    /// A file of more than 1,000 profiles or more than 1,000 figures fails closed (E16).
    /// </summary>
    /// <param name="profileCount">The number of profiles, <c>Live</c> included</param>
    /// <param name="figureCount">The number of figures</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(CompanionProfiles.MaxProfiles + 1, 1, DisplayName = "1,001 profiles")]
    [DataRow(2, CompanionProfiles.MaxFigures + 1, DisplayName = "1,001 figures")]
    public void CompanionProfilesTooManyEntriesUsesLiveAndLinksNothing(int profileCount, int figureCount)
    {
        // Arrange
        var path = WriteFile(BuildFile(profileCount, figureCount));

        // Act
        var (profiles, notices) = LoadAndObserveTwoFigures(path);

        // Assert
        AssertFailedClosed(profiles, notices);
    }

    /// <summary>
    /// A file of exactly <see cref="CompanionProfiles.MaxFileLength" /> bytes is read; one byte more fails closed (AC9).
    /// </summary>
    /// <param name="length">The length of the file in bytes</param>
    /// <param name="isInvalid">Whether the file is invalid</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(CompanionProfiles.MaxFileLength, false, DisplayName = "At the bound")]
    [DataRow(CompanionProfiles.MaxFileLength + 1, true, DisplayName = "Above the bound")]
    public void CompanionProfilesFileAboveBoundIsInvalid(int length, bool isInvalid)
    {
        // Arrange
        var text = Expand(ValidFile);
        var bytes = Encoding.UTF8.GetBytes(text + new string(' ', length - Encoding.UTF8.GetByteCount(text)));

        File.WriteAllBytes(ProfilesPath, bytes);

        // Act
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Assert
        Assert.HasCount(length, bytes, "The file should have the length of the case");
        Assert.AreEqual(isInvalid, profiles.IsFileInvalid, "Only a file above the bound should be invalid");
        Assert.HasCount(isInvalid ? 1 : 2, profiles.Profiles, "A file above the bound should give Live alone");
    }

    /// <summary>
    /// A path that cannot be read as a file fails closed (E16).
    /// </summary>
    [TestMethod]
    [TestCategory("AC8")]
    public void CompanionProfilesLoadUnreadableFileIsInvalid()
    {
        // Arrange
        Directory.CreateDirectory(ProfilesPath);

        // Act
        var (profiles, notices) = LoadAndObserveTwoFigures(ProfilesPath);

        // Assert
        AssertFailedClosed(profiles, notices);
    }

    /// <summary>
    /// An unusable file stays as it was while figures are observed, and is replaced by a valid file once a profile is
    /// created or selected (E16).
    /// </summary>
    /// <param name="change">The change made while the writer runs: observe, create or select</param>
    /// <param name="isReplaced">Whether the file is replaced</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("observe", false, DisplayName = "Figures observed")]
    [DataRow("create", true, DisplayName = "Profile created")]
    [DataRow("select", true, DisplayName = "Profile selected")]
    public async Task CompanionProfilesInvalidFileReplacedOnlyByProfileSave(string change, bool isReplaced)
    {
        // Arrange
        var original = Encoding.UTF8.GetBytes("not json " + SecondName);

        await File.WriteAllBytesAsync(ProfilesPath, original, TestContext.CancellationToken);

        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        await RunUntilCancelledAsync(profiles, running => Change(running, change));

        // Assert
        var written = await File.ReadAllBytesAsync(ProfilesPath, TestContext.CancellationToken);
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());

        Assert.AreEqual(isReplaced, written.SequenceEqual(original) == false, "Only a created or selected profile should replace the file");
        Assert.AreEqual(isReplaced == false, reloaded.IsFileInvalid, "A replaced file should be valid");
    }

    /// <summary>
    /// A created profile gets a new identity and the name, is appended after <c>Live</c> in creation order, becomes active
    /// and raises <see cref="CompanionProfiles.ActiveChanged" /> (S5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void CompanionProfilesCreateSelectsAndKeepsLiveFirst()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var changes = 0;

        profiles.ActiveChanged += (_, _) => changes++;

        // Act
        var second = profiles.Create(SecondName);
        var third = profiles.Create("Third");

        // Assert
        Assert.AreSequenceEqual([CompanionProfile.Live.Id, second.Id, third.Id], profiles.Profiles.Select(profile => profile.Id).ToArray(), "Live should stay first and the new profiles follow in creation order");
        Assert.AreEqual(SecondName, second.Name, "The created profile should carry the name");
        Assert.AreNotEqual(Guid.Empty, second.Id, "The created profile should get an identity");
        Assert.AreNotEqual(second.Id, third.Id, "Each created profile should get a new identity");
        Assert.AreSame(third, profiles.Active, "The last created profile should be active");
        Assert.AreEqual(2, changes, "Each creation should raise ActiveChanged");
        Assert.AreEqual(SecondName, profiles.NameOf(second.Id), "The created profile's name should be found by its identity");
    }

    /// <summary>
    /// Selecting a profile makes it active and raises <see cref="CompanionProfiles.ActiveChanged" /> (S6).
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void CompanionProfilesSelectMakesProfileActive()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        profiles.Create(SecondName);

        var changes = 0;

        profiles.ActiveChanged += (_, _) => changes++;

        // Act
        profiles.Select(CompanionProfile.Live.Id);

        // Assert
        Assert.AreEqual(CompanionProfile.Live.Id, profiles.Active.Id, "Live should be active again");
        Assert.AreEqual(1, changes, "The selection should raise ActiveChanged");
    }

    /// <summary>
    /// An identity no profile has gives no name.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void CompanionProfilesNameOfUnknownIsNull()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        var name = profiles.NameOf(new Guid(OtherId));

        // Assert
        Assert.IsNull(name, "An unknown identity should give no name");
    }

    /// <summary>
    /// A name <see cref="CompanionProfiles.CheckNewName" /> refuses is not created: <see cref="ArgumentException" /> for the
    /// name, no new profile, the active profile kept and no <see cref="CompanionProfiles.ActiveChanged" />.
    /// </summary>
    /// <param name="name">The refused name</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("", DisplayName = "Blank")]
    [DataRow("live", DisplayName = "Exists ignoring case")]
    [DataRow("Tab\tName", DisplayName = "Control character")]
    public void CompanionProfilesCreateRefusedNameThrows(string name)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var changes = 0;

        profiles.ActiveChanged += (_, _) => changes++;

        // Act
        var exception = Assert.ThrowsExactly<ArgumentException>(() => profiles.Create(name), "A refused name should throw");

        // Assert
        Assert.AreEqual("name", exception.ParamName, "The exception should name the parameter");
        Assert.HasCount(1, profiles.Profiles, "No profile should be created");
        Assert.AreSame(CompanionProfile.Live, profiles.Active, "Live should stay active");
        Assert.AreEqual(0, changes, "A refused name should raise no ActiveChanged");
    }

    /// <summary>
    /// Selecting an identity no profile has throws <see cref="ArgumentException" /> for the identity and keeps the active
    /// profile, without <see cref="CompanionProfiles.ActiveChanged" />.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void CompanionProfilesSelectUnknownThrows()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var second = profiles.Create(SecondName);
        var changes = 0;

        profiles.ActiveChanged += (_, _) => changes++;

        // Act
        var exception = Assert.ThrowsExactly<ArgumentException>(() => profiles.Select(new Guid(OtherId)), "An unknown identity should throw");

        // Assert
        Assert.AreEqual("id", exception.ParamName, "The exception should name the parameter");
        Assert.AreSame(second, profiles.Active, "The active profile should be kept");
        Assert.AreEqual(0, changes, "An unknown identity should raise no ActiveChanged");
    }

    /// <summary>
    /// The name rule of the reader and the window: not blank, at most 64 UTF-16 code units, well-formed UTF-16 and no
    /// control character (E6).
    /// </summary>
    /// <param name="name">The name before the code unit is appended</param>
    /// <param name="appended">The code unit appended to the name, or <c>-1</c> for none</param>
    /// <param name="expected">The problem, or <c>null</c> when the name is valid</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("", -1, SettingsProblemKind.NameBlank, DisplayName = "Empty")]
    [DataRow("   ", -1, SettingsProblemKind.NameBlank, DisplayName = "Blank")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", -1, null, DisplayName = "64 characters")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", -1, SettingsProblemKind.NameTooLong, DisplayName = "65 characters")]
    [DataRow("Bell", 0x0007, SettingsProblemKind.NameInvalidCharacters, DisplayName = "U+0007")]
    [DataRow("Delete", 0x007F, SettingsProblemKind.NameInvalidCharacters, DisplayName = "U+007F")]
    [DataRow("Lone", 0xD800, SettingsProblemKind.NameInvalidCharacters, DisplayName = "Lone U+D800")]
    [DataRow("Lone", 0xDC00, SettingsProblemKind.NameInvalidCharacters, DisplayName = "Lone U+DC00")]
    [DataRow("Caesar", -1, null, DisplayName = "Plain name")]
    [DataRow("Zweité", -1, null, DisplayName = "Non-ASCII letter")]
    public void CompanionProfilesCheckNameRefusesEachInvalidName(string name, int appended, SettingsProblemKind? expected)
    {
        // Arrange
        var checkedName = appended < 0 ? name : name + (char)appended;

        // Act
        var problem = CompanionProfiles.CheckName(checkedName);

        // Assert
        Assert.AreEqual(expected, problem, "The name should get the problem of its case");
    }

    /// <summary>
    /// A surrogate pair counts as two of the 64 code units and is well-formed: 32 pairs are valid, 33 too long (E6).
    /// </summary>
    /// <param name="pairs">The number of surrogate pairs</param>
    /// <param name="expected">The problem, or <c>null</c> when the name is valid</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(32, null, DisplayName = "32 pairs")]
    [DataRow(33, SettingsProblemKind.NameTooLong, DisplayName = "33 pairs")]
    public void CompanionProfilesCheckNameCountsSurrogatePairs(int pairs, SettingsProblemKind? expected)
    {
        // Arrange
        var name = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F600), pairs));

        // Act
        var problem = CompanionProfiles.CheckName(name);

        // Assert
        Assert.HasCount(pairs * 2, name, "Each pair should be two code units");
        Assert.AreEqual(expected, problem, "The pairs should count as two code units each");
    }

    /// <summary>
    /// A new name also refuses a name equal to an existing one ignoring case, besides the name rule (E6).
    /// </summary>
    /// <param name="name">The new name</param>
    /// <param name="expected">The problem, or <c>null</c> when the name can be used</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow("Live", SettingsProblemKind.NameExists, DisplayName = "Live")]
    [DataRow("LIVE", SettingsProblemKind.NameExists, DisplayName = "Live in other case")]
    [DataRow("marker-second", SettingsProblemKind.NameExists, DisplayName = "Created name in other case")]
    [DataRow("", SettingsProblemKind.NameBlank, DisplayName = "Blank")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", SettingsProblemKind.NameTooLong, DisplayName = "65 characters")]
    [DataRow("Third", null, DisplayName = "New name")]
    public void CompanionProfilesCheckNewNameRefusesEachInvalidName(string name, SettingsProblemKind? expected)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        profiles.Create(SecondName);

        // Act
        var problem = profiles.CheckNewName(name);

        // Assert
        Assert.AreEqual(expected, problem, "The new name should get the problem of its case");
    }

    /// <summary>
    /// A new name with a control character or a lone surrogate is refused (E6).
    /// </summary>
    /// <param name="appended">The code unit appended to a valid name</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(0x0007, DisplayName = "U+0007")]
    [DataRow(0xD800, DisplayName = "Lone U+D800")]
    public void CompanionProfilesCheckNewNameRefusesInvalidCharacters(int appended)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        var problem = profiles.CheckNewName("Name" + (char)appended);

        // Assert
        Assert.AreEqual(SettingsProblemKind.NameInvalidCharacters, problem, "The name should hold characters that cannot be used");
    }

    /// <summary>
    /// With 1,000 profiles no more can be created; with 999 one more can (E6).
    /// </summary>
    /// <param name="created">The number of profiles created besides <c>Live</c></param>
    /// <param name="expected">The problem, or <c>null</c> when a profile can be created</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(CompanionProfiles.MaxProfiles - 1, SettingsProblemKind.ProfilesFull, DisplayName = "1,000 profiles")]
    [DataRow(CompanionProfiles.MaxProfiles - 2, null, DisplayName = "999 profiles")]
    public void CompanionProfilesCheckNewNameRefusesProfileBeyondBound(int created, SettingsProblemKind? expected)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        CreateProfiles(profiles, created);

        // Act
        var problem = profiles.CheckNewName("One more");

        // Assert
        Assert.HasCount(created + 1, profiles.Profiles, "The profiles should be created");
        Assert.AreEqual(expected, problem, "Only the 1,001st profile should be refused");
    }

    /// <summary>
    /// A first figure on a profile without one is linked silently (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesFirstFigureLinkedSilently()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        var notices = ObserveSessions(profiles, FigureA, FigureA);

        // Assert
        Assert.AreSequenceEqual([null, null], notices, "A first figure and its next session should give no notice");
    }

    /// <summary>
    /// A new figure on a profile that has a figure gives the notice with the active profile's name (S8).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesNewFigureOnProfileWithFigureGivesNotice()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        profiles.Create(SecondName);

        // Act
        var notices = ObserveSessions(profiles, FigureA, FigureB);

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.NewFigure, SecondName)], Describe(notices), "Only the second figure should give the notice, naming the active profile");
    }

    /// <summary>
    /// A figure last used with another profile gives the notice with that profile's name (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesFigureOfOtherProfileGivesNotice()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        ObserveSessions(profiles, FigureA);
        profiles.Create(SecondName);

        // Act
        var notice = profiles.ObserveSessionStart(FigureA);

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, "Live")], Describe([notice]), "The figure should be named as last used with Live");
    }

    /// <summary>
    /// An ignored notice links the figure to the active profile, so its next session on another profile names that one
    /// (S7, record 0041).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesIgnoredNoticeLinksToActive()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        ObserveSessions(profiles, FigureA);
        profiles.Create(SecondName);
        ObserveSessions(profiles, FigureA);
        profiles.Select(CompanionProfile.Live.Id);

        // Act
        var notice = profiles.ObserveSessionStart(FigureA);

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe([notice]), "The ignored notice should have linked the figure to the second profile");
    }

    /// <summary>
    /// Selecting a profile while a figure's session runs moves the figure's link to it (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesSwitchDuringSessionMovesLink()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var second = profiles.Create(SecondName);

        profiles.Select(CompanionProfile.Live.Id);
        profiles.ObserveSessionStart(FigureA);
        profiles.Select(second.Id);
        profiles.ObserveSessionEnd();
        profiles.Select(CompanionProfile.Live.Id);

        // Act
        var notice = profiles.ObserveSessionStart(FigureA);

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, SecondName)], Describe([notice]), "The selection during the session should have moved the link to the second profile");
    }

    /// <summary>
    /// After the session end a selection leaves the figure's link where it was (S7).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesSessionEndKeepsLink()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var second = profiles.Create(SecondName);

        profiles.Select(CompanionProfile.Live.Id);
        ObserveSessions(profiles, FigureA);
        profiles.Select(second.Id);

        // Act
        var notice = profiles.ObserveSessionStart(FigureA);

        // Assert
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, "Live")], Describe([notice]), "The link should stay with Live after the session end");
    }

    /// <summary>
    /// Beyond 1,000 figures the oldest link is dropped: the second figure is still linked to <c>Live</c>, the first is new
    /// again (G3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CompanionProfilesOldestLinkDroppedBeyondBound()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var figures = Enumerable.Range(0, CompanionProfiles.MaxFigures + 1).Select(FigureName).ToArray();

        ObserveSessions(profiles, figures);
        profiles.Create(SecondName);

        // Act
        var notices = ObserveSessions(profiles, figures[1], figures[0]);

        // Assert
        (ProfileNoticeKind, string)[] expected = [(ProfileNoticeKind.LastUsedWithOther, "Live"), (ProfileNoticeKind.NewFigure, SecondName)];

        Assert.AreSequenceEqual(expected, Describe(notices), "The second figure should still be linked to Live and the first be new to the second profile");
    }

    /// <summary>
    /// Each session start sets the pending notice to its own notice: the notice of a new figure or of a figure last used
    /// with another profile, none for a first figure and none with an unusable profiles file (S12).
    /// </summary>
    /// <param name="scenario">The scenario: first figure, new figure, other profile or invalid file</param>
    /// <param name="expected">The kind of the expected notice, or <see langword="null" /> for none</param>
    [TestMethod]
    [TestCategory("AC13")]
    [DataRow("first figure", null, DisplayName = "First figure")]
    [DataRow("new figure", ProfileNoticeKind.NewFigure, DisplayName = "New figure")]
    [DataRow("other profile", ProfileNoticeKind.LastUsedWithOther, DisplayName = "Figure of another profile")]
    [DataRow("invalid file", null, DisplayName = "Invalid profiles file")]
    public void CompanionProfilesPendingNoticeSetBySessionStart(string scenario, ProfileNoticeKind? expected)
    {
        // Arrange
        var (profiles, figure) = PrepareSessionStart(scenario);

        // Act
        var notice = profiles.ObserveSessionStart(figure);

        // Assert
        Assert.AreEqual(expected, notice?.Kind, $"The session start should give its notice: {scenario}");
        Assert.AreSame(notice, profiles.PendingNotice, $"The pending notice should be the notice of the session start: {scenario}");
    }

    /// <summary>
    /// The next session start replaces the pending notice: with its own notice when it gives one, with none when it gives
    /// none (S12).
    /// </summary>
    /// <param name="figure">The figure of the next session start</param>
    /// <param name="hasNotice">Whether the next session start gives a notice</param>
    [TestMethod]
    [TestCategory("AC13")]
    [DataRow("MARKER-Figure-C", true, DisplayName = "Next start with a notice")]
    [DataRow(FigureB, false, DisplayName = "Next start without a notice")]
    public void CompanionProfilesPendingNoticeReplacedByNextSessionStart(string figure, bool hasNotice)
    {
        // Arrange
        var (profiles, first) = LoadWithPendingNotice();

        // Act
        var next = profiles.ObserveSessionStart(figure);

        // Assert
        Assert.AreEqual(hasNotice, next is not null, $"The next session start should give a notice only for a new figure: {figure}");
        Assert.AreSame(next, profiles.PendingNotice, $"The pending notice should be the notice of the next session start: {figure}");
        Assert.AreNotSame(first, profiles.PendingNotice, $"The first notice should no longer be pending: {figure}");
    }

    /// <summary>
    /// The end of the session clears the pending notice (S12).
    /// </summary>
    [TestMethod]
    [TestCategory("AC13")]
    public void CompanionProfilesPendingNoticeClearedBySessionEnd()
    {
        // Arrange
        var (profiles, first) = LoadWithPendingNotice();

        // Act
        profiles.ObserveSessionEnd();

        // Assert
        Assert.IsNotNull(first, "The session start should have given a notice");
        Assert.IsNull(profiles.PendingNotice, "The session end should clear the pending notice");
    }

    /// <summary>
    /// Selecting another profile or creating one clears the pending notice, also while the session still runs (S12).
    /// </summary>
    /// <param name="change">The change: select or create</param>
    [TestMethod]
    [TestCategory("AC13")]
    [DataRow("select", DisplayName = "Select")]
    [DataRow("create", DisplayName = "Create")]
    public void CompanionProfilesPendingNoticeClearedBySelectAndCreate(string change)
    {
        // Arrange
        var (profiles, first) = LoadWithPendingNotice();

        // Act
        Change(profiles, change);

        // Assert
        Assert.IsNotNull(first, "The session start should have given a notice");
        Assert.IsNull(profiles.PendingNotice, $"The change should clear the pending notice: {change}");
    }

    /// <summary>
    /// Figures are observed only in a run that sends live data: not in a replay, mode Off, an invalid setting or an
    /// invalid command line (S7).
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="kind">The kind of the setting</param>
    /// <param name="expected">Whether figures are observed</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Forward, true, DisplayName = "Forward mode")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Local, true, DisplayName = "Local mode")]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Forward, true, DisplayName = "Forward mode with --record")]
    [DataRow(CompanionMode.Record, LocalServerSettingKind.Local, true, DisplayName = "Local mode with --record")]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Forward, false, DisplayName = "Replay in forward mode")]
    [DataRow(CompanionMode.Replay, LocalServerSettingKind.Local, false, DisplayName = "Replay in local mode")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Off, false, DisplayName = "Mode Off")]
    [DataRow(CompanionMode.Normal, LocalServerSettingKind.Invalid, false, DisplayName = "Invalid setting")]
    [DataRow(CompanionMode.Invalid, LocalServerSettingKind.Forward, false, DisplayName = "Invalid command line")]
    public void CompanionProfilesIsObservedOnlyWhenSendingLive(CompanionMode mode, LocalServerSettingKind kind, bool expected)
    {
        // Arrange
        var setting = new LocalServerSetting
                      {
                          Kind = kind
                      };

        // Act
        var isObserved = CompanionProfiles.IsObserved(mode, setting);

        // Assert
        Assert.AreEqual(expected, isObserved, "Figures should be observed exactly when live data is sent");
    }

    /// <summary>
    /// The writer writes the file of the plan's format: version, active profile, profiles in order, figures by the lowercase
    /// hexadecimal SHA-256 of the name's UTF-8 bytes, not indented, non-ASCII characters escaped (G3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task CompanionProfilesSavedFileHasPlannedFormat()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        CompanionProfile? second = null;

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         second = running.Create("Zweité");
                                         ObserveSessions(running, FigureA);
                                     });

        // Assert
        var expected = PlannedFile.Replace("{created}", second!.Id.ToString("D"), StringComparison.Ordinal).Replace("{key}", FigureKey(FigureA), StringComparison.Ordinal);

        Assert.Contains("\\u00E9", expected, "The expectation should hold the escape");
        Assert.AreEqual(expected, await File.ReadAllTextAsync(ProfilesPath, TestContext.CancellationToken), "The file should be written in the plan's format");
        Assert.IsFalse(File.Exists(ProfilesPath + ".tmp"), "No temporary file should be left");
    }

    /// <summary>
    /// The file holds the figure only as the lowercase hexadecimal SHA-256 of its UTF-8 bytes, never its name (N2).
    /// </summary>
    /// <param name="figure">The name of the figure</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    [DataRow(FigureA, DisplayName = "ASCII name")]
    [DataRow("MARKER-Fīgūre Cæsar", DisplayName = "Non-ASCII name")]
    public async Task CompanionProfilesFileHoldsNoFigureName(string figure)
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        await RunUntilCancelledAsync(profiles, running => ObserveSessions(running, figure));

        // Assert
        var text = await File.ReadAllTextAsync(ProfilesPath, TestContext.CancellationToken);

        Assert.Contains($"\"figure\":\"{FigureKey(figure)}\"", text, "The file should hold the hash of the figure's UTF-8 bytes");
        Assert.DoesNotContain("MARKER", text, "The file should not hold the figure's name");
        Assert.IsEmpty(_logger.Entries.Where(entry => entry.Message.Contains("MARKER", StringComparison.Ordinal) || entry.Message.Contains(FigureKey(figure), StringComparison.Ordinal)).ToList(), "No log line should hold the figure or its key");
    }

    /// <summary>
    /// When cancelled the writer writes the latest state once more and completes; no temporary file is left (N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task CompanionProfilesRunAsyncWritesLatestStateOnCancel()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        CompanionProfile? second = null;

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         second = running.Create(SecondName);
                                         running.Create("Third");
                                         running.Select(second.Id);
                                     });

        // Assert
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());

        Assert.IsFalse(reloaded.IsFileInvalid, "The written file should be valid");
        Assert.AreSequenceEqual(["Live", SecondName, "Third"], reloaded.Profiles.Select(profile => profile.Name).ToArray(), "Every created profile should be written in order");
        Assert.AreEqual(second!.Id, reloaded.Active.Id, "The latest selection should be written");
        Assert.IsFalse(File.Exists(ProfilesPath + ".tmp"), "No temporary file should be left");
    }

    /// <summary>
    /// The largest file the writer can write, 1,000 profiles of 32 escaped surrogate pairs and 1,000 figures, is
    /// 566,706 bytes, within the reader's bound, and reads back valid (G3, Security B1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task CompanionProfilesWorstCaseFileReadsBackValid()
    {
        // Arrange
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);
        var names = Enumerable.Range(1, CompanionProfiles.MaxProfiles - 1).Select(WorstCaseName).ToArray();
        var figures = Enumerable.Range(0, CompanionProfiles.MaxFigures).Select(FigureName).ToArray();

        // Act
        await RunUntilCancelledAsync(profiles,
                                     running =>
                                     {
                                         CreateProfiles(running, names);
                                         ObserveSessions(running, figures);
                                     });

        // Assert
        var reloaded = CompanionProfiles.Load(ProfilesPath, new RecordingLogger());
        var notice = ObserveOnLive(reloaded, figures[0]);

        // 88 bytes of frame; Live 7 + 36 + 10 + 4 + 2 = 59 bytes; 999 profiles of 7 + 36 + 10 + 32 * 12 + 2 = 439 bytes and a
        // comma each; 1,000 figures of 11 + 64 + 13 + 36 + 2 = 126 bytes and 999 commas: 88 + 59 + 999 * 440 + 126,999 = 566,706,
        // below the bound of 1,048,576.
        Assert.AreEqual(566706L, new FileInfo(ProfilesPath).Length, "The worst-case file should have the computed length");
        Assert.IsFalse(reloaded.IsFileInvalid, "The worst-case file should read back valid");
        Assert.AreSequenceEqual(["Live", .. names], reloaded.Profiles.Select(profile => profile.Name).ToArray(), "Every name should read back as written");
        Assert.AreSequenceEqual([(ProfileNoticeKind.LastUsedWithOther, names[^1])], Describe([notice]), "The figures should read back linked to the last profile");
    }

    /// <summary>
    /// A write that fails logs the warning with the exception's type, keeps the state in memory and ends the writer
    /// without an exception (N5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC9")]
    public async Task CompanionProfilesRunAsyncWriteFailureLogsAndKeepsState()
    {
        // Arrange
        var blocker = Path.Combine(_root, "blocker");

        await File.WriteAllTextAsync(blocker, "a file where the folder should be", TestContext.CancellationToken);

        var profiles = CompanionProfiles.Load(Path.Combine(blocker, "profiles.json"), _logger);
        CompanionProfile? second = null;

        // Act
        await RunUntilCancelledAsync(profiles, running => second = running.Create(SecondName));

        // Assert
        var warnings = _logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).ToList();

        Assert.IsNotEmpty(warnings, "The failed write should be logged");
        Assert.IsEmpty(warnings.Where(warning => warning.StartsWith(NotSavedLinePrefix, StringComparison.Ordinal) == false).ToList(), "Every warning should be the failed write");
        Assert.IsEmpty(warnings.Where(warning => warning.Contains(SecondName, StringComparison.Ordinal) || warning.Contains(_root, StringComparison.Ordinal)).ToList(), "The warning should name no profile and no path");
        Assert.AreSame(second, profiles.Active, "The state should be kept in memory");
    }

    /// <summary>
    /// A write whose temporary file is written but cannot replace the file logs the failed write and deletes the
    /// temporary file; the path is left as it was (N5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task CompanionProfilesRunAsyncReplaceFailureDeletesTemporaryFile()
    {
        // Arrange
        Directory.CreateDirectory(ProfilesPath);

        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        // Act
        await RunUntilCancelledAsync(profiles, running => running.Create(SecondName));

        // Assert
        var warnings = _logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).ToList();

        Assert.Contains(warning => warning.StartsWith(NotSavedLinePrefix, StringComparison.Ordinal), warnings, "The failed replace should be logged");
        Assert.IsFalse(File.Exists(ProfilesPath + ".tmp"), "The temporary file should be deleted");
        Assert.IsTrue(Directory.Exists(ProfilesPath), "The path should be left as it was");
    }

    /// <summary>
    /// Replaces the placeholders of a test text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The text with the identities, the figure key and the long texts filled in</returns>
    private static string Expand(string text)
    {
        return text.Replace("{liveUpper}", LiveId.ToUpperInvariant(), StringComparison.Ordinal)
                   .Replace("{live}", LiveId, StringComparison.Ordinal)
                   .Replace("{secondUpper}", SecondId.ToUpperInvariant(), StringComparison.Ordinal)
                   .Replace("{secondN}", SecondId.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal)
                   .Replace("{second}", SecondId, StringComparison.Ordinal)
                   .Replace("{other}", OtherId, StringComparison.Ordinal)
                   .Replace("{keyUpper}", FigureKey(FigureA).ToUpperInvariant(), StringComparison.Ordinal)
                   .Replace("{key}", FigureKey(FigureA), StringComparison.Ordinal)
                   .Replace("{nonHex}", new string('g', 64), StringComparison.Ordinal)
                   .Replace("{long}", new string('A', CompanionProfiles.MaxNameLength + 1), StringComparison.Ordinal);
    }

    /// <summary>
    /// Replaces a part of the valid file and fills in the placeholders.
    /// </summary>
    /// <param name="find">The part to replace, which the valid file must hold</param>
    /// <param name="replacement">The replacement</param>
    /// <returns>The changed text</returns>
    private static string Mutate(string find, string replacement)
    {
        Assert.Contains(find, ValidFile, "The part to replace should be in the valid file");

        var text = Expand(ValidFile.Replace(find, replacement, StringComparison.Ordinal));

        Assert.AreNotEqual(Expand(ValidFile), text, "The case should change the valid file");

        return text;
    }

    /// <summary>
    /// Gives the key of a figure: the lowercase hexadecimal SHA-256 of its name's UTF-8 bytes.
    /// </summary>
    /// <param name="figure">The name of the figure</param>
    /// <returns>The key</returns>
    private static string FigureKey(string figure)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(figure)));
    }

    /// <summary>
    /// Gives the name of a numbered figure.
    /// </summary>
    /// <param name="index">The number</param>
    /// <returns>The name</returns>
    private static string FigureName(int index)
    {
        return "Figure " + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gives a distinct name of 32 surrogate pairs, 64 code units, each escaped as two <c>\uXXXX</c> by the writer.
    /// </summary>
    /// <param name="index">The number of the name, from 1 to 999</param>
    /// <returns>The name</returns>
    private static string WorstCaseName(int index)
    {
        return char.ConvertFromUtf32(0x10000 + index) + string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F600), 31));
    }

    /// <summary>
    /// Builds a valid-looking file of a number of profiles and figures, all figures linked to <c>Live</c>.
    /// </summary>
    /// <param name="profileCount">The number of profiles, <c>Live</c> included</param>
    /// <param name="figureCount">The number of figures</param>
    /// <returns>The text</returns>
    private static string BuildFile(int profileCount, int figureCount)
    {
        var profiles = Enumerable.Range(1, profileCount - 1).Select(index => $$"""{"id":"{{new Guid(index, 0, 0, new byte[8]):D}}","name":"P{{index}}"}""");
        var figures = Enumerable.Range(0, figureCount).Select(index => $$"""{"figure":"{{FigureKey(FigureName(index))}}","profile":"{{LiveId}}"}""");

        return $$"""{"version":1,"active":"{{LiveId}}","profiles":[{"id":"{{LiveId}}","name":"Live"},{{string.Join(',', profiles)}}],"figures":[{{string.Join(',', figures)}}]}""";
    }

    /// <summary>
    /// Creates numbered profiles.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="count">The number of profiles to create</param>
    private static void CreateProfiles(CompanionProfiles profiles, int count)
    {
        CreateProfiles(profiles, [.. Enumerable.Range(1, count).Select(index => "Profile " + index.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
    }

    /// <summary>
    /// Creates profiles of the given names.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="names">The names</param>
    private static void CreateProfiles(CompanionProfiles profiles, string[] names)
    {
        foreach (var name in names)
        {
            profiles.Create(name);
        }
    }

    /// <summary>
    /// Observes one session of each figure, start and end, and returns the notices.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="figures">The figures in order</param>
    /// <returns>The notice of each session start</returns>
    private static ProfileNotice?[] ObserveSessions(CompanionProfiles profiles, params string[] figures)
    {
        var notices = new ProfileNotice?[figures.Length];

        for (var index = 0; index < figures.Length; index++)
        {
            notices[index] = profiles.ObserveSessionStart(figures[index]);
            profiles.ObserveSessionEnd();
        }

        return notices;
    }

    /// <summary>
    /// Selects <c>Live</c> and observes the session start of a figure.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="figure">The figure</param>
    /// <returns>The notice</returns>
    private static ProfileNotice? ObserveOnLive(CompanionProfiles profiles, string figure)
    {
        profiles.Select(CompanionProfile.Live.Id);

        return profiles.ObserveSessionStart(figure);
    }

    /// <summary>
    /// Gives the kind and profile name of each notice, leaving out sessions without one.
    /// </summary>
    /// <param name="notices">The notices</param>
    /// <returns>The kinds and names</returns>
    private static (ProfileNoticeKind Kind, string ProfileName)[] Describe(IEnumerable<ProfileNotice?> notices)
    {
        return [.. notices.OfType<ProfileNotice>().Select(notice => (notice.Kind, notice.ProfileName))];
    }

    /// <summary>
    /// Makes a change while the writer runs: observe figures, create a profile or select <c>Live</c>.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="change">The change</param>
    private static void Change(CompanionProfiles profiles, string change)
    {
        switch (change)
        {
            case "create":
                {
                    profiles.Create(SecondName);
                }

                break;

            case "select":
                {
                    profiles.Select(CompanionProfile.Live.Id);
                }

                break;

            default:
                {
                    ObserveSessions(profiles, FigureA, FigureB);
                }

                break;
        }
    }

    /// <summary>
    /// Writes a test text to the profiles file, the markers as the bytes they stand for.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The path</returns>
    private string WriteFile(string text)
    {
        var bytes = new List<byte>();
        var parts = text.Replace(ByteOrderMarkMarker, char.ConvertFromUtf32(0xFEFF), StringComparison.Ordinal).Split(InvalidByteMarker);

        bytes.AddRange(Encoding.UTF8.GetBytes(parts[0]));

        foreach (var part in parts.Skip(1))
        {
            bytes.Add(0xFF);
            bytes.AddRange(Encoding.UTF8.GetBytes(part));
        }

        File.WriteAllBytes(ProfilesPath, [.. bytes]);

        return ProfilesPath;
    }

    /// <summary>
    /// Loads the profiles and observes two sessions on <c>Live</c>: figure A, which the test files link to the second
    /// profile, and figure B.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The profiles and the notices of the two session starts</returns>
    private (CompanionProfiles Profiles, ProfileNotice?[] Notices) LoadAndObserveTwoFigures(string path)
    {
        var profiles = CompanionProfiles.Load(path, _logger);

        return (profiles, ObserveSessions(profiles, FigureA, FigureB));
    }

    /// <summary>
    /// Loads the profiles, selects <c>Live</c> and observes the session start of a figure.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="figure">The figure</param>
    /// <returns>The profiles and the notice</returns>
    private (CompanionProfiles Profiles, ProfileNotice?[] Notices) LoadAndObserveOnLive(string path, string figure)
    {
        var profiles = CompanionProfiles.Load(path, _logger);

        return (profiles, [ObserveOnLive(profiles, figure)]);
    }

    /// <summary>
    /// Loads the profiles of a scenario and gives the figure of its next session start.
    /// </summary>
    /// <param name="scenario">The scenario: first figure, new figure, other profile or invalid file</param>
    /// <returns>The profiles and the figure</returns>
    private (CompanionProfiles Profiles, string Figure) PrepareSessionStart(string scenario)
    {
        if (scenario == "invalid file")
        {
            return (CompanionProfiles.Load(WriteFile("not a profiles file"), _logger), FigureA);
        }

        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        if (scenario == "first figure")
        {
            return (profiles, FigureA);
        }

        ObserveSessions(profiles, FigureA);

        if (scenario == "other profile")
        {
            profiles.Create(SecondName);

            return (profiles, FigureA);
        }

        return (profiles, FigureB);
    }

    /// <summary>
    /// Loads the profiles, creates and selects the profile <c>Other</c>, observes a session of figure A on it and the start
    /// of a session of figure B, which gives the notice of a new figure.
    /// </summary>
    /// <returns>The profiles and the notice of the session start of figure B</returns>
    private (CompanionProfiles Profiles, ProfileNotice? Notice) LoadWithPendingNotice()
    {
        var profiles = CompanionProfiles.Load(ProfilesPath, _logger);

        profiles.Create("Other");
        ObserveSessions(profiles, FigureA);

        return (profiles, profiles.ObserveSessionStart(FigureB));
    }

    /// <summary>
    /// Asserts that the profiles failed closed: <c>Live</c> alone and active, the file invalid, no notice, and one warning
    /// that quotes no value of the file.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="notices">The notices of the observed sessions</param>
    private void AssertFailedClosed(CompanionProfiles profiles, ProfileNotice?[] notices)
    {
        var lines = _logger.Entries.Select(entry => (entry.Level, entry.Message)).ToList();
        string[] values = [SecondName, LiveId, SecondId, FigureKey(FigureA), "MARKER"];

        Assert.IsTrue(profiles.IsFileInvalid, "The file should be invalid");
        Assert.AreSequenceEqual([CompanionProfile.Live.Id], profiles.Profiles.Select(profile => profile.Id).ToArray(), "Live should be the one profile");
        Assert.AreEqual(CompanionProfile.Live.Id, profiles.Active.Id, "Live should be active");
        Assert.AreSequenceEqual(new ProfileNotice?[] { null, null }, notices, "No figure should be linked, so no session gives a notice");
        Assert.HasCount(1, lines, "One line should be logged");
        Assert.AreEqual(LogLevel.Warning, lines[0].Level, "The line should be a warning");
        Assert.StartsWith(InvalidLinePrefix, lines[0].Message, "The line should name the invalid profiles file");
        Assert.IsEmpty(values.Where(value => lines[0].Message.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList(), "The line should quote no value of the file");
    }

    /// <summary>
    /// Starts the writer, makes a change, cancels the writer and waits until it completed.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="change">The change</param>
    /// <returns>A task that completes when the writer completed</returns>
    private async Task RunUntilCancelledAsync(CompanionProfiles profiles, Action<CompanionProfiles> change)
    {
        using var stop = new CancellationTokenSource();

        var run = profiles.RunAsync(stop.Token);

        change(profiles);

        await stop.CancelAsync();

        try
        {
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The writer may end with the cancellation.
        }
    }

    #endregion // Methods
}