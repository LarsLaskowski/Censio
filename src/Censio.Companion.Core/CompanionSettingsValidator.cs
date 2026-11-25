namespace Censio.Companion.Core;

/// <summary>
/// Checks the values of the settings window.
/// </summary>
public static class CompanionSettingsValidator
{
    #region Methods

#pragma warning disable RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Checks the values.
    /// </summary>
    /// <param name="values">The values</param>
    /// <param name="directoryExists">Tells whether a folder exists</param>
    /// <param name="isInstallation">Tells whether a folder is a game installation</param>
    /// <returns>The problems, empty when the values are valid</returns>
    public static IReadOnlyList<SettingsProblem> Validate(CompanionSettingsValues values, Func<string, bool> directoryExists, Func<string, bool> isInstallation)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}