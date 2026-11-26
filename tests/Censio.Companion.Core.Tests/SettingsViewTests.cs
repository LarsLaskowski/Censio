namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="SettingsView" />.
/// </summary>
[TestClass]
public sealed class SettingsViewTests
{
    #region Constants

    /// <summary>
    /// The identity of <c>Live</c>.
    /// </summary>
    private const string LiveId = "b6a1f3e2-7c4d-4e8a-9f21-5d3c0a7e1b94";

    /// <summary>
    /// The identity of the active profile.
    /// </summary>
    private const string ActiveId = "0f8c2d3e-1111-4a2b-8c3d-00000000a101";

    /// <summary>
    /// The identity of another listed profile.
    /// </summary>
    private const string OtherId = "0f8c2d3e-1111-4a2b-8c3d-00000000a102";

    /// <summary>
    /// An identity the list does not hold.
    /// </summary>
    private const string UnlistedId = "0f8c2d3e-1111-4a2b-8c3d-00000000a103";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// A field is enabled in the modes that use it (the port with Local, address and token with Forward, the buffer limits
    /// with Local and Forward, the rest always), and a field named in the problems in every mode, so a file problem of
    /// another mode can be edited away (G5, Challenge 5).
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="mode">The mode</param>
    /// <param name="hasProblem">Whether the view names a problem of the field</param>
    /// <param name="expected">Whether the field should be enabled</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(SettingsField.LocalPort, CompanionSettingsMode.Local, false, true, DisplayName = "Port with Local")]
    [DataRow(SettingsField.LocalPort, CompanionSettingsMode.Off, false, false, DisplayName = "Port with Off")]
    [DataRow(SettingsField.LocalPort, CompanionSettingsMode.Forward, false, false, DisplayName = "Port with Forward")]
    [DataRow(SettingsField.LocalPort, CompanionSettingsMode.Forward, true, true, DisplayName = "Port with a problem with Forward")]
    [DataRow(SettingsField.ServerAddress, CompanionSettingsMode.Forward, false, true, DisplayName = "Address with Forward")]
    [DataRow(SettingsField.ServerAddress, CompanionSettingsMode.Local, false, false, DisplayName = "Address with Local")]
    [DataRow(SettingsField.ServerAddress, CompanionSettingsMode.Local, true, true, DisplayName = "Address with a problem with Local")]
    [DataRow(SettingsField.ServerToken, CompanionSettingsMode.Forward, false, true, DisplayName = "Token with Forward")]
    [DataRow(SettingsField.ServerToken, CompanionSettingsMode.Local, false, false, DisplayName = "Token with Local")]
    [DataRow(SettingsField.ServerToken, CompanionSettingsMode.Local, true, true, DisplayName = "Token with a problem with Local")]
    [DataRow(SettingsField.ServerToken, CompanionSettingsMode.Off, true, true, DisplayName = "Token with a problem with Off")]
    [DataRow(SettingsField.BufferHours, CompanionSettingsMode.Local, false, true, DisplayName = "Hours with Local")]
    [DataRow(SettingsField.BufferHours, CompanionSettingsMode.Off, false, false, DisplayName = "Hours with Off")]
    [DataRow(SettingsField.BufferHours, CompanionSettingsMode.Off, true, true, DisplayName = "Hours with a problem with Off")]
    [DataRow(SettingsField.BufferMegabytes, CompanionSettingsMode.Forward, false, true, DisplayName = "Megabytes with Forward")]
    [DataRow(SettingsField.BufferMegabytes, CompanionSettingsMode.Off, false, false, DisplayName = "Megabytes with Off")]
    [DataRow(SettingsField.BufferMegabytes, CompanionSettingsMode.Off, true, true, DisplayName = "Megabytes with a problem with Off")]
    [DataRow(SettingsField.Mode, CompanionSettingsMode.Off, false, true, DisplayName = "Mode with Off")]
    [DataRow(SettingsField.GameFolder, CompanionSettingsMode.Off, false, true, DisplayName = "Game folder with Off")]
    [DataRow(SettingsField.Language, CompanionSettingsMode.Off, false, true, DisplayName = "Language with Off")]
    [DataRow(SettingsField.ProfileName, CompanionSettingsMode.Off, false, true, DisplayName = "Profile name with Off")]
    public void SettingsViewIsFieldEnabledKeepsFileProblemFieldEnabledInEveryMode(SettingsField field, CompanionSettingsMode mode, bool hasProblem, bool expected)
    {
        // Arrange
        var view = CreateView(CreateProblems(field, hasProblem));

        // Act
        var isEnabled = view.IsFieldEnabled(field, mode);

        // Assert
        Assert.AreEqual(expected, isEnabled, $"{field} should be enabled in {mode} only when the mode uses it or a problem names it");
    }

    /// <summary>
    /// The text of a view is the name of its type alone and never holds the token or a profile name (N5).
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void SettingsViewToStringHoldsNoTokenOrName()
    {
        // Arrange
        const string token = "MARKER-Token-0123456789abcdefghijKLMNOP";
        const string profileName = "MARKER-Profile";
        var profile = new CompanionProfile
                      {
                          Id = new Guid("0f8c2d3e-1111-4a2b-8c3d-00000000a002"),
                          Name = profileName
                      };
        var view = new SettingsView
                   {
                       Texts = CompanionTexts.English,
                       Values = new CompanionSettingsValues
                                {
                                    Mode = CompanionSettingsMode.Forward,
                                    LocalPort = "5000",
                                    ServerAddress = "ws://nas:8080/ingest",
                                    ServerToken = token,
                                    BufferHours = "24",
                                    BufferMegabytes = "100",
                                    GameFolder = string.Empty
                                },
                       Problems = [],
                       ProfileList = [CompanionProfile.Live, profile],
                       ActiveProfile = profile.Id,
                       Warnings = []
                   };

        // Act
        var text = view.ToString() ?? string.Empty;

        // Assert
        Assert.AreEqual(typeof(SettingsView).FullName, text, "The text of the view should be the name of its type alone");
        Assert.DoesNotContain("MARKER", text, "The text of the view should hold neither the token nor a profile name");
    }

    /// <summary>
    /// <c>Delete profile…</c> is refused for <c>Live</c>, for the active profile and for an identity the list does not hold,
    /// and allowed for another listed profile (E1).
    /// </summary>
    /// <param name="selected">The identity selected in the drop-down</param>
    /// <param name="expected">Whether the profile can be deleted</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(LiveId, false, DisplayName = "Live")]
    [DataRow(ActiveId, false, DisplayName = "Active profile")]
    [DataRow(OtherId, true, DisplayName = "Other listed profile")]
    [DataRow(UnlistedId, false, DisplayName = "Identity not listed")]
    public void SettingsViewCanDeleteRefusesLiveAndActive(string selected, bool expected)
    {
        // Arrange
        var view = CreateProfilesView([CompanionProfile.Live, CreateProfile(ActiveId, "Active"), CreateProfile(OtherId, "Other")], new Guid(ActiveId));

        // Act
        var canDelete = view.CanDelete(new Guid(selected));

        // Assert
        Assert.AreEqual(expected, canDelete, $"Deleting {selected} should be allowed only for a listed profile that is neither Live nor active");
    }

    /// <summary>
    /// Creates a profile.
    /// </summary>
    /// <param name="id">The identity in the form D</param>
    /// <param name="name">The name</param>
    /// <returns>The profile</returns>
    private static CompanionProfile CreateProfile(string id, string name)
    {
        return new CompanionProfile
               {
                   Id = new Guid(id),
                   Name = name
               };
    }

    /// <summary>
    /// Creates a view of the default values with the given profiles.
    /// </summary>
    /// <param name="profiles">The profiles</param>
    /// <param name="active">The identity of the active profile</param>
    /// <returns>The view</returns>
    private static SettingsView CreateProfilesView(IReadOnlyList<CompanionProfile> profiles, Guid active)
    {
        var view = CreateView([]);

        return new SettingsView
               {
                   Texts = view.Texts,
                   Values = view.Values,
                   Problems = view.Problems,
                   ProfileList = profiles,
                   ActiveProfile = active,
                   Warnings = view.Warnings
               };
    }

    /// <summary>
    /// Creates the problems of a view: one of the field, or none.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="hasProblem">Whether the field has a problem</param>
    /// <returns>The problems</returns>
    private static List<SettingsProblem> CreateProblems(SettingsField field, bool hasProblem)
    {
        var problems = new List<SettingsProblem>();

        if (hasProblem)
        {
            problems.Add(new SettingsProblem
                         {
                             Field = field,
                             Kind = SettingsProblemKind.WrittenTwice
                         });
        }

        return problems;
    }

    /// <summary>
    /// Creates a view of the default values with the given problems.
    /// </summary>
    /// <param name="problems">The problems</param>
    /// <returns>The view</returns>
    private static SettingsView CreateView(IReadOnlyList<SettingsProblem> problems)
    {
        return new SettingsView
               {
                   Texts = CompanionTexts.English,
                   Values = new CompanionSettingsValues
                            {
                                Mode = CompanionSettingsMode.Off,
                                LocalPort = "5000",
                                ServerAddress = string.Empty,
                                ServerToken = string.Empty,
                                BufferHours = "24",
                                BufferMegabytes = "100",
                                GameFolder = string.Empty
                            },
                   Problems = problems,
                   ProfileList = [CompanionProfile.Live],
                   ActiveProfile = CompanionProfile.Live.Id,
                   Warnings = []
               };
    }

    #endregion // Methods
}