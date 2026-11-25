namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="SettingsView" />.
/// </summary>
[TestClass]
public sealed class SettingsViewTests
{
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