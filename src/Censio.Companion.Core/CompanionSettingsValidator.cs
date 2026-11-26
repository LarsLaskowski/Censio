namespace Censio.Companion.Core;

/// <summary>
/// Checks the values of the settings window.
/// </summary>
public static class CompanionSettingsValidator
{
    #region Methods

    /// <summary>
    /// Checks every field of the values that holds a text.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="directoryExists">Whether a folder exists</param>
    /// <param name="isInstallation">Whether a folder is an Anno 117 installation</param>
    /// <returns>The problems, one per invalid field at most</returns>
    public static IReadOnlyList<SettingsProblem> Validate(CompanionSettingsValues values, Func<string, bool> directoryExists, Func<string, bool> isInstallation)
    {
        var problems = new List<SettingsProblem>();

        AddNumberProblem(problems, SettingsField.LocalPort, values.LocalPort, 65535, SettingsProblemKind.PortRange);

        if (values.ServerAddress.Length == 0)
        {
            if (values.Mode == CompanionSettingsMode.Forward)
            {
                problems.Add(CreateProblem(SettingsField.ServerAddress, SettingsProblemKind.AddressMissing));
            }
        }
        else if (CompanionCommandLine.TryParseServerAddress(values.ServerAddress, out _) == false)
        {
            problems.Add(CreateProblem(SettingsField.ServerAddress, SettingsProblemKind.AddressInvalid));
        }

        if (values.ServerToken.Length > 0 && ReplayTokenFile.ReadToken(values.ServerToken) is null)
        {
            problems.Add(CreateProblem(SettingsField.ServerToken, SettingsProblemKind.TokenInvalid));
        }

        AddNumberProblem(problems, SettingsField.BufferHours, values.BufferHours, LiveBufferLimits.MaxHours, SettingsProblemKind.HoursRange);
        AddNumberProblem(problems, SettingsField.BufferMegabytes, values.BufferMegabytes, LiveBufferLimits.MaxMegabytes, SettingsProblemKind.MegabytesRange);

        var folderKind = CheckFolder(values.GameFolder, directoryExists, isInstallation);

        if (folderKind is { } kind)
        {
            problems.Add(CreateProblem(SettingsField.GameFolder, kind));
        }

        return problems;
    }

    /// <summary>
    /// Adds a problem when a text that is not empty is not a whole number from 1 to a maximum.
    /// </summary>
    /// <param name="problems">The problems</param>
    /// <param name="field">The field</param>
    /// <param name="text">The text</param>
    /// <param name="maximum">The largest number</param>
    /// <param name="kind">The problem kind</param>
    private static void AddNumberProblem(List<SettingsProblem> problems, SettingsField field, string text, int maximum, SettingsProblemKind kind)
    {
        if (text.Length > 0 && CompanionSettings.TryParseWholeNumber(text, 1, maximum, out _) == false)
        {
            problems.Add(CreateProblem(field, kind));
        }
    }

    /// <summary>
    /// Checks a game folder.
    /// </summary>
    /// <param name="folder">The folder, empty for none</param>
    /// <param name="directoryExists">Whether a folder exists</param>
    /// <param name="isInstallation">Whether a folder is an Anno 117 installation</param>
    /// <returns>The problem kind, or <see langword="null" /> when the folder is valid or empty</returns>
    private static SettingsProblemKind? CheckFolder(string folder, Func<string, bool> directoryExists, Func<string, bool> isInstallation)
    {
        if (folder.Length == 0)
        {
            return null;
        }

        if (Path.IsPathFullyQualified(folder) == false)
        {
            return SettingsProblemKind.FolderNotFullPath;
        }

        if (directoryExists(folder) == false)
        {
            return SettingsProblemKind.FolderMissing;
        }

        return isInstallation(folder) ? null : SettingsProblemKind.FolderNotInstallation;
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

    #endregion // Methods
}