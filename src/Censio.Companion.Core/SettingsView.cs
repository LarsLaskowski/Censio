namespace Censio.Companion.Core;

/// <summary>
/// What the settings window shows.
/// </summary>
public sealed class SettingsView
{
    #region Properties

    /// <summary>
    /// Gets the texts.
    /// </summary>
    public required CompanionTexts Texts { get; init; }

    /// <summary>
    /// Gets the values of the fields.
    /// </summary>
    public required CompanionSettingsValues Values { get; init; }

    /// <summary>
    /// Gets the problem of the settings file as a whole, <c>null</c> if none.
    /// </summary>
    public string? FileProblem { get; init; }

    /// <summary>
    /// Gets the problems of the fields.
    /// </summary>
    public required IReadOnlyList<SettingsProblem> Problems { get; init; }

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public required IReadOnlyList<CompanionProfile> ProfileList { get; init; }

    /// <summary>
    /// Gets the id of the active profile.
    /// </summary>
    public required Guid ActiveProfile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the profiles file is invalid.
    /// </summary>
    public bool IsProfilesFileInvalid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Companion starts with Windows.
    /// </summary>
    public bool IsAutostartEnabled { get; init; }

    /// <summary>
    /// Gets the warnings.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Gets the number of deleted profiles whose history the server has not deleted yet.
    /// </summary>
    public int WaitingDeletions { get; init; }

    /// <summary>
    /// Gets a value indicating whether as many deletions wait as the Companion keeps.
    /// </summary>
    public bool IsDeletionQueueFull { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets a value indicating whether a field is enabled in a mode.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="mode">The mode</param>
    /// <returns><c>true</c> if the field is enabled</returns>
    public bool IsFieldEnabled(SettingsField field, CompanionSettingsMode mode)
    {
        if (Problems.Any(problem => problem.Field == field))
        {
            return true;
        }

        return field switch
               {
                   SettingsField.LocalPort => mode == CompanionSettingsMode.Local,
                   SettingsField.ServerAddress or SettingsField.ServerToken => mode == CompanionSettingsMode.Forward,
                   SettingsField.BufferHours or SettingsField.BufferMegabytes => mode is CompanionSettingsMode.Local or CompanionSettingsMode.Forward,
                   _ => true
               };
    }

#pragma warning disable RH2003, S1172, S2325
    // Skeleton: removed with the implementation
    /// <summary>
    /// Gets a value indicating whether a profile can be deleted.
    /// </summary>
    /// <param name="selected">The identity of the selected profile</param>
    /// <returns><c>true</c> if the profile can be deleted</returns>
    public bool CanDelete(Guid selected)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S1172, S2325

    #endregion // Methods
}