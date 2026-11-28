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

    /// <summary>
    /// Gets the number of merged profiles whose history the server has not moved yet.
    /// </summary>
    public int WaitingMerges { get; init; }

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

    /// <summary>
    /// Determines whether the window can delete a profile.
    /// </summary>
    /// <param name="selected">The identity of the selected profile</param>
    /// <returns><see langword="true" /> when the profile is listed and is neither <c>Live</c> nor the active profile</returns>
    public bool CanDelete(Guid selected)
    {
        return CompanionProfiles.IsDeletable(selected, ActiveProfile, ProfileList);
    }

    // Skeleton: removed with the implementation
#pragma warning disable S1172, S2325, RH2003
    /// <summary>
    /// Determines whether the window can merge a profile into another.
    /// </summary>
    /// <param name="selected">The identity of the selected profile</param>
    /// <returns><see langword="true" /> when the profile is listed and is neither <c>Live</c> nor the active profile</returns>
    public bool CanMerge(Guid selected)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the profiles a profile can be merged into.
    /// </summary>
    /// <param name="selected">The identity of the selected profile</param>
    /// <returns>Every other listed profile, in list order</returns>
    public IReadOnlyList<CompanionProfile> MergeTargets(Guid selected)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    /// <summary>
    /// Determines whether the window can rename a profile.
    /// </summary>
    /// <param name="selected">The identity of the selected profile</param>
    /// <returns><see langword="true" /> when the profile is listed and is not <c>Live</c></returns>
    public bool CanRename(Guid selected)
    {
        return CompanionProfiles.IsRenamable(selected, ProfileList);
    }

    /// <summary>
    /// Checks a new name of a listed profile.
    /// </summary>
    /// <param name="profile">The identity of the profile</param>
    /// <param name="name">The new name</param>
    /// <returns>The problem, or <see langword="null" /> when the profile can get the name</returns>
    public SettingsProblemKind? CheckRename(Guid profile, string name)
    {
        return CompanionProfiles.CheckRename(profile, name, ProfileList);
    }

    #endregion // Methods
}