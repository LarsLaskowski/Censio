namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="CompanionSettingsValidator" />.
/// </summary>
[TestClass]
public sealed class CompanionSettingsValidatorTests
{
    #region Constants

    /// <summary>
    /// The marker in a test text that stands for a token of 1024 characters, the longest valid length.
    /// </summary>
    private const string LongestTokenMarker = "<token 1024>";

    /// <summary>
    /// The marker in a test text that stands for a token of 1025 characters, one above the longest valid length.
    /// </summary>
    private const string TooLongTokenMarker = "<token 1025>";

    /// <summary>
    /// A folder of the fake file system that exists and is an installation.
    /// </summary>
    private const string InstallationFolder = "/games/anno117";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Each invalid value, of the selected mode or another, gets its own problem and no other (E1–E6).
    /// </summary>
    /// <param name="mode">The selected mode</param>
    /// <param name="field">The field with the invalid text</param>
    /// <param name="text">The invalid text</param>
    /// <param name="expected">The expected problem kind</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "0", SettingsProblemKind.PortRange, DisplayName = "Port 0")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "65536", SettingsProblemKind.PortRange, DisplayName = "Port 65536")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "abc", SettingsProblemKind.PortRange, DisplayName = "Port abc")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "5.0", SettingsProblemKind.PortRange, DisplayName = "Port 5.0")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "\"5\"", SettingsProblemKind.PortRange, DisplayName = "Port in quotes")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "  ", SettingsProblemKind.PortRange, DisplayName = "Port blank")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "-1", SettingsProblemKind.PortRange, DisplayName = "Port -1")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.LocalPort, "65536", SettingsProblemKind.PortRange, DisplayName = "Port of another mode")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerAddress, "", SettingsProblemKind.AddressMissing, DisplayName = "Address missing")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerAddress, "http://nas:8080/ingest", SettingsProblemKind.AddressInvalid, DisplayName = "Address http")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerAddress, "ws://user:secret@nas:8080/ingest", SettingsProblemKind.AddressInvalid, DisplayName = "Address with user")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerAddress, "ws://nas:8080/ingest#top", SettingsProblemKind.AddressInvalid, DisplayName = "Address with fragment")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.ServerAddress, "http://nas:8080/ingest", SettingsProblemKind.AddressInvalid, DisplayName = "Address of another mode")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "abcdefghijklmnopqrstuvwxyz01234", SettingsProblemKind.TokenInvalid, DisplayName = "Token of 31")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, TooLongTokenMarker, SettingsProblemKind.TokenInvalid, DisplayName = "Token of 1025")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "abcdefghijklmnop qrstuvwxyz0123456", SettingsProblemKind.TokenInvalid, DisplayName = "Token with space")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "abcdefghijklmnopqrstuvwxyz01234ä", SettingsProblemKind.TokenInvalid, DisplayName = "Token not ASCII")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.ServerToken, "short", SettingsProblemKind.TokenInvalid, DisplayName = "Token of another mode")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferHours, "0", SettingsProblemKind.HoursRange, DisplayName = "Hours 0")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferHours, "169", SettingsProblemKind.HoursRange, DisplayName = "Hours 169")]
    [DataRow(CompanionSettingsMode.Off, SettingsField.BufferHours, "abc", SettingsProblemKind.HoursRange, DisplayName = "Hours abc, mode Off")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferMegabytes, "0", SettingsProblemKind.MegabytesRange, DisplayName = "Megabytes 0")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.BufferMegabytes, "10241", SettingsProblemKind.MegabytesRange, DisplayName = "Megabytes 10241")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.GameFolder, "games/anno117", SettingsProblemKind.FolderNotFullPath, DisplayName = "Relative folder")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.GameFolder, "/missing/anno117", SettingsProblemKind.FolderMissing, DisplayName = "Missing folder")]
    [DataRow(CompanionSettingsMode.Off, SettingsField.GameFolder, "/games/other", SettingsProblemKind.FolderNotInstallation, DisplayName = "Not an installation")]
    public void CompanionSettingsValidatorRefusesEachInvalidValue(CompanionSettingsMode mode, SettingsField field, string text, SettingsProblemKind expected)
    {
        // Arrange
        var values = CreateValidValues(mode, field, Expand(text));
        var problem = new SettingsProblem
                      {
                          Field = field,
                          Kind = expected
                      };

        // Act
        var problems = CompanionSettingsValidator.Validate(values, DirectoryExists, IsInstallation);

        // Assert
        Assert.AreSequenceEqual(new[] { problem }, problems.ToArray(), $"The text {text} of {field} in mode {mode} should give its problem and no other");
    }

    /// <summary>
    /// The boundary values, a token with spaces around it, an empty token, folder or address outside forward mode are
    /// accepted.
    /// </summary>
    /// <param name="mode">The selected mode</param>
    /// <param name="field">The field with the boundary text</param>
    /// <param name="text">The boundary text</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "1", DisplayName = "Port 1")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "65535", DisplayName = "Port 65535")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "05", DisplayName = "Port 05")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.LocalPort, "", DisplayName = "No port")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferHours, "", DisplayName = "No hours")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferMegabytes, "", DisplayName = "No megabytes")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferHours, "1", DisplayName = "Hours 1")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferHours, "168", DisplayName = "Hours 168")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferMegabytes, "1", DisplayName = "Megabytes 1")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.BufferMegabytes, "10240", DisplayName = "Megabytes 10240")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "abcdefghijklmnopqrstuvwxyz012345", DisplayName = "Token of 32")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, LongestTokenMarker, DisplayName = "Token of 1024")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "!\"#$%&'()*+,-./0123456789:;<=>?@[\\]^_`{|}~", DisplayName = "Token of every sign")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, " \tabcdefghijklmnopqrstuvwxyz012345\r\n", DisplayName = "Token with spaces around")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerToken, "", DisplayName = "No token")]
    [DataRow(CompanionSettingsMode.Forward, SettingsField.ServerAddress, "wss://censio.example.org/ingest", DisplayName = "Address wss")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.ServerAddress, "", DisplayName = "No address in local mode")]
    [DataRow(CompanionSettingsMode.Off, SettingsField.ServerAddress, "", DisplayName = "No address in mode Off")]
    [DataRow(CompanionSettingsMode.Local, SettingsField.GameFolder, "", DisplayName = "No folder")]
    public void CompanionSettingsValidatorAcceptsBoundaries(CompanionSettingsMode mode, SettingsField field, string text)
    {
        // Arrange
        var values = CreateValidValues(mode, field, Expand(text));

        // Act
        var problems = CompanionSettingsValidator.Validate(values, DirectoryExists, IsInstallation);

        // Assert
        Assert.IsEmpty(problems, $"The text {text} of {field} in mode {mode} should be accepted");
    }

    /// <summary>
    /// The folder checks run only for a folder that is given.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void CompanionSettingsValidatorEmptyFolderChecksNoFolder()
    {
        // Arrange
        var values = CreateValidValues(CompanionSettingsMode.Local, SettingsField.GameFolder, string.Empty);
        var checkedFolders = new List<string>();

        // Act
        var problems = CompanionSettingsValidator.Validate(values,
                                                           folder =>
                                                           {
                                                               checkedFolders.Add(folder);

                                                               return true;
                                                           },
                                                           folder =>
                                                           {
                                                               checkedFolders.Add(folder);

                                                               return true;
                                                           });

        // Assert
        Assert.IsEmpty(problems, "An empty folder should be accepted");
        Assert.IsEmpty(checkedFolders, "An empty folder should not be checked");
    }

    /// <summary>
    /// Every invalid field is reported at once.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void CompanionSettingsValidatorReportsEveryInvalidField()
    {
        // Arrange
        var values = new CompanionSettingsValues
                     {
                         Mode = CompanionSettingsMode.Forward,
                         LocalPort = "0",
                         ServerAddress = string.Empty,
                         ServerToken = "short",
                         BufferHours = "169",
                         BufferMegabytes = "10241",
                         GameFolder = "games/anno117"
                     };
        var expected = new[]
                       {
                           CreateProblem(SettingsField.LocalPort, SettingsProblemKind.PortRange),
                           CreateProblem(SettingsField.ServerAddress, SettingsProblemKind.AddressMissing),
                           CreateProblem(SettingsField.ServerToken, SettingsProblemKind.TokenInvalid),
                           CreateProblem(SettingsField.BufferHours, SettingsProblemKind.HoursRange),
                           CreateProblem(SettingsField.BufferMegabytes, SettingsProblemKind.MegabytesRange),
                           CreateProblem(SettingsField.GameFolder, SettingsProblemKind.FolderNotFullPath)
                       };

        // Act
        var problems = CompanionSettingsValidator.Validate(values, DirectoryExists, IsInstallation);

        // Assert
        Assert.AreSequenceEqual(expected, problems.OrderBy(problem => problem.Field).ToArray(), "Every invalid field should be reported");
    }

    /// <summary>
    /// Whether a folder exists in the fake file system: every folder below <c>/games</c>.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>Whether it exists</returns>
    private static bool DirectoryExists(string folder)
    {
        return folder.StartsWith("/games/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a folder of the fake file system is an installation: only <see cref="InstallationFolder" />.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>Whether it is an installation</returns>
    private static bool IsInstallation(string folder)
    {
        return folder == InstallationFolder;
    }

    /// <summary>
    /// Replaces a token marker by a token of its length.
    /// </summary>
    /// <param name="text">The test text</param>
    /// <returns>The text</returns>
    private static string Expand(string text)
    {
        return text.Replace(LongestTokenMarker, new string('t', 1024), StringComparison.Ordinal)
                   .Replace(TooLongTokenMarker, new string('t', 1025), StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a problem.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="kind">The kind</param>
    /// <returns>The problem</returns>
    private static SettingsProblem CreateProblem(SettingsField field, SettingsProblemKind kind)
    {
        return new SettingsProblem
               {
                   Field = field,
                   Kind = kind
               };
    }

    /// <summary>
    /// Creates valid values of every field, the installation folder and a forward address and token included, with one
    /// field's text replaced.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="field">The replaced field</param>
    /// <param name="text">The text of the replaced field</param>
    /// <returns>The values</returns>
    private static CompanionSettingsValues CreateValidValues(CompanionSettingsMode mode, SettingsField field, string text)
    {
        var texts = new Dictionary<SettingsField, string>
                    {
                        [SettingsField.LocalPort] = "5117",
                        [SettingsField.ServerAddress] = "ws://nas:8080/ingest",
                        [SettingsField.ServerToken] = "ForwardToken-0123456789abcdefghijKLMNOP",
                        [SettingsField.BufferHours] = "48",
                        [SettingsField.BufferMegabytes] = "200",
                        [SettingsField.GameFolder] = InstallationFolder
                    };

        texts[field] = text;

        return new CompanionSettingsValues
               {
                   Mode = mode,
                   LocalPort = texts[SettingsField.LocalPort],
                   ServerAddress = texts[SettingsField.ServerAddress],
                   ServerToken = texts[SettingsField.ServerToken],
                   BufferHours = texts[SettingsField.BufferHours],
                   BufferMegabytes = texts[SettingsField.BufferMegabytes],
                   GameFolder = texts[SettingsField.GameFolder]
               };
    }

    #endregion // Methods
}